using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using VatsimBridge.Hubs;
using VatsimBridge.Models;
using VatsimBridge.Services;

namespace VatsimBridge.Controllers
{
    [ApiController]
    [Route("api/plugin")]
    public class PluginEventController : ControllerBase
    {
        private readonly IHubContext<VatsimHub> _hubContext;
        private readonly IPushNotificationService _pushService;
        private readonly IMessageStorageService _storageService;
        private readonly ILogger<PluginEventController> _logger;

        public PluginEventController(
            IHubContext<VatsimHub> hubContext,
            IPushNotificationService pushService,
            IMessageStorageService storageService,
            ILogger<PluginEventController> logger)
        {
            _hubContext = hubContext;
            _pushService = pushService;
            _storageService = storageService;
            _logger = logger;
        }

        /// <summary>
        /// Receive events from vPilot plugin
        /// </summary>
        [HttpPost("event")]
        public async Task<IActionResult> ReceiveEvent([FromBody] PluginEventDto eventData)
        {
            try
            {
                _logger.LogInformation($"Received plugin event: {eventData.Type}");

                switch (eventData.Type)
                {
                    case "message":
                        await HandleMessageEvent(eventData);
                        break;

                    case "state":
                        await HandleStateEvent(eventData);
                        break;

                    default:
                        _logger.LogWarning($"Unknown event type: {eventData.Type}");
                        break;
                }

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling plugin event");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        private async Task HandleMessageEvent(PluginEventDto eventData)
        {
            // 插件发来的 payload 是 camelCase(content/from/messageType)，
            // MessageDto 属性是 PascalCase，必须忽略大小写否则字段全为 null。
            var message = System.Text.Json.JsonSerializer.Deserialize<MessageDto>(
                eventData.Payload.GetRawText(),
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (message != null)
            {
                // Store message in history
                _storageService.StoreMessage(message);

                // Broadcast to all connected clients via SignalR
                await _hubContext.Clients.All.SendAsync("ReceiveMessage", message);

                // Send push notification for private messages or mentions
                if (message.MessageType == "private" || message.MentionedUs)
                {
                    var notification = new PushNotificationDto
                    {
                        Title = message.MessageType == "private"
                            ? $"Private Message from {message.From}"
                            : "You were mentioned",
                        Body = message.Content,
                        Data = new Dictionary<string, string>
                        {
                            { "type", message.MessageType },
                            { "messageId", message.Id },
                            { "from", message.From },
                            { "timestamp", message.Timestamp.ToString("o") }
                        }
                    };

                    // Send to all connected users (in production, filter by user)
                    try
                    {
                        // TODO: In production, get specific user IDs and send targeted notifications
                        _logger.LogInformation($"Should send push notification: {notification.Title}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send push notification");
                    }
                }
            }
        }

        private async Task HandleStateEvent(PluginEventDto eventData)
        {
            // Broadcast state update to all connected clients
            await _hubContext.Clients.All.SendAsync("StateUpdated", eventData.Payload);
        }
    }
}
