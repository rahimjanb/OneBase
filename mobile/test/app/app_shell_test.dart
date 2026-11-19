import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:onebase_mobile/app/app.dart';
import 'package:onebase_mobile/app/shell/bottom_nav.dart';
import 'package:onebase_mobile/app/theme/tokens.dart';
import 'package:onebase_mobile/core/auth/token_storage.dart';
import 'package:onebase_mobile/core/config/app_config.dart';
import 'package:onebase_mobile/core/database/app_database.dart';
import 'package:onebase_mobile/core/storage/settings_store.dart';

import '../helpers/fake_server.dart';
import '../helpers/harness.dart';
import '../helpers/test_db.dart';

void main() {
  late AppDatabase db;
  late FakeServer server;

  setUp(() {
    db = memoryDb();
    server = FakeServer()..json('GET', '/api/auth/me', profileJson());
  });
  tearDown(() => db.close());

  /// Приложение с сохранённым входом: проверка входа проходит, открывается оболочка.
  Future<void> pumpApp(WidgetTester tester, {Map<String, String> settings = const {}}) async {
    await tester.pumpWidget(
      ProviderScope(
        retry: (_, _) => null,
        overrides: baseOverrides(
          db: db,
          settings: settings,
          adapter: server.adapter,
          tokens: MemoryTokenStorage(StoredToken(fakeJwt('u-1'), DateTime(2036))),
        ),
        child: const OneBaseApp(),
      ),
    );
    await tester.pumpAndSettle();
  }

  Finder nav(String label) => find.descendant(of: find.byType(ObNavScaffold), matching: find.text(label));

  testWidgets('оболочка как у веба: три вкладки по правам, «Профиль» и синяя кнопка; веер — остальные разделы', (tester) async {
    await pumpApp(tester, settings: {SettingsStore.localeKey: 'ru'});
    for (final label in ['Главная', 'Консультант', 'Аналитика', 'Профиль']) {
      expect(nav(label), findsOneWidget);
    }
    // «Задачи» не помещаются в три вкладки — они в веере.
    expect(nav('Задачи'), findsNothing);
    await tester.tap(find.bySemanticsLabel('Открыть меню разделов'));
    await tester.pumpAndSettle();
    expect(find.text('Задачи'), findsOneWidget);
    expect(find.text('Финансы'), findsOneWidget);
    await tester.tap(find.bySemanticsLabel('Закрыть меню'));
    await tester.pumpAndSettle();
    expect(find.text('Финансы'), findsNothing);

    await tester.tap(nav('Профиль'));
    await tester.pumpAndSettle();
    expect(find.text('Оформление'), findsOneWidget);
    expect(find.text('Иван Петров'), findsOneWidget);
    await tester.scrollUntilVisible(find.text('onebase.test'), 200, scrollable: find.byType(Scrollable).hitTestable().first);
    expect(find.text('onebase.test'), findsOneWidget);
  });

  testWidgets('тема из настроек и переключение на тёмную сохраняется', (tester) async {
    await pumpApp(tester, settings: {SettingsStore.localeKey: 'ru', SettingsStore.themeKey: 'light'});
    BuildContext ctx() => tester.element(find.byType(ObNavScaffold));
    expect(Theme.of(ctx()).extension<ObColors>()!.page, ObColors.light.page);

    await tester.tap(nav('Профиль'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Тёмная'));
    await tester.pumpAndSettle();
    expect(Theme.of(ctx()).brightness, Brightness.dark);
    // Тёмная — тема по умолчанию (как у веба): в настройках не хранится.
    expect(await SettingsStore(db).readAll(), isNot(contains(SettingsStore.themeKey)));
  });

  testWidgets('язык: переключение на узбекский меняет подписи', (tester) async {
    await pumpApp(tester, settings: {SettingsStore.localeKey: 'ru'});
    await tester.tap(nav('Профиль'));
    await tester.pumpAndSettle();
    await tester.tap(find.text("O'zbekcha"));
    await tester.pumpAndSettle();
    expect(nav('Bosh sahifa'), findsOneWidget);
    expect(find.text('Ko\'rinish'), findsOneWidget);
  });

  testWidgets('без адреса сервера — понятный экран, а не падение', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: baseOverrides(
          db: db,
          settings: {SettingsStore.localeKey: 'ru'},
          config: const AppConfig(env: AppEnv.production, apiBaseUrl: '', appVersion: '0.1.0'),
        ),
        child: const OneBaseApp(),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Адрес сервера не задан при сборке приложения.'), findsOneWidget);
  });
}
