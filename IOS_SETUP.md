# iOS Setup Guide

Complete guide to build and deploy Swift Companion App on iOS.

## 📋 Prerequisites

### 1. Hardware & Software
- **Mac with Xcode 14+** (required for iOS development)
- **iOS device** with iOS 12.0+ (for testing)
- **Apple Developer Account** ($99/year for App Store, free for personal testing)

### 2. Install Tools
```bash
# Install Xcode from App Store
# Install Command Line Tools
xcode-select --install

# Install CocoaPods
sudo gem install cocoapods

# Install Flutter (if not already)
brew install flutter
flutter doctor
```

## 🔐 Certificate Setup

### Option 1: Free Personal Account (Development Only)

1. **Open Xcode**
   ```bash
   cd mobile-app/ios
   open Runner.xcworkspace
   ```

2. **Sign in with Apple ID**
   - Xcode → Settings → Accounts
   - Click `+` → Add Apple ID
   - Sign in with your Apple ID

3. **Configure Signing**
   - Select `Runner` target
   - Go to "Signing & Capabilities"
   - Check "Automatically manage signing"
   - Select your Team (Personal Team)
   - Change Bundle Identifier to unique:
     ```
     com.yourname.swiftcompanion
     ```

4. **Limitations**
   - ⚠️ Only 3 apps can be installed at once
   - ⚠️ Apps expire after 7 days (need re-install)
   - ⚠️ Cannot use Push Notifications
   - ⚠️ Cannot publish to App Store

### Option 2: Paid Developer Account (Recommended)

1. **Join Apple Developer Program**
   - Visit https://developer.apple.com/programs/
   - Enroll ($99/year)
   - Wait for approval (1-2 days)

2. **Create App ID**
   - Go to https://developer.apple.com/account
   - Certificates, IDs & Profiles → Identifiers
   - Click `+` → App IDs → App
   - Description: `Swift Companion`
   - Bundle ID: `com.swift.companion` (explicit)
   - Capabilities: Check
     - ✅ Push Notifications
     - ✅ Background Modes
     - ✅ Network Extensions (for local network)

3. **Create Certificates**

   **Development Certificate**:
   ```bash
   # Generate CSR
   # Keychain Access → Certificate Assistant → Request from CA
   # Save to disk: CertificateSigningRequest.certSigningRequest
   ```
   - Upload CSR to developer.apple.com
   - Download `ios_development.cer`
   - Double-click to install

   **Distribution Certificate** (for App Store):
   - Same process but choose "Distribution" certificate
   - Download `ios_distribution.cer`

4. **Create Provisioning Profiles**

   **Development Profile**:
   - Profiles → `+` → iOS App Development
   - Select App ID: `Swift Companion`
   - Select Certificate (your development cert)
   - Select Devices (register your test devices first)
   - Name: `Swift Companion Development`
   - Download and double-click to install

   **App Store Profile**:
   - Profiles → `+` → App Store
   - Select App ID: `Swift Companion`
   - Select Distribution Certificate
   - Name: `Swift Companion App Store`
   - Download and double-click to install

## 🛠️ Build Configuration

### 1. Update Bundle ID in Xcode
```bash
cd mobile-app/ios
open Runner.xcworkspace
```

In Xcode:
- Runner target → General
- Bundle Identifier: `com.swift.companion` (or your registered ID)
- Team: Select your team
- Signing: Select provisioning profile

### 2. Enable Capabilities
Runner target → Signing & Capabilities → `+ Capability`:
- ✅ Push Notifications
- ✅ Background Modes
  - Remote notifications
  - Background fetch
  - Background processing

### 3. Update Info.plist (Already Done)
Required permissions:
- ✅ Camera (for QR scanning)
- ✅ Local Network (for bridge discovery)
- ✅ Background Modes

## 📱 Build & Install

### Development Build (via Xcode)

1. **Connect iPhone via USB**
2. **Trust Computer** on iPhone
3. **Select Device** in Xcode toolbar
4. **Run** (⌘R) or click Play button
5. **First Run**: iPhone Settings → General → VPN & Device Management → Trust Developer

### Development Build (via Flutter)

