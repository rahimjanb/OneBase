import 'package:freezed_annotation/freezed_annotation.dart';

part 'dashboard_models.freezed.dart';
part 'dashboard_models.g.dart';

// Ответы OneBase API для Dashboard — только нужные экрану поля. Названия полей — как в JSON сервера (camelCase).
// Значения не пересчитываются на телефоне: выполнение, прогноз, уровни цвета и сравнения считает сервер.

// ───────────── Продажи: /api/sales/overview ─────────────

@freezed
abstract class SalesPeriod with _$SalesPeriod {
  const factory SalesPeriod({
    required int year,
    required int month,

    /// Последний день с данными («2026-11-12»).
    required String dataThrough,
    String? today,
    @Default(0) int workedDays,
    double? elapsedDays,
    required int daysInMonth,
    @Default(false) bool closed,
    String? previousCutoff,
  }) = _SalesPeriod;

  factory SalesPeriod.fromJson(Map<String, Object?> json) => _$SalesPeriodFromJson(json);
}

/// Показатель с целью: значение, цель, доля от цели и цвет уровня (Good / Warning / Bad) — с сервера.
@freezed
abstract class TargetValue with _$TargetValue {
  const factory TargetValue({
    double? value,
    @Default(0) double target,
    double? ratio,
    String? level,
    double? forecast,
  }) = _TargetValue;

  factory TargetValue.fromJson(Map<String, Object?> json) => _$TargetValueFromJson(json);
}

/// Темп месяца: сколько в день продаём и сколько нужно до плана.
@freezed
abstract class SalesPace with _$SalesPace {
  const factory SalesPace({
    double? expected,
    double? paceKg,
    double? requiredKg,
    String? level,
  }) = _SalesPace;

  factory SalesPace.fromJson(Map<String, Object?> json) => _$SalesPaceFromJson(json);
}

@freezed
abstract class SalesKpi with _$SalesKpi {
  const factory SalesKpi({
    @Default(0) double factKg,
    double? planKg,
    double? execution,
    double? forecastKg,
    double? forecastExecution,
    @Default(0) double revenue,

    /// АКБ — точки, которые купили в месяце (клиенты).
    @Default(0) int akb,
    TargetValue? conversion,
    TargetValue? revenuePerOutlet,
    TargetValue? akbPerAgent,
    @Default(0) int visitsWithoutOrder,
    @Default(0) int visitsDone,
    @Default(0) int activeAgents,

    /// Откуда план: rop | factory | linko.
    String? planSource,
    String? executionLevel,
    SalesPace? pace,

    /// Факт в охвате плана (кг): у недель продаж факт приводится к нему, чтобы недели сходились с выполнением месяца.
    double? planFactKg,
  }) = _SalesKpi;

  factory SalesKpi.fromJson(Map<String, Object?> json) => _$SalesKpiFromJson(json);
}

@freezed
abstract class FlagCounts with _$FlagCounts {
  const factory FlagCounts({@Default(0) int critical, @Default(0) int risk}) = _FlagCounts;

  factory FlagCounts.fromJson(Map<String, Object?> json) => _$FlagCountsFromJson(json);
}

@freezed
abstract class SalesOverview with _$SalesOverview {
  const factory SalesOverview({
    required SalesPeriod period,
    required SalesKpi kpi,
    @Default(0) int activeAgents,
    @Default(FlagCounts()) FlagCounts flags,
  }) = _SalesOverview;

  factory SalesOverview.fromJson(Map<String, Object?> json) => _$SalesOverviewFromJson(json);
}

/// Месяц, за который у продаж есть данные (`/api/sales/months`).
@freezed
abstract class SalesMonth with _$SalesMonth {
  const factory SalesMonth({required int year, required int month}) = _SalesMonth;

  factory SalesMonth.fromJson(Map<String, Object?> json) => _$SalesMonthFromJson(json);
}

// ───────────── Продажи по дням: срез /api/sales/republic ─────────────

