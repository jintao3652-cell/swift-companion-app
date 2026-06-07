import 'package:flutter_local_notifications/flutter_local_notifications.dart';
import 'package:flutter/foundation.dart';
import 'dart:io';

/// 本地通知服务（已移除 Firebase / FCM，改用 SignalR 实时通道 + 本地通知）
class PushNotificationService {
  final FlutterLocalNotificationsPlugin _localNotifications =
      FlutterLocalNotificationsPlugin();

  // 保留 getter 以兼容调用方；无 FCM 时恒为 null
  String? get fcmToken => null;

  Future<void> initialize() async {
    // Windows 不支持本地通知，跳过初始化
    if (!kIsWeb && (Platform.isWindows || Platform.isLinux || Platform.isMacOS)) {
      debugPrint('Local notifications not supported on desktop platforms');
      return;
    }

    try {
      const androidSettings =
          AndroidInitializationSettings('@mipmap/ic_launcher');
      const iosSettings = DarwinInitializationSettings(
        requestAlertPermission: true,
        requestBadgePermission: true,
        requestSoundPermission: true,
      );

      const initSettings = InitializationSettings(
        android: androidSettings,
        iOS: iosSettings,
      );

      await _localNotifications.initialize(
        initSettings,
        onDidReceiveNotificationResponse: _onNotificationTap,
      );

      // Request Android 13+ notification permission
      await _localNotifications
          .resolvePlatformSpecificImplementation<
              AndroidFlutterLocalNotificationsPlugin>()
          ?.requestNotificationsPermission();

      // Request iOS notification permissions
      await _localNotifications
          .resolvePlatformSpecificImplementation<
              IOSFlutterLocalNotificationsPlugin>()
          ?.requestPermissions(
            alert: true,
            badge: true,
            sound: true,
          );
    } catch (e) {
      debugPrint('Error initializing local notifications: $e');
    }
  }

  /// 收到消息时调用，弹出本地通知
  Future<void> showNotification({
    required String title,
    required String body,
    String? payload,
  }) async {
    // Windows/Desktop 不显示通知，只打印日志
    if (!kIsWeb && (Platform.isWindows || Platform.isLinux || Platform.isMacOS)) {
      debugPrint('Notification: $title - $body');
      return;
    }

    const androidDetails = AndroidNotificationDetails(
      'swift_companion',
      'Swift Companion',
      channelDescription: 'VATSIM messages and notifications',
      importance: Importance.high,
      priority: Priority.high,
      showWhen: true,
    );

    const iosDetails = DarwinNotificationDetails(
      presentAlert: true,
      presentBadge: true,
      presentSound: true,
    );

    const details = NotificationDetails(
      android: androidDetails,
      iOS: iosDetails,
    );

    await _localNotifications.show(
      DateTime.now().millisecondsSinceEpoch ~/ 1000,
      title,
      body,
      details,
      payload: payload,
    );
  }

  void _onNotificationTap(NotificationResponse response) {
    debugPrint('Notification tapped: ${response.payload}');
  }

  /// Test notification to verify setup
  Future<void> showTestNotification() async {
    await showNotification(
      title: 'Swift Companion',
      body: 'Notifications are working!',
      payload: 'test',
    );
  }
}
