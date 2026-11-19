import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../../app/shell/bottom_nav.dart';
import '../../../app/theme/tokens.dart';
import '../../../core/api/api_exception.dart';
import '../../../core/api/api_providers.dart';
import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/user_profile.dart';
import '../../../core/errors/app_failure.dart';
import '../../../core/permissions/access.dart';
import '../../../shared/extensions/context_ext.dart';
import '../../../shared/widgets/detail_sheet.dart';
import '../../../shared/widgets/ob_app_bar.dart';
import '../../../shared/widgets/pills.dart';
import '../../../shared/widgets/pressable.dart';
import '../../../shared/widgets/states.dart';
import '../../analytics/data/analytics_api.dart';
import '../../analytics/presentation/analytics_providers.dart';
import '../../dashboard/data/dashboard_models.dart';
import '../../dashboard/data/home_repository.dart';
import '../../dashboard/domain/home_view.dart';
import '../domain/departments_view.dart';

String _today() {
  final t = tashkentNow();
  return '${t.year}-${t.month.toString().padLeft(2, '0')}-${t.day.toString().padLeft(2, '0')}';
}

Load<D> _loadError<D>(Object e) => AppFailure.from(e).kind == ApiErrorKind.forbidden ? Forbidden<D>() : Failed<D>();

/// Продажи месяца: текущий — общий с главной кэш `/api/sales/overview` (минута), выбранный — этот месяц.
final deptSalesProvider = FutureProvider.autoDispose.family<Load<SalesData>, DeptMonth>((ref, m) async {
  try {
    final o = m.current
        ? await ref.read(homeRepositoryProvider).overview()
        : await ref.read(analyticsApiProvider).overview(SalesMonth(year: m.year, month: m.month), CancelToken());
    return Loaded((
      year: o.period.year,
      month: o.period.month,
      closed: o.period.closed,
      planKg: o.kpi.planKg,
      factKg: o.kpi.factKg,
      execution: o.kpi.execution,
      forecastExecution: o.kpi.forecastExecution,
    ));
  } on Object catch (e) {
    return _loadError(e);
  }
});

/// Явка за месяц (`/api/hr/reports`) и, у текущего месяца, сегодня (`/api/hr/overview`: на месте от «по графику»).
final deptHrProvider = FutureProvider.autoDispose.family<Load<HrData>, DeptMonth>((ref, m) async {
  final repo = ref.read(homeRepositoryProvider);
  final range = monthRange(m, _today());
  Object? reportError;
  Object? todayError;
  double? rate;
  double? present;
  var reportOk = false;
  var todayOk = false;
  await Future.wait([
    () async {
      try {
        rate = await repo.attendance(range.from, range.to, past: !m.current);
        reportOk = true;
      } on Object catch (e) {
        reportError = e;
      }
    }(),
    if (m.current)
      () async {
        try {
          final o = await repo.hrOverview(CancelToken());
          present = o.scheduled > 0 ? 100 * o.present / o.scheduled : null;
          todayOk = true;
        } on Object catch (e) {
          todayError = e;
        }
      }(),
  ]);
  if (!reportOk && !todayOk) {
    final errors = [?reportError, ?todayError];
    return errors.any((e) => _loadError<HrData>(e) is Failed) ? const Failed() : const Forbidden();
  }
  return Loaded((monthRate: rate, presentPct: present));
});

/// Снабжение — заявки к закупке на сегодня (`/api/procurement/board`, только путь закупки).
final deptSupplyProvider = FutureProvider.autoDispose<Load<SupplyData>>((ref) async {
  final today = _today();
  try {
    final p = await ref.read(apiClientProvider).getExtract('/api/procurement/board', (j) {
      final pipe = (j! as Map<String, Object?>)['pipeline'] as Map<String, Object?>?;
      return (
        toBuy: (pipe?['requestsAwaitingOrder'] as num?)?.toInt(),
        overdue: (pipe?['overdueToBuy'] as num?)?.toInt(),
        onApproval: (pipe?['onApprovalCount'] as num?)?.toInt(),
      );
    }, query: {'from': '${today.substring(0, 8)}01', 'to': today});
    return Loaded(p);
  } on Object catch (e) {
    return _loadError(e);
  }
});

/// Куда ведёт карточка отдела (как `departmentHref` веба): свой раздел, иначе задачи отдела.
String? departmentRoute(String code, UserProfile? me, List<String> perms) {
  final own = Access.departmentSections[code];
  if (own != null && Access.canOpen(own, perms)) {
    return switch (code) {
      'sales' => salesHomeRoute(me?.salesScope?.home),
      'hr' => '/home/hr',
      'supply' => '/home/warehouse',
      'production' => '/home/production',
      _ => '/home/finance',
    };
  }
  if (Access.canOpen(Access.tasks, perms)) return '/tasks?department=$code';
  return null;
}

