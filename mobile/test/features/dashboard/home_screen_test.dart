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

/// Главная в дизайне мобильного веба на синтетических ответах: блоки, тексты и переходы.
void main() {
  late AppDatabase db;
  late FakeServer server;

  setUp(() {
    db = memoryDb();
    server = FakeServer();
  });
  tearDown(() => db.close());

  Future<void> pumpHome(WidgetTester tester, {bool online = true, List<String>? permissions}) async {
    tester.view.physicalSize = const Size(1179, 2556);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    serveDashboard(server, permissions: permissions);
    await tester.pumpWidget(
      ProviderScope(
        retry: (_, _) => null,
        overrides: baseOverrides(
          db: db,
          online: online,
          settings: {SettingsStore.localeKey: 'ru'},
          adapter: server.adapter,
          tokens: MemoryTokenStorage(StoredToken(fakeJwt('u-1'), DateTime(2036))),
          clock: () => DateTime.utc(2026, 11, 20, 9),
        ),
        child: const OneBaseApp(),
      ),
    );
    await tester.pumpAndSettle();
  }

  Future<void> scrollTo(WidgetTester tester, Finder f) async {
    await tester.scrollUntilVisible(f, 300, scrollable: find.byType(Scrollable).hitTestable().first);
    await tester.pumpAndSettle();
  }

  testWidgets('приветствие, «Ждут вас», главный показатель, быстрые действия', (tester) async {
    await pumpHome(tester, permissions: [
      'sales.read', 'finance.read', 'hr.read', 'work.tasks', 'ai.agents.run', 'procurement.request', //
    ]);
    expect(find.textContaining('Добрый день, Иван'), findsOneWidget);
    expect(find.text('Ждут вас'), findsOneWidget);
    expect(find.text('Можно получить на складе: 2'), findsOneWidget);
    expect(find.text('Выполнение плана продаж'), findsOneWidget);
    expect(find.text('81,0%'), findsWidgets);
    expect(find.text('+5,0%'), findsOneWidget);
    expect(find.text('Прогноз'), findsOneWidget);
    for (final a in ['Задача', 'Спросить AI', 'Экспорт', 'Отчёт AI']) {
      expect(find.text(a), findsOneWidget);
    }
  });

  testWidgets('«Фокус дня»: критичное первым; динамика; задачи; отделы', (tester) async {
    await pumpHome(tester);
    await scrollTo(tester, find.text('Фокус дня'));
    expect(find.text('Продажи в регионе упали'), findsOneWidget);
    final critical = tester.getTopLeft(find.text('Продажи в регионе упали')).dy;
    final opportunity = tester.getTopLeft(find.text('Можно вырастить АКБ')).dy;
    expect(critical, lessThan(opportunity));
    await scrollTo(tester, find.text('Динамика'));
    await scrollTo(tester, find.text('Отделы'));
    expect(find.text('Явка с начала месяца'), findsOneWidget);
  });

  testWidgets('нет сети — плашка «Нет соединения», данные из кэша остаются', (tester) async {
    await pumpHome(tester, online: false);
    expect(find.textContaining('Нет соединения'), findsOneWidget);
  });

  testWidgets('без прав на продажи и AI — нет главного показателя продаж и «Фокуса дня»', (tester) async {
    await pumpHome(tester, permissions: ['work.tasks']);
    expect(find.text('Выполнение плана продаж'), findsNothing);
    expect(find.text('Спросить AI'), findsNothing);
    expect(find.text('Фокус дня'), findsNothing);
    expect(server.count('GET', '/api/sales/overview'), 0);
    expect(server.count('GET', '/api/ai/alerts'), 0);
  });
}
