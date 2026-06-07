# VATSIM Companion Bridge Service

Bridge 服务是 VATSIM Companion App 的核心中转服务，运行在用户的 Windows PC 上，负责连接 vPilot 插件和移动端 App。

## 功能特性

✅ **双向通信**
- SignalR WebSocket 实时通信
- REST API 端点
- 与 vPilot 插件 HTTP 通信

✅ **认证与配对**
- 6 位数配对码
- QR 码扫描
- JWT Token 认证
- 自动过期保护

✅ **消息管理**
- 内存消息存储（最多 1000 条）
- 消息历史查询
- 按类型过滤
- 分页支持

✅ **推送通知**
- Firebase Cloud Messaging (FCM)
- 私信通知
- Callsign 提及通知
- SELCAL 通知

✅ **状态监控**
- 插件连接检测
- vPilot 状态查询
- 健康检查端点

## 架构

```
Mobile App (WebSocket) ←→ Bridge Service (HTTP) ←→ vPilot Plugin
                                    ↓
                          Firebase (Push Notifications)
```

## 文件结构

```
VatsimBridge/
├── Program.cs                              # 应用入口
├── appsettings.json                        # 配置文件
├── Controllers/
│   ├── StatusController.cs                 # 状态 API
│   ├── PairingController.cs                # 配对 API
│   ├── MessagesController.cs               # 消息历史 API
│   └── PluginEventController.cs            # 插件事件接收
├── Hubs/
│   └── VatsimHub.cs                        # SignalR Hub
├── Services/
│   ├── PluginCommunicationService.cs       # 插件通信
│   ├── PushNotificationService.cs          # 推送通知
│   ├── PairingService.cs                   # 配对与 JWT
│   └── MessageStorageService.cs            # 消息存储
└── Models/
    └── Dtos.cs                             # 数据模型
```

## 快速开始

### 前置要求

- .NET 7.0 SDK 或更高版本
- Windows 10/11
- vPilot 插件已安装并运行

### 编译和运行

#### 开发模式

```bash
cd VatsimBridge
dotnet restore
dotnet run
```

访问：http://localhost:5000

#### 发布模式

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

输出位置：`bin/Release/net7.0/win-x64/publish/`

### Docker 运行（可选）

```dockerfile
# 未来支持
```

## 配置

### appsettings.json

```json
{
  "Port": "5000",
  "Jwt": {
    "SecretKey": "YOUR_SECRET_KEY_HERE_MUST_BE_32_CHARS_OR_MORE",
    "Issuer": "VatsimCompanionBridge",
    "Audience": "VatsimCompanionApp",
    "ExpirationMinutes": "1440"
  },
  "PushNotification": {
    "FcmServerKey": "YOUR_FCM_SERVER_KEY",
    "EnablePush": true
  }
}
```

### 环境变量

可以通过环境变量覆盖配置：

```bash
# Windows
set Jwt__SecretKey=YourSecretKey
set PushNotification__FcmServerKey=YourFcmKey

# Linux/macOS
export Jwt__SecretKey=YourSecretKey
export PushNotification__FcmServerKey=YourFcmKey
```

## API 端点

### 基础 URL
```
http://localhost:5000
```

### 主要端点

#### 1. 状态检查

```http
GET /api/status
```

**响应**：
```json
{
  "bridgeVersion": "1.0.0",
  "vPilotConnected": true,
  "pluginConnected": true,
  "callsign": "ABC123",
  "uptime": 3600
}
```

#### 2. 健康检查

```http
GET /api/status/health
```

#### 3. 开始配对

```http
POST /api/pairing/start
```

**响应**：
```json
{
  "success": true,
  "pairingCode": "123456",
  "qrCode": "data:image/png;base64,...",
  "bridgeUrl": "http://localhost:5000",
  "expiresAt": "2026-06-02T12:10:00Z"
}
```

#### 4. 验证配对

```http
POST /api/pairing/verify
Content-Type: application/json

{
  "pairingCode": "123456",
  "deviceId": "device_123",
  "deviceName": "iPhone 15 Pro",
  "fcmToken": "fcm_token_here"
}
```

**响应**：
```json
{
  "success": true,
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "userId": "user_abc123",
  "expiresIn": 86400
}
```

#### 5. 获取消息历史

```http
GET /api/messages?type=private&limit=50&offset=0
```

#### 6. 清空消息

```http
DELETE /api/messages
```

## SignalR Hub

### 连接

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl('http://localhost:5000/vatsimhub', {
    accessTokenFactory: () => yourJwtToken
  })
  .build();

await connection.start();
```

### 服务器推送事件

```javascript
// 接收消息
connection.on('ReceiveMessage', (message) => {
  console.log('Message:', message);
});

// 飞机状态更新
connection.on('AircraftStateUpdated', (state) => {
  console.log('State:', state);
});

// 连接状态变化
connection.on('VPilotConnectionChanged', (status) => {
  console.log('vPilot status:', status);
});
```

### 客户端调用方法

```javascript
// 发送私信
await connection.invoke('SendPrivateMessage', 'ABC123', 'Hello');

