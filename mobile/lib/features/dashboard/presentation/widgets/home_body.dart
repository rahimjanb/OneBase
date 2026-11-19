import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../../../app/theme/tokens.dart';
import '../../../../core/core_providers.dart';
import '../../../../core/network/connectivity.dart';
import '../../../../shared/extensions/context_ext.dart';
import '../../../../shared/widgets/ob_card.dart';
import '../../../../shared/widgets/pills.dart';
import '../../../../shared/widgets/pressable.dart';
import '../../../../shared/widgets/rings.dart';
import '../../../../shared/widgets/section_header.dart';
import '../../../../shared/widgets/segmented.dart';
import '../../../../shared/widgets/skeleton.dart';
import '../../domain/home_view.dart';
import '../dashboard_providers.dart';
import 'home_head.dart';
import 'home_labels.dart';

const _show = 3;

/// «Фокус дня» — находки AI-консультанта: до трёх, по важности, по выбранным отделам; ключевая цифра слева, тап — связанный раздел.
class FocusCard extends ConsumerWidget {
  const FocusCard({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(alertsBlock);
    final alerts = state.data;
    if (alerts == null) return state.isInitialLoading ? const _CardSkeleton(rows: 3) : const SizedBox.shrink();
    final c = context.ob;
    final l10n = context.l10n;
    final filters = ref.watch(homeFiltersProvider);
    final work = ref.watch(workBlock).data;
    final all = focusOf(alerts);
    final list = all.where((i) => filters.includes(i.category)).toList();
    final shown = list.take(_show).toList();
    final more = list.length - shown.length;
    final departments = alerts.alerts.map((a) => a.category).toSet().length;
    final subtitle = [
      if (departments > 0) l10n.focusDepartments(departments),
      if (work != null) l10n.focusActiveTasks(work.open),
    ].join(' · ');
    final question = shown.isEmpty ? l10n.focusAskEmpty : l10n.focusAskQuestion(shown.map((i) => '«${i.text}»').join('; '));

    return ObCard(
      tint: c.focus,
      padding: const EdgeInsets.only(top: 18, bottom: 6),
      clip: true,
      child: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(18, 0, 14, 14),
            child: Row(
              children: [
                const IconTile(icon: LucideIcons.sparkles, tone: Tone.violet, size: 40, radius: 12),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Wrap(
                        spacing: 8,
                        crossAxisAlignment: WrapCrossAlignment.center,
                        children: [
                          Semantics(header: true, child: Text(l10n.focusTitle, style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700).copyWith(color: c.ink))),
                          if (list.isNotEmpty) SoftBadge(label: 'AI · ${list.length}', tone: Tone.violet),
                        ],
                      ),
                      const SizedBox(height: 3),
                      Text(subtitle.isEmpty ? l10n.focusFallback : subtitle,
                          maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12, color: c.ink3)),
                    ],
                  ),
                ),
                if (ref.watch(dashboardAccessProvider).ai)
                  HeaderButton(
                    icon: LucideIcons.messageCircle,
                    semanticLabel: l10n.quickAskAi,
                    onTap: () => context.go(Uri(path: '/ai', queryParameters: {'ask': question}).toString()),
                  ),
              ],
            ),
          ),
          if (shown.isEmpty)
            Container(
              decoration: BoxDecoration(border: Border(top: BorderSide(color: c.rowDivider))),
              padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 16),
              child: Row(
                children: [
                  Icon(LucideIcons.circleCheck, size: 20, color: c.ok),
                  const SizedBox(width: 10),
                  Text(l10n.focusCalm, style: TextStyle(fontSize: 14, fontWeight: FontWeight.w500, color: c.ink)),
                ],
              ),
            )
          else
            for (final i in shown) _FocusRow(item: i),
          if (more > 0)
            Pressable(
              onTap: () => context.go('/home/insights'),
              child: Container(
                constraints: const BoxConstraints(minHeight: 44),
                decoration: BoxDecoration(border: Border(top: BorderSide(color: c.rowDivider))),
                alignment: Alignment.center,
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(l10n.focusMore(more), style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: c.accentStrong)),
                    Icon(LucideIcons.chevronRight, size: 15, color: c.accentStrong),
                  ],
                ),
              ),
            ),
        ],
      ),
    );
  }
}

