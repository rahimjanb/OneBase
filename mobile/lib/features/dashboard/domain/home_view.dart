import 'dart:math' as math;

import '../data/dashboard_models.dart';

// Вид главной — перенос расчётов мобильного «Обзора» веба (`frontend/src/lib/dashboard-data.ts`, `dashboard-home.ts`,
// `dashboard-mobile.ts`), чтобы телефон показывал те же цифры, что и сайт. Сервер считает показатели (выполнение, прогноз, уровни,
// явку); здесь — только раскладка по блокам и недельные ряды из дневных итогов, как это делает сервер веба (BFF) для своей страницы.

// ───────────── Даты по времени компании (Ташкент, UTC+5 без перехода на летнее время) ─────────────

DateTime tashkentNow([DateTime? now]) => (now ?? DateTime.now()).toUtc().add(const Duration(hours: 5));

String isoDate(DateTime d) =>
    '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';

DateTime parseDate(String s) => DateTime.utc(int.parse(s.substring(0, 4)), int.parse(s.substring(5, 7)), int.parse(s.substring(8, 10)));

String addDays(String date, int n) => isoDate(parseDate(date).add(Duration(days: n)));

/// Понедельник недели даты.
String mondayOf(String date) => addDays(date, 1 - parseDate(date).weekday);

/// Номер недели ISO: «2026-10-30» → 44.
int isoWeek(String date) {
  final d = parseDate(date);
  final thursday = d.add(Duration(days: 4 - d.weekday));
  final yearStart = DateTime.utc(thursday.year);
  return ((thursday.difference(yearStart).inDays + 1) / 7).ceil();
}

/// «2026-W47» — неделя AI-отчёта.
String weekId(DateTime now) {
  final t = tashkentNow(now);
  final date = isoDate(t);
  final d = parseDate(date);
  final thursday = d.add(Duration(days: 4 - d.weekday));
  return '${thursday.year}-W${isoWeek(date).toString().padLeft(2, '0')}';
}

/// Время суток для приветствия: утро 5–12, день 12–18, вечер 18–23, ночь.
enum DayPart { morning, day, evening, night }

DayPart dayPartOf(DateTime now) {
  final h = tashkentNow(now).hour;
  if (h >= 5 && h < 12) return DayPart.morning;
  if (h >= 12 && h < 18) return DayPart.day;
  if (h >= 18 && h < 23) return DayPart.evening;
  return DayPart.night;
}

/// Вечер и ночь — значок луны.
bool isNight(DateTime now) {
  final h = tashkentNow(now).hour;
  return h >= 18 || h < 5;
}

// ───────────── Тона ─────────────

enum HomeTone { success, warning, danger }

/// Уровень продаж с сервера: от 90% — зелёный, от 60% — оранжевый, ниже — красный.
HomeTone salesTone(String? level, double value) => switch (level) {
      'Good' => HomeTone.success,
      'Warning' => HomeTone.warning,
      'Bad' => HomeTone.danger,
      _ => value >= 90 ? HomeTone.success : (value >= 60 ? HomeTone.warning : HomeTone.danger),
    };

/// Явка — как в HR: от 80% — зелёный, 70–79% — жёлтый, ниже — красный.
HomeTone attendanceTone(double value) => value >= 80 ? HomeTone.success : (value >= 70 ? HomeTone.warning : HomeTone.danger);

// ───────────── Быстрые данные: продажи, HR, задачи ─────────────

class SalesBrief {
  const SalesBrief({
    required this.period,
    required this.execution,
    required this.forecastExecution,
    required this.level,
    required this.revenue,
    required this.vsPrevMonth,
  });

  final SalesPeriod period;

  /// Факт к плану месяца, 0–1; плана нет — null.
  final double? execution;
  final double? forecastExecution;
  final String? level;
  final double revenue;

  /// Продажи (кг) к тем же дням прошлого месяца: +0,02 = +2 %.
  final double? vsPrevMonth;