/// Кг по дням месяца и «те же дни прошлого месяца» — срез большого ответа `republic` (234 КБ), вырезанный в фоновом изоляте.
@freezed
abstract class SalesDaily with _$SalesDaily {
  const factory SalesDaily({
    required int year,
    required int month,
    required String dataThrough,
    String? today,
    required int daysInMonth,
    @Default(false) bool closed,

    /// Кг за каждый день месяца; null — дня ещё нет или данных нет.
    required List<double?> days,
    @Default(<bool>[]) List<bool> sundays,
    @Default(0) double total,

    /// Сумма строк «те же дни» по регионам: сейчас и в прошлом месяце (по ту же дату).
    @Default(0) double sameNowKg,
    @Default(0) double sameBeforeKg,
    String? previousCutoff,

    /// План и факт месяца (кг) и прошедшие дни — для недель «Динамики» главной.
    double? planKg,
    double? planFactKg,
    @Default(0) double factKg,
    @Default(0) int workedDays,
    double? elapsedDays,
  }) = _SalesDaily;

  const SalesDaily._();

  factory SalesDaily.fromJson(Map<String, Object?> json) => _$SalesDailyFromJson(json);

  /// К тем же дням прошлого месяца: +0,04 = +4 %. Нет прошлого — null.
  double? get vsPrevMonth => sameBeforeKg > 0 ? sameNowKg / sameBeforeKg - 1 : null;
}

/// Вырезать из ответа `/api/sales/republic` ряд по дням. Функция верхнего уровня — выполняется в фоновом изоляте.
SalesDaily salesDailyOf(Object? json) {
  final root = json! as Map<String, Object?>;
  final period = root['period']! as Map<String, Object?>;
  final calendar = root['calendar']! as Map<String, Object?>;
  final kpi = root['kpi'] as Map<String, Object?>? ?? const {};
  final same = (root['sameDays'] as List<Object?>? ?? const []).cast<Map<String, Object?>>();
  double sum(String key) => same.fold(0, (a, r) => a + ((r[key] as num?)?.toDouble() ?? 0));
  return SalesDaily(
    year: period['year']! as int,
    month: period['month']! as int,
    dataThrough: period['dataThrough']! as String,
    today: period['today'] as String?,
    daysInMonth: period['daysInMonth']! as int,
    closed: period['closed'] as bool? ?? false,
    previousCutoff: period['previousCutoff'] as String?,
    days: [for (final d in calendar['totalDays']! as List<Object?>) (d as num?)?.toDouble()],
    sundays: [for (final s in calendar['sundays'] as List<Object?>? ?? const []) s == true],
    total: (calendar['total'] as num?)?.toDouble() ?? 0,
    sameNowKg: sum('kgNow'),
    sameBeforeKg: sum('kgBefore'),
    planKg: (kpi['planKg'] as num?)?.toDouble(),
    planFactKg: (kpi['planFactKg'] as num?)?.toDouble(),
    factKg: (kpi['factKg'] as num?)?.toDouble() ?? 0,
    workedDays: period['workedDays'] as int? ?? 0,
    elapsedDays: (period['elapsedDays'] as num?)?.toDouble(),
  );
}

// ───────────── Финансы (1С): /api/finance/overview ─────────────

@freezed
abstract class FinanceMeta with _$FinanceMeta {
  const factory FinanceMeta({
    @Default(false) bool hasData,
    @Default(false) bool demo,

    /// Пояснение сервера, если данных нет («Данных 1С в OneBase пока нет: …»).
    String? note,
    String? dataThrough,
    String? syncedAt,
    String? organizationName,
    required String from,
    required String to,
  }) = _FinanceMeta;

  factory FinanceMeta.fromJson(Map<String, Object?> json) => _$FinanceMetaFromJson(json);
}

@freezed
abstract class PnlValue with _$PnlValue {
  const factory PnlValue({
    required String code,
    required String title,

    /// income | expense | total
    required String kind,
    @Default(0) double value,
    double? previous,
  }) = _PnlValue;

  factory PnlValue.fromJson(Map<String, Object?> json) => _$PnlValueFromJson(json);
}

