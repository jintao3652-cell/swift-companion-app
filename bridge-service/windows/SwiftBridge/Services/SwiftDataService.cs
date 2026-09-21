using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using SwiftBridge.DBus;
using SwiftBridge.Hubs;
using SwiftBridge.Models;

namespace SwiftBridge.Services;

/// <summary>
/// swift 数据服务：通过 swiftCore 的 DBus 接口获取全部数据（飞机/ATC/消息/连接状态），
/// 不需要任何外部 FSD API。
///
/// swift 端要求：swiftLauncher 选择「GUI 和 Core（分布式）」模式并启动 swiftCore，
/// DBus 地址形如 tcp:host=127.0.0.1,port=45000（对应 swiftCore.exe --dbus 参数）。
///
/// wire 格式要点（源自 swift 源码 mixindbus.h / dbus.h）：
/// - 值对象成员按 metaclass 顺序"拍平"写入父结构（嵌套值对象不产生子结构）
/// - 所有 C++ 枚举序列化为单元素结构 (i)
/// - 物理量序列化为单个 double（默认 SI 单位：Hz/m/rad/(m/s)）
/// - QDateTime = ((iii)(iiii)i)
/// - 容器（CSequence 等）= DBus 数组，元素为完整结构
/// </summary>
public class SwiftDataService : BackgroundService
{
    public const string IfaceNetwork = "org.swift_project.swift_core.contextnetwork";
    public const string IfaceOwn = "org.swift_project.swift_core.contextownaircraft";
    public const string PathNetwork = "/network";
    public const string PathOwn = "/ownaircraft";

    /// <summary>CTextMessageList 的元素签名（message s, timestamp x, sender, recipient, frequency d）。</summary>
    public const string TextMessageElemSig = "sxsss(i)sss(i)d";

    private readonly ILogger<SwiftDataService> _logger;
    private readonly IHubContext<SwiftHub> _hub;
    private readonly IMessageStorageService _storage;
    private readonly IConfiguration _configuration;

    private DBusClient? _dbus;

    // ---- 快照（雷达/ATC/本机）----
    private readonly ConcurrentDictionary<string, NearbyAircraftDto> _radar = new();
    private readonly ConcurrentDictionary<string, AtcListDto> _atc = new();

    private volatile bool _swiftConnected;      // swift 是否连上 VATSIM 网络
    private volatile bool _dbusReady;           // DBus 是否可达
    private string _ownCallsign = "";
    private AircraftStateDto _ownState = new();

    public SwiftDataService(ILogger<SwiftDataService> logger, IHubContext<SwiftHub> hub,
        IMessageStorageService storage, IConfiguration configuration)
    {
        _logger = logger;
        _hub = hub;
        _storage = storage;
        _configuration = configuration;
    }

    public bool Simulate { get; private set; }

    // ============ 生命周期 ============

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 模拟模式必须显式开启（Swift:Simulate=true），默认连接真实 swift DBus
        Simulate = _configuration["Swift:Simulate"] == "true";
        var address = _configuration["Swift:Address"] ?? "tcp:host=127.0.0.1,port=45000";
        var pollMs = int.TryParse(_configuration["Swift:PollMs"], out var p) ? p : 1000;

