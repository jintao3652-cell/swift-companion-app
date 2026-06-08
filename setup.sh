#!/bin/bash

echo "============================================"
echo "Swift Companion App - Setup Script"
echo "============================================"
echo ""

# Check .NET
if ! command -v dotnet &> /dev/null; then
    echo "ERROR: .NET SDK not found"
    echo "Install: https://dotnet.microsoft.com/download"
    exit 1
fi
echo "✓ .NET SDK found"

# Check Flutter
if ! command -v flutter &> /dev/null; then
    echo "⚠ Flutter not found (needed for mobile app)"
    echo "Install: https://flutter.dev/docs/get-started/install"
else
    echo "✓ Flutter found"
fi

# Setup Bridge Service
echo ""
echo "[1/2] Setting up Bridge Service..."
cd bridge-service/linux

echo "Restoring packages..."
dotnet restore
if [ $? -ne 0 ]; then
    echo "ERROR: Failed to restore packages"
    exit 1
fi

echo "Building Bridge..."
dotnet build -c Release
if [ $? -ne 0 ]; then
    echo "ERROR: Failed to build Bridge"
    exit 1
fi

cd ../..
echo "✓ Bridge Service ready"

# Setup Flutter App
echo ""
echo "[2/2] Setting up Flutter App..."
cd mobile-app

if [ -f "pubspec.yaml" ]; then
    if command -v flutter &> /dev/null; then
        echo "Getting Flutter packages..."
        flutter pub get
        if [ $? -eq 0 ]; then
            echo "✓ Flutter app ready"
        else
            echo "⚠ Flutter pub get failed"
        fi
    else
        echo "⚠ Skipped (Flutter not installed)"
    fi
fi

cd ..

# Generate config template
echo ""
echo "Creating .env.example..."
cat > .env.example << 'EOF'
# Swift Companion Configuration

# Bridge Service
BRIDGE_PORT=5000
JWT_SECRET=CHANGE_THIS_TO_A_RANDOM_32_CHAR_STRING

# Firebase (Optional - for push notifications)
FCM_SERVER_KEY=YOUR_FCM_SERVER_KEY_HERE
EOF

echo ""
echo "============================================"
echo "Setup Complete!"
echo "============================================"
echo ""
echo "NEXT STEPS:"
echo ""
echo "1. Install Cloudflare Tunnel (optional):"
echo "   Ubuntu/Debian:"
echo "     wget https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64.deb"
echo "     sudo dpkg -i cloudflared-linux-amd64.deb"
echo ""
echo "   Arch Linux:"
echo "     yay -S cloudflared-bin"
echo ""
echo "2. Configure Bridge Service:"
echo "   - Edit bridge-service/linux/appsettings.json"
echo "   - Change JWT SecretKey"
echo ""
echo "3. Start services:"
echo "   ./start-bridge.sh    # Bridge + Cloudflare Tunnel"
echo ""
echo "4. Firebase Setup (for notifications):"
echo "   - Create project at console.firebase.google.com"
echo "   - Download config files"
echo "   - Place in mobile-app/android/app/ and mobile-app/ios/Runner/"
echo ""
