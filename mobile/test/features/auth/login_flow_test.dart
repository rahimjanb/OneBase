import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:onebase_mobile/app/app.dart';
import 'package:onebase_mobile/app/shell/bottom_nav.dart';
import 'package:onebase_mobile/core/auth/token_storage.dart';
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

  setUp(() {
    db = memoryDb();
    server = FakeServer();
    tokens = MemoryTokenStorage();
  });
  tearDown(() => db.close());

  Future<void> pumpApp(WidgetTester tester) async {
    await tester.pumpWidget(
      ProviderScope(
        retry: (_, _) => null,
        overrides: baseOverrides(db: db, adapter: server.adapter, tokens: tokens, settings: {SettingsStore.localeKey: 'ru'}),
        child: const OneBaseApp(),
      ),
    );
    await tester.pumpAndSettle();
  }

  Finder nav(String label) => find.descendant(of: find.byType(ObNavScaffold), matching: find.text(label));

  testWidgets('без входа — экран входа; пустые поля — подсказка', (tester) async {
    await pumpApp(tester);
    expect(find.text('Вход в OneBase'), findsOneWidget);
    await tester.tap(find.text('Войти'));
    await tester.pumpAndSettle();
    expect(find.text('Введите логин и пароль.'), findsOneWidget);
    expect(server.requests, isEmpty);
  });

  testWidgets('неверный пароль — понятная ошибка; верный — главная с вкладками по правам', (tester) async {
    server.json('POST', '/api/auth/login', {}, status: 401);
    await pumpApp(tester);
    await tester.enterText(find.byType(TextField).at(0), 'ivan');
    await tester.enterText(find.byType(TextField).at(1), 'wrong');
    await tester.tap(find.text('Войти'));
    await tester.pumpAndSettle();
    expect(find.text('Неверный логин или пароль.'), findsOneWidget);

    server
      ..json('POST', '/api/auth/login', {'accessToken': fakeJwt('u-1'), 'expiresAt': '2036-11-15T00:00:00Z'})
      ..json('GET', '/api/auth/me', profileJson(permissions: ['sales.read', 'work.tasks']));
    await tester.enterText(find.byType(TextField).at(1), 'secret');
    await tester.tap(find.text('Войти'));
    await tester.pumpAndSettle();

    expect(find.text('Вход в OneBase'), findsNothing);
    expect(nav('Главная'), findsOneWidget);
    expect(nav('Аналитика'), findsOneWidget);
    expect(nav('Задачи'), findsOneWidget);
    expect(nav('Консультант'), findsNothing);
    expect(tokens.value, isNotNull);
  });

  testWidgets('сервер закрыл сеанс — экран входа с причиной', (tester) async {
    tokens.value = StoredToken(fakeJwt('u-1'), DateTime(2036));
    server.json('GET', '/api/auth/me', {}, status: 401);
    await pumpApp(tester);
    expect(find.text('Вход в OneBase'), findsOneWidget);
    expect(find.textContaining('Сеанс закрыт'), findsOneWidget);
  });

  testWidgets('выход из профиля с подтверждением', (tester) async {
    tokens.value = StoredToken(fakeJwt('u-1'), DateTime(2036));
    server
      ..json('GET', '/api/auth/me', profileJson())
      ..on('POST', '/api/auth/logout', (_) => FakeAdapter.empty());
    await pumpApp(tester);
    await tester.tap(nav('Профиль'));
    await tester.pumpAndSettle();
    final logout = find.text('Выйти');
    await tester.scrollUntilVisible(logout, 200, scrollable: find.byType(Scrollable).hitTestable().first);
    await tester.ensureVisible(logout);
    await tester.pumpAndSettle();
    await tester.tap(logout);
    await tester.pumpAndSettle();
    expect(find.text('Выйти из OneBase?'), findsOneWidget);
    await tester.tap(find.widgetWithText(TextButton, 'Выйти'));
    await tester.pumpAndSettle();
    expect(find.text('Вход в OneBase'), findsOneWidget);
    expect(server.count('POST', '/api/auth/logout'), 1);
    expect(tokens.value, isNull);
  });

  testWidgets('первый запуск без сети с сохранённым токеном — «Повторить», затем вход', (tester) async {
    tokens.value = StoredToken(fakeJwt('u-1'), DateTime(2036));
    server.offline('GET', '/api/auth/me');
    await pumpApp(tester);
    expect(find.text('Не удалось проверить вход'), findsOneWidget);
    server.json('GET', '/api/auth/me', profileJson());
    await tester.tap(find.text('Повторить'));
    await tester.pumpAndSettle();
    expect(nav('Главная'), findsOneWidget);
  });
}
