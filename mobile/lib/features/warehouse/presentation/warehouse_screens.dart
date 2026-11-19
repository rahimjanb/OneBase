import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/cache/cached_block.dart';
import '../../../core/cache/paged.dart';
import '../../../core/errors/app_failure.dart';
import '../../../core/network/connectivity.dart';
import '../../../core/utils/debouncer.dart';
import '../../../l10n/gen/app_localizations.dart';
import '../../../shared/extensions/context_ext.dart';
import '../../../shared/widgets/detail_sheet.dart';
import '../../../shared/widgets/ob_app_bar.dart';
import '../../../shared/widgets/ob_card.dart';
import '../../../shared/widgets/pills.dart';
import '../../../shared/widgets/pressable.dart';
import '../../../shared/widgets/section_header.dart';
import '../../../shared/widgets/states.dart';
import '../../analytics/presentation/analytics_screen.dart';
import '../../analytics/presentation/widgets/sales_widgets.dart';
import '../../production/data/production_api.dart';
import '../../production/data/production_models.dart';

ProductionApi _api(Ref ref) => ref.read(productionApiProvider);

class StockSummaryBlock extends CachedBlock<StockSummary> {
  @override
  String get cacheKey => 'warehouse/summary';

  @override
  Future<StockSummary> fetch(CancelToken cancel) => _api(ref).stockSummary(cancel);

  @override
  StockSummary decode(Object? json) => StockSummary.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(StockSummary data) => data.toJson();
}

final stockSummaryBlock = NotifierProvider.autoDispose<StockSummaryBlock, BlockState<StockSummary>>(StockSummaryBlock.new);

typedef StockArgs = ({String q, String? filter});

class StockController extends PagedController<StockRow> {
  StockController(this.args);

  final StockArgs args;

  @override
  Future<PageChunk<StockRow>> fetchPage(int page, String? cursor, CancelToken cancel) => _api(ref).stock(args.q, args.filter, page, cancel);
}

final stockProvider = NotifierProvider.autoDispose.family<StockController, PagedState<StockRow>, StockArgs>(StockController.new);

class MovementsController extends PagedController<MovementRow> {
  MovementsController(this.kind);

  final String? kind;

  @override
  Future<PageChunk<MovementRow>> fetchPage(int page, String? cursor, CancelToken cancel) => _api(ref).movements(kind, page, cancel);
}

final movementsProvider = NotifierProvider.autoDispose.family<MovementsController, PagedState<MovementRow>, String?>(MovementsController.new);

class ItemBlock extends CachedBlock<ItemCard> {
  ItemBlock(this.id);

  final String id;

  @override
  String get cacheKey => 'warehouse/item/$id';

  @override
  Future<ItemCard> fetch(CancelToken cancel) => _api(ref).item(id, cancel);

  @override
  ItemCard decode(Object? json) => ItemCard.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(ItemCard data) => data.toJson();
}

final itemBlock = NotifierProvider.autoDispose.family<ItemBlock, BlockState<ItemCard>, String>(ItemBlock.new);

class WarehousesBlock extends CachedBlock<Warehouses> {
  @override
  String get cacheKey => 'warehouse/warehouses';

  @override
  Future<Warehouses> fetch(CancelToken cancel) => _api(ref).warehouses(cancel);

  @override
  Warehouses decode(Object? json) => Warehouses.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(Warehouses data) => data.toJson();
}

final warehousesBlock = NotifierProvider.autoDispose<WarehousesBlock, BlockState<Warehouses>>(WarehousesBlock.new);

String movementLabel(AppLocalizations l10n, String kind) => switch (kind) {
      'Receipt' => l10n.mvReceipt,
      'Issue' => l10n.mvIssue,
      'WriteOff' => l10n.mvWriteOff,
      'Adjustment' => l10n.mvAdjustment,
      'TransferOut' || 'TransferIn' => l10n.mvTransfer,
      'Reversal' => l10n.mvReversal,
      'ProductionIssue' || 'ProductionReturn' || 'ProductionOutput' => l10n.mvProduction,
      _ => kind,
    };

String _d(String iso) => iso.length >= 10 ? '${iso.substring(8, 10)}.${iso.substring(5, 7)}' : iso;

/// Строка движения: вид, позиция, количество со знаком, остаток после, документ.
class MovementTile extends StatelessWidget {
  const MovementTile({super.key, required this.m, this.showItem = true});

