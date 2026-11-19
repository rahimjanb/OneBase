import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/api/api_exception.dart';
import '../../../core/cache/cached_block.dart';
import '../../../core/cache/paged.dart';
import '../../../core/errors/app_failure.dart';
import '../../../l10n/gen/app_localizations.dart';
import '../../../shared/extensions/context_ext.dart';
import '../../../shared/widgets/ob_app_bar.dart';
import '../../../shared/widgets/ob_card.dart';
import '../../../shared/widgets/pills.dart';
import '../../../shared/widgets/section_header.dart';
import '../../../shared/widgets/states.dart';
import '../../analytics/presentation/analytics_screen.dart';
import '../../analytics/presentation/widgets/sales_widgets.dart';
import '../data/production_api.dart';
import '../data/production_models.dart';

ProductionApi _api(Ref ref) => ref.read(productionApiProvider);

class TechBlock extends CachedBlock<ProductionDashboard> {
  @override
  String get cacheKey => 'production/technologist';

  @override
  Future<ProductionDashboard> fetch(CancelToken cancel) => _api(ref).technologist(cancel);

  @override
  ProductionDashboard decode(Object? json) => ProductionDashboard.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(ProductionDashboard data) => data.toJson();
}

final techBlock = NotifierProvider.autoDispose<TechBlock, BlockState<ProductionDashboard>>(TechBlock.new);

class ProdOverviewBlock extends CachedBlock<ProductionOverview> {
  @override
  String get cacheKey => 'production/overview';

  @override
  Future<ProductionOverview> fetch(CancelToken cancel) => _api(ref).overview(cancel);

  @override
  ProductionOverview decode(Object? json) => ProductionOverview.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(ProductionOverview data) => data.toJson();
}

final prodOverviewBlock = NotifierProvider.autoDispose<ProdOverviewBlock, BlockState<ProductionOverview>>(ProdOverviewBlock.new);

class OrdersController extends PagedController<ProductionOrderRow> {
  OrdersController(this.status);

  final String? status;

  @override
  Future<PageChunk<ProductionOrderRow>> fetchPage(int page, String? cursor, CancelToken cancel) => _api(ref).orders(status, page, cancel);
}

final ordersProvider = NotifierProvider.autoDispose.family<OrdersController, PagedState<ProductionOrderRow>, String?>(OrdersController.new);

class ShiftsController extends PagedController<ShiftRow> {
  @override
  Future<PageChunk<ShiftRow>> fetchPage(int page, String? cursor, CancelToken cancel) => _api(ref).shifts(null, page, cancel);
}

final shiftsProvider = NotifierProvider.autoDispose<ShiftsController, PagedState<ShiftRow>>(ShiftsController.new);

class OrderBlock extends CachedBlock<ProductionOrderDetail> {
  OrderBlock(this.id);

  final String id;

  @override
  String get cacheKey => 'production/order/$id';

  @override
  Future<ProductionOrderDetail> fetch(CancelToken cancel) => _api(ref).order(id, cancel);

  @override
  ProductionOrderDetail decode(Object? json) => ProductionOrderDetail.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(ProductionOrderDetail data) => data.toJson();
}

final orderBlock = NotifierProvider.autoDispose.family<OrderBlock, BlockState<ProductionOrderDetail>, String>(OrderBlock.new);

String orderStatusLabel(AppLocalizations l10n, String s) => switch (s) {
      'Draft' => l10n.prStatus_Draft,
      'Released' => l10n.prStatus_Released,
      'InProgress' => l10n.prStatus_InProgress,
      'Completed' => l10n.prStatus_Completed,
      'Cancelled' => l10n.prStatus_Cancelled,
      _ => s,
    };

Tone orderStatusTone(String s) => switch (s) {
      'Draft' => Tone.neutral,
      'Released' => Tone.accent,
      'InProgress' => Tone.violet,
      'Completed' => Tone.ok,
      _ => Tone.neutral,
    };

