import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/cache/cached_block.dart';
import '../../../core/navigation/web_links.dart';
import '../../../shared/extensions/context_ext.dart';
import '../../../shared/widgets/detail_sheet.dart';
import '../../../shared/widgets/ob_app_bar.dart';
import '../../../shared/widgets/ob_card.dart';
import '../../../shared/widgets/pills.dart';
import '../../../shared/widgets/pressable.dart';
import '../../../shared/widgets/section_header.dart';
import '../../../shared/widgets/segmented.dart';
import '../../../shared/widgets/skeleton.dart';
import '../../../shared/widgets/states.dart';
import '../../dashboard/data/dashboard_models.dart';
import '../../dashboard/presentation/dashboard_providers.dart';
import '../data/analytik_models.dart';
import 'analytics_providers.dart';
import 'widgets/sales_widgets.dart';

/// «Аналитика»: продажи (выполнение плана и показатели, как «Вторичка» веба; РМ/СВ/ТП — свой охват), сравнение периодов по модулям
/// (сервер считает кодом, без AI), финансы 1С, производство, находки Аналитика. Переходы — вглубь до региона, ТП и точки.
class AnalyticsScreen extends ConsumerWidget {
  const AnalyticsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final a = ref.watch(analyticsAccessProvider);
    final month = ref.watch(salesMonthProvider);
    return ObPage(
      title: l10n.navAnalytics,
      actions: [if (a.sales) const MonthPill()],
      onRefresh: () async {
        await Future.wait([
          if (a.salesOrg) ref.read(overviewBlock(month).notifier).refresh(),
          for (final m in a.compareModules) ref.read(compareBlock((module: m, period: ref.read(_periodProvider))).notifier).refresh(),
          if (a.finance) ref.read(homeFinanceBlock.notifier).refresh(),
          if (a.production) ref.read(technologistBlock.notifier).refresh(),
        ]);
      },
      slivers: [
        SliverList.list(
          children: [
            if (a.salesOrg) const _OrgSales() else if (a.sales) const _ScopedSales(),
            if (a.compareModules.isNotEmpty) ...[const SizedBox(height: ObSpace.block), const _CompareSection()],
            if (a.finance) ...[const SizedBox(height: ObSpace.block), const _FinanceSection()],
            if (a.production) ...[const SizedBox(height: ObSpace.block), const _ProductionSection()],
            if (a.analytik) ...[const SizedBox(height: ObSpace.block), const _FindingsSection()],
            if (!a.sales && !a.finance && !a.production && !a.analytik)
              EmptyView(icon: LucideIcons.chartNoAxesColumn, title: l10n.stateEmpty),
          ],
        ),
      ],
    );
  }
}

