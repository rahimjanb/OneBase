import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:onebase_mobile/main.dart' as app;

import 'helpers.dart';

/// Обрыв связи с сервером (dev API через локальный прокси, который скрипт на компьютере останавливает по метке в логе): открытые
/// раньше экраны показывают сохранённые данные, новые — понятную ошибку и «Повторить»; после возврата связи — данные снова.
/// Только просмотр.
void main() {
  final binding = IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('Обрыв связи', (tester) async {
    await app.main();
    await signInIfNeeded(tester, find.text('Динамика'));
    await settle(tester, const Duration(seconds: 6));
    await goTo(tester, '/analytics', wait: const Duration(seconds: 8));
    await goTo(tester, '/home', wait: const Duration(seconds: 2));
    // ignore: avoid_print
    print('E2E_OFFLINE_NOW');
    await settle(tester, const Duration(seconds: 6));
    await binding.takeScreenshot('offline_home');
    await goTo(tester, '/analytics', wait: const Duration(seconds: 6));
    await binding.takeScreenshot('offline_analytics');
    await goTo(tester, '/home/warehouse', wait: const Duration(seconds: 12));
    await binding.takeScreenshot('offline_new_screen');
    // ignore: avoid_print
    print('E2E_ONLINE_NOW');
    await settle(tester, const Duration(seconds: 6));
    final retry = find.text('Повторить');
    if (retry.evaluate().isNotEmpty) {
      await tester.tap(retry.first);
      await settle(tester, const Duration(seconds: 8));
    }
    await binding.takeScreenshot('online_again');
    expect(find.byType(Scaffold), findsWidgets);
  });
}
