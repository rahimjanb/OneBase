import 'package:flutter_test/flutter_test.dart';
import 'package:onebase_mobile/features/dashboard/data/dashboard_models.dart';
import 'package:onebase_mobile/features/dashboard/domain/home_view.dart';

import '../../helpers/dashboard_fixtures.dart';

/// Расчёты главной — перенос веба (`dashboard-data.ts`, `dashboard-home.ts`, `dashboard-mobile.ts`): те же входы — те же числа.
void main() {
  final overview = SalesOverview.fromJson(salesOverviewJson());
  final republic = salesDailyOf(salesRepublicJson());

  group('даты Ташкента и недели', () {
    test('ISO-неделя, понедельник, неделя AI-отчёта', () {
      expect(isoWeek('2026-10-30'), 44);
      expect(isoWeek('2026-01-01'), 1);
      expect(isoWeek('2027-01-01'), 53);
      expect(mondayOf('2026-11-20'), '2026-11-16');
      expect(mondayOf('2026-11-16'), '2026-11-16');
      expect(mondayOf('2026-11-22'), '2026-11-16');
      expect(weekId(DateTime.utc(2026, 11, 20, 20)), '2026-W47');
      // 22:00 UTC воскресенья — в Ташкенте уже понедельник следующей недели.
      expect(weekId(DateTime.utc(2026, 11, 22, 22)), '2026-W48');
    });

    test('12 недель по понедельникам, последняя — текущая', () {
      final weeks = homeWeeks('2026-11-20');
      expect(weeks, hasLength(12));
      expect(weeks.last.start, '2026-11-16');
      expect(weeks.first.start, '2026-08-31');
      expect(weeks.last.week, 47);
      expect(currentDaysOf('2026-11-20'), 5);
      expect(monthsFor(weeks, 2026, 11), [(2026, 8), (2026, 9), (2026, 10), (2026, 11)]);
    });

    test('приветствие по времени Ташкента', () {
      expect(dayPartOf(DateTime.utc(2026, 11, 20, 2)), DayPart.morning); // 07:00
      expect(dayPartOf(DateTime.utc(2026, 11, 20, 8)), DayPart.day); // 13:00
      expect(dayPartOf(DateTime.utc(2026, 11, 20, 14)), DayPart.evening); // 19:00
      expect(dayPartOf(DateTime.utc(2026, 11, 20, 19)), DayPart.night); // 00:00
      expect(isNight(DateTime.utc(2026, 11, 20, 14)), isTrue);
    });
  });

  group('главная карточка', () {
    test('есть план продаж — выполнение плана, кольцо цели 90%, к тем же дням', () {
      final v = homeView(sales: SalesBrief.of(overview, republic), hr: HrBrief.of(HrOverview.fromJson(hrJson()), 84.6), tasks: WorkOverview.fromJson(workJson()));
      expect(v.hero!.kind, HeroKind.sales);
      expect(v.hero!.value, closeTo(81, 0.001));
      expect(v.hero!.target, 90);
      expect(v.hero!.ringTone, HomeTone.warning); // уровень сервера Warning
      expect(v.hero!.delta, closeTo(5, 0.001)); // (1100 + 1000) / (1000 + 1000) − 1 = +5%
      expect(v.stats.map((s) => s.kind), [StatKind.forecast, StatKind.attendance, StatKind.tasks]);
      expect(v.headline!.kind, HeroKind.sales);
    });

    test('нет плана продаж — явка сегодня, норма 80%, три колонки из того, что есть', () {
      final noPlan = SalesOverview.fromJson(salesOverviewJson(execution: null));
      final hr = HrBrief.of(HrOverview.fromJson(hrJson()), 81.9);
      final v = homeView(sales: SalesBrief.of(noPlan, republic), hr: hr, tasks: WorkOverview.fromJson(workJson()));
      expect(v.hero!.kind, HeroKind.attendance);
      expect(v.hero!.target, 80);
      expect(v.stats.map((s) => s.kind), [StatKind.tasks, StatKind.attendanceMonth, StatKind.revenue]);
      expect(v.headline!.kind, HeroKind.attendance);
    });

    test('ничего нет — ни карточки, ни итога', () {
      final v = homeView();
      expect(v.hero, isNull);
      expect(v.stats, isEmpty);
      expect(v.headline, isNull);
    });
  });

  group('недели продаж', () {
    SalesDaily month({required int m, double? planKg, double? planFactKg, double factKg = 0, bool closed = true, int days = 30, String? through}) =>
        SalesDaily(
          year: 2026,
          month: m,
          dataThrough: through ?? '2026-${m.toString().padLeft(2, '0')}-$days',
          daysInMonth: days,
          closed: closed,
          today: '2026-11-20',
          days: [for (var d = 1; d <= days; d++) 100.0],
          planKg: planKg,
          planFactKg: planFactKg,
          factKg: factKg,
          workedDays: 20,
          elapsedDays: 19.5,
        );

    test('план недели по темпу месяца, факт в охвате плана; без плана — выполнения нет, выручка — сумма по дням', () {
      final weeks = homeWeeks('2026-11-20');
      final byMonth = {
        '2026-10': MonthDays(kg: month(m: 10, planKg: 3100, days: 31), sum: month(m: 10, days: 31)),
        '2026-11': MonthDays(
          kg: month(m: 11, planKg: 3000, planFactKg: 1500, factKg: 2000, closed: false, through: '2026-11-19'),
        ),
      };
      final r = salesWeeks(weeks, byMonth);
      // Неделя 26.10–01.11: 6 дней октября (план 100/день, факт 100) + 1 день ноября (план 100, факт 100 × 0,75).
      final i = weeks.indexWhere((w) => w.start == '2026-10-26');
      expect(r.execution[i], closeTo(100 * (600 + 75) / 700, 1e-9));
      expect(r.revenue[i], 600); // выручка есть только у октября
      // Сентябрь не загружен — недель без данных нет.
      expect(r.execution.first, isNull);
      expect(r.revenue.first, isNull);
      // Текущая неделя: 16–19.11 (данные до 19-го), сегодня 20-е — без данных.
      expect(r.execution.last, closeTo(100 * (4 * 75) / (4 * 100), 1e-9));
    });

    test('шкала и итоги ряда как в вебе', () {
      const pct = TrendSeries(key: SeriesKey.sales, weeks: [1, 2, 3], values: [70, null, 95], unit: SeriesUnit.pct, target: 90);
      expect(trendScale(pct), (lo: 60.0, hi: 100.0));
      final st = trendStats(pct);
      expect(st.count, 2);
      expect(st.average, 82.5);
      expect(st.best, 95);
      expect(st.above, 1);
      const low = TrendSeries(key: SeriesKey.attendance, weeks: [1, 2], values: [42, 104], unit: SeriesUnit.pct, target: 80);
      expect(trendScale(low), (lo: 30.0, hi: 110.0));
      const money = TrendSeries(key: SeriesKey.revenue, weeks: [1, 2], values: [3.62e9, 1e9], unit: SeriesUnit.money);
      expect(trendScale(money), (lo: 0.0, hi: 4e9));
      expect(trendStats(money).above, isNull);
      expect(weekChange([null, 70, 75.5]), 5.5);
      expect(weekChange([70]), isNull);
    });
  });

  group('«Фокус дня» и отделы', () {
    test('ключевая цифра находки — как у веба', () {
      expect(metricOf('Продажи в регионе Бекобод снизились на 41%').metric, (value: '41%', caption: ''));
      expect(metricOf('Запасы заканчиваются: 28 SKU меньше чем на 15 дней продаж').metric, (value: '28', caption: 'SKU'));
      final tail = metricOf('Выполнение плана под угрозой — прогноз 44%');
      expect(tail.text, 'Выполнение плана под угрозой');
      expect(tail.metric, (value: '44%', caption: 'прогноз'));
      expect(metricOf('Без чисел').metric, isNull);
      expect(metricOf('Выручка 1 234,5 млн').metric!.value, '1 234,5');
    });

    test('находки по важности: критичные, важные, возможности; отчёт недели — из сводки', () {
      final alerts = AiAlerts.fromJson(alertsJson());
      expect(focusOf(alerts).map((f) => f.severity), [Severity.critical, Severity.warning, Severity.info]);
      final r = weeklyReportOf(alerts, DateTime.utc(2026, 11, 20))!;
      expect((r.id, r.risks, r.recommendations), ('2026-W47', 2, 2));
      expect(weeklyReportOf(null, DateTime.utc(2026, 11, 20)), isNull);
    });

    test('отделы: требующие внимания первыми, изменение за неделю — из рядов', () {
      final rows = homeDepartments(
        sales: SalesBrief.of(overview, republic),
        hr: const HrBrief(presentPct: 70, comparePct: null, monthRate: 85, syncedAt: null),
        trends: HomeTrends(weeks: homeWeeks('2026-11-20'), currentDays: 5, sales: [80, 84], attendance: [86, 85]),
      );
      expect(rows.map((r) => r.code), ['sales', 'hr']);
      expect(rows.first.attention, isTrue);
      expect(rows.first.trendWeek, 4);
      expect(rows.last.attention, isFalse);
      expect(rows.last.trendWeek, -1);
    });
  });
}