Tone severityTone(Severity s) => switch (s) {
      Severity.critical => Tone.bad,
      Severity.warning => Tone.warn,
      Severity.info => Tone.accent,
    };

class _FocusRow extends StatelessWidget {
  const _FocusRow({required this.item});

  final FocusItem item;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final l10n = context.l10n;
    final tone = severityTone(item.severity);
    final fg = c.toneFg(tone);
    return Pressable(
      onTap: () => openWebLink(context, item.href),
      semanticLabel: '${severityLabel(l10n, item.severity)}, ${categoryName(l10n, item.category)}: ${item.text}',
      child: Container(
        decoration: BoxDecoration(border: Border(top: BorderSide(color: c.rowDivider))),
        padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 13),
        child: Row(
          children: [
            Container(
              width: 46,
              height: 46,
              decoration: BoxDecoration(color: c.toneBg(tone), borderRadius: BorderRadius.circular(12)),
              alignment: Alignment.center,
              child: item.metric == null
                  ? Icon(item.severity == Severity.info ? LucideIcons.lightbulb : LucideIcons.triangleAlert, size: 20, color: fg)
                  : Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        FittedBox(
                          fit: BoxFit.scaleDown,
                          child: Text(item.metric!.value, style: TextStyle(fontSize: 15, fontWeight: FontWeight.w700, height: 1, color: fg)),
                        ),
                        if (item.metric!.caption.isNotEmpty) ...[
                          const SizedBox(height: 3),
                          Text(item.metric!.caption,
                              maxLines: 1, overflow: TextOverflow.clip, style: TextStyle(fontSize: 9, fontWeight: FontWeight.w500, height: 1, color: fg)),
                        ],
                      ],
                    ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Container(width: 6, height: 6, decoration: BoxDecoration(color: c.toneSolid(tone), shape: BoxShape.circle)),
                      const SizedBox(width: 5),
                      Text(severityLabel(l10n, item.severity), style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: fg)),
                      Flexible(
                        child: Text(' · ${categoryName(l10n, item.category)}',
                            maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 11, color: c.ink3)),
                      ),
                    ],
                  ),
                  const SizedBox(height: 3),
                  Text(item.text, style: TextStyle(fontSize: 14, height: 1.35, fontWeight: FontWeight.w500, color: c.ink)),
                ],
              ),
            ),
            const SizedBox(width: 8),
            Icon(LucideIcons.chevronRight, size: 16, color: c.subtle),
          ],
        ),
      ),
    );
  }
}

/// «Динамика»: вкладки рядов (продажи к плану, выручка, явка — какие есть), три итога и 12 недель столбиками. Шкала — как у веба,
/// без подписей оси; линия цели позади столбиков; ниже цели — бледные, выше — ярче, текущая неделя — градиентом. Тап — значение.
class DynamicsCard extends ConsumerStatefulWidget {
  const DynamicsCard({super.key});

  @override
  ConsumerState<DynamicsCard> createState() => _DynamicsCardState();
}

