using Microsoft.AspNetCore.SignalR;
using SwiftBridge.Hubs;

namespace SwiftBridge.Services;

public class SwiftDbusService
{
    private readonly IHubContext<SwiftHub> _hubContext;
    private readonly ILogger<SwiftDbusService> _logger;
    private readonly IConfiguration _configuration;
    private bool _isConnected;
    private string? _connectedServer;
    private Timer? _reconnectTimer;

    public SwiftDbusService(
        IHubContext<SwiftHub> hubContext,
        ILogger<SwiftDbusService> logger,
        IConfiguration configuration)
    {
        _hubContext = hubContext;
        _logger = logger;
        _configuration = configuration;
    }

    public string? ConnectedServer => _connectedServer;

    public async Task<bool> ConnectAsync(string? address = null)
    {
        try
        {
            _logger.LogInformation("Connecting to Swift D-Bus");

            // Check if simulated mode
            if (_configuration["Swift:Simulate"] != "false")
            {
                _logger.LogWarning("Running in SIMULATED mode (Swift:Simulate != false)");
                _isConnected = true;
                await NotifyConnectionChanged(true);
                return true;
            }

            // TODO: Implement real D-Bus connection
            // Requires Tmds.DBus.Protocol API knowledge
            // See DBUS_INTEGRATION.md for implementation guide
            _logger.LogError("Real D-Bus not implemented. Set Swift:Simulate=true for development.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Swift D-Bus");
            _isConnected = false;
            await NotifyConnectionChanged(false);
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        _reconnectTimer?.Dispose();
        _isConnected = false;
        await NotifyConnectionChanged(false);
    }

    public async Task<AircraftState?> GetAircraftStateAsync()
    {
        if (!_isConnected) return null;

        try
        {
            // TODO: Replace with actual D-Bus calls
            // Example D-Bus paths for swift:
            // /org/swift_project/swift/OwnAircraft - GetPosition(), GetCallsign(), etc.

            _connectedServer = "VATSIM";
            return new AircraftState
            {
                Callsign = "SWIFT123",
                Latitude = 37.7749,
                Longitude = -122.4194,
                Altitude = 5000,
                GroundSpeed = 250,
                Heading = 90,
                Server = _connectedServer,
                Transponder = "2000",
                TransponderMode = 3,
                Com1Frequency = 118750,
                Com2Frequency = 121500,
                OnGround = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get aircraft state");
            return null;
        }
    }

    public async Task<List<AtcStation>?> GetAtcStationsAsync()
    {
        if (!_isConnected) return null;

        try
        {
            // TODO: Replace with actual D-Bus calls
            // Example D-Bus path:
            // /org/swift_project/swift/AtcStations - GetStations()

            return new List<AtcStation>
            {
                new AtcStation
                {
                    Callsign = "ZSSS_TWR",
                    Name = "Shanghai Tower",
                    Frequency = 118750,
                    Latitude = 31.1434,
                    Longitude = 121.8052,
                    Range = 50.0
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get ATC stations");
            return null;
        }
    }

    public async Task<bool> SendPrivateMessageAsync(string recipient, string message)
    {
        if (!_isConnected) return false;

        try
        {
            _logger.LogInformation("Sending private message to {Recipient}", recipient);
            await Task.Delay(10);
            await _hubContext.Clients.All.SendAsync("MessageSent", new { type = "private", recipient, message, success = true, timestamp = DateTime.UtcNow });
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
            _logger.LogInformation("Sending radio message");
            await Task.Delay(10);
            await _hubContext.Clients.All.SendAsync("MessageSent", new { type = "radio", message, success = true, timestamp = DateTime.UtcNow });
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
    public string? Transponder { get; set; }
    public int? TransponderMode { get; set; }
    public int? Com1Frequency { get; set; }
    public int? Com2Frequency { get; set; }
    public bool OnGround { get; set; }
}

public class AtcStation
{
    public string Callsign { get; set; } = "";
    public string Name { get; set; } = "";
    public int Frequency { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Range { get; set; }
}
