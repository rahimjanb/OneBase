import 'dart:math' as math;

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/api/api_providers.dart';
import '../../../core/cache/cached_block.dart';
import '../../../core/cache/paged.dart';
import '../../../core/utils/debouncer.dart';
import '../../../l10n/gen/app_localizations.dart';
import '../../../shared/extensions/context_ext.dart';
import '../../../shared/widgets/ob_app_bar.dart';
import '../../../shared/widgets/ob_card.dart';
import '../../../shared/widgets/pills.dart';
import '../../../shared/widgets/rings.dart';
import '../../../shared/widgets/section_header.dart';
import '../../../shared/widgets/states.dart';
import '../../analytics/presentation/analytics_screen.dart';
import '../../analytics/presentation/widgets/sales_widgets.dart';
import '../data/hr_models.dart';

// ───────────── Данные ─────────────

class HrDayBlock extends CachedBlock<HrDay> {
  @override
  String get cacheKey => 'hr/day';

  /// В «Требует внимания» — имена сотрудников: на телефоне не сохраняется.
  @override
  bool get persist => false;

  @override
  Future<HrDay> fetch(CancelToken cancel) =>
      ref.read(apiClientProvider).get('/api/hr/overview', (j) => HrDay.fromJson(j! as Map<String, Object?>), cancel: cancel);

  @override
  HrDay decode(Object? json) => HrDay.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(HrDay data) => data.toJson();
}

final hrDayBlock = NotifierProvider.autoDispose<HrDayBlock, BlockState<HrDay>>(HrDayBlock.new);

class EmployeesController extends PagedController<HrEmployeeRow> {
  EmployeesController(this.search);

  final String search;

  @override
  Future<PageChunk<HrEmployeeRow>> fetchPage(int page, String? cursor, CancelToken cancel) => ref.read(apiClientProvider).get(
        '/api/hr/employees',
        (j) {
          final m = j! as Map<String, Object?>;
          final total = m['total'] as int? ?? 0;
          return PageChunk([for (final e in m['items']! as List<Object?>) HrEmployeeRow.fromJson(e! as Map<String, Object?>)], hasMore: page * 50 < total);
        },
        query: {'search': search.trim().isEmpty ? null : search.trim(), 'status': 'active', 'sort': 'name', 'page': page, 'pageSize': 50},
        cancel: cancel,
      );
}

final employeesProvider = NotifierProvider.autoDispose.family<EmployeesController, PagedState<HrEmployeeRow>, String>(EmployeesController.new);

class EmployeeBlock extends CachedBlock<HrEmployeeCard> {
  EmployeeBlock(this.id);

  final String id;

  @override
  String get cacheKey => 'hr/employee/$id';

  @override
  bool get persist => false;

  @override
  Future<HrEmployeeCard> fetch(CancelToken cancel) => ref.read(apiClientProvider).getExtract('/api/hr/employees/$id', hrCardOf, cancel: cancel);

  @override
  HrEmployeeCard decode(Object? json) => HrEmployeeCard.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(HrEmployeeCard data) => data.toJson();
}

final employeeBlock = NotifierProvider.autoDispose.family<EmployeeBlock, BlockState<HrEmployeeCard>, String>(EmployeeBlock.new);

String dayStatusName(AppLocalizations l10n, String? s) => switch (s) {
      'Present' => l10n.hrDay_Present,
      'Absent' => l10n.hrDay_Absent,
      'OnLeave' => l10n.hrDay_OnLeave,
      'DayOff' => l10n.hrDay_DayOff,
      'Expected' => l10n.hrDay_Expected,
      'NoSchedule' => l10n.hrDay_NoSchedule,
      'Unknown' => l10n.hrDay_Unknown,
      'NotEmployed' => l10n.hrDay_NotEmployed,
      _ => '—',
    };

Tone dayTone(String? s) => switch (s) {
      'Present' => Tone.ok,
      'Absent' => Tone.bad,
      'OnLeave' => Tone.violet,
      'Expected' => Tone.accent,
      'Unknown' => Tone.warn,
      _ => Tone.neutral,
    };

String _hm(String? iso) => iso == null || iso.length < 16 ? '—' : iso.substring(11, 16);

// ───────────── Раздел ─────────────

/// «HR»: явка сегодня (вовремя, опоздали, отсутствуют, по документу, ожидаются, не ясно), средний приход, проходы по часам, требует
/// внимания, подразделения; сотрудники с поиском → карточка (итоги и дни за 14 дней).
class HrScreen extends ConsumerStatefulWidget {
  const HrScreen({super.key, this.employees = false});

  final bool employees;

  @override
  ConsumerState<HrScreen> createState() => _HrScreenState();
}

class _HrScreenState extends ConsumerState<HrScreen> {
  late bool _employees = widget.employees;
  String _search = '';
  final _debounce = Debouncer();
  final _scroll = ScrollController();

  @override
  void initState() {
    super.initState();
    _scroll.addListener(() {
      if (_employees && _scroll.position.extentAfter < 500) ref.read(employeesProvider(_search).notifier).loadMore();
    });
  }