  factory SalesBrief.of(SalesOverview overview, SalesDaily? republic) => SalesBrief(
        period: overview.period,
        execution: overview.kpi.execution,
        forecastExecution: overview.kpi.forecastExecution,
        level: overview.kpi.executionLevel,
        revenue: overview.kpi.revenue,
        vsPrevMonth: republic?.vsPrevMonth,
      );
}

class HrBrief {
  const HrBrief({required this.presentPct, required this.comparePct, required this.monthRate, required this.syncedAt});

  /// Явка сегодня: на месте от «по графику», %.
  final double? presentPct;

  /// Прошлый рабочий день в это время, %.
  final double? comparePct;

  /// Явка с начала месяца, %.
  final double? monthRate;
  final String? syncedAt;

  factory HrBrief.of(HrOverview o, double? monthRate) => HrBrief(
        presentPct: o.scheduled > 0 ? 100 * o.present / o.scheduled : null,
        comparePct: o.compare?.presentPct,
        monthRate: monthRate,
        syncedAt: o.sync?.lastSuccessAt,
      );
}

/// Куда ведёт показатель главной.
enum HomeTarget { sales, hr, hrReports, tasks, tasksOverdue, tasksDone, tasksProgress, tasksReview }

class HomeHero {
  const HomeHero({required this.kind, required this.value, required this.delta, required this.ringTone, required this.target});

  /// sales — выполнение плана продаж; attendance — явка сегодня.
  final HeroKind kind;
  final double value;

  /// Изменение к прошлому периоду: % (продажи) или п. п. (явка).
  final double? delta;
  final HomeTone ringTone;

  /// Цель кольца: 90% у продаж, норма 80% у явки.
  final double target;

  HomeTarget get route => kind == HeroKind.sales ? HomeTarget.sales : HomeTarget.hr;
}

enum HeroKind { sales, attendance }

enum StatKind { forecast, attendance, tasks, attendanceMonth, revenue }

enum StatFormat { pct1, int, money }

enum StatSubKind { toMonthEnd, pp, overdue, dueWeek, perMonth }

enum SubTone { success, danger, link, muted }

class HomeStat {
  const HomeStat({
    required this.kind,
    required this.value,
    required this.format,
    this.warning = false,
    this.sub,
    this.subValue = 0,
    this.subTone = SubTone.muted,
    required this.route,
  });

  final StatKind kind;
  final double value;
  final StatFormat format;
  final bool warning;
  final StatSubKind? sub;

  /// Число в подписи (п. п., количество задач).
  final double subValue;
  final SubTone subTone;
  final HomeTarget route;
}

class HomeHeadline {
  const HomeHeadline({required this.kind, required this.value, required this.tone});

  final HeroKind kind;
  final double value;
  final HomeTone tone;
}

class HomeView {
  const HomeView({required this.hero, required this.stats, required this.headline});

  final HomeHero? hero;
  final List<HomeStat> stats;
  final HomeHeadline? headline;
}

