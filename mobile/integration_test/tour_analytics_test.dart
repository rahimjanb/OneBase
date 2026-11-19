import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:onebase_mobile/main.dart' as app;

import 'helpers.dart';

/// Аналитика и клиенты на настоящем API (dev, только чтение; правка точки не сохраняется).
void main() {
  final binding = IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  Future<void> shots(WidgetTester tester, String name, int count) async {
    await binding.takeScreenshot('${name}_0');
    for (var i = 1; i < count; i++) {
      await scrollBy(tester, 650);
      await binding.takeScreenshot('${name}_$i');
    }
  }

  testWidgets('Аналитика, продажи вглубь, клиенты', (tester) async {
    await app.main();
    await signInIfNeeded(tester, find.text('Динамика'));
    await goTo(tester, '/analytics', wait: const Duration(seconds: 8));
    await shots(tester, 'an_hub', 4);
    await goTo(tester, '/analytics/sales', wait: const Duration(seconds: 8));
    await shots(tester, 'an_rep', 4);
    await goTo(tester, '/analytics/sales/regions/01a1899b-e675-749c-b2c4-1132e3883e08', wait: const Duration(seconds: 8));
    await shots(tester, 'an_region', 3);
    await goTo(tester, '/analytics/sales/agents', wait: const Duration(seconds: 8));
    await shots(tester, 'an_agents', 2);
    await goTo(tester, '/analytics/sales/agents/476', wait: const Duration(seconds: 8));
    await shots(tester, 'an_agent', 3);
    await goTo(tester, '/home/sales', wait: const Duration(seconds: 8));
    await shots(tester, 'crm_list', 1);
    await goTo(tester, '/home/sales/points/29519', wait: const Duration(seconds: 8));
    await shots(tester, 'crm_point', 3);
    expect(find.byType(Scaffold), findsWidgets);
  });
}
