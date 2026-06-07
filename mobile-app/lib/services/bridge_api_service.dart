import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../models/controller_info.dart';

class BridgeApiService {
  final Dio _dio;
  String? _baseUrl;
  String? _token;

  BridgeApiService() : _dio = Dio() {
    _dio.options.connectTimeout = const Duration(seconds: 10);
    _dio.options.receiveTimeout = const Duration(seconds: 10);

    // 添加日志拦截器
    if (kDebugMode) {
      _dio.interceptors.add(LogInterceptor(
        requestBody: true,
        responseBody: true,
      ));
    }
  }

  void setBaseUrl(String baseUrl) {
    _baseUrl = baseUrl;
    _dio.options.baseUrl = baseUrl;
  }

  void setToken(String token) {
    _token = token;
    _dio.options.headers['Authorization'] = 'Bearer $token';
  }

  // 获取状态
  Future<Map<String, dynamic>> getStatus() async {
    try {
      final response = await _dio.get('/api/status');
      return response.data as Map<String, dynamic>;
    } catch (e) {
      debugPrint('Error getting status: $e');
      rethrow;
    }
  }

  // 健康检查
  Future<bool> healthCheck() async {
    try {
      final response = await _dio.get('/api/status/health');
      return response.statusCode == 200;
    } catch (e) {
      debugPrint('Health check failed: $e');
      return false;
    }
  }

  // 开始配对
  Future<Map<String, dynamic>> startPairing() async {
    try {
      final response = await _dio.post('/api/pairing/start');
      return response.data as Map<String, dynamic>;
    } catch (e) {
      debugPrint('Error starting pairing: $e');
      rethrow;
    }
  }

  // 验证配对码
  Future<Map<String, dynamic>> verifyPairing({
    required String pairingCode,
    required String deviceId,
    required String deviceName,
    String? fcmToken,
  }) async {
    try {
      final response = await _dio.post('/api/pairing/verify', data: {
        'pairingCode': pairingCode,
        'deviceId': deviceId,
        'deviceName': deviceName,
        'fcmToken': fcmToken,
      });
      return response.data as Map<String, dynamic>;
    } catch (e) {
      debugPrint('Error verifying pairing: $e');
      rethrow;
    }
  }

  // 刷新 Token
  Future<Map<String, dynamic>> refreshToken(String refreshToken) async {
    try {
      final response = await _dio.post('/api/pairing/refresh', data: {
        'refreshToken': refreshToken,
      });
      return response.data as Map<String, dynamic>;
    } catch (e) {
      debugPrint('Error refreshing token: $e');
      rethrow;
    }
  }

  // 获取消息历史
  Future<List<dynamic>> getMessages({
    String? type,
    int limit = 50,
    int offset = 0,
  }) async {
    try {
      final response = await _dio.get('/api/messages', queryParameters: {
        if (type != null) 'type': type,
        'limit': limit,
        'offset': offset,
      });

      final data = response.data as Map<String, dynamic>;
      return data['messages'] as List<dynamic>;
    } catch (e) {
      debugPrint('Error getting messages: $e');
      rethrow;
    }
  }

  // 标记消息已读
  Future<void> markMessageAsRead(String messageId) async {
    try {
      await _dio.put('/api/messages/$messageId/read');
    } catch (e) {
      debugPrint('Error marking message as read: $e');
      rethrow;
    }
  }

  // 清空消息历史
  Future<void> clearMessages() async {
    try {
      await _dio.delete('/api/messages');
    } catch (e) {
      debugPrint('Error clearing messages: $e');
      rethrow;
    }
  }

  // 获取飞机状态
  Future<Map<String, dynamic>> getAircraftState() async {
    try {
      final response = await _dio.get('/api/aircraft/state');
      return response.data as Map<String, dynamic>;
    } catch (e) {
      debugPrint('Error getting aircraft state: $e');
      rethrow;
    }
  }

  // 获取在线管制（周围管制列表）
  Future<ControllersResponse> getOnlineControllers() async {
    try {
      final response = await _dio.get('/api/atc/online');
      return ControllersResponse.fromJson(response.data as Map<String, dynamic>);
    } catch (e) {
      debugPrint('Error getting online controllers: $e');
      rethrow;
    }
  }

  // 获取附近飞机
  Future<List<dynamic>> getNearbyAircraft({int radius = 50}) async {
    try {
      final response = await _dio.get('/api/aircraft/nearby', queryParameters: {
        'radius': radius,
      });

      final data = response.data as Map<String, dynamic>;
      return data['aircraft'] as List<dynamic>;
    } catch (e) {
      debugPrint('Error getting nearby aircraft: $e');
      rethrow;
    }
  }

  // 获取快捷指令
  Future<List<dynamic>> getQuickCommands() async {
    try {
      final response = await _dio.get('/api/commands/quick');
      final data = response.data as Map<String, dynamic>;
      return data['commands'] as List<dynamic>;
    } catch (e) {
      debugPrint('Error getting quick commands: $e');
      rethrow;
    }
  }

  // 执行指令
  Future<Map<String, dynamic>> executeCommand(
    String command, {
    Map<String, String>? parameters,
  }) async {
    try {
      final response = await _dio.post('/api/commands/execute', data: {
        'command': command,
        if (parameters != null) 'parameters': parameters,
      });
      return response.data as Map<String, dynamic>;
    } catch (e) {
      debugPrint('Error executing command: $e');
      rethrow;
    }
  }
}

// Provider
final bridgeApiServiceProvider = Provider<BridgeApiService>((ref) {
  return BridgeApiService();
});
