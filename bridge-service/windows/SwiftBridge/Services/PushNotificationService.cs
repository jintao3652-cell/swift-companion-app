using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SwiftBridge.Models;

namespace SwiftBridge.Services
{
    public interface IPushNotificationService
    {
        Task RegisterTokenAsync(string userId, string fcmToken, string deviceId);
        Task SendNotificationAsync(string userId, PushNotificationDto notification);
    }

    public class PushNotificationService : IPushNotificationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PushNotificationService> _logger;
        private readonly IConfiguration _configuration;
        private readonly ConcurrentDictionary<string, UserTokenInfo> _userTokens;

        private const string FCM_API_URL = "https://fcm.googleapis.com/fcm/send";

        public PushNotificationService(
            HttpClient httpClient,
            ILogger<PushNotificationService> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
            _userTokens = new ConcurrentDictionary<string, UserTokenInfo>();

            var serverKey = _configuration["PushNotification:FcmServerKey"];
            if (!string.IsNullOrEmpty(serverKey) && serverKey != "YOUR_FCM_SERVER_KEY_HERE")
            {
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"key={serverKey}");
            }
        }

        public Task RegisterTokenAsync(string userId, string fcmToken, string deviceId)
        {
            var tokenInfo = new UserTokenInfo
            {
                UserId = userId,
                FcmToken = fcmToken,
                DeviceId = deviceId,
                RegisteredAt = DateTime.UtcNow
            };

            _userTokens.AddOrUpdate(userId, tokenInfo, (key, old) => tokenInfo);
            _logger.LogInformation($"Registered FCM token for user {userId}");

            return Task.CompletedTask;
        }

        public async Task SendNotificationAsync(string userId, PushNotificationDto notification)
        {
            if (!_userTokens.TryGetValue(userId, out var tokenInfo))
            {
                _logger.LogWarning($"No FCM token found for user {userId}");
                return;
            }

            await SendToFcmAsync(tokenInfo.FcmToken, notification);
        }

        private async Task SendToFcmAsync(string fcmToken, PushNotificationDto notification)
        {
            var payload = new
            {
                to = fcmToken,
                notification = new
                {
                    title = notification.Title,
                    body = notification.Body,
                    sound = "default"
                },
                data = notification.Data,
                priority = "high"
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(FCM_API_URL, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Push notification sent: {notification.Title}");
            }
            else
            {
                _logger.LogWarning("Failed to send push notification");
            }
        }

        private class UserTokenInfo
        {
            public string UserId { get; set; }
            public string FcmToken { get; set; }
            public string DeviceId { get; set; }
            public DateTime RegisteredAt { get; set; }
        }
    }
}
