# iOS Quick Start (5 Minutes)

最快速的方式在 iPhone 上运行 Swift Companion App。

## 🎯 免费方式（仅限个人测试）

### 前提条件
- ✅ Mac 电脑 + Xcode 14+
- ✅ iPhone (iOS 12+) + USB 数据线
- ✅ 免费 Apple ID

### 步骤

1. **打开项目**
   ```bash
   cd mobile-app/ios
   pod install
   open Runner.xcworkspace
   ```

2. **登录 Apple ID**
   - Xcode → Settings → Accounts → `+` 添加 Apple ID

3. **修改 Bundle ID**
   - 选择 `Runner` target
   - General → Bundle Identifier
   - 改为：`com.你的名字.swiftcompanion`

4. **启用自动签名**
   - Signing & Capabilities
   - ✅ Automatically manage signing
   - Team: 选择你的名字 (Personal Team)

5. **连接 iPhone**
   - 用 USB 线连接 Mac
   - iPhone 弹出信任提示 → 信任

6. **运行**
   - Xcode 顶部选择你的 iPhone
   - 点击 ▶️ 运行

7. **信任开发者（首次）**
   - iPhone: 设置 → 通用 → VPN与设备管理
   - 找到你的 Apple ID → 信任

✅ **完成！** 应用已安装在 iPhone 上。

### ⚠️ 限制
- 应用 7 天后过期（需重新安装）
- 最多同时 3 个应用
- 无推送通知
- 不能分享给他人

---

## 💰 付费方式（完整功能）

### 开通开发者账号
1. 访问 https://developer.apple.com/programs/
2. 支付 $99/年
3. 等待 1-2 天审核

### 配置证书
```bash
# 1. 注册 App ID
Bundle ID: com.swift.companion
Capabilities: Push Notifications, Background Modes

# 2. 创建开发证书
Keychain Access → 请求证书 → 上传到 developer.apple.com

# 3. 创建 Provisioning Profile
关联 App ID + 证书 + 设备
```

### 构建安装
```bash
# 方式 1: Xcode (同上，但选 Team 为付费账号)

# 方式 2: Flutter 命令行
cd mobile-app
flutter build ios --release
flutter install
```

### 优势
- ✅ 无过期限制
- ✅ 支持推送通知
- ✅ 可发布到 TestFlight/App Store
- ✅ 无应用数量限制

---

## 📱 TestFlight 分发（分享给测试用户）

### 1. 构建归档
```bash
cd mobile-app
flutter build ipa --release
```

### 2. 上传到 App Store Connect
- Xcode → Window → Organizer
- 选择归档 → Distribute App → App Store Connect

### 3. 添加测试员
- https://appstoreconnect.apple.com
- TestFlight → 内部测试/外部测试
- 添加邮箱 → 发送邀请

测试员：
1. 下载 TestFlight App
2. 接受邀请
3. 安装应用

---

## 🚀 App Store 发布（公开下载）

### 准备材料
- [ ] 应用图标 (1024×1024)
- [ ] 截图 (6.5", 5.5" 必需)
- [ ] 应用描述、关键词
- [ ] 隐私政策 URL（必需）
- [ ] 支持 URL

### 提交审核
1. App Store Connect → 我的 App
2. 新版本 → 填写信息
3. 提交审核
4. 等待 1-3 天
5. 批准后发布

---

## 💡 常见问题

**Q: 没有 Mac 怎么办？**
A: 可以租用 MacinCloud ($30/月) 或 MacStadium，或借朋友的 Mac。

**Q: 应用 7 天后打不开？**
A: 免费账号限制。重新连接 Xcode 运行即可，或升级付费账号。

**Q: "Untrusted Developer" 错误？**
A: 设置 → 通用 → VPN与设备管理 → 信任开发者。

**Q: 编译失败 "No profiles for..."？**
A: 改 Bundle ID 为唯一值，确保自动签名已启用。

**Q: 想发布给朋友用？**
A: 必须付费账号 + TestFlight，或上架 App Store。

---

## 📞 需要帮助？

- 详细指南: `IOS_SETUP.md`
- Flutter 文档: https://docs.flutter.dev/deployment/ios
- Apple 开发者: https://developer.apple.com

**最快路径**: 免费 Apple ID → 改 Bundle ID → Xcode 运行 → 7 天后重装。
