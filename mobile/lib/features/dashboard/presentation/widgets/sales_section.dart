import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../app/theme/tokens.dart';
import '../../../../shared/extensions/context_ext.dart';
import '../../../../shared/widgets/detail_sheet.dart';
import '../../../../shared/widgets/kpi_tile.dart';
import '../../data/dashboard_models.dart';
import '../../domain/dashboard_period.dart';
import '../dashboard_providers.dart';
import 'block_section.dart';
import 'charts.dart';

/// Продажи месяца: четыре плитки (выручка, продано, клиенты, выполнение плана) и график по дням. Плитки и график — разные запросы:
/// тяжёлый ряд по дням не держит плитки.
class SalesSection extends ConsumerWidget {
  const SalesSection({super.key, required this.period});

  final DashboardPeriod period;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final overview = ref.watch(salesOverviewBlock(period));
    final daily = ref.watch(salesDailyBlock(period));
    final l10n = context.l10n;
    final o = overview.data;
    void detail() => showSalesDetail(context, o!, daily.data);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        BlockSection<SalesOverview>(
          title: o == null ? l10n.salesTitle : l10n.salesTitleMonth(context.fmt.month(o.period.year, o.period.month)),
          state: overview,
          card: false,
          skeletonHeight: 236,
          moreLabel: l10n.actionDetails,
          onMore: detail,
          onRetry: () => ref.read(salesOverviewBlock(period).notifier).refresh(),
          builder: (context, o) => _KpiGrid(overview: o, daily: daily.data, onTap: detail),
        ),
        if (!overview.forbidden)
          BlockSection<SalesDaily>(
            title: l10n.salesTrendTitle,
            state: daily,
            skeletonHeight: 230,
            onRetry: () => ref.read(salesDailyBlock(period).notifier).refresh(),
            builder: (context, d) => _Trend(daily: d),
          ),
      ],
    );
  }
}

class _KpiGrid extends StatelessWidget {
  const _KpiGrid({required this.overview, required this.daily, required this.onTap});

  final SalesOverview overview;
  final SalesDaily? daily;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final fmt = context.fmt;
    final c = context.ob;
    final k = overview.kpi;
    final through = DateTime.tryParse(overview.period.dataThrough);
    final vs = daily?.vsPrevMonth;
    final perAgent = k.akbPerAgent;
    final planShown = k.execution ?? k.forecastExecution;

    Widget row(Widget a, Widget b) => IntrinsicHeight(
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [Expanded(child: a), const SizedBox(width: ObSpace.md), Expanded(child: b)],
          ),
        );

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        row(
          KpiTile(
            icon: Icons.payments_outlined,
            label: l10n.kpiRevenue,
            value: fmt.money(k.revenue),
            caption: through == null ? null : l10n.kpiThrough(fmt.dayMonth(through)),
            onTap: onTap,
          ),
          KpiTile(
            icon: Icons.inventory_2_outlined,
            label: l10n.kpiSold,
            value: fmt.weight(k.factKg),
            footer: vs == null ? null : DeltaBadge(ratio: vs, label: l10n.vsSameDays),
            caption: k.forecastKg == null ? null : l10n.kpiForecast(fmt.weight(k.forecastKg!)),
            onTap: onTap,
          ),
        ),
        const SizedBox(height: ObSpace.md),
        row(
          KpiTile(
            icon: Icons.storefront_outlined,
            label: l10n.kpiClients,
            value: fmt.count(k.akb),
            caption: perAgent?.value == null
                ? null
                : l10n.kpiPerAgent(fmt.decimal(perAgent!.value!), fmt.count(perAgent.target)),
            onTap: onTap,
          ),
          KpiTile(
            icon: Icons.flag_outlined,
            label: l10n.kpiPlan,
            accent: c.level(k.executionLevel),
            value: k.execution != null ? fmt.percent(k.execution!) : (planShown != null ? fmt.percent(planShown) : '—'),
            caption: k.execution != null
                ? (k.forecastExecution == null ? null : l10n.kpiForecast(fmt.percent(k.forecastExecution!)))
                : (k.planKg == null ? l10n.kpiNoPlan : null),
            onTap: onTap,
          ),
        ),
      ],
    );
  }
}

class _Trend extends StatelessWidget {
  const _Trend({required this.daily});

  final SalesDaily daily;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final fmt = context.fmt;
    final vs = daily.vsPrevMonth;
    final hasToday = !daily.closed && daily.today != null && daily.today == daily.dataThrough;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(l10n.salesTrendCaption(fmt.month(daily.year, daily.month), fmt.weight(daily.total)), style: context.text.bodyMedium),
        if (vs != null) ...[
          const SizedBox(height: 2),
          DeltaBadge(ratio: vs, label: l10n.vsSameDays),
        ],
        const SizedBox(height: ObSpace.lg),
        if (daily.total <= 0)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: ObSpace.xl),
            child: Text(l10n.salesTrendEmpty, style: context.text.bodyMedium),
          )
        else
          Semantics(
            label: l10n.salesTrendSemantics(fmt.month(daily.year, daily.month), fmt.weight(daily.total)),
            child: SalesDailyChart(daily: daily),
          ),
        if (hasToday) ...[
          const SizedBox(height: ObSpace.sm),
          Text(l10n.salesTrendToday, style: context.text.bodySmall),
        ],
      ],
    );
  }
}

