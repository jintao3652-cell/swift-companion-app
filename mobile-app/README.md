# VATSIM Companion Mobile App

Flutter 移动应用，支持 Android 和 iOS，用于远程监控和控制 swift 连飞软件。

## 功能特性

✅ **设备配对**
- 6 位数配对码输入
- QR 码扫描（即将支持）
- 自动保存配对信息
- 启动时自动重连

✅ **实时消息**
- 接收私信
- 接��频率消息
- 发送消息（私信/频率）
- Callsign 提及高亮
- 消息历史查询

✅ **飞机状态**
- 实时位置、高度、速度
- 航向和垂直速度
- COM 频率显示
- 应答机代码
- 在地/空中状态

✅ **推送通知**
- 私信通知
- Callsign 提及
- SELCAL 提醒
- 后台通知

✅ **快捷指令**
- 预设消息模板
- 常用 ATC 指令
- 一键发送

## 技术栈

- **框架**: Flutter 3.x
- **状态管理**: Riverpod 2.4+
- **网络**: 
  - Dio (REST API)
  - SignalR (.NET Core)
- **推送**: Firebase Cloud Messaging
- **本地存储**: SharedPreferences + Hive
- **UI**: Material Design 3

## 项目结构

```
lib/
├── main.dart                          # 应用入口
├── config/
│   └── theme.dart                     # 主题配置
├── models/
│   ├── message.dart                   # 消息模型
│   ├── aircraft_state.dart            # 飞机状态模型
│   └── connection_state.dart          # 连接状态模型
├── services/
│   ├── websocket_service.dart         # WebSocket 服务
│   ├── bridge_api_service.dart        # REST API 服务
│   ├── push_notification_service.dart # 推送通知服务
│   └── storage_service.dart           # 本地存储服务
├── providers/
│   ├── connection_provider.dart       # 连接状态管理
│   ├── message_provider.dart          # 消息管理
│   └── aircraft_state_provider.dart   # 飞机状态管理
└── screens/
    ├── pairing/
    │   └── pairing_screen.dart        # 配对界面
    ├── home/
    │   └── home_screen.dart           # 主页
    ├── chat/
    │   └── chat_screen.dart           # 聊天界面
    └── status/
        └── aircraft_status_screen.dart # 状态界面
```

## 快速开始

### 前置要求

- Flutter SDK 3.0.0 或更高版本
- Dart SDK 3.0.0 或更高版本
- Android Studio / Xcode
- Firebase 项目（用于推送通知）

### 安装依赖

```bash
cd mobile-app
flutter pub get
```

### 配置 Firebase

#### Android