String shiftStatusLabel(AppLocalizations l10n, String s) => switch (s) {
      'Draft' => l10n.prShiftStatus_Draft,
      'Closed' => l10n.prShiftStatus_Closed,
      'Approved' => l10n.prShiftStatus_Approved,
      'Superseded' => l10n.prShiftStatus_Superseded,
      'Cancelled' => l10n.prShiftStatus_Cancelled,
      _ => s,
    };

String _d(String iso) => iso.length >= 10 ? '${iso.substring(8, 10)}.${iso.substring(5, 7)}' : iso;

/// Ссылки проблем технолога (пути веба) → экраны раздела.
String productionRoute(String? link) {
  final s = Uri.tryParse(link ?? '')?.pathSegments ?? const [];
  if (s.length >= 3 && s[1] == 'orders') return '/home/production/orders/${s[2]}';
  return '/home/production';
}

enum _ProdTab { overview, orders, shifts }

/// «Производство»: выпуск, план и брак (технолог), сырьё, сроки и запас; задания на выпуск (→ карточка с действиями по правам); смены.
class ProductionScreen extends ConsumerStatefulWidget {
  const ProductionScreen({super.key});

  @override
  ConsumerState<ProductionScreen> createState() => _ProductionScreenState();
}

class _ProductionScreenState extends ConsumerState<ProductionScreen> {
  _ProdTab _tab = _ProdTab.overview;
  String? _status = 'open';
  final _scroll = ScrollController();

  @override
  void initState() {
    super.initState();
    _scroll.addListener(() {
      if (_scroll.position.extentAfter > 500) return;
      if (_tab == _ProdTab.orders) ref.read(ordersProvider(_status).notifier).loadMore();
      if (_tab == _ProdTab.shifts) ref.read(shiftsProvider.notifier).loadMore();
    });
  }

  @override
  void dispose() {
    _scroll.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final tabs = {_ProdTab.overview: l10n.prTabOverview, _ProdTab.orders: l10n.prTabOrders, _ProdTab.shifts: l10n.prTabShifts};
    return ObPage(
      title: l10n.moduleProduction,
      controller: _scroll,
      onRefresh: () async {
        switch (_tab) {
          case _ProdTab.overview:
            await Future.wait([ref.read(techBlock.notifier).refresh(), ref.read(prodOverviewBlock.notifier).refresh()]);
          case _ProdTab.orders:
            await ref.read(ordersProvider(_status).notifier).refresh();
          case _ProdTab.shifts:
            await ref.read(shiftsProvider.notifier).refresh();
        }
      },
      slivers: [
        SliverToBoxAdapter(
          child: Padding(
            padding: const EdgeInsets.only(bottom: 14),
            child: Row(children: [
              for (final e in tabs.entries) ...[
                FilterPill(label: e.value, chevron: false, active: e.key == _tab, onTap: () => setState(() => _tab = e.key)),
                const SizedBox(width: 8),
              ],
            ]),
          ),
        ),
        ...switch (_tab) {
          _ProdTab.overview => [const SliverToBoxAdapter(child: _Overview())],
          _ProdTab.orders => _orders(context),
          _ProdTab.shifts => _shifts(context),
        },
      ],
    );
  }

