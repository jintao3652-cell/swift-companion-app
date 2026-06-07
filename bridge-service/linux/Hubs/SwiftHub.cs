using Microsoft.AspNetCore.SignalR;
using SwiftBridge.Services;

namespace SwiftBridge.Hubs;

public class SwiftHub : Hub
{
    private readonly SwiftDbusService _swiftService;
    private readonly IMessageStorageService _messageStorage;
    private readonly ILogger<SwiftHub> _logger;

    public SwiftHub(
        SwiftDbusService swiftService,
        IMessageStorageService messageStorage,
        ILogger<SwiftHub> logger)
    {
        _swiftService = swiftService;
        _messageStorage = messageStorage;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);

        var recentMessages = _messageStorage.GetMessages(limit: 50);
        await Clients.Caller.SendAsync("MessageHistory", recentMessages);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendPrivateMessage(string recipient, string message)
    {
        _logger.LogInformation("Sending private message to {Recipient}", recipient);
        var success = await _swiftService.SendPrivateMessageAsync(recipient, message);

        await Clients.Caller.SendAsync("MessageSent", new { success, recipient, message });
    }

    public async Task SendRadioMessage(string message)
    {
        _logger.LogInformation("Sending radio message");
        var success = await _swiftService.SendRadioMessageAsync(message);

        await Clients.Caller.SendAsync("MessageSent", new { success, message });
    }

    public async Task<object?> GetAircraftState()
    {
        var state = await _swiftService.GetAircraftStateAsync();
        return state;
    }
}