  final MovementRow m;
  final bool showItem;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final fmt = context.fmt;
    final l10n = context.l10n;
    final plus = m.quantity >= 0;
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
      child: Row(children: [
        Icon(plus ? LucideIcons.arrowDownLeft : LucideIcons.arrowUpRight, size: 16, color: plus ? c.ok : c.ink3),
        const SizedBox(width: 10),
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(showItem ? m.itemName : movementLabel(l10n, m.kind), maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 13, color: c.ink)),
            Text(
              [_d(m.createdAt), if (showItem) movementLabel(l10n, m.kind), m.documentNumber, ?m.departmentName, if (m.reversedBy != null) '↩ ${m.reversedBy}'].join(' · '),
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(fontSize: 11, color: c.ink3),
            ),
          ]),
        ),
        Column(crossAxisAlignment: CrossAxisAlignment.end, children: [
          Text('${plus ? '+' : ''}${fmt.decimal(m.quantity, digits: 3)} ${m.unitCode}',
              style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: plus ? c.ok : c.ink)),
          Text('→ ${fmt.decimal(m.balanceAfter, digits: 3)}', style: TextStyle(fontSize: 11, color: c.ink3)),
        ]),
      ]),
    );
  }
}

enum _WhTab { stock, moves }

/// «Склад»: остатки сырья (поиск, подборки, сводка), движения; операции — приход, выдача отделу, списание (только кладовщику склада).
class WarehouseScreen extends ConsumerStatefulWidget {
  const WarehouseScreen({super.key});

  @override
  ConsumerState<WarehouseScreen> createState() => _WarehouseScreenState();
}

class _WarehouseScreenState extends ConsumerState<WarehouseScreen> {
  _WhTab _tab = _WhTab.stock;
  String _q = '';
  String? _filter;
  final _debounce = Debouncer();
  final _scroll = ScrollController();

  StockArgs get _args => (q: _q, filter: _filter);

