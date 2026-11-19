import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:onebase_mobile/app/theme/theme.dart';
import 'package:onebase_mobile/core/auth/auth_controller.dart';
import 'package:onebase_mobile/core/auth/user_profile.dart';
import 'package:onebase_mobile/core/localization/formatters.dart';
import 'package:onebase_mobile/core/navigation/web_links.dart';
import 'package:onebase_mobile/features/departments/domain/departments_view.dart';
import 'package:onebase_mobile/features/departments/presentation/departments_screen.dart';
import 'package:onebase_mobile/features/insights/presentation/insights_screen.dart';
import 'package:onebase_mobile/features/search/presentation/search_screen.dart';
import 'package:onebase_mobile/l10n/gen/app_localizations.dart';
import 'package:onebase_mobile/l10n/gen/app_localizations_ru.dart';

import '../helpers/dashboard_fixtures.dart';
import '../helpers/fake_server.dart';
import '../helpers/harness.dart';
import '../helpers/test_db.dart';

Future<void> pumpScreen(WidgetTester tester, FakeServer server, Widget screen, {required List<String> permissions, List<String> departments = const []}) async {
  tester.view.physicalSize = const Size(1179, 2556);
  tester.view.devicePixelRatio = 3;
  addTearDown(tester.view.reset);
  final db = memoryDb();
  addTearDown(db.close);
  await tester.pumpWidget(ProviderScope(
    retry: (_, _) => null,
    overrides: [
      ...baseOverrides(db: db, adapter: server.adapter),
      currentProfileProvider.overrideWithValue(UserProfile(id: 'u-1', login: 'u', name: 'Тест', permissions: permissions, departments: departments)),
    ],
    child: MaterialApp(
      theme: ObTheme.dark(),
      locale: const Locale('ru'),
      supportedLocales: AppLocalizations.supportedLocales,
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      home: screen,
    ),
  ));
  await tester.pumpAndSettle();
}

