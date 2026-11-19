import '../../../../core/localization/formatters.dart';
import '../../../../l10n/gen/app_localizations.dart';
import '../../domain/home_view.dart';
import '../dashboard_providers.dart';

// Подписи главной на языке интерфейса: доменные значения (виды показателей, отделы, периоды) → текст.

String periodLabel(AppLocalizations l10n, HomePeriod p) => switch (p) {
      HomePeriod.d7 => l10n.filterPeriod7d,
      HomePeriod.d30 => l10n.filterPeriod30d,
      HomePeriod.quarter => l10n.filterPeriodQuarter,
      HomePeriod.year => l10n.filterPeriodYear,
    };

String categoryName(AppLocalizations l10n, String code) => switch (code) {
      'finance' => l10n.catFinance,
      'sales' => l10n.catSales,
      'marketing' => l10n.catMarketing,
      'hr' => l10n.catHr,
      'production' => l10n.catProduction,
      'supply' => l10n.catSupply,
      _ => code,
    };

String heroLabel(AppLocalizations l10n, HeroKind k) => k == HeroKind.sales ? l10n.heroSalesPlan : l10n.homeAttendanceToday;

String statLabel(AppLocalizations l10n, StatKind k) => switch (k) {
      StatKind.forecast => l10n.statForecast,
      StatKind.attendance => l10n.homeAttendanceToday,
      StatKind.tasks => l10n.navTasks,
      StatKind.attendanceMonth => l10n.statAttendanceMonth,
      StatKind.revenue => l10n.dynRevenue,
    };

String? statSub(AppLocalizations l10n, Fmt fmt, HomeStat s) => switch (s.sub) {
      null => null,
      StatSubKind.toMonthEnd => l10n.statToMonthEnd,
      StatSubKind.pp => '${fmt.signed(s.subValue, 1)} ${l10n.unitPp}',
      StatSubKind.overdue => l10n.statOverdueN(s.subValue.round()),
      StatSubKind.dueWeek => l10n.statDueWeekN(s.subValue.round()),
      StatSubKind.perMonth => l10n.statPerMonth,
    };

String taskSliceLabel(AppLocalizations l10n, TaskSlice s) => switch (s) {
      TaskSlice.done => l10n.tasksDone,
      TaskSlice.progress => l10n.tasksInWork,
      TaskSlice.review => l10n.tasksOnReview,
    };

String departmentMetric(AppLocalizations l10n, String code) => code == 'sales' ? l10n.deptSalesMetric : l10n.deptHrMetric;

String seriesLabel(AppLocalizations l10n, SeriesKey k) => switch (k) {
      SeriesKey.sales => l10n.dynSales,
      SeriesKey.revenue => l10n.dynRevenue,
      SeriesKey.attendance => l10n.dynAttendance,
    };

String severityLabel(AppLocalizations l10n, Severity s) => switch (s) {
      Severity.critical => l10n.severityCritical,
      Severity.warning => l10n.severityWarning,
      Severity.info => l10n.severityOpportunity,
    };

/// Экран приложения для показателя главной.
String homeTargetRoute(HomeTarget t) => switch (t) {
      HomeTarget.sales => '/analytics/sales',
      HomeTarget.hr => '/home/hr',
      HomeTarget.hrReports => '/home/hr?tab=reports',
      HomeTarget.tasks => '/tasks',
      HomeTarget.tasksOverdue => '/tasks?status=overdue',
      HomeTarget.tasksDone => '/tasks?status=Done',
      HomeTarget.tasksProgress => '/tasks?status=New,InProgress',
      HomeTarget.tasksReview => '/tasks?status=Review',
    };
