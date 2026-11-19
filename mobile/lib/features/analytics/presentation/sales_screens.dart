import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/utils/debouncer.dart';
import '../../../shared/extensions/context_ext.dart';
import '../../../shared/widgets/ob_app_bar.dart';
import '../../../shared/widgets/ob_card.dart';
import '../../../shared/widgets/pills.dart';
import '../../../shared/widgets/pressable.dart';
import '../../../shared/widgets/rings.dart';
import '../../../shared/widgets/section_header.dart';
import '../../../shared/widgets/states.dart';
import '../data/sales_models.dart';
import 'analytics_providers.dart';
import 'analytics_screen.dart';
import 'widgets/sales_widgets.dart';

const _gap = SizedBox(height: ObSpace.block);

/// Республика, направление или «Мои регионы»: показатели, направления и регионы, продажи по дням, те же дни, категории.
class GroupScreen extends ConsumerWidget {
  const GroupScreen({super.key, required this.path});

  /// `republic`, `my` или `directions/{id}`.
  final String path;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final month = ref.watch(salesMonthProvider);
    final args = (path: path, month: month);
    final s = ref.watch(groupBlock(args));
    final title = s.data?.name ?? (path == 'republic' ? l10n.anRepublic : l10n.anMyRegions);
    return ObPage(
      title: title,
      subtitle: s.data?.subtitle == null ? null : Text(s.data!.subtitle!),
      actions: const [MonthPill()],
      onRefresh: () => ref.read(groupBlock(args).notifier).refresh(),
      slivers: [
        SliverToBoxAdapter(
          child: blockView(context, s, skeleton: 400, onRetry: () => ref.read(groupBlock(args).notifier).refresh(), (g) {
            final fmt = context.fmt;
            final directions = g.cards.where((c) => c.kind == 'direction').toList();
            final regionCards = g.cards.where((c) => c.kind == 'region').toList();
            final regions = regionCards.isNotEmpty ? regionCards : g.regions;
            return Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                PlanCard(kpi: g.kpi, period: g.period),
                const SizedBox(height: 12),
                KpiGrid(kpi: g.kpi, onAgents: () => context.go('/analytics/sales/agents')),
                if (directions.isNotEmpty) ...[
                  _gap,
                  SectionHeader(title: l10n.anDirections),
                  const SizedBox(height: 12),
                  for (final d in directions) ...[
                    UnitCard(row: d, onTap: d.isChannel || d.factKg == 0 && d.agents == 0 ? null : () => context.push('/analytics/sales/directions/${d.id}')),
                    const SizedBox(height: 10),
                  ],
                ],
                if (regions.isNotEmpty) ...[
                  _gap,
                  SectionHeader(title: l10n.anRegions, trailing: Text('${regions.length}', style: TextStyle(fontSize: 13, color: context.ob.ink3))),
                  const SizedBox(height: 12),
                  for (final r in regions) ...[
                    UnitCard(
                      row: r,
                      onTap: r.id.startsWith('00000000') ? null : () => context.push('/analytics/sales/regions/${r.id}'),
                    ),
                    const SizedBox(height: 10),
                  ],
                ],
                if (g.days.any((d) => d != null)) ...[
                  _gap,
                  SectionHeader(title: l10n.anByDays),
                  const SizedBox(height: 12),
                  ObCard(child: DailyBars(values: g.days, format: fmt.weight)),
                ],
                if (g.sameDays.isNotEmpty) ...[
                  _gap,
                  SectionHeader(title: l10n.anSameDays),
                  const SizedBox(height: 12),
                  SameDaysList(
                    rows: g.sameDays,
                    onTap: (r) => r.id.startsWith('00000000') ? null : context.push('/analytics/sales/regions/${r.id}'),
                  ),
                ],
                if (g.categoryCards.isNotEmpty) ...[
                  _gap,
                  SectionHeader(title: l10n.anCategories),
                  const SizedBox(height: 12),
                  CategoryList(cards: g.categoryCards),
                ],
              ],
            );
          }),
        ),
      ],
    );
  }
}

