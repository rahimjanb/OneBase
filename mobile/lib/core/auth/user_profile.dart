import 'package:freezed_annotation/freezed_annotation.dart';

import 'perm.dart';

part 'user_profile.freezed.dart';
part 'user_profile.g.dart';

/// Охват «Продаж»: уровень (Organization | Rm | Supervisor | Agent) и своя страница.
@freezed
abstract class SalesScope with _$SalesScope {
  const factory SalesScope({
    @Default('Organization') String level,
    String? home,
  }) = _SalesScope;

  factory SalesScope.fromJson(Map<String, Object?> json) => _$SalesScopeFromJson(json);
}

/// Кто вошёл — ответ `GET /api/auth/me`. Права и роли сервер берёт из БД на каждом запросе, поэтому профиль перечитывается
/// при запуске и возврате в приложение. Используется только для навигации — доступ всё равно проверяет сервер.
@freezed
abstract class UserProfile with _$UserProfile {
  const factory UserProfile({
    required String id,
    required String login,
    String? email,
    required String name,
    String? firstName,
    String? lastName,
    String? position,
    String? departmentName,
    @Default(<String>[]) List<String> roles,

    /// Главная на сайте: overview — «Обзор» компании, my_day — «Мой день» сотрудника, work — «Рабочий стол» снабжения и склада.
    @Default('overview') String home,
    @Default(<String>[]) List<String> permissions,
    @Default(false) bool canOpenSettings,
    @Default(false) bool hrAccess,
    @Default(false) bool hiringRequests,
    SalesScope? salesScope,

    /// Отделы, задачи которых открываются пользователю.
    @Default(<String>[]) List<String> departments,
  }) = _UserProfile;

  const UserProfile._();

  factory UserProfile.fromJson(Map<String, Object?> json) => _$UserProfileFromJson(json);

  bool can(String permission) => permissions.contains(permission);

  bool canAny(Iterable<String> codes) => codes.any(permissions.contains);

  /// Продажи на уровне всей организации (республика): РМ, СВ и ТП видят только своё.
  bool get salesOrganization => can(Perm.salesRead) && (salesScope?.level ?? 'Organization') == 'Organization';

  /// Инициалы для аватара: «Иван Петров» → «ИП».
  String get initials {
    final parts = name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
    if (parts.isEmpty) return login.isEmpty ? '?' : login[0].toUpperCase();
    return parts.take(2).map((p) => p[0].toUpperCase()).join();
  }
}
