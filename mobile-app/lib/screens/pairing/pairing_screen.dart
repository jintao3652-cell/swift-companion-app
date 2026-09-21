import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'dart:io';
import 'dart:convert';
import '../../services/bridge_api_service.dart';
import '../../services/storage_service.dart';
import '../../services/push_notification_service.dart';
import '../../providers/connection_provider.dart';
import '../../services/bridge_installer_service.dart';
import 'qr_scanner_screen.dart';

class PairingScreen extends ConsumerStatefulWidget {
  const PairingScreen({Key? key}) : super(key: key);

  @override
  ConsumerState<PairingScreen> createState() => _PairingScreenState();
}

class _PairingScreenState extends ConsumerState<PairingScreen> {
  final TextEditingController _pairingCodeController = TextEditingController();
  late final TextEditingController _bridgeAddressController =
      TextEditingController(
    text: BridgeInstallerService.isSupported
        ? '127.0.0.1:${BridgeInstallerService.bridgePort}'
        : '',
  );
  final StorageService _storageService = StorageService();
  final PushNotificationService _pushService = PushNotificationService();
  bool _isLoading = false;
  String? _errorMessage;

  @override
  void dispose() {
    _pairingCodeController.dispose();
    _bridgeAddressController.dispose();
    super.dispose();
  }

  Future<void> _handlePairing() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final pairingCode = _pairingCodeController.text.trim();
      final bridgeAddress = _bridgeAddressController.text.trim();

      if (pairingCode.isEmpty) {
        throw Exception('Please enter pairing code');
      }

      if (bridgeAddress.isEmpty) {
        throw Exception('Please enter bridge address');
      }

      if (pairingCode.length != 6) {
        throw Exception('Pairing code must be 6 digits');
      }

      // 手机上 localhost/127.0.0.1 指手机自身，必然连接失败 —— 提前拦截并给出正确指引
      if (!BridgeInstallerService.isSupported) {
        final host = bridgeAddress
            .replaceAll(RegExp(r'^https?://'), '')
            .split(':')
            .first
            .trim()
            .toLowerCase();
        if (host == 'localhost' || host == '127.0.0.1' || host == '::1' || host == '[::1]') {
          throw Exception(
              'On your phone, "localhost" means the phone itself — not your PC.\n\n'
              'Please enter your PC\'s LAN IP instead (run "ipconfig" on the PC and use its IPv4 address, e.g. 192.168.1.100:${BridgeInstallerService.bridgePort}), '
              'or tap "Scan QR Code" and scan the code shown by the PC app.');
        }
      }

      // 1. 连接到 Bridge API
      final baseUrl = normalizeBridgeUrl(bridgeAddress);

      final apiService = BridgeApiService();
      apiService.setBaseUrl(baseUrl);

      // 2. 获取设备信息
      final deviceId = await _storageService.getOrCreateDeviceId();
      final deviceName = await _getDeviceName();

      // 3. 初始化推送通知
      await _pushService.initialize();
      final fcmToken = _pushService.fcmToken;

      // 4. 验证配对码
      final response = await apiService.verifyPairing(
        pairingCode: pairingCode,
        deviceId: deviceId,
        deviceName: deviceName,
        fcmToken: fcmToken,
      );

      if (response['success'] != true) {
        throw Exception(response['error'] ?? 'Pairing failed');
      }

      final token = response['token'] as String;
      final userId = response['userId'] as String;

      // 5. 保存配对信息
      await _storageService.saveAuthToken(token);
      await _storageService.saveUserId(userId);
      await _storageService.saveBridgeAddress(bridgeAddress);
      await _storageService.setIsPaired(true);

      // 6. 连接 WebSocket
      final connectionNotifier = ref.read(connectionProvider.notifier);
      await connectionNotifier.connect(bridgeAddress, token);

      // 7. 注册推送 Token
      if (fcmToken != null) {
        await connectionNotifier.wsService.registerPushToken(fcmToken, deviceId);
      }

