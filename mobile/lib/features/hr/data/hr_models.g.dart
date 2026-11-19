// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'hr_models.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_HrAttention _$HrAttentionFromJson(Map<String, dynamic> json) => _HrAttention(
  employeeId: json['employeeId'] as String,
  employee: json['employee'] as String,
  division: json['division'] as String?,
  kind: json['kind'] as String,
  firstIn: json['firstIn'] as String?,
  lateMinutes: (json['lateMinutes'] as num?)?.toInt(),
);

Map<String, dynamic> _$HrAttentionToJson(_HrAttention instance) =>
    <String, dynamic>{
      'employeeId': instance.employeeId,
      'employee': instance.employee,
      'division': ?instance.division,
      'kind': instance.kind,
      'firstIn': ?instance.firstIn,
      'lateMinutes': ?instance.lateMinutes,
    };

_HrDivisionDay _$HrDivisionDayFromJson(Map<String, dynamic> json) =>
    _HrDivisionDay(
      id: (json['id'] as num?)?.toInt(),
      name: json['name'] as String,
      active: (json['active'] as num?)?.toInt() ?? 0,
      scheduled: (json['scheduled'] as num?)?.toInt() ?? 0,
      present: (json['present'] as num?)?.toInt() ?? 0,
      late: (json['late'] as num?)?.toInt() ?? 0,
      absent: (json['absent'] as num?)?.toInt() ?? 0,
    );

Map<String, dynamic> _$HrDivisionDayToJson(_HrDivisionDay instance) =>
    <String, dynamic>{
      'id': ?instance.id,
      'name': instance.name,
      'active': instance.active,
      'scheduled': instance.scheduled,
      'present': instance.present,
      'late': instance.late,
      'absent': instance.absent,
    };