  @override
  void initState() {
    super.initState();
    _scroll.addListener(() {
      if (_scroll.position.extentAfter > 500) return;
      if (_tab == _WhTab.stock) ref.read(stockProvider(_args).notifier).loadMore();
      if (_tab == _WhTab.moves) ref.read(movementsProvider(null).notifier).loadMore();
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
    final fmt = context.fmt;
    final summary = ref.watch(stockSummaryBlock).data;
    final whs = ref.watch(warehousesBlock).data;
    final posts = whs?.warehouses.any((w) => w.isActive && w.posts) ?? false;
    final filters = <String?, String>{
      null: l10n.whFilterAll,
      'low': '${l10n.whFilterLow}${summary == null ? '' : ' ${summary.low}'}',
      'reserved': '${l10n.whFilterReserved}${summary == null ? '' : ' ${summary.withReserve}'}',
      'empty': '${l10n.whFilterEmpty}${summary == null ? '' : ' ${summary.empty}'}',
      'expiring': '${l10n.whFilterExpiring}${summary == null ? '' : ' ${summary.expiring}'}',
      'expired': '${l10n.whFilterExpired}${summary == null ? '' : ' ${summary.expired}'}',
    };
    final stock = ref.watch(stockProvider(_args));
    final moves = _tab == _WhTab.moves ? ref.watch(movementsProvider(null)) : null;
    return ObPage(
      title: l10n.moduleWarehouse,
      controller: _scroll,
      floatingActionButton: posts
          ? FloatingActionButton.extended(
              backgroundColor: c.accentFill,
              foregroundColor: Colors.white,
              onPressed: () => showObSheet<void>(context, title: l10n.whOperation, builder: (context) => const StockOperationForm()),
              icon: const Icon(LucideIcons.plus),
              label: Text(l10n.whOperation),
            )
          : null,
      onRefresh: () async {
        await Future.wait([
          ref.read(stockSummaryBlock.notifier).refresh(),
          if (_tab == _WhTab.stock) ref.read(stockProvider(_args).notifier).refresh() else ref.read(movementsProvider(null).notifier).refresh(),
        ]);
      },
      slivers: [
        SliverToBoxAdapter(
          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            Row(children: [
              FilterPill(label: l10n.whTabStock, chevron: false, active: _tab == _WhTab.stock, onTap: () => setState(() => _tab = _WhTab.stock)),
              const SizedBox(width: 8),
              FilterPill(label: l10n.whTabMoves, chevron: false, active: _tab == _WhTab.moves, onTap: () => setState(() => _tab = _WhTab.moves)),
            ]),
            const SizedBox(height: 12),
            if (_tab == _WhTab.stock) ...[
              TextField(
                decoration: InputDecoration(hintText: l10n.whSearch, prefixIcon: const Icon(LucideIcons.search, size: 18), isDense: true),
                onChanged: (v) => _debounce(() => setState(() => _q = v)),
              ),
              const SizedBox(height: 10),
              SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                clipBehavior: Clip.none,
                child: Row(children: [
                  for (final e in filters.entries) ...[
                    FilterPill(label: e.value, chevron: false, active: e.key == _filter, onTap: () => setState(() => _filter = e.key)),
                    const SizedBox(width: 8),
                  ],
                ]),
              ),
              const SizedBox(height: 12),
            ],
          ]),
        ),
        if (_tab == _WhTab.stock) ...[
          if (stock.isInitialLoading)
            const SliverToBoxAdapter(child: LoadingView())
          else if (stock.items.isEmpty && stock.failure != null)
            SliverToBoxAdapter(child: ErrorView(failure: stock.failure!, onRetry: () => ref.read(stockProvider(_args).notifier).refresh()))
          else if (stock.items.isEmpty)
            SliverToBoxAdapter(child: EmptyView(icon: LucideIcons.package, title: l10n.stateEmpty, compact: true))
          else
            SliverList.separated(
              itemCount: stock.items.length,
              separatorBuilder: (context, i) => const SizedBox(height: 8),
              itemBuilder: (context, i) {
                final r = stock.items[i];
                return ObCard(
                  onTap: () => context.push('/home/warehouse/items/${r.itemId}'),
                  padding: const EdgeInsets.all(14),
                  child: Row(children: [
                    Expanded(
                      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                        Text(r.name, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: c.ink)),
                        Text('${r.sku} · ${r.categoryName}', style: TextStyle(fontSize: 11, color: c.ink3)),
                      ]),
                    ),
                    Column(crossAxisAlignment: CrossAxisAlignment.end, children: [
                      Text('${fmt.decimal(r.available, digits: 3)} ${r.unitCode}',
                          style: TextStyle(fontSize: 14, fontWeight: FontWeight.w700, color: r.low ? c.warn : r.onHand <= 0 ? c.ink3 : c.ink)),
                      if (r.reserved > 0) Text('${l10n.whReserved} ${fmt.decimal(r.reserved, digits: 3)}', style: TextStyle(fontSize: 11, color: c.ink3)),
                    ]),
                  ]),
                );
              },
            ),
        ] else if (moves != null) ...[
          if (moves.isInitialLoading)
            const SliverToBoxAdapter(child: LoadingView())
          else if (moves.items.isEmpty && moves.failure != null)
            SliverToBoxAdapter(child: ErrorView(failure: moves.failure!, onRetry: () => ref.read(movementsProvider(null).notifier).refresh()))
          else
            SliverToBoxAdapter(child: ObListCard(children: [for (final m in moves.items) MovementTile(m: m)])),
        ],
      ],
    );
  }
}

/// Карточка позиции: остаток, резерв, свободно, минимум; резервы по заявкам; последние движения.
class ItemScreen extends ConsumerWidget {
  const ItemScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final c = context.ob;
    final fmt = context.fmt;
    final s = ref.watch(itemBlock(id));
    return ObPage(
      title: s.data?.item.name ?? l10n.whItem,
      subtitle: s.data == null ? null : Text('${s.data!.item.sku} · ${s.data!.item.categoryName}'),
      onRefresh: () => ref.read(itemBlock(id).notifier).refresh(),
      slivers: [
        SliverToBoxAdapter(
          child: blockView(context, s, onRetry: () => ref.read(itemBlock(id).notifier).refresh(), (card) {
            final it = card.item;
            String q(double v) => '${fmt.decimal(v, digits: 3)} ${it.unitCode}';
            return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              ObCard(
                tint: c.kpi,
                child: Column(children: [
                  MetricRow(items: [(l10n.whOnHand, q(it.onHand), null), (l10n.whReserved, q(it.reserved), null), (l10n.whAvailable, q(it.available), null)]),
                  if (it.minQty > 0 || it.nearestExpiry != null) ...[
                    const SizedBox(height: 12),
                    MetricRow(items: [
                      (l10n.whMin, q(it.minQty), null),
                      (l10n.prExpired, q(it.expired), null),
                      ('↯', it.nearestExpiry == null ? '—' : _d(it.nearestExpiry!), null),
                    ]),
                  ],
                ]),
              ),
              if (card.reservations.isNotEmpty) ...[
                const SizedBox(height: ObSpace.block),
                SectionHeader(title: l10n.whReservations),
                const SizedBox(height: 12),
                ObListCard(children: [
                  for (final r in card.reservations)
                    Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                      child: Row(children: [
                        Expanded(child: Text('${r.requestNumber} · ${r.requestTitle}', maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 13, color: c.ink))),
                        Text(q(r.quantity), style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: c.ink)),
                      ]),
                    ),
                ]),
              ],
              if (card.movements.isNotEmpty) ...[
                const SizedBox(height: ObSpace.block),
                SectionHeader(title: l10n.whMovements),
                const SizedBox(height: 12),
                ObListCard(children: [for (final m in card.movements.take(30)) MovementTile(m: m, showItem: false)]),
              ],
            ]);
          }),
        ),
      ],
    );
  }
}