  @override
  void dispose() {
    _debounce.cancel();
    _scroll.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final c = context.ob;
    return ObPage(
      title: l10n.moduleHr,
      controller: _scroll,
      onRefresh: () => _employees ? ref.read(employeesProvider(_search).notifier).refresh() : ref.read(hrDayBlock.notifier).refresh(),
      slivers: [
        SliverToBoxAdapter(
          child: Padding(
            padding: const EdgeInsets.only(bottom: 12),
            child: Row(children: [
              FilterPill(label: l10n.hrTabOverview, chevron: false, active: !_employees, onTap: () => setState(() => _employees = false)),
              const SizedBox(width: 8),
              FilterPill(label: l10n.hrTabEmployees, chevron: false, active: _employees, onTap: () => setState(() => _employees = true)),
            ]),
          ),
        ),
        if (!_employees)
          const SliverToBoxAdapter(child: _HrOverview())
        else ...[
          SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: TextField(
                decoration: InputDecoration(hintText: l10n.hrSearchEmp, prefixIcon: const Icon(LucideIcons.search, size: 18), isDense: true),
                onChanged: (v) => _debounce(() => setState(() => _search = v)),
              ),
            ),
          ),
          () {
            final s = ref.watch(employeesProvider(_search));
            if (s.isInitialLoading) return const SliverToBoxAdapter(child: LoadingView());
            if (s.items.isEmpty && s.failure != null) {
              return SliverToBoxAdapter(child: ErrorView(failure: s.failure!, onRetry: () => ref.read(employeesProvider(_search).notifier).refresh()));
            }
            return SliverList.separated(
              itemCount: s.items.length,
              separatorBuilder: (context, i) => const SizedBox(height: 8),
              itemBuilder: (context, i) {
                final e = s.items[i];
                final name = displayName(e.fullName);
                return ObCard(
                  onTap: () => context.push('/home/hr/employees/${e.id}'),
                  padding: const EdgeInsets.all(12),
                  child: Row(children: [
                    CircleAvatar(
                      radius: 20,
                      backgroundColor: c.accentSoft,
                      child: Text(name.split(' ').take(2).map((p) => p[0]).join(), style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: c.accentStrong)),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                        Text(name, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: c.ink)),
                        Text([?e.job, ?e.division].join(' · '), maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12, color: c.ink3)),
                      ]),
                    ),
                    if (e.today != null) SoftBadge(label: e.todayLate ? l10n.hrLateN : dayStatusName(l10n, e.today), tone: e.todayLate ? Tone.warn : dayTone(e.today)),
                  ]),
                );
              },
            );
          }(),
        ],
      ],
    );
  }
}

class _HrOverview extends ConsumerWidget {
  const _HrOverview();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final c = context.ob;
    final fmt = context.fmt;
    final s = ref.watch(hrDayBlock);
    return blockView(context, s, onRetry: () => ref.read(hrDayBlock.notifier).refresh(), (d) {
      final pct = d.scheduled > 0 ? 100 * d.present / d.scheduled : 0.0;
      final tiles = [
        (l10n.hrOnTime, d.present - d.late, Tone.ok),
        (l10n.hrLateN, d.late, Tone.warn),
        (l10n.hrAbsentN, d.absent, Tone.bad),
        (l10n.hrLeaveN, d.onLeave, Tone.violet),
        (l10n.hrExpectedN, d.expected, Tone.accent),
        (l10n.hrUnknownN, d.unknown, Tone.neutral),
      ];
      final maxHour = d.passesByHour.fold<int>(0, math.max);
      return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        ObCard(
          tint: c.kpi,
          radius: ObRadius.xl,
          padding: const EdgeInsets.all(20),
          child: Row(children: [
            Expanded(
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text(l10n.homeAttendanceToday, style: TextStyle(fontSize: 13, color: c.ink3)),
                const SizedBox(height: 6),
                Text(fmt.pct1(pct), style: context.text.displayMedium),
                Text(l10n.hrPresentOf('${d.present}', '${d.scheduled}'), style: TextStyle(fontSize: 12, color: c.ink3)),
                if (d.avgArrival != null) Text('${l10n.hrAvgArrival}: ${d.avgArrival!.substring(0, 5)}', style: TextStyle(fontSize: 12, color: c.ink3)),
              ]),
            ),
            GoalRing(value: pct, color: pct >= 80 ? c.ok : pct >= 70 ? c.warn : c.bad, caption: l10n.heroNorm, target: '80%'),
          ]),
        ),
        const SizedBox(height: 12),
        GridView.count(
          crossAxisCount: 3,
          padding: EdgeInsets.zero,
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          mainAxisSpacing: 8,
          crossAxisSpacing: 8,
          childAspectRatio: 1.45,
          children: [
            for (final (label, n, tone) in tiles)
              ObCard(
                padding: const EdgeInsets.all(10),
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
                  Text(label, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 11, color: c.ink3)),
                  Text('$n', style: TextStyle(fontSize: 20, fontWeight: FontWeight.w700, color: n > 0 && tone != Tone.ok ? c.toneFg(tone) : c.ink)),
                ]),
              ),
          ],
        ),
        if (maxHour > 0) ...[
          const SizedBox(height: ObSpace.block),
          SectionHeader(title: l10n.hrByHour),
          const SizedBox(height: 10),
          ObCard(child: DailyBars(values: [for (final v in d.passesByHour) v.toDouble()], format: fmt.count, height: 70)),
        ],
        if (d.attention.isNotEmpty) ...[
          const SizedBox(height: ObSpace.block),
          SectionHeader(title: l10n.hrAttention, trailing: Text('${d.attentionTotal}', style: TextStyle(fontSize: 13, color: c.ink3))),
          const SizedBox(height: 10),
          ObListCard(children: [
            for (final a in d.attention)
              InkWell(
                onTap: () => context.push('/home/hr/employees/${a.employeeId}'),
                child: Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
                  child: Row(children: [
                    Expanded(
                      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                        Text(displayName(a.employee), maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 14, color: c.ink)),
                        if (a.division != null) Text(a.division!, style: TextStyle(fontSize: 11, color: c.ink3)),
                      ]),
                    ),
                    SoftBadge(
                      label: switch (a.kind) {
                        'late' => l10n.hrLateMin(a.lateMinutes ?? 0),
                        'absent' => l10n.hrDay_Absent,
                        _ => l10n.hrDay_Unknown,
                      },
                      tone: a.kind == 'absent' ? Tone.bad : Tone.warn,
                    ),
                  ]),
                ),
              ),
          ]),
        ],
        if (d.divisions.isNotEmpty) ...[
          const SizedBox(height: ObSpace.block),
          SectionHeader(title: l10n.hrDivisions),
          const SizedBox(height: 10),
          ObListCard(children: [
            for (final dv in d.divisions.where((x) => x.scheduled > 0))
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Row(children: [
                    Expanded(child: Text(dv.name, style: TextStyle(fontSize: 14, color: c.ink))),
                    Text('${dv.present}/${dv.scheduled}', style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: c.ink)),
                  ]),
                  const SizedBox(height: 6),
                  ProgressLine(value: dv.present / dv.scheduled, color: dv.present / dv.scheduled >= 0.8 ? c.ok : c.warn, height: 4),
                ]),
              ),
          ]),
        ],
      ]);
    });
  }
}