1. 在 [Firebase Console](https://console.firebase.google.com/) 创建项目
2. 添加 Android 应用
3. 下载 `google-services.json`
4. 放置到 `android/app/` 目录

#### iOS

1. 添加 iOS 应用到 Firebase 项目
2. 下载 `GoogleService-Info.plist`
3. 放置到 `ios/Runner/` 目录
4. 在 Xcode 中添加到项目

### 运行应用

#### 开发模式

```bash
# 查看可用设备
flutter devices

# 运行到指定设备
flutter run -d <device-id>

# Android 模拟器
flutter run -d emulator-5554

# iOS 模拟器
flutter run -d iPhone-15-Pro
```

#### 热重载

开发过程中按 `r` 热重载，按 `R` 热重启。

### 构建发布版本

#### Android APK

```bash
flutter build apk --release
```

输出: `build/app/outputs/flutter-apk/app-release.apk`

#### Android App Bundle (推荐)

```bash
flutter build appbundle --release
```

输出: `build/app/outputs/bundle/release/app-release.aab`

#### iOS

```bash
flutter build ios --release
```

然后在 Xcode 中打开项目并 Archive。

## 配置

### Android 配置

#### `android/app/build.gradle`

```gradle
android {
    compileSdkVersion 34
    
    defaultConfig {
        applicationId "com.vatsim.companion"
        minSdkVersion 26
        targetSdkVersion 34
        versionCode 1
        versionName "1.0.0"
    }
}
```

#### 权限 (`android/app/src/main/AndroidManifest.xml`)

```xml
<uses-permission android:name="android.permission.INTERNET"/>
<uses-permission android:name="android.permission.CAMERA"/> <!-- 用于 QR 扫描 -->
<uses-permission android:name="android.permission.VIBRATE"/>
```

### iOS 配置

#### `ios/Runner/Info.plist`

```xml
<key>NSCameraUsageDescription</key>
<string>需要相机权限以扫描 QR 码</string>

<key>NSLocalNetworkUsageDescription</key>
<string>需要访问本地网络以连接 Bridge 服务</string>
```

#### 最低版本

`ios/Podfile`:
```ruby
platform :ios, '13.0'
```

## 使用说明

### 首次配对

1. **启动 Bridge 服务**
   - 在 PC 上运行 Bridge
   - 调用 `POST /api/pairing/start` 获取配对码

2. **打开 App**
   - 首次启动会显示配对界面

3. **输入信息**
   - Bridge 地址: `192.168.1.100:5000` (你的电脑 IP)
   - 配对码: 6 位数字

4. **完成配对**
   - App 会自动连接并保存配对信息
   - 下次启动自动重连

### Android 模拟器连接本地 Bridge

如果 Bridge 运行在开发机上:
```
地址使用: 10.0.2.2:5000
```

### iOS 模拟器连接本地 Bridge

```
地址使用: localhost:5000
```

### 真机连接本地 Bridge

确保手机和电脑在同一网络，使用电脑的局域网 IP:
```
地址使用: 192.168.1.100:5000
```

查看电脑 IP:
```bash
# Windows
ipconfig

# macOS/Linux
ifconfig
```

## 主要功能

### 消息功能

#### 接收消息

- 自动接收所有私信
- 接收包含你 Callsign 的频率消息
- 消息实时显示

#### 发送消息

```dart
// 频率消息
await messagesNotifier.sendRadioMessage('Request taxi');

// 私信
await messagesNotifier.sendPrivateMessage('ABC123', 'Hello!');
```

#### 消息历史

```dart
// 加载历史
await messagesNotifier.loadHistory();

// 按类型过滤
final privateMessages = messagesNotifier.getMessagesByType(MessageType.private);
```

### 状态监控

#### 飞机状态

```dart
// 请求更新
await aircraftStateNotifier.requestUpdate();

// 获取状态
final state = ref.watch(aircraftStateProvider);
```

#### 连接状态

```dart
final connectionState = ref.watch(connectionProvider);
final isConnected = connectionState.isConnected;
final callsign = connectionState.callsign;
```

### 推送通知

#### 配置

推送通知会在以下情况触发:
- 收到私信
- 频率消息中提到你的 Callsign
- 收到 SELCAL

#### 处理通知点击

在 `push_notification_service.dart` 的 `_handleMessageOpenedApp` 方法中自定义。

## 调试

### Flutter DevTools

```bash
flutter pub global activate devtools
flutter pub global run devtools
```

### 日志查看

```bash
# 实时日志
flutter logs

# 过滤日志
flutter logs | grep "VATSIM"
```

### 调试 WebSocket

在 `websocket_service.dart` 中已包含详细日志:
```dart
logging: (level, message) => debugPrint('SignalR: $message')
```

### 调试 API 请求

在 `bridge_api_service.dart` 中启用 Dio 拦截器:
```dart
if (kDebugMode) {
  _dio.interceptors.add(LogInterceptor(
    requestBody: true,
    responseBody: true,
  ));
}
```

## 性能优化

### 减小 APK 大小

```bash
flutter build apk --release --split-per-abi
```

会生成三个 APK:
- `app-armeabi-v7a-release.apk` (32-bit ARM)
- `app-arm64-v8a-release.apk` (64-bit ARM)
- `app-x86_64-release.apk` (x86 64-bit)

### 启用混淆

在 `android/app/build.gradle`:
```gradle
buildTypes {
    release {
        minifyEnabled true
        shrinkResources true
        proguardFiles getDefaultProguardFile('proguard-android.txt'), 'proguard-rules.pro'
    }
}
```

## 常见问题

### WebSocket 连接失败

**症状**: App 显示 "Disconnected"

**解决方案**:
1. 检查 Bridge 是否运行 (访问 http://bridge-ip:5000)
2. 检查防火墙设置
3. 确认网络连接
4. 查看 App 日志

### 推送通知不工作

**检查项**:
1. Firebase 配置文件是否正确放置
2. FCM Server Key 是否配置到 Bridge
3. App 是否请求了通知权限
4. 设备是否支持 Google Play Services (Android)

### Android 模拟器无法连接

使用 `10.0.2.2` 代替 `localhost`:
```
Bridge 地址: 10.0.2.2:5000
```

### iOS 编译失败

```bash
cd ios
pod deintegrate
pod install
cd ..
flutter clean
flutter pub get
flutter run
```

### 签名问题 (iOS)

在 Xcode 中:
1. 选择 Runner target
2. Signing & Capabilities
3. 选择你的 Team
4. 让 Xcode 自动管理签名

## 测试

### 单元测试

```bash
flutter test
```

### 集成测试

```bash
flutter drive --target=test_driver/app.dart
```

### Widget 测试

```bash
flutter test test/widget_test.dart
```

## 发布

### Android (Google Play)

1. 生成签名密钥
```bash
keytool -genkey -v -keystore release.jks -keyalg RSA -keysize 2048 -validity 10000 -alias release
```

2. 配置 `android/key.properties`
```properties
storePassword=<password>
keyPassword=<password>
keyAlias=release
storeFile=release.jks
```

3. 构建 App Bundle
```bash
flutter build appbundle --release
```

4. 上传到 Google Play Console

### iOS (App Store)

1. 在 Xcode 中配置签名
2. Archive 应用
3. 通过 Xcode Organizer 上传到 App Store Connect
4. 提交审核

## 路线图

### v1.1 (即将推出)
- [ ] QR 码扫描配对
- [ ] 在线 ATC 列表
- [ ] 附近飞机显示
- [ ] 飞行计划查看

### v1.2
- [ ] 地图视图
- [ ] 语音转文字
- [ ] 自定义快捷指令
- [ ] 消息搜索

### v2.0
- [ ] 离线模式
- [ ] 多语言支持
- [ ] 主题自定义

## 贡献

欢迎贡献代码！请遵循 [CONTRIBUTING.md](../CONTRIBUTING.md)。

## 许可证

MIT License

## 支持

- GitHub Issues
- VATSIM 开发者论坛
- Discord

## 致谢

- Flutter Team
- VATSIM Network
- swift 开发者
- 所有贡献者