void main() {
  setUpAll(() => initializeDateFormatting('ru'));
  final l10n = AppLocalizationsRu();
  final ctx = DeptCtx(l10n, Fmt(l10n), month: 'ноябрь 2026', current: true);
  final past = DeptCtx(l10n, Fmt(l10n), month: 'октябрь 2026', current: false);

  group('Отделы: карточки как у веба', () {
    test('продажи: прогноз, выполнен, закрытый месяц, без плана', () {
      SalesData s({double? plan = 100000, double fact = 81000, double? forecast = 0.95, bool closed = false}) =>
          (year: 2026, month: 11, closed: closed, planKg: plan, factKg: fact, execution: plan == null ? null : fact / plan, forecastExecution: forecast);
      final a = salesCard(Loaded(s()), ctx);
      expect((a.value, a.status, a.bar?.value, a.bar?.forecast), (95, DeptStatus.attention, 81, 95));
      expect(a.detail, 'Факт 81 из 100 т (81%) · прогноз 95%');
      expect(salesCard(Loaded(s(forecast: 1.04)), ctx).status, DeptStatus.ok);
      expect(salesCard(Loaded(s(fact: 101000)), ctx).status, DeptStatus.done);
      final closed = salesCard(Loaded(s(closed: true)), past);
      expect((closed.value, closed.label, closed.bar?.forecast), (81, 'Выполнение плана', null));
      final noPlan = salesCard(Loaded(s(plan: null)), ctx);
      expect((noPlan.status, noPlan.value, noPlan.detail), (DeptStatus.attention, null, 'Плана на ноябрь 2026 нет · факт 81 т'));
      expect(salesCard(const Failed(), ctx).status, DeptStatus.error);
      expect(salesCard(const Forbidden(), ctx).value, isNull);
    });

    test('HR: норма 80%, «сегодня»; прошлый месяц без строки «сегодня»; снабжение — только на сегодня', () {
      final h = hrCard(const Loaded((monthRate: 84.6, presentPct: 87.0)), ctx);
      expect((h.value, h.status, h.detail, h.label), (85, DeptStatus.ok, 'Явка сегодня: 87%', 'Явка с начала месяца'));
      expect(hrCard(const Loaded((monthRate: 70.0, presentPct: 0.0)), ctx).detail, 'Сегодня отметок пока нет');
      expect(hrCard(const Loaded((monthRate: 70.0, presentPct: null)), past).detail, isNull);
      expect(hrCard(const Loaded((monthRate: null, presentPct: null)), ctx).detail, 'Нет данных за ноябрь 2026');
      final sp = supplyCard(const Loaded((toBuy: 8, overdue: 2, onApproval: 3)), ctx);
      expect((sp.value, sp.status, sp.detail), (75, DeptStatus.attention, 'К закупке 8 · просрочено 2 · на согласовании 3'));
      expect(supplyCard(const Loaded((toBuy: 0, overdue: 0, onApproval: 0)), ctx).value, 100);
      expect(supplyCard(const Loaded((toBuy: 8, overdue: 2, onApproval: 3)), past).detail, startsWith('Показатель только на сегодня'));
    });

    test('месяц из ссылки: будущий и неверный — текущий; границы месяца', () {
      expect(parseMonth('2027-01', '2026-11-20').key, '2026-11');
      expect(parseMonth('2026-13', '2026-11-20').current, isTrue);
      expect(parseMonth('2026-02', '2026-11-20').current, isFalse);
      expect(monthRange(parseMonth('2026-02', '2026-11-20'), '2026-11-20'), (from: '2026-02-01', to: '2026-02-28'));
      expect(monthRange(parseMonth(null, '2026-11-20'), '2026-11-20'), (from: '2026-11-01', to: '2026-11-20'));
      expect(monthOptions('2026-02-10', count: 3).map((m) => m.key), ['2026-02', '2026-01', '2025-12']);
    });
  });

  test('ссылки веба: задачи отдела, новая задача, заявка → список заявок, отделы с месяцем', () {
    expect(appRouteForWeb('/tasks/production'), '/tasks?department=production');
    expect(appRouteForWeb('/tasks/new'), '/tasks/new');
    expect(appRouteForWeb('/requests/01a1cc8f-2e11-79b7-bc9d-d3179d487100'), '/home/requests');
    expect(appRouteForWeb('/departments?month=2026-10'), '/home/departments?month=2026-10');
  });

  testWidgets('экран «Отделы»: только открытые отделы, показатели продаж и HR, у производства — «Нет данных»', (tester) async {
    final server = FakeServer()
      ..json('GET', '/api/sales/overview', salesOverviewJson())
      ..json('GET', '/api/hr/reports', {'attendanceRate': 84.6})
      ..json('GET', '/api/hr/overview', hrJson());
    await pumpScreen(tester, server, const DepartmentsScreen(), permissions: ['sales.read', 'hr.read', 'work.tasks'], departments: ['production']);
    expect(find.text('Производство'), findsOneWidget);
    expect(find.text('Продажи'), findsOneWidget);
    expect(find.text('HR'), findsOneWidget);
    expect(find.text('Финансы'), findsNothing);
    expect(find.text('Снабжение'), findsNothing);
    expect(find.text('95%'), findsOneWidget);
    expect(find.text('85%'), findsOneWidget);
    expect(find.text('Явка сегодня: 87%'), findsOneWidget);
    expect(find.textContaining('Нет данных за'), findsOneWidget);
    expect(server.count('GET', '/api/procurement/board'), 0);
  });

  testWidgets('экран «AI-находки»: сводка, фильтр по отделу; «Проверить сейчас» — только администратору AI', (tester) async {
    final alerts = alertsJson();
    (alerts['summary']! as Map<String, Object?>)['byCategory'] = [
      {'category': 'sales', 'problems': 1, 'critical': 1, 'opportunities': 1},
      {'category': 'supply', 'problems': 1, 'critical': 0, 'opportunities': 0},
      {'category': 'finance', 'problems': 0, 'critical': 0, 'opportunities': 0},
    ];
    final server = FakeServer()
      ..json('GET', '/api/ai/alerts', alerts)
      ..json('GET', '/api/ai/agents', [
        {'code': 'director', 'name': 'Главный консультант', 'isConsultant': true, 'available': true, 'description': 'Понимает вопрос'},
        {'code': 'sales', 'name': 'Sales AI', 'role': 'Директор по продажам', 'isConsultant': false, 'available': true},
      ]);
    await pumpScreen(tester, server, const InsightsScreen(), permissions: ['ai.agents.run', 'sales.read']);
    expect(find.text('Можно вырастить АКБ'), findsOneWidget); // список ниже — лениво, по прокрутке
    expect(find.text('Проверить сейчас'), findsNothing);
    expect(find.text('Все · 3'), findsOneWidget);
    expect(find.textContaining('Финансы'), findsNothing); // отдел без находок — без чипа
    await tester.ensureVisible(find.text('Снабжение · 1'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Снабжение · 1'));
    await tester.pumpAndSettle();
    expect(find.text('Продажи в регионе упали'), findsNothing);
    expect(find.text('Сырьё на исходе'), findsOneWidget);
    await tester.scrollUntilVisible(find.text('Sales AI'), 200, scrollable: find.byType(Scrollable).first);
    expect(find.text('Sales AI'), findsOneWidget);
    expect(server.count('POST', '/api/ai/alerts/run'), 0);
  });

  testWidgets('поиск: разделы и отделы сразу, задачи и сотрудники — с сервера от двух букв; без права HR людей не ищем', (tester) async {
    final server = FakeServer()
      ..json('GET', '/api/work/tasks', {
        'items': [{'id': 't1', 'title': 'Продлить договор поставки', 'departmentName': 'Снабжение'}],
        'total': 1,
      })
      ..json('GET', '/api/hr/employees', {'items': [{'id': 'e1', 'fullName': 'PROKOFIEV IVAN', 'job': 'Технолог'}], 'total': 1});
    await pumpScreen(tester, server, const SearchScreen(), permissions: ['sales.read', 'work.tasks', 'ai.agents.run']);
    expect(find.text('Задачи'), findsWidgets); // раздел «Задачи» в списке разделов
    await tester.enterText(find.byType(TextField), 'про');
    await tester.pump(const Duration(milliseconds: 300));
    await tester.pumpAndSettle();
    expect(find.text('Профиль'), findsOneWidget);
    expect(find.text('Продажи'), findsOneWidget); // отдел
    expect(find.text('Продлить договор поставки'), findsOneWidget);
    expect(find.text('Prokofiev Ivan'), findsNothing);
    expect(server.count('GET', '/api/hr/employees'), 0);
    final q = server.requests.singleWhere((r) => r.path == '/api/work/tasks').queryParameters;
    expect((q['scope'], q['search'], q['pageSize']), ('all', 'про', 5));

    await tester.enterText(find.byType(TextField), 'zzzz');
    await tester.pump(const Duration(milliseconds: 300));
    server.json('GET', '/api/work/tasks', {'items': <Object?>[], 'total': 0});
    await tester.pumpAndSettle();
  });
}