  List<Widget> _orders(BuildContext context) {
    final l10n = context.l10n;
    final c = context.ob;
    final fmt = context.fmt;
    final s = ref.watch(ordersProvider(_status));
    final filters = {'open': l10n.prFilterOpen, 'Completed': l10n.prFilterDone, null: l10n.prFilterAll};
    return [
      SliverToBoxAdapter(
        child: Padding(
          padding: const EdgeInsets.only(bottom: 12),
          child: Row(children: [
            for (final e in filters.entries) ...[
              FilterPill(label: e.value, chevron: false, active: e.key == _status, onTap: () => setState(() => _status = e.key)),
              const SizedBox(width: 8),
            ],
          ]),
        ),
      ),
      if (s.isInitialLoading)
        const SliverToBoxAdapter(child: LoadingView())
      else if (s.items.isEmpty && s.failure != null)
        SliverToBoxAdapter(child: ErrorView(failure: s.failure!, onRetry: () => ref.read(ordersProvider(_status).notifier).refresh()))
      else if (s.items.isEmpty)
        SliverToBoxAdapter(child: EmptyView(icon: LucideIcons.factory, title: l10n.stateEmpty, compact: true))
      else
        SliverList.separated(
          itemCount: s.items.length,
          separatorBuilder: (context, i) => const SizedBox(height: 10),
          itemBuilder: (context, i) {
            final o = s.items[i];
            return ObCard(
              onTap: () => context.push('/home/production/orders/${o.id}'),
              padding: const EdgeInsets.all(14),
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Row(children: [
                  Text(o.number, style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: c.ink3)),
                  const Spacer(),
                  SoftBadge(label: orderStatusLabel(l10n, o.status), tone: orderStatusTone(o.status)),
                ]),
                const SizedBox(height: 4),
                Text(o.recipeName, style: TextStyle(fontSize: 15, fontWeight: FontWeight.w600, color: c.ink)),
                const SizedBox(height: 6),
                Text(
                  [l10n.prPlanned('${fmt.decimal(o.plannedQty)} ${o.outputUnit}', _d(o.plannedDate)), if (o.outputQty != null) '${l10n.prOutput} ${fmt.decimal(o.outputQty!)} ${o.outputUnit}', o.departmentName].join(' · '),
                  style: TextStyle(fontSize: 12, color: c.ink3),
                ),
                if (o.overconsumed) ...[const SizedBox(height: 6), SoftBadge(label: l10n.prOverconsumed, tone: Tone.warn)],
              ]),
            );
          },
        ),
    ];
  }

  List<Widget> _shifts(BuildContext context) {
    final l10n = context.l10n;
    final c = context.ob;
    final fmt = context.fmt;
    final s = ref.watch(shiftsProvider);
    return [
      if (s.isInitialLoading)
        const SliverToBoxAdapter(child: LoadingView())
      else if (s.items.isEmpty && s.failure != null)
        SliverToBoxAdapter(child: ErrorView(failure: s.failure!, onRetry: () => ref.read(shiftsProvider.notifier).refresh()))
      else if (s.items.isEmpty)
        SliverToBoxAdapter(child: EmptyView(icon: LucideIcons.clock, title: l10n.stateEmpty, compact: true))
      else
        SliverList.separated(
          itemCount: s.items.length,
          separatorBuilder: (context, i) => const SizedBox(height: 10),
          itemBuilder: (context, i) {
            final sh = s.items[i];
            return ObCard(
              padding: const EdgeInsets.all(14),
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Row(children: [
                  Text('${sh.number} · ${sh.orderNumber}', style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: c.ink3)),
                  const Spacer(),
                  SoftBadge(label: shiftStatusLabel(l10n, sh.status), tone: sh.status == 'Approved' ? Tone.ok : sh.status == 'Closed' ? Tone.warn : Tone.neutral),
                ]),
                const SizedBox(height: 4),
                Text(sh.recipeName, style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: c.ink)),
                const SizedBox(height: 4),
                Text(
                  '${l10n.prShift(sh.shiftNo, _d(sh.shiftDate))} · ${sh.operatorName}${sh.goodOutput == null ? '' : ' · ${fmt.decimal(sh.goodOutput!)} ${sh.outputUnit}'}',
                  style: TextStyle(fontSize: 12, color: c.ink3),
                ),
              ]),
            );
          },
        ),
    ];
  }
}

