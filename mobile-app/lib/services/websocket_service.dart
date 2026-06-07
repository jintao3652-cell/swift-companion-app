import 'package:signalr_netcore/signalr_client.dart';
import 'package:flutter/foundation.dart';
import '../models/message.dart';
import '../models/aircraft_state.dart';

class WebSocketService {
  HubConnection? _hubConnection;
  String? _bridgeUrl;
  bool _isConnected = false;

  // Callbacks
  Function(Message)? onMessageReceived;
  Function(AircraftState)? onAircraftStateUpdated;
  Function(Map<String, dynamic>)? onStateUpdated;
  Function(bool)? onConnectionChanged;

  bool get isConnected => _isConnected;

  Future<void> connect(String bridgeUrl, {String? token}) async {
    try {
      _bridgeUrl = bridgeUrl;

      final httpConnectionOptions = HttpConnectionOptions(
        accessTokenFactory: token != null ? () async => token : null,
        // Cloudflare 免费隧道不稳定支持 WebSocket 长连接，强制走 Long Polling
        transport: HttpTransportType.LongPolling,
        // 默认 2s 太短，隧道往返延迟高会超时
        requestTimeout: 30000,
      );

      _hubConnection = HubConnectionBuilder()
          .withUrl('$bridgeUrl/vatsimhub', options: httpConnectionOptions)
          .withAutomaticReconnect()
          .build();

      // 注册事件监听
      _hubConnection!.on('ReceiveMessage', _handleMessageReceived);
      _hubConnection!.on('AircraftStateUpdated', _handleAircraftStateUpdated);
      _hubConnection!.on('StateUpdated', _handleStateUpdated);
      _hubConnection!.on('SwiftConnectionChanged', _handleSwiftConnectionChanged);

      // 连接状态监听
      _hubConnection!.onclose(({error}) {
        debugPrint('SignalR connection closed: $error');
        _isConnected = false;
        onConnectionChanged?.call(false);
      });

      _hubConnection!.onreconnecting(({error}) {
        debugPrint('SignalR reconnecting: $error');
        _isConnected = false;
        onConnectionChanged?.call(false);
      });

      _hubConnection!.onreconnected(({connectionId}) {
        debugPrint('SignalR reconnected: $connectionId');
        _isConnected = true;
        onConnectionChanged?.call(true);
      });

      // 启动连接
      await _hubConnection!.start();
      _isConnected = true;
      onConnectionChanged?.call(true);

      debugPrint('SignalR connected to $bridgeUrl');
    } catch (e) {
      debugPrint('Error connecting to SignalR: $e');
      _isConnected = false;
      onConnectionChanged?.call(false);
      rethrow;
    }
  }

  Future<void> disconnect() async {
    try {
      if (_hubConnection != null) {
        await _hubConnection!.stop();
        _isConnected = false;
        onConnectionChanged?.call(false);
        debugPrint('SignalR disconnected');
      }
    } catch (e) {
      debugPrint('Error disconnecting from SignalR: $e');
    }
  }

  // 发送私信
  Future<Map<String, dynamic>> sendPrivateMessage(String recipient, String message) async {
    try {
      if (!_isConnected || _hubConnection == null) {
        throw Exception('Not connected to bridge');
      }

      final result = await _hubConnection!.invoke(
        'SendPrivateMessage',
        args: [recipient, message],
      );

      return result as Map<String, dynamic>;
    } catch (e) {
      debugPrint('Error sending private message: $e');
      rethrow;
    }
  }

  // 发送频率消息
  Future<Map<String, dynamic>> sendRadioMessage(String message) async {
    try {
      if (!_isConnected || _hubConnection == null) {
        throw Exception('Not connected to bridge');
      }

      final result = await _hubConnection!.invoke(
        'SendRadioMessage',
        args: [message],
      );

      return result as Map<String, dynamic>;
    } catch (e) {
      debugPrint('Error sending radio message: $e');
      rethrow;
    }
  }

  // 执行指令
  Future<Map<String, dynamic>> executeCommand(String command) async {
    try {
      if (!_isConnected || _hubConnection == null) {
        throw Exception('Not connected to bridge');
      }

      final result = await _hubConnection!.invoke(
        'ExecuteCommand',
        args: [command],
      );

      return result as Map<String, dynamic>;
    } catch (e) {
      debugPrint('Error executing command: $e');
      rethrow;
    }
  }

  // 请求飞机状态
  Future<void> requestAircraftState() async {
    try {
      if (!_isConnected || _hubConnection == null) {
        throw Exception('Not connected to bridge');
      }

      await _hubConnection!.invoke('RequestAircraftState');
    } catch (e) {
      debugPrint('Error requesting aircraft state: $e');
      rethrow;
    }
  }

  // 注册推送令牌
  Future<void> registerPushToken(String fcmToken, String deviceId) async {
    try {
      if (!_isConnected || _hubConnection == null) {
        throw Exception('Not connected to bridge');
      }

      await _hubConnection!.invoke(
        'RegisterPushToken',
        args: [fcmToken, deviceId],
      );

      debugPrint('Push token registered');
    } catch (e) {
      debugPrint('Error registering push token: $e');
      rethrow;
    }
  }

  // 事件处理
  void _handleMessageReceived(List<Object?>? args) {
    if (args == null || args.isEmpty) return;

    try {
      final data = args[0] as Map<String, dynamic>;
      final message = Message.fromJson(data);
      onMessageReceived?.call(message);
    } catch (e) {
      debugPrint('Error handling message received: $e');
    }
  }

  void _handleAircraftStateUpdated(List<Object?>? args) {
    if (args == null || args.isEmpty) return;

    try {
      final data = args[0] as Map<String, dynamic>;
      final state = AircraftState.fromJson(data);
      onAircraftStateUpdated?.call(state);
    } catch (e) {
      debugPrint('Error handling aircraft state updated: $e');
    }
  }

  void _handleStateUpdated(List<Object?>? args) {
    if (args == null || args.isEmpty) return;

    try {
      final data = args[0] as Map<String, dynamic>;
      onStateUpdated?.call(data);
    } catch (e) {
      debugPrint('Error handling state updated: $e');
    }
  }

  void _handleSwiftConnectionChanged(List<Object?>? args) {
    if (args == null || args.isEmpty) return;

    try {
      final data = args[0] as Map<String, dynamic>;
      final connected = data['connected'] as bool? ?? false;
      debugPrint('swift connection changed: $connected');
    } catch (e) {
      debugPrint('Error handling swift connection changed: $e');
    }
  }
}
