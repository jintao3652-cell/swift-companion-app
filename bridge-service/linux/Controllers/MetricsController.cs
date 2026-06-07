using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace SwiftBridge.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetricsController : ControllerBase
{
    private static readonly DateTime _startTime = DateTime.UtcNow;
    private static long _totalMessages = 0;
    private static long _totalErrors = 0;

    [HttpGet]
    public IActionResult GetMetrics()
    {
        var process = Process.GetCurrentProcess();
        var uptime = DateTime.UtcNow - _startTime;

        return Ok(new
        {
            uptime = new
            {
                totalSeconds = uptime.TotalSeconds,
                formatted = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m"
            },
            memory = new
            {
                workingSetMB = process.WorkingSet64 / 1024 / 1024,
                privateMemoryMB = process.PrivateMemorySize64 / 1024 / 1024
            },
            cpu = new
            {
                totalProcessorTime = process.TotalProcessorTime.TotalSeconds,
                threads = process.Threads.Count
            },
            messages = new
            {
                total = _totalMessages,
                errors = _totalErrors,
                errorRate = _totalMessages > 0 ? (double)_totalErrors / _totalMessages : 0
            }
        });
    }

    public static void IncrementMessageCount() => Interlocked.Increment(ref _totalMessages);
    public static void IncrementErrorCount() => Interlocked.Increment(ref _totalErrors);
}