/// «Отделы»: вход в разделы отделов и их показатели за месяц. Видны только отделы, открытые пользователю (свой раздел или задачи
/// отдела). Настоящие показатели — у продаж (план месяца), HR (явка) и снабжения (заявки к закупке, только сегодня); у производства,
/// финансов и маркетинга источников пока нет — «Нет данных». Каждая карточка грузится отдельно: сбой одной — «Повторить» только у неё.
class DepartmentsScreen extends ConsumerStatefulWidget {
  const DepartmentsScreen({super.key, this.month});

  /// «2026-11» — из ссылки веба `?month=`.
  final String? month;

  @override
  ConsumerState<DepartmentsScreen> createState() => _DepartmentsScreenState();
}

class _DepartmentsScreenState extends ConsumerState<DepartmentsScreen> {
  late DeptMonth _month = parseMonth(widget.month, _today());

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final fmt = context.fmt;
    final me = ref.watch(currentProfileProvider);
    final perms = Access.screenPermissions(me);
    bool open(String code) => Access.departmentOpen(code, perms, me?.departments ?? const []);
    bool can(List<String> need) => me != null && Access.canOpen(need, perms);
    final ctx = DeptCtx(l10n, fmt, month: fmt.monthYear(_month.year, _month.month).toLowerCase(), current: _month.current);

    DeptCard card(String code) => switch (code) {
      'sales' => salesCard(can(Access.sales) ? _value(ref.watch(deptSalesProvider(_month))) : const Forbidden(), ctx),
      'hr' => hrCard(can(Access.hr) ? _value(ref.watch(deptHrProvider(_month))) : const Forbidden(), ctx),
      'supply' => supplyCard(can(Access.warehouse) && _month.current ? _value(ref.watch(deptSupplyProvider)) : const Forbidden(), ctx),
      _ => noDataCard(code, ctx),
    };

