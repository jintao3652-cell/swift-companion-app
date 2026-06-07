#!/bin/bash

# Notification Setup Verification Script

echo "================================"
echo "Flutter Local Notifications Test"
echo "================================"
echo ""

# Check Flutter
echo "1. Checking Flutter installation..."
if ! command -v flutter &> /dev/null; then
    echo "❌ Flutter not found in PATH"
    exit 1
fi
echo "✅ Flutter found: $(flutter --version | head -1)"
echo ""

# Check dependencies
echo "2. Checking dependencies..."
if ! grep -q "flutter_local_notifications" pubspec.yaml; then
    echo "❌ flutter_local_notifications not found in pubspec.yaml"
    exit 1
fi
echo "✅ flutter_local_notifications found in pubspec.yaml"
echo ""

# Check Android manifest
echo "3. Checking Android configuration..."
if grep -q "POST_NOTIFICATIONS" android/app/src/main/AndroidManifest.xml; then
    echo "✅ POST_NOTIFICATIONS permission found"
else
    echo "⚠️  POST_NOTIFICATIONS permission not found (required for Android 13+)"
fi
echo ""

# Check iOS configuration
echo "4. Checking iOS configuration..."
if [ -f "ios/Runner/Info.plist" ]; then
    echo "✅ Info.plist exists"
else
    echo "❌ Info.plist not found"
fi
echo ""

# Check service implementation
echo "5. Checking service implementation..."
if [ -f "lib/services/push_notification_service.dart" ]; then
    echo "✅ push_notification_service.dart exists"
    if grep -q "requestNotificationsPermission" lib/services/push_notification_service.dart; then
        echo "✅ Android permission request implemented"
    fi
    if grep -q "requestPermissions" lib/services/push_notification_service.dart; then
        echo "✅ iOS permission request implemented"
    fi
else
    echo "❌ push_notification_service.dart not found"
fi
echo ""

echo "================================"
echo "Run the following commands to test:"
echo ""
echo "  # Install dependencies"
echo "  flutter pub get"
echo ""
echo "  # Run on Android"
echo "  flutter run -d android"
echo ""
echo "  # Run on iOS"
echo "  flutter run -d ios"
echo ""
echo "Expected behavior:"
echo "  1. App requests notification permission on first launch"
echo "  2. Test notification appears 2 seconds after launch"
echo "  3. Private messages trigger notifications"
echo "  4. Callsign mentions trigger notifications"
echo "================================"