/// Складская операция: приход, выдача отделу или списание одной позицией. ⚠ Остатки меняются: только при связи, с подтверждением и ответом
/// сервера; ключ формы (`clientKey`) защищает от двойной проводки при обрыве связи и повторе.
class StockOperationForm extends ConsumerStatefulWidget {
  const StockOperationForm({super.key});

  @override
  ConsumerState<StockOperationForm> createState() => _StockOperationFormState();
}

class _StockOperationFormState extends ConsumerState<StockOperationForm> {
  final _clientKey = newClientKey();
  StockOp _op = StockOp.issue;
  CatalogItem? _item;
  String? _warehouse;
  String? _department;
  final _qty = TextEditingController();
  final _cost = TextEditingController();
  final _comment = TextEditingController();
  final _receiver = TextEditingController();
  bool _busy = false;
  AppFailure? _error;
  String? _validation;

  @override
  void dispose() {
    for (final c in [_qty, _cost, _comment, _receiver]) {
      c.dispose();
    }
    super.dispose();
  }

  String _opLabel(AppLocalizations l10n, StockOp op) => switch (op) {
        StockOp.receipt => l10n.whOpReceipt,
        StockOp.issue => l10n.whOpIssue,
        StockOp.writeOff => l10n.whOpWriteOff,
      };

  Future<void> _pickItem() async {
    final picked = await showObSheet<CatalogItem>(context, title: context.l10n.whPickItem, builder: (context) => _CatalogPicker(warehouse: _warehouse));
    if (picked != null) setState(() => _item = picked);
  }

