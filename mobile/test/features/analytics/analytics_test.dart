import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:onebase_mobile/core/api/api_client.dart';
import 'package:onebase_mobile/core/api/api_exception.dart';
import 'package:onebase_mobile/core/api/api_providers.dart';
import 'package:onebase_mobile/features/analytics/data/analytik_models.dart';
import 'package:onebase_mobile/features/analytics/data/sales_models.dart';
import 'package:onebase_mobile/features/analytics/presentation/analytics_providers.dart';
import 'package:onebase_mobile/features/analytics/presentation/analytics_screen.dart';
import 'package:onebase_mobile/features/sales/data/crm_api.dart';
import 'package:onebase_mobile/features/sales/presentation/clients_screens.dart';

import '../../helpers/dashboard_fixtures.dart';
import '../../helpers/fake_adapter.dart';
import '../../helpers/fake_server.dart';

Map<String, Object?> unit(String id, {String kind = 'region'}) => {
      'id': id, 'name': 'Регион $id', 'factKg': 100.0, 'revenue': 1e6, 'akb': 5, 'agents': 2, 'regionCount': 1, //
      'flags': {'critical': 1, 'risk': 0}, 'kind': kind, 'isChannel': false,
    };

void main() {
  group('срезы тяжёлых ответов продаж', () {
    test('республика: артикулы и календарь визитов отбрасываются, дни — из календаря', () {
      final json = {
        ...salesRepublicJson(),
        'name': 'Республика',
        'subtitle': '22 региона',
        'kpi': salesOverviewJson()['kpi'],
        'cards': [unit('d1', kind: 'direction')],
        'regions': [unit('r1'), unit('r2')],
        'categoryCards': [
          {'id': '-1', 'name': 'Бамбук', 'skuSold': 8, 'skuTotal': 10, 'factKg': 44.6, 'revenue': 1.2e9, 'akb': 39, 'silent': 2, 'lost': 1, 'skus': [{'productId': 1}]},
        ],
        'visitCalendar': [for (var i = 0; i < 100; i++) {'id': '$i'}],
      };
      final g = groupViewOf(json);
      expect(g.name, 'Республика');
      expect(g.cards.single.kind, 'direction');
      expect(g.regions, hasLength(2));
      expect(g.regions.first.flags.critical, 1);
      expect(g.categoryCards.single.lost, 1);
      expect(g.days, hasLength(30));
      expect(g.days.first, 5100);
      expect(g.toJson().toString(), isNot(contains('skus')));
      // Кэш: модель переживает запись и чтение.
      expect(GroupView.fromJson(g.toJson()), g);
    });

    test('карточка ТП: точки месяца — из ассортимента, остальное ассортимента не хранится', () {
      final a = agentViewOf({
        'period': salesPeriodJson(),
        'agentId': 476,
        'name': '476 Хиндол',
        'factKg': 17042.9,
        'revenue': 5.7e8,
        'conversion': {'value': 0.945, 'regionMedian': 0.965},
        'flags': [
          {'kind': 'TempoDrop', 'severity': 'Critical', 'label': 'Темп', 'title': 'Падение темпа', 'explanation': '64%'},
        ],
        'assortment': {
          'stores': [{'marketId': 29519, 'name': 'Nodir Optom', 'kg': 1773.6, 'revenue': 4.8e7, 'categories': 2, 'positions': 12, 'share': 0.085}],
          'products': [{'productId': 1}],
        },
        'silent': [{'marketId': 1, 'name': 'Sherbek', 'prevKg': 143.7, 'prevRevenue': 4.9e6, 'sku': 15}],
      });
      expect(a.stores.single.marketId, 29519);
      expect(a.silent.single.prevKg, 143.7);
      expect(a.flags.single.severity, 'Critical');
      expect(a.conversion.regionMedian, 0.965);
    });
  });

  test('сравнение периодов: персонал и задачи — одним списком; рост опозданий — плохо', () {
    final b = analytikOf({
      'period': {'kind': 'month', 'from': '2026-11-01', 'to': '2026-11-20', 'label': '1–20 ноября 2026'},
      'hr': {
        'period': {'kind': 'month', 'from': '2026-11-01', 'to': '2026-11-20', 'label': '1–20 ноября 2026'},
        'kpis': [{'code': 'hr.late', 'title': 'Опоздания', 'value': 1499, 'unit': 'случ.', 'previous': 1602, 'changePercent': -6.4}],
        'notes': ['n1'],
      },
      'work': {
        'period': {'kind': 'month', 'from': '2026-11-01', 'to': '2026-11-20'},
        'kpis': [{'code': 'work.overdue', 'title': 'Просрочено', 'value': null, 'unit': 'шт'}],
      },
    });
    expect(b.kpis.map((k) => k.code), ['hr.late', 'work.overdue']);
    expect(b.kpis.last.value, isNull);
    expect(b.notes, ['n1']);
    expect(kpiGoodWhenDown('hr.late'), isTrue);
    expect(kpiGoodWhenDown('sales.revenue'), isFalse);
  });

  test('домашняя страница продаж РМ/СВ/ТП — в экраны приложения', () {
    expect(salesHomeRoute('/sales/regions/r-1'), '/analytics/sales/regions/r-1');
    expect(salesHomeRoute('/sales/directions/d-1'), '/analytics/sales/directions/d-1');
    expect(salesHomeRoute('/sales/agents/476'), '/analytics/sales/agents/476');
    expect(salesHomeRoute('/sales/my'), '/analytics/sales/my');
    expect(salesHomeRoute('/sales'), '/analytics/sales');
    expect(salesHomeRoute(null), '/analytics/sales');
  });

  group('клиенты (Sales Base) постранично', () {
    late FakeServer server;
    late ProviderContainer c;

    setUp(() {
      server = FakeServer();
      final dio = Dio(BaseOptions(baseUrl: 'http://onebase.test'))..httpClientAdapter = server.adapter;
      c = ProviderContainer.test(overrides: [apiClientProvider.overrideWithValue(ApiClient(dio, parseInBackground: false))]);
    });

    Map<String, Object?> page(int from, int count, int total) => {
          'items': [for (var i = from; i < from + count; i++) {'marketId': i, 'name': 'Точка $i', 'status': 'Active', 'priority': 'Medium', 'sales30': 1.0}],
          'total': total,
          'page': from ~/ 50 + 1,
          'pageSize': 50,
        };

    test('первая страница, подгрузка, смена фильтра — с начала и с параметрами', () async {
      server.on('GET', '/api/field/customers', (o) {
        final p = o.queryParameters['page'] as int;
        return FakeAdapter.json(page((p - 1) * 50, p == 1 ? 50 : 10, 60));
      });
      c.listen(customersProvider, (_, _) {});
      await pumpEventQueue();
      expect(c.read(customersProvider).items, hasLength(50));
      expect(c.read(customersProvider).hasMore, isTrue);
      await c.read(customersProvider.notifier).loadMore();
      expect(c.read(customersProvider).items, hasLength(60));
      expect(c.read(customersProvider).hasMore, isFalse);

      c.read(customerQueryProvider.notifier).set(const CustomerQuery(search: 'бозор', attention: 'decline'));
      await pumpEventQueue();
      expect(c.read(customersProvider).items, hasLength(50));
      final last = server.requests.last.queryParameters;
      expect(last['search'], 'бозор');
      expect(last['attention'], 'decline');
      expect(last['page'], 1);
    });

    test('нет карточки Sales Base — 403, список не падает', () async {
      server.json('GET', '/api/field/customers', {'error': 'Вас ещё не добавили в Sales Base'}, status: 403);
      c.listen(customersProvider, (_, _) {});
      await pumpEventQueue();
      expect(c.read(customersProvider).failure?.kind, ApiErrorKind.forbidden);
    });
  });

  test('модель сравнения: value = null — «нет данных», а не ноль', () {
    final k = AnalytikKpi.fromJson({'code': 'sales.kg', 'title': 'Продажи', 'unit': 'кг'});
    expect(k.value, isNull);
    expect(k.kind, 'fact');
  });
}
