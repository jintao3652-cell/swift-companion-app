#!/bin/bash

echo "============================================"
echo "Swift Companion - Bridge + Cloudflare Tunnel Launcher"
echo "============================================"
echo ""

# Check .NET
if ! command -v dotnet &> /dev/null; then
    echo "ERROR: .NET SDK not found"
    exit 1
fi

# Check cloudflared
if ! command -v cloudflared &> /dev/null; then
    echo "WARNING: cloudflared not found. Installing..."
    echo "Download from: https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/"
    echo ""
    echo "Or install via:"
    echo "  Ubuntu/Debian: wget https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64.deb && sudo dpkg -i cloudflared-linux-amd64.deb"
    echo "  Arch: yay -S cloudflared-bin"
    echo ""
    exit 1
fi

# Start Bridge Service
echo "[1/2] Starting Bridge Service..."
cd bridge-service/linux
dotnet run --configuration Release &
BRIDGE_PID=$!
sleep 3
echo "Bridge running on http://localhost:5000 (PID: $BRIDGE_PID)"
echo ""

# Start Cloudflare Tunnel
echo "[2/2] Starting Cloudflare Tunnel..."
cd ../..

if [ -f "$HOME/.cloudflared/config.yml" ]; then
    echo "Using existing tunnel configuration"
    cloudflared tunnel run &
    CF_PID=$!
else
    echo "No tunnel configured. Starting quick tunnel..."
    echo "NOTE: Quick tunnels give random URLs each time"
    echo "For permanent URL, run: cloudflared tunnel login"
    echo ""
    cloudflared tunnel --url http://localhost:5000 &
    CF_PID=$!
fi

echo ""
echo "============================================"
echo "Services Started!"
echo "============================================"
echo ""
echo "Bridge Service: http://localhost:5000"
echo "Cloudflare Tunnel: Check above for public URL"
echo ""
echo "API Endpoints:"
echo "  GET  /api/status"
echo "  GET  /api/swift/state"
echo ""
echo "To stop: Press Ctrl+C"
echo ""

# Trap Ctrl+C to cleanup
trap "echo ''; echo 'Stopping services...'; kill $BRIDGE_PID $CF_PID 2>/dev/null; exit 0" INT

# Wait
wait
