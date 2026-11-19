// Синтетические ответы OneBase API для тестов Dashboard: форма — как у настоящего сервера, числа — выдуманные, не данные компании.

import 'fake_server.dart';

Map<String, Object?> salesPeriodJson({int year = 2026, int month = 11, bool closed = false}) => {
      'year': year,
      'month': month,
      'dataThrough': '$year-${month.toString().padLeft(2, '0')}-12',
      'workedDays': 12,
      'daysInMonth': 30,
      'previousCutoff': '$year-10-12',
      'plan': 'rop',
      'availablePlans': <String>[],
      'today': '$year-${month.toString().padLeft(2, '0')}-12',
      'elapsedDays': 11.6,
      'closed': closed,
    };

Map<String, Object?> salesOverviewJson({int year = 2026, int month = 11, double revenue = 2500000000, double? execution = 0.81}) => {
      'period': salesPeriodJson(year: year, month: month),
      'kpi': {
        'factKg': 85000.5,
        'planKg': execution == null ? null : 105000,
        'execution': execution,
        'forecastKg': 210000,
        'forecastExecution': execution == null ? null : 0.95,
        'revenue': revenue,
        'akb': 4321,
        'conversion': {'value': 0.55, 'target': 0.7, 'ratio': 0.78, 'level': 'Warning', 'forecast': null},
        'revenuePerOutlet': {'value': 600000, 'target': 1000000, 'ratio': 0.6, 'level': 'Bad', 'forecast': 800000},
        'akbPerAgent': {'value': 55.5, 'target': 100, 'ratio': 0.555, 'level': 'Bad', 'forecast': 90},
        'visitsWithoutOrder': 3000,
        'visitsDone': 9000,
        'activeAgents': 80,
        'revenuePlan': null,
        'planFactKg': null,
        'planForecastKg': null,
        'planAgents': 0,
        'visitsOutsideTeam': 12,
        'planSource': 'rop',
        'executionLevel': execution == null ? null : 'Warning',
        'altPlan': null,
        'pace': {'expected': 0.4, 'paceKg': 7000, 'requiredKg': 9000, 'level': 'Warning'},
      },
      'activeAgents': 80,
      'flags': {'critical': 3, 'risk': 7},
      'vacancies': 1,
    };

/// `republic` с лишними полями — как настоящий: телефон берёт только календарь и «те же дни».
Map<String, Object?> salesRepublicJson({int year = 2026, int month = 11}) => {
      'period': salesPeriodJson(year: year, month: month),
      'name': 'Республика',
      'kpi': {'factKg': 85000.5},
      'regions': [
        for (var i = 0; i < 5; i++) {'id': 'r$i', 'name': 'Регион $i', 'factKg': 1000 * i},
      ],
      'sameDays': [
        {'id': 'r1', 'name': 'Регион 1', 'kgBefore': 1000, 'kgNow': 1100, 'sumBefore': 1, 'sumNow': 1},
        {'id': 'r2', 'name': 'Регион 2', 'kgBefore': 1000, 'kgNow': 1000, 'sumBefore': 1, 'sumNow': 1},
      ],
      'calendar': {
        'metric': 'kg',
        'categoryId': null,
        'sundays': [for (var d = 1; d <= 30; d++) d % 7 == 1],
        'rows': <Object?>[],
        'totalDays': [for (var d = 1; d <= 30; d++) d <= 12 ? 5000.0 + d * 100 : null],
        'total': 85000.5,
      },
    };

Map<String, Object?> financeJson({bool hasData = true}) => {
      'meta': {
        'source': '1С:Бухгалтерия',
        'kind': 'fact',
        'demo': false,
        'syncedAt': null,
        'infobase': null,
        'organizationId': null,
        'organizationName': null,
        'from': '2026-11-01',
        'to': '2026-11-19',
        'dataThrough': hasData ? '2026-11-18' : null,
        'hasData': hasData,
        'note': hasData ? null : 'Данных 1С в OneBase пока нет: подключите 1С в настройках.',
      },
      'pnl': [
        {'code': 'net_revenue', 'title': 'Чистая выручка', 'kind': 'total', 'value': hasData ? 1200000000 : 0, 'previous': hasData ? 1000000000 : 0},
        {'code': 'net_profit', 'title': 'Чистая прибыль', 'kind': 'total', 'value': hasData ? 150000000 : 0, 'previous': hasData ? 200000000 : 0},
      ],
      'months': [
        for (var m = 0; m < 12; m++)
          {'month': '2026-${(m % 12 + 1).toString().padLeft(2, '0')}-01', 'revenue': hasData ? 1e9 + m * 1e7 : 0, 'expenses': hasData ? 8e8 : 0, 'netProfit': 0},
      ],
      'cashTotal': hasData ? 300000000 : 0,
      'cash': <Object?>[],
      'receivables': 0,
      'payables': 0,
      'advancesGiven': 0,
      'advancesReceived': 0,
      'inventory': <Object?>[],
      'balancesMonth': null,
      'expenseGrowth': <Object?>[],
    };