class _DynamicsCardState extends ConsumerState<DynamicsCard> {
  SeriesKey? _key;
  int? _tip;

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(homeTrendsBlock);
    final l10n = context.l10n;
    final trends = state.data;
    if (trends == null) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SectionHeader(title: l10n.dynTitle),
          const SizedBox(height: 12),
          if (state.isInitialLoading) const _CardSkeleton(rows: 4) else _EmptyCard(text: l10n.dynEmpty),
        ],
      );
    }
    final series = homeSeries(trends);
    final s = series.where((x) => x.key == _key).firstOrNull ?? series.firstOrNull;
    if (s == null) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [SectionHeader(title: l10n.dynTitle), const SizedBox(height: 12), _EmptyCard(text: l10n.dynEmpty)],
      );
    }
    final c = context.ob;
    final fmt = context.fmt;
    String valueText(double v) => s.unit == SeriesUnit.money ? fmt.compact(v) : fmt.pct1(v);
    final st = trendStats(s);
    final scale = trendScale(s);
    const h = 130.0;
    double hOf(double v) => (v.clamp(scale.lo, scale.hi) - scale.lo) / (scale.hi - scale.lo) * h;
    final last = s.values.length - 1;
    final stats = [
      (l10n.dynAverage, st.average == null ? '—' : valueText(st.average!), c.ink),
      (l10n.dynBest, st.best == null ? '—' : valueText(st.best!), c.ok),
      st.above != null ? (l10n.dynAbove, l10n.dynOfTotal(st.above!, st.count), c.ink) : (l10n.dynForWeeks(s.values.length), valueText(st.total), c.ink),
    ];

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(
          title: l10n.dynTitle,
          onMore: () => context.go(s.key == SeriesKey.attendance ? '/home/hr?tab=reports' : '/analytics/sales'),
        ),
        const SizedBox(height: 12),
        ObCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              if (series.length > 1) ...[
                ObSegmented<SeriesKey>(
                  values: [for (final x in series) x.key],
                  selected: s.key,
                  label: (k) => seriesLabel(l10n, k),
                  onChanged: (k) => setState(() {
                    _key = k;
                    _tip = null;
                  }),
                ),
                const SizedBox(height: 16),
              ],
              IntrinsicHeight(
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    for (var i = 0; i < stats.length; i++)
                      Expanded(
                        child: Container(
                          padding: i == 0 ? const EdgeInsets.only(right: 8) : const EdgeInsets.only(left: 14),
                          decoration: i == 0 ? null : BoxDecoration(border: Border(left: BorderSide(color: c.line))),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(stats[i].$1, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 11, color: c.ink3)),
                              const SizedBox(height: 4),
                              FittedBox(
                                fit: BoxFit.scaleDown,
                                alignment: Alignment.centerLeft,
                                child: Text(
                                  stats[i].$2,
                                  style: TextStyle(fontSize: s.unit == SeriesUnit.money ? 15 : 17, fontWeight: FontWeight.w700, height: 1.2, color: stats[i].$3),
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                  ],
                ),
              ),
              const SizedBox(height: 16),
              Semantics(
                label: l10n.dynSemantics(seriesLabel(l10n, s.key), stats[0].$2, stats[1].$2),
                child: SizedBox(
                  height: h,
                  child: LayoutBuilder(
                    builder: (context, box) {
                      final n = s.values.length;
                      const gap = 5.0;
                      final barW = (box.maxWidth - gap * (n - 1)) / n;
                      return Stack(
                        clipBehavior: Clip.none,
                        children: [
                          if (s.target != null) ...[
                            Positioned(
                              left: 0,
                              right: 0,
                              bottom: hOf(s.target!),
                              child: CustomPaint(size: const Size(double.infinity, 1), painter: _DashPainter(c.warn.withValues(alpha: 0.7))),
                            ),
                            Positioned(
                              left: 0,
                              bottom: hOf(s.target!) + 4,
                              child: Container(
                                padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                                decoration: BoxDecoration(color: c.warnSoft, borderRadius: BorderRadius.circular(6)),
                                child: Text(l10n.dynGoal(fmt.fixed(s.target!)), style: TextStyle(fontSize: 10, fontWeight: FontWeight.w600, color: c.warn)),
                              ),
                            ),
                          ],
                          for (var i = 0; i < n; i++)
                            if (s.values[i] != null)
                              Positioned(
                                left: i * (barW + gap),
                                bottom: 0,
                                width: barW,
                                height: h,
                                child: GestureDetector(
                                  behavior: HitTestBehavior.opaque,
                                  onTap: () => setState(() => _tip = _tip == i ? null : i),
                                  child: Align(
                                    alignment: Alignment.bottomCenter,
                                    child: TweenAnimationBuilder<double>(
                                      key: ValueKey('${s.key}-$i'),
                                      tween: Tween(begin: 0, end: hOf(s.values[i]!).clamp(3, h)),
                                      duration: Duration(milliseconds: MediaQuery.disableAnimationsOf(context) ? 0 : 500 + i * 25),
                                      curve: Curves.easeOut,
                                      builder: (context, v, _) => Container(
                                        height: v,
                                        decoration: BoxDecoration(
                                          borderRadius: const BorderRadius.vertical(top: Radius.circular(5)),
                                          gradient: i == last
                                              ? const LinearGradient(begin: Alignment.topCenter, end: Alignment.bottomCenter, colors: [Color(0xFF6E9BFF), Color(0xFF3D7BFF)])
                                              : null,
                                          color: i == last
                                              ? null
                                              : c.accent.withValues(
                                                  alpha: (s.target == null ? 0.40 : (s.values[i]! >= s.target! ? 0.55 : 0.25)) * (_tip == i ? 1.4 : 1),
                                                ),
                                        ),
                                      ),
                                    ),
                                  ),
                                ),
                              ),
                          if (_tip != null && s.values[_tip!] != null)
                            Positioned(
                              left: _tip! < 2
                                  ? _tip! * (barW + gap)
                                  : _tip! > last - 2
                                      ? null
                                      : _tip! * (barW + gap) + barW / 2,
                              right: _tip! > last - 2 ? (last - _tip!) * (barW + gap) : null,
                              bottom: hOf(s.values[_tip!]!).clamp(3, h) + 6,
                              child: FractionalTranslation(
                                translation: Offset(_tip! >= 2 && _tip! <= last - 2 ? -0.5 : 0, 0),
                                child: _Tip(text: l10n.dynWeekValue(s.weeks[_tip!], valueText(s.values[_tip!]!))),
                              ),
                            ),
                        ],
                      );
                    },
                  ),
                ),
              ),
              const SizedBox(height: 8),
              ExcludeSemantics(
                child: Row(
                  children: [
                    for (var i = 0; i < s.weeks.length; i++) ...[
                      if (i > 0) const SizedBox(width: 5),
                      Expanded(
                        child: Text(
                          '${s.weeks[i]}',
                          textAlign: TextAlign.center,
                          style: TextStyle(fontSize: 10, fontWeight: i == last ? FontWeight.w700 : FontWeight.w400, color: i == last ? c.ink : c.ink3),
                        ),
                      ),
                    ],
                  ],
                ),
              ),
              const SizedBox(height: 12),
              Text(l10n.dynCaption(s.weeks[last], l10n.daysN(trends.currentDays)), style: TextStyle(fontSize: 11, color: c.ink3)),
            ],
          ),
        ),
      ],
    );
  }
}