/// Месяц продаж: «Ноябрь 2026» — список месяцев с данными.
class MonthPill extends ConsumerWidget {
  const MonthPill({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final fmt = context.fmt;
    final month = ref.watch(salesMonthProvider);
    final months = ref.watch(salesMonthsBlock).data ?? const <SalesMonth>[];
    final shown = month ?? months.firstOrNull;
    return Padding(
      padding: const EdgeInsets.only(right: 4),
      child: FilterPill(
        icon: LucideIcons.calendar,
        label: shown == null ? l10n.anMonthPick : fmt.monthYear(shown.year, shown.month),
        onTap: () => showObSheet<void>(
          context,
          title: l10n.anMonthPick,
          builder: (context) => Column(
            children: [
              for (final m in months.take(18))
                Pressable(
                  onTap: () {
                    ref.read(salesMonthProvider.notifier).set(m == months.first ? null : m);
                    Navigator.pop(context);
                  },
                  child: Container(
                    height: 48,
                    alignment: Alignment.centerLeft,
                    padding: const EdgeInsets.symmetric(horizontal: 12),
                    decoration: BoxDecoration(
                      color: m == shown ? context.ob.accentSoft : null,
                      borderRadius: BorderRadius.circular(ObRadius.md),
                    ),
                    child: Text(
                      fmt.monthYear(m.year, m.month),
                      style: TextStyle(fontSize: 15, fontWeight: m == shown ? FontWeight.w600 : FontWeight.w400, color: m == shown ? context.ob.accentStrong : context.ob.ink),
                    ),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Показать блок: заготовка, ошибка с «Повторить», данные.
Widget blockView<T>(BuildContext context, BlockState<T> s, Widget Function(T) builder, {required VoidCallback onRetry, double skeleton = 160}) {
  if (s.data != null) return builder(s.data as T);
  if (s.isInitialLoading || (s.loading && s.failure == null)) {
    return ObCard(child: SkeletonGroup(child: Skeleton(height: skeleton - 36)));
  }
  if (s.forbidden) return const SizedBox.shrink();
  if (s.failure != null) return ObCard(child: ErrorView(failure: s.failure!, onRetry: onRetry, compact: true));
  return const SizedBox.shrink();
}

class _OrgSales extends ConsumerWidget {
  const _OrgSales();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final c = context.ob;
    final month = ref.watch(salesMonthProvider);
    final s = ref.watch(overviewBlock(month));
    return blockView(
      context,
      s,
      skeleton: 320,
      onRetry: () => ref.read(overviewBlock(month).notifier).refresh(),
      (o) {
        final bad = o.flags.critical + o.flags.risk;
        final normal = (o.activeAgents - bad).clamp(0, 1 << 30);
        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            SectionHeader(title: l10n.anSales, onMore: () => context.go('/analytics/sales'), moreLabel: l10n.anRepublic),
            const SizedBox(height: 12),
            PlanCard(kpi: o.kpi, period: o.period),
            const SizedBox(height: 12),
            KpiGrid(kpi: o.kpi, onAgents: () => context.go('/analytics/sales/agents')),
            const SizedBox(height: 12),
            ObCard(
              onTap: () => context.go('/analytics/sales/agents'),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(l10n.anAgentsAttention(bad, o.activeAgents), style: TextStyle(fontSize: 15, fontWeight: FontWeight.w600, color: c.ink)),
                      ),
                      Icon(LucideIcons.chevronRight, size: 16, color: c.subtle),
                    ],
                  ),
                  const SizedBox(height: 10),
                  ClipRRect(
                    borderRadius: BorderRadius.circular(4),
                    child: SizedBox(
                      height: 8,
                      child: Row(
                        children: [
                          if (o.flags.critical > 0) Expanded(flex: o.flags.critical, child: ColoredBox(color: c.bad)),
                          if (o.flags.risk > 0) Expanded(flex: o.flags.risk, child: ColoredBox(color: c.warn)),
                          if (normal > 0) Expanded(flex: normal, child: ColoredBox(color: c.ok)),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: 10),
                  Wrap(spacing: 12, children: [
                    DotLabel(color: c.bad, label: '${l10n.anCritical} ${o.flags.critical}'),
                    DotLabel(color: c.warn, label: '${l10n.anRisk} ${o.flags.risk}'),
                    DotLabel(color: c.ok, label: '${l10n.anNormal} $normal'),
                  ]),
                ],
              ),
            ),
          ],
        );
      },
    );
  }
}

/// РМ, СВ, ТП: продажи своего охвата — их домашняя страница (республика и обзор им закрыты).
class _ScopedSales extends ConsumerWidget {
  const _ScopedSales();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final a = ref.watch(analyticsAccessProvider);
    final route = salesHomeRoute(a.salesHome);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(title: l10n.anSales),
        const SizedBox(height: 12),
        ObCard(
          onTap: () => context.go(route),
          child: Row(
            children: [
              const IconTile(icon: LucideIcons.chartNoAxesColumn, tone: Tone.accent),
              const SizedBox(width: 12),
              Expanded(
                child: Text(
                  route.contains('/agents/') ? l10n.anAgents : (route.contains('/regions/') ? l10n.anRegions : l10n.anMyRegions),
                  style: TextStyle(fontSize: 15, fontWeight: FontWeight.w600, color: context.ob.ink),
                ),
              ),
              Icon(LucideIcons.chevronRight, size: 16, color: context.ob.subtle),
            ],
          ),
        ),
        const SizedBox(height: 10),
        ObCard(
          onTap: () => context.go('/analytics/sales/agents'),
          child: Row(
            children: [
              const IconTile(icon: LucideIcons.users, tone: Tone.violet),
              const SizedBox(width: 12),
              Expanded(child: Text(l10n.anAgents, style: TextStyle(fontSize: 15, fontWeight: FontWeight.w600, color: context.ob.ink))),
              Icon(LucideIcons.chevronRight, size: 16, color: context.ob.subtle),
            ],
          ),
        ),
      ],
    );
  }
}

class _PeriodController extends Notifier<String> {
  @override
  String build() => 'month';

  void set(String p) => state = p;
}

final _periodProvider = NotifierProvider<_PeriodController, String>(_PeriodController.new);
final _moduleProvider = NotifierProvider<_ModuleController, String?>(_ModuleController.new);

class _ModuleController extends Notifier<String?> {
  @override
  String? build() => null;

  void set(String m) => state = m;
}

/// Показатели, где рост — плохо (возвраты, брак, опоздания, прогулы, просрочки).
bool kpiGoodWhenDown(String code) =>
    const ['sales.returns', 'production.defect_rate', 'production.overconsumption', 'production.overdue_orders', 'hr.late', 'hr.late_minutes', 'hr.absent', 'hr.absent_today', 'hr.early', 'work.overdue']
        .contains(code);

/// «Сравнение периодов»: модуль (продажи, финансы, производство, персонал) и период (неделя, месяц, квартал, год) — показатели к
/// прошлому периоду, неполные данные — пометкой сервера.
class _CompareSection extends ConsumerWidget {
  const _CompareSection();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final c = context.ob;
    final fmt = context.fmt;
    final a = ref.watch(analyticsAccessProvider);
    final modules = a.compareModules;
    final module = ref.watch(_moduleProvider) ?? modules.first;
    final period = ref.watch(_periodProvider);
    final s = ref.watch(compareBlock((module: module, period: period)));
    String moduleLabel(String m) => switch (m) {
          'sales' => l10n.anModSales,
          'finance' => l10n.anModFinance,
          'production' => l10n.anModProduction,
          _ => l10n.anModHr,
        };
    String value(AnalytikKpi k, double? v) {
      if (v == null) return l10n.anNoData;
      return switch (k.unit) {
        'сум' => '${fmt.compact(v)} ${l10n.unitSum}',
        'кг' => fmt.weight(v),
        '%' => '${fmt.decimal(v)}%',
        _ => '${fmt.decimal(v)} ${k.unit}'.trim(),
      };
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(title: l10n.anCompare),
        const SizedBox(height: 12),
        if (modules.length > 1) ...[
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: Row(
              children: [
                for (final m in modules) ...[
                  FilterPill(label: moduleLabel(m), active: m == module, chevron: false, onTap: () => ref.read(_moduleProvider.notifier).set(m)),
                  const SizedBox(width: 8),
                ],
              ],
            ),
          ),
          const SizedBox(height: 10),
        ],
        ObSegmented<String>(
          values: const ['week', 'month', 'quarter', 'year'],
          selected: period,
          label: (p) => switch (p) {
            'week' => l10n.anPeriodWeek,
            'month' => l10n.anPeriodMonth,
            'quarter' => l10n.anPeriodQuarter,
            _ => l10n.anPeriodYear,
          },
          onChanged: (p) => ref.read(_periodProvider.notifier).set(p),
        ),
        const SizedBox(height: 12),
        if (s.data == null && s.loading)
          ObCard(
            child: Row(
              children: [
                const SizedBox.square(dimension: 18, child: CircularProgressIndicator(strokeWidth: 2)),
                const SizedBox(width: 12),
                Expanded(child: Text(l10n.anSlow, style: TextStyle(fontSize: 13, color: c.ink3))),
              ],
            ),
          )
        else
          blockView(
            context,
            s,
            onRetry: () => ref.read(compareBlock((module: module, period: period)).notifier).refresh(),
            (b) => ObCard(
              padding: EdgeInsets.zero,
              clip: true,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Padding(
                    padding: const EdgeInsets.fromLTRB(16, 14, 16, 6),
                    child: Text('${b.period.label}${b.period.previousLabel.isEmpty ? '' : ' · ${l10n.anCompareWith(b.period.previousLabel)}'}',
                        style: TextStyle(fontSize: 12, color: c.ink3)),
                  ),
                  for (final k in b.kpis)
                    Pressable(
                      onTap: k.href == null ? null : () => context.go(appRouteForWeb(k.href)),
                      child: Container(
                        decoration: BoxDecoration(border: Border(top: BorderSide(color: c.line))),
                        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 11),
                        child: Row(
                          children: [
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(k.title, style: TextStyle(fontSize: 14, color: c.ink)),
                                  if (k.previous != null)
                                    Text(value(k, k.previous), style: TextStyle(fontSize: 11, color: c.ink3)),
                                ],
                              ),
                            ),
                            Text(value(k, k.value), style: TextStyle(fontSize: 14, fontWeight: FontWeight.w700, color: k.value == null ? c.ink3 : c.ink)),
                            if (k.changePercent != null) ...[
                              const SizedBox(width: 8),
                              SoftBadge(
                                label: '${fmt.signed(k.changePercent!, 1)}%',
                                tone: (k.changePercent! >= 0) != kpiGoodWhenDown(k.code) ? Tone.ok : Tone.bad,
                              ),
                            ],
                          ],
                        ),
                      ),
                    ),
                  if (!b.complete || b.notes.isNotEmpty)
                    Container(
                      decoration: BoxDecoration(border: Border(top: BorderSide(color: c.line))),
                      padding: const EdgeInsets.fromLTRB(16, 10, 16, 12),
                      child: Text(
                        [if (!b.complete) l10n.anIncomplete, ...b.notes].join(' · '),
                        style: TextStyle(fontSize: 11, height: 1.4, color: c.warn),
                      ),
                    ),
                ],
              ),
            ),
          ),
      ],
    );
  }
}