// 发送频率消息
await connection.invoke('SendRadioMessage', 'Request taxi');

// 执行指令
await connection.invoke('ExecuteCommand', '.atis KJFK');

// 请求状态
await connection.invoke('RequestAircraftState');

// 注册推送 Token
await connection.invoke('RegisterPushToken', fcmToken, deviceId);
```

## 配对流程

### 1. 生成配对码

Bridge 启动后，调用：

```bash
curl -X POST http://localhost:5000/api/pairing/start
```

会在控制台显示：

```
===========================================
VATSIM Companion - Pairing Code
===========================================
  Code: 123456
  URL:  http://localhost:5000
===========================================
```

### 2. 移动端验证

用户在 App 中输入配对码，App 调用：

```bash
curl -X POST http://localhost:5000/api/pairing/verify \
  -H "Content-Type: application/json" \
  -d '{
    "pairingCode": "123456",
    "deviceId": "device_abc",
    "deviceName": "iPhone 15 Pro",
    "fcmToken": "fcm_token"
  }'
```

### 3. 获取 Token

验证成功后返回 JWT Token，用于后续所有请求。

### 4. 建立 WebSocket

使用 Token 连接 SignalR Hub，开始实时通信。

## 推送通知配置

### 获取 FCM Server Key

1. 访问 [Firebase Console](https://console.firebase.google.com/)
2. 选择项目
3. 项目设置 → Cloud Messaging
4. 复制 **服务器密钥**
5. 更新 `appsettings.json` 中的 `FcmServerKey`

### 测试推送

```bash
curl -X POST http://localhost:5000/api/plugin/event \
  -H "Content-Type: application/json" \
  -d '{
    "type": "message",
    "payload": {
      "messageType": "private",
      "from": "TEST123",
      "content": "Test notification"
    }
  }'
```

## 日志

### 查看实时日志

```bash
dotnet run --Logging:LogLevel:Default=Debug
```

### 日志级别

- `Debug` - 详细调试信息
- `Information` - 正常操作日志（默认）
- `Warning` - 警告信息
- `Error` - 错误信息

### 日志输出

- 控制台
- Debug Output (Visual Studio)

## 调试

### Visual Studio

1. 打开 `VatsimBridge.sln`
2. 按 F5 启动调试
3. 设置断点
4. 测试 API 端点

### VS Code

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": ".NET Core Launch",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/bin/Debug/net7.0/VatsimBridge.dll",
      "args": [],
      "cwd": "${workspaceFolder}",
      "stopAtEntry": false
    }
  ]
}
```

### Postman/Insomnia

使用 API 测试工具测试端点：

1. 导入 Swagger JSON
2. 测试各个端点
3. 验证响应

## 性能优化

### 内存管理

- 消息存储限制为 1000 条
- 自动清理过期配对码
- SignalR 连接池管理

### 网络优化

- HTTP 连接重用
- WebSocket 压缩
- 请求超时控制

### 并发处理

- 异步 I/O
- 线程安全集合
- 锁粒度优化

## 安全性

### 认证

- JWT Token 验证
- Token 过期检查
- 配对码单次使用

### 通信安全

- 本地通信（localhost）
- 生产环境使用 HTTPS
- CORS 策略控制

### 数据保护

- 不记录敏感信息
- 内存存储（不持久化到磁盘）
- 符合 VATSIM 隐私政策

## 常见问题

### 端口被占用

修改 `appsettings.json` 中的 `Port`：

```json
{
  "Port": "5001"
}
```

或使用命令行参数：

```bash
dotnet run --urls="http://localhost:5001"
```

### vPilot 插件无法连接

**检查项**：

1. 插件是否已加载（查看 vPilot Plugins 菜单）
2. 插件 HTTP 服务器是否运行（访问 http://localhost:8765/status）
3. 防火墙是否阻止
4. Bridge 日志中是否有错误

### SignalR 连接失败

**检查项**：

1. CORS 策略
2. Token 是否有效
3. WebSocket 支持
4. 网络连接

### 推送通知不工作

**检查项**：

1. FCM Server Key 是否正确
2. 移动端 FCM Token 是否已注册
3. Firebase 项目配置
4. 网络连接

## 部署

### Windows 服务

使用 NSSM（Non-Sucking Service Manager）：

```bash
nssm install VatsimBridge "C:\Path\To\VatsimBridge.exe"
nssm set VatsimBridge AppDirectory "C:\Path\To\"
nssm start VatsimBridge
```

### 开机自启动

1. 编译为单文件可执行程序
2. 创建快捷方式到启动文件夹
3. 或使用 Task Scheduler

### 防火墙规则

```powershell
New-NetFirewallRule -DisplayName "VATSIM Companion Bridge" `
  -Direction Inbound -Protocol TCP -LocalPort 5000 -Action Allow
```

## 更新日志

### v1.0.0 (2026-06-02)
- ✅ 初始版本
- ✅ JWT 认证
- ✅ 配对功能
- ✅ 消息存储
- ✅ 推送通知
- ✅ SignalR Hub

## 许可证

MIT License

## 支持

- GitHub Issues
- VATSIM 开发者论坛
- Discord
