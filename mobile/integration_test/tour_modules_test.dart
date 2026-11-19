import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:onebase_mobile/main.dart' as app;

import 'helpers.dart';

/// Финансы, производство, склад и заявки на настоящем API (dev, только просмотр: операции и действия не нажимаются).
void main() {
  final binding = IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  Future<void> shots(WidgetTester tester, String name, int count) async {
    await binding.takeScreenshot('${name}_0');
    for (var i = 1; i < count; i++) {
      await scrollBy(tester, 650);
      await binding.takeScreenshot('${name}_$i');
    }
  }

  testWidgets('Финансы, производство, склад, заявки', (tester) async {
    await app.main();
    await signInIfNeeded(tester, find.text('Динамика'));
    await goTo(tester, '/home/finance', wait: const Duration(seconds: 6));
    await shots(tester, 'fin', 1);
    await goTo(tester, '/home/production', wait: const Duration(seconds: 6));
    await shots(tester, 'prod', 2);
    await goTo(tester, '/home/production/orders/01a1c559-2b9b-773b-86c7-37b6559dd02b', wait: const Duration(seconds: 6));
    await shots(tester, 'order', 2);
    await goTo(tester, '/home/warehouse', wait: const Duration(seconds: 6));
    await shots(tester, 'wh', 1);
    await goTo(tester, '/home/warehouse/items/01a1a693-f994-7808-9763-19321c7175fd', wait: const Duration(seconds: 6));
    await shots(tester, 'item', 1);
    await goTo(tester, '/home/requests', wait: const Duration(seconds: 6));
    await shots(tester, 'req', 1);
    expect(find.byType(Scaffold), findsWidgets);
  });
}