/// Регион: показатели, план и факт по месяцам, состав выручки, команда ТП (→ карточка ТП), те же дни, категории.
class RegionScreen extends ConsumerWidget {
  const RegionScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final month = ref.watch(salesMonthProvider);
    final args = (id: id, month: month);
    final s = ref.watch(regionBlock(args));
    final r = s.data;
    final sub = r == null
        ? null
        : [
            if (r.directionName != null) r.directionName!,
            if (r.supervisor != null) l10n.anSupervisor(r.supervisor!),
            if (r.dealer != null) l10n.anDealer(r.dealer!),
          ].join(' · ');
    return ObPage(
      title: r?.name ?? l10n.anRegions,
      subtitle: sub == null || sub.isEmpty ? null : Text(sub),
      actions: const [MonthPill()],
      onRefresh: () => ref.read(regionBlock(args).notifier).refresh(),
      slivers: [
        SliverToBoxAdapter(
          child: blockView(context, s, skeleton: 400, onRetry: () => ref.read(regionBlock(args).notifier).refresh(), (r) {
            final fmt = context.fmt;
            final c = context.ob;
            final maxKg = r.months.fold<double>(0, (a, m) => math.max(a, math.max(m.planKg ?? 0, m.factKg ?? 0)));
            return Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                PlanCard(kpi: r.kpi, period: r.period),
                const SizedBox(height: 12),
                KpiGrid(kpi: r.kpi),
                if (r.team.isNotEmpty) ...[
                  _gap,
                  SectionHeader(title: l10n.anTeam, trailing: Text('${r.team.length}', style: TextStyle(fontSize: 13, color: c.ink3))),
                  const SizedBox(height: 12),
                  ObListCard(children: [for (final t in r.team) _TeamTile(row: t)]),
                ],
                if (r.months.isNotEmpty && maxKg > 0) ...[
                  _gap,
                  SectionHeader(title: l10n.anPlanFactMonths),
                  const SizedBox(height: 12),
                  ObCard(
                    child: Column(
                      children: [
                        for (final m in r.months)
                          Padding(
                            padding: const EdgeInsets.symmetric(vertical: 4),
                            child: Row(
                              children: [
                                SizedBox(width: 34, child: Text(fmt.monthShort(DateTime(r.period.year, m.month)), style: TextStyle(fontSize: 11, color: c.ink3))),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      if (m.planKg != null)
                                        FractionallySizedBox(
                                          widthFactor: (m.planKg! / maxKg).clamp(0.01, 1.0),
                                          child: Container(height: 5, decoration: BoxDecoration(color: c.lineStrong, borderRadius: BorderRadius.circular(3))),
                                        ),
                                      const SizedBox(height: 2),
                                      FractionallySizedBox(
                                        widthFactor: ((m.factKg ?? 0) / maxKg).clamp(0.01, 1.0),
                                        child: Container(height: 7, decoration: BoxDecoration(color: c.accent, borderRadius: BorderRadius.circular(3))),
                                      ),
                                    ],
                                  ),
                                ),
                                SizedBox(
                                  width: 64,
                                  child: Text(fmt.weight(m.factKg ?? 0), textAlign: TextAlign.right, style: TextStyle(fontSize: 11, color: c.ink2)),
                                ),
                              ],
                            ),
                          ),
                        const SizedBox(height: 6),
                        Row(children: [
                          DotLabel(color: c.lineStrong, label: l10n.anPlanKg),
                          const SizedBox(width: 12),
                          DotLabel(color: c.accent, label: l10n.anFact),
                        ]),
                      ],
                    ),
                  ),
                ],
                if (r.categories.isNotEmpty) ...[
                  _gap,
                  SectionHeader(title: l10n.anRevenueMix),
                  const SizedBox(height: 12),
                  ObCard(
                    child: Column(
                      children: [
                        for (final cat in r.categories)
                          Padding(
                            padding: const EdgeInsets.symmetric(vertical: 5),
                            child: Row(
                              children: [
                                Expanded(child: Text(cat.name, style: TextStyle(fontSize: 13, color: c.ink))),
                                SizedBox(width: 90, child: ProgressLine(value: (cat.share ?? 0).clamp(0.0, 1.0), color: c.accent, height: 5)),
                                SizedBox(
                                  width: 48,
                                  child: Text(cat.share == null ? '—' : fmt.percent(cat.share!), textAlign: TextAlign.right, style: TextStyle(fontSize: 12, color: c.ink2)),
                                ),
                              ],
                            ),
                          ),
                      ],
                    ),
                  ),
                ],
                if (r.days.any((d) => d != null)) ...[
                  _gap,
                  SectionHeader(title: l10n.anByDays),
                  const SizedBox(height: 12),
                  ObCard(child: DailyBars(values: r.days, format: fmt.weight)),
                ],
                if (r.sameDays.isNotEmpty) ...[
                  _gap,
                  SectionHeader(title: l10n.anSameDays),
                  const SizedBox(height: 12),
                  SameDaysList(rows: r.sameDays, onTap: (row) => context.push('/analytics/sales/agents/${row.id}')),
                ],
                if (r.categoryCards.isNotEmpty) ...[
                  _gap,
                  SectionHeader(title: l10n.anCategories),
                  const SizedBox(height: 12),
                  CategoryList(cards: r.categoryCards),
                ],
              ],
            );
          }),
        ),
      ],
    );
  }
}

