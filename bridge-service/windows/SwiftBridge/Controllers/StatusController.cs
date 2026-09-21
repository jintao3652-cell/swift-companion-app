using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwiftBridge.Services;

namespace SwiftBridge.Controllers
{
    /// <summary>
    /// 状态接口：全部来自 SwiftDataService（swift DBus / 模拟模式）。
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    public class StatusController : ControllerBase
    {
        private readonly SwiftDataService _swift;
        private readonly ILogger<StatusController> _logger;

        public StatusController(SwiftDataService swift, ILogger<StatusController> logger)
        {
            _swift = swift;
            _logger = logger;
        }

        /// <summary>Bridge 总状态</summary>
        [HttpGet("api/status")]
        public IActionResult GetStatus()
        {
            return Ok(new
            {
                bridge = "running",
                mode = _swift.Simulate ? "simulated" : "swift-dbus",
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>健康检查</summary>
        [HttpGet("api/status/health")]
        public IActionResult Health() => Ok(new { status = "healthy", timestamp = DateTime.UtcNow });

        /// <summary>本机飞机状态（swift getOwnAircraft）</summary>
        [HttpGet("api/status/aircraft")]
        public async Task<IActionResult> GetAircraft()
        {
            var state = await _swift.GetAircraftStateAsync();
            return Ok(state);
        }

        /// <summary>在线 ATC 列表（swift getAtcStationsOnline）</summary>
        [HttpGet("api/status/atc")]
        public async Task<IActionResult> GetAtc()
        {
            var atc = await _swift.GetAtcStationsAsync();
            return Ok(atc);
        }

        /// <summary>范围内飞机（雷达数据，swift getAircraftInRange）</summary>
        [HttpGet("api/aircraft/nearby")]
        public async Task<IActionResult> GetNearby(
            [FromQuery] double? lat, [FromQuery] double? lon,
            [FromQuery] double? radiusNm)
        {
            var list = await _swift.GetAircraftInRangeAsync(lat, lon, radiusNm);
            return Ok(list);
        }
    }
}
