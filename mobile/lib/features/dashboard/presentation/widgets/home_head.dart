import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:share_plus/share_plus.dart';

import '../../../../app/theme/tokens.dart';
import '../../../../core/core_providers.dart';
import '../../../../core/localization/formatters.dart';
import '../../../../core/navigation/web_links.dart';
import '../../../../l10n/gen/app_localizations.dart';
import '../../../../shared/extensions/context_ext.dart';
import '../../../../shared/widgets/detail_sheet.dart';
import '../../../../shared/widgets/ob_card.dart';
import '../../../../shared/widgets/pills.dart';
import '../../../../shared/widgets/pressable.dart';
import '../../../../shared/widgets/rings.dart';
import '../../../../shared/widgets/skeleton.dart';
import '../../domain/home_view.dart';
import '../dashboard_providers.dart';
import 'home_labels.dart';

/// Приветствие: «ПЯТНИЦА · 20 НОЯБРЯ» с солнцем или луной, «Добрый вечер, {имя}» по времени Ташкента и итог — выполнение плана
/// продаж (нет доступа — явка сегодня) и сколько отделов (из выбранных) требуют внимания.
class HomeGreeting extends ConsumerWidget {
  const HomeGreeting({super.key, required this.name});

  final String name;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.ob;
    final l10n = context.l10n;
    final now = ref.watch(clockProvider)();
    final local = tashkentNow(now);
    final label = DateFormat('EEEE · d MMMM', l10n.localeName).format(DateTime(local.year, local.month, local.day)).toUpperCase();
    final greeting = switch (dayPartOf(now)) {
      DayPart.morning => l10n.greetingMorning,
      DayPart.day => l10n.greetingDay,
      DayPart.evening => l10n.greetingEvening,
      DayPart.night => l10n.greetingNight,
    };
    final fast = ref.watch(homeFastProvider);
    final filters = ref.watch(homeFiltersProvider);
    final trends = ref.watch(homeTrendsBlock).data;
    final view = fast == null ? null : homeView(sales: fast.sales, hr: fast.hr, tasks: fast.tasks);
    final departments = fast == null
        ? const <MobileDepartment>[]
        : homeDepartments(sales: fast.sales, hr: fast.hr, tasks: fast.tasks, trends: trends).where((d) => filters.includes(d.code)).toList();
    final attention = departments.where((d) => d.attention).length;
    final headline = view?.headline;