```bash
cd mobile-app

# Build for device
flutter build ios --release

# Or run directly
flutter run -d <device-id>

# List devices
flutter devices
```

### TestFlight (Beta Testing)

1. **Archive Build**
   ```bash
   cd mobile-app
   flutter build ipa --release
   ```

2. **Upload to App Store Connect**
   - Open Xcode
   - Window → Organizer
   - Select archive → Distribute App → App Store Connect
   - Upload

3. **Configure in App Store Connect**
   - https://appstoreconnect.apple.com
   - My Apps → Swift Companion → TestFlight
   - Add Internal/External testers
   - Submit for review (external only)

4. **Invite Testers**
   - Copy invite link
   - Testers install TestFlight app
   - Accept invite

### App Store Release

1. **Prepare Metadata**
   - App name, description, keywords
   - Screenshots (6.5", 5.5" required)
   - Privacy policy URL
   - Support URL

2. **Submit for Review**
   - App Store Connect → My Apps
   - Select version → Submit for Review
   - Answer compliance questions
   - Wait 1-3 days for review

3. **Approval & Release**
   - Auto-release or manual release after approval

## 🔒 Push Notifications Setup (Optional)

### 1. Create APNs Key
- developer.apple.com → Keys → `+`
- Name: `Swift Companion APNs`
- Enable: Apple Push Notifications service (APNs)
- Download `.p8` file
- Note: Key ID and Team ID

### 2. Configure Firebase (if using FCM)
```bash
# Add iOS app in Firebase Console
# Download GoogleService-Info.plist
cp GoogleService-Info.plist mobile-app/ios/Runner/
```

### 3. Upload APNs Key to Firebase
- Firebase Console → Project Settings → Cloud Messaging
- Apple app configuration → Upload APNs key (.p8)

## 🧪 Testing Checklist

- [ ] App launches successfully
- [ ] Camera permission for QR scanning
- [ ] Network permission for bridge connection
- [ ] Pairing with bridge service works
- [ ] Messages send/receive in foreground
- [ ] Messages receive in background
- [ ] App doesn't crash after backgrounding
- [ ] Notifications display correctly
- [ ] App works after re-opening

## ❗ Common Issues

### "Untrusted Developer"
- iPhone Settings → General → VPN & Device Management
- Tap developer name → Trust

### "Could not launch app"
- Check device is unlocked
- Check provisioning profile includes this device
- Clean build: Product → Clean Build Folder (⇧⌘K)

### "Provisioning profile expired"
- Renew in developer.apple.com
- Download new profile
- Re-select in Xcode

### Pod install fails
```bash
cd mobile-app/ios
rm Podfile.lock
rm -rf Pods
pod install --repo-update
```

### Xcode signing errors
```bash
# Delete derived data
rm -rf ~/Library/Developer/Xcode/DerivedData

# Re-download provisioning profiles
cd ~/Library/MobileDevice/Provisioning\ Profiles
rm *
# Re-download from developer.apple.com
```

## 💰 Costs

| Item | Cost | Frequency |
|------|------|-----------|
| **Free Testing** | $0 | - |
| Developer Account | $99 | Yearly |
| App Store listing | $0 | Included |
| TestFlight | $0 | Included |

## 📚 Resources

