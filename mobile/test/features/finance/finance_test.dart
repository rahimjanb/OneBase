import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:onebase_mobile/app/theme/theme.dart';
import 'package:onebase_mobile/core/auth/token_storage.dart';
import 'package:onebase_mobile/features/finance/data/finance_models.dart';
import 'package:onebase_mobile/features/finance/presentation/finance_screens.dart';
import 'package:onebase_mobile/l10n/gen/app_localizations.dart';

import '../../helpers/fake_server.dart';
import '../../helpers/harness.dart';
import '../../helpers/test_db.dart';

Map<String, Object?> meta({bool hasData = true}) => {
      'source': '1С:Бухгалтерия', 'kind': 'fact', 'demo': false, 'hasData': hasData, //
      'note': hasData ? null : 'Данных 1С в OneBase пока нет',
      'from': '2026-11-01', 'to': '2026-11-20', 'dataThrough': hasData ? '2026-11-19' : null,
    };

void main() {
  test('период по Ташкенту: этот месяц, прошлый, квартал, с начала года', () {
    final now = DateTime.utc(2026, 11, 20, 20); // в Ташкенте уже 21-е
    expect(finRange(FinPeriod.mtd, now), (from: '2026-11-01', to: '2026-11-21'));
    expect(finRange(FinPeriod.prev, now), (from: '2026-10-01', to: '2026-10-31'));
    expect(finRange(FinPeriod.quarter, now), (from: '2026-10-01', to: '2026-11-21'));
    expect(finRange(FinPeriod.year, now), (from: '2026-01-01', to: '2026-11-21'));
    expect(finRange(FinPeriod.prev, DateTime.utc(2026, 1, 10)), (from: '2025-12-01', to: '2025-12-31'));
  });

  test('ссылки отклонений (пути веба) → экраны раздела', () {
    expect(financeRoute('/finance/documents/3f2a'), '/home/finance/documents/3f2a');
    expect(financeRoute('/finance/entries?registrar=abc'), '/home/finance/entries?registrar=abc');
    expect(financeRoute('/finance/accounts/9410'), '/home/finance/entries?account=9410');
    expect(financeRoute(null), '/home/finance');
  });

  test('расчёты: корзины просрочки и строки', () {
    final s = FinSettlements.fromJson({
      'meta': meta(),
      'kind': 'receivables',
      'total': 1000,
      'overdue': 300,
      'buckets': {'not_due': 700, '1_30': 200, '31_60': 0, '61_90': 0, 'over_90': 100, 'no_term': 0},
      'rows': [{'counterpartyId': 'c1', 'name': 'ООО Ромашка', 'balance': 600, 'overdue': 100, 'maxOverdueDays': 95, 'buckets': {}, 'termsKnown': true}],
      'method': 'Срок — по договору',
    });
    expect(s.buckets['over_90'], 100);
    expect(s.rows.single.maxOverdueDays, 95);
  });

  group('экран', () {
    late FakeServer server;

    Future<void> pump(WidgetTester tester) async {
      tester.view.physicalSize = const Size(1179, 2556);
      tester.view.devicePixelRatio = 3;
      addTearDown(tester.view.reset);
      final db = memoryDb();
      addTearDown(db.close);
      await tester.pumpWidget(ProviderScope(
        retry: (_, _) => null,
        overrides: baseOverrides(db: db, adapter: server.adapter, tokens: MemoryTokenStorage(), clock: () => DateTime.utc(2026, 11, 20, 9)),
        child: MaterialApp(
          theme: ObTheme.dark(),
          locale: const Locale('ru'),
          supportedLocales: AppLocalizations.supportedLocales,
          localizationsDelegates: AppLocalizations.localizationsDelegates,
          home: const FinanceScreen(),
        ),
      ));
      await tester.pumpAndSettle();
    }

    setUp(() => server = FakeServer()..json('GET', '/api/finance/organizations', <Object?>[]));

    testWidgets('нет данных 1С — пояснение сервера, а не нули', (tester) async {
      server.json('GET', '/api/finance/overview', {'meta': meta(hasData: false), 'pnl': <Object?>[], 'months': <Object?>[]});
      await pump(tester);
      expect(find.text('Данных 1С в OneBase пока нет'), findsOneWidget);
      expect(find.text('Чистая выручка'), findsNothing);
    });

    testWidgets('обзор с данными, период в запросе; вкладка «Долги»', (tester) async {
      server
        ..json('GET', '/api/finance/overview', {
          'meta': meta(),
          'pnl': [
            {'code': 'net_revenue', 'title': 'Чистая выручка', 'kind': 'total', 'value': 2.5e9, 'previous': 2e9},
            {'code': 'gross_profit', 'title': 'Валовая прибыль', 'kind': 'total', 'value': 9e8, 'previous': 1e9},
            {'code': 'net_profit', 'title': 'Чистая прибыль', 'kind': 'total', 'value': 4e8, 'previous': null},
          ],
          'months': [for (var m = 1; m <= 12; m++) {'month': '2026-${m.toString().padLeft(2, '0')}-01', 'revenue': m * 1e8, 'expenses': 1e8, 'netProfit': 1e7}],
          'cashTotal': 7.5e8,
          'receivables': 3e8,
          'payables': 1e8,
        })
        ..json('GET', '/api/finance/settlements', {
          'meta': meta(),
          'kind': 'receivables',
          'total': 1000,
          'overdue': 300,
          'buckets': {'not_due': 700, 'over_90': 300},
          'rows': [{'counterpartyId': 'c1', 'name': 'ООО Ромашка', 'balance': 600, 'overdue': 100, 'maxOverdueDays': 95}],
          'method': 'Срок — по договору',
        });
      await pump(tester);
      expect(find.text('Чистая выручка'), findsWidgets);
      expect(find.text('2,5 млрд'), findsOneWidget);
      expect(find.text('к прошлому периоду +25%'), findsOneWidget);
      expect(find.text('Проводки по 2026-11-19'), findsOneWidget);
      final q = server.requests.firstWhere((r) => r.path == '/api/finance/overview').queryParameters;
      expect((q['from'], q['to']), ('2026-11-01', '2026-11-20'));

      await tester.ensureVisible(find.text('Долги'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Долги'));
      await tester.pumpAndSettle();
      expect(find.text('ООО Ромашка'), findsOneWidget);
      expect(find.text('просрочка до 95 дн.'), findsOneWidget);
      expect(find.text('Больше 90 дней'), findsOneWidget);
    });
  });
}