class _TeamTile extends StatelessWidget {
  const _TeamTile({required this.row});

  final TeamRow row;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final fmt = context.fmt;
    final l10n = context.l10n;
    return Pressable(
      onTap: row.isVacancy ? null : () => context.push('/analytics/sales/agents/${row.agentId}'),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(child: Text(row.name, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: c.ink))),
                if (row.isVacancy) SoftBadge(label: l10n.anVacancy, tone: Tone.neutral),
                if (row.execution != null)
                  Text(fmt.percent(row.execution!), style: TextStyle(fontSize: 14, fontWeight: FontWeight.w700, color: c.toneFg(levelTone(row.executionLevel)))),
              ],
            ),
            const SizedBox(height: 6),
            Text(
              '${l10n.anFact} ${fmt.weight(row.factKg)} · ${l10n.anRevenue} ${fmt.compact(row.revenue)} · ${l10n.anStrike} ${row.strike == null ? '—' : fmt.percent(row.strike!)}',
              style: TextStyle(fontSize: 12, color: c.ink3),
            ),
            if (row.flags.any((f) => f.severity != 'Info')) ...[const SizedBox(height: 8), FlagPills(flags: row.flags, showNone: false)],
          ],
        ),
      ),
    );
  }
}

enum _AgentFilter { all, planOk, below, noPlan, critical, risk, remarks, vacancy }

enum _AgentSort { execution, fact, revenue, name }

/// Агенты (ТП): поиск по имени, ID или региону, фильтры по статусу, сортировка — как список веба (сервер фильтров не даёт).
class AgentsScreen extends ConsumerStatefulWidget {
  const AgentsScreen({super.key});

  @override
  ConsumerState<AgentsScreen> createState() => _AgentsScreenState();
}

class _AgentsScreenState extends ConsumerState<AgentsScreen> {
  final _debounce = Debouncer(const Duration(milliseconds: 250));
  String _query = '';
  _AgentFilter _filter = _AgentFilter.all;
  _AgentSort _sort = _AgentSort.execution;

  @override
  void dispose() {
    _debounce.cancel();
    super.dispose();
  }

  bool _match(AgentCardRow a) {
    final q = _query.trim().toLowerCase();
    if (q.isNotEmpty && !'${a.name} ${a.agentId} ${a.regionName}'.toLowerCase().contains(q)) return false;
    final bad = a.flags.where((f) => f.severity == 'Critical').isNotEmpty;
    final risk = a.flags.where((f) => f.severity == 'Risk').isNotEmpty;
    return switch (_filter) {
      _AgentFilter.all => !a.isVacancy,
      _AgentFilter.planOk => (a.execution ?? 0) >= 1,
      _AgentFilter.below => a.execution != null && a.execution! < 1,
      _AgentFilter.noPlan => a.planKg == null && !a.isVacancy,
      _AgentFilter.critical => bad,
      _AgentFilter.risk => risk && !bad,
      _AgentFilter.remarks => bad || risk,
      _AgentFilter.vacancy => a.isVacancy,
    };
  }