@freezed
abstract class MonthPoint with _$MonthPoint {
  const factory MonthPoint({
    /// Первое число месяца: «2026-11-01».
    required String month,
    @Default(0) double revenue,

    /// Расходы месяца — себестоимость, расходы периода и финансовые (считает сервер).
    @Default(0) double expenses,
    @Default(0) double netProfit,
  }) = _MonthPoint;

  factory MonthPoint.fromJson(Map<String, Object?> json) => _$MonthPointFromJson(json);
}

@freezed
abstract class FinanceOverview with _$FinanceOverview {
  const factory FinanceOverview({
    required FinanceMeta meta,
    @Default(<PnlValue>[]) List<PnlValue> pnl,
    @Default(<MonthPoint>[]) List<MonthPoint> months,
    @Default(0) double cashTotal,
    @Default(0) double receivables,
    @Default(0) double payables,
  }) = _FinanceOverview;

  const FinanceOverview._();

  factory FinanceOverview.fromJson(Map<String, Object?> json) => _$FinanceOverviewFromJson(json);

  PnlValue? line(String code) => pnl.where((p) => p.code == code).firstOrNull;

  /// Расходы выбранного месяца — точка ряда за последний месяц периода.
  MonthPoint? get lastMonth => months.isEmpty ? null : months.last;
}

/// Организация 1С — фильтр финансовых блоков.
@freezed
abstract class FinanceOrganization with _$FinanceOrganization {
  const factory FinanceOrganization({required String id, required String name}) = _FinanceOrganization;

  factory FinanceOrganization.fromJson(Map<String, Object?> json) => _$FinanceOrganizationFromJson(json);
}

// ───────────── HR: /api/hr/overview ─────────────

@freezed
abstract class HrDivision with _$HrDivision {
  const factory HrDivision({
    @JsonKey(fromJson: _string) required String id,
    required String name,
    @Default(0) int active,
    @Default(0) int scheduled,
    @Default(0) int present,
    @Default(0) int late,
    @Default(0) int absent,
  }) = _HrDivision;

  factory HrDivision.fromJson(Map<String, Object?> json) => _$HrDivisionFromJson(json);
}

@freezed
abstract class HrCompare with _$HrCompare {
  const factory HrCompare({String? date, double? presentPct}) = _HrCompare;

  factory HrCompare.fromJson(Map<String, Object?> json) => _$HrCompareFromJson(json);
}

@freezed
abstract class HrSync with _$HrSync {
  const factory HrSync({String? lastSuccessAt, String? lastStatus}) = _HrSync;

  factory HrSync.fromJson(Map<String, Object?> json) => _$HrSyncFromJson(json);
}

/// Явка дня по Verifix.
@freezed
abstract class HrOverview with _$HrOverview {
  const factory HrOverview({
    required String date,
    @Default(0) int active,
    @Default(0) int scheduled,
    @Default(0) int present,
    @Default(0) int late,
    @Default(0) int absent,
    @Default(0) int onLeave,
    HrCompare? compare,
    @Default(<HrDivision>[]) List<HrDivision> divisions,
    HrSync? sync,
  }) = _HrOverview;

  const HrOverview._();

  factory HrOverview.fromJson(Map<String, Object?> json) => _$HrOverviewFromJson(json);

  /// Явка: на месте от «по графику», 0–1; графика нет — null.
  double? get presentShare => scheduled > 0 ? present / scheduled : null;
}

// ───────────── Задачи: /api/work/overview ─────────────

@freezed
abstract class WorkDepartment with _$WorkDepartment {
  const factory WorkDepartment({
    required String code,
    required String name,
    @Default(0) int open,
    @Default(0) int overdue,
    @Default(0) int review,
    @Default(0) int blocked,
  }) = _WorkDepartment;

  factory WorkDepartment.fromJson(Map<String, Object?> json) => _$WorkDepartmentFromJson(json);
}

