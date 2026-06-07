using Microsoft.AspNetCore.Mvc;
using SwiftBridge.Services;
using System.Diagnostics;

namespace SwiftBridge.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatusController : ControllerBase
    {
        private readonly SwiftDbusService _swiftService;
        private readonly ILogger<StatusController> _logger;

        public StatusController(
            SwiftDbusService swiftService,
            ILogger<StatusController> logger)
        {
            _swiftService = swiftService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetStatus()
        {
            var aircraftState = await _swiftService.GetAircraftStateAsync();

            var status = new
            {
                bridgeVersion = "1.0.0",
                platform = "Linux",
                swiftConnected = aircraftState != null,
                callsign = aircraftState?.Callsign,
                server = _swiftService.ConnectedServer,
                uptime = DateTime.UtcNow.Subtract(Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds,
                timestamp = DateTime.UtcNow
            };

            return Ok(status);
        }

        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow
            });
        }

        [HttpGet("aircraft")]
        public async Task<IActionResult> GetAircraft()
        {
            var state = await _swiftService.GetAircraftStateAsync();

            if (state == null)
            {
                return NotFound(new { error = "swift not connected or no aircraft data" });
            }

            return Ok(state);
        }
    }
}
