# Local Notifications Setup for AetherLink Flutter App

## Overview
The Flutter mobile app now uses `flutter_local_notifications` for both Android and iOS local notifications.

## Implementation Details

### 1. Dependencies
- `flutter_local_notifications: ^17.2.4` (already in pubspec.yaml)

### 2. Android Configuration
- **Permissions**: Added in [AndroidManifest.xml](android/app/src/main/AndroidManifest.xml:6)
  - `POST_NOTIFICATIONS` (required for Android 13+)
- **Notification Icon**: Uses `@mipmap/ic_launcher`
- **Runtime Permission**: Automatically requested on Android 13+ via the service

### 3. iOS Configuration
- **Permissions**: Automatically requested during initialization
  - Alert, Badge, Sound permissions requested
- **Info.plist**: No additional configuration needed for local notifications

### 4. Service Implementation
Location: [lib/services/push_notification_service.dart](lib/services/push_notification_service.dart)

**Key Features**:
- `initialize()`: Sets up notification channels and requests permissions
- `showNotification()`: Displays local notifications
- `showTestNotification()`: Test method to verify setup
- `_onNotificationTap()`: Handles notification tap events

### 5. Integration Points
- **Initialization**: [lib/main.dart:64](lib/main.dart#L64) - Called during app startup
- **Message Notifications**: [lib/providers/message_provider.dart:41-58](lib/providers/message_provider.dart#L41-L58)
  - Private messages trigger notifications
  - Radio messages with callsign mentions trigger notifications

## Testing

### Android Testing
1. Run the app on Android device/emulator (API 21+)
2. Grant notification permission when prompted (Android 13+)
3. Wait 2 seconds after launch - test notification should appear
4. Send a private message or mention the callsign - notification should appear

### iOS Testing
1. Run the app on iOS device/simulator (iOS 10+)
2. Grant notification permission when prompted
3. Wait 2 seconds after launch - test notification should appear
4. Send a private message or mention the callsign - notification should appear

## How to Run

```bash
# Android
flutter run -d android

# iOS
flutter run -d ios

# Build
flutter build apk --release
flutter build ios --release
```

## Notification Triggers
1. **Test Notification**: Appears 2 seconds after app initialization
2. **Private Messages**: Any private message received
3. **Callsign Mentions**: When callsign is mentioned in radio messages

## Troubleshooting

### Android
- Ensure `minSdkVersion >= 21`
- Check notification settings in device Settings > Apps > VATSIM Companion
- For Android 13+, permission must be granted

### iOS
- Check notification settings in device Settings > Notifications > VATSIM Companion
- Permissions must be granted during first launch
- Simulator may not show banners - use device for testing

## Files Modified
1. [lib/services/push_notification_service.dart](lib/services/push_notification_service.dart) - Added permission requests and test method
2. [lib/main.dart](lib/main.dart) - Added test notification trigger

## Next Steps
- Remove test notification call in production
- Add notification actions (reply, mark as read)
- Implement notification history
- Add custom notification sounds