class _Tip extends StatelessWidget {
  const _Tip({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: c.popBg,
        borderRadius: BorderRadius.circular(8),
        boxShadow: const [BoxShadow(color: Color(0x40000000), blurRadius: 24, offset: Offset(0, 8))],
      ),
      child: Text(text, style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: c.popText)),
    );
  }
}

class _DashPainter extends CustomPainter {
  _DashPainter(this.color);

  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final p = Paint()
      ..color = color
      ..strokeWidth = 1;
    for (var x = 0.0; x < size.width; x += 7) {
      canvas.drawLine(Offset(x, 0), Offset((x + 4).clamp(0, size.width), 0), p);
    }
  }

  @override
  bool shouldRepaint(_DashPainter old) => old.color != color;
}

/// «Задачи»: кольцо — выполненные за месяц, в работе и на проверке, доля выполненных в центре и легенда с числами; просроченные — строкой.
class TasksCard extends ConsumerWidget {
  const TasksCard({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(workBlock);
    final l10n = context.l10n;
    final work = state.data;
    if (work == null) {
      if (state.forbidden || !state.isInitialLoading) return const SizedBox.shrink();
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [SectionHeader(title: l10n.navTasks), const SizedBox(height: 12), const _CardSkeleton(rows: 3)],
      );
    }
    final c = context.ob;
    final fmt = context.fmt;
    final t = homeTasks(work);
    final filters = ref.watch(homeFiltersProvider);
    final sum = t.slices.fold<int>(0, (a, s) => a + s.$2);
    final donePct = (t.done / (t.total < 1 ? 1 : t.total) * 100).round();
    final scope = t.departments.where((d) => filters.includes(d.code));
    final top = scope.fold<({String code, String name, int overdue})?>(null, (best, d) => d.overdue > (best?.overdue ?? 0) ? d : best);
    Color sliceColor(TaskSlice s) => switch (s) {
          TaskSlice.done => c.ok,
          TaskSlice.progress => c.accent,
          TaskSlice.review => c.violet,
        };
    String sliceRoute(TaskSlice s) => homeTargetRoute(switch (s) {
          TaskSlice.done => HomeTarget.tasksDone,
          TaskSlice.progress => HomeTarget.tasksProgress,
          TaskSlice.review => HomeTarget.tasksReview,
        });

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(title: l10n.navTasks, onMore: () => context.go('/tasks?view=all'), moreLabel: l10n.tasksAllN(fmt.count(t.total))),
        const SizedBox(height: 12),
        if (sum == 0 && t.overdue == 0)
          _EmptyCard(text: l10n.tasksNone)
        else
          ObCard(
            child: Column(
              children: [
                Row(
                  children: [
                    Donut(
                      segments: [for (final s in t.slices) (s.$2.toDouble(), sliceColor(s.$1))],
                      center: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text('$donePct%', style: TextStyle(fontSize: 26, fontWeight: FontWeight.w700, height: 1, letterSpacing: -0.5, color: c.ink)),
                          const SizedBox(height: 4),
                          Text(l10n.tasksDonePct, style: TextStyle(fontSize: 11, color: c.ink3)),
                        ],
                      ),
                    ),
                    const SizedBox(width: 20),
                    Expanded(
                      child: Column(
                        children: [
                          for (final s in t.slices)
                            _LegendRow(
                              marker: Container(width: 8, height: 8, decoration: BoxDecoration(color: sliceColor(s.$1), borderRadius: BorderRadius.circular(2))),
                              label: taskSliceLabel(l10n, s.$1),
                              value: fmt.count(s.$2),
                              onTap: () => context.go(sliceRoute(s.$1)),
                            ),
                          _LegendRow(
                            marker: Icon(LucideIcons.alarmClock, size: 11, color: c.bad),
                            label: l10n.tasksOverdueLabel,
                            value: fmt.count(t.overdue),
                            valueColor: t.overdue > 0 ? c.bad : null,
                            onTap: () => context.go(homeTargetRoute(HomeTarget.tasksOverdue)),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
                if (t.overdue > 0) ...[
                  const SizedBox(height: 16),
                  Pressable(
                    onTap: () => context.go(homeTargetRoute(HomeTarget.tasksOverdue)),
                    child: Container(
                      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 11),
                      decoration: BoxDecoration(color: c.badSoft, borderRadius: BorderRadius.circular(12)),
                      child: Row(
                        children: [
                          Icon(LucideIcons.alarmClock, size: 18, color: c.bad),
                          const SizedBox(width: 10),
                          Expanded(
                            child: Text(
                              l10n.tasksOverdueBanner(t.overdue) + (top == null ? '' : l10n.tasksOverdueIn(top.overdue, top.name)),
                              style: TextStyle(fontSize: 13, height: 1.35, color: c.ink),
                            ),
                          ),
                          const SizedBox(width: 8),
                          Text(l10n.actionOpen, style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: c.bad)),
                        ],
                      ),
                    ),
                  ),
                ],
              ],
            ),
          ),
      ],
    );
  }
}

