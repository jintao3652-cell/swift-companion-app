using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VatsimBridge.Services
{
    // VATSIM 官方 v3 datafeed 字段 (data.vatsim.net)。vatsim-radar 有 Cloudflare 403 不可用。
    public class VatsimFlightPlan
    {
        [JsonPropertyName("departure")] public string Departure { get; set; }
        [JsonPropertyName("arrival")] public string Arrival { get; set; }
        [JsonPropertyName("altitude")] public string Altitude { get; set; } // 巡航高度，字符串如 "34000"
    }

    public class VatsimPilot
    {
        [JsonPropertyName("callsign")] public string Callsign { get; set; }
        [JsonPropertyName("latitude")] public double Latitude { get; set; }
        [JsonPropertyName("longitude")] public double Longitude { get; set; }
        [JsonPropertyName("altitude")] public int Altitude { get; set; }
        [JsonPropertyName("groundspeed")] public int Groundspeed { get; set; }
        [JsonPropertyName("heading")] public int Heading { get; set; }
        [JsonPropertyName("transponder")] public string Transponder { get; set; }
        [JsonPropertyName("flight_plan")] public VatsimFlightPlan FlightPlan { get; set; }
    }

    public class VatsimControllerInfo
    {
        [JsonPropertyName("cid")] public int Cid { get; set; }
        [JsonPropertyName("callsign")] public string Callsign { get; set; }
        [JsonPropertyName("frequency")] public string Frequency { get; set; }
        [JsonPropertyName("facility")] public int Facility { get; set; }
        [JsonPropertyName("text_atis")] public List<string> TextAtis { get; set; }
    }

    public class VatsimDataFeed
    {
        [JsonPropertyName("pilots")] public List<VatsimPilot> Pilots { get; set; } = new();
        [JsonPropertyName("controllers")] public List<VatsimControllerInfo> Controllers { get; set; } = new();
        [JsonPropertyName("atis")] public List<VatsimControllerInfo> Atis { get; set; } = new();
    }

    public interface IVatsimDataService
    {
        VatsimPilot GetPilot(string callsign);
        List<VatsimControllerInfo> GetControllersNear(double lat, double lon);
        int? GetPreviousAltitude(string callsign); // 上一轮高度，用于判升降
        Task<AirportCoord> GetAirportAsync(string icao); // 机场经纬度(带缓存)
    }

    public class AirportCoord
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
        public int ElevationFt { get; set; }
    }

    // xflysim 机场 API 响应
    public class XflyAirportResponse
    {
        [JsonPropertyName("data")] public XflyAirportData Data { get; set; }
    }
    public class XflyAirportData
    {
        [JsonPropertyName("latitudeDeg")] public double LatitudeDeg { get; set; }
        [JsonPropertyName("longitudeDeg")] public double LongitudeDeg { get; set; }
        [JsonPropertyName("elevationFt")] public int ElevationFt { get; set; }
    }

    /// <summary>
    /// 后台每 15 秒拉取 VATSIM 官方 datafeed 并缓存，供本机状态/管制列表查询。
    /// </summary>
    public class VatsimDataService : BackgroundService, IVatsimDataService
    {
        private const string FeedUrl = "https://data.vatsim.net/v3/vatsim-data.json";
        private readonly HttpClient _http;
        private readonly ILogger<VatsimDataService> _logger;
        private volatile VatsimDataFeed _feed = new();
        // callsign -> 上一轮高度，用于判断爬升/下降
        private Dictionary<string, int> _prevAlt = new(StringComparer.OrdinalIgnoreCase);

        public VatsimDataService(IHttpClientFactory factory, ILogger<VatsimDataService> logger)
        {
            _http = factory.CreateClient();
            _http.Timeout = TimeSpan.FromSeconds(20);
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var json = await _http.GetStringAsync(FeedUrl, stoppingToken);
                    var feed = JsonSerializer.Deserialize<VatsimDataFeed>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (feed != null)
                    {
                        // 先记录旧高度快照(供本轮趋势判断)，再替换 feed
                        _prevAlt = _feed.Pilots.ToDictionary(p => p.Callsign, p => p.Altitude,
                            StringComparer.OrdinalIgnoreCase);
                        _feed = feed;
                    }
                    _logger.LogInformation($"VATSIM data updated: {_feed.Pilots.Count} pilots, {_feed.Controllers.Count} controllers, {_feed.Atis.Count} atis");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to fetch VATSIM datafeed: {ex.Message}");
                }
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }

        public int? GetPreviousAltitude(string callsign)
        {
            if (string.IsNullOrEmpty(callsign)) return null;
            return _prevAlt.TryGetValue(callsign, out var a) ? a : (int?)null;
        }

        // 机场坐标缓存：ICAO -> 坐标(null 表示查过但失败，避免反复请求)
        private readonly Dictionary<string, AirportCoord> _airportCache =
            new(StringComparer.OrdinalIgnoreCase);

        public async Task<AirportCoord> GetAirportAsync(string icao)
        {
            if (string.IsNullOrEmpty(icao)) return null;
            if (_airportCache.TryGetValue(icao, out var cached)) return cached;
            try
            {
                var resp = await _http.GetFromJsonAsync<XflyAirportResponse>(
                    $"https://api.xflysim.com/pilot/api/realTimeMap/airports/{icao}");
                var d = resp?.Data;
                var coord = (d != null && d.LatitudeDeg != 0)
                    ? new AirportCoord { Lat = d.LatitudeDeg, Lon = d.LongitudeDeg, ElevationFt = d.ElevationFt }
                    : null;
                _airportCache[icao] = coord;
                return coord;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to fetch airport {icao}: {ex.Message}");
                _airportCache[icao] = null;
                return null;
            }
        }

        public static double DistanceNm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 3440.065; // 海里
            double dLat = ToRad(lat2 - lat1), dLon = ToRad(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }
        private static double ToRad(double deg) => deg * Math.PI / 180.0;

        public VatsimPilot GetPilot(string callsign)
        {
            if (string.IsNullOrEmpty(callsign)) return null;
            return _feed.Pilots.FirstOrDefault(p =>
                string.Equals(p.Callsign, callsign, StringComparison.OrdinalIgnoreCase));
        }

        public List<VatsimControllerInfo> GetControllersNear(double lat, double lon)
        {
            // 全局管制列表：controllers + atis 合并，过滤 OBS(facility 0 且非 ATIS) 和无频率
            var result = new List<VatsimControllerInfo>();
            foreach (var c in _feed.Controllers.Concat(_feed.Atis))
            {
                if (string.IsNullOrEmpty(c.Frequency) || c.Frequency == "199.998") continue;
                if (c.Facility <= 0 && !(c.Callsign?.EndsWith("_ATIS") ?? false)) continue;
                result.Add(c);
            }
            return result;
        }
    }
}