class _Overview extends ConsumerWidget {
  const _Overview();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final c = context.ob;
    final fmt = context.fmt;
    final tech = ref.watch(techBlock);
    final ov = ref.watch(prodOverviewBlock);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        blockView(context, tech, onRetry: () => ref.read(techBlock.notifier).refresh(), (t) {
          return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            ObCard(
              tint: c.kpi,
              child: Column(children: [
                MetricRow(items: [
                  (l10n.anProductionToday, fmt.weight(t.todayKg), null),
                  (l10n.prGood, fmt.weight(t.goodKg), '${l10n.prPlan} ${fmt.weight(t.planKg)}'),
                  (l10n.anCompletion, t.completionPercent == null ? '—' : '${fmt.decimal(t.completionPercent!)}%', null),
                ]),
                const SizedBox(height: 14),
                MetricRow(items: [
                  (l10n.anDefect, t.defectPercent == null ? '—' : '${fmt.decimal(t.defectPercent!)}%', fmt.weight(t.defectKg)),
                  (l10n.anActiveOrders, '${t.activeOrders}', null),
                  (l10n.prClosedShifts, '${t.closedShifts}', '${l10n.prOpenShifts}: ${t.openShifts}'),
                ]),
                if (t.normDeviationPercent != null) ...[
                  const SizedBox(height: 10),
                  Align(
                    alignment: Alignment.centerLeft,
                    child: Text('${l10n.prNormDev}: ${fmt.signed(t.normDeviationPercent!, 1)}%', style: TextStyle(fontSize: 12, color: c.ink3)),
                  ),
                ],
              ]),
            ),
            if (t.days.any((d) => d.goodKg > 0)) ...[
              const SizedBox(height: 12),
              ObCard(child: DailyBars(values: [for (final d in t.days) d.goodKg], format: fmt.weight, height: 80)),
            ],
            if (t.issues.isNotEmpty) ...[
              const SizedBox(height: ObSpace.block),
              SectionHeader(title: l10n.prIssues),
              const SizedBox(height: 12),
              for (final i in t.issues) ...[
                ObCard(
                  onTap: () => context.push(productionRoute(i.link)),
                  child: Row(children: [
                    Icon(LucideIcons.triangleAlert, size: 18, color: i.kind == 'approval' || i.kind == 'shortage' ? c.warn : c.bad),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                        Text(i.title, style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: c.ink)),
                        if (i.detail.isNotEmpty) Text(i.detail, style: TextStyle(fontSize: 12, color: c.ink3)),
                      ]),
                    ),
                  ]),
                ),
                const SizedBox(height: 8),
              ],
            ],
            if (t.byProduct.isNotEmpty) ...[
              const SizedBox(height: ObSpace.block),
              SectionHeader(title: l10n.prByProduct),
              const SizedBox(height: 12),
              ObListCard(children: [
                for (final p in t.byProduct)
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                    child: Row(children: [
                      Expanded(child: Text(p.name, style: TextStyle(fontSize: 13, color: c.ink))),
                      Text(fmt.weight(p.value), style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: c.ink)),
                    ]),
                  ),
              ]),
            ],
          ]);
        }),
        const SizedBox(height: ObSpace.block),
        blockView(context, ov, onRetry: () => ref.read(prodOverviewBlock.notifier).refresh(), (o) {
          return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            ObCard(
              child: Column(children: [
                MetricRow(items: [
                  (l10n.prStockValue, fmt.compact(o.stockValue), null),
                  (l10n.prExpiring, '${o.expiring30.count}', null),
                  (l10n.prExpired, '${o.expired.count}', null),
                ]),
                const SizedBox(height: 14),
                MetricRow(items: [
                  (l10n.prBelowMin, '${o.belowMin}', null),
                  (l10n.anActiveOrders, '${o.openOrders}', null),
                  (l10n.prOverdueOrders, '${o.overdueOrders}', null),
                ]),
              ]),
            ),
            if (o.lowDays.isNotEmpty) ...[
              const SizedBox(height: ObSpace.block),
              SectionHeader(title: l10n.prLowDays),
              const SizedBox(height: 12),
              ObListCard(children: [
                for (final d in o.lowDays)
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                    child: Row(children: [
                      Expanded(child: Text(d.name, style: TextStyle(fontSize: 13, color: c.ink))),
                      Text('${fmt.decimal(d.onHand)} ${d.unitCode}', style: TextStyle(fontSize: 12, color: c.ink3)),
                      const SizedBox(width: 10),
                      Text(l10n.prDaysLeft(fmt.decimal(d.days)),
                          style: TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: d.days < 14 ? c.bad : d.days < 30 ? c.warn : c.ink)),
                    ]),
                  ),
              ]),
            ],
          ]);
        }),
      ],
    );
  }
}

