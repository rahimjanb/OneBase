import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:onebase_mobile/app/app.dart';
import 'package:onebase_mobile/app/theme/tokens.dart';
import 'package:onebase_mobile/core/config/app_config.dart';
import 'package:onebase_mobile/core/database/app_database.dart';
import 'package:onebase_mobile/core/storage/settings_store.dart';

import '../helpers/harness.dart';
import '../helpers/test_db.dart';

void main() {
  late AppDatabase db;

  setUp(() => db = memoryDb());
  tearDown(() => db.close());

  Future<void> pumpApp(WidgetTester tester, {Map<String, String> settings = const {}}) async {
    await tester.pumpWidget(
      ProviderScope(
        retry: (_, _) => null,
        overrides: baseOverrides(db: db, settings: settings),
        child: const OneBaseApp(),
      ),
    );
    await tester.pumpAndSettle();
  }

  testWidgets('оболочка: пять вкладок, переход по вкладкам', (tester) async {
    await pumpApp(tester, settings: {SettingsStore.localeKey: 'ru'});
    for (final label in ['Главная', 'Аналитика', 'Задачи', 'AI', 'Профиль']) {
      expect(find.descendant(of: find.byType(NavigationBar), matching: find.text(label)), findsOneWidget);
    }
    await tester.tap(find.descendant(of: find.byType(NavigationBar), matching: find.text('Профиль')));
    await tester.pumpAndSettle();
    expect(find.text('Оформление'), findsOneWidget);
    expect(find.text('http://onebase.test'), findsOneWidget);
  });

  testWidgets('тема из настроек и переключение на тёмную сохраняется', (tester) async {
    await pumpApp(tester, settings: {SettingsStore.localeKey: 'ru', SettingsStore.themeKey: 'light'});
    BuildContext ctx() => tester.element(find.byType(NavigationBar));
    expect(Theme.of(ctx()).extension<ObColors>()!.page, ObColors.light.page);

    await tester.tap(find.descendant(of: find.byType(NavigationBar), matching: find.text('Профиль')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Тёмная'));
    await tester.pumpAndSettle();
    expect(Theme.of(ctx()).brightness, Brightness.dark);
    expect(await SettingsStore(db).readAll(), containsPair(SettingsStore.themeKey, 'dark'));
  });

  testWidgets('язык: переключение на узбекский меняет подписи', (tester) async {
    await pumpApp(tester, settings: {SettingsStore.localeKey: 'ru'});
    await tester.tap(find.descendant(of: find.byType(NavigationBar), matching: find.text('Профиль')));
    await tester.pumpAndSettle();
    await tester.tap(find.text("O'zbekcha"));
    await tester.pumpAndSettle();
    expect(find.descendant(of: find.byType(NavigationBar), matching: find.text('Bosh sahifa')), findsOneWidget);
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
