import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/api_exception.dart';
import '../api/api_providers.dart';
import '../cache/cache_store.dart';
import '../core_providers.dart';
import '../errors/app_failure.dart';
import '../logging/log.dart';
import '../storage/settings_store.dart';
import '../synchronization/outbox.dart';
import 'auth_repository.dart';
import 'token_holder.dart';
import 'token_storage.dart';
import 'user_profile.dart';

/// Состояние входа.
@immutable
sealed class AuthState {
  const AuthState();
}

/// Проверяем сохранённый вход (запуск).
class AuthChecking extends AuthState {
  const AuthChecking();
}

/// Токен есть, но проверить его не удалось и сохранённого профиля нет (первый запуск без сети): «Повторить» или «Выйти».
class AuthRestoreFailed extends AuthState {
  const AuthRestoreFailed(this.failure);

  final AppFailure failure;
}

enum SignOutReason {
  /// Сервер закрыл сеанс: выход на другом устройстве, смена пароля, пользователь отключён.
  expired,
}

class AuthSignedOut extends AuthState {
  const AuthSignedOut([this.reason]);

  final SignOutReason? reason;
}

class AuthSignedIn extends AuthState {
  const AuthSignedIn(this.profile, {this.offline = false});

  final UserProfile profile;

  /// Профиль из кэша: сервер сейчас недоступен.
  final bool offline;
}

/// Хранилище токена — подменяется в тестах.
final tokenStorageProvider = Provider<TokenStorage>((ref) => SecureTokenStorage());

/// Вход в OneBase. Токен долгий (у сервера — 10 лет), сеанс закрывается на сервере, поэтому refresh-токена нет:
/// - запуск: токен из Keychain/Keystore → `GET /api/auth/me`. Без сети — профиль из кэша. 401 — на экран входа;
/// - любой 401 во время работы → сеанс закрыт → токен и кэш удаляются → экран входа с причиной;
/// - возврат в приложение → профиль перечитывается: права меняются без повторного входа, как на сервере.
class AuthController extends Notifier<AuthState> {
  static const _profileKey = 'auth/me';
  static const refreshEvery = Duration(seconds: 60);

  DateTime? _lastRefresh;
  bool _installChecked = false;

  TokenStorage get _storage => ref.read(tokenStorageProvider);
  TokenHolder get _tokens => ref.read(tokenHolderProvider);
  AuthRepository get _repo => ref.read(authRepositoryProvider);
  CacheStore get _cache => ref.read(cacheStoreProvider);
  DateTime _now() => ref.read(clockProvider)();

  @override
  AuthState build() {
    final sub = ref.watch(unauthorizedSignalProvider).stream.listen((_) => unawaited(expire()));
    ref.onDispose(sub.cancel);
    Future.microtask(restore);
    return const AuthChecking();
  }

  /// Проверить сохранённый вход.
  Future<void> restore() async {
    state = const AuthChecking();
    await _forgetPreviousInstall();
    StoredToken? stored;
    try {
      stored = await _storage.read();
    } on Object catch (e, s) {
      // Android после восстановления из резервной копии не может расшифровать старые данные — считаем, что входа нет.
      Log.e('auth', 'Хранилище токена не читается', e, s);
      await _safeClearStorage();
    }
    if (stored == null || stored.isExpired(_now())) {
      if (stored != null) await _safeClearStorage();
      _set(const AuthSignedOut());
      return;
    }

    _tokens.value = stored.token;
    try {
      final profile = await _repo.me();
      await _remember(profile);
      _set(AuthSignedIn(profile));
    } on ApiException catch (e) {
      if (e.kind == ApiErrorKind.unauthorized) {
        await _clearLocal();
        _set(const AuthSignedOut(SignOutReason.expired));
        return;
      }
      final cached = await _cachedProfile(stored.token);
      _set(cached != null ? AuthSignedIn(cached, offline: true) : AuthRestoreFailed(AppFailure.from(e)));
    }
  }

