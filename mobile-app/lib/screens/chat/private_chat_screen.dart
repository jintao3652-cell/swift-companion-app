import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../models/message.dart';
import '../../providers/message_provider.dart';

/// 与单个用户的私信会话页
class PrivateChatScreen extends ConsumerStatefulWidget {
  final String peer; // 对方呼号
  const PrivateChatScreen({Key? key, required this.peer}) : super(key: key);

  @override
  ConsumerState<PrivateChatScreen> createState() => _PrivateChatScreenState();
}

class _PrivateChatScreenState extends ConsumerState<PrivateChatScreen> {
  final TextEditingController _controller = TextEditingController();
  final ScrollController _scrollController = ScrollController();

  @override
  void dispose() {
    _controller.dispose();
    _scrollController.dispose();
    super.dispose();
  }

  void _send() async {
    final text = _controller.text.trim();
    if (text.isEmpty) return;
    try {
      await ref.read(messagesProvider.notifier).sendPrivateMessage(widget.peer, text);
      _controller.clear();
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
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Failed to send: $e'), backgroundColor: Colors.red),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final messages = ref.watch(messagesProvider);
    // 与该 peer 的会话：对方发来的(from==peer) 或自己发给对方的(to==peer)
    final convo = messages
        .where((m) =>
            m.type == MessageType.private &&
            (m.from == widget.peer || m.to == widget.peer))
        .toList();

    return Scaffold(
      appBar: AppBar(title: Text(widget.peer)),
      body: Column(
        children: [
          Expanded(
            child: convo.isEmpty
                ? const Center(child: Text('No messages'))
                : ListView.builder(
                    controller: _scrollController,
                    padding: const EdgeInsets.all(16),
                    itemCount: convo.length,
                    itemBuilder: (_, i) => _bubble(convo[i]),
                  ),
          ),
          _input(),
        ],
      ),
    );
  }

  Widget _bubble(Message m) {
    final isOutgoing = m.from == 'Me' || m.to == widget.peer && m.from != widget.peer;
    return Align(
      alignment: isOutgoing ? Alignment.centerRight : Alignment.centerLeft,
      child: Container(
        margin: const EdgeInsets.only(bottom: 8),
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
        decoration: BoxDecoration(
          color: isOutgoing ? Theme.of(context).primaryColor : Colors.grey.shade200,
          borderRadius: BorderRadius.circular(12),
        ),
        child: Text(
          m.content,
          style: TextStyle(color: isOutgoing ? Colors.white : Colors.black87),
        ),
      ),
    );
  }

  Widget _input() {
    return Container(
      padding: const EdgeInsets.all(12),
      child: Row(
        children: [
          Expanded(
            child: TextField(
              controller: _controller,
              decoration: const InputDecoration(
                hintText: 'Type a private message...',
                border: OutlineInputBorder(),
                contentPadding: EdgeInsets.symmetric(horizontal: 16, vertical: 12),
              ),
              textInputAction: TextInputAction.send,
              onSubmitted: (_) => _send(),
            ),
          ),
          const SizedBox(width: 8),
          IconButton.filled(icon: const Icon(Icons.send), onPressed: _send),
        ],
      ),
    );
  }
}