/// Главный показатель — выполнение плана продаж; нет доступа или плана — явка; три колонки под ним — из того, что есть.
HomeView homeView({SalesBrief? sales, HrBrief? hr, WorkOverview? tasks}) {
  final execution = sales?.execution != null ? 100 * sales!.execution! : null;
  final present = hr?.presentPct;
  final presentDelta = present != null && hr?.comparePct != null ? present - hr!.comparePct! : null;

  HomeHero? hero;
  if (sales != null && execution != null) {
    hero = HomeHero(
      kind: HeroKind.sales,
      value: execution,
      delta: sales.vsPrevMonth != null ? 100 * sales.vsPrevMonth! : null,
      ringTone: salesTone(sales.level, execution),
      target: 90,
    );
  } else if (present != null) {
    hero = HomeHero(kind: HeroKind.attendance, value: present, delta: presentDelta, ringTone: attendanceTone(present), target: 80);
  }

  final candidates = <HomeStat?>[
    sales?.forecastExecution != null && hero?.kind == HeroKind.sales
        ? HomeStat(
            kind: StatKind.forecast,
            value: 100 * sales!.forecastExecution!,
            format: StatFormat.pct1,
            sub: StatSubKind.toMonthEnd,
            route: HomeTarget.sales,
          )
        : null,
    present != null && hero?.kind != HeroKind.attendance
        ? HomeStat(
            kind: StatKind.attendance,
            value: present,
            format: StatFormat.pct1,
            warning: attendanceTone(present) != HomeTone.success,
            sub: presentDelta != null ? StatSubKind.pp : null,
            subValue: presentDelta ?? 0,
            subTone: presentDelta == null ? SubTone.muted : (presentDelta >= 0 ? SubTone.success : SubTone.danger),
            route: HomeTarget.hr,
          )
        : null,
    tasks != null
        ? HomeStat(
            kind: StatKind.tasks,
            value: tasks.open.toDouble(),
            format: StatFormat.int,
            sub: tasks.overdue > 0 ? StatSubKind.overdue : (tasks.dueThisWeek > 0 ? StatSubKind.dueWeek : null),
            subValue: (tasks.overdue > 0 ? tasks.overdue : tasks.dueThisWeek).toDouble(),
            subTone: tasks.overdue > 0 ? SubTone.danger : SubTone.link,
            route: tasks.overdue > 0 ? HomeTarget.tasksOverdue : HomeTarget.tasks,
          )
        : null,
    hr?.monthRate != null
        ? HomeStat(kind: StatKind.attendanceMonth, value: hr!.monthRate!, format: StatFormat.pct1, route: HomeTarget.hrReports)
        : null,
    sales != null
        ? HomeStat(kind: StatKind.revenue, value: sales.revenue, format: StatFormat.money, sub: StatSubKind.perMonth, route: HomeTarget.sales)
        : null,
  ];

  return HomeView(
    hero: hero,
    stats: candidates.whereType<HomeStat>().take(3).toList(),
    headline: execution != null
        ? HomeHeadline(kind: HeroKind.sales, value: execution, tone: salesTone(sales?.level, execution))
        : present != null
            ? HomeHeadline(kind: HeroKind.attendance, value: present, tone: attendanceTone(present))
            : null,
  );
}

// ───────────── Задачи ─────────────

enum TaskSlice { done, progress, review }

class MobileTasks {
  const MobileTasks({required this.total, required this.done, required this.overdue, required this.slices, required this.departments});

  final int total;
  final int done;
  final int overdue;
  final List<(TaskSlice, int)> slices;
  final List<({String code, String name, int overdue})> departments;
}

/// Кольцо: выполненные за месяц, в работе (новые и в работе), на проверке; просроченные — часть открытых, отдельной строкой.
MobileTasks homeTasks(WorkOverview t) => MobileTasks(
      total: t.doneThisMonth + t.open,
      done: t.doneThisMonth,
      overdue: t.overdue,
      slices: [
        (TaskSlice.done, t.doneThisMonth),
        (TaskSlice.progress, math.max(0, t.open - t.review)),
        (TaskSlice.review, t.review),
      ],
      departments: [for (final d in t.departments) (code: d.code, name: d.name, overdue: d.overdue)],
    );

// ───────────── Недели «Динамики» ─────────────

class HomeWeek {
  const HomeWeek({required this.start, required this.end, required this.week});

  final String start;
  final String end;
  final int week;
}

/// 12 недель по понедельникам, последняя — текущая (неполная).
List<HomeWeek> homeWeeks(String today) {
  final lastMonday = mondayOf(today);
  return [
    for (var i = 0; i < 12; i++)
      () {
        final start = addDays(lastMonday, -7 * (11 - i));
        return HomeWeek(start: start, end: addDays(start, 6), week: isoWeek(start));
      }(),
  ];
}

