import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:onebase_mobile/core/api/api_exception.dart';
import 'package:onebase_mobile/core/api/api_providers.dart';
import 'package:onebase_mobile/core/auth/auth_controller.dart';
import 'package:onebase_mobile/core/auth/auth_repository.dart';
import 'package:onebase_mobile/core/auth/token_storage.dart';
import 'package:onebase_mobile/core/cache/cache_store.dart';
import 'package:onebase_mobile/core/database/app_database.dart';
import 'package:onebase_mobile/core/storage/settings_store.dart';

import '../../helpers/fake_adapter.dart';
import '../../helpers/fake_server.dart';
import '../../helpers/harness.dart';
import '../../helpers/test_db.dart';

void main() {
  late AppDatabase db;
  late FakeServer server;
  late MemoryTokenStorage tokens;
  var now = DateTime(2026, 11, 19, 10);

  setUp(() {
    db = memoryDb();
    server = FakeServer();
    tokens = MemoryTokenStorage();
    now = DateTime(2026, 11, 19, 10);
  });
  tearDown(() => db.close());

  ProviderContainer make() {
    final c = ProviderContainer.test(
      retry: (_, _) => null,
      overrides: baseOverrides(db: db, adapter: server.adapter, tokens: tokens, clock: () => now),
    );
    return c;
  }

  /// Дождаться конца проверки входа.
  Future<AuthState> settle(ProviderContainer c) async {
    c.read(authControllerProvider);
    for (var i = 0; i < 50 && c.read(authControllerProvider) is AuthChecking; i++) {
      await Future<void>.delayed(Duration.zero);
    }
    return c.read(authControllerProvider);
  }

  final token = fakeJwt('u-1');
  final stored = StoredToken(token, DateTime(2036, 11, 15));

  test('нет сохранённого входа → экран входа', () async {
    final c = make();
    expect(await settle(c), isA<AuthSignedOut>().having((s) => s.reason, 'reason', isNull));
    expect(server.requests, isEmpty);
  });

  test('сохранённый вход → профиль с сервера, токен в заголовке, профиль в кэше', () async {
    tokens.value = stored;
    server.json('GET', '/api/auth/me', profileJson());
    final c = make();
    final s = await settle(c);
    expect(s, isA<AuthSignedIn>().having((s) => s.profile.name, 'name', 'Иван Петров').having((s) => s.offline, 'offline', false));
    expect(server.requests.single.headers['Authorization'], 'Bearer $token');
    expect(await CacheStore(db).read('auth/me', userId: 'u-1'), isNotNull);
  });

  test('сервер закрыл сеанс (401) → выход с причиной, токен удалён', () async {
    tokens.value = stored;
    server.json('GET', '/api/auth/me', {}, status: 401);
    final c = make();
    expect(await settle(c), isA<AuthSignedOut>().having((s) => s.reason, 'reason', SignOutReason.expired));
    expect(tokens.value, isNull);
    expect(c.read(tokenHolderProvider).value, isNull);
  });

  test('нет сети, профиль сохранён → вход без сети', () async {
    tokens.value = stored;
    await CacheStore(db).write('auth/me', profileJson(), userId: 'u-1');
    server.offline('GET', '/api/auth/me');
    final c = make();
    expect(await settle(c), isA<AuthSignedIn>().having((s) => s.offline, 'offline', true));
    expect(tokens.value, isNotNull);
  });

  test('нет сети, профиля нет → «не удалось проверить вход», токен не удаляется', () async {
    tokens.value = stored;
    server.offline('GET', '/api/auth/me');
    final c = make();
    expect(
      await settle(c),
      isA<AuthRestoreFailed>().having((s) => s.failure.kind, 'kind', ApiErrorKind.network),
    );
    expect(tokens.value, isNotNull);
  });

  test('истёкший токен на телефоне → экран входа без запроса', () async {
    tokens.value = StoredToken(token, DateTime(2026, 1, 1));
    final c = make();
    expect(await settle(c), isA<AuthSignedOut>());
    expect(tokens.value, isNull);
    expect(server.requests, isEmpty);
  });

  test('вход: токен сохранён, профиль загружен, чужой кэш удалён', () async {
    await CacheStore(db).write('dashboard/x', {'a': 1}, userId: 'other-user');
    server
      ..json('POST', '/api/auth/login', {'accessToken': token, 'expiresAt': '2036-11-15T21:31:54Z'})
      ..json('GET', '/api/auth/me', profileJson());
    final c = make();
    await settle(c);
    await c.read(authControllerProvider.notifier).login('  ivan ', 'secret');

    final login = server.requests.firstWhere((r) => r.path == '/api/auth/login');
    expect(login.data, {'login': 'ivan', 'password': 'secret'});
    expect(tokens.value?.token, token);
    expect(tokens.value?.expiresAt, DateTime.utc(2036, 11, 15, 21, 31, 54));
    expect(c.read(authControllerProvider), isA<AuthSignedIn>());
    expect(await CacheStore(db).read('dashboard/x', userId: 'other-user'), isNull);
  });

  test('неверный пароль → ошибка 401, токена нет, сеанс не закрывается сигналом', () async {
    server.json('POST', '/api/auth/login', {}, status: 401);
    final c = make();
    await settle(c);
    await expectLater(
      c.read(authControllerProvider.notifier).login('ivan', 'wrong'),
      throwsA(isA<ApiException>().having((e) => e.kind, 'kind', ApiErrorKind.unauthorized)),
    );
    expect(tokens.value, isNull);
    expect(c.read(authControllerProvider), isA<AuthSignedOut>().having((s) => s.reason, 'reason', isNull));
  });

  test('лимит попыток → 429 отдаётся экрану', () async {
    server.json('POST', '/api/auth/login', {}, status: 429);
    final c = make();
    await settle(c);
    await expectLater(
      c.read(authControllerProvider.notifier).login('ivan', 'x'),
      throwsA(isA<ApiException>().having((e) => e.kind, 'kind', ApiErrorKind.rateLimited)),
    );
  });

  test('401 на любом запросе во время работы → сеанс закрыт, кэш удалён', () async {
    tokens.value = stored;
    server
      ..json('GET', '/api/auth/me', profileJson())
      ..json('GET', '/api/work/overview', {}, status: 401);
    final c = make();
    await settle(c);
    await CacheStore(db).write('dashboard/x', {'a': 1}, userId: 'u-1');
    await expectLater(c.read(apiClientProvider).get('/api/work/overview', (j) => j), throwsA(isA<ApiException>()));
    await Future<void>.delayed(Duration.zero);
    await Future<void>.delayed(Duration.zero);
    expect(c.read(authControllerProvider), isA<AuthSignedOut>().having((s) => s.reason, 'reason', SignOutReason.expired));
    expect(tokens.value, isNull);
    expect(await CacheStore(db).count(), 0);
  });

  test('выход: сервер закрывает сеанс, на телефоне — ни токена, ни кэша', () async {
    tokens.value = stored;
    server
      ..json('GET', '/api/auth/me', profileJson())
      ..on('POST', '/api/auth/logout', (_) => FakeAdapter.empty());
    final c = make();
    await settle(c);
    await c.read(authControllerProvider.notifier).logout();
    expect(server.count('POST', '/api/auth/logout'), 1);
    expect(server.requests.last.headers['Authorization'], 'Bearer $token');
    expect(tokens.value, isNull);
    expect(await CacheStore(db).count(), 0);
    expect(c.read(authControllerProvider), isA<AuthSignedOut>().having((s) => s.reason, 'reason', isNull));
  });

  test('выход без сети — всё равно выходим на телефоне', () async {
    tokens.value = stored;
    server
      ..json('GET', '/api/auth/me', profileJson())
      ..offline('POST', '/api/auth/logout');
    final c = make();
    await settle(c);
    await c.read(authControllerProvider.notifier).logout();
    expect(tokens.value, isNull);
    expect(c.read(authControllerProvider), isA<AuthSignedOut>());
  });

  test('возврат в приложение: права перечитываются, но не чаще раза в минуту', () async {
    tokens.value = stored;
    server.json('GET', '/api/auth/me', profileJson(permissions: ['sales.read']));
    final c = make();
    await settle(c);
    server.json('GET', '/api/auth/me', profileJson(permissions: ['sales.read', 'work.tasks']));

    await c.read(authControllerProvider.notifier).refreshProfile();
    expect((c.read(authControllerProvider) as AuthSignedIn).profile.permissions, contains('work.tasks'));
    expect(server.count('GET', '/api/auth/me'), 2);

    now = now.add(const Duration(seconds: 30));
    await c.read(authControllerProvider.notifier).refreshProfile();
    expect(server.count('GET', '/api/auth/me'), 2);

    now = now.add(const Duration(seconds: 31));
    await c.read(authControllerProvider.notifier).refreshProfile();
    expect(server.count('GET', '/api/auth/me'), 3);
  });

  test('свежая установка: токен, оставшийся в Keychain от удалённого приложения, не используется', () async {
    tokens.value = stored;
    server.json('GET', '/api/auth/me', profileJson());
    final c = ProviderContainer.test(
      retry: (_, _) => null,
      overrides: baseOverrides(db: db, adapter: server.adapter, tokens: tokens, clock: () => now, freshInstall: true),
    );
    expect(await settle(c), isA<AuthSignedOut>().having((s) => s.reason, 'reason', isNull));
    expect(tokens.value, isNull);
    expect(server.requests, isEmpty);
    expect(await SettingsStore(db).readAll(), contains(SettingsStore.installedKey));
  });

  test('id пользователя из токена', () {
    expect(userIdOfToken(fakeJwt('01a18991-3300')), '01a18991-3300');
    expect(userIdOfToken('not-a-jwt'), isNull);
    expect(userIdOfToken('a.%%%.c'), isNull);
  });
}
