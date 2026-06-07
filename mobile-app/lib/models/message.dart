class Message {
  final String id;
  final MessageType type;
  final String from;
  final String? to;
  final String content;
  final DateTime timestamp;
  final bool isRead;
  final int? frequency;

  Message({
    required this.id,
    required this.type,
    required this.from,
    this.to,
    required this.content,
    required this.timestamp,
    this.isRead = false,
    this.frequency,
  });

  factory Message.fromJson(Map<String, dynamic> json) {
    return Message(
      id: json['id'] ?? '',
      type: MessageType.fromString(json['messageType'] ?? json['type'] ?? 'system'),
      from: json['from'] ?? '',
      to: json['to'],
      content: json['content'] ?? '',
      timestamp: json['timestamp'] != null
          ? DateTime.tryParse(json['timestamp'].toString()) ?? DateTime.now()
          : DateTime.now(),
      isRead: json['isRead'] ?? false,
      frequency: json['frequency'],
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'type': type.value,
      'from': from,
      'to': to,
      'content': content,
      'timestamp': timestamp.toIso8601String(),
      'isRead': isRead,
      'frequency': frequency,
    };
  }

  Message copyWith({
    String? id,
    MessageType? type,
    String? from,
    String? to,
    String? content,
    DateTime? timestamp,
    bool? isRead,
    int? frequency,
  }) {
    return Message(
      id: id ?? this.id,
      type: type ?? this.type,
      from: from ?? this.from,
      to: to ?? this.to,
      content: content ?? this.content,
      timestamp: timestamp ?? this.timestamp,
      isRead: isRead ?? this.isRead,
      frequency: frequency ?? this.frequency,
    );
  }
}

enum MessageType {
  private,
  radio,
  system;

  String get value {
    switch (this) {
      case MessageType.private:
        return 'private';
      case MessageType.radio:
        return 'radio';
      case MessageType.system:
        return 'system';
    }
  }

  static MessageType fromString(String value) {
    switch (value.toLowerCase()) {
      case 'private':
        return MessageType.private;
      case 'radio':
        return MessageType.radio;
      default:
        return MessageType.system;
    }
  }
}
