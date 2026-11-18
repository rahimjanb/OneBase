import 'package:flutter_test/flutter_test.dart';
import 'package:onebase_mobile/features/dashboard/data/dashboard_models.dart';
import 'package:onebase_mobile/features/dashboard/domain/dashboard_period.dart';
import 'package:onebase_mobile/features/dashboard/presentation/widgets/charts.dart';
import 'package:onebase_mobile/features/dashboard/presentation/widgets/insights_section.dart';

import '../../helpers/dashboard_fixtures.dart';

void main() {
  test('продажи: KPI, цели и уровни — как прислал сервер', () {
    final o = SalesOverview.fromJson(salesOverviewJson());
    expect(o.period.month, 11);
    expect(o.period.elapsedDays, 11.6);
    expect(o.kpi.revenue, 2500000000);
    expect(o.kpi.akb, 4321);
    expect(o.kpi.execution, 0.81);
    expect(o.kpi.akbPerAgent?.level, 'Bad');
    expect(o.kpi.pace?.requiredKg, 9000);
    expect(o.flags.critical, 3);
    // Кэш: toJson → fromJson без потерь.
    expect(SalesOverview.fromJson(o.toJson()), o);
  });

  test('ряд по дням вырезается из большого ответа republic; к тем же дням — по сумме строк', () {
    final d = salesDailyOf(salesRepublicJson());
    expect(d.days.length, 30);
    expect(d.days.first, 5100);
    expect(d.days[12], isNull);
    expect(d.sundays.first, isTrue);
    expect(d.total, 85000.5);
    expect(d.sameNowKg, 2100);
    expect(d.sameBeforeKg, 2000);
    expect(d.vsPrevMonth, closeTo(0.05, 1e-9));
    expect(SalesDaily.fromJson(d.toJson()), d);
  });

  test('без прошлого месяца сравнения нет', () {
    final json = salesRepublicJson()..['sameDays'] = <Object?>[];
    expect(salesDailyOf(json).vsPrevMonth, isNull);
  });

  test('финансы: строки P&L по коду, расходы — из ряда месяцев', () {
    final f = FinanceOverview.fromJson(financeJson());
    expect(f.meta.hasData, isTrue);
    expect(f.line('net_revenue')?.previous, 1000000000);
    expect(f.line('нет такой'), isNull);
    expect(f.lastMonth?.expenses, 8e8);
    final empty = FinanceOverview.fromJson(financeJson(hasData: false));
    expect(empty.meta.note, contains('1С'));
  });

  test('HR: доля явки и числовой id подразделения', () {
    final h = HrOverview.fromJson(hrJson());
    expect(h.presentShare, 0.87);
    expect(h.divisions.first.id, '1');
    expect(HrOverview.fromJson({'date': '2026-11-19'}).presentShare, isNull);
  });

  test('задачи, события, находки', () {
    expect(WorkOverview.fromJson(workJson()).myUpcoming.single.priority, 'High');
    expect(NotificationPage.fromJson(eventsJson()).items.length, 3);
    final a = AiAlerts.fromJson(alertsJson());
    expect(sortAlerts(a.alerts).map((x) => x.severity), ['Critical', 'Warning', 'Opportunity']);
  });

  group('период', () {
    test('последние данные — без параметров', () {
      const p = DashboardPeriod.latest();
      expect(p.salesQuery, isEmpty);
      expect(p.financeQuery, isEmpty);
      expect(p.key, 'latest');
    });

    test('месяц: продажи — год и месяц, финансы — с 1-го по последний день', () {
      expect(const DashboardPeriod(2026, 11).salesQuery, {'year': 2026, 'month': 11});
      expect(const DashboardPeriod(2026, 11).financeQuery, {'from': '2026-11-01', 'to': '2026-11-30'});
      expect(const DashboardPeriod(2028, 2).financeQuery, {'from': '2028-02-01', 'to': '2028-02-29'});
      expect(const DashboardPeriod(2026, 12).financeQuery['to'], '2026-12-31');
    });

    test('последние 12 месяцев переходят через год', () {
      final months = recentMonths(DateTime(2026, 2, 10));
      expect(months.first, const DashboardPeriod(2026, 2));
      expect(months[2], const DashboardPeriod(2025, 12));
      expect(months.length, 12);
    });
  });

  test('шаг сетки графика — 1, 2 или 5 × 10ⁿ', () {
    expect(niceStep(5400), 10000);
    expect(niceStep(1800), 2000);
    expect(niceStep(420), 500);
    expect(niceStep(0), 1);
  });
}