  int _compare(AgentCardRow a, AgentCardRow b) => switch (_sort) {
        _AgentSort.execution => (b.execution ?? -1).compareTo(a.execution ?? -1),
        _AgentSort.fact => b.factKg.compareTo(a.factKg),
        _AgentSort.revenue => b.revenue.compareTo(a.revenue),
        _AgentSort.name => a.name.compareTo(b.name),
      };

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final c = context.ob;
    final month = ref.watch(salesMonthProvider);
    final s = ref.watch(agentsBlock(month));
    String filterLabel(_AgentFilter f) => switch (f) {
          _AgentFilter.all => l10n.anFilterAll,
          _AgentFilter.planOk => l10n.anFilterPlanOk,
          _AgentFilter.below => l10n.anFilterBelow,
          _AgentFilter.noPlan => l10n.anFilterNoPlan,
          _AgentFilter.critical => l10n.anCritical,
          _AgentFilter.risk => l10n.anRisk,
          _AgentFilter.remarks => l10n.anFilterRemarks,
          _AgentFilter.vacancy => l10n.anFilterVacancy,
        };
    String sortLabel(_AgentSort s) => switch (s) {
          _AgentSort.execution => l10n.anSortExecution,
          _AgentSort.fact => l10n.anSortFact,
          _AgentSort.revenue => l10n.anSortRevenue,
          _AgentSort.name => l10n.anSortName,
        };
    final all = s.data?.agents ?? const <AgentCardRow>[];
    final rows = all.where(_match).toList()..sort(_compare);
    int count(_AgentFilter f) {
      final keep = _filter;
      _filter = f;
      final n = all.where(_match).length;
      _filter = keep;
      return n;
    }

    return ObPage(
      title: l10n.anAgents,
      actions: const [MonthPill()],
      onRefresh: () => ref.read(agentsBlock(month).notifier).refresh(),
      slivers: [
        SliverToBoxAdapter(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              TextField(
                decoration: InputDecoration(hintText: l10n.anSearchAgents, prefixIcon: const Icon(LucideIcons.search, size: 18), isDense: true),
                onChanged: (v) => _debounce(() => setState(() => _query = v)),
              ),
              const SizedBox(height: 10),
              SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                clipBehavior: Clip.none,
                child: Row(
                  children: [
                    for (final f in _AgentFilter.values) ...[
                      FilterPill(
                        label: s.data == null ? filterLabel(f) : '${filterLabel(f)} ${count(f)}',
                        active: f == _filter,
                        chevron: false,
                        onTap: () => setState(() => _filter = f),
                      ),
                      const SizedBox(width: 8),
                    ],
                  ],
                ),
              ),
              const SizedBox(height: 10),
              Row(
                children: [
                  Expanded(
                    child: Text(l10n.anShown(rows.length, all.where((a) => !a.isVacancy).length), style: TextStyle(fontSize: 12, color: c.ink3)),
                  ),
                  PopupMenuButton<_AgentSort>(
                    initialValue: _sort,
                    onSelected: (v) => setState(() => _sort = v),
                    itemBuilder: (context) => [for (final v in _AgentSort.values) PopupMenuItem(value: v, child: Text(sortLabel(v)))],
                    child: Row(children: [
                      Icon(LucideIcons.arrowDownUp, size: 14, color: c.accentStrong),
                      const SizedBox(width: 4),
                      Text(sortLabel(_sort), style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: c.accentStrong)),
                    ]),
                  ),
                ],
              ),
              const SizedBox(height: 10),
            ],
          ),
        ),
        if (s.data == null)
          SliverToBoxAdapter(child: blockView(context, s, onRetry: () => ref.read(agentsBlock(month).notifier).refresh(), (_) => const SizedBox.shrink()))
        else if (rows.isEmpty)
          SliverToBoxAdapter(child: EmptyView(icon: LucideIcons.searchX, title: l10n.stateEmpty, compact: true))
        else
          SliverList.separated(
            itemCount: rows.length,
            itemBuilder: (context, i) => _AgentCard(row: rows[i], targets: s.data!.targets),
            separatorBuilder: (context, i) => const SizedBox(height: 10),
          ),
      ],
    );
  }
}