    return Semantics(
      header: true,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(isNight(now) ? LucideIcons.moon : LucideIcons.sun, size: 13, color: c.warn),
                const SizedBox(width: 6),
                Flexible(
                  child: Text(
                    label,
                    style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, letterSpacing: 0.8, color: c.ink3),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 6),
            Text(name.isEmpty ? greeting : l10n.greetingName(greeting, name), style: context.text.headlineMedium),
            if (headline != null || departments.isNotEmpty) ...[
              const SizedBox(height: 6),
              Text.rich(
                TextSpan(
                  style: TextStyle(fontSize: 14, height: 1.4, color: c.ink3),
                  children: [
                    if (headline != null) ...[
                      TextSpan(text: '${headline.kind == HeroKind.sales ? l10n.homePlanDone : l10n.homeAttendanceToday} '),
                      TextSpan(
                        text: context.fmt.pct1(headline.value),
                        style: TextStyle(fontWeight: FontWeight.w700, color: toneColor(c, headline.tone)),
                      ),
                    ],
                    if (headline != null && departments.isNotEmpty) const TextSpan(text: ' · '),
                    if (departments.isNotEmpty)
                      attention > 0
                          ? TextSpan(text: l10n.homeDeptsAttention(attention), style: TextStyle(fontWeight: FontWeight.w500, color: c.warn))
                          : TextSpan(text: l10n.homeAllDeptsOk, style: TextStyle(fontWeight: FontWeight.w500, color: c.ok)),
                  ],
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

Color toneColor(ObColors c, HomeTone t) => switch (t) {
      HomeTone.success => c.ok,
      HomeTone.warning => c.warn,
      HomeTone.danger => c.bad,
    };

/// «Ждут вас»: согласования на шаге пользователя, «можно получить на складе», принятое за неделю (директору и финансисту).
class HomeAttention extends ConsumerWidget {
  const HomeAttention({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final data = ref.watch(procurementHomeBlock).data;
    if (data == null || !data.hasAttention) return const SizedBox.shrink();
    final c = context.ob;
    final l10n = context.l10n;
    final q = data.requests;
    Widget link(IconData icon, String text, VoidCallback onTap) => Pressable(
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.symmetric(vertical: 4),
            child: Row(
              children: [
                Icon(icon, size: 16, color: c.accentStrong),
                const SizedBox(width: 8),
                Flexible(child: Text(text, style: TextStyle(fontSize: 14, fontWeight: FontWeight.w500, color: c.accentStrong))),
                const SizedBox(width: 6),
                Icon(LucideIcons.arrowRight, size: 14, color: c.accentStrong),
              ],
            ),
          ),
        );
    return ObCard(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(l10n.homeWaiting, style: context.text.titleMedium!.copyWith(fontWeight: FontWeight.w600)),
          const SizedBox(height: 8),
          if (q.awaitingMe > 0) ...[
            link(LucideIcons.gavel, l10n.homeAwaitingApproval(q.awaitingMe), () => context.go('/home/requests?tab=approvals')),
            for (final a in q.approvals.take(3))
              Pressable(
                onTap: () => context.go('/home/requests/${a.id}'),
                child: Padding(
                  padding: const EdgeInsets.symmetric(vertical: 3),
                  child: Row(
                    children: [
                      Expanded(
                        child: Text('${a.number} · ${a.title}', maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 14, color: c.ink)),
                      ),
                      const SizedBox(width: 12),
                      Text(context.fmt.money(a.amount), style: TextStyle(fontSize: 13, color: c.ink2, fontFeatures: const [FontFeature.tabularFigures()])),
                    ],
                  ),
                ),
              ),
            const SizedBox(height: 8),
          ],
          if (data.pickUp > 0) ...[
            link(LucideIcons.packageCheck, l10n.homePickUp(data.pickUp),
                () => context.go(q.departmentToPickUp > 0 ? '/home/requests?tab=department' : '/home/requests?tab=my')),
            Text(q.departmentToPickUp > 0 ? l10n.homePickUpDept : l10n.homePickUpMine, style: TextStyle(fontSize: 14, height: 1.4, color: c.ink2)),
            const SizedBox(height: 8),
          ],
          if (data.showAccepted) ...[
            Row(
              children: [
                Icon(LucideIcons.packageOpen, size: 16, color: c.ink3),
                const SizedBox(width: 8),
                Expanded(child: Text(l10n.homeAcceptedWeek(context.fmt.money(data.acceptedAmount)), style: TextStyle(fontSize: 14, fontWeight: FontWeight.w500, color: c.ink))),
              ],
            ),
            for (final a in data.accepted.take(3))
              Padding(
                padding: const EdgeInsets.only(top: 6),
                child: Text('${a.supplierName}${a.summary == null ? '' : ' · ${a.summary}'}',
                    maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 14, color: c.ink)),
              ),
          ],
        ],
      ),
    );
  }
}

/// Чипы фильтров главной в одну строку с прокруткой: период (7 / 30 дней, квартал, год) и отделы (только доступные пользователю).
class HomeFiltersBar extends ConsumerWidget {
  const HomeFiltersBar({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final f = ref.watch(homeFiltersProvider);
    final departments = ref.watch(dashboardAccessProvider).departments;
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      clipBehavior: Clip.none,
      child: Row(
        children: [
          FilterPill(
            icon: LucideIcons.calendar,
            label: periodLabel(l10n, f.period),
            active: f.period != HomePeriod.d30,
            onTap: () => _pickPeriod(context, ref),
          ),
          if (departments.isNotEmpty) ...[
            const SizedBox(width: 8),
            FilterPill(
              icon: LucideIcons.building2,
              label: f.departments.isEmpty
                  ? l10n.filterAllDepartments
                  : f.departments.length == 1
                      ? categoryName(l10n, f.departments.first)
                      : l10n.filterDepartmentsN(f.departments.length),
              active: f.departments.isNotEmpty,
              onTap: () => _pickDepartments(context, ref, departments),
            ),
          ],
        ],
      ),
    );
  }