      // 8. 跳转到主页
      if (mounted) {
        Navigator.pushReplacementNamed(context, '/home');
      }
    } on DioException catch (e) {
      setState(() {
        _errorMessage = _friendlyDioError(e);
      });
    } catch (e) {
      setState(() {
        _errorMessage = e.toString().replaceAll('Exception: ', '');
      });
    } finally {
      setState(() {
        _isLoading = false;
      });
    }
  }

  /// 把 Dio 连接错误翻译成用户能照着做的提示
  String _friendlyDioError(DioException e) {
    final port = BridgeInstallerService.bridgePort;

    // Bridge 有响应：解析服务端返回的错误文案（如 "Invalid or expired pairing code"）
    final resp = e.response;
    if (resp != null) {
      String? serverMsg;
      final data = resp.data;
      if (data is Map && data['error'] is String) {
        serverMsg = data['error'] as String;
      } else if (data is String) {
        final m = RegExp(r'"error"\s*:\s*"([^"]+)"').firstMatch(data);
        if (m != null) serverMsg = m.group(1);
      }
      final status = resp.statusCode ?? 0;
      switch (status) {
        case 400:
        case 401:
          return serverMsg != null
              ? '$serverMsg.\n\n'
                  'The pairing code is valid for 10 minutes and single-use. '
                  'Check the CURRENT code shown in the PC Companion app '
                  '(it changes every time the app restarts), then re-enter it '
                  '— or just tap "Scan QR Code".'
              : 'Invalid or expired pairing code.\n\n'
                  'The code is valid for 10 minutes and single-use. '
                  'Check the CURRENT code shown in the PC Companion app, '
                  'then re-enter it — or just tap "Scan QR Code".';
        case 404:
          return 'Bridge endpoint not found (HTTP 404). '
              'The address may point to a different app — '
              'use the address/QR code shown in the PC Companion app.';
        case >= 500:
          return 'Tunnel or server unreachable (HTTP ${resp.statusCode}).\n\n'
              'Cloudflare quick tunnels get a NEW address every time the PC app '
              'restarts — the saved address is probably stale. Open the PC '
              'Companion app and re-scan its QR code (or copy the current '
              'Cloudflare URL). On the same Wi-Fi, the PC\'s LAN IP '
              '(e.g. 192.168.1.100:$port) is more stable.';
        default:
          return 'Bridge returned HTTP ${resp.statusCode}'
              '${serverMsg != null ? ': $serverMsg' : ''}.';
      }
    }

    switch (e.type) {
      case DioExceptionType.connectionError:
      case DioExceptionType.connectionTimeout:
        return 'Cannot reach the Bridge at "$_bridgeAddressController.text".\n\n'
            'Please check:\n'
            '1. The PC Companion app (SwiftCompanion) is running — it starts the Bridge automatically.\n'
            '2. Your phone and the PC are on the same Wi-Fi network.\n'
            '3. The address is the PC\'s LAN IP (run "ipconfig" on the PC, use its IPv4 address, e.g. 192.168.1.100:$port) — NOT localhost.\n'
            'Or simply tap "Scan QR Code" and scan the code shown in the PC app.';
      default:
        return 'Bridge request failed: ${e.message ?? e.type.name}\n\n'
            'Make sure the PC Companion app is running and the address is the PC\'s LAN IP (e.g. 192.168.1.100:$port).';
    }
  }

  /// 扫码: 解析二维码内的 {bridgeUrl, pairingCode} JSON, 填入输入框并自动配对
  Future<void> _handleScan() async {
    final raw = await Navigator.push<String>(
      context,
      MaterialPageRoute(builder: (_) => const QrScannerScreen()),
    );
    if (raw == null) return;
    try {
      final data = jsonDecode(raw) as Map<String, dynamic>;
      final url = (data['bridgeUrl'] ?? '').toString();
      final code = (data['pairingCode'] ?? '').toString();
      if (url.isEmpty || code.isEmpty) throw const FormatException();
      _bridgeAddressController.text = url;
      _pairingCodeController.text = code;
      await _handlePairing();
    } catch (_) {
      setState(() => _errorMessage = 'Invalid QR code');
    }
  }

  Future<String> _getDeviceName() async {
    try {
      if (Platform.isAndroid) {
        return 'Android Device';
      } else if (Platform.isIOS) {
        return 'iOS Device';
      }
      return 'Mobile Device';
    } catch (e) {
      return 'Mobile Device';
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Pair with Bridge'),
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          child: Padding(
            padding: const EdgeInsets.all(24.0),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
              Icon(
                Icons.link,
                size: 80,
                color: Theme.of(context).primaryColor,
              ),
              const SizedBox(height: 32),
              Text(
                'Connect to Bridge',
                style: Theme.of(context).textTheme.headlineSmall,
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 8),
              Text(
                'Start the Bridge service on your PC running swift, then enter the pairing code shown.',
                style: Theme.of(context).textTheme.bodyMedium,
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 32),
              TextField(
                controller: _bridgeAddressController,
                decoration: const InputDecoration(
                  labelText: 'Bridge Address',
                  hintText: 'PC LAN IP, e.g. 192.168.1.100:${BridgeInstallerService.bridgePort} (not localhost)',
                  prefixIcon: Icon(Icons.computer),
                ),
                keyboardType: TextInputType.url,
                enabled: !_isLoading,
              ),
              const SizedBox(height: 16),
              TextField(
                controller: _pairingCodeController,
                decoration: const InputDecoration(
                  labelText: 'Pairing Code',
                  hintText: '123456',
                  prefixIcon: Icon(Icons.pin),
                ),
                keyboardType: TextInputType.number,
                maxLength: 6,
                enabled: !_isLoading,
              ),
              if (_errorMessage != null) ...[
                const SizedBox(height: 16),
                Card(
                  color: Colors.red.shade50,
                  child: Padding(
                    padding: const EdgeInsets.all(12.0),
                    child: Row(
                      children: [
                        const Icon(Icons.error_outline, color: Colors.red),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Text(
                            _errorMessage!,
                            style: const TextStyle(color: Colors.red),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ],
              const SizedBox(height: 24),
              ElevatedButton(
                onPressed: _isLoading ? null : _handlePairing,
                style: ElevatedButton.styleFrom(
                  padding: const EdgeInsets.symmetric(vertical: 16),
                ),
                child: _isLoading
                    ? const SizedBox(
                        height: 20,
                        width: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Text('Connect'),
              ),
              const SizedBox(height: 16),
              OutlinedButton.icon(
                onPressed: _isLoading ? null : _handleScan,
                icon: const Icon(Icons.qr_code_scanner),
                label: const Text('Scan QR Code'),
                style: OutlinedButton.styleFrom(
                  padding: const EdgeInsets.symmetric(vertical: 16),
                ),
              ),
              const SizedBox(height: 32),
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(16.0),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          const Icon(Icons.info_outline, size: 20),
                          const SizedBox(width: 8),
                          Text(
                            'How to Connect',
                            style: Theme.of(context).textTheme.titleSmall,
                          ),
                        ],
                      ),
                      const SizedBox(height: 12),
                      const Text(
                        '1. Install and start the Bridge service on your PC\n'
                        '2. Start swift\n'
                        '3. The Bridge will display a pairing code\n'
                        '4. Enter the code here to connect',
                        style: TextStyle(fontSize: 13),
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
        ),
      ),
    );
  }
}