- [Apple Developer](https://developer.apple.com)
- [App Store Connect](https://appstoreconnect.apple.com)
- [Flutter iOS Setup](https://docs.flutter.dev/deployment/ios)
- [Xcode Help](https://developer.apple.com/documentation/xcode)
- [TestFlight Guide](https://developer.apple.com/testflight/)

## 🚀 Quick Start (Free Testing)

**Fastest way to test on your iPhone**:

```bash
# 1. Install Xcode from App Store
# 2. Sign in with Apple ID in Xcode → Settings → Accounts

# 3. Open project
cd mobile-app/ios
open Runner.xcworkspace

# 4. In Xcode:
#    - Select Runner target
#    - Change Bundle ID to: com.YOURNAME.swiftcompanion
#    - Enable "Automatically manage signing"
#    - Select your Personal Team
#    - Connect iPhone
#    - Click Run (⌘R)

# 5. On iPhone:
#    Settings → General → VPN & Device Management → Trust
```

Done! App is now running on your iPhone. Re-install every 7 days.

---

**For Production**: Get paid developer account and follow full certificate setup above.

### "Untrusted Developer" on Device
**Solution**: Settings → General → VPN & Device Management → Trust

### "Could not launch" in Xcode
**Solution**: 
- Clean build folder (Shift+⌘K)
- Delete derived data
- Restart Xcode

### Provisioning Profile Mismatch
**Solution**:
- Check Bundle ID matches App ID
- Ensure profile contains selected device
- Refresh profiles in Xcode

### CocoaPods Issues
**Solution**:
```bash
cd mobile-app/ios
rm -rf Pods Podfile.lock
pod install --repo-update
```

### App Crashes on Launch
**Solution**:
- Check Xcode console for crash logs
- Verify all permissions in Info.plist
- Ensure all required frameworks are linked

## 📊 Build Variants

### Debug Build
```bash
flutter build ios --debug
# Enables hot reload, debugging, larger size
```

### Release Build
```bash
flutter build ios --release
# Optimized, no debugging, smaller size
```

### Profile Build
```bash
flutter build ios --profile
# Performance profiling enabled
```

## 🔍 Debugging

### View Logs in Xcode
- Window → Devices and Simulators
- Select device → Open Console
- Filter: Process = Runner

### Flutter Logs
```bash
flutter logs
```

### Crash Reports
- Xcode → Window → Organizer → Crashes
- Or: Settings → Privacy → Analytics → Analytics Data

## 📦 Automated Build (CI/CD)

### GitHub Actions (Requires Self-Hosted macOS Runner)

```yaml
name: Build iOS

on:
  push:
    branches: [ main ]

jobs:
  build-ios:
    runs-on: macos-latest
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup Flutter
      uses: subosito/flutter-action@v2
      with:
        flutter-version: '3.24.0'
    
    - name: Install CocoaPods
      run: |
        cd mobile-app/ios
        pod install
    
    - name: Build IPA
      env:
        MATCH_PASSWORD: ${{ secrets.MATCH_PASSWORD }}
      run: |
        cd mobile-app
        flutter build ipa --release \
          --export-options-plist=ios/ExportOptions.plist
    
    - name: Upload to TestFlight
      env:
        APP_STORE_CONNECT_API_KEY: ${{ secrets.ASC_API_KEY }}
      run: |
        xcrun altool --upload-app \
          --type ios \
          --file build/ios/ipa/*.ipa \
          --apiKey $APP_STORE_CONNECT_API_KEY
```

### Fastlane (Advanced)
```bash
# Install fastlane
sudo gem install fastlane

cd mobile-app/ios
fastlane init

# Configure lanes in Fastfile
# fastlane beta - Upload to TestFlight
# fastlane release - Submit to App Store
```

## 📝 Checklist for App Store Submission

- [ ] Bundle ID registered in App ID
- [ ] Distribution certificate created
- [ ] App Store provisioning profile created
- [ ] App icon set (1024x1024)
- [ ] Launch screen configured
- [ ] All required device screenshots
- [ ] Privacy policy URL (required)
- [ ] Support URL
- [ ] App description and keywords
- [ ] Content rating completed
- [ ] Export compliance answered
- [ ] Test on real device
- [ ] No crashes or critical bugs
- [ ] App size < 4GB

## 🔗 Useful Links

- **Apple Developer**: https://developer.apple.com
- **App Store Connect**: https://appstoreconnect.apple.com
- **Flutter iOS Deployment**: https://docs.flutter.dev/deployment/ios
- **Xcode Download**: https://developer.apple.com/xcode/
- **CocoaPods**: https://cocoapods.org
- **Fastlane**: https://fastlane.tools

## 💰 Cost Summary

| Item | Cost | Frequency |
|------|------|-----------|
| Apple Developer Account | $99 | Per year |
| Mac (if you don't have one) | $999+ | One-time |
| iOS Device (testing) | $429+ | One-time |
| **Total (first year)** | **~$1,527+** | - |
| **Renewal** | **$99** | Annual |

**Free Alternative**: Use personal Apple ID for development testing only (limitations apply).

---

**Note**: This guide assumes you're building the app yourself. For distribution to others, an Apple Developer account is required.
