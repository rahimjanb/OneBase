import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:onebase_mobile/main.dart' as app;

import 'helpers.dart';

/// Память: 6 кругов по тяжёлым экранам (главная, аналитика и республика, склад, уведомления, отделы, находки, HR, сохранённый чат).
/// Скрипт на компьютере пишет RSS процесса раз в секунду и по меткам кругов; рост от круга к кругу — признак утечки. Только просмотр.
void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();
  const chat = String.fromEnvironment('E2E_CHAT');

  testWidgets('Память', (tester) async {
    await app.main();
    await signInIfNeeded(tester, find.text('Динамика'));
    const screens = [
      '/home',
      '/analytics',
      '/analytics/sales',
      '/home/warehouse',
      '/home/notifications',
      '/home/departments',
      '/home/insights',
      '/home/hr',
      if (chat != '') '/ai/c/$chat',
    ];
    for (var lap = 1; lap <= 6; lap++) {
      for (final s in screens) {
        await goTo(tester, s, wait: const Duration(seconds: 3));
        await scrollBy(tester, 500);
      }
      await goTo(tester, '/home', wait: const Duration(seconds: 3));
      // ignore: avoid_print
      print('E2E_LAP $lap');
    }
    expect(find.byType(Scaffold), findsWidgets);
  });
}