  Future<void> _submit() async {
    final l10n = context.l10n;
    final qty = double.tryParse(_qty.text.replaceAll(',', '.').replaceAll(' ', ''));
    setState(() => _validation = null);
    if (_item == null) return setState(() => _validation = l10n.whPickItem);
    if (qty == null || qty <= 0) return setState(() => _validation = l10n.whQuantity(_item!.unitCode));
    if (_op == StockOp.issue && _department == null) return setState(() => _validation = l10n.whDepartment);
    if (_op == StockOp.writeOff && _comment.text.trim().isEmpty) return setState(() => _validation = l10n.whReasonRequired);
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(l10n.prConfirmTitle),
        content: Text(l10n.whConfirm(_opLabel(l10n, _op), _item!.name, '${context.fmt.decimal(qty, digits: 3)} ${_item!.unitCode}')),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: Text(l10n.actionCancel)),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: Text(l10n.whPost)),
        ],
      ),
    );
    if (ok != true || !mounted) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(productionApiProvider).postStock(_op, {
        'lines': [
          {'itemId': _item!.id, 'quantity': qty, if (_op == StockOp.receipt && _cost.text.trim().isNotEmpty) 'unitCost': double.tryParse(_cost.text.replaceAll(',', '.'))},
        ],
        'comment': _comment.text.trim().isEmpty ? null : _comment.text.trim(),
        'warehouseId': _warehouse,
        if (_op == StockOp.issue) 'departmentId': _department,
        if (_op == StockOp.issue && _receiver.text.trim().isNotEmpty) 'receiver': _receiver.text.trim(),
        'clientKey': _clientKey,
      });
      ref.invalidate(stockSummaryBlock);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(l10n.whPosted)));
      Navigator.pop(context);
    } on Object catch (e) {
      // Ключ формы тот же: повтор после обрыва связи не проведёт документ второй раз.
      setState(() {
        _busy = false;
        _error = AppFailure.from(e);
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final c = context.ob;
    final online = ref.watch(isOnlineProvider).value ?? true;
    final whs = ref.watch(warehousesBlock).data;
    final posting = whs?.warehouses.where((w) => w.isActive && w.posts).toList() ?? const <Warehouse>[];
    _warehouse ??= (posting.where((w) => w.isDefault).firstOrNull ?? posting.firstOrNull)?.id;
    if (posting.isEmpty) return Text(l10n.whNoPosts, style: TextStyle(fontSize: 14, color: c.ink2));
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      Wrap(spacing: 8, runSpacing: 8, children: [
        for (final op in StockOp.values) FilterPill(label: _opLabel(l10n, op), chevron: false, active: op == _op, onTap: () => setState(() => _op = op)),
      ]),
      if (posting.length > 1) ...[
        const SizedBox(height: 12),
        DropdownButtonFormField<String>(
          initialValue: _warehouse,
          decoration: InputDecoration(labelText: l10n.whWarehouse),
          items: [for (final w in posting) DropdownMenuItem(value: w.id, child: Text(w.name))],
          onChanged: (v) => setState(() => _warehouse = v),
        ),
      ],
      const SizedBox(height: 12),
      Pressable(
        onTap: _pickItem,
        child: InputDecorator(
          decoration: InputDecoration(labelText: l10n.whItem, suffixIcon: const Icon(LucideIcons.search, size: 18)),
          child: Text(_item == null ? l10n.whPickItem : '${_item!.sku} · ${_item!.name}', style: TextStyle(fontSize: 15, color: _item == null ? c.subtle : c.ink)),
        ),
      ),
      const SizedBox(height: 12),
      TextField(
        controller: _qty,
        keyboardType: const TextInputType.numberWithOptions(decimal: true),
        decoration: InputDecoration(labelText: l10n.whQuantity(_item?.unitCode ?? '')),
      ),
      if (_op == StockOp.receipt) ...[
        const SizedBox(height: 12),
        TextField(controller: _cost, keyboardType: const TextInputType.numberWithOptions(decimal: true), decoration: InputDecoration(labelText: l10n.whUnitCost)),
      ],
      if (_op == StockOp.issue) ...[
        const SizedBox(height: 12),
        DropdownButtonFormField<String>(
          initialValue: _department,
          isExpanded: true,
          decoration: InputDecoration(labelText: l10n.whDepartment),
          items: [for (final d in whs?.departments ?? const <Department>[]) DropdownMenuItem(value: d.id, child: Text(d.name))],
          onChanged: (v) => setState(() => _department = v),
        ),
        const SizedBox(height: 12),
        TextField(controller: _receiver, maxLength: 200, decoration: InputDecoration(labelText: l10n.whReceiver, counterText: '')),
      ],
      const SizedBox(height: 12),
      TextField(controller: _comment, maxLines: 2, decoration: InputDecoration(labelText: _op == StockOp.writeOff ? l10n.prReason : l10n.whComment)),
      if (_validation != null) ...[const SizedBox(height: 8), Text(_validation!, style: TextStyle(fontSize: 13, color: c.bad))],
      if (_error != null) ...[const SizedBox(height: 8), Text(_error!.describe(l10n), style: TextStyle(fontSize: 13, color: c.bad))],
      if (!online) ...[const SizedBox(height: 8), Text(l10n.whOnline, style: TextStyle(fontSize: 12, color: c.warn))],
      const SizedBox(height: 14),
      FilledButton(
        onPressed: _busy || !online ? null : _submit,
        child: _busy ? const SizedBox.square(dimension: 20, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white)) : Text(l10n.whPost),
      ),
    ]);
  }
}

class _CatalogPicker extends ConsumerStatefulWidget {
  const _CatalogPicker({this.warehouse});

  final String? warehouse;

  @override
  ConsumerState<_CatalogPicker> createState() => _CatalogPickerState();
}

class _CatalogPickerState extends ConsumerState<_CatalogPicker> {
  final _debounce = Debouncer();
  List<CatalogItem> _items = const [];
  bool _loading = false;
  CancelToken? _cancel;

