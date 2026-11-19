import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:onebase_mobile/main.dart' as app;

import 'helpers.dart';

/// Профиль и светлая тема на настоящем API (dev, только чтение: переключатели «Что присылать» и Telegram не нажимаются).
void main() {
  final binding = IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('Профиль и светлая тема', (tester) async {
    await app.main();
    await signInIfNeeded(tester, find.text('Динамика'));
    await settle(tester, const Duration(seconds: 3));
    await tester.tap(find.text('Профиль').last);
    await settle(tester, const Duration(seconds: 4));
    await binding.takeScreenshot('profile_0');
    for (var i = 1; i <= 4; i++) {
      await scrollBy(tester, 600);
      await binding.takeScreenshot('profile_$i');
    }
    await tester.drag(find.byType(Scrollable).first, const Offset(0, 5000));
    await settle(tester);
    await tester.tap(find.text('Светлая'));
    await settle(tester, const Duration(seconds: 1));
    await binding.takeScreenshot('profile_light');
    await tester.tap(find.text('Главная').last);
    await settle(tester, const Duration(seconds: 2));
    await binding.takeScreenshot('home_light_0');
    await scrollBy(tester, 700);
    await binding.takeScreenshot('home_light_1');
    await tester.tap(find.bySemanticsLabel('Открыть меню разделов'));
    await settle(tester, const Duration(milliseconds: 1500));
    await binding.takeScreenshot('home_light_fan');
    await tester.tap(find.bySemanticsLabel('Закрыть меню'));
    await settle(tester);
    // Вернуть тёмную (по умолчанию).
    await tester.tap(find.text('Профиль').last);
    await settle(tester, const Duration(seconds: 1));
    await tester.drag(find.byType(Scrollable).first, const Offset(0, 5000));
    await settle(tester);
    await tester.tap(find.text('Тёмная'));
    await settle(tester);
  });
}