class _AgentCard extends StatelessWidget {
  const _AgentCard({required this.row, required this.targets});

  final AgentCardRow row;
  final AgentTargets targets;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final l10n = context.l10n;
    final fmt = context.fmt;
    final tone = levelTone(row.executionLevel);
    final place = [row.regionName, row.directionName, row.supervisor].whereType<String>().where((s) => s.isNotEmpty).join(' · ');
    return ObCard(
      onTap: row.isVacancy ? null : () => context.push('/analytics/sales/agents/${row.agentId}'),
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(row.name, style: TextStyle(fontSize: 15, fontWeight: FontWeight.w600, color: c.ink)),
                    if (place.isNotEmpty) Text(place, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12, color: c.ink3)),
                  ],
                ),
              ),
              if (row.isVacancy) SoftBadge(label: l10n.anVacancy, tone: Tone.neutral),
            ],
          ),
          const SizedBox(height: 10),
          if (row.execution != null) ...[
            Row(
              children: [
                Text('${l10n.anExecution} ', style: TextStyle(fontSize: 12, color: c.ink3)),
                Text(fmt.percent(row.execution!), style: TextStyle(fontSize: 14, fontWeight: FontWeight.w700, color: c.toneFg(tone == Tone.neutral ? Tone.accent : tone))),
                const Spacer(),
                Text(l10n.anPlanOf(fmt.weight(row.factKg), fmt.weight(row.planKg ?? 0)), style: TextStyle(fontSize: 11, color: c.ink3)),
              ],
            ),
            const SizedBox(height: 6),
            ProgressLine(value: row.execution!.clamp(0.0, 1.0), color: c.toneSolid(tone == Tone.neutral ? Tone.accent : tone), height: 5),
          ] else
            Text(l10n.anAgentNoPlan(fmt.weight(row.factKg)), style: TextStyle(fontSize: 12, color: c.ink3)),
          const SizedBox(height: 12),
          MetricRow(items: [
            (l10n.anRevenue, fmt.compact(row.revenue), null),
            ('АКБ', fmt.count(row.akb), l10n.anTarget(fmt.decimal(targets.akbPerAgent))),
            (l10n.anStrike, row.conversion == null ? '—' : fmt.percent(row.conversion!), null),
          ]),
          const SizedBox(height: 10),
          MetricRow(items: [
            (l10n.anVisits, fmt.count(row.visits), l10n.anWithOrder(row.visitsWithOrder)),
            (l10n.anOrders, fmt.count(row.orders), null),
            (l10n.anTempo, row.tempo == null ? '—' : fmt.percent(row.tempo!), l10n.anCategoriesN(row.categories)),
          ]),
          if (row.flags.any((f) => f.severity != 'Info')) ...[const SizedBox(height: 10), FlagPills(flags: row.flags, showNone: false)],
        ],
      ),
    );
  }
}