class _LegendRow extends StatelessWidget {
  const _LegendRow({required this.marker, required this.label, required this.value, required this.onTap, this.valueColor});

  final Widget marker;
  final String label;
  final String value;
  final VoidCallback onTap;
  final Color? valueColor;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    return Pressable(
      onTap: onTap,
      child: ConstrainedBox(
        constraints: const BoxConstraints(minHeight: 30),
        child: Row(
          children: [
            SizedBox(width: 12, child: Center(child: marker)),
            const SizedBox(width: 8),
            Expanded(child: Text(label, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 13, color: c.ink2))),
            Text(value, style: TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: valueColor ?? c.ink)),
          ],
        ),
      ),
    );
  }
}

/// «Отделы»: ключевая метрика, изменение за неделю, полоса выполнения и статус словами. Сначала требующие внимания.
class DepartmentsCard extends ConsumerWidget {
  const DepartmentsCard({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final fast = ref.watch(homeFastProvider);
    if (fast == null) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [SectionHeader(title: l10n.deptTitle), const SizedBox(height: 12), const _CardSkeleton(rows: 2)],
      );
    }
    final c = context.ob;
    final fmt = context.fmt;
    final filters = ref.watch(homeFiltersProvider);
    final trends = ref.watch(homeTrendsBlock).data;
    final rows = homeDepartments(sales: fast.sales, hr: fast.hr, tasks: fast.tasks, trends: trends).where((d) => filters.includes(d.code)).toList();
    final attention = rows.where((d) => d.attention).length;
    final fine = rows.length - attention;
    final access = ref.watch(dashboardAccessProvider);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(
          title: l10n.deptTitle,
          onMore: access.departments.isEmpty ? null : () => context.go('/home/departments'),
          moreLabel: l10n.actionAll,
          trailing: rows.isEmpty
              ? null
              : Wrap(spacing: 10, children: [
                  if (fine > 0) DotLabel(color: c.ok, label: l10n.deptOkN(fine)),
                  if (attention > 0) DotLabel(color: c.warn, label: l10n.deptAttentionN(attention)),
                ]),
        ),
        const SizedBox(height: 12),
        if (rows.isEmpty)
          _EmptyCard(text: l10n.deptNone)
        else
          ObListCard(
            children: [
              for (var i = 0; i < rows.length; i++)
                Pressable(
                  onTap: () => context.go(rows[i].code == 'sales' ? '/analytics/sales' : '/home/hr'),
                  child: Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
                    child: Column(
                      children: [
                        Row(
                          children: [
                            IconTile(icon: rows[i].code == 'sales' ? LucideIcons.shoppingCart : LucideIcons.users, tone: rows[i].attention ? Tone.warn : Tone.neutral),
                            const SizedBox(width: 12),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(categoryName(l10n, rows[i].code), style: TextStyle(fontSize: 15, fontWeight: FontWeight.w600, color: c.ink)),
                                  Text(departmentMetric(l10n, rows[i].code), maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12, color: c.ink3)),
                                ],
                              ),
                            ),
                            Column(
                              crossAxisAlignment: CrossAxisAlignment.end,
                              children: [
                                Text('${fmt.fixed(rows[i].value)}%', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700, height: 1, color: c.ink)),
                                if (rows[i].trendWeek != null) ...[
                                  const SizedBox(height: 3),
                                  Row(
                                    mainAxisSize: MainAxisSize.min,
                                    children: [
                                      Icon(rows[i].trendWeek! >= 0 ? LucideIcons.trendingUp : LucideIcons.trendingDown,
                                          size: 12, color: rows[i].trendWeek! >= 0 ? c.ok : c.bad),
                                      const SizedBox(width: 3),
                                      Text(
                                        l10n.deptWeekTrend(fmt.signed(rows[i].trendWeek!, rows[i].trendWeek!.abs() < 10 ? 1 : 0)),
                                        style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: rows[i].trendWeek! >= 0 ? c.ok : c.bad),
                                      ),
                                    ],
                                  ),
                                ],
                              ],
                            ),
                          ],
                        ),
                        const SizedBox(height: 10),
                        Row(
                          children: [
                            Expanded(
                              child: ProgressLine(
                                value: rows[i].value / 100,
                                color: rows[i].attention ? c.warn : c.ok,
                                delay: Duration(milliseconds: i * 40),
                              ),
                            ),
                            const SizedBox(width: 12),
                            SoftBadge(label: rows[i].attention ? l10n.statusAttention : l10n.statusOk, tone: rows[i].attention ? Tone.warn : Tone.ok),
                          ],
                        ),
                      ],
                    ),
                  ),
                ),
            ],
          ),
      ],
    );
  }
}