    final codes = departmentCodes.where(open).toList();
    return ObPage(
      title: l10n.moduleDepartments,
      onRefresh: () async {
        await ref.read(homeRepositoryProvider).invalidateLive();
        ref
          ..invalidate(deptSalesProvider)
          ..invalidate(deptHrProvider)
          ..invalidate(deptSupplyProvider);
      },
      slivers: [
        SliverToBoxAdapter(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(0, 4, 0, 12),
            child: Align(
              alignment: Alignment.centerLeft,
              child: FilterPill(
                icon: LucideIcons.calendar,
                label: fmt.monthYear(_month.year, _month.month),
                onTap: () => showObSheet<void>(
                  context,
                  title: l10n.anMonthPick,
                  builder: (sheet) => Column(
                    children: [
                      for (final m in monthOptions(_today()))
                        Pressable(
                          onTap: () {
                            setState(() => _month = m);
                            Navigator.pop(sheet);
                          },
                          child: Container(
                            height: 48,
                            alignment: Alignment.centerLeft,
                            padding: const EdgeInsets.symmetric(horizontal: 12),
                            decoration: BoxDecoration(
                              color: m == _month ? context.ob.accentSoft : null,
                              borderRadius: BorderRadius.circular(ObRadius.md),
                            ),
                            child: Text(
                              fmt.monthYear(m.year, m.month),
                              style: TextStyle(fontSize: 15, fontWeight: m == _month ? FontWeight.w700 : FontWeight.w500, color: context.ob.ink),
                            ),
                          ),
                        ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
        if (codes.isEmpty)
          SliverFillRemaining(
            hasScrollBody: false,
            child: EmptyView(icon: LucideIcons.grid3x3, title: l10n.dpNone),
          )
        else
          SliverPadding(
            padding: EdgeInsets.only(bottom: navContentInset(context)),
            sliver: SliverList.separated(
              itemCount: codes.length,
              separatorBuilder: (context, i) => const SizedBox(height: 12),
              itemBuilder: (context, i) {
                final code = codes[i];
                final route = departmentRoute(code, me, perms);
                return DeptCardView(
                  card: card(code),
                  onTap: route == null ? null : () => context.push(route),
                  onRetry: () => switch (code) {
                    'sales' => ref.invalidate(deptSalesProvider(_month)),
                    'hr' => ref.invalidate(deptHrProvider(_month)),
                    _ => ref.invalidate(deptSupplyProvider),
                  },
                );
              },
            ),
          ),
      ],
    );
  }

  /// Пока грузится — карточка без числа (как «нет доступа»): каркас не прыгает.
  Load<D> _value<D>(AsyncValue<Load<D>> v) => v.value ?? Forbidden<D>();
}

IconData _deptIcon(String code) => switch (code) {
  'production' => LucideIcons.factory,
  'finance' => LucideIcons.wallet,
  'sales' => LucideIcons.shoppingCart,
  'marketing' => LucideIcons.megaphone,
  'supply' => LucideIcons.package,
  'hr' => LucideIcons.users,
  _ => LucideIcons.building2,
};

/// Карточка отдела — мобильная карточка веба: значок отдела, название и показатель, крупное значение со статусом, полоса, строка деталей.
class DeptCardView extends StatelessWidget {
  const DeptCardView({super.key, required this.card, this.onTap, required this.onRetry});

  final DeptCard card;
  final VoidCallback? onTap;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final c = context.ob;
    final (tone, statusLabel, statusIcon) = switch (card.status) {
      DeptStatus.done => (Tone.ok, l10n.dpStatusDone, LucideIcons.badgeCheck),
      DeptStatus.ok => (Tone.ok, l10n.dpStatusOk, LucideIcons.circleCheck),
      DeptStatus.attention => (Tone.warn, l10n.dpStatusAttention, LucideIcons.triangleAlert),
      DeptStatus.none => (Tone.neutral, l10n.dpStatusNone, LucideIcons.circleDashed),
      DeptStatus.error => (Tone.bad, l10n.dpStatusError, LucideIcons.circleX),
    };
    final fg = c.toneFg(tone);
    final bg = c.toneBg(tone);
    final fill = tone == Tone.neutral ? c.ink3 : fg;
    final head = Row(
      children: [
        Container(
          width: 38,
          height: 38,
          decoration: BoxDecoration(color: tone == Tone.warn || tone == Tone.bad ? bg : c.muted, borderRadius: BorderRadius.circular(ObRadius.md)),
          child: Icon(
            _deptIcon(card.code),
            size: 19,
            color: tone == Tone.warn || tone == Tone.bad
                ? fg
                : tone == Tone.neutral
                ? c.ink3
                : c.ink,
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                card.name,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700, color: c.ink),
              ),
              Text(
                card.status == DeptStatus.error ? l10n.dpLoadFailed : card.label,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontSize: 12, color: c.ink3),
              ),
            ],
          ),
        ),
        const SizedBox(width: 8),
        Column(
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            Text(
              card.value == null ? '—' : '${card.value}%',
              style: TextStyle(
                fontSize: 22,
                height: 1,
                fontWeight: FontWeight.w700,
                color: c.ink,
                fontFeatures: const [FontFeature.tabularFigures()],
              ),
            ),
            const SizedBox(height: 6),
            SoftBadge(label: statusLabel, tone: tone, icon: statusIcon),
          ],
        ),
      ],
    );
    final border = switch (tone) {
      Tone.warn => c.warn.withValues(alpha: 0.25),
      Tone.bad => c.bad.withValues(alpha: 0.25),
      _ => c.line,
    };
    final decoration = BoxDecoration(
      color: c.surface,
      borderRadius: BorderRadius.circular(ObRadius.lg),
      border: Border.all(color: border),
    );
    if (card.status == DeptStatus.error) {
      return Container(
        padding: const EdgeInsets.all(16),
        decoration: decoration,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            head,
            const SizedBox(height: 12),
            OutlinedButton.icon(onPressed: onRetry, icon: const Icon(LucideIcons.refreshCw, size: 16), label: Text(l10n.actionRetry)),
          ],
        ),
      );
    }
    final bar = card.bar;
    return Pressable(
      onTap: onTap,
      semanticLabel: '${card.name}, ${card.label} ${card.value == null ? '—' : '${card.value}%'}, $statusLabel',
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: decoration,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            head,
            const SizedBox(height: 12),
            ClipRRect(
              borderRadius: BorderRadius.circular(3),
              child: SizedBox(
                height: 6,
                child: LayoutBuilder(
                  builder: (context, box) => Stack(
                    children: [
                      Positioned.fill(child: ColoredBox(color: c.muted)),
                      if (bar?.forecast != null)
                        Positioned(
                          left: 0,
                          top: 0,
                          bottom: 0,
                          width: box.maxWidth * bar!.forecast! / 100,
                          child: DecoratedBox(
                            decoration: BoxDecoration(color: fill.withValues(alpha: 0.3), borderRadius: BorderRadius.circular(3)),
                          ),
                        ),
                      if (bar != null)
                        Positioned(
                          left: 0,
                          top: 0,
                          bottom: 0,
                          width: box.maxWidth * bar.value / 100,
                          child: DecoratedBox(
                            decoration: BoxDecoration(color: fill, borderRadius: BorderRadius.circular(3)),
                          ),
                        ),
                    ],
                  ),
                ),
              ),
            ),
            if (card.detail != null) ...[
              const SizedBox(height: 12),
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Padding(
                    padding: const EdgeInsets.only(top: 1),
                    child: Icon(
                      switch (card.detailIcon) {
                        DeptDetailIcon.cart => LucideIcons.shoppingCart,
                        DeptDetailIcon.attendance => LucideIcons.userCheck,
                        DeptDetailIcon.sales => LucideIcons.shoppingBag,
                        DeptDetailIcon.info => LucideIcons.info,
                      },
                      size: 14,
                      color: c.ink3,
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      card.detail!,
                      style: TextStyle(fontSize: 12, height: 1.35, fontWeight: FontWeight.w500, color: c.ink2),
                    ),
                  ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }
}