/// Месяцы, которые покрывают недели: от текущего назад, пока начало месяца не раньше первой недели.
List<(int, int)> monthsFor(List<HomeWeek> weeks, int year, int month) {
  final out = <(int, int)>[];
  var y = year;
  var m = month;
  while (true) {
    out.insert(0, (y, m));
    final first = '${y.toString().padLeft(4, '0')}-${m.toString().padLeft(2, '0')}-01';
    if (first.compareTo(weeks.first.start) <= 0) break;
    if (m == 1) {
      m = 12;
      y--;
    } else {
      m--;
    }
  }
  return out;
}

/// Дни месяца продаж: кг (с планом и фактом) и, если есть, выручка по дням.
class MonthDays {
  const MonthDays({required this.kg, this.sum});

  final SalesDaily kg;
  final SalesDaily? sum;
}

/// Продажи по неделям из дневных итогов месяцев (кг и выручка) — как у веба. План недели — по темпу: план месяца ÷ дней в месяце за
/// каждый день недели (неполный сегодняшний — его долей). Факт — в охвате плана: дневной кг × (факт с планом ÷ весь факт месяца).
/// Месяц без плана в выполнение недели не входит.
({List<double?> execution, List<double?> revenue}) salesWeeks(List<HomeWeek> weeks, Map<String, MonthDays> byMonth) {
  final execution = <double?>[];
  final revenue = <double?>[];
  for (final w in weeks) {
    var fact = 0.0;
    var plan = 0.0;
    var money = 0.0;
    var days = 0;
    for (var i = 0; i < 7; i++) {
      final date = addDays(w.start, i);
      final y = int.parse(date.substring(0, 4));
      final m = int.parse(date.substring(5, 7));
      final d = int.parse(date.substring(8, 10));
      final data = byMonth['$y-$m'];
      if (data == null || date.compareTo(data.kg.dataThrough) > 0) continue;
      if (d - 1 >= data.kg.days.length) continue;
      final kg = data.kg.days[d - 1];
      if (kg == null) continue;
      days++;
      final sumDays = data.sum?.days;
      money += (sumDays != null && d - 1 < sumDays.length ? sumDays[d - 1] : null) ?? 0;
      final k = data.kg;
      final planKg = k.planKg;
      if (planKg == null || planKg <= 0) continue;
      final share = !k.closed && date == k.today
          ? math.min(1.0, math.max(0.0, (k.elapsedDays ?? k.workedDays.toDouble()) - (k.workedDays - 1)))
          : 1.0;
      final scope = k.planFactKg != null && k.factKg > 0 ? k.planFactKg! / k.factKg : 1.0;
      fact += kg * scope;
      plan += planKg / k.daysInMonth * share;
    }
    execution.add(plan > 0 ? 100 * fact / plan : null);
    revenue.add(days > 0 ? money : null);
  }
  return (execution: execution, revenue: revenue);
}

class HomeTrends {
  const HomeTrends({required this.weeks, required this.currentDays, this.sales, this.revenue, this.attendance});

  final List<HomeWeek> weeks;

  /// Сколько дней текущей недели прошло (пн = 1).
  final int currentDays;
  final List<double?>? sales;
  final List<double?>? revenue;
  final List<double?>? attendance;

  Map<String, Object?> toJson() => {
        'weeks': [for (final w in weeks) {'start': w.start, 'end': w.end, 'week': w.week}],
        'currentDays': currentDays,
        'sales': sales,
        'revenue': revenue,
        'attendance': attendance,
      };

  factory HomeTrends.fromJson(Map<String, Object?> j) {
    List<double?>? list(Object? v) => v == null ? null : [for (final x in v as List<Object?>) (x as num?)?.toDouble()];
    return HomeTrends(
      weeks: [
        for (final w in (j['weeks']! as List<Object?>).cast<Map<String, Object?>>())
          HomeWeek(start: w['start']! as String, end: w['end']! as String, week: w['week']! as int),
      ],
      currentDays: j['currentDays']! as int,
      sales: list(j['sales']),
      revenue: list(j['revenue']),
      attendance: list(j['attendance']),
    );
  }
}

