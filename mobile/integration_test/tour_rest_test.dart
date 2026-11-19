import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:onebase_mobile/main.dart' as app;

import 'helpers.dart';

/// Отделы, AI-находки, поиск, уведомления на настоящем API (dev, только просмотр: «Проверить сейчас» и отметки не нажимаются).
void main() {
  final binding = IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('Отделы, находки, поиск, уведомления', (tester) async {
    await app.main();
    await signInIfNeeded(tester, find.text('Динамика'));
    await goTo(tester, '/home/departments', wait: const Duration(seconds: 6));
    await binding.takeScreenshot('dept_0');
    await scrollBy(tester, 600);
    await binding.takeScreenshot('dept_1');
    await goTo(tester, '/home/insights', wait: const Duration(seconds: 5));
    await binding.takeScreenshot('insights_0');
    await scrollBy(tester, 700);
    await binding.takeScreenshot('insights_1');
    await goTo(tester, '/home/search', wait: const Duration(seconds: 2));
    await tester.enterText(find.byType(TextField).first, 'про');
    await settle(tester, const Duration(seconds: 3));
    await binding.takeScreenshot('search_0');
    await tester.testTextInput.receiveAction(TextInputAction.done);
    await goTo(tester, '/home/notifications', wait: const Duration(seconds: 5));
    await binding.takeScreenshot('notifications_0');
    await goTo(tester, '/home/settings', wait: const Duration(seconds: 3));
    await binding.takeScreenshot('settings_redirect');
    expect(find.byType(Scaffold), findsWidgets);
  });
}