_HrDay _$HrDayFromJson(Map<String, dynamic> json) => _HrDay(
  date: json['date'] as String,
  active: (json['active'] as num?)?.toInt() ?? 0,
  scheduled: (json['scheduled'] as num?)?.toInt() ?? 0,
  present: (json['present'] as num?)?.toInt() ?? 0,
  late: (json['late'] as num?)?.toInt() ?? 0,
  absent: (json['absent'] as num?)?.toInt() ?? 0,
  onLeave: (json['onLeave'] as num?)?.toInt() ?? 0,
  expected: (json['expected'] as num?)?.toInt() ?? 0,
  unknown: (json['unknown'] as num?)?.toInt() ?? 0,
  avgArrival: json['avgArrival'] as String?,
  passesByHour:
      (json['passesByHour'] as List<dynamic>?)
          ?.map((e) => (e as num).toInt())
          .toList() ??
      const <int>[],
  attention:
      (json['attention'] as List<dynamic>?)
          ?.map((e) => HrAttention.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <HrAttention>[],
  attentionTotal: (json['attentionTotal'] as num?)?.toInt() ?? 0,
  divisions:
      (json['divisions'] as List<dynamic>?)
          ?.map((e) => HrDivisionDay.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <HrDivisionDay>[],
);

Map<String, dynamic> _$HrDayToJson(_HrDay instance) => <String, dynamic>{
  'date': instance.date,
  'active': instance.active,
  'scheduled': instance.scheduled,
  'present': instance.present,
  'late': instance.late,
  'absent': instance.absent,
  'onLeave': instance.onLeave,
  'expected': instance.expected,
  'unknown': instance.unknown,
  'avgArrival': ?instance.avgArrival,
  'passesByHour': instance.passesByHour,
  'attention': instance.attention.map((e) => e.toJson()).toList(),
  'attentionTotal': instance.attentionTotal,
  'divisions': instance.divisions.map((e) => e.toJson()).toList(),
};

_HrEmployeeRow _$HrEmployeeRowFromJson(Map<String, dynamic> json) =>
    _HrEmployeeRow(
      id: json['id'] as String,
      verifixId: (json['verifixId'] as num?)?.toInt() ?? 0,
      fullName: json['fullName'] as String,
      status: json['status'] as String? ?? 'W',
      division: json['division'] as String?,
      job: json['job'] as String?,
      schedule: json['schedule'] as String?,
      lastEventAt: json['lastEventAt'] as String?,
      today: json['today'] as String?,
      todayLate: json['todayLate'] as bool? ?? false,
    );

Map<String, dynamic> _$HrEmployeeRowToJson(_HrEmployeeRow instance) =>
    <String, dynamic>{
      'id': instance.id,
      'verifixId': instance.verifixId,
      'fullName': instance.fullName,
      'status': instance.status,
      'division': ?instance.division,
      'job': ?instance.job,
      'schedule': ?instance.schedule,
      'lastEventAt': ?instance.lastEventAt,
      'today': ?instance.today,
      'todayLate': instance.todayLate,
    };

_HrDayResult _$HrDayResultFromJson(Map<String, dynamic> json) => _HrDayResult(
  date: json['date'] as String,
  status: json['status'] as String,
  planBegin: json['planBegin'] as String?,
  planEnd: json['planEnd'] as String?,
  firstIn: json['firstIn'] as String?,
  lastOut: json['lastOut'] as String?,
  late: json['late'] as bool? ?? false,
  lateMinutes: (json['lateMinutes'] as num?)?.toInt(),
  earlyLeave: json['earlyLeave'] as bool? ?? false,
  workedMinutes: (json['workedMinutes'] as num?)?.toInt(),
  leaveName: json['leaveName'] as String?,
  reason: json['reason'] as String?,
);

Map<String, dynamic> _$HrDayResultToJson(_HrDayResult instance) =>
    <String, dynamic>{
      'date': instance.date,
      'status': instance.status,
      'planBegin': ?instance.planBegin,
      'planEnd': ?instance.planEnd,
      'firstIn': ?instance.firstIn,
      'lastOut': ?instance.lastOut,
      'late': instance.late,
      'lateMinutes': ?instance.lateMinutes,
      'earlyLeave': instance.earlyLeave,
      'workedMinutes': ?instance.workedMinutes,
      'leaveName': ?instance.leaveName,
      'reason': ?instance.reason,
    };

_HrSummary _$HrSummaryFromJson(Map<String, dynamic> json) => _HrSummary(
  planDays: (json['planDays'] as num?)?.toInt() ?? 0,
  presentDays: (json['presentDays'] as num?)?.toInt() ?? 0,
  lateDays: (json['lateDays'] as num?)?.toInt() ?? 0,
  lateMinutes: (json['lateMinutes'] as num?)?.toInt() ?? 0,
  absentDays: (json['absentDays'] as num?)?.toInt() ?? 0,
  leaveDays: (json['leaveDays'] as num?)?.toInt() ?? 0,
  workedMinutes: (json['workedMinutes'] as num?)?.toInt() ?? 0,
  planMinutes: (json['planMinutes'] as num?)?.toInt() ?? 0,
);

Map<String, dynamic> _$HrSummaryToJson(_HrSummary instance) =>
    <String, dynamic>{
      'planDays': instance.planDays,
      'presentDays': instance.presentDays,
      'lateDays': instance.lateDays,
      'lateMinutes': instance.lateMinutes,
      'absentDays': instance.absentDays,
      'leaveDays': instance.leaveDays,
      'workedMinutes': instance.workedMinutes,
      'planMinutes': instance.planMinutes,
    };

_HrEmployeeCard _$HrEmployeeCardFromJson(Map<String, dynamic> json) =>
    _HrEmployeeCard(
      employee: HrEmployeeRow.fromJson(
        json['employee'] as Map<String, dynamic>,
      ),
      from: json['from'] as String,
      to: json['to'] as String,
      days:
          (json['days'] as List<dynamic>?)
              ?.map((e) => HrDayResult.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <HrDayResult>[],
      summary: json['summary'] == null
          ? const HrSummary()
          : HrSummary.fromJson(json['summary'] as Map<String, dynamic>),
    );

Map<String, dynamic> _$HrEmployeeCardToJson(_HrEmployeeCard instance) =>
    <String, dynamic>{
      'employee': instance.employee.toJson(),
      'from': instance.from,
      'to': instance.to,
      'days': instance.days.map((e) => e.toJson()).toList(),
      'summary': instance.summary.toJson(),
    };