        if (Simulate)
        {
            _logger.LogWarning("SwiftDataService 运行在 SIMULATED 模式（Swift:Simulate != false）");
            RunSimulatedLoop(stoppingToken);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _dbus = new DBusClient(address, _logger);
                _dbus.SignalReceived += OnSignal;
                if (!await _dbus.ConnectAsync(stoppingToken))
                {
                    await Task.Delay(5000, stoppingToken);
                    continue;
                }

                // 连接后立即 introspect，记录真实签名（校准解码映射用）
                try
                {
                    var xml = await _dbus.IntrospectAsync(PathNetwork, stoppingToken);
                    _logger.LogInformation("swift /network introspection:\n{Xml}", xml);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Introspect /network 失败（不影响继续轮询）");
                }

                await PollLoopAsync(stoppingToken, pollMs);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SwiftDataService 主循环异常，5 秒后重连");
                await Task.Delay(5000, stoppingToken);
            }
            finally
            {
                if (_dbus != null) await _dbus.DisposeAsync();
                _dbus = null;
                _dbusReady = false;
            }
        }
    }

    // ============ 轮询循环 ============

    private async Task PollLoopAsync(CancellationToken ct, int pollMs)
    {
        var lastAircraftPoll = DateTime.MinValue;
        var lastAtcPoll = DateTime.MinValue;
        var lastCount = -1;

        while (!ct.IsCancellationRequested && _dbus is { IsConnected: true })
        {
            if (!_dbusReady)
            {
                _dbusReady = true;
                await BroadcastSwiftConnectionAsync();
            }

            // 1) 网络连接状态（每轮）
            try
            {
                var r = await _dbus.CallMethodAsync(PathNetwork, IfaceNetwork, "isConnected", null, null, ct);
                var connected = r.Decoded is { Count: > 0 } d && d[0].Bool();
                if (connected != _swiftConnected)
                {
                    _swiftConnected = connected;
                    await BroadcastSwiftConnectionAsync();
                }
            }
            catch (Exception ex) { _logger.LogDebug(ex, "isConnected 失败"); }

            // 2) 本机飞机（每轮，1s）
            try
            {
                var r = await _dbus.CallMethodAsync(PathOwn, IfaceOwn, "getOwnAircraft", null, null, ct);
                if (r.Decoded is { Count: > 0 })
                {
                    var fields = Flatten(r.Decoded[0]);
                    UpdateOwnState(fields);
                }
            }
            catch (Exception ex) { _logger.LogDebug(ex, "getOwnAircraft 失败"); }

            // 3) 范围内飞机（2s 或数量变化）
            if ((DateTime.UtcNow - lastAircraftPoll).TotalMilliseconds >= 2000)
            {
                lastAircraftPoll = DateTime.UtcNow;
                try
                {
                    var rc = await _dbus.CallMethodAsync(PathNetwork, IfaceNetwork, "getAircraftInRangeCount", null, null, ct);
                    var count = rc.Decoded is { Count: > 0 } c ? c[0].Int() : 0;
                    if (count != lastCount || _radar.IsEmpty)
                    {
                        lastCount = count;
                        var r = await _dbus.CallMethodAsync(PathNetwork, IfaceNetwork, "getAircraftInRange", null, null, ct);
                        if (r.Decoded is { Count: > 0 } arr && arr[0].Kind == 'a')
                            DecodeAircraftList(arr[0]);
                        _logger.LogInformation("雷达更新: {Count} 架飞机", _radar.Count);
                    }
                }
                catch (Exception ex) { _logger.LogDebug(ex, "getAircraftInRange 失败"); }
            }

            // 4) ATC（5s）
            if ((DateTime.UtcNow - lastAtcPoll).TotalMilliseconds >= 5000)
            {
                lastAtcPoll = DateTime.UtcNow;
                try
                {
                    var w = new DBusWire.BodyWriter();
                    w.WriteBool(true); // recalculateDistance
                    var r = await _dbus.CallMethodAsync(PathNetwork, IfaceNetwork, "getAtcStationsOnline",
                        "b", wr => wr.WriteBool(true), ct);
                    if (r.Decoded is { Count: > 0 } arr && arr[0].Kind == 'a')
                        DecodeAtcList(arr[0]);
                    _logger.LogInformation("ATC 更新: {Count} 个台站", _atc.Count);
                }
                catch (Exception ex) { _logger.LogDebug(ex, "getAtcStationsOnline 失败"); }
            }

            // 5) 本机状态广播（每轮）
            await _hub.Clients.All.SendAsync("AircraftStateUpdated", _ownState, ct);

            await Task.Delay(pollMs, ct);
        }
    }

    // ============ 信号处理 ============

    private void OnSignal(DBusWire.IncomingMessage msg)
    {
        if (msg.Interface != IfaceNetwork) return;
        try
        {
            switch (msg.Member)
            {
                case "textMessagesReceived":
                case "textMessageSent":
                    if (msg.Decoded is { Count: > 0 })
                        HandleTextMessages(msg.Decoded[0], sent: msg.Member == "textMessageSent");
                    break;
                case "connectionStatusChanged":
                    var nowConnected = msg.Decoded is { Count: > 0 } d
                        && d[0].Kind == '(' && d[0].Struct is { Count: > 0 } inner && inner[0].Int() == 1;
                    _swiftConnected = nowConnected;
                    _ = BroadcastSwiftConnectionAsync();
                    break;
                case "changedAircraftInRange":
                case "changedAircraftInRangeDigest":
                case "changedAtcStationsOnline":
                case "changedAtcStationsOnlineDigest":
                    // 触发立即重 poll：由轮询循环的时间戳判断即可，这里仅记日志
                    _logger.LogDebug("swift 信号: {Member}", msg.Member);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "处理 swift 信号 {Member} 失败", msg.Member);
        }
    }

    private void HandleTextMessages(DBusWire.DValue listVal, bool sent)
    {
        if (listVal.Kind == 'a' && listVal.Array != null)
        {
            foreach (var elem in listVal.Array) HandleOneTextMessage(elem, sent);
        }
        else if (listVal.Kind == '(')
        {
            // 单条 CTextMessage（textMessageSent）
            HandleOneTextMessage(listVal, sent);
        }
    }

    private void HandleOneTextMessage(DBusWire.DValue msg, bool sent)
    {
        // CTextMessage 拍平字段: 0 message, 1 timestampMs, 2-5 senderCallsign, 6-9 recipientCallsign, 10 freqHz
        var f = Flatten(msg);
        if (f.Count < 11) return;

        var content = f[0].Str();
        var sender = f[2].Str();
        var recipient = f[6].Str();
        var freqHz = f[10].Double();

        var dto = new MessageDto
        {
            Type = recipient.Length > 0 ? "private" : "radio",
            MessageType = recipient.Length > 0 ? "private" : "radio",
            From = sender,
            To = recipient,
            Content = content,
            Frequency = (int)Math.Round(freqHz / 1000), // kHz
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(f[1].Long()).UtcDateTime
        };
        dto.MentionedUs = !string.IsNullOrEmpty(_ownCallsign) && content.Contains(_ownCallsign, StringComparison.OrdinalIgnoreCase)
                          && dto.Type == "radio";

        _storage.StoreMessage(dto);
        _ = _hub.Clients.All.SendAsync("ReceiveMessage", dto);
        _logger.LogInformation("swift 消息 {Type} {From}->{To}: {Content}", dto.Type, dto.From, dto.To, dto.Content);
    }

    private async Task BroadcastSwiftConnectionAsync()
    {
        await _hub.Clients.All.SendAsync("SwiftConnectionChanged", new
        {
            connected = _swiftConnected,
            dbus = _dbusReady,
            simulate = Simulate,
            callsign = _ownCallsign,
            timestamp = DateTime.UtcNow
        });
    }

    // ============ 解码 ============

    /// <summary>把结构拍平成一维字段列表（等价于 swift 的 flat wire 格式语义）。</summary>
    private static List<DBusWire.DValue> Flatten(DBusWire.DValue v)
    {
        if (v.Kind != '(') return new List<DBusWire.DValue> { v };
        var result = new List<DBusWire.DValue>();
        foreach (var f in v.Struct ?? new List<DBusWire.DValue>())
            result.AddRange(Flatten(f));
        return result;
    }

    private static double LatFromNormal(double x, double y, double z)
        => Math.Atan2(z, Math.Sqrt(x * x + y * y)) * 180.0 / Math.PI;

    private static double LonFromNormal(double x, double y, double z)
        => Math.Atan2(y, x) * 180.0 / Math.PI;

    /// <summary>
    /// CSimulatedAircraft 拍平索引（来源：simulatedaircraft.h / aircraftsituation.h / coordinategeodetic.h metaclass）。
    /// </summary>
    private void UpdateOwnState(List<DBusWire.DValue> f)
    {
        if (f.Count < 50) return;
        _ownCallsign = f[0].Str();
        var x = f[17].Double(); var y = f[18].Double(); var z = f[19].Double();
        var altM = f[20].Double();
        var headingRad = f[24].Double();
        var gsMs = f[27].Double();
        var onGround = f[43].Int() == 1; // OnGroundDetails enum: OnGround=1
        var com1Hz = f[51].Double();
        var com2Hz = f[59].Double();

        _ownState = new AircraftStateDto
        {
            Success = true,
            Callsign = _ownCallsign,
            Connected = _swiftConnected,
            Position = new PositionDto
            {
                Latitude = LatFromNormal(x, y, z),
                Longitude = LonFromNormal(x, y, z),
                Altitude = (int)Math.Round(altM * 3.28084) // 英尺
            },
            Heading = (int)Math.Round(NormDeg(headingRad * 180.0 / Math.PI)),
            GroundSpeed = (int)Math.Round(gsMs * 1.94384), // 节
            Squawk = f[66].Int().ToString("D4", CultureInfo.InvariantCulture),
            Status = onGround ? "ground" : "enRoute",
            Com1Frequency = (int)Math.Round(com1Hz / 1000),
            Com2Frequency = (int)Math.Round(com2Hz / 1000),
            OnGround = onGround,
            Timestamp = DateTime.UtcNow
        };
    }

    private void DecodeAircraftList(DBusWire.DValue arr)
    {
        var seen = new HashSet<string>();
        foreach (var elem in arr.Array ?? new List<DBusWire.DValue>())
        {
            var f = Flatten(elem);
            if (f.Count < 50) continue;
            var callsign = f[0].Str();
            if (string.IsNullOrEmpty(callsign) || callsign == _ownCallsign) continue;
            var x = f[17].Double(); var y = f[18].Double(); var z = f[19].Double();
            var altM = f[20].Double();
            var headingRad = f[24].Double();
            var gsMs = f[27].Double();

            var dto = new NearbyAircraftDto
            {
                Callsign = callsign,
                Position = new PositionDto
                {
                    Latitude = LatFromNormal(x, y, z),
                    Longitude = LonFromNormal(x, y, z),
                    Altitude = (int)Math.Round(altM * 3.28084)
                },
                Heading = (int)Math.Round(NormDeg(headingRad * 180.0 / Math.PI)),
                GroundSpeed = (int)Math.Round(gsMs * 1.94384)
            };
            _radar[callsign] = dto;
            seen.Add(callsign);
        }
        foreach (var stale in _radar.Keys.Where(k => !seen.Contains(k)).ToList())
            _radar.TryRemove(stale, out _);
    }

    private void DecodeAtcList(DBusWire.DValue arr)
    {
        var seen = new HashSet<string>();
        foreach (var elem in arr.Array ?? new List<DBusWire.DValue>())
        {
            // CAtcStation 拍平: 0-3 callsign, 4-12 controller(CUser), 13 freqHz,
            // 14-19 position(x,y,z,alt,datum,alttype), 20 range m, 21 isOnline, 22 afv,
            // 23 logoff((iii)(iiii)i), 24-26 atis, 27-29 metar, 30 relDist, 31 relBearing
            var f = Flatten(elem);
            if (f.Count < 20) continue;
            var callsign = f[0].Str();
            if (string.IsNullOrEmpty(callsign)) continue;

            var x = f[14].Double(); var y = f[15].Double(); var z = f[16].Double();
            var dto = new AtcListDto
            {
                Callsign = callsign,
                Frequency = (int)Math.Round(f[13].Double() / 1000), // kHz
                Name = f[5].Str(), // controller realname
                FacilityType = FacilityTypeOf(callsign)
            };
            // 位置信息附加到雷达快照（ATC 也显示在雷达上）
            _atc[callsign] = dto;
            _radar[callsign] = new NearbyAircraftDto
            {
                Callsign = callsign,
                Position = new PositionDto
                {
                    Latitude = LatFromNormal(x, y, z),
                    Longitude = LonFromNormal(x, y, z),
                    Altitude = 0
                },
                Heading = 0,
                GroundSpeed = 0,
                DistanceNm = 0,
                // 复用字段区分: 负海拔不适用，改用 Type 无字段 → 用 dto 侧列表区分
            };
            seen.Add(callsign);
        }
        foreach (var stale in _atc.Keys.Where(k => !seen.Contains(k)).ToList())
        {
            _atc.TryRemove(stale, out _);
            _radar.TryRemove(stale, out _);
        }
    }

    private static string FacilityTypeOf(string callsign)
    {
        var suffix = callsign.Contains('_') ? callsign[(callsign.LastIndexOf('_') + 1)..].ToUpperInvariant() : "";
        return suffix switch
        {
            "CTR" or "FSS" or "UIR" => "CENTER",
            "APP" or "DEP" or "ARR" => "APP-DEP",
            "TWR" => "TOWER",
            "GND" => "GROUND",
            "DEL" or "CLR" => "CLR",
            "RMP" or "RAMP" => "RAMP",
            "ATIS" => "ATIS",
            _ => "CENTER"
        };
    }

    private static double NormDeg(double d)
    {
        d %= 360;
        if (d < 0) d += 360;
        return d;
    }

    // ============ 发送 ============

    public async Task<OperationResult> SendRadioMessageAsync(string message)
    {
        return await SendTextAsync(radio: true, recipient: "", message);
    }

    public async Task<OperationResult> SendPrivateMessageAsync(string recipient, string message)
    {
        if (string.IsNullOrWhiteSpace(recipient))
            return OperationResult.Failure("Recipient callsign is required");
        return await SendTextAsync(radio: false, recipient.Trim().ToUpperInvariant(), message);
    }

    private async Task<OperationResult> SendTextAsync(bool radio, string recipient, string message)
    {
        if (!Simulate && _dbus is not { IsConnected: true })
            return OperationResult.Failure("swift DBus not connected");
        if (!_swiftConnected)
            return OperationResult.Failure("swift 未连接 VATSIM 网络");

        try
        {
            // CTextMessageList = 数组，元素 CTextMessage 拍平结构:
            //   message s, timestamp x, senderCallsign (s s s (i)), recipientCallsign (s s s (i)), frequency d
            double freqHz = 0;
            if (radio)
            {
                // COM1 active 频率（拍平索引 51，见 UpdateOwnState）
                freqHz = 0;
                if (_ownState.Com1Frequency > 0) freqHz = _ownState.Com1Frequency * 1000.0;
                if (freqHz <= 0) return OperationResult.Failure("无法确定 COM1 频率");
            }

            var senderCs = _ownCallsign ?? "";
            var dbusi = _dbus;
            if (dbusi == null) return OperationResult.Failure("DBus not connected");

            await dbusi.CallMethodAsync(PathNetwork, IfaceNetwork, "sendTextMessages",
                "a(" + TextMessageElemSig + ")",
                w => w.WriteArray(1, (wr, _) =>
                {
                    wr.BeginStruct();
                    wr.WriteString(message);
                    wr.WriteInt64(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    // sender callsign（typeHint 用 (i) 结构）
                    wr.WriteString(senderCs); wr.WriteString(senderCs); wr.WriteString(""); wr.BeginStruct(); wr.WriteInt32(0);
                    // recipient callsign
                    wr.WriteString(recipient); wr.WriteString(recipient); wr.WriteString(""); wr.BeginStruct(); wr.WriteInt32(0);
                    wr.WriteDouble(freqHz);
                }), CancellationToken.None);

            _logger.LogInformation("已发送 {Type} 消息 → {Target}: {Message}", radio ? "radio" : "private",
                radio ? freqHz / 1000 + "kHz" : recipient, message);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "swift 发送消息失败");
            return OperationResult.Failure($"Failed to send: {ex.Message}");
        }
    }

    // ============ 查询接口（供 Hub / Controller）============

    public Task<AircraftStateDto> GetAircraftStateAsync() => Task.FromResult(_ownState);

    public Task<List<NearbyAircraftDto>> GetAircraftInRangeAsync(double? lat, double? lon, double? radiusNm)
    {
        var list = _radar.Values.ToList();
        if (lat.HasValue && lon.HasValue)
        {
            foreach (var a in list)
            {
                a.DistanceNm = HaversineNm(lat.Value, lon.Value, a.Position.Latitude, a.Position.Longitude);
            }
            if (radiusNm.HasValue)
                list = list.Where(a => a.DistanceNm <= radiusNm.Value).ToList();
        }
        return Task.FromResult(list.OrderByDescending(a => a.GroundSpeed).ToList());
    }

    public Task<List<AtcListDto>> GetAtcStationsAsync()
        => Task.FromResult(_atc.Values.OrderBy(a => a.Callsign).ToList());

    public Task<ConnectionStatusDto> GetConnectionStatusAsync()
        => Task.FromResult(new ConnectionStatusDto
        {
            Connected = _swiftConnected,
            Callsign = _ownCallsign,
            Server = "swift/VATSIM"
        });

    private static double HaversineNm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 3440.065; // 地球半径海里
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    // ============ 模拟模式（无 swift 时开发用）============

    private void RunSimulatedLoop(CancellationToken ct)
    {
        _ = Task.Run(async () =>
        {
            _dbusReady = true;
            _swiftConnected = true;
            _ownCallsign = "SIM1234";
            var rnd = new Random();
            var baseLat = 31.1434; var baseLon = 121.8052; // ZSPD
            var atcs = new[]
            {
                ("ZSPD_TWR", 118800, "Shanghai Tower"),
                ("ZSPD_GND", 121700, "Shanghai Ground"),
                ("ZSSS_APP", 120300, "Hongqiao Approach"),
            };
            var calls = new[] { "CES2109", "CSN6502", "CCA1832", "UAL858", "BAW169" };
            var heading = 0.0;

            while (!ct.IsCancellationRequested)
            {
                heading = (heading + 2) % 360;
                var alt = 8000 + rnd.Next(-200, 200);

                _ownState = new AircraftStateDto
                {
                    Success = true,
                    Callsign = _ownCallsign,
                    Connected = true,
                    Position = new PositionDto { Latitude = baseLat, Longitude = baseLon, Altitude = alt },
                    Heading = (int)heading,
                    GroundSpeed = 420,
                    Squawk = "2000",
                    Status = "enRoute",
                    Com1Frequency = 1188,
                    Com2Frequency = 1215,
                    OnGround = false,
                    Timestamp = DateTime.UtcNow
                };

                _radar[_ownCallsign] = new NearbyAircraftDto
                {
                    Callsign = _ownCallsign,
                    Position = new PositionDto { Latitude = baseLat, Longitude = baseLon, Altitude = alt },
                    Heading = (int)heading, GroundSpeed = 420
                };
                foreach (var c in calls)
                {
                    var a = _radar.TryGetValue(c, out var existing) ? existing : new NearbyAircraftDto { Callsign = c };
                    var dLat = (rnd.NextDouble() - 0.5) * 0.3;
                    var dLon = (rnd.NextDouble() - 0.5) * 0.3;
                    a.Position = new PositionDto
                    {
                        Latitude = baseLat + dLat,
                        Longitude = baseLon + dLon,
                        Altitude = 5000 + rnd.Next(0, 15000)
                    };
                    a.Heading = rnd.Next(360);
                    a.GroundSpeed = 300 + rnd.Next(200);
                    _radar[c] = a;
                }
                foreach (var (cs, freq, name) in atcs)
                {
                    _atc[cs] = new AtcListDto { Callsign = cs, Frequency = freq, Name = name, FacilityType = FacilityTypeOf(cs) };
                    _radar[cs] = new NearbyAircraftDto
                    {
                        Callsign = cs,
                        Position = new PositionDto { Latitude = baseLat + (rnd.NextDouble() - 0.5) * 0.1, Longitude = baseLon + (rnd.NextDouble() - 0.5) * 0.1, Altitude = 0 }
                    };
                }

                await _hub.Clients.All.SendAsync("AircraftStateUpdated", _ownState, ct);

                // 每 45 秒来一条模拟无线电
                if (rnd.Next(45) == 0)
                {
                    var from = calls[rnd.Next(calls.Length)];
                    var dto = new MessageDto
                    {
                        Type = "radio", MessageType = "radio",
                        From = from, To = "",
                        Content = $"SIM: maintaining flight level one two zero, {from}",
                        Frequency = 118800,
                        Timestamp = DateTime.UtcNow
                    };
                    _storage.StoreMessage(dto);
                    await _hub.Clients.All.SendAsync("ReceiveMessage", dto, ct);
                }

                await Task.Delay(1000, ct);
            }
        }, ct);
    }
}
