using Microsoft.AspNetCore.Mvc;
using VatsimBridge.Models;
using VatsimBridge.Services;
using System.Diagnostics;

namespace VatsimBridge.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatusController : ControllerBase
    {
        private readonly IPluginCommunicationService _pluginService;
        private readonly ILogger<StatusController> _logger;

        public StatusController(
            IPluginCommunicationService pluginService,
            ILogger<StatusController> logger)
        {
            _pluginService = pluginService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetStatus()
        {
            var pluginConnected = await _pluginService.IsPluginConnectedAsync();
            var state = await _pluginService.GetCurrentStateAsync();

            var status = new
            {
                bridgeVersion = "1.0.0",
                vPilotConnected = state.Connected,
                pluginConnected = pluginConnected,
                callsign = state.Callsign,
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
    }
}