  void _pickPeriod(BuildContext context, WidgetRef ref) {
    final current = ref.read(homeFiltersProvider).period;
    showObSheet<void>(
      context,
      title: context.l10n.periodTitle,
      builder: (context) => Column(
        children: [
          for (final p in HomePeriod.values)
            _OptionRow(
              label: periodLabel(context.l10n, p),
              selected: p == current,
              onTap: () {
                ref.read(homeFiltersProvider.notifier).setPeriod(p);
                Navigator.of(context).pop();
              },
            ),
        ],
      ),
    );
  }

  void _pickDepartments(BuildContext context, WidgetRef ref, List<String> codes) {
    showObSheet<void>(
      context,
      title: context.l10n.deptTitle,
      builder: (context) => _DepartmentsPicker(codes: codes),
    );
  }
}

class _DepartmentsPicker extends ConsumerStatefulWidget {
  const _DepartmentsPicker({required this.codes});

  final List<String> codes;

  @override
  ConsumerState<_DepartmentsPicker> createState() => _DepartmentsPickerState();
}

class _DepartmentsPickerState extends ConsumerState<_DepartmentsPicker> {
  late final _selected = {...ref.read(homeFiltersProvider).departments};

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    return Column(
      children: [
        for (final code in widget.codes)
          _OptionRow(
            label: categoryName(l10n, code),
            selected: _selected.contains(code),
            check: true,
            onTap: () => setState(() => _selected.contains(code) ? _selected.remove(code) : _selected.add(code)),
          ),
        const SizedBox(height: 12),
        Row(
          children: [
            Expanded(
              child: OutlinedButton(
                onPressed: () {
                  ref.read(homeFiltersProvider.notifier).setDepartments(const []);
                  Navigator.of(context).pop();
                },
                child: Text(l10n.filterReset),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: FilledButton(
                onPressed: () {
                  ref.read(homeFiltersProvider.notifier).setDepartments([for (final c in widget.codes) if (_selected.contains(c)) c]);
                  Navigator.of(context).pop();
                },
                child: Text(l10n.filterApply),
              ),
            ),
          ],
        ),
      ],
    );
  }
}

class _OptionRow extends StatelessWidget {
  const _OptionRow({required this.label, required this.selected, required this.onTap, this.check = false});

  final String label;
  final bool selected;
  final VoidCallback onTap;
  final bool check;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    return Pressable(
      onTap: onTap,
      selected: selected,
      child: Container(
        constraints: const BoxConstraints(minHeight: 48),
        padding: const EdgeInsets.symmetric(horizontal: 12),
        margin: const EdgeInsets.only(bottom: 4),
        decoration: BoxDecoration(color: selected ? c.accentSoft : Colors.transparent, borderRadius: BorderRadius.circular(ObRadius.md)),
        child: Row(
          children: [
            Expanded(
              child: Text(label,
                  style: TextStyle(fontSize: 15, fontWeight: selected ? FontWeight.w600 : FontWeight.w400, color: selected ? c.accentStrong : c.ink)),
            ),
            if (selected) Icon(LucideIcons.check, size: 18, color: c.accentStrong) else if (check) Icon(LucideIcons.square, size: 18, color: c.subtle),
          ],
        ),
      ),
    );
  }
}

