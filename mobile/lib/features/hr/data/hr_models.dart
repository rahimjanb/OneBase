import 'package:freezed_annotation/freezed_annotation.dart';

part 'hr_models.freezed.dart';
part 'hr_models.g.dart';

// HR (Verifix через OneBase, `/api/hr/*`): доступ — право `hr.read` или руководитель отдела с подразделениями (`hrAccess` из
// `/api/auth/me`). Фото сотрудников в OneBase нет — инициалы. Время HR — местное (Ташкент), показывается как есть.

@freezed
abstract class HrAttention with _$HrAttention {
  const factory HrAttention({
    required String employeeId,
    required String employee,
    String? division,

    /// absent | late | unknown
    required String kind,
    String? firstIn,
    int? lateMinutes,
  }) = _HrAttention;

  factory HrAttention.fromJson(Map<String, Object?> json) => _$HrAttentionFromJson(json);
}

@freezed
abstract class HrDivisionDay with _$HrDivisionDay {
  const factory HrDivisionDay({
    int? id,
    required String name,
    @Default(0) int active,
    @Default(0) int scheduled,
    @Default(0) int present,
    @Default(0) int late,
    @Default(0) int absent,
  }) = _HrDivisionDay;

  factory HrDivisionDay.fromJson(Map<String, Object?> json) => _$HrDivisionDayFromJson(json);
}

@freezed
abstract class HrDay with _$HrDay {
  const factory HrDay({
    required String date,
    @Default(0) int active,
    @Default(0) int scheduled,
    @Default(0) int present,
    @Default(0) int late,
    @Default(0) int absent,
    @Default(0) int onLeave,
    @Default(0) int expected,
    @Default(0) int unknown,
    String? avgArrival,
    @Default(<int>[]) List<int> passesByHour,
    @Default(<HrAttention>[]) List<HrAttention> attention,
    @Default(0) int attentionTotal,
    @Default(<HrDivisionDay>[]) List<HrDivisionDay> divisions,
  }) = _HrDay;

  factory HrDay.fromJson(Map<String, Object?> json) => _$HrDayFromJson(json);
}

@freezed
abstract class HrEmployeeRow with _$HrEmployeeRow {
  const factory HrEmployeeRow({
    required String id,
    @Default(0) int verifixId,
    required String fullName,

    /// W — работает, D — уволен, U — не принят.
    @Default('W') String status,
    String? division,
    String? job,
    String? schedule,
    String? lastEventAt,

    /// Present, Absent, OnLeave, DayOff, Expected, NoSchedule, Unknown, NotEmployed
    String? today,
    @Default(false) bool todayLate,
  }) = _HrEmployeeRow;

  factory HrEmployeeRow.fromJson(Map<String, Object?> json) => _$HrEmployeeRowFromJson(json);
}

@freezed
abstract class HrDayResult with _$HrDayResult {
  const factory HrDayResult({
    required String date,
    required String status,
    String? planBegin,
    String? planEnd,
    String? firstIn,
    String? lastOut,
    @Default(false) bool late,
    int? lateMinutes,
    @Default(false) bool earlyLeave,
    int? workedMinutes,
    String? leaveName,
    String? reason,
  }) = _HrDayResult;

  factory HrDayResult.fromJson(Map<String, Object?> json) => _$HrDayResultFromJson(json);
}

@freezed
abstract class HrSummary with _$HrSummary {
  const factory HrSummary({
    @Default(0) int planDays,
    @Default(0) int presentDays,
    @Default(0) int lateDays,
    @Default(0) int lateMinutes,
    @Default(0) int absentDays,
    @Default(0) int leaveDays,
    @Default(0) int workedMinutes,
    @Default(0) int planMinutes,
  }) = _HrSummary;

  factory HrSummary.fromJson(Map<String, Object?> json) => _$HrSummaryFromJson(json);
}

@freezed
abstract class HrEmployeeCard with _$HrEmployeeCard {
  const factory HrEmployeeCard({
    required HrEmployeeRow employee,
    required String from,
    required String to,
    @Default(<HrDayResult>[]) List<HrDayResult> days,
    @Default(HrSummary()) HrSummary summary,
  }) = _HrEmployeeCard;

  factory HrEmployeeCard.fromJson(Map<String, Object?> json) => _$HrEmployeeCardFromJson(json);
}

/// Карточка без отметок по времени (до 5000 строк) — телефону нужны итоги и дни.
HrEmployeeCard hrCardOf(Object? json) {
  final j = {...json! as Map<String, Object?>}..remove('events');
  return HrEmployeeCard.fromJson(j);
}

/// Имена из Verifix — ЗАГЛАВНЫМИ: «ABDUG'ANIYEV ABDUSATTOR» → «Abdug'aniyev Abdusattor» (как `displayName` веба).
String displayName(String raw) => raw
    .toLowerCase()
    .split(RegExp(r'\s+'))
    .where((p) => p.isNotEmpty)
    .map((p) => p[0].toUpperCase() + p.substring(1))
    .join(' ');
