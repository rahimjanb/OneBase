// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'consultant_models.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_ChatMessage _$ChatMessageFromJson(Map<String, dynamic> json) => _ChatMessage(
  id: json['id'] as String,
  role: json['role'] as String,
  status: json['status'] as String? ?? 'completed',
  content: json['content'] as String? ?? '',
  error: json['error'] as String?,
  createdAt: json['createdAt'] as String,
  durationMs: (json['durationMs'] as num?)?.toInt() ?? 0,
  answer: json['answer'] as Map<String, dynamic>?,
  details: json['details'] as Map<String, dynamic>?,
);

Map<String, dynamic> _$ChatMessageToJson(_ChatMessage instance) =>
    <String, dynamic>{
      'id': instance.id,
      'role': instance.role,
      'status': instance.status,
      'content': instance.content,
      'error': ?instance.error,
      'createdAt': instance.createdAt,
      'durationMs': instance.durationMs,
      'answer': ?instance.answer,
      'details': ?instance.details,
    };

_ConversationSummary _$ConversationSummaryFromJson(Map<String, dynamic> json) =>
    _ConversationSummary(
      id: json['id'] as String,
      title: json['title'] as String,
      lastMessageAt: json['lastMessageAt'] as String,
      source: json['source'] as String? ?? 'web',
    );

Map<String, dynamic> _$ConversationSummaryToJson(
  _ConversationSummary instance,
) => <String, dynamic>{
  'id': instance.id,
  'title': instance.title,
  'lastMessageAt': instance.lastMessageAt,
  'source': instance.source,
};

_ConversationView _$ConversationViewFromJson(Map<String, dynamic> json) =>
    _ConversationView(
      id: json['id'] as String,
      title: json['title'] as String,
      messages:
          (json['messages'] as List<dynamic>?)
              ?.map((e) => ChatMessage.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <ChatMessage>[],
    );

Map<String, dynamic> _$ConversationViewToJson(_ConversationView instance) =>
    <String, dynamic>{
      'id': instance.id,
      'title': instance.title,
      'messages': instance.messages.map((e) => e.toJson()).toList(),
    };

_AiStatus _$AiStatusFromJson(Map<String, dynamic> json) => _AiStatus(
  ready: json['ready'] as bool? ?? false,
  message: json['message'] as String?,
);

Map<String, dynamic> _$AiStatusToJson(_AiStatus instance) => <String, dynamic>{
  'ready': instance.ready,
  'message': ?instance.message,
};
