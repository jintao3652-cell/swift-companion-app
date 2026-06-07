# Swift Companion App

Mobile companion app for Swift pilot client on Linux. Enables real-time text messaging and aircraft monitoring from your phone while flying on VATSIM.

## 🚀 Features

- 📱 **Mobile App** (Android/iOS) - Stay connected while flying
- 💬 **Private Messaging** - Text chat with ATC and other pilots
- 📻 **Radio Messages** - View radio communications
- ✈️ **Aircraft Status** - Monitor your flight data in real-time
- 🎮 **Controller List** - See nearby ATC controllers with frequencies
- 🔔 **Push Notifications** - Never miss important messages
- 🔒 **Secure Pairing** - QR code or manual pairing with JWT authentication
- 🌐 **Cloudflare Tunnel** - Access from anywhere via HTTPS

## 📋 Requirements

### Bridge Service (Linux PC)
- Linux (Ubuntu 20.04+, Debian 11+, or similar)
- .NET 8.0 SDK or Runtime
- Swift pilot client installed and running
- Swift D-Bus API enabled

### Mobile App
- Android 6.0+ or iOS 12.0+
- Internet connection (Wi-Fi or mobile data)

## 🛠️ Quick Start

### 1️⃣ Install Bridge Service

**Automatic Start Script**:
```bash
./start.sh
```

**Manual Installation**:
```bash
cd bridge-service/linux
dotnet run
```

### 2️⃣ Build Mobile App

**Build APK**:
```bash
./build-apk.bat  # Windows
# or
cd mobile-app && flutter build apk --release  # Linux
```
Output: `mobile-app/build/app/outputs/flutter-apk/app-release.apk`

**Install on Phone**:
- Transfer APK to phone
- Enable "Install from Unknown Sources"
- Install APK

### 3️⃣ Pairing

1. Start Swift and connect to VATSIM
2. Launch Bridge Service (runs on http://0.0.0.0:5000)
3. Open mobile app → Generate pairing code
4. Enter code on Bridge Service API (POST /api/pairing/complete)
5. Connected! ✅

### 4️⃣ External Access (Optional)

**Use Cloudflare Tunnel for external access**:
```bash
cloudflared tunnel --url http://localhost:5000
```
Copy the `https://*.trycloudflare.com` URL to mobile app

## 📁 Project Structure

```
swift-companion-app/
├── bridge-service/
│   └── linux/                # .NET 8.0 Bridge Service
│       ├── SwiftBridge.csproj
│       ├── Program.cs
│       ├── Controllers/      # REST API endpoints
│       ├── Hubs/            # SignalR hub for real-time
│       └── Services/        # Swift D-Bus communication
├── mobile-app/              # Flutter mobile app
│   ├── lib/
│   │   ├── screens/        # UI screens
│   │   ├── services/       # API & WebSocket services
│   │   ├── providers/      # State management (Riverpod)
│   │   └── models/         # Data models
│   ├── android/
│   └── ios/
├── build-apk.bat          # Build Android APK
├── start.sh               # Linux startup script
└── README.md
```

## 🔧 Configuration

### Bridge Service
`bridge-service/linux/appsettings.json`:
```json
{
  "Port": "5000",
  "Jwt": {
    "SecretKey": "your-secret-key-min-32-chars-long",
    "ExpiryMinutes": 43200
  },
  "Pairing": {
    "CodeLength": 6,
    "ExpiryMinutes": 10
  },
  "Swift": {
    "DbusHost": "127.0.0.1",
    "DbusPort": 45000
  }
}
```

### Swift D-Bus
- Ensure Swift D-Bus service is running on port 45000
- Bridge connects via TCP: `tcp:host=127.0.0.1,port=45000`

## 🌐 API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/status` | GET | Bridge & Swift status |
| `/api/status/aircraft` | GET | Current aircraft state |
| `/api/pairing/start` | POST | Generate pairing code |
| `/api/pairing/complete` | POST | Complete pairing with code |
| `/api/messages` | GET | Recent messages |
| `/swifthub` | WebSocket | SignalR hub for real-time |

## 📱 Mobile App Features

### Screens
- **Pairing** - Connect to Bridge Service
- **Home** - Quick overview & recent messages
- **Chat** - Private & radio messages
- **Status** - Aircraft state & nearby controllers
- **Settings** - Connection management

### State Management
- Uses Riverpod for reactive state
- Automatic reconnection on connection loss
- Message persistence with local storage
- Foreground service for background connectivity

## 🔒 Security

- JWT authentication for mobile → bridge
- Short-lived pairing codes (10 min expiry)
- HTTPS support via Cloudflare Tunnel
- No sensitive data stored in app

## 🐛 Troubleshooting

**Bridge won't start**:
- Check Swift is running
- Verify .NET 8.0 is installed: `dotnet --version`
- Check port 5000 is available

**Mobile app can't connect**:
- Verify bridge is running (check http://localhost:5000)
- Check firewall allows port 5000
- Use Cloudflare Tunnel for external access

**No messages received**:
- Verify Swift D-Bus is accessible on port 45000
- Check Swift connected to VATSIM network
- Verify callsign matches in app and Swift

**D-Bus connection failed**:
- Check Swift D-Bus service: `netstat -tlnp | grep 45000`
- Verify D-Bus configuration in Swift settings

## 📦 Building from Source

### Bridge Service
```bash
cd bridge-service/linux
dotnet build -c Release
dotnet publish -c Release -r linux-x64 --self-contained
```

### Mobile App
```bash
cd mobile-app
flutter pub get
flutter build apk --release
```

## 🔧 Development Status

### ✅ Implemented
- Bridge Service architecture
- REST API endpoints
- SignalR hub for real-time communication
- JWT authentication
- Pairing service
- Message storage
- Mobile app UI

### ⏳ TODO
- Actual Swift D-Bus integration (currently simulated)
- Swift D-Bus signal monitoring
- Message receiving from Swift
- Real-time aircraft state updates

**Note**: D-Bus integration is currently placeholder. To complete:
1. Study Swift D-Bus API documentation
2. Implement `SwiftDbusService.cs` using `Tmds.DBus.Protocol`
3. Connect to Swift D-Bus endpoint
4. Subscribe to Swift signals

## 🤝 Contributing

1. Fork the repository
2. Create feature branch (`git checkout -b feature/amazing`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing`)
5. Open Pull Request

## 📄 License

MIT License - See LICENSE file for details

## 🔗 Links

- Swift: https://github.com/swift-project/pilotclient
- VATSIM: https://www.vatsim.net
- Cloudflare Tunnel: https://developers.cloudflare.com/cloudflare-one/connections/connect-apps/
- Tmds.DBus.Protocol: https://github.com/tmds/Tmds.DBus

## 💡 Tips

- Keep Bridge Service running while flying
- Use Cloudflare Tunnel for stable external access
- Enable notifications for important messages
- Check controller list for nearby ATC frequencies

---

Made for VATSIM pilots on Linux ✈️ by the community
