// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'profile_models.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_WorkdayToday _$WorkdayTodayFromJson(Map<String, dynamic> json) =>
    _WorkdayToday(
      status: json['status'] as String,
      shiftStart: json['shiftStart'] as String?,
      shiftEnd: json['shiftEnd'] as String?,
      arrival: json['arrival'] as String?,
      departure: json['departure'] as String?,
      workedMinutes: (json['workedMinutes'] as num?)?.toInt(),
      plannedMinutes: (json['plannedMinutes'] as num?)?.toInt(),
      lateMinutes: (json['lateMinutes'] as num?)?.toInt(),
      leaveName: json['leaveName'] as String?,
      reason: json['reason'] as String?,
    );

Map<String, dynamic> _$WorkdayTodayToJson(_WorkdayToday instance) =>
    <String, dynamic>{
      'status': instance.status,
      'shiftStart': ?instance.shiftStart,
      'shiftEnd': ?instance.shiftEnd,
      'arrival': ?instance.arrival,
      'departure': ?instance.departure,
      'workedMinutes': ?instance.workedMinutes,
      'plannedMinutes': ?instance.plannedMinutes,
      'lateMinutes': ?instance.lateMinutes,
      'leaveName': ?instance.leaveName,
      'reason': ?instance.reason,
    };

_WorkdayDay _$WorkdayDayFromJson(Map<String, dynamic> json) => _WorkdayDay(
  date: json['date'] as String,
  status: json['status'] as String,
  lateMinutes: (json['lateMinutes'] as num?)?.toInt(),
  workedMinutes: (json['workedMinutes'] as num?)?.toInt(),
);

Map<String, dynamic> _$WorkdayDayToJson(_WorkdayDay instance) =>
    <String, dynamic>{
      'date': instance.date,
      'status': instance.status,
      'lateMinutes': ?instance.lateMinutes,
      'workedMinutes': ?instance.workedMinutes,
    };

_WorkdayTotals _$WorkdayTotalsFromJson(Map<String, dynamic> json) =>
    _WorkdayTotals(
      workedMinutes: (json['workedMinutes'] as num?)?.toInt() ?? 0,
      lateCount: (json['lateCount'] as num?)?.toInt() ?? 0,
      lateMinutes: (json['lateMinutes'] as num?)?.toInt() ?? 0,
    );

Map<String, dynamic> _$WorkdayTotalsToJson(_WorkdayTotals instance) =>
    <String, dynamic>{
      'workedMinutes': instance.workedMinutes,
      'lateCount': instance.lateCount,
      'lateMinutes': instance.lateMinutes,
    };

_MyWorkday _$MyWorkdayFromJson(Map<String, dynamic> json) => _MyWorkday(
  available: json['available'] as bool? ?? false,
  unavailableReason: json['unavailableReason'] as String?,
  today: json['today'] == null
      ? null
      : WorkdayToday.fromJson(json['today'] as Map<String, dynamic>),
  week:
      (json['week'] as List<dynamic>?)
          ?.map((e) => WorkdayDay.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <WorkdayDay>[],
  totals: json['totals'] == null
      ? null
      : WorkdayTotals.fromJson(json['totals'] as Map<String, dynamic>),
  syncedAt: json['syncedAt'] as String?,
);

Map<String, dynamic> _$MyWorkdayToJson(_MyWorkday instance) =>
    <String, dynamic>{
      'available': instance.available,
      'unavailableReason': ?instance.unavailableReason,
      'today': ?instance.today?.toJson(),
      'week': instance.week.map((e) => e.toJson()).toList(),
      'totals': ?instance.totals?.toJson(),
      'syncedAt': ?instance.syncedAt,
    };

_MyTasks _$MyTasksFromJson(Map<String, dynamic> json) => _MyTasks(
  available: json['available'] as bool? ?? false,
  total: (json['total'] as num?)?.toInt() ?? 0,
  fresh: (json['new'] as num?)?.toInt() ?? 0,
  inProgress: (json['inProgress'] as num?)?.toInt() ?? 0,
  waiting: (json['waiting'] as num?)?.toInt() ?? 0,
  overdue: (json['overdue'] as num?)?.toInt() ?? 0,
  done: (json['done'] as num?)?.toInt() ?? 0,
);

Map<String, dynamic> _$MyTasksToJson(_MyTasks instance) => <String, dynamic>{
  'available': instance.available,
  'total': instance.total,
  'new': instance.fresh,
  'inProgress': instance.inProgress,
  'waiting': instance.waiting,
  'overdue': instance.overdue,
  'done': instance.done,
};

_NotificationPrefs _$NotificationPrefsFromJson(Map<String, dynamic> json) =>
    _NotificationPrefs(
      muted:
          (json['muted'] as List<dynamic>?)?.map((e) => e as String).toList() ??
          const <String>[],
      categories:
          (json['categories'] as List<dynamic>?)
              ?.map((e) => e as String)
              .toList() ??
          const <String>[],
    );

Map<String, dynamic> _$NotificationPrefsToJson(_NotificationPrefs instance) =>
    <String, dynamic>{
      'muted': instance.muted,
      'categories': instance.categories,
    };

_TelegramLink _$TelegramLinkFromJson(Map<String, dynamic> json) =>
    _TelegramLink(
      permissionId: json['permissionId'] as String,
      account: json['account'] as String,
      accountUsername: json['accountUsername'] as String?,
      username: json['username'] as String?,
      mode: json['mode'] as String?,
      verifiedAt: json['verifiedAt'] as String?,
    );

Map<String, dynamic> _$TelegramLinkToJson(_TelegramLink instance) =>
    <String, dynamic>{
      'permissionId': instance.permissionId,
      'account': instance.account,
      'accountUsername': ?instance.accountUsername,
      'username': ?instance.username,
      'mode': ?instance.mode,
      'verifiedAt': ?instance.verifiedAt,
    };

_TelegramCode _$TelegramCodeFromJson(Map<String, dynamic> json) =>
    _TelegramCode(
      code: json['code'] as String,
      expiresAt: json['expiresAt'] as String,
      sendTo:
          (json['sendTo'] as List<dynamic>?)
              ?.map((e) => e as String)
              .toList() ??
          const <String>[],
    );

Map<String, dynamic> _$TelegramCodeToJson(_TelegramCode instance) =>
    <String, dynamic>{
      'code': instance.code,
      'expiresAt': instance.expiresAt,
      'sendTo': instance.sendTo,
    };
