using Microsoft.AspNetCore.Mvc;
using VatsimBridge.Models;
using VatsimBridge.Services;

namespace VatsimBridge.Controllers
{
    [ApiController]
    [Route("api")]
    public class AtcController : ControllerBase
    {
        private readonly IPluginCommunicationService _pluginService;
        private readonly IVatsimDataService _vatsim;

        public AtcController(IPluginCommunicationService pluginService, IVatsimDataService vatsim)
        {
            _pluginService = pluginService;
            _vatsim = vatsim;
        }

        // 本机实时状态：呼号来自插件，位置/高度/速度/状态来自 VATSIM datafeed
        [HttpGet("aircraft/state")]
        public async Task<IActionResult> GetAircraftState()
        {
            var baseState = await _pluginService.GetCurrentStateAsync();
            var callsign = baseState.Callsign;
            var pilot = _vatsim.GetPilot(callsign);

            if (pilot == null)
            {
                // 未连线或 datafeed 未收录
                return Ok(new AircraftStateDto { Callsign = callsign, Connected = baseState.Connected });
            }

            return Ok(new AircraftStateDto
            {
                Callsign = callsign,
                Connected = true,
                Position = new PositionDto
                {
                    Latitude = pilot.Latitude,
                    Longitude = pilot.Longitude,
                    Altitude = pilot.Altitude
                },
                Heading = pilot.Heading,
                GroundSpeed = pilot.Groundspeed,
                Squawk = pilot.Transponder,
                Status = await InferStatus(pilot, _vatsim.GetPreviousAltitude(callsign)),
                OnGround = pilot.Groundspeed < 50,
                Timestamp = DateTime.UtcNow
            });
        }

        // 按 速度/高度/巡航高度/升降趋势/到机场距离 推断飞行相位
        private async Task<string> InferStatus(Services.VatsimPilot p, int? prevAlt)
        {
            var gs = p.Groundspeed;
            var alt = p.Altitude;
            int.TryParse(p.FlightPlan?.Altitude, out var cruise); // 计划巡航高度(可能为0)

            // 到出发/到达机场距离(海里)，查不到则为 null
            var dep = await _vatsim.GetAirportAsync(p.FlightPlan?.Departure);
            var arr = await _vatsim.GetAirportAsync(p.FlightPlan?.Arrival);
            double? distDep = dep != null ? Services.VatsimDataService.DistanceNm(p.Latitude, p.Longitude, dep.Lat, dep.Lon) : null;
            double? distArr = arr != null ? Services.VatsimDataService.DistanceNm(p.Latitude, p.Longitude, arr.Lat, arr.Lon) : null;

            // 地面相位(结合机场距离更准)
            if (gs <= 3) return "atGate";
            if (gs < 40 && alt < 1500) return "taxiing";

            // 升降趋势(与上一轮高度比)
            var trend = prevAlt.HasValue ? alt - prevAlt.Value : 0;

            // 落地/到达：低速 + 低空 + 靠近到达机场
            if (gs < 50 && distArr.HasValue && distArr < 3) return "arrived";

            // 接近：靠近到达机场 + 低空
            if (distArr.HasValue && distArr < 80 && alt < 12000) return "approaching";

            if (trend > 200) return "climbing";
            if (trend < -200) return "descending";

            // 趋势平稳时按高度/巡航高度/距离
            if (cruise > 0 && alt >= cruise - 2000) return "cruising";
            if (alt < 10000) return "approaching";
            return "enRoute";
        }

        // 在线管制列表，按类型分组。vatsim-radar 管制无坐标，返回全部在线管制。
        [HttpGet("atc/online")]
        public async Task<IActionResult> GetOnlineAtc()
        {
            var baseState = await _pluginService.GetCurrentStateAsync();
            var pilot = _vatsim.GetPilot(baseState.Callsign);

            // 无坐标也返回全部管制（located 仅表示本机是否在 datafeed 中定位成功）
            var all = _vatsim.GetControllersNear(pilot?.Latitude ?? 0, pilot?.Longitude ?? 0);
            var list = all.Select(c => new ControllerInfoDto
            {
                Callsign = c.Callsign,
                Frequency = c.Frequency,
                Type = ClassifyFacility(c.Callsign),
                Atis = c.TextAtis != null ? string.Join(" ", c.TextAtis) : null
            }).ToList();

            return Ok(new { located = pilot != null, controllers = list });
        }

        // 按 callsign 后缀分类，符合 App 期望的分组键
        private static string ClassifyFacility(string callsign)
        {
            if (string.IsNullOrEmpty(callsign)) return "OTHER";
            var cs = callsign.ToUpperInvariant();
            if (cs.EndsWith("_ATIS")) return "ATIS";
            if (cs.EndsWith("_CTR")) return "CENTER";
            if (cs.EndsWith("_FSS")) return "FSS";
            if (cs.EndsWith("_APP")) return "APPROACH";
            if (cs.EndsWith("_DEP")) return "DEPARTURE";
            if (cs.EndsWith("_TWR")) return "TOWER";
            if (cs.EndsWith("_GND")) return "GROUND";
            if (cs.EndsWith("_DEL")) return "DELIVERY";
            return "OTHER";
        }
    }
}
