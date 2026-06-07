import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_foreground_task/flutter_foreground_task.dart';
import 'package:flutter/foundation.dart';
import 'dart:async';
import '../models/connection_state.dart';
import '../services/websocket_service.dart';
import '../services/bridge_api_service.dart';
import '../services/push_notification_service.dart';

/// 把用户输入的地址规整成完整 baseUrl。
/// 支持: https://xxx.trycloudflare.com、http://host:port、host:port(默认补 http)
String normalizeBridgeUrl(String input) {
  var addr = input.trim();
  if (addr.endsWith('/')) addr = addr.substring(0, addr.length - 1);
  if (addr.startsWith('http://') || addr.startsWith('https://')) {
    return addr;
  }
  // 无协议: 默认局域网 http; 显式带端口则保留
  return 'http://$addr';
}

/// Connection state provider
final connectionProvider = StateNotifierProvider<ConnectionNotifier, ConnectionState>((ref) {
  return ConnectionNotifier();
});

class ConnectionNotifier extends StateNotifier<ConnectionState> {
  ConnectionNotifier() : super(ConnectionState.disconnected());

  final WebSocketService _wsService = WebSocketService();
  final BridgeApiService _apiService = BridgeApiService();
  final PushNotificationService _notifications = PushNotificationService();
  bool _wasConnected = false;
  Timer? _reconnectTimer;
  String? _lastBridgeAddress;
  String? _lastToken;

  Future<void> connect(String bridgeAddress, String token) async {
    try {
      final baseUrl = normalizeBridgeUrl(bridgeAddress);
      final uri = Uri.parse(baseUrl);

      // 保存连接信息用于重连
      _lastBridgeAddress = bridgeAddress;
      _lastToken = token;

      _apiService.setBaseUrl(baseUrl);
      _apiService.setToken(token);

      // 先注册回调，再连接，避免漏掉连接成功事件
      _wsService.onConnectionChanged = (connected) {
        state = state.copyWith(isConnected: connected);
        // 由"已连接"变为"断开"时提醒并启动自动重连
        if (_wasConnected && !connected) {
          _notifications.showNotification(
            title: 'Disconnected',
            body: 'Connection lost, trying to reconnect...',
          );
          _startAutoReconnect();
        } else if (connected) {
          // 连接成功，停止重连计时器
          _stopAutoReconnect();
        }
        _wasConnected = connected;
      };

      // 监听 vPilot 状态更新(含呼号、网络连接状态、服务器)
      _wsService.onStateUpdated = (data) {
        final callsign = data['callsign'] as String?;
        final connected = data['connected'] as bool?;
        final server = data['server'] as String?;
        state = state.copyWith(
          callsign: callsign,
          vPilotConnected: connected,
          server: server,
        );
      };

      await _wsService.connect(baseUrl, token: token);

      state = ConnectionState.connected(
        bridgeAddress: uri.host,
        bridgePort: uri.hasPort ? uri.port : (uri.scheme == 'https' ? 443 : 80),
      );
      await _startKeepAlive();
      _startConnectionHealthCheck();
    } catch (e) {
      state = ConnectionState.disconnected();
      _startAutoReconnect(); // 连接失败也启动重连
      rethrow;
    }
  }

  /// 启动前台服务保活: 息屏/后台时维持 SignalR 连接
  Future<void> _startKeepAlive() async {
    FlutterForegroundTask.init(
      androidNotificationOptions: AndroidNotificationOptions(
        channelId: 'aetherlink_keepalive',
        channelName: 'AetherLink',
      ),
      iosNotificationOptions: const IOSNotificationOptions(),
      foregroundTaskOptions: ForegroundTaskOptions(
        eventAction: ForegroundTaskEventAction.nothing(),
        autoRunOnBoot: false,
        allowWakeLock: true,
      ),
    );
    if (await FlutterForegroundTask.isRunningService) return;
    await FlutterForegroundTask.startService(
      notificationTitle: 'AetherLink',
      notificationText: 'Connected — listening for messages',
    );
  }

  Future<void> disconnect() async {
    _stopAutoReconnect();
    _stopHealthCheck();
    await _wsService.disconnect();
    await FlutterForegroundTask.stopService();
    state = ConnectionState.disconnected();
    _lastBridgeAddress = null;
    _lastToken = null;
  }

  /// 启动自动重连（每 5 秒尝试一次）
  void _startAutoReconnect() {
    if (_reconnectTimer != null && _reconnectTimer!.isActive) return;
    if (_lastBridgeAddress == null || _lastToken == null) return;

    debugPrint('Starting auto-reconnect timer (5s interval)');
    _reconnectTimer = Timer.periodic(const Duration(seconds: 5), (_) async {
      if (state.isConnected) {
        _stopAutoReconnect();
        return;
      }

      debugPrint('Attempting to reconnect...');
      try {
        await connect(_lastBridgeAddress!, _lastToken!);
        debugPrint('Reconnection successful');
        _notifications.showNotification(
          title: 'Reconnected',
          body: 'Connection restored successfully',
        );
      } catch (e) {
        debugPrint('Reconnection failed: $e');
      }
    });
  }

  /// 停止自动重连
  void _stopAutoReconnect() {
    _reconnectTimer?.cancel();
    _reconnectTimer = null;
    debugPrint('Auto-reconnect timer stopped');
  }

  Timer? _healthCheckTimer;

  /// 启动连接健康检查（每 5 秒检查一次连接状态）
  void _startConnectionHealthCheck() {
    _stopHealthCheck(); // 先停止旧的计时器

    _healthCheckTimer = Timer.periodic(const Duration(seconds: 5), (_) async {
      if (!state.isConnected) {
        debugPrint('Health check: Not connected, triggering reconnect');
        _startAutoReconnect();
        return;
      }

      // 可选：通过 ping/pong 或轻量级 API 调用验证连接
      try {
        // await _wsService.ping(); // 如果有 ping 方法
        debugPrint('Health check: Connection OK');
      } catch (e) {
        debugPrint('Health check failed: $e');
        _wsService.onConnectionChanged?.call(false);
      }
    });
  }

  /// 停止健康检查
  void _stopHealthCheck() {
    _healthCheckTimer?.cancel();
    _healthCheckTimer = null;
  }

  @override
  void dispose() {
    _stopAutoReconnect();
    _stopHealthCheck();
    super.dispose();
  }

  void updateCallsign(String callsign) {
    state = state.copyWith(callsign: callsign);
  }

  void updateVPilotStatus(bool connected) {
    state = state.copyWith(vPilotConnected: connected);
  }

  WebSocketService get wsService => _wsService;
  BridgeApiService get apiService => _apiService;
}
