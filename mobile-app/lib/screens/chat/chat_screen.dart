import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../models/message.dart';
import '../../providers/message_provider.dart';
import '../../providers/connection_provider.dart';
import '../settings/settings_screen.dart';
import 'private_chat_screen.dart';

class ChatScreen extends ConsumerStatefulWidget {
  const ChatScreen({Key? key}) : super(key: key);

  @override
  ConsumerState<ChatScreen> createState() => _ChatScreenState();
}

class _ChatScreenState extends ConsumerState<ChatScreen>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;
  final TextEditingController _messageController = TextEditingController();
  final ScrollController _scrollController = ScrollController();

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 2, vsync: this);
    _tabController.addListener(() {
      if (mounted) setState(() {}); // tab 切换时刷新底部输入框显隐
    });

    // Load message history on init
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(messagesProvider.notifier).loadHistory();
    });
  }

  @override
  void dispose() {
    _tabController.dispose();
    _messageController.dispose();
    _scrollController.dispose();
    super.dispose();
  }

  void _sendMessage() async {
    final text = _messageController.text.trim();
    if (text.isEmpty) return;

    final messageNotifier = ref.read(messagesProvider.notifier);

    try {
      // 底部输入框仅 Radio tab 可见，统一发 radio 消息
      await messageNotifier.sendRadioMessage(text);

      _messageController.clear();

      // Scroll to bottom
      Future.delayed(const Duration(milliseconds: 100), () {
        if (_scrollController.hasClients) {
          _scrollController.animateTo(
            _scrollController.position.maxScrollExtent,
            duration: const Duration(milliseconds: 300),
            curve: Curves.easeOut,
          );
        }
      });
    } catch (e) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Failed to send message: $e'),
          backgroundColor: Colors.red,
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Messages'),
        actions: [
          IconButton(
            icon: const Icon(Icons.settings),
            onPressed: () {
              Navigator.push(
                context,
                MaterialPageRoute(builder: (_) => const SettingsScreen()),
              );
            },
          ),
        ],
        bottom: TabBar(
          controller: _tabController,
          tabs: const [
            Tab(text: 'Radio', icon: Icon(Icons.radio, size: 20)),
            Tab(text: 'Private', icon: Icon(Icons.person, size: 20)),
          ],
        ),
      ),
      body: Column(
        children: [
          // 连接状态指示器
          _buildConnectionIndicator(),

          // 消息列表
          Expanded(
            child: TabBarView(
              controller: _tabController,
              children: [
                _buildMessageList(MessageType.radio),
                _buildMessageList(MessageType.private),
              ],
            ),
          ),

          // 快捷指令(仅 Radio tab)
          if (_tabController.index == 0) _buildQuickCommands(),

          // 输入框(仅 Radio tab；私信在会话卡片内回复)
          if (_tabController.index == 0) _buildMessageInput(),
        ],
      ),
    );
  }

  Widget _buildConnectionIndicator() {
    final connectionState = ref.watch(connectionProvider);
    final isConnected = connectionState.isConnected;
    final callsign = connectionState.callsign;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      color: isConnected ? Colors.green.shade50 : Colors.red.shade50,
      child: Row(
        children: [
          Icon(
            isConnected ? Icons.check_circle : Icons.error_outline,
            size: 16,
            color: isConnected ? Colors.green : Colors.red,
          ),
          const SizedBox(width: 8),
          Text(
            isConnected
                ? (callsign != null ? 'Connected as $callsign' : 'Connected')
                : 'Disconnected - Check Bridge',
            style: TextStyle(
              fontSize: 13,
              color: isConnected ? Colors.green.shade900 : Colors.red.shade900,
            ),
          ),
          const Spacer(),
          if (!isConnected)
            TextButton(
              onPressed: () {
                Navigator.push(
                  context,
                  MaterialPageRoute(builder: (_) => const SettingsScreen()),
                );
              },
              child: const Text('Reconnect', style: TextStyle(fontSize: 12)),
            ),
        ],
      ),
    );
  }

  // 私信会话卡片列表：按对方呼号分组，显示最后一条消息
  Widget _buildPrivateConversations(List<Message> messages) {
    final privates =
        messages.where((m) => m.type == MessageType.private).toList();

    // 按对方呼号分组(对方 = from!='Me' 时取 from，否则取 to)
    final Map<String, Message> latest = {};
    for (final m in privates) {
      final peer = m.from == 'Me' ? (m.to ?? '') : m.from;
      if (peer.isEmpty) continue;
      final existing = latest[peer];
      if (existing == null || m.timestamp.isAfter(existing.timestamp)) {
        latest[peer] = m;
      }
    }

    if (latest.isEmpty) {
      return Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.person, size: 64, color: Colors.grey),
            const SizedBox(height: 16),
            Text('No private messages yet',
                style: TextStyle(color: Colors.grey.shade600)),
          ],
        ),
      );
    }

    final peers = latest.keys.toList()
      ..sort((a, b) => latest[b]!.timestamp.compareTo(latest[a]!.timestamp));

    return ListView.builder(
      padding: const EdgeInsets.all(8),
      itemCount: peers.length,
      itemBuilder: (_, i) {
        final peer = peers[i];
        final msg = latest[peer]!;
        return Card(
          child: ListTile(
            leading: CircleAvatar(child: Text(peer.substring(0, 1))),
            title: Text(peer, style: const TextStyle(fontWeight: FontWeight.bold)),
            subtitle: Text(msg.content,
                maxLines: 1, overflow: TextOverflow.ellipsis),
            trailing: Text(_formatTime(msg.timestamp),
                style: const TextStyle(fontSize: 10, color: Colors.grey)),
            onTap: () => Navigator.push(
              context,
              MaterialPageRoute(builder: (_) => PrivateChatScreen(peer: peer)),
            ),
          ),
        );
      },
    );
  }

  Widget _buildMessageList(MessageType type) {
    final messages = ref.watch(messagesProvider);

    // 私信 tab：卡片式会话列表，点进卡片才能回复
    if (type == MessageType.private) {
      return _buildPrivateConversations(messages);
    }

    // Radio tab 同时显示频率消息和系统消息(连接状态等)
    final filteredMessages = messages
        .where((m) => m.type == type ||
            (type == MessageType.radio && m.type == MessageType.system))
        .toList();

    if (filteredMessages.isEmpty) {
      return Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(
              type == MessageType.radio ? Icons.radio : Icons.person,
              size: 64,
              color: Colors.grey,
            ),
            const SizedBox(height: 16),
            Text(
              type == MessageType.radio
                  ? 'No radio messages yet'
                  : 'No private messages yet',
              style: TextStyle(color: Colors.grey.shade600),
            ),
          ],
        ),
      );
    }

    return ListView.builder(
      controller: _scrollController,
      padding: const EdgeInsets.all(16),
      itemCount: filteredMessages.length,
      itemBuilder: (context, index) {
        final message = filteredMessages[index];
        return _buildMessageBubble(message);
      },
    );
  }

  Widget _buildMessageBubble(Message message) {
    final isOutgoing = false; // TODO: 检查是否是自己发送的消息

    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Row(
        mainAxisAlignment:
            isOutgoing ? MainAxisAlignment.end : MainAxisAlignment.start,
        children: [
          if (!isOutgoing) ...[
            CircleAvatar(
              radius: 16,
              child: Text(
                message.from.substring(0, 1),
                style: const TextStyle(fontSize: 12),
              ),
            ),
            const SizedBox(width: 8),
          ],
          Flexible(
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
              decoration: BoxDecoration(
                color: isOutgoing
                    ? Theme.of(context).primaryColor
                    : Colors.grey.shade200,
                borderRadius: BorderRadius.circular(12),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (!isOutgoing)
                    Text(
                      message.from,
                      style: TextStyle(
                        fontWeight: FontWeight.bold,
                        fontSize: 12,
                        color: isOutgoing ? Colors.white70 : Colors.black87,
                      ),
                    ),
                  if (!isOutgoing) const SizedBox(height: 4),
                  Text(
                    message.content,
                    style: TextStyle(
                      color: isOutgoing ? Colors.white : Colors.black87,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    _formatTime(message.timestamp),
                    style: TextStyle(
                      fontSize: 10,
                      color: isOutgoing ? Colors.white70 : Colors.grey,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildQuickCommands() {
    return Container(
      height: 60,
      padding: const EdgeInsets.symmetric(horizontal: 8),
      child: ListView(
        scrollDirection: Axis.horizontal,
        children: [
          _buildQuickCommandButton('Check In', '.chat Good day, checking in'),
          _buildQuickCommandButton('ATIS', '.atis'),
          _buildQuickCommandButton('Ready', '.chat Ready for departure'),
          _buildQuickCommandButton('Roger', '.chat Roger'),
        ],
      ),
    );
  }

  Widget _buildQuickCommandButton(String label, String command) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 4),
      child: ActionChip(
        label: Text(label),
        onPressed: () {
          _messageController.text = command;
        },
      ),
    );
  }

  Widget _buildMessageInput() {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Theme.of(context).scaffoldBackgroundColor,
        boxShadow: [
          BoxShadow(
            color: Colors.black.withOpacity(0.05),
            blurRadius: 4,
            offset: const Offset(0, -2),
          ),
        ],
      ),
      child: Row(
        children: [
          Expanded(
            child: TextField(
              controller: _messageController,
              decoration: const InputDecoration(
                hintText: 'Type a message or command...',
                border: OutlineInputBorder(),
                contentPadding: EdgeInsets.symmetric(horizontal: 16, vertical: 12),
              ),
              maxLines: null,
              textInputAction: TextInputAction.send,
              onSubmitted: (_) => _sendMessage(),
            ),
          ),
          const SizedBox(width: 8),
          IconButton.filled(
            icon: const Icon(Icons.send),
            onPressed: _sendMessage,
          ),
        ],
      ),
    );
  }

  String _formatTime(DateTime time) {
    final now = DateTime.now();
    final difference = now.difference(time);

    if (difference.inMinutes < 1) {
      return 'Just now';
    } else if (difference.inMinutes < 60) {
      return '${difference.inMinutes}m ago';
    } else if (difference.inHours < 24) {
      return '${difference.inHours}h ago';
    } else {
      return '${time.hour}:${time.minute.toString().padLeft(2, '0')}';
    }
  }
}