  /// Вход логином (или почтой) и паролем. Ошибка — [ApiException]: 401 — неверно, 429 — лимит попыток.
  Future<void> login(String login, String password) async {
    final stored = await _repo.login(login.trim(), password);
    _tokens.value = stored.token;
    final UserProfile profile;
    try {
      profile = await _repo.me();
    } on Object {
      _tokens.value = null;
      rethrow;
    }
    await _storage.write(stored);
    // Вошёл другой пользователь — чужой кэш на телефоне не остаётся.
    await _cache.clear(keepUserId: profile.id);
    await clearPendingOps(ref.read(appDatabaseProvider), keepUserId: profile.id);
    await _remember(profile);
    _lastRefresh = _now();
    _set(AuthSignedIn(profile));
  }

  /// Выход: сеанс закрывается на сервере (если сеть есть), токен и кэш удаляются с телефона в любом случае.
  Future<void> logout() async {
    try {
      await _repo.logout().timeout(const Duration(seconds: 5));
    } on Object catch (e) {
      Log.w('auth', 'Выход на сервере не подтверждён — выходим на телефоне', e);
    }
    await _clearLocal();
    _set(const AuthSignedOut());
  }

  /// Сервер ответил 401 на запрос с токеном — сеанс закрыт.
  Future<void> expire() async {
    if (state is! AuthSignedIn) return;
    Log.i('auth', 'Сеанс закрыт сервером');
    await _clearLocal();
    _set(const AuthSignedOut(SignOutReason.expired));
  }

  /// Перечитать профиль и права (возврат в приложение, не чаще раза в минуту; [force] — сразу).
  Future<void> refreshProfile({bool force = false}) async {
    final current = state;
    if (current is! AuthSignedIn) return;
    final now = _now();
    if (!force && _lastRefresh != null && now.difference(_lastRefresh!) < refreshEvery && !current.offline) return;
    _lastRefresh = now;
    try {
      final profile = await _repo.me();
      await _remember(profile);
      if (state is AuthSignedIn) _set(AuthSignedIn(profile));
    } on ApiException catch (e) {
      // 401 обработает сигнал «сеанс закрыт»; сеть — остаёмся на прежнем профиле.
      Log.d('auth', 'Профиль не обновлён: ${e.kind.name}');
    }
  }

  /// Свежая установка (в базе приложения нет отметки): на iOS Keychain переживает удаление приложения, и без этого после
  /// переустановки подхватился бы старый вход. Старый токен удаляется — нужен новый вход.
  Future<void> _forgetPreviousInstall() async {
    if (_installChecked) return;
    _installChecked = true;
    if (ref.read(initialSettingsProvider).containsKey(SettingsStore.installedKey)) return;
    Log.i('auth', 'Первый запуск после установки — прежний вход с этого устройства не используется');
    await _safeClearStorage();
    try {
      await ref.read(settingsStoreProvider).write(SettingsStore.installedKey, _now().toUtc().toIso8601String());
    } on Object catch (e, s) {
      Log.e('auth', 'Отметка установки не записана', e, s);
    }
  }

  void _set(AuthState next) {
    if (ref.mounted) state = next;
  }

  Future<void> _remember(UserProfile profile) => _cache.write(_profileKey, profile.toJson(), userId: profile.id);

  Future<UserProfile?> _cachedProfile(String token) async {
    final userId = userIdOfToken(token);
    if (userId == null) return null;
    final cached = await _cache.read(_profileKey, userId: userId);
    final json = cached?.json;
    if (json is! Map<String, Object?>) return null;
    try {
      return UserProfile.fromJson(json);
    } on Object {
      return null;
    }
  }

  Future<void> _clearLocal() async {
    _tokens.value = null;
    _lastRefresh = null;
    await _safeClearStorage();
    await _cache.clear();
    await clearPendingOps(ref.read(appDatabaseProvider));
  }

  Future<void> _safeClearStorage() async {
    try {
      await _storage.clear();
    } on Object catch (e, s) {
      Log.e('auth', 'Хранилище токена не очищено', e, s);
    }
  }
}

final authControllerProvider = NotifierProvider<AuthController, AuthState>(AuthController.new);

/// Профиль вошедшего пользователя (null — не вошёл).
final currentProfileProvider = Provider<UserProfile?>((ref) {
  final auth = ref.watch(authControllerProvider);
  return auth is AuthSignedIn ? auth.profile : null;
});
