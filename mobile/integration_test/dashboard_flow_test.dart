import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:onebase_mobile/main.dart' as app;

import 'helpers.dart';

/// Dashboard на настоящем OneBase API (локальный dev API, только чтение): блоки с реальными данными, детализация, период, тёмная
/// тема. Если сеанса нет — вход через `--dart-define=E2E_LOGIN/E2E_PASSWORD`.
void main() {
  final binding = IntegrationTestWidgetsFlutterBinding.ensureInitialized();
  const login = String.fromEnvironment('E2E_LOGIN');
  const password = String.fromEnvironment('E2E_PASSWORD');

  Future<void> scrollTo(WidgetTester tester, Finder f) async {
    await tester.scrollUntilVisible(f, 300, scrollable: find.byType(Scrollable).first);
    await tester.ensureVisible(f);
    await settle(tester, const Duration(milliseconds: 600));
  }

  testWidgets('Dashboard на реальных данных', (tester) async {
    await app.main();
    final screen = await pumpUntilAny(tester, [find.byType(NavigationBar), find.text('Войти')]);
    if (screen == 1) {
      await tester.enterText(find.byType(TextField).at(0), login);
      await tester.enterText(find.byType(TextField).at(1), password);
      await tester.tap(find.text('Войти'));
      await pumpUntil(tester, find.byType(NavigationBar));
    }

    // Блоки грузятся независимо: ждём плитки продаж и график.
    await pumpUntil(tester, find.text('Выручка'), timeout: const Duration(seconds: 40));
    await pumpUntil(tester, find.text('Динамика продаж'));
    await settle(tester, const Duration(seconds: 3));
    await binding.takeScreenshot('s3_home_top');

    await scrollTo(tester, find.text('Динамика продаж'));
    await settle(tester, const Duration(seconds: 2));
    await binding.takeScreenshot('s3_sales_trend');

    await scrollTo(tester, find.text('Финансы · 1С'));
    await binding.takeScreenshot('s3_finance');

    await scrollTo(tester, find.text('Подразделения'));
    await binding.takeScreenshot('s3_departments');

    await scrollTo(tester, find.text('Последние события'));
    await settle(tester, const Duration(seconds: 1));
    await binding.takeScreenshot('s3_events');

    // Детализация продаж.
    await tester.drag(find.byType(Scrollable).first, const Offset(0, 6000));
    await settle(tester, const Duration(seconds: 1));
    await tester.tap(find.text('Выручка'));
    await settle(tester, const Duration(seconds: 1));
    await binding.takeScreenshot('s3_sales_detail');
    await tester.tapAt(const Offset(200, 60));
    await settle(tester, const Duration(seconds: 1));

    // AI-находки — все.
    await scrollTo(tester, find.text('Фокус дня · AI'));
    await binding.takeScreenshot('s3_ai');

    // Период: прошлый месяц.
    await tester.drag(find.byType(Scrollable).first, const Offset(0, 6000));
    await settle(tester, const Duration(seconds: 1));
    await tester.tap(find.text('Последние данные'));
    await settle(tester, const Duration(seconds: 1));
    await binding.takeScreenshot('s3_period_sheet');
    await tester.tap(find.text('Октябрь 2026'));
    await pumpUntil(tester, find.text('Продажи · Октябрь'), timeout: const Duration(seconds: 40));
    await settle(tester, const Duration(seconds: 3));
    await binding.takeScreenshot('s3_october');

    // Тёмная тема.
    await tester.tap(navItem('Профиль'));
    await settle(tester, const Duration(seconds: 1));
    await tester.tap(find.text('Тёмная'));
    await settle(tester, const Duration(seconds: 1));
    await tester.tap(navItem('Главная'));
    await settle(tester, const Duration(seconds: 2));
    await binding.takeScreenshot('s3_dark_top');
    await scrollTo(tester, find.text('Динамика продаж'));
    await settle(tester, const Duration(seconds: 1));
    await binding.takeScreenshot('s3_dark_trend');

    // Вернуть тему как в системе.
    await tester.tap(navItem('Профиль'));
    await settle(tester, const Duration(seconds: 1));
    await tester.tap(find.text('Как в системе'));
    await settle(tester, const Duration(seconds: 1));
  });
}
