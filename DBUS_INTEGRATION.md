# D-Bus Integration Guide

## Current Status

Swift D-Bus integration is **NOT IMPLEMENTED** - currently using simulated data.

## Implementation Checklist

### 1. Study Swift D-Bus API
- [ ] Read Swift documentation for D-Bus interface
- [ ] Identify service name (e.g., `org.swift.pilot`)
- [ ] Identify object paths (e.g., `/org/swift/Pilot`)
- [ ] List available methods:
  - `SendTextMessage(recipient: string, message: string)`
  - `SendRadioMessage(message: string, frequency: int)`
  - `GetAircraftPosition() -> (lat: double, lon: double, alt: double)`
  - `GetCallsign() -> string`
- [ ] List available signals:
  - `TextMessageReceived(from: string, message: string)`
  - `RadioMessageReceived(message: string, frequency: int)`
  - `PositionUpdated(lat: double, lon: double, alt: double)`

### 2. Update SwiftDbusService.cs

Replace placeholder implementation in `bridge-service/linux/Services/SwiftDbusService.cs`:

```csharp
using Tmds.DBus.Protocol;

public class SwiftDbusService
{
    private Connection? _connection;
    
    public async Task<bool> ConnectAsync(string address = "tcp:host=127.0.0.1,port=45000")
    {
        try
        {
            _connection = new Connection(address);
            await _connection.ConnectAsync();
            
            // Subscribe to signals
            await SubscribeToSignalsAsync();
            
            _isConnected = true;
            await NotifyConnectionChanged(true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "D-Bus connection failed");
            return false;
        }
    }
    
    private async Task SubscribeToSignalsAsync()
    {
        // Subscribe to TextMessageReceived signal
        await _connection.AddMatchAsync(new MatchRule
        {
            Type = MessageType.Signal,
            Interface = "org.swift.Pilot",
            Member = "TextMessageReceived"
        });
        
        // Handle incoming signals
        _connection.AddMethodHandler(OnSignalReceived);
    }
    
    private ValueTask OnSignalReceived(Message message, MessageHandlerOptions options)
    {
        // Parse signal and notify mobile app via SignalR
        return default;
    }
    
    public async Task<AircraftState?> GetAircraftStateAsync()
    {
        if (!_isConnected || _connection == null) return null;
        
        // Call Swift D-Bus method
        var message = Message.CreateMethodCall(
            destination: "org.swift.Pilot",
            path: "/org/swift/Pilot",
            @interface: "org.swift.Pilot",
            member: "GetAircraftPosition"
        );
        
        var reply = await _connection.CallMethodAsync(message);
        var reader = new Reader(reply);
        
        var lat = reader.ReadDouble();
        var lon = reader.ReadDouble();
        var alt = reader.ReadDouble();
        
        return new AircraftState
        {
            Latitude = lat,
            Longitude = lon,
            Altitude = alt,
            // ...
        };
    }
}
```

### 3. Test with Real Swift

```bash
# Check Swift D-Bus is running
netstat -tlnp | grep 45000

# Test D-Bus connection
dbus-send --session --dest=org.swift.Pilot \
  --type=method_call --print-reply \
  /org/swift/Pilot org.swift.Pilot.GetCallsign

# Monitor D-Bus signals
dbus-monitor "type='signal',interface='org.swift.Pilot'"
```

### 4. Known Issues

- **Security**: Tmds.DBus.Protocol 0.20.0 has high severity vulnerability
  - Update to latest version or use alternative library
- **Connection Resilience**: Add reconnection logic with exponential backoff
- **Error Handling**: Distinguish network errors vs Swift not running
- **Performance**: D-Bus calls should be async, avoid blocking SignalR hub

### 5. References

- Tmds.DBus docs: https://github.com/tmds/Tmds.DBus
- Swift docs: (add link when available)
- D-Bus specification: https://dbus.freedesktop.org/doc/dbus-specification.html

## Testing Without Swift

Current simulated mode allows mobile app development without Swift installation.
To toggle:

```csharp
// In SwiftDbusService.cs
public async Task<bool> ConnectAsync(...)
{
    if (Environment.GetEnvironmentVariable("SWIFT_SIMULATE") == "true")
    {
        // Use simulated data
        return true;
    }
    // Real D-Bus connection
}
```