@freezed
abstract class WorkTaskBrief with _$WorkTaskBrief {
  const factory WorkTaskBrief({
    required String id,
    required String title,

    /// New | InProgress | Review | Done | Cancelled
    required String status,

    /// Low | Medium | High | Urgent
    @Default('Medium') String priority,
    String? dueDate,
    @Default(false) bool overdue,
    String? departmentName,
    String? assigneeName,
  }) = _WorkTaskBrief;

  factory WorkTaskBrief.fromJson(Map<String, Object?> json) => _$WorkTaskBriefFromJson(json);
}

@freezed
abstract class WorkOverview with _$WorkOverview {
  const factory WorkOverview({
    @Default(0) int open,
    @Default(0) int inProgress,
    @Default(0) int review,
    @Default(0) int overdue,
    @Default(0) int blocked,
    @Default(0) int dueThisWeek,
    @Default(0) int doneThisMonth,
    @Default(0) int myOpen,
    @Default(0) int myOverdue,
    @Default(0) int awaitingMyReview,
    @Default(<WorkDepartment>[]) List<WorkDepartment> departments,
    @Default(<WorkTaskBrief>[]) List<WorkTaskBrief> myUpcoming,
  }) = _WorkOverview;

  factory WorkOverview.fromJson(Map<String, Object?> json) => _$WorkOverviewFromJson(json);
}

// ───────────── События: /api/notifications ─────────────

@freezed
abstract class NotificationItem with _$NotificationItem {
  const factory NotificationItem({
    required String id,

    /// tasks | hr | ai | system …
    String? category,
    String? kind,
    required String title,
    String? body,
    required String createdAt,
    @Default(false) bool read,

    /// info | warning | critical
    String? severity,

    /// Вид события («task_assigned» …) и ссылка — путь веба.
    String? type,
    String? href,
  }) = _NotificationItem;

  factory NotificationItem.fromJson(Map<String, Object?> json) => _$NotificationItemFromJson(json);
}

@freezed
abstract class NotificationPage with _$NotificationPage {
  const factory NotificationPage({
    @Default(<NotificationItem>[]) List<NotificationItem> items,
    @Default(0) int unread,
  }) = _NotificationPage;

  factory NotificationPage.fromJson(Map<String, Object?> json) => _$NotificationPageFromJson(json);
}

// ───────────── AI-находки: /api/ai/alerts ─────────────

@freezed
abstract class AlertsSummary with _$AlertsSummary {
  const factory AlertsSummary({
    @Default(0) int problems,
    @Default(0) int critical,
    @Default(0) int recommendations,
    @Default(0) int opportunities,
  }) = _AlertsSummary;

  factory AlertsSummary.fromJson(Map<String, Object?> json) => _$AlertsSummaryFromJson(json);
}

@freezed
abstract class AlertsRun with _$AlertsRun {
  const factory AlertsRun({String? at}) = _AlertsRun;

  factory AlertsRun.fromJson(Map<String, Object?> json) => _$AlertsRunFromJson(json);
}

@freezed
abstract class AiAlert with _$AiAlert {
  const factory AiAlert({
    required String id,

    /// sales | finance | hr | production | supply | marketing
    required String category,

    /// Critical | Warning | Opportunity
    required String severity,
    required String title,
    String? message,
    String? recommendation,
    String? detectedAt,

    /// Правило, которое нашло проблему («sales.region.drop»).
    String? rule,

    /// Куда ведёт находка — путь веба («/sales/regions/…»), приложение переводит его в свой экран.
    String? href,
  }) = _AiAlert;

  factory AiAlert.fromJson(Map<String, Object?> json) => _$AiAlertFromJson(json);
}

@freezed
abstract class AiAlerts with _$AiAlerts {
  const factory AiAlerts({
    @Default(AlertsSummary()) AlertsSummary summary,
    AlertsRun? lastRun,
    @Default(<AiAlert>[]) List<AiAlert> alerts,
  }) = _AiAlerts;

  factory AiAlerts.fromJson(Map<String, Object?> json) => _$AiAlertsFromJson(json);
}

String _string(Object? v) => '$v';