/// Карточка сотрудника: статус сегодня, итоги 14 дней (пришёл, опоздания, отсутствия, отработано), дни (приход, уход, опоздание).
class EmployeeScreen extends ConsumerWidget {
  const EmployeeScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final c = context.ob;
    final s = ref.watch(employeeBlock(id));
    final e = s.data?.employee;
    return ObPage(
      title: e == null ? l10n.moduleHr : displayName(e.fullName),
      subtitle: e == null ? null : Text([?e.job, ?e.division].join(' · ')),
      onRefresh: () => ref.read(employeeBlock(id).notifier).refresh(),
      slivers: [
        SliverToBoxAdapter(
          child: blockView(context, s, onRetry: () => ref.read(employeeBlock(id).notifier).refresh(), (card) {
            final sm = card.summary;
            String hours(int m) => l10n.myDayHoursMinutes(m ~/ 60, m % 60);
            return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              if (card.employee.schedule != null) ...[
                Text(card.employee.schedule!, style: TextStyle(fontSize: 12, color: c.ink3)),
                const SizedBox(height: 10),
              ],
              ObCard(
                tint: c.kpi,
                child: MetricRow(items: [
                  (l10n.hrPeriod14, l10n.hrSummaryPresent(sm.presentDays, sm.planDays), null),
                  (l10n.hrSummaryLate, '${sm.lateDays}', sm.lateMinutes > 0 ? hours(sm.lateMinutes) : null),
                  (l10n.hrSummaryAbsent, '${sm.absentDays}', null),
                ]),
              ),
              const SizedBox(height: 10),
              ObCard(child: MetricRow(items: [(l10n.hrWorked, hours(sm.workedMinutes), sm.planMinutes > 0 ? '/ ${hours(sm.planMinutes)}' : null)])),
              const SizedBox(height: ObSpace.block),
              SectionHeader(title: l10n.hrDays),
              const SizedBox(height: 10),
              ObListCard(children: [
                for (final d in card.days.where((d) => d.status != 'NotEmployed'))
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
                    child: Row(children: [
                      SizedBox(width: 46, child: Text('${d.date.substring(8, 10)}.${d.date.substring(5, 7)}', style: TextStyle(fontSize: 13, color: c.ink2))),
                      Expanded(
                        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                          Text('${_hm(d.firstIn)} – ${_hm(d.lastOut)}', style: TextStyle(fontSize: 13, color: c.ink)),
                          if (d.reason != null || d.leaveName != null) Text(d.leaveName ?? d.reason!, style: TextStyle(fontSize: 11, color: c.ink3)),
                        ]),
                      ),
                      SoftBadge(label: d.late ? l10n.hrLateMin(d.lateMinutes ?? 0) : dayStatusName(l10n, d.status), tone: d.late ? Tone.warn : dayTone(d.status)),
                    ]),
                  ),
              ]),
            ]);
          }),
        ),
      ],
    );
  }
}
