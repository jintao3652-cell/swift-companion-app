import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../models/message.dart';
import '../providers/connection_provider.dart';
import '../services/push_notification_service.dart';

/// Messages state provider
final messagesProvider = StateNotifierProvider<MessagesNotifier, List<Message>>((ref) {
  final connectionNotifier = ref.watch(connectionProvider.notifier);
  return MessagesNotifier(connectionNotifier);
});

class MessagesNotifier extends StateNotifier<List<Message>> {
  MessagesNotifier(this._connectionNotifier) : super([]) {
    _initialize();
  }

  final ConnectionNotifier _connectionNotifier;
  final PushNotificationService _notifications = PushNotificationService();

  void _initialize() {
    // Setup WebSocket message listener
    _connectionNotifier.wsService.onMessageReceived = (message) {
      addMessage(message);
    };
  }

  void addMessage(Message message) {
    // 去重：相同 id，或相同 发件人+内容+10秒内时间戳 视为重复
    final isDup = state.any((m) =>
        (message.id.isNotEmpty && m.id == message.id) ||
        (m.from == message.from &&
            m.content == message.content &&
            m.type == message.type &&
            message.timestamp.difference(m.timestamp).abs().inSeconds < 10));
    if (isDup) return;
    state = [...state, message];
    _maybeNotify(message);
  }

  /// 私信 或 公屏提及本机呼号时弹本地通知(忽略自己发出的)
  void _maybeNotify(Message message) {
    if (message.from == 'Me') return;
    final callsign = _connectionNotifier.state.callsign;
    if (message.type == MessageType.private) {
      _notifications.showNotification(
        title: 'Private message from ${message.from}',
        body: message.content,
      );
    } else if (message.type == MessageType.radio &&
        callsign != null &&
        callsign.isNotEmpty &&
        message.content.toUpperCase().contains(callsign.toUpperCase())) {
      _notifications.showNotification(
        title: 'Your callsign mentioned',
        body: '${message.from}: ${message.content}',
      );
    }
  }

  void clearMessages() {
    state = [];
  }

  Future<void> loadHistory() async {
    try {
      final response = await _connectionNotifier.apiService.getMessages(limit: 100);
      final messages = response.map((m) => Message.fromJson(m)).toList();
      state = messages;
    } catch (e) {
      // Error loading history
      print('Error loading message history: $e');
    }
  }

  Future<void> sendPrivateMessage(String recipient, String message) async {
    try {
      await _connectionNotifier.wsService.sendPrivateMessage(recipient, message);

      // Add to local state optimistically
      addMessage(Message(
        id: DateTime.now().millisecondsSinceEpoch.toString(),
        type: MessageType.private,
        from: 'Me', // TODO: Get actual callsign
        to: recipient,
        content: message,
        timestamp: DateTime.now(),
        isRead: true,
      ));
    } catch (e) {
      rethrow;
    }
  }

  Future<void> sendRadioMessage(String message) async {
    try {
      await _connectionNotifier.wsService.sendRadioMessage(message);

      // Add to local state optimistically
      addMessage(Message(
        id: DateTime.now().millisecondsSinceEpoch.toString(),
        type: MessageType.radio,
        from: 'Me', // TODO: Get actual callsign
        content: message,
        timestamp: DateTime.now(),
        isRead: true,
      ));
    } catch (e) {
      rethrow;
    }
  }

  List<Message> getMessagesByType(MessageType type) {
    return state.where((m) => m.type == type).toList();
  }
}