class _FinanceSection extends ConsumerWidget {
  const _FinanceSection();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final c = context.ob;
    final fmt = context.fmt;
    final s = ref.watch(homeFinanceBlock);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(title: l10n.anModFinance, onMore: () => context.go('/home/finance'), moreLabel: l10n.anOpenAll),
        const SizedBox(height: 12),
        blockView(context, s, onRetry: () => ref.read(homeFinanceBlock.notifier).refresh(), (FinanceOverview f) {
          if (!f.meta.hasData) {
            return ObCard(
              onTap: () => context.go('/home/finance'),
              child: Text(f.meta.note ?? l10n.financeNoData, style: TextStyle(fontSize: 13, height: 1.4, color: c.ink3)),
            );
          }
          return ObCard(
            onTap: () => context.go('/home/finance'),
            child: MetricRow(items: [
              (l10n.financeNetRevenue, fmt.compact(f.line('net_revenue')?.value ?? 0), null),
              (l10n.financeExpenses, fmt.compact(f.lastMonth?.expenses ?? 0), null),
              (l10n.financeNetProfit, fmt.compact(f.line('net_profit')?.value ?? 0), null),
            ]),
          );
        }),
      ],
    );
  }
}

class _ProductionSection extends ConsumerWidget {
  const _ProductionSection();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final fmt = context.fmt;
    final s = ref.watch(technologistBlock);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(title: l10n.anModProduction, onMore: () => context.go('/home/production'), moreLabel: l10n.anOpenAll),
        const SizedBox(height: 12),
        blockView(
          context,
          s,
          onRetry: () => ref.read(technologistBlock.notifier).refresh(),
          (t) => ObCard(
            onTap: () => context.go('/home/production'),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                MetricRow(items: [
                  (l10n.anProductionToday, fmt.weight(t.todayKg), null),
                  (l10n.anCompletion, t.completionPercent == null ? '—' : '${fmt.decimal(t.completionPercent!)}%', null),
                  (l10n.anDefect, t.defectPercent == null ? '—' : '${fmt.decimal(t.defectPercent!)}%', fmt.weight(t.defectKg)),
                ]),
                if (t.days.isNotEmpty) ...[
                  const SizedBox(height: 12),
                  DailyBars(values: [for (final d in t.days) d.goodKg], format: fmt.weight, height: 70),
                ],
              ],
            ),
          ),
        ),
      ],
    );
  }
}

class _FindingsSection extends ConsumerWidget {
  const _FindingsSection();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final c = context.ob;
    final nav = ref.watch(analytikNavBlock).data;
    if (nav == null || nav.total == 0) return const SizedBox.shrink();
    return ObCard(
      tint: c.focus,
      onTap: () => context.go('/home/insights'),
      child: Row(
        children: [
          const IconTile(icon: LucideIcons.brainCircuit, tone: Tone.violet, size: 40, radius: 12),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(l10n.anFindingsTitle, style: TextStyle(fontSize: 15, fontWeight: FontWeight.w700, color: c.ink)),
                const SizedBox(height: 2),
                Text(l10n.anFindingsLine(nav.critical, nav.warning, nav.info), style: TextStyle(fontSize: 12, color: c.ink3)),
              ],
            ),
          ),
          Icon(LucideIcons.chevronRight, size: 16, color: c.subtle),
        ],
      ),
    );
  }
}
