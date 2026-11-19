import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:onebase_mobile/app/theme/theme.dart';
import 'package:onebase_mobile/core/auth/token_storage.dart';
import 'package:onebase_mobile/features/production/data/production_api.dart';
import 'package:onebase_mobile/features/production/presentation/production_screens.dart';
import 'package:onebase_mobile/features/warehouse/presentation/warehouse_screens.dart';
import 'package:onebase_mobile/l10n/gen/app_localizations.dart';

import '../../helpers/fake_adapter.dart';
import '../../helpers/fake_server.dart';
import '../../helpers/harness.dart';
import '../../helpers/test_db.dart';

void main() {
  test('ключ формы — UUID, у каждой формы свой', () {
    final a = newClientKey();
    expect(RegExp(r'^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$').hasMatch(a), isTrue);
    expect(newClientKey(), isNot(a));
  });

  test('ссылки проблем технолога → карточка задания', () {
    expect(productionRoute('/production/orders/abc'), '/home/production/orders/abc');
    expect(productionRoute('/production/shifts/x'), '/home/production');
  });

  testWidgets('складская операция: подтверждение, обрыв связи, повтор с тем же ключом — один документ', (tester) async {
    tester.view.physicalSize = const Size(1179, 2556);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    final db = memoryDb();
    addTearDown(db.close);
    var attempts = 0;
    final keys = <String>[];
    final server = FakeServer()
      ..json('GET', '/api/procurement/warehouses', {
        'warehouses': [{'id': 'w1', 'name': 'Центральный склад', 'isActive': true, 'isDefault': true, 'posts': true}],
        'departments': [{'id': 'd1', 'code': 'production', 'name': 'Производство'}],
      })
      ..json('GET', '/api/procurement/catalog', [{'id': 'i1', 'sku': '101-165', 'name': 'Сахар Импорт', 'unitCode': 'кг', 'available': 25400}])
      ..on('POST', '/api/procurement/stock/write-off', (o) {
        attempts++;
        final body = o.data is String ? jsonDecode(o.data as String) as Map<String, Object?> : o.data as Map<String, Object?>;
        keys.add(body['clientKey']! as String);
        if (attempts == 1) throw DioException.connectionError(requestOptions: o, reason: 'обрыв');
        return FakeAdapter.json({'id': 'doc-1'});
      });
    await tester.pumpWidget(ProviderScope(
      retry: (_, _) => null,
      overrides: baseOverrides(db: db, adapter: server.adapter, tokens: MemoryTokenStorage()),
      child: MaterialApp(
        theme: ObTheme.dark(),
        locale: const Locale('ru'),
        supportedLocales: AppLocalizations.supportedLocales,
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        home: const Scaffold(body: SingleChildScrollView(child: StockOperationForm())),
      ),
    ));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Списание'));
    await tester.pumpAndSettle();
    // Без позиции и причины — подсказка, запроса нет.
    await tester.tap(find.text('Провести'));
    await tester.pumpAndSettle();
    expect(find.text('Выберите позицию'), findsWidgets);
    expect(attempts, 0);

    await tester.tap(find.text('Выберите позицию').first);
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField).last, 'сахар');
    await tester.pump(const Duration(milliseconds: 400));
    await tester.pumpAndSettle();
    await tester.tap(find.text('101-165 · Сахар Импорт'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField).at(0), '12,5');
    await tester.enterText(find.byType(TextField).at(1), 'Испорчено');
    await tester.tap(find.text('Провести'));
    await tester.pumpAndSettle();
    expect(find.textContaining('Сахар Импорт — 12,5 кг'), findsOneWidget);
    await tester.tap(find.widgetWithText(FilledButton, 'Провести').last);
    await tester.pumpAndSettle();
    expect(attempts, 1);
    expect(find.textContaining('Нет соединения'), findsOneWidget);

    // Повтор той же формы — тот же ключ.
    await tester.tap(find.text('Провести'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Провести').last);
    await tester.pumpAndSettle();
    expect(attempts, 2);
    expect(keys.toSet(), hasLength(1));
  });
}
