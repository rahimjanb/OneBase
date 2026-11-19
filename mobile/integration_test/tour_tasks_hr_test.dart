import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:onebase_mobile/main.dart' as app;

import 'helpers.dart';

/// Задачи и HR на настоящем API (dev, только просмотр: задача не создаётся, форма только открывается).
void main() {
  final binding = IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('Задачи и HR', (tester) async {
    await app.main();
    await signInIfNeeded(tester, find.text('Динамика'));
    await goTo(tester, '/tasks', wait: const Duration(seconds: 5));
    await binding.takeScreenshot('tasks_0');
    await goTo(tester, '/tasks/new', wait: const Duration(seconds: 4));
    await binding.takeScreenshot('tasks_new');
    await goTo(tester, '/home/hr', wait: const Duration(seconds: 6));
    await binding.takeScreenshot('hr_0');
    await scrollBy(tester, 650);
    await binding.takeScreenshot('hr_1');
    await goTo(tester, '/home/hr?tab=employees', wait: const Duration(seconds: 6));
    await binding.takeScreenshot('hr_emp');
    expect(find.byType(Scaffold), findsWidgets);
  });
}