/// Главная карточка: главный показатель крупно (выполнение плана продаж; нет — явка), изменение к прошлому периоду, кольцо цели;
/// ниже — до трёх колонок (прогноз, явка, задачи, явка за месяц, выручка), тап ведёт в раздел.
class KpiHero extends ConsumerWidget {
  const KpiHero({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final fast = ref.watch(homeFastProvider);
    final c = context.ob;
    if (fast == null) return const _HeroSkeleton();
    final view = homeView(sales: fast.sales, hr: fast.hr, tasks: fast.tasks);
    final hero = view.hero;
    if (hero == null && view.stats.isEmpty) return const SizedBox.shrink();
    final l10n = context.l10n;
    final fmt = context.fmt;
    final up = (hero?.delta ?? 0) >= 0;
    return ObCard(
      tint: hero == null ? null : c.kpi,
      radius: hero == null ? ObRadius.lg : ObRadius.xl,
      padding: const EdgeInsets.all(20),
      semanticLabel: hero == null ? null : '${heroLabel(l10n, hero.kind)} ${fmt.pct1(hero.value)}',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (hero != null) ...[
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Pressable(
                    onTap: () => context.go(homeTargetRoute(hero.route)),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(heroLabel(l10n, hero.kind), style: TextStyle(fontSize: 13, fontWeight: FontWeight.w500, color: c.ink3)),
                        const SizedBox(height: 6),
                        CountUp(value: hero.value, format: fmt.pct1, style: context.text.displayLarge),
                        if (hero.delta != null) ...[
                          const SizedBox(height: 12),
                          Wrap(
                            spacing: 8,
                            runSpacing: 4,
                            crossAxisAlignment: WrapCrossAlignment.center,
                            children: [
                              SoftBadge(
                                tone: up ? Tone.ok : Tone.bad,
                                icon: up ? LucideIcons.trendingUp : LucideIcons.trendingDown,
                                size: 12,
                                label: hero.kind == HeroKind.sales ? '${fmt.signed(hero.delta!, 1)}%' : '${fmt.signed(hero.delta!, 1)} ${l10n.unitPp}',
                              ),
                              Text(hero.kind == HeroKind.sales ? l10n.heroVsSameDays : l10n.heroVsPrevDay, style: TextStyle(fontSize: 12, color: c.ink3)),
                            ],
                          ),
                        ],
                      ],
                    ),
                  ),
                ),
                const SizedBox(width: 12),
                GoalRing(
                  value: hero.value,
                  color: toneColor(c, hero.ringTone),
                  caption: hero.kind == HeroKind.sales ? l10n.heroGoal : l10n.heroNorm,
                  target: '${fmt.fixed(hero.target)}%',
                ),
              ],
            ),
            if (view.stats.isNotEmpty) Padding(padding: const EdgeInsets.symmetric(vertical: 18), child: Divider(height: 1, color: c.divider)),
          ],
          if (view.stats.isNotEmpty)
            IntrinsicHeight(
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  for (var i = 0; i < view.stats.length; i++) Expanded(child: _Stat(stat: view.stats[i], first: i == 0)),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

class _Stat extends StatelessWidget {
  const _Stat({required this.stat, required this.first});

  final HomeStat stat;
  final bool first;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final l10n = context.l10n;
    final fmt = context.fmt;
    String format(double v) => switch (stat.format) {
          StatFormat.pct1 => fmt.pct1(v),
          StatFormat.money => fmt.compact(v),
          StatFormat.int => fmt.count(v),
        };
    final sub = statSub(l10n, fmt, stat);
    final subColor = switch (stat.subTone) {
      SubTone.success => c.ok,
      SubTone.danger => c.bad,
      SubTone.link => c.accentStrong,
      SubTone.muted => c.ink3,
    };
    return Pressable(
      onTap: () => context.go(homeTargetRoute(stat.route)),
      child: Container(
        padding: first ? const EdgeInsets.only(right: 8) : const EdgeInsets.only(left: 14),
        decoration: first ? null : BoxDecoration(border: Border(left: BorderSide(color: c.divider))),
        constraints: const BoxConstraints(minHeight: 44),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(statLabel(l10n, stat.kind), maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 11, fontWeight: FontWeight.w500, color: c.ink3)),
            const SizedBox(height: 4),
            FittedBox(
              fit: BoxFit.scaleDown,
              alignment: Alignment.centerLeft,
              child: CountUp(
                value: stat.value,
                format: format,
                style: TextStyle(fontSize: 20, fontWeight: FontWeight.w700, height: 1, letterSpacing: -0.4, color: stat.warning ? c.warn : c.ink),
              ),
            ),
            if (sub != null) ...[
              const SizedBox(height: 4),
              Text(sub, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: subColor)),
            ],
          ],
        ),
      ),
    );
  }
}

