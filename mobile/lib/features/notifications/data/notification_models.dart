import 'package:freezed_annotation/freezed_annotation.dart';

part 'notification_models.freezed.dart';
part 'notification_models.g.dart';

// Уведомления (`/api/notifications*`): тип, раздел, важность, действия и ссылка (путь веба) — с сервера. Отключённые в «Что присылать»
// разделы сервер не отдаёт ни в ленте, ни в счётчиках. Realtime нет: веб опрашивает; телефон — при открытии и раз в минуту на экране.

@freezed
abstract class NotifAction with _$NotifAction {
  const factory NotifAction({required String id, required String label, @Default('secondary') String kind, required String action}) = _NotifAction;

  factory NotifAction.fromJson(Map<String, Object?> json) => _$NotifActionFromJson(json);
}

@freezed
abstract class Notif with _$Notif {
  const factory Notif({
    required String id,
    @Default('system') String type,
    @Default('system') String category,
    String? kind,
    required String title,
    String? body,
    String? href,
    required String createdAt,
    @Default(false) bool read,

    /// critical | warning | info
    @Default('info') String severity,
    @Default(<NotifAction>[]) List<NotifAction> actions,
  }) = _Notif;

  factory Notif.fromJson(Map<String, Object?> json) => _$NotifFromJson(json);
}

@freezed
abstract class NotifCounts with _$NotifCounts {
  const factory NotifCounts({@Default(0) int all, @Default(0) int unread, @Default(0) int tasks, @Default(0) int ai, @Default(0) int hr}) = _NotifCounts;

  factory NotifCounts.fromJson(Map<String, Object?> json) => _$NotifCountsFromJson(json);
}
