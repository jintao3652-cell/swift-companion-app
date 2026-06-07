using System;
using System.Collections.Generic;
using System.Linq;
using VatsimBridge.Models;

namespace VatsimBridge.Services
{
    public interface IMessageStorageService
    {
        void StoreMessage(MessageDto message);
        List<MessageDto> GetMessages(string messageType = null, int limit = 50, int offset = 0);
        MessageDto GetMessageById(string messageId);
        void MarkAsRead(string messageId);
        void ClearMessages();
        int GetTotalCount(string messageType = null);
    }

    /// <summary>
    /// In-memory message storage service
    /// </summary>
    public class MessageStorageService : IMessageStorageService
    {
        private readonly List<MessageDto> _messages;
        private readonly object _lockObject = new object();
        private readonly ILogger<MessageStorageService> _logger;
        private const int MAX_MESSAGES = 1000;

        public MessageStorageService(ILogger<MessageStorageService> logger)
        {
            _logger = logger;
            _messages = new List<MessageDto>();
        }

        public void StoreMessage(MessageDto message)
        {
            lock (_lockObject)
            {
                _messages.Add(message);

                // Keep only the latest MAX_MESSAGES
                if (_messages.Count > MAX_MESSAGES)
                {
                    int toRemove = _messages.Count - MAX_MESSAGES;
                    _messages.RemoveRange(0, toRemove);
                    _logger.LogDebug($"Removed {toRemove} old messages");
                }

                _logger.LogDebug($"Stored message: {message.Id} from {message.From}");
            }
        }

        public List<MessageDto> GetMessages(string messageType = null, int limit = 50, int offset = 0)
        {
            lock (_lockObject)
            {
                IEnumerable<MessageDto> query = _messages;

                // Filter by type if specified
                if (!string.IsNullOrEmpty(messageType))
                {
                    query = query.Where(m => m.MessageType.Equals(messageType, StringComparison.OrdinalIgnoreCase));
                }

                // Sort by timestamp descending (newest first)
                query = query.OrderByDescending(m => m.Timestamp);

                // Apply pagination
                query = query.Skip(offset).Take(limit);

                return query.ToList();
            }
        }

        public MessageDto GetMessageById(string messageId)
        {
            lock (_lockObject)
            {
                return _messages.FirstOrDefault(m => m.Id == messageId);
            }
        }

        public void MarkAsRead(string messageId)
        {
            lock (_lockObject)
            {
                var message = _messages.FirstOrDefault(m => m.Id == messageId);
                if (message != null)
                {
                    // Note: MessageDto doesn't have IsRead property in current definition
                    // You may need to add it
                    _logger.LogDebug($"Marked message {messageId} as read");
                }
            }
        }

        public void ClearMessages()
        {
            lock (_lockObject)
            {
                int count = _messages.Count;
                _messages.Clear();
                _logger.LogInformation($"Cleared {count} messages");
            }
        }

        public int GetTotalCount(string messageType = null)
        {
            lock (_lockObject)
            {
                if (string.IsNullOrEmpty(messageType))
                {
                    return _messages.Count;
                }

                return _messages.Count(m => m.MessageType.Equals(messageType, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