class _HeroSkeleton extends StatelessWidget {
  const _HeroSkeleton();

  @override
  Widget build(BuildContext context) => ObCard(
        radius: ObRadius.xl,
        padding: const EdgeInsets.all(20),
        semanticLabel: context.l10n.stateLoading,
        child: const SkeletonGroup(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Skeleton(width: 140, height: 13),
              SizedBox(height: 10),
              Skeleton(width: 160, height: 40),
              SizedBox(height: 14),
              Skeleton(width: 200, height: 18),
              SizedBox(height: 24),
              Skeleton(height: 44),
            ],
          ),
        ),
      );
}

/// Быстрые действия — четыре кнопки в ряд: «Задача» (новая задача), «Спросить AI» (вопрос по текущим фильтрам), «Экспорт» (таблица
/// CSV через «Поделиться»), «Отчёт AI» (находки консультанта). Кнопки без права не показываются.
class QuickActions extends ConsumerWidget {
  const QuickActions({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.ob;
    final l10n = context.l10n;
    final access = ref.watch(dashboardAccessProvider);
    final filters = ref.watch(homeFiltersProvider);
    final scope = filters.departments.isEmpty ? '' : ' (${filters.departments.map((d) => categoryName(l10n, d)).join(', ')})';
    final question = l10n.homeAskQuestion(periodLabel(l10n, filters.period), scope);
    final tiles = <Widget>[
      if (access.tasks)
        _ActionTile(
          icon: LucideIcons.plus,
          label: l10n.quickTask,
          primary: true,
          onTap: () => context.go('/tasks/new'),
        ),
      if (access.ai)
        _ActionTile(
          icon: LucideIcons.sparkles,
          iconColor: c.violet,
          label: l10n.quickAskAi,
          onTap: () => context.go(Uri(path: '/ai', queryParameters: {'ask': question}).toString()),
        ),
      _ActionTile(icon: LucideIcons.fileDown, label: l10n.quickExport, onTap: () => _export(context, ref)),
      if (access.ai) _ActionTile(icon: LucideIcons.fileChartColumn, label: l10n.quickReportAi, onTap: () => context.go('/home/insights')),
    ];
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        for (var i = 0; i < tiles.length; i++) ...[
          if (i > 0) const SizedBox(width: 8),
          Expanded(child: tiles[i]),
        ],
        for (var i = tiles.length; i < 4; i++) ...[const SizedBox(width: 8), const Expanded(child: SizedBox.shrink())],
      ],
    );
  }

  Future<void> _export(BuildContext context, WidgetRef ref) async {
    final l10n = context.l10n;
    final fmt = context.fmt;
    final box = context.findRenderObject() as RenderBox?;
    final fast = ref.read(homeFastProvider);
    final trends = ref.read(homeTrendsBlock).data;
    final period = periodLabel(l10n, ref.read(homeFiltersProvider).period);
    final csv = homeCsv(l10n, fmt, period, fast, trends);
    final bytes = utf8.encode('﻿$csv');
    final name = 'onebase-obzor-${isoDate(tashkentNow(ref.read(clockProvider)()))}.csv';
    await SharePlus.instance.share(ShareParams(
      files: [XFile.fromData(bytes, mimeType: 'text/csv', name: name)],
      fileNameOverrides: [name],
      title: l10n.exportTitle,
      sharePositionOrigin: box == null ? null : box.localToGlobal(Offset.zero) & box.size,
    ));
  }
}

