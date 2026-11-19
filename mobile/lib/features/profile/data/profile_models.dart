import 'package:freezed_annotation/freezed_annotation.dart';

part 'profile_models.freezed.dart';
part 'profile_models.g.dart';

// Профиль: «Мой день» (`/api/me/workday`), мои задачи (`/api/me/tasks`), «Что присылать» (`/api/notifications/preferences`),
// привязка Telegram (`/api/me/telegram`). Тема, язык и организация 1С сервером не хранятся — настройки телефона.

@freezed
abstract class WorkdayToday with _$WorkdayToday {
  const factory WorkdayToday({
    /// not_started, at_work, on_break, out, finished, day_off, on_leave, absent, no_schedule, unclear, not_employed
    required String status,
    String? shiftStart,
    String? shiftEnd,
    String? arrival,
    String? departure,
    int? workedMinutes,
    int? plannedMinutes,
    int? lateMinutes,
    String? leaveName,
    String? reason,
  }) = _WorkdayToday;

  factory WorkdayToday.fromJson(Map<String, Object?> json) => _$WorkdayTodayFromJson(json);
}

@freezed
abstract class WorkdayDay with _$WorkdayDay {
  const factory WorkdayDay({
    required String date,

    /// ok, late, early_leave, absent, leave, day_off, not_started, no_schedule, unclear, not_employed, future
    required String status,
    int? lateMinutes,
    int? workedMinutes,
  }) = _WorkdayDay;

  factory WorkdayDay.fromJson(Map<String, Object?> json) => _$WorkdayDayFromJson(json);
}

@freezed
abstract class WorkdayTotals with _$WorkdayTotals {
  const factory WorkdayTotals({@Default(0) int workedMinutes, @Default(0) int lateCount, @Default(0) int lateMinutes}) = _WorkdayTotals;

  factory WorkdayTotals.fromJson(Map<String, Object?> json) => _$WorkdayTotalsFromJson(json);
}

@freezed
abstract class MyWorkday with _$MyWorkday {
  const factory MyWorkday({
    @Default(false) bool available,
    String? unavailableReason,
    WorkdayToday? today,
    @Default(<WorkdayDay>[]) List<WorkdayDay> week,
    WorkdayTotals? totals,
    String? syncedAt,
  }) = _MyWorkday;

  factory MyWorkday.fromJson(Map<String, Object?> json) => _$MyWorkdayFromJson(json);
}

@freezed
abstract class MyTasks with _$MyTasks {
  const factory MyTasks({
    @Default(false) bool available,
    @Default(0) int total,
    @JsonKey(name: 'new') @Default(0) int fresh,
    @Default(0) int inProgress,
    @Default(0) int waiting,
    @Default(0) int overdue,
    @Default(0) int done,
  }) = _MyTasks;

  factory MyTasks.fromJson(Map<String, Object?> json) => _$MyTasksFromJson(json);
}

@freezed
abstract class NotificationPrefs with _$NotificationPrefs {
  const factory NotificationPrefs({
    @Default(<String>[]) List<String> muted,
    @Default(<String>[]) List<String> categories,
  }) = _NotificationPrefs;

  factory NotificationPrefs.fromJson(Map<String, Object?> json) => _$NotificationPrefsFromJson(json);
}

@freezed
abstract class TelegramLink with _$TelegramLink {
  const factory TelegramLink({
    required String permissionId,
    required String account,
    String? accountUsername,
    String? username,

    /// Full — полный доступ; иначе — только отправленные данные.
    String? mode,
    String? verifiedAt,
  }) = _TelegramLink;

  factory TelegramLink.fromJson(Map<String, Object?> json) => _$TelegramLinkFromJson(json);
}

@freezed
abstract class TelegramCode with _$TelegramCode {
  const factory TelegramCode({required String code, required String expiresAt, @Default(<String>[]) List<String> sendTo}) = _TelegramCode;

  factory TelegramCode.fromJson(Map<String, Object?> json) => _$TelegramCodeFromJson(json);
}
