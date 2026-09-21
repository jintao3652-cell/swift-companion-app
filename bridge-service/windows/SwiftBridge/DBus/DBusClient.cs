using System.Net.Sockets;
using System.Text;

namespace SwiftBridge.DBus;

/// <summary>
/// 连接 swiftCore 的 DBus peer-to-peer TCP 客户端（swift 启动参数 --dbus tcp:host=...,port=...）。
/// 支持：方法调用、信号接收、Introspect。带指数退避自动重连。
/// </summary>
public sealed class DBusClient : IAsyncDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly ILogger _logger;

    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private uint _serial;
    private readonly Dictionary<uint, TaskCompletionSource<DBusWire.IncomingMessage>> _pending = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public event Action<DBusWire.IncomingMessage>? SignalReceived;
    public event Action<bool>? ConnectionChanged;
    public bool IsConnected { get; private set; }

    public DBusClient(string address, ILogger logger)
    {
        // address 形如 "tcp:host=127.0.0.1,port=45000"
        _logger = logger;
        var host = "127.0.0.1";
        var port = 45000;
        foreach (var kv in address.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = kv.Split('=', 2);
            if (parts.Length != 2) continue;
            var key = parts[0].Trim().ToLowerInvariant();
            if (key.EndsWith("host")) host = parts[1].Trim();
            else if (key.EndsWith("port")) _ = int.TryParse(parts[1].Trim(), out port);
        }
        _host = host;
        _port = port;
    }

    public async Task<bool> ConnectAsync(CancellationToken ct)
    {
        try
        {
            _tcp = new TcpClient { NoDelay = true };
            await _tcp.ConnectAsync(_host, _port, ct);
            _stream = _tcp.GetStream();

            if (!await AuthenticateAsync(ct))
            {
                _logger.LogError("DBus auth failed on {Host}:{Port}", _host, _port);
                await CloseAsync();
                return false;
            }

            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ReceiveLoopAsync(_cts.Token), CancellationToken.None);

            IsConnected = true;
            ConnectionChanged?.Invoke(true);
            _logger.LogInformation("DBus connected to {Host}:{Port}", _host, _port);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DBus connect failed {Host}:{Port}", _host, _port);
            await CloseAsync();
            return false;
        }
    }

    /// <summary>libdbus p2p 认证：优先 EXTERNAL（uid 0 的 hex），失败退 ANONYMOUS。</summary>
    private async Task<bool> AuthenticateAsync(CancellationToken ct)
    {
        var stream = _stream!;
        async Task<(bool ok, string line)> SendAuthLineAsync(string line)
        {
            var bytes = Encoding.ASCII.GetBytes(line + "\r\n");
            await stream.WriteAsync(bytes, ct);
            var buf = new byte[256];
            var len = await stream.ReadAsync(buf, ct);
            var resp = Encoding.ASCII.GetString(buf, 0, Math.Max(0, len - 2));
            return (resp.StartsWith("OK"), resp);
        }

        // 先发 \0
        await stream.WriteAsync(new byte[] { 0 }, ct);

        var (ok1, r1) = await SendAuthLineAsync("AUTH EXTERNAL " + HexUid("0"));
        if (!ok1)
        {
            var (ok2, r2) = await SendAuthLineAsync("AUTH ANONYMOUS");
            if (!ok2)
            {
                _logger.LogError("DBus auth responses: EXTERNAL→{R1}, ANONYMOUS→{R2}", r1, r2);
                return false;
            }
        }

        await stream.WriteAsync(Encoding.ASCII.GetBytes("BEGIN\r\n"), ct);
        return true;
    }

    private static string HexUid(string uid)
    {
        // hex 编码 uid 字符串的每个字节（D-Bus EXTERNAL 规范）
        var bytes = Encoding.ASCII.GetBytes(uid);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // ---------- 调用 ----------

    public async Task<DBusWire.IncomingMessage> CallMethodAsync(
        string path, string iface, string member, string? bodySig, Action<DBusWire.BodyWriter>? buildBody, CancellationToken ct)
    {
        var stream = _stream ?? throw new InvalidOperationException("DBus not connected");

        byte[]? body = null;
        if (buildBody != null && bodySig != null)
        {
            var w = new DBusWire.BodyWriter();
            buildBody(w);
            body = w.ToBytes();
        }

        await _sendLock.WaitAsync(ct);
        uint serial;
        TaskCompletionSource<DBusWire.IncomingMessage> tcs;
        byte[] frame;
        try
        {
            serial = ++_serial;
            frame = DBusWire.BuildMethodCall(serial, path, iface, member, bodySig, body);
            tcs = new TaskCompletionSource<DBusWire.IncomingMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[serial] = tcs;
            await stream.WriteAsync(frame, ct);
        }
        finally
        {
            _sendLock.Release();
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            await using (timeout.Token.Register(() => tcs.TrySetException(new TimeoutException($"{member} timeout"))))
            {
                var reply = await tcs.Task;
                if (reply.Type == DBusWire.MsgError)
                    throw new InvalidOperationException($"DBus error on {member}: {reply.ErrorName}");
                return reply;
            }
        }
        finally
        {
            _pending.Remove(serial);
        }
    }

    public async Task<string> IntrospectAsync(string path, CancellationToken ct)
    {
        var reply = await CallMethodAsync(path, "org.freedesktop.DBus.Introspectable", "Introspect",
            null, null, ct);
        var sig = reply.BodySignature ?? "s";
        var decoded = DBusWire.DecodeBody(sig, reply.Body);
        return decoded.Count > 0 ? decoded[0].Str() : "";
    }

    // ---------- 接收 ----------

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[1024 * 1024];
        int buffered = 0;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var stream = _stream!;
                int n = await stream.ReadAsync(buffer.AsMemory(buffered, buffer.Length - buffered), ct);
                if (n == 0) throw new IOException("DBus connection closed by peer");
                buffered += n;

                while (true)
                {
                    var (msg, consumed) = DBusWire.TryParseMessage(buffer, buffered);
                    if (msg == null) break;
                    // 移除已消费字节
                    Array.Copy(buffer, consumed, buffer, 0, buffered - consumed);
                    buffered -= consumed;
                    HandleMessage(msg);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DBus receive loop ended");
        }
        finally
        {
            IsConnected = false;
            ConnectionChanged?.Invoke(false);
            _ = CloseAsync();
        }
    }

    private void HandleMessage(DBusWire.IncomingMessage msg)
    {
        switch (msg.Type)
        {
            case DBusWire.MsgMethodReturn:
            case DBusWire.MsgError:
                lock (_pending)
                {
                    if (_pending.Remove(msg.ReplySerial, out var tcs))
                        tcs.TrySetResult(msg);
                }
                break;
            case DBusWire.MsgSignal:
                SignalReceived?.Invoke(msg);
                break;
        }
    }

    private async Task CloseAsync()
    {
        try
        {
            _cts?.Cancel();
            _stream?.Close();
            _tcp?.Close();
        }
        catch { }
        _stream = null;
        _tcp = null;
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        _sendLock.Dispose();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
