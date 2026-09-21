using Microsoft.AspNetCore.Mvc;
using SwiftBridge.Services;

namespace SwiftBridge.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MessagesController : ControllerBase
    {
        private readonly IMessageStorageService _storageService;
        private readonly ILogger<MessagesController> _logger;

        public MessagesController(
            IMessageStorageService storageService,
            ILogger<MessagesController> logger)
        {
            _storageService = storageService;
            _logger = logger;
        }

        /// <summary>
        /// Get message history
        /// </summary>
        [HttpGet]
        public IActionResult GetMessages(
            [FromQuery] string type = null,
            [FromQuery] int limit = 50,
            [FromQuery] int offset = 0,
            [FromQuery] string from = null)
        {
            try
            {
                if (limit > 200)
                {
                    limit = 200; // Max limit
                }

                var messages = _storageService.GetMessages(type, limit, offset);
                var total = _storageService.GetTotalCount(type);

                // Filter by sender if specified
                if (!string.IsNullOrEmpty(from))
                {
                    messages = messages.Where(m => m.From.Equals(from, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                var response = new
                {
                    success = true,
                    messages = messages,
                    total = total,
                    limit = limit,
                    offset = offset,
                    hasMore = offset + limit < total
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting messages");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get single message by ID
        /// </summary>
        [HttpGet("{id}")]
        public IActionResult GetMessage(string id)
        {
            try
            {
                var message = _storageService.GetMessageById(id);

                if (message == null)
                {
                    return NotFound(new { success = false, error = "Message not found" });
                }

                return Ok(new { success = true, message = message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting message");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Mark message as read
        /// </summary>
        [HttpPut("{id}/read")]
        public IActionResult MarkAsRead(string id)
        {
            try
            {
                var message = _storageService.GetMessageById(id);

                if (message == null)
                {
                    return NotFound(new { success = false, error = "Message not found" });
                }

                _storageService.MarkAsRead(id);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message as read");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Clear all messages
        /// </summary>
        [HttpDelete]
        public IActionResult ClearMessages()
        {
            try
            {
                int count = _storageService.GetTotalCount();
                _storageService.ClearMessages();

                _logger.LogInformation($"Cleared {count} messages");

                return Ok(new { success = true, deletedCount = count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing messages");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }
}