int currentDaysOf(String today) => parseDate(today).difference(parseDate(mondayOf(today))).inDays + 1;

enum SeriesKey { sales, revenue, attendance }

enum SeriesUnit { pct, money }

class TrendSeries {
  const TrendSeries({required this.key, required this.weeks, required this.values, required this.unit, this.target});

  final SeriesKey key;
  final List<int> weeks;
  final List<double?> values;
  final SeriesUnit unit;
  final double? target;
}

/// Недельные ряды: продажи к плану (цель 90%), выручка (без цели), явка (норма 80%). Ряда нет — вкладки нет.
List<TrendSeries> homeSeries(HomeTrends t) {
  final weeks = [for (final w in t.weeks) w.week];
  bool has(List<double?>? v) => v != null && v.any((x) => x != null);
  return [
    if (has(t.sales)) TrendSeries(key: SeriesKey.sales, weeks: weeks, values: t.sales!, unit: SeriesUnit.pct, target: 90),
    if (has(t.revenue)) TrendSeries(key: SeriesKey.revenue, weeks: weeks, values: t.revenue!, unit: SeriesUnit.money),
    if (has(t.attendance)) TrendSeries(key: SeriesKey.attendance, weeks: weeks, values: t.attendance!, unit: SeriesUnit.pct, target: 80),
  ];
}

/// «Круглый» верх шкалы денег: 3,62 млрд → 4 млрд.
double niceCeil(double value) {
  if (value <= 0) return 1;
  final step = math.pow(10, (math.log(value) / math.ln10).floor()).toDouble() / 2;
  return (value / step).ceil() * step;
}

/// Шкала ряда: проценты — от 60 до 100 (шире, если значения или цель выходят), деньги — от нуля до «круглого» максимума.
({double lo, double hi}) trendScale(TrendSeries s) {
  final values = s.values.whereType<double>().toList();
  if (s.unit == SeriesUnit.money) return (lo: 0, hi: niceCeil(values.fold(0.0, math.max)));
  final all = [...values, ?s.target];
  final lo = math.max(0.0, math.min(60.0, (((all.reduce(math.min)) - 5) / 10).floor() * 10.0));
  final hi = math.max(100.0, (((all.reduce(math.max)) + 2) / 10).ceil() * 10.0);
  return (lo: lo, hi: hi);
}

/// Итоги ряда: среднее, лучшая неделя, недель не ниже цели (без цели — сумма за период).
({int count, double? average, double? best, int? above, double total}) trendStats(TrendSeries s) {
  final values = s.values.whereType<double>().toList();
  final total = values.fold(0.0, (a, b) => a + b);
  return (
    count: values.length,
    average: values.isEmpty ? null : total / values.length,
    best: values.isEmpty ? null : values.reduce(math.max),
    above: s.target == null ? null : values.where((v) => v >= s.target!).length,
    total: total,
  );
}

/// Последнее изменение ряда за неделю (последняя неделя с данными к предыдущей).
double? weekChange(List<double?>? values) {
  if (values == null) return null;
  final known = values.whereType<double>().toList();
  return known.length > 1 ? known.last - known[known.length - 2] : null;
}

// ───────────── Отделы ─────────────

class MobileDepartment {
  const MobileDepartment({required this.code, required this.value, required this.trendWeek, required this.attention, required this.overdue});

  /// sales | hr
  final String code;
  final double value;
  final double? trendWeek;
  final bool attention;
  final int overdue;
}

