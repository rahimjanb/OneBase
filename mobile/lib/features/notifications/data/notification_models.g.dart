// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'notification_models.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_NotifAction _$NotifActionFromJson(Map<String, dynamic> json) => _NotifAction(
  id: json['id'] as String,
  label: json['label'] as String,
  kind: json['kind'] as String? ?? 'secondary',
  action: json['action'] as String,
);

Map<String, dynamic> _$NotifActionToJson(_NotifAction instance) =>
    <String, dynamic>{
      'id': instance.id,
      'label': instance.label,
      'kind': instance.kind,
      'action': instance.action,
    };

_Notif _$NotifFromJson(Map<String, dynamic> json) => _Notif(
  id: json['id'] as String,
  type: json['type'] as String? ?? 'system',
  category: json['category'] as String? ?? 'system',
  kind: json['kind'] as String?,
  title: json['title'] as String,
  body: json['body'] as String?,
  href: json['href'] as String?,
  createdAt: json['createdAt'] as String,
  read: json['read'] as bool? ?? false,
  severity: json['severity'] as String? ?? 'info',
  actions:
      (json['actions'] as List<dynamic>?)
          ?.map((e) => NotifAction.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <NotifAction>[],
);

Map<String, dynamic> _$NotifToJson(_Notif instance) => <String, dynamic>{
  'id': instance.id,
  'type': instance.type,
  'category': instance.category,
  'kind': ?instance.kind,
  'title': instance.title,
  'body': ?instance.body,
  'href': ?instance.href,
  'createdAt': instance.createdAt,
  'read': instance.read,
  'severity': instance.severity,
  'actions': instance.actions.map((e) => e.toJson()).toList(),
};

_NotifCounts _$NotifCountsFromJson(Map<String, dynamic> json) => _NotifCounts(
  all: (json['all'] as num?)?.toInt() ?? 0,
  unread: (json['unread'] as num?)?.toInt() ?? 0,
  tasks: (json['tasks'] as num?)?.toInt() ?? 0,
  ai: (json['ai'] as num?)?.toInt() ?? 0,
  hr: (json['hr'] as num?)?.toInt() ?? 0,
);

Map<String, dynamic> _$NotifCountsToJson(_NotifCounts instance) =>
    <String, dynamic>{
      'all': instance.all,
      'unread': instance.unread,
      'tasks': instance.tasks,
      'ai': instance.ai,
      'hr': instance.hr,
    };
