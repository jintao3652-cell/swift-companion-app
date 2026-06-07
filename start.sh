#!/bin/bash

echo "========================================"
echo "Swift Companion - Quick Start"
echo "========================================"
echo

# Check if dotnet is installed
if ! command -v dotnet &> /dev/null; then
    echo "ERROR: .NET SDK not found"
    echo "Install: https://dotnet.microsoft.com/download"
    exit 1
fi

# Check if swift is running
if ! pgrep -x "swift" > /dev/null; then
    echo "WARNING: swift is not running"
    echo "Please start swift first"
fi

echo "Starting Bridge Service..."
cd bridge-service/linux

# Restore and build if needed
if [ ! -d "bin" ]; then
    echo "Building Bridge Service..."
    dotnet restore
    dotnet build --configuration Release
fi

# Start Bridge
echo
echo "Bridge Service starting on http://localhost:5000"
echo "Press Ctrl+C to stop"
echo
dotnet run --configuration Release
