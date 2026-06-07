# 快速测试指南

## 问题修复完成 ✅

### 1. 符号链接错误 ✅
已通过 `flutter clean` 清理缓存

### 2. 后台保活 ✅
- 前台服务已配置
- 退到后台时连接保持

### 3. 自动重连 ✅
- 每 5 秒检查连接状态
- 断线后每 5 秒尝试重连
- 重连成功/失败都有通知

## 立即测试

### Swift 项目
```bash
cd swift-companion-app/mobile-app
flutter run
```

### VATSIM 项目
```bash
cd vatsim-companion-app/mobile-app
flutter run
```

### 选择设备
当看到：
```
[1]: Windows (windows)
[2]: Edge (edge)
```

**推荐选择**: 输入 `1` (Windows 桌面应用)

## 测试自动重连

### 步骤 1: 正常连接
1. 运行应用
2. 输入 Bridge 地址和配对码
3. 点击 "Connect"
4. 等待连接成功

### 步骤 2: 模拟断线
**方法 A**: 关闭 Bridge 服务
```bash
# 在 Bridge 服务终端按 Ctrl+C
```

**方法 B**: 断开网络
- Windows: 关闭 WiFi 或拔网线
- 等待几秒

### 步骤 3: 观察自动重连
1. 查看应用通知: "Connection lost, trying to reconnect..."
2. 查看控制台日志:
   ```
   Starting auto-reconnect timer (5s interval)
   Attempting to reconnect...
   Reconnection failed: [error]
   Attempting to reconnect...
   ```

### 步骤 4: 恢复连接
**方法 A**: 重启 Bridge
```bash
dotnet run
```

**方法 B**: 恢复网络

### 步骤 5: 验证重连成功
1. 查看通知: "Connection restored successfully"
2. 查看控制台:
   ```
   Reconnection successful
   Auto-reconnect timer stopped
   ```
3. 应用状态变为已连接

## 测试后台保活

### Android/iOS 设备
1. 连接成功后
2. 按 Home 键退到后台
3. 等待 5 分钟
4. 回到应用
5. ✅ 连接仍然活跃

### 通知栏
- 应该看到 "AetherLink" 前台服务通知
- 内容: "Connected — listening for messages"

## 预期行为

### 正常情况
```
✅ 连接成功
✅ 每 5 秒健康检查
✅ 控制台显示: "Health check: Connection OK"
```

### 断线情况
```
⚠️ 连接断开
⚠️ 通知: "Connection lost, trying to reconnect..."
⚠️ 每 5 秒尝试重连
⚠️ 控制台显示: "Attempting to reconnect..."
```

### 重连成功
```
✅ 连接恢复
✅ 通知: "Connection restored successfully"
✅ 停止重连计时器
✅ 控制台显示: "Reconnection successful"
```

## 控制台日志示例

### 成功场景
```
flutter: Connecting to bridge at http://192.168.1.100:5000
flutter: SignalR connected to http://192.168.1.100:5000
flutter: Starting connection health check (5s interval)
flutter: Health check: Connection OK
flutter: Health check: Connection OK
```

### 断线重连场景
```
flutter: SignalR connection closed: Connection lost
flutter: Starting auto-reconnect timer (5s interval)
flutter: Attempting to reconnect...
flutter: Reconnection failed: SocketException: Failed to connect
flutter: Attempting to reconnect...
flutter: Reconnection successful
flutter: Auto-reconnect timer stopped
```

## 常见问题

### Q: Windows 运行后看不到通知？
A: Windows 版本不显示移动端通知，这是正常的。在 Android/iOS 设备上测试。

### Q: 重连太频繁？
A: 可以修改间隔：
```dart
// connection_provider.dart 第 123 行
Timer.periodic(const Duration(seconds: 10), ...) // 改为 10 秒
```

### Q: 如何查看详细日志？
A: 运行时添加 `--verbose`:
```bash
flutter run --verbose
```

### Q: 如何停止自动重连？
A: 点击应用中的 "Disconnect" 按钮

## 性能影响

### CPU
- 健康检查: 每 5 秒执行一次轻量检查
- 几乎无 CPU 占用

### 内存
- 增加约 1-2 MB (计时器对象)

### 电池
- 前台服务: 轻微增加电池消耗
- 推荐在充电时使用

### 网络
- 每 5 秒可能产生小量流量
- Long Polling 模式下更明显

## 下一步

如果测试成功：
1. ✅ 在实际 Android/iOS 设备上测试
2. ✅ 测试长时间后台运行（30分钟+）
3. ✅ 测试网络切换场景
4. ✅ 准备发布版本

如果遇到问题：
1. 检查控制台日志
2. 查看 [BACKGROUND_KEEP_ALIVE.md](BACKGROUND_KEEP_ALIVE.md)
3. 调整重连间隔
