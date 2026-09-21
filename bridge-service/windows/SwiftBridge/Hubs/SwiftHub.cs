using Microsoft.AspNetCore.SignalR;
using SwiftBridge.Models;
using SwiftBridge.Services;

namespace SwiftBridge.Hubs
{
    /// <summary>
    /// SignalR Hub：与手机端实时通信。数据全部来自 SwiftDataService（swift DBus）。
    /// 方法/事件名保持与手机端既有实现兼容。
    /// </summary>
    public class SwiftHub : Hub
    {
        private readonly SwiftDataService _swift;
        private readonly IPushNotificationService _pushService;
        private readonly ILogger<SwiftHub> _logger;

        public SwiftHub(SwiftDataService swift, IPushNotificationService pushService, ILogger<SwiftHub> logger)
        {
            _swift = swift;
            _pushService = pushService;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var connectionId = Context.ConnectionId;
            var user = Context.User?.Identity?.Name ?? "Anonymous";
            _logger.LogInformation("Client connected: {Conn} (User: {User})", connectionId, user);

            // 发送当前状态
            var state = await _swift.GetAircraftStateAsync();
            await Clients.Caller.SendAsync("StateUpdated", state);
            await Clients.Caller.SendAsync("AircraftStateUpdated", state);

            await base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Client disconnected: {Conn}", Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }

        /// <summary>发送私信（经 swift sendTextMessages 上 VATSIM 网络）。</summary>
        public async Task<OperationResult> SendPrivateMessage(string recipient, string message)
        {
            _logger.LogInformation("PM → {Recipient}: {Message}", recipient, message);
            return await _swift.SendPrivateMessageAsync(recipient, message);
        }

        /// <summary>发送无线电消息（COM1 当前频率）。</summary>
        public async Task<OperationResult> SendRadioMessage(string message)
        {
            _logger.LogInformation("Radio: {Message}", message);
            return await _swift.SendRadioMessageAsync(message);
        }

        /// <summary>快捷命令（保留兼容；swift 模式下不支持 vPilot 指令）。</summary>
        public Task<OperationResult> ExecuteCommand(string command)
        {
            return Task.FromResult(OperationResult.Failure(
                $"Command '{command}' not supported in swift mode"));
        }

        /// <summary>请求当前飞机状态。</summary>
        public async Task RequestAircraftState()
        {
            var state = await _swift.GetAircraftStateAsync();
            await Clients.Caller.SendAsync("AircraftStateUpdated", state);
        }

        /// <summary>注册推送 Token（保留兼容）。</summary>
        public async Task RegisterPushToken(string fcmToken, string deviceId)
        {
            var user = Context.User?.Identity?.Name ?? "Anonymous";
            await _pushService.RegisterTokenAsync(user, fcmToken, deviceId);
            _logger.LogInformation("Registered push token for user {User}, device {Device}", user, deviceId);
        }
    }
}