class _ActionTile extends StatelessWidget {
  const _ActionTile({required this.icon, required this.label, required this.onTap, this.primary = false, this.iconColor});

  final IconData icon;
  final String label;
  final VoidCallback onTap;
  final bool primary;
  final Color? iconColor;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final fg = primary ? Colors.white : c.ink;
    return Pressable(
      onTap: onTap,
      haptic: true,
      semanticLabel: label,
      child: Container(
        padding: const EdgeInsets.only(top: 14, bottom: 12, left: 4, right: 4),
        decoration: BoxDecoration(
          color: primary ? null : c.surface,
          gradient: primary
              ? LinearGradient(begin: Alignment.topCenter, end: Alignment.bottomCenter, colors: [c.accentFill, Color.lerp(c.accentFill, Colors.white, 0.12)!])
              : null,
          borderRadius: BorderRadius.circular(ObRadius.tile),
          border: primary ? null : Border.all(color: c.line),
        ),
        child: Column(
          children: [
            Container(
              width: 36,
              height: 36,
              decoration: BoxDecoration(shape: BoxShape.circle, color: primary ? const Color(0x26FFFFFF) : c.muted),
              child: Icon(icon, size: 18, color: primary ? Colors.white : (iconColor ?? c.ink2)),
            ),
            const SizedBox(height: 8),
            Text(label, textAlign: TextAlign.center, maxLines: 2, style: TextStyle(fontSize: 12, height: 1.2, fontWeight: FontWeight.w600, color: fg)),
          ],
        ),
      ),
    );
  }
}

/// Таблица «Экспорт»: показатели, задачи и отделы — «;» и запятая в дробях (как ждёт русский Excel), BOM для кириллицы.
String homeCsv(AppLocalizations l10n, Fmt fmt, String period, HomeFast? fast, HomeTrends? trends) {
  String cell(Object v) {
    final s = v is num ? fmt.decimal(v, digits: v == v.roundToDouble() ? 0 : 1) : v.toString();
    return RegExp(r'[;"\n]').hasMatch(s) ? '"${s.replaceAll('"', '""')}"' : s;
  }

  final rows = <List<Object>>[
    ['OneBase', period],
    [],
    [l10n.homeKeyMetric, ''],
  ];
  if (fast != null) {
    final view = homeView(sales: fast.sales, hr: fast.hr, tasks: fast.tasks);
    if (view.hero != null) rows.add([heroLabel(l10n, view.hero!.kind), '${fmt.fixed(view.hero!.value, 1)}%']);
    for (final s in view.stats) {
      rows.add([
        statLabel(l10n, s.kind),
        switch (s.format) {
          StatFormat.pct1 => '${fmt.fixed(s.value, 1)}%',
          StatFormat.money => fmt.compact(s.value),
          StatFormat.int => fmt.count(s.value),
        },
      ]);
    }
    if (fast.tasks != null) {
      final t = homeTasks(fast.tasks!);
      rows
        ..add([])
        ..add([l10n.navTasks, ''])
        ..addAll([for (final s in t.slices) [taskSliceLabel(l10n, s.$1), s.$2]])
        ..add([l10n.tasksOverdueLabel, t.overdue]);
    }
    final depts = homeDepartments(sales: fast.sales, hr: fast.hr, tasks: fast.tasks, trends: trends);
    if (depts.isNotEmpty) {
      rows
        ..add([])
        ..add([l10n.deptTitle, l10n.homeKeyMetric, '%', l10n.statusAttention]);
      for (final d in depts) {
        rows.add([categoryName(l10n, d.code), departmentMetric(l10n, d.code), d.value, d.attention ? l10n.statusAttention : l10n.statusOk]);
      }
    }
  }
  return rows.map((r) => r.map(cell).join(';')).join('\r\n');
}

/// Переход по ссылке веба из находки или события.
void openWebLink(BuildContext context, String? href) => context.go(appRouteForWeb(href));
