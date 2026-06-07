using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;
using VatsimBridge.Models;
using VatsimBridge.Services;

namespace VatsimBridge.Hubs
{
    /// <summary>
    /// SignalR Hub for real-time communication with mobile app
    /// </summary>
    public class VatsimHub : Hub
    {
        private readonly IPluginCommunicationService _pluginService;
        private readonly IPushNotificationService _pushService;
        private readonly ILogger<VatsimHub> _logger;

        public VatsimHub(
            IPluginCommunicationService pluginService,
            IPushNotificationService pushService,
            ILogger<VatsimHub> logger)
        {
            _pluginService = pluginService;
            _pushService = pushService;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var connectionId = Context.ConnectionId;
            var user = Context.User?.Identity?.Name ?? "Anonymous";

            _logger.LogInformation($"Client connected: {connectionId} (User: {user})");

            // 发送当前状态
            var state = await _pluginService.GetCurrentStateAsync();
            await Clients.Caller.SendAsync("StateUpdated", state);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var connectionId = Context.ConnectionId;
            _logger.LogInformation($"Client disconnected: {connectionId}");

            if (exception != null)
            {
                _logger.LogError(exception, "Client disconnected with error");
            }

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Send private message
        /// </summary>
        public async Task<OperationResult> SendPrivateMessage(string recipient, string message)
        {
            try
            {
                _logger.LogInformation($"Sending private message to {recipient}: {message}");

                var result = await _pluginService.SendMessageAsync(new SendMessageRequest
                {
                    MessageType = "private",
                    Recipient = recipient,
                    Message = message
                });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending private message");
                return OperationResult.Failure($"Failed to send message: {ex.Message}");
            }
        }

        /// <summary>
        /// Send radio message
        /// </summary>
        public async Task<OperationResult> SendRadioMessage(string message)
        {
            try
            {
                _logger.LogInformation($"Sending radio message: {message}");

                var result = await _pluginService.SendMessageAsync(new SendMessageRequest
                {
                    MessageType = "radio",
                    Message = message
                });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending radio message");
                return OperationResult.Failure($"Failed to send message: {ex.Message}");
            }
        }

        /// <summary>
        /// Execute vPilot command (e.g., .atis, .chat)
        /// </summary>
        public async Task<OperationResult> ExecuteCommand(string command)
        {
            try
            {
                _logger.LogInformation($"Executing command: {command}");

                var result = await _pluginService.ExecuteCommandAsync(command);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing command");
                return OperationResult.Failure($"Failed to execute command: {ex.Message}");
            }
        }

        /// <summary>
        /// Request current aircraft state
        /// </summary>
        public async Task RequestAircraftState()
        {
            try
            {
                var state = await _pluginService.GetCurrentStateAsync();
                await Clients.Caller.SendAsync("AircraftStateUpdated", state);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting aircraft state");
            }
        }

        /// <summary>
        /// Register FCM token for push notifications
        /// </summary>
        public async Task RegisterPushToken(string fcmToken, string deviceId)
        {
            try
            {
                var user = Context.User?.Identity?.Name ?? "Anonymous";
                await _pushService.RegisterTokenAsync(user, fcmToken, deviceId);
                _logger.LogInformation($"Registered push token for user {user}, device {deviceId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering push token");
            }
        }
    }
}