Map<String, Object?> hrJson() => {
      'date': '2026-11-19',
      'now': '2026-11-19T10:00:00',
      'isToday': true,
      'active': 120,
      'scheduled': 100,
      'present': 87,
      'late': 5,
      'absent': 8,
      'onLeave': 2,
      'compare': {'date': '2026-11-18', 'presentPct': 90.0, 'avgArrival': '08:55:00'},
      'divisions': [
        {'id': 1, 'name': 'Цех 1', 'active': 40, 'scheduled': 40, 'present': 38, 'late': 1, 'absent': 1},
        {'id': 2, 'name': 'Склад', 'active': 20, 'scheduled': 20, 'present': 15, 'late': 3, 'absent': 2},
      ],
      'sync': {'lastSuccessAt': '2026-11-19T09:50:00Z', 'lastStatus': 'Succeeded'},
      'seesAll': true,
    };

Map<String, Object?> workJson() => {
      'open': 12,
      'inProgress': 5,
      'review': 2,
      'overdue': 3,
      'blocked': 1,
      'dueThisWeek': 4,
      'doneThisMonth': 9,
      'myOpen': 2,
      'myOverdue': 1,
      'awaitingMyReview': 0,
      'pendingSuggestions': 0,
      'departments': [
        {'code': 'sales', 'name': 'Продажи', 'open': 6, 'overdue': 2, 'review': 1, 'blocked': 0},
      ],
      'myUpcoming': [
        {'id': 't1', 'title': 'Сверить остатки', 'status': 'InProgress', 'priority': 'High', 'dueDate': '2026-11-20', 'overdue': false, 'departmentName': 'Склад'},
      ],
    };

Map<String, Object?> eventsJson({int count = 3}) => {
      'items': [
        for (var i = 0; i < count; i++)
          {
            'id': 'n$i',
            'type': 'task',
            'category': i.isEven ? 'tasks' : 'hr',
            'kind': 'TaskAssigned',
            'title': 'Событие $i',
            'body': 'Описание события $i',
            'href': null,
            'link': null,
            'createdAt': '2026-11-19T09:${(10 + i).toString().padLeft(2, '0')}:00Z',
            'readAt': null,
            'read': i > 0,
            'severity': 'info',
            'actions': <Object?>[],
          },
      ],
      'nextCursor': null,
      'counts': {'all': count, 'unread': 1},
      'unread': 1,
    };

Map<String, Object?> alertsJson() => {
      'summary': {'problems': 2, 'critical': 1, 'recommendations': 2, 'opportunities': 1, 'byCategory': <Object?>[]},
      'lastRun': {'at': '2026-11-19T08:00:00Z', 'active': 3, 'created': 0, 'resolved': 0, 'errors': <Object?>[]},
      'alerts': [
        {'id': 'a1', 'category': 'sales', 'rule': 'r', 'severity': 'Opportunity', 'title': 'Можно вырастить АКБ', 'message': 'м1', 'recommendation': 'р1', 'detectedAt': '2026-11-18T10:00:00Z'},
        {'id': 'a2', 'category': 'sales', 'rule': 'r', 'severity': 'Critical', 'title': 'Продажи в регионе упали', 'message': 'м2', 'recommendation': 'Разобрать с РМ', 'detectedAt': '2026-11-17T10:00:00Z'},
        {'id': 'a3', 'category': 'supply', 'rule': 'r', 'severity': 'Warning', 'title': 'Сырьё на исходе', 'message': 'м3', 'recommendation': 'р3', 'detectedAt': '2026-11-19T10:00:00Z'},
      ],
    };

/// Все маршруты Dashboard с данными.
void serveDashboard(FakeServer s, {List<String>? permissions}) {
  s
    ..json(
      'GET',
      '/api/auth/me',
      permissions == null ? profileJson() : profileJson(permissions: permissions, hrAccess: permissions.contains('hr.read')),
    )
    ..json('GET', '/api/sales/overview', salesOverviewJson())
    ..json('GET', '/api/sales/republic', salesRepublicJson())
    ..json('GET', '/api/sales/months', [
      {'year': 2026, 'month': 11},
      {'year': 2026, 'month': 10},
    ])
    ..json('GET', '/api/finance/overview', financeJson())
    ..json('GET', '/api/finance/organizations', <Object?>[])
    ..json('GET', '/api/hr/overview', hrJson())
    ..json('GET', '/api/work/overview', workJson())
    ..json('GET', '/api/notifications', eventsJson())
    ..json('GET', '/api/ai/alerts', alertsJson())
    ..json('GET', '/api/hr/reports', {'attendanceRate': 84.6, 'punctuality': 60.1})
    ..json('GET', '/api/procurement/home', {
      'roles': {'director': false, 'financier': false},
      'requests': {'awaitingMe': 0, 'approvals': <Object?>[], 'departmentToPickUp': 0, 'mineToPickUp': 2},
      'accepted': <Object?>[],
      'acceptedCount': 0,
      'acceptedAmount': 0,
    });
}