/// «Еженедельный AI-отчёт»: виден, пока отчёт этой недели не открыт; открыли — скрыт до следующей недели (отметка на устройстве).
class ReportBanner extends ConsumerWidget {
  const ReportBanner({super.key});

  static const readKey = 'ai_report_read';

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final alerts = ref.watch(alertsBlock).data;
    final report = weeklyReportOf(alerts, ref.watch(clockProvider)());
    final read = ref.watch(_reportReadProvider);
    if (report == null || read.value == report.id || read.isLoading) return const SizedBox.shrink();
    final c = context.ob;
    final l10n = context.l10n;
    return ObCard(
      tint: c.report,
      padding: const EdgeInsets.all(16),
      onTap: () async {
        await ref.read(settingsStoreProvider).write(readKey, report.id);
        ref.invalidate(_reportReadProvider);
        if (context.mounted) context.go('/home/insights');
      },
      semanticLabel: l10n.reportTitle,
      child: Row(
        children: [
          Container(
            width: 44,
            height: 44,
            decoration: BoxDecoration(color: c.tile, borderRadius: BorderRadius.circular(12)),
            child: Icon(LucideIcons.fileChartColumn, size: 20, color: c.ink),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(l10n.reportTitle, style: TextStyle(fontSize: 14, fontWeight: FontWeight.w700, color: c.ink)),
                const SizedBox(height: 2),
                Text(l10n.reportLine(l10n.risksN(report.risks), l10n.recsN(report.recommendations)),
                    style: TextStyle(fontSize: 12, height: 1.35, color: c.ink2)),
              ],
            ),
          ),
          const SizedBox(width: 12),
          Container(
            width: 36,
            height: 36,
            decoration: BoxDecoration(color: c.arrowBg, shape: BoxShape.circle),
            child: Icon(LucideIcons.arrowRight, size: 18, color: c.arrowFg),
          ),
        ],
      ),
    );
  }
}

