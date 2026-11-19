import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:onebase_mobile/main.dart' as app;

import 'helpers.dart';

/// Главная в дизайне веба на настоящем API (dev, только чтение): снимки экрана сверху вниз и веер разделов.
void main() {
  final binding = IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('Главная и веер', (tester) async {
    await app.main();
    await signInIfNeeded(tester, find.text('Динамика'));
    await settle(tester, const Duration(seconds: 6));
    await binding.takeScreenshot('home_0');
    for (var i = 1; i <= 6; i++) {
      await scrollBy(tester, 560);
      await binding.takeScreenshot('home_$i');
    }
    await tester.tap(find.bySemanticsLabel('Открыть меню разделов'));
    await settle(tester, const Duration(milliseconds: 1500));
    await binding.takeScreenshot('home_fan');
    await tester.tap(find.bySemanticsLabel('Закрыть меню'));
    await settle(tester, const Duration(milliseconds: 1200));
  });
}
