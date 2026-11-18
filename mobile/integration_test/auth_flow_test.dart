import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:onebase_mobile/main.dart' as app;

import 'helpers.dart';

/// Вход на настоящем OneBase API (локальный dev API): экран входа → вход → главная → профиль. Учётные данные — только через
/// `--dart-define=E2E_LOGIN=… --dart-define=E2E_PASSWORD=…`, в коде их нет. Только чтение: ничего не сохраняет.
void main() {
  final binding = IntegrationTestWidgetsFlutterBinding.ensureInitialized();
  const login = String.fromEnvironment('E2E_LOGIN');
  const password = String.fromEnvironment('E2E_PASSWORD');

  testWidgets('вход под существующей учётной записью и профиль', (tester) async {
    expect(login, isNotEmpty, reason: 'Нужен --dart-define=E2E_LOGIN');
    await app.main();
    await pumpUntil(tester, find.text('Войти'));
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
  });
}