/// Детализация продаж: все показатели месяца с целями и уровнями сервера и таблица по дням (то же, что на графике).
void showSalesDetail(BuildContext context, SalesOverview o, SalesDaily? daily) {
  final l10n = context.l10n;
  final fmt = context.fmt;
  final k = o.kpi;
  final c = context.ob;
  final through = DateTime.tryParse(o.period.dataThrough);

  showDetailSheet(
    context,
    title: l10n.salesTitleMonth(fmt.monthYear(o.period.year, o.period.month)),
    subtitle: through == null ? null : l10n.kpiThrough(fmt.dayMonth(through)),
    children: (context) => [
      DetailGroup(
        title: l10n.detailVolume,
        children: [
          DetailRow(label: l10n.kpiSold, value: fmt.weight(k.factKg)),
          DetailRow(label: l10n.rowPlan, value: k.planKg == null ? l10n.kpiNoPlan : fmt.weight(k.planKg!)),
          if (k.execution != null)
            DetailRow(label: l10n.rowExecution, value: fmt.percent(k.execution!), valueColor: c.level(k.executionLevel)),
          if (k.forecastKg != null)
            DetailRow(
              label: l10n.rowForecast,
              value: fmt.weight(k.forecastKg!),
              hint: k.forecastExecution == null ? null : l10n.rowForecastPlan(fmt.percent(k.forecastExecution!)),
            ),
          if (k.pace?.paceKg != null) DetailRow(label: l10n.rowPace, value: fmt.weight(k.pace!.paceKg!)),
          if (k.pace?.requiredKg != null)
            DetailRow(label: l10n.rowRequired, value: fmt.weight(k.pace!.requiredKg!), valueColor: c.level(k.pace!.level)),
          if (daily?.vsPrevMonth != null)
            DetailRow(label: l10n.vsSameDaysTitle, value: fmt.signedPercent(daily!.vsPrevMonth!)),
        ],
      ),
      DetailGroup(
        title: l10n.detailMoney,
        children: [
          DetailRow(label: l10n.kpiRevenue, value: fmt.money(k.revenue)),
          if (k.revenuePerOutlet != null) _targetRow(context, l10n.rowRevenuePerOutlet, k.revenuePerOutlet!, fmt.money),
        ],
      ),
      DetailGroup(
        title: l10n.detailClients,
        children: [
          DetailRow(label: l10n.kpiClients, value: fmt.count(k.akb)),
          DetailRow(label: l10n.rowAgents, value: fmt.count(k.activeAgents)),
          if (k.akbPerAgent != null) _targetRow(context, l10n.rowAkbPerAgent, k.akbPerAgent!, fmt.decimal),
          DetailRow(label: l10n.rowVisits, value: fmt.count(k.visitsDone)),
          DetailRow(label: l10n.rowVisitsNoOrder, value: fmt.count(k.visitsWithoutOrder)),
          if (k.conversion != null) _targetRow(context, l10n.rowConversion, k.conversion!, fmt.percent),
          DetailRow(
            label: l10n.rowFlags,
            value: '${o.flags.critical + o.flags.risk}',
            hint: l10n.rowFlagsHint(o.flags.critical, o.flags.risk),
          ),
        ],
      ),
      if (daily != null && daily.total > 0)
        DetailGroup(
          title: l10n.detailByDay,
          children: [
            for (var i = daily.days.length - 1; i >= 0; i--)
              if (daily.days[i] != null)
                DetailRow(label: fmt.dayMonth(DateTime(daily.year, daily.month, i + 1)), value: fmt.weight(daily.days[i]!)),
          ],
        ),
    ],
  );
}

/// Показатель с целью: значение (цветом уровня с сервера) и «цель …, N% от цели».
Widget _targetRow(BuildContext context, String label, TargetValue t, String Function(double) format) {
  final l10n = context.l10n;
  final fmt = context.fmt;
  final c = context.ob;
  return DetailRow(
    label: label,
    value: t.value == null ? '—' : format(t.value!),
    valueColor: c.level(t.level),
    hint: [
      l10n.rowTarget(format(t.target)),
      if (t.ratio != null) l10n.rowOfTarget(fmt.percent(t.ratio!)),
      if (t.forecast != null) l10n.kpiForecast(format(t.forecast!)),
    ].join(' · '),
  );
}
