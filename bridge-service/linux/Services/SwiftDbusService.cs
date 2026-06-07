using Microsoft.AspNetCore.SignalR;
using SwiftBridge.Hubs;

namespace SwiftBridge.Services;

public class SwiftDbusService
{
    private readonly IHubContext<SwiftHub> _hubContext;
    private readonly ILogger<SwiftDbusService> _logger;
    private bool _isConnected;
    private string? _connectedServer;

    public SwiftDbusService(
        IHubContext<SwiftHub> hubContext,
        ILogger<SwiftDbusService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public string? ConnectedServer => _connectedServer;

    public async Task<bool> ConnectAsync(string address = "tcp:host=127.0.0.1,port=45000")
    {
        try
        {
            _logger.LogInformation("Connecting to swift D-Bus at {Address}", address);

            // TODO: Implement actual D-Bus connection using Tmds.DBus.Protocol
            // For now, simulate connection
            await Task.Delay(100);

            _isConnected = true;
            _logger.LogInformation("Connected to swift D-Bus successfully (simulated)");

            await NotifyConnectionChanged(true);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to swift D-Bus");
            _isConnected = false;
            await NotifyConnectionChanged(false);
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        _isConnected = false;
        await NotifyConnectionChanged(false);
    }

    public async Task<AircraftState?> GetAircraftStateAsync()
    {
        if (!_isConnected) return null;

        try
        {
            // TODO: Call swift D-Bus API to get actual server name
            await Task.Delay(10);

            // Simulate getting server info from swift
            _connectedServer = "VATSIM";

            return new AircraftState
            {
                Callsign = "SWIFT123",
                Latitude = 37.7749,
                Longitude = -122.4194,
                Altitude = 5000,
                GroundSpeed = 250,
                Heading = 90,
                Server = _connectedServer
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get aircraft state");
            return null;
        }
    }

    public async Task<bool> SendPrivateMessageAsync(string recipient, string message)
    {
        if (!_isConnected) return false;

        try
        {
            _logger.LogInformation("Sending private message to {Recipient}: {Message}", recipient, message);

            // TODO: Call swift D-Bus sendTextMessage API
            await Task.Delay(10);

            await _hubContext.Clients.All.SendAsync("MessageSent", new
            {
                type = "private",
                recipient,
                message,
                success = true,
                timestamp = DateTime.UtcNow
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send private message");
            return false;
        }
    }

    public async Task<bool> SendRadioMessageAsync(string message)
    {
        if (!_isConnected) return false;

        try
        {
            _logger.LogInformation("Sending radio message: {Message}", message);

            // TODO: Call swift D-Bus sendRadioMessage API
            await Task.Delay(10);

            await _hubContext.Clients.All.SendAsync("MessageSent", new
            {
                type = "radio",
                message,
                success = true,
                timestamp = DateTime.UtcNow
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send radio message");
            return false;
        }
    }

    private async Task NotifyConnectionChanged(bool connected)
    {
        await _hubContext.Clients.All.SendAsync("SwiftConnectionChanged", new
        {
            connected,
            timestamp = DateTime.UtcNow
        });
    }
}

public class AircraftState
{
    public string? Callsign { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; }
    public double GroundSpeed { get; set; }
    public double Heading { get; set; }
    public string? Server { get; set; }
}