/// Карточка ТП: показатели с медианой региона, замечания с пояснениями, план по категориям, те же дни, точки месяца (→ точка),
/// «ещё не купили», новые точки.
class AgentScreen extends ConsumerWidget {
  const AgentScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final month = ref.watch(salesMonthProvider);
    final args = (id: id, month: month);
    final s = ref.watch(agentBlock(args));
    final a = s.data;
    return ObPage(
      title: a?.name ?? l10n.anAgents,
      subtitle: a == null ? null : Text('${a.regionName ?? ''} · ID ${a.agentId}'),
      actions: const [MonthPill()],
      onRefresh: () => ref.read(agentBlock(args).notifier).refresh(),
      slivers: [
        SliverToBoxAdapter(
          child: blockView(context, s, skeleton: 400, onRetry: () => ref.read(agentBlock(args).notifier).refresh(), (a) {
            final c = context.ob;
            final fmt = context.fmt;
            String? median(MedianValue m, String Function(double) f) => m.regionMedian == null ? null : l10n.anMedian(f(m.regionMedian!));
            final tone = levelTone(a.executionLevel);
            return Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                ObCard(
                  tint: c.kpi,
                  radius: ObRadius.xl,
                  padding: const EdgeInsets.all(20),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(l10n.anPlanCard, style: TextStyle(fontSize: 13, color: c.ink3)),
                      const SizedBox(height: 6),
                      if (a.execution != null) ...[
                        Text(fmt.percent(a.execution!), style: context.text.displayMedium!.copyWith(color: c.toneFg(tone == Tone.neutral ? Tone.accent : tone))),
                        Text(l10n.anPlanOf(fmt.weight(a.factKg), fmt.weight(a.planKg ?? 0)), style: TextStyle(fontSize: 12, color: c.ink3)),
                        const SizedBox(height: 10),
                        ProgressLine(value: a.execution!.clamp(0.0, 1.0), color: c.toneSolid(tone == Tone.neutral ? Tone.accent : tone)),
                      ] else
                        Text(l10n.anAgentNoPlan(fmt.weight(a.factKg)), style: TextStyle(fontSize: 14, color: c.ink2)),
                      if (a.sameDays?.kgDelta != null) ...[
                        const SizedBox(height: 10),
                        SoftBadge(
                          label: '${fmt.signedPercent(a.sameDays!.kgDelta!)} ${l10n.vsSameDays}',
                          tone: a.sameDays!.kgDelta! >= 0 ? Tone.ok : Tone.bad,
                          icon: a.sameDays!.kgDelta! >= 0 ? LucideIcons.trendingUp : LucideIcons.trendingDown,
                        ),
                      ],
                    ],
                  ),
                ),
                const SizedBox(height: 12),
                ObCard(
                  child: Column(
                    children: [
                      MetricRow(items: [
                        (l10n.anRevenue, fmt.compact(a.revenue), null),
                        ('АКБ', fmt.count(a.akb), a.revenuePerOutlet == null ? null : fmt.compact(a.revenuePerOutlet!)),
                        (l10n.anVisits, fmt.count(a.visits), l10n.anWithOrder(a.visitsWithOrder)),
                      ]),
                      const SizedBox(height: 14),
                      MetricRow(items: [
                        (l10n.anConversion, a.conversion.value == null ? '—' : fmt.percent(a.conversion.value!), median(a.conversion, fmt.percent)),
                        (l10n.anSumPerVisit, a.sumPerVisit.value == null ? '—' : fmt.compact(a.sumPerVisit.value!), median(a.sumPerVisit, fmt.compact)),
                        (l10n.anAvgCheck, a.avgCheck.value == null ? '—' : fmt.compact(a.avgCheck.value!), median(a.avgCheck, fmt.compact)),
                      ]),
                      const SizedBox(height: 14),
                      MetricRow(items: [
                        (l10n.anKgPerOutlet, a.kgPerOutlet == null ? '—' : fmt.weight(a.kgPerOutlet!), null),
                        (l10n.anCategories, '${a.categories}', l10n.anTarget(fmt.decimal(a.categoryTarget))),
                        (l10n.anTempo, a.tempo == null ? '—' : fmt.percent(a.tempo!), null),
                      ]),
                    ],
                  ),
                ),
                _gap,
                SectionHeader(title: l10n.anFindings),
                const SizedBox(height: 12),
                if (a.flags.where((f) => f.severity != 'Info').isEmpty)
                  ObCard(
                    child: Row(children: [
                      Icon(LucideIcons.circleCheck, size: 18, color: c.ok),
                      const SizedBox(width: 10),
                      Text(l10n.anNoRemarks, style: TextStyle(fontSize: 14, color: c.ink)),
                    ]),
                  )
                else
                  for (final f in a.flags.where((f) => f.severity != 'Info')) ...[
                    ObCard(
                      borderColor: c.toneFg(flagTone(f.severity)).withValues(alpha: 0.35),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(children: [
                            SoftBadge(label: f.label, tone: flagTone(f.severity)),
                            const SizedBox(width: 8),
                            Expanded(child: Text(f.title, style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: c.ink))),
                          ]),
                          const SizedBox(height: 6),
                          Text(f.explanation, style: TextStyle(fontSize: 13, height: 1.4, color: c.ink2)),
                        ],
                      ),
                    ),
                    const SizedBox(height: 10),
                  ],
                if (a.categoryPlan.isNotEmpty) ...[
                  _gap,
                  SectionHeader(title: l10n.anPlanCard),
                  const SizedBox(height: 12),
                  ObListCard(children: [
                    for (final p in a.categoryPlan)
                      Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 11),
                        child: Row(
                          children: [
                            Expanded(child: Text(p.name, style: TextStyle(fontSize: 13, color: c.ink))),
                            Text(l10n.anPlanOf(fmt.weight(p.factKg), p.planKg == null ? '—' : fmt.weight(p.planKg!)), style: TextStyle(fontSize: 12, color: c.ink3)),
                            const SizedBox(width: 8),
                            Text(p.execution == null ? '—' : fmt.percent(p.execution!),
                                style: TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: c.toneFg(levelTone(p.executionLevel)))),
                          ],
                        ),
                      ),
                  ]),
                ],
                if (a.stores.isNotEmpty) ...[
                  _gap,
                  SectionHeader(title: l10n.anStores, trailing: Text('${a.stores.length}', style: TextStyle(fontSize: 13, color: c.ink3))),
                  const SizedBox(height: 12),
                  ObListCard(children: [
                    for (final st in a.stores.take(30))
                      _MarketTile(name: st.name, value: fmt.weight(st.kg), sub: fmt.compact(st.revenue), onTap: () => context.push('/home/sales/points/${st.marketId}')),
                  ]),
                ],
                if (a.silent.isNotEmpty) ...[
                  _gap,
                  SectionHeader(title: l10n.anSilentMarkets, trailing: Text('${a.silent.length} / ${a.silentBase}', style: TextStyle(fontSize: 13, color: c.ink3))),
                  const SizedBox(height: 12),
                  ObListCard(children: [
                    for (final st in a.silent.take(20))
                      _MarketTile(
                        name: st.name,
                        value: fmt.weight(st.prevKg),
                        sub: l10n.anPrevMonth(fmt.compact(st.prevRevenue)),
                        onTap: () => context.push('/home/sales/points/${st.marketId}'),
                      ),
                  ]),
                ],
                if (a.newMarkets.isNotEmpty) ...[
                  _gap,
                  SectionHeader(title: l10n.anNewMarkets),
                  const SizedBox(height: 12),
                  ObListCard(children: [
                    for (final st in a.newMarkets.take(20))
                      _MarketTile(name: st.name, value: fmt.weight(st.kg), sub: fmt.compact(st.revenue), onTap: () => context.push('/home/sales/points/${st.marketId}')),
                  ]),
                ],
              ],
            );
          }),
        ),
      ],
    );
  }
}

class _MarketTile extends StatelessWidget {
  const _MarketTile({required this.name, required this.value, required this.sub, required this.onTap});

  final String name;
  final String value;
  final String sub;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    return Pressable(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 11),
        child: Row(
          children: [
            Icon(LucideIcons.store, size: 16, color: c.ink3),
            const SizedBox(width: 10),
            Expanded(child: Text(name, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 14, color: c.ink))),
            Column(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                Text(value, style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: c.ink)),
                Text(sub, style: TextStyle(fontSize: 11, color: c.ink3)),
              ],
            ),
            const SizedBox(width: 6),
            Icon(LucideIcons.chevronRight, size: 14, color: c.subtle),
          ],
        ),
      ),
    );
  }
}