/// Задание на выпуск: план и выпуск, сырьё (нужно, выдано, осталось, на складе, перерасход), история; действия — по флагам `can`
/// сервера, каждое с подтверждением и ответом сервера (без оптимистичного интерфейса), конфликт версии — перечитать.
class OrderScreen extends ConsumerWidget {
  const OrderScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final c = context.ob;
    final fmt = context.fmt;
    final s = ref.watch(orderBlock(id));
    final o = s.data;
    return ObPage(
      title: o?.number ?? l10n.prTabOrders,
      subtitle: o == null ? null : Text(o.recipeName),
      onRefresh: () => ref.read(orderBlock(id).notifier).refresh(),
      slivers: [
        SliverToBoxAdapter(
          child: blockView(context, s, onRetry: () => ref.read(orderBlock(id).notifier).refresh(), (o) {
            return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              ObCard(
                tint: c.kpi,
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  SoftBadge(label: orderStatusLabel(l10n, o.status), tone: orderStatusTone(o.status), size: 12),
                  const SizedBox(height: 12),
                  MetricRow(items: [
                    (l10n.prPlan, '${fmt.decimal(o.plannedQty)} ${o.outputUnit}', _d(o.plannedDate)),
                    (l10n.prOutput, o.outputQty == null ? (o.shiftsGoodOutput == null ? '—' : '${fmt.decimal(o.shiftsGoodOutput!)} ${o.outputUnit}') : '${fmt.decimal(o.outputQty!)} ${o.outputUnit}', null),
                    (l10n.prOpenShifts, '${o.shiftsOpen}', null),
                  ]),
                  const SizedBox(height: 10),
                  Text([o.departmentName, o.warehouseName, ?o.responsibleName].join(' · '), style: TextStyle(fontSize: 12, color: c.ink3)),
                ]),
              ),
              const SizedBox(height: 12),
              _OrderActions(order: o),
              if (o.lines.isNotEmpty) ...[
                const SizedBox(height: ObSpace.block),
                SectionHeader(title: l10n.prMaterials),
                const SizedBox(height: 12),
                ObListCard(children: [
                  for (final line in o.lines)
                    Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 11),
                      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                        Row(children: [
                          Expanded(child: Text(line.name, style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: c.ink))),
                          if (line.overconsumed) SoftBadge(label: l10n.prOverconsumed, tone: Tone.warn),
                        ]),
                        const SizedBox(height: 4),
                        Text(
                          [
                            l10n.prRequired('${fmt.decimal(line.required)} ${line.unitCode}'),
                            l10n.prIssued(fmt.decimal(line.issued)),
                            if (line.left > 0) l10n.prLeft(fmt.decimal(line.left)),
                            l10n.prAvailable(fmt.decimal(line.available)),
                          ].join(' · '),
                          style: TextStyle(fontSize: 12, color: line.left > line.available ? c.bad : c.ink3),
                        ),
                      ]),
                    ),
                ]),
              ],
              if (o.history.isNotEmpty) ...[
                const SizedBox(height: ObSpace.block),
                SectionHeader(title: l10n.prHistory),
                const SizedBox(height: 12),
                ObListCard(children: [
                  for (final h in o.history.take(20))
                    Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                      child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                        SizedBox(width: 48, child: Text(_d(h.createdAt), style: TextStyle(fontSize: 12, color: c.ink3))),
                        Expanded(child: Text([h.text, ?h.actorName, ?h.comment].join(' · '), style: TextStyle(fontSize: 13, color: c.ink))),
                      ]),
                    ),
                ]),
              ],
            ]);
          }),
        ),
      ],
    );
  }
}

class _OrderActions extends ConsumerStatefulWidget {
  const _OrderActions({required this.order});

