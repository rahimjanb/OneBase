import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:onebase_mobile/main.dart' as app;

import 'helpers.dart';

/// Вход и выход на настоящем OneBase API (локальный dev API): свежая установка (старый токен из Keychain не подхватывается) →
/// экран входа → вход → главная → профиль → выход (сеанс закрывается на сервере). Учётные данные — только через
/// `--dart-define=E2E_LOGIN=… --dart-define=E2E_PASSWORD=…`, в коде их нет. Данных компании не меняет.
void main() {
  final binding = IntegrationTestWidgetsFlutterBinding.ensureInitialized();
  const login = String.fromEnvironment('E2E_LOGIN');
  const password = String.fromEnvironment('E2E_PASSWORD');

  testWidgets('вход под существующей учётной записью и профиль', (tester) async {
    expect(login, isNotEmpty, reason: 'Нужен --dart-define=E2E_LOGIN');
    await app.main();
    final screen = await pumpUntilAny(tester, [find.text('Войти'), find.byType(NavigationBar)]);
    expect(screen, 0, reason: 'После установки приложения вход из Keychain прежней установки не должен подхватываться');
    await settle(tester);
    await binding.takeScreenshot('s2_login');

    await tester.enterText(find.byType(TextField).at(0), login);
    await tester.enterText(find.byType(TextField).at(1), password);
    await tester.tap(find.text('Войти'));
    await pumpUntil(tester, find.byType(NavigationBar));
    await settle(tester, const Duration(seconds: 2));
    await binding.takeScreenshot('s2_home');

    await tester.tap(navItem('Профиль'));
    await settle(tester, const Duration(seconds: 1));
    await binding.takeScreenshot('s2_profile');

    final logout = find.widgetWithText(OutlinedButton, 'Выйти');
    await tester.scrollUntilVisible(logout, 300, scrollable: find.byType(Scrollable).first);
    await tester.ensureVisible(logout);
    await settle(tester);
    await tester.tap(logout);
    await settle(tester);
    await binding.takeScreenshot('s2_logout_confirm');
    await tester.tap(find.widgetWithText(TextButton, 'Выйти').last);
    await pumpUntil(tester, find.text('Вход в OneBase'));
  });
}