final _reportReadProvider = FutureProvider.autoDispose<String?>((ref) => ref.read(settingsStoreProvider).read(ReportBanner.readKey));

/// Нет связи: главная остаётся с последними данными, сверху — «Нет соединения · данные на 14:12».
class OfflineNote extends ConsumerWidget {
  const OfflineNote({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final online = ref.watch(isOnlineProvider).value ?? true;
    if (online) return const SizedBox.shrink();
    final c = context.ob;
    final at = ref.watch(homeStatusProvider).at;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
      decoration: BoxDecoration(color: c.warnSoft, borderRadius: BorderRadius.circular(12)),
      child: Row(
        children: [
          Icon(LucideIcons.wifiOff, size: 16, color: c.warn),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              at == null ? context.l10n.offlineBanner : context.l10n.offlineSince(context.fmt.time(at)),
              style: TextStyle(fontSize: 13, fontWeight: FontWeight.w500, color: c.warn),
            ),
          ),
        ],
      ),
    );
  }
}

/// Свежесть данных внизу: «Данные обновлены 5 мин назад · потяните, чтобы обновить»; не удалось — «Не удалось обновить · Повторить».
class DataStatus extends ConsumerStatefulWidget {
  const DataStatus({super.key, required this.onRetry});

  final Future<void> Function() onRetry;

  @override
  ConsumerState<DataStatus> createState() => _DataStatusState();
}

class _DataStatusState extends ConsumerState<DataStatus> {
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _timer = Timer.periodic(const Duration(seconds: 30), (_) => setState(() {}));
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final l10n = context.l10n;
    final status = ref.watch(homeStatusProvider);
    final online = ref.watch(isOnlineProvider).value ?? true;
    final failed = status.failed || !online;
    final now = ref.watch(clockProvider)();
    final minutes = status.at == null ? 0 : now.difference(status.at!).inMinutes;
    return ConstrainedBox(
      constraints: const BoxConstraints(minHeight: 44),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Container(width: 6, height: 6, decoration: BoxDecoration(color: failed ? c.bad : c.ok, shape: BoxShape.circle)),
          const SizedBox(width: 8),
          if (failed) ...[
            Text('${l10n.dataFailed} · ', style: TextStyle(fontSize: 11, color: c.ink3)),
            Pressable(
              onTap: online ? widget.onRetry : null,
              child: Text(l10n.actionRetry, style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: c.accentStrong)),
            ),
          ] else
            Flexible(
              child: Text(
                '${status.loading ? l10n.dataUpdating : minutes < 1 ? l10n.dataUpdatedNow : l10n.dataUpdatedMin(minutes)} · ${l10n.dataPullHint}',
                textAlign: TextAlign.center,
                style: TextStyle(fontSize: 11, color: c.ink3),
              ),
            ),
        ],
      ),
    );
  }
}

class _EmptyCard extends StatelessWidget {
  const _EmptyCard({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) => ObCard(
        padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 22),
        child: Text(text, textAlign: TextAlign.center, style: TextStyle(fontSize: 14, color: context.ob.ink3)),
      );
}

class _CardSkeleton extends StatelessWidget {
  const _CardSkeleton({required this.rows});

  final int rows;

  @override
  Widget build(BuildContext context) => ObCard(
        semanticLabel: context.l10n.stateLoading,
        child: SkeletonGroup(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Skeleton(width: 120, height: 16),
              for (var i = 0; i < rows; i++) ...[const SizedBox(height: 14), const Skeleton(height: 36)],
            ],
          ),
        ),
      );
}

/// Отступ под карточками главной (как `gap-[20px]` веба).
const homeGap = SizedBox(height: ObSpace.block);