  final ProductionOrderDetail order;

  @override
  ConsumerState<_OrderActions> createState() => _OrderActionsState();
}

class _OrderActionsState extends ConsumerState<_OrderActions> {
  bool _busy = false;

  Future<void> _run(String done, Future<void> Function() action) async {
    final messenger = ScaffoldMessenger.of(context);
    final l10n = context.l10n;
    setState(() => _busy = true);
    try {
      await action();
      messenger.showSnackBar(SnackBar(content: Text(l10n.prDone(done))));
    } on Object catch (e) {
      final f = AppFailure.from(e);
      messenger.showSnackBar(SnackBar(content: Text(f.kind == ApiErrorKind.conflict ? l10n.prConflict : f.describe(l10n))));
    } finally {
      await ref.read(orderBlock(widget.order.id).notifier).refresh();
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<bool> _confirm(String title, String message, {TextEditingController? input, String? inputLabel, TextInputType? keyboard}) async {
    final l10n = context.l10n;
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(title),
        content: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(message),
          if (input != null) ...[
            const SizedBox(height: 12),
            TextField(controller: input, autofocus: true, keyboardType: keyboard, decoration: InputDecoration(labelText: inputLabel)),
          ],
        ]),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: Text(l10n.actionCancel)),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: Text(title)),
        ],
      ),
    );
    return ok ?? false;
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final o = widget.order;
    final api = ref.read(productionApiProvider);
    final buttons = <Widget>[
      if (o.can.release)
        FilledButton.icon(
          onPressed: _busy
              ? null
              : () async {
                  if (await _confirm(l10n.prRelease, l10n.prConfirmRelease)) await _run(l10n.prRelease, () => api.release(o.id));
                },
          icon: const Icon(LucideIcons.play, size: 18),
          label: Text(l10n.prRelease),
        ),
      if (o.can.complete)
        FilledButton.icon(
          onPressed: _busy
              ? null
              : () async {
                  final qty = TextEditingController(text: o.shiftsGoodOutput == null ? '' : context.fmt.decimal(o.shiftsGoodOutput!).replaceAll(' ', ''));
                  final ok = await _confirm(
                    l10n.prComplete,
                    l10n.prConfirmComplete('${qty.text} ${o.outputUnit}'),
                    input: qty,
                    inputLabel: l10n.prOutputQty(o.outputUnit),
                    keyboard: const TextInputType.numberWithOptions(decimal: true),
                  );
                  final value = double.tryParse(qty.text.replaceAll(',', '.').replaceAll(' ', ''));
                  if (ok && value != null && value >= 0) await _run(l10n.prComplete, () => api.complete(o.id, value, o.version, null));
                },
          icon: const Icon(LucideIcons.circleCheck, size: 18),
          label: Text(l10n.prComplete),
        ),
      if (o.can.receive)
        OutlinedButton.icon(
          onPressed: _busy
              ? null
              : () async {
                  if (await _confirm(l10n.prReceive, l10n.prConfirmReceive)) await _run(l10n.prReceive, () => api.receiveOutput(o.id));
                },
          icon: const Icon(LucideIcons.packagePlus, size: 18),
          label: Text(l10n.prReceive),
        ),
      if (o.can.cancel)
        TextButton.icon(
          onPressed: _busy
              ? null
              : () async {
                  final reason = TextEditingController();
                  final ok = await _confirm(l10n.prCancel, '', input: reason, inputLabel: l10n.prReason);
                  if (ok && reason.text.trim().isNotEmpty) await _run(l10n.prCancel, () => api.cancel(o.id, reason.text.trim()));
                },
          icon: Icon(LucideIcons.x, size: 18, color: context.ob.bad),
          label: Text(l10n.prCancel, style: TextStyle(color: context.ob.bad)),
        ),
    ];
    if (buttons.isEmpty) return const SizedBox.shrink();
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      for (final b in buttons) ...[b, const SizedBox(height: 8)],
    ]);
  }
}