  Future<void> _search(String q) async {
    _cancel?.cancel();
    if (q.trim().length < 2) return setState(() => _items = const []);
    final cancel = _cancel = CancelToken();
    setState(() => _loading = true);
    try {
      final items = await ref.read(productionApiProvider).catalog(q.trim(), widget.warehouse, cancel);
      if (mounted && !cancel.isCancelled) setState(() => _items = items);
    } on Object {
      // Сеть — список останется прежним.
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  void dispose() {
    _debounce.cancel();
    _cancel?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final fmt = context.fmt;
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      TextField(
        autofocus: true,
        decoration: InputDecoration(hintText: context.l10n.whSearch, prefixIcon: const Icon(LucideIcons.search, size: 18)),
        onChanged: (v) => _debounce(() => _search(v)),
      ),
      if (_loading) const Padding(padding: EdgeInsets.all(12), child: LinearProgressIndicator()),
      for (final it in _items.take(30))
        Pressable(
          onTap: () => Navigator.pop(context, it),
          child: Padding(
            padding: const EdgeInsets.symmetric(vertical: 10),
            child: Row(children: [
              Expanded(child: Text('${it.sku} · ${it.name}', style: TextStyle(fontSize: 14, color: c.ink))),
              if (it.available != null) Text('${fmt.decimal(it.available!, digits: 3)} ${it.unitCode}', style: TextStyle(fontSize: 12, color: c.ink3)),
            ]),
          ),
        ),
    ]);
  }
}

// ───────────── Заявки ─────────────

class RequestsController extends PagedController<RequestRow> {
  RequestsController(this.scope);

  final String scope;

  @override
  Future<PageChunk<RequestRow>> fetchPage(int page, String? cursor, CancelToken cancel) => _api(ref).requests(scope, page, cancel);
}

final requestsProvider = NotifierProvider.autoDispose.family<RequestsController, PagedState<RequestRow>, String>(RequestsController.new);

/// «Заявки» на закупку: мои, отдела, все (по правам сервера) — номер, название, статус, сумма, срок.
class RequestsScreen extends ConsumerStatefulWidget {
  const RequestsScreen({super.key, this.initialScope = 'my'});

  final String initialScope;

  @override
  ConsumerState<RequestsScreen> createState() => _RequestsScreenState();
}

class _RequestsScreenState extends ConsumerState<RequestsScreen> {
  late String _scope = widget.initialScope;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final c = context.ob;
    final fmt = context.fmt;
    final s = ref.watch(requestsProvider(_scope));
    final scopes = {'my': l10n.rqScopeMy, 'department': l10n.rqScopeDepartment, 'all': l10n.rqScopeAll};
    return ObPage(
      title: l10n.moduleRequests,
      onRefresh: () => ref.read(requestsProvider(_scope).notifier).refresh(),
      slivers: [
        SliverToBoxAdapter(
          child: Padding(
            padding: const EdgeInsets.only(bottom: 12),
            child: Row(children: [
              for (final e in scopes.entries) ...[
                FilterPill(label: e.value, chevron: false, active: e.key == _scope, onTap: () => setState(() => _scope = e.key)),
                const SizedBox(width: 8),
              ],
            ]),
          ),
        ),
        if (s.isInitialLoading)
          const SliverToBoxAdapter(child: LoadingView())
        else if (s.items.isEmpty && s.failure != null)
          SliverToBoxAdapter(child: ErrorView(failure: s.failure!, onRetry: () => ref.read(requestsProvider(_scope).notifier).refresh()))
        else if (s.items.isEmpty)
          SliverToBoxAdapter(child: EmptyView(icon: LucideIcons.clipboardPen, title: l10n.stateEmpty, compact: true))
        else
          SliverList.separated(
            itemCount: s.items.length,
            separatorBuilder: (context, i) => const SizedBox(height: 10),
            itemBuilder: (context, i) {
              final r = s.items[i];
              return ObCard(
                padding: const EdgeInsets.all(14),
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Row(children: [
                    Text(r.number, style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: c.ink3)),
                    const Spacer(),
                    SoftBadge(label: r.status, tone: r.status == 'Approved' || r.status == 'Fulfilled' ? Tone.ok : r.status == 'Rejected' ? Tone.bad : Tone.accent),
                  ]),
                  const SizedBox(height: 4),
                  Text(r.title, style: TextStyle(fontSize: 15, fontWeight: FontWeight.w600, color: c.ink)),
                  const SizedBox(height: 4),
                  Text(
                    [r.departmentName, r.initiatorName, l10n.rqItems(r.itemsCount), if (r.desiredDate != null) l10n.rqDesired(_d(r.desiredDate!))].join(' · '),
                    style: TextStyle(fontSize: 12, color: c.ink3),
                  ),
                  if (r.estimatedTotal > 0) ...[
                    const SizedBox(height: 6),
                    Text('${fmt.compact(r.estimatedTotal)} ${l10n.unitSum}', style: TextStyle(fontSize: 14, fontWeight: FontWeight.w700, color: c.ink)),
                  ],
                ]),
              );
            },
          ),
      ],
    );
  }
}
