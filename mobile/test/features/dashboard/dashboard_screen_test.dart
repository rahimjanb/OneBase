import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:onebase_mobile/app/app.dart';
import 'package:onebase_mobile/core/auth/token_storage.dart';
import 'package:onebase_mobile/core/database/app_database.dart';
import 'package:onebase_mobile/core/storage/settings_store.dart';

import '../../helpers/dashboard_fixtures.dart';
import '../../helpers/fake_server.dart';
import '../../helpers/harness.dart';
import '../../helpers/test_db.dart';

void main() {
  late AppDatabase db;
  late FakeServer server;

  setUp(() {
    db = memoryDb();
    server = FakeServer();
  });
  tearDown(() => db.close());

  Future<void> pumpApp(WidgetTester tester) async {
    tester.view.physicalSize = const Size(1179, 2556);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(
      ProviderScope(
        retry: (_, _) => null,
        overrides: baseOverrides(
          db: db,
          adapter: server.adapter,
          tokens: MemoryTokenStorage(StoredToken(fakeJwt('u-1'), DateTime(2036))),
          settings: {SettingsStore.localeKey: 'ru'},
          clock: () => DateTime(2026, 11, 19, 10, 0),
        ),
        child: const OneBaseApp(),
      ),
    );
    await tester.pumpAndSettle();
  }

  Future<void> scrollTo(WidgetTester tester, Finder f) async {
    await tester.scrollUntilVisible(f, 300, scrollable: find.byType(Scrollable).first);
    await tester.pumpAndSettle();
  }

  String plain(String s) => s.replaceAll(' ', ' ');

  Finder textPlain(String s) => find.byWidgetPredicate((w) => w is Text && w.data != null && plain(w.data!) == s);

  testWidgets('все блоки на данных сервера; каждый — своим запросом', (tester) async {
    serveDashboard(server);
    await pumpApp(tester);

    expect(find.text('Продажи · Ноябрь'), findsOneWidget);
    expect(textPlain('2,5 млрд сум'), findsOneWidget);
    expect(textPlain('85 т'), findsOneWidget);
    expect(textPlain('4 321'), findsOneWidget);
    expect(find.text('81%'), findsOneWidget);
    expect(find.textContaining('+5%'), findsWidgets);

    await scrollTo(tester, find.text('Фокус дня · AI'));
    expect(find.text('Продажи в регионе упали'), findsOneWidget);

    await scrollTo(tester, find.text('Финансы · 1С'));
    expect(find.text('Чистая выручка'), findsWidgets);

    await scrollTo(tester, find.text('Явка сегодня'));
    expect(find.text('87%'), findsOneWidget);

    await scrollTo(tester, find.text('Сверить остатки'));
    await scrollTo(tester, find.text('Событие 0'));

    for (final path in [
      '/api/sales/overview',
      '/api/sales/republic',
      '/api/finance/overview',
      '/api/hr/overview',
      '/api/work/overview',
      '/api/notifications',
      '/api/ai/alerts',
    ]) {
      expect(server.count('GET', path), 1, reason: path);
    }
  });

  testWidgets('упал один блок — остальные работают; «Повторить» возвращает блок', (tester) async {
    serveDashboard(server);
    server.json('GET', '/api/sales/overview', {'error': 'down'}, status: 500);
    await pumpApp(tester);

    expect(find.text('Ошибка на сервере. Попробуйте позже.'), findsOneWidget);
    expect(find.text('Динамика продаж'), findsOneWidget);
    await scrollTo(tester, find.text('Явка сегодня'));
    expect(find.text('87%'), findsOneWidget);

    server.json('GET', '/api/sales/overview', salesOverviewJson());
    await tester.drag(find.byType(Scrollable).first, const Offset(0, 4000));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Повторить').first);
    await tester.pumpAndSettle();
    expect(textPlain('2,5 млрд сум'), findsOneWidget);
  });

  testWidgets('нет данных 1С — пояснение сервера, а не нули', (tester) async {
    serveDashboard(server);
    server.json('GET', '/api/finance/overview', financeJson(hasData: false));
    await pumpApp(tester);
    await scrollTo(tester, find.text('Данных 1С пока нет'));
    expect(find.text('Данных 1С в OneBase пока нет: подключите 1С в настройках.'), findsOneWidget);
  });

  testWidgets('без прав — блоки не запрашиваются, на главной только события', (tester) async {
    serveDashboard(server, permissions: const []);
    await pumpApp(tester);
    expect(find.text('Продажи · Ноябрь'), findsNothing);
    expect(find.textContaining('только события'), findsOneWidget);
    expect(find.text('Событие 0'), findsOneWidget);
    expect(server.count('GET', '/api/sales/overview'), 0);
    expect(server.count('GET', '/api/finance/overview'), 0);
    expect(server.count('GET', '/api/ai/alerts'), 0);
  });

  testWidgets('без сети — последние данные из кэша и причина', (tester) async {
    serveDashboard(server);
    await pumpApp(tester);
    expect(textPlain('2,5 млрд сум'), findsOneWidget);

    // Новый запуск: сервер недоступен, кэш на телефоне есть.
    await tester.pumpWidget(const SizedBox());
    for (final route in server.routes.keys.toList()) {
      if (route != 'GET /api/auth/me') server.offline(route.split(' ').first, route.split(' ').last);
    }
    await pumpApp(tester);
    expect(textPlain('2,5 млрд сум'), findsOneWidget);
    expect(find.textContaining('Нет соединения с сервером'), findsWidgets);
  });

  testWidgets('смена периода — запрос за выбранный месяц', (tester) async {
    serveDashboard(server);
    await pumpApp(tester);
    server.json('GET', '/api/sales/overview', salesOverviewJson(month: 10, revenue: 3100000000, execution: null));
    await tester.tap(find.text('Последние данные'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Октябрь 2026'));
    await tester.pumpAndSettle();
    final req = server.requests.lastWhere((r) => r.path == '/api/sales/overview');
    expect(req.queryParameters, {'year': 2026, 'month': 10});
    final fin = server.requests.lastWhere((r) => r.path == '/api/finance/overview');
    expect(fin.queryParameters, {'from': '2026-10-01', 'to': '2026-10-31'});
    expect(find.text('Продажи · Октябрь'), findsOneWidget);
    expect(textPlain('3,1 млрд сум'), findsOneWidget);
    expect(find.text('План не задан'), findsOneWidget);
  });

  testWidgets('детализация продаж открывается по плитке', (tester) async {
    serveDashboard(server);
    await pumpApp(tester);
    await tester.tap(find.text('Выручка'));
    await tester.pumpAndSettle();
    expect(find.text('Продажи · Ноябрь 2026'), findsOneWidget);
    expect(find.text('Выручка на точку'), findsOneWidget);
    await tester.scrollUntilVisible(find.text('Сигналы по ТП'), 300, scrollable: find.byType(Scrollable).last);
    expect(find.text('Сигналы по ТП'), findsOneWidget);
  });

  testWidgets('pull-to-refresh обновляет блоки', (tester) async {
    serveDashboard(server);
    await pumpApp(tester);
    await tester.fling(find.byType(Scrollable).first, const Offset(0, 500), 1500);
    await tester.pumpAndSettle();
    expect(server.count('GET', '/api/sales/overview'), 2);
    expect(server.count('GET', '/api/notifications'), 2);
  });
}
