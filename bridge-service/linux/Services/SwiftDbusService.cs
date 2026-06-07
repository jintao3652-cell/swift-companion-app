using Microsoft.AspNetCore.SignalR;
using SwiftBridge.Hubs;
using Tmds.DBus.Protocol;

namespace SwiftBridge.Services;

public class SwiftDbusService
{
    private readonly IHubContext<SwiftHub> _hubContext;
    private readonly ILogger<SwiftDbusService> _logger;
    private readonly IConfiguration _configuration;
    private Connection? _connection;
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
        address ??= $"tcp:host={_configuration["Swift:DbusHost"]},port={_configuration["Swift:DbusPort"]}";

        try
        {
            _logger.LogInformation("Connecting to Swift D-Bus at {Address}", address);

            // Check if simulated mode
            if (_configuration["Swift:Simulate"] == "true")
            {
                _logger.LogWarning("Running in SIMULATED mode (Swift:Simulate=true)");
                _isConnected = true;
                await NotifyConnectionChanged(true);
                return true;
            }

            _connection = new Connection(address);
            await _connection.ConnectAsync();

            // Subscribe to Swift signals
            await SubscribeToSignalsAsync();

            _isConnected = true;
            _logger.LogInformation("Connected to Swift D-Bus successfully");
            await NotifyConnectionChanged(true);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Swift D-Bus - will retry in background");
            _isConnected = false;
            await NotifyConnectionChanged(false);
            StartReconnectTimer();
            return false;
        }
    }

    private void StartReconnectTimer()
    {
        _reconnectTimer?.Dispose();
        _reconnectTimer = new Timer(async _ =>
        {
            if (!_isConnected)
            {
                _logger.LogInformation("Attempting to reconnect to Swift D-Bus...");
                await ConnectAsync();
            }
        }, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }

    private async Task SubscribeToSignalsAsync()
    {
        if (_connection == null) return;

        try
        {
            // Subscribe to text message received signal
            // Adjust interface/member names based on actual Swift D-Bus API
            await _connection.AddMatchAsync(new MatchRule
            {
                Type = MessageType.Signal,
                Interface = "org.swift.pilot.Text",
                Member = "TextMessageReceived"
            });

            // Subscribe to radio message signal
            await _connection.AddMatchAsync(new MatchRule
            {
                Type = MessageType.Signal,
                Interface = "org.swift.pilot.Radio",
                Member = "RadioMessageReceived"
            });

            // Subscribe to position updates
            await _connection.AddMatchAsync(new MatchRule
            {
                Type = MessageType.Signal,
                Interface = "org.swift.pilot.Aircraft",
                Member = "PositionUpdated"
            });

            // Add signal handler
            _connection.AddMethodHandler(OnDbusSignalReceived);

            _logger.LogInformation("Subscribed to Swift D-Bus signals");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to Swift D-Bus signals");
        }
    }

    private ValueTask OnDbusSignalReceived(Message message, MessageHandlerOptions options)
    {
        try
        {
            var member = message.MemberAsString;
            _logger.LogDebug("Received D-Bus signal: {Member}", member);

            switch (member)
            {
                case "TextMessageReceived":
                    HandleTextMessage(message);
                    break;
                case "RadioMessageReceived":
                    HandleRadioMessage(message);
                    break;
                case "PositionUpdated":
                    HandlePositionUpdate(message);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling D-Bus signal");
        }

        return default;
    }

    private void HandleTextMessage(Message message)
    {
        try
        {
            var reader = new Reader(message);
            var from = reader.ReadString();
            var text = reader.ReadString();

            _logger.LogInformation("Text message from {From}: {Text}", from, text);

            _hubContext.Clients.All.SendAsync("ReceiveMessage", new
            {
                type = "private",
                from,
                content = text,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse text message");
        }
    }

    private void HandleRadioMessage(Message message)
    {
        try
        {
            var reader = new Reader(message);
            var text = reader.ReadString();
            var frequency = reader.ReadInt32();

            _logger.LogInformation("Radio message on {Frequency}: {Text}", frequency, text);

            _hubContext.Clients.All.SendAsync("ReceiveMessage", new
            {
                type = "radio",
                content = text,
                frequency,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse radio message");
        }
    }

    private void HandlePositionUpdate(Message message)
    {
        _logger.LogDebug("Position updated (not broadcasting to reduce traffic)");
    }

    private void HandlePositionUpdate(Message message)
    {
        try
        {
            var reader = new Reader(message);
            var lat = reader.ReadDouble();
            var lon = reader.ReadDouble();
            var alt = reader.ReadDouble();

            _logger.LogDebug("Position updated: {Lat}, {Lon}, {Alt}", lat, lon, alt);

            _hubContext.Clients.All.SendAsync("PositionUpdated", new
            {
                latitude = lat,
                longitude = lon,
                altitude = alt,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse position update");
        }
    }

    public async Task DisconnectAsync()
    {
        _reconnectTimer?.Dispose();
        if (_connection != null)
        {
            await _connection.DisconnectAsync();
            _connection.Dispose();
            _connection = null;
        }
        _isConnected = false;
        await NotifyConnectionChanged(false);
    }

    public async Task<AircraftState?> GetAircraftStateAsync()
    {
        if (!_isConnected) return null;

        try
        {
            // Simulated mode
            if (_configuration["Swift:Simulate"] == "true")
            {
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

            if (_connection == null) return null;

            // Call Swift D-Bus method to get aircraft position
            var message = Message.CreateMethodCall(
                destination: "org.swift.pilot",
                path: "/org/swift/pilot/Aircraft",
                @interface: "org.swift.pilot.Aircraft",
                member: "GetPosition"
            );

            var reply = await _connection.CallMethodAsync(message);
            var reader = new Reader(reply);

            var lat = reader.ReadDouble();
            var lon = reader.ReadDouble();
            var alt = reader.ReadDouble();
            var gs = reader.ReadDouble();
            var hdg = reader.ReadDouble();
            var callsign = reader.ReadString();

            _connectedServer = "VATSIM"; // Could get from another D-Bus call

            return new AircraftState
            {
                Callsign = callsign,
                Latitude = lat,
                Longitude = lon,
                Altitude = alt,
                GroundSpeed = gs,
                Heading = hdg,
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
            _logger.LogInformation("Sending private message to {Recipient}", recipient);

            if (_configuration["Swift:Simulate"] == "true")
            {
                await Task.Delay(10);
                await _hubContext.Clients.All.SendAsync("MessageSent", new { type = "private", recipient, message, success = true, timestamp = DateTime.UtcNow });
                return true;
            }

            if (_connection == null) return false;

            var msg = Message.CreateMethodCall(
                destination: "org.swift.pilot",
                path: "/org/swift/pilot/Text",
                @interface: "org.swift.pilot.Text",
                member: "SendPrivateMessage"
            );

            var writer = msg.GetBodyWriter();
            writer.WriteString(recipient);
            writer.WriteString(message);

            await _connection.CallMethodAsync(msg);

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

            if (_configuration["Swift:Simulate"] == "true")
            {
                await Task.Delay(10);
                await _hubContext.Clients.All.SendAsync("MessageSent", new { type = "radio", message, success = true, timestamp = DateTime.UtcNow });
                return true;
            }

            if (_connection == null) return false;

            var msg = Message.CreateMethodCall(
                destination: "org.swift.pilot",
                path: "/org/swift/pilot/Radio",
                @interface: "org.swift.pilot.Radio",
                member: "SendRadioMessage"
            );

            var writer = msg.GetBodyWriter();
            writer.WriteString(message);

            await _connection.CallMethodAsync(msg);

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

    public void Dispose()
    {
        _reconnectTimer?.Dispose();
        _connection?.Dispose();
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
