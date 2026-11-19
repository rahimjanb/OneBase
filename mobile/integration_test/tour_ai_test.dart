import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:onebase_mobile/main.dart' as app;

import 'helpers.dart';

/// Консультант на настоящем API (dev, только просмотр): стартовый экран, вопрос из `?ask=` в поле (не отправляется), история,
/// сохранённый чат. Вопрос модели не задаётся — вызов платный.
void main() {
  final binding = IntegrationTestWidgetsFlutterBinding.ensureInitialized();
  const chat = String.fromEnvironment('E2E_CHAT');

  testWidgets('Консультант', (tester) async {
    await app.main();
    await signInIfNeeded(tester, find.text('Динамика'));
    await goTo(tester, '/ai', wait: const Duration(seconds: 4));
    await binding.takeScreenshot('ai_start');
    await goTo(tester, '/ai?ask=${Uri.encodeQueryComponent('Почему упали продажи в Самарканде?')}', wait: const Duration(seconds: 3));
    await binding.takeScreenshot('ai_ask');
    await tester.tap(find.bySemanticsLabel('История').first);
    await tester.pump(const Duration(seconds: 3));
    await binding.takeScreenshot('ai_history');
    await tester.tapAt(const Offset(200, 60));
    await tester.pump(const Duration(seconds: 1));
    if (chat.isNotEmpty) {
      await goTo(tester, '/ai/c/$chat', wait: const Duration(seconds: 5));
      await binding.takeScreenshot('ai_chat_0');
      await scrollBy(tester, -900);
      await binding.takeScreenshot('ai_chat_1');
    }
    expect(find.byType(Scaffold), findsWidgets);
  });
}