/// Отделы с данными: «Продажи» — выполнение плана месяца, HR — явка с начала месяца. Сначала требующие внимания, внутри — по значению.
List<MobileDepartment> homeDepartments({SalesBrief? sales, HrBrief? hr, WorkOverview? tasks, HomeTrends? trends}) {
  int overdue(String code) => tasks?.departments.where((d) => d.code == code).firstOrNull?.overdue ?? 0;
  final rows = <MobileDepartment>[
    if (sales?.execution != null)
      () {
        final value = 100 * sales!.execution!;
        return MobileDepartment(
          code: 'sales',
          value: value,
          trendWeek: weekChange(trends?.sales),
          attention: salesTone(sales.level, value) != HomeTone.success,
          overdue: overdue('sales'),
        );
      }(),
    if (hr?.monthRate != null)
      MobileDepartment(
        code: 'hr',
        value: hr!.monthRate!,
        trendWeek: weekChange(trends?.attendance),
        attention: hr.monthRate! < 80,
        overdue: overdue('hr'),
      ),
  ];
  rows.sort((a, b) => a.attention == b.attention ? a.value.compareTo(b.value) : (a.attention ? -1 : 1));
  return rows;
}

// ───────────── «Фокус дня» и AI-отчёт ─────────────

enum Severity { critical, warning, info }

class FocusItem {
  const FocusItem({required this.id, required this.severity, required this.category, required this.text, required this.metric, required this.href});

  final String id;
  final Severity severity;
  final String category;
  final String text;
  final ({String value, String caption})? metric;

  /// Путь веба («/sales/regions/…») — переводится в экран приложения.
  final String? href;
}

const _number = r'\d[\d\s  ]*(?:[.,]\d+)?';
final _tail = RegExp('^(.+?)\\s+[—–]\\s+(?:(\\p{L}+)\\s+)?($_number\\s?%)\$', unicode: true);
final _first = RegExp('($_number)(\\s?%|\\s?SKU\\b)?', unicode: true);

String _compact(String s) => s.replaceAll(RegExp(r'[\s  ]+'), ' ').trim();

/// Ключевая цифра находки для плашки: «44%» / «прогноз», «33» / «SKU»; числа нет — null.
({String text, ({String value, String caption})? metric}) metricOf(String text) {
  final tail = _tail.firstMatch(text);
  if (tail != null) {
    return (text: tail.group(1)!, metric: (value: _compact(tail.group(3)!).replaceAll(' %', '%'), caption: tail.group(2) ?? ''));
  }
  final first = _first.firstMatch(text);
  if (first == null) return (text: text, metric: null);
  final unit = first.group(2)?.trim() ?? '';
  return (
    text: text,
    metric: (value: _compact(first.group(1)!) + (unit == '%' ? '%' : ''), caption: unit == 'SKU' ? 'SKU' : ''),
  );
}

Severity severityOf(String s) => switch (s) {
      'Critical' => Severity.critical,
      'Opportunity' => Severity.info,
      _ => Severity.warning,
    };

/// Все находки консультанта строками «Фокуса дня»: сначала критичные, потом важные, потом возможности.
List<FocusItem> focusOf(AiAlerts alerts) {
  final items = [
    for (final a in alerts.alerts)
      () {
        final m = metricOf(a.title);
        return FocusItem(id: a.id, severity: severityOf(a.severity), category: a.category, text: m.text, metric: m.metric, href: a.href);
      }(),
  ];
  // Сортировка устойчивая: при равной важности — порядок сервера.
  final indexed = items.indexed.toList()
    ..sort((a, b) {
      final r = a.$2.severity.index.compareTo(b.$2.severity.index);
      return r != 0 ? r : a.$1.compareTo(b.$1);
    });
  return [for (final e in indexed) e.$2];
}

class WeeklyReport {
  const WeeklyReport({required this.id, required this.risks, required this.recommendations});

  final String id;
  final int risks;
  final int recommendations;
}

/// Еженедельный AI-отчёт — риски и рекомендации из находок консультанта; нечего показать — null.
WeeklyReport? weeklyReportOf(AiAlerts? alerts, DateTime now) {
  if (alerts == null) return null;
  final s = alerts.summary;
  return s.problems + s.recommendations > 0 ? WeeklyReport(id: weekId(now), risks: s.problems, recommendations: s.recommendations) : null;
}

/// Отделы компании — фильтр главной и подписи находок.
const homeDepartmentCodes = ['finance', 'sales', 'marketing', 'hr', 'production', 'supply'];
