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
import '../../analytics/presentation/analytics_providers.dart';
import '../../analytics/presentation/analytics_screen.dart';
import '../../analytics/presentation/widgets/sales_widgets.dart';
import '../data/crm_api.dart';
import '../data/crm_models.dart';

// ───────────── Провайдеры ─────────────

class CustomerQueryController extends Notifier<CustomerQuery> {
  @override
  CustomerQuery build() => const CustomerQuery();

  void set(CustomerQuery q) => state = q;
}

final customerQueryProvider = NotifierProvider<CustomerQueryController, CustomerQuery>(CustomerQueryController.new);

/// Точки Sales Base постранично (по 50): поиск, «внимание», статус, сортировка — на сервере.
class CustomersController extends PagedController<FieldCustomerRow> {
  @override
  PagedState<FieldCustomerRow> build() {
    ref.watch(customerQueryProvider);
    return super.build();
  }

  @override
  Future<PageChunk<FieldCustomerRow>> fetchPage(int page, String? cursor, CancelToken cancel) =>
      ref.read(crmApiProvider).customers(ref.read(customerQueryProvider), page, cancel);
}

final customersProvider = NotifierProvider.autoDispose<CustomersController, PagedState<FieldCustomerRow>>(CustomersController.new);

class CustomerBlock extends CachedBlock<FieldCustomerCard> {
  CustomerBlock(this.id);

  final int id;

  @override
  String get cacheKey => 'crm/customer/$id';

  /// В карточке — телефон и контакт точки: на телефоне не сохраняются.
  @override
  bool get persist => false;

  @override
  Future<FieldCustomerCard> fetch(CancelToken cancel) => ref.read(crmApiProvider).customer(id, cancel);

  @override
  FieldCustomerCard decode(Object? json) => FieldCustomerCard.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(FieldCustomerCard data) => data.toJson();
}

final customerBlock = NotifierProvider.autoDispose.family<CustomerBlock, BlockState<FieldCustomerCard>, int>(CustomerBlock.new);

class AuditBlock extends CachedBlock<List<AuditEntry>> {
  AuditBlock(this.id);

  final int id;

  @override
  String get cacheKey => 'crm/audit/$id';

  @override
  bool get persist => false;

  @override
  Future<List<AuditEntry>> fetch(CancelToken cancel) => ref.read(crmApiProvider).audit(id, cancel);

  @override
  List<AuditEntry> decode(Object? json) => [for (final e in json! as List<Object?>) AuditEntry.fromJson(e! as Map<String, Object?>)];

  @override
  Object? encode(List<AuditEntry> data) => [for (final e in data) e.toJson()];
}

final auditBlock = NotifierProvider.autoDispose.family<AuditBlock, BlockState<List<AuditEntry>>, int>(AuditBlock.new);

// ───────────── Подписи ─────────────

String statusLabel(AppLocalizations l10n, String s) => switch (s) {
      'Problem' => l10n.crmStatusProblem,
      'Inactive' => l10n.crmStatusInactive,
      _ => l10n.crmStatusActive,
    };

Tone statusTone(String s) => switch (s) {
      'Problem' => Tone.bad,
      'Inactive' => Tone.neutral,
      _ => Tone.ok,
    };

String priorityLabel(AppLocalizations l10n, String p) => switch (p) {
      'Low' => l10n.crmPriorityLow,
      'High' => l10n.crmPriorityHigh,
      'Urgent' => l10n.crmPriorityUrgent,
      _ => l10n.crmPriorityMedium,
    };

Tone priorityTone(String p) => switch (p) {
      'High' => Tone.warn,
      'Urgent' => Tone.bad,
      _ => Tone.neutral,
    };

String _date(String? iso) => iso == null || iso.length < 10 ? '—' : '${iso.substring(8, 10)}.${iso.substring(5, 7)}';

// ───────────── Список точек ─────────────

/// «Клиенты»: точки Sales Base с поиском (по паузе ввода), подборками «внимание», статусом и сортировкой; подгрузка по мере прокрутки.
class ClientsScreen extends ConsumerStatefulWidget {
  const ClientsScreen({super.key});

  @override
  ConsumerState<ClientsScreen> createState() => _ClientsScreenState();
}

class _ClientsScreenState extends ConsumerState<ClientsScreen> {
  final _debounce = Debouncer();
  final _scroll = ScrollController();

  @override
  void initState() {
    super.initState();
    _scroll.addListener(() {
      if (_scroll.position.extentAfter < 600) ref.read(customersProvider.notifier).loadMore();
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
    final q = ref.watch(customerQueryProvider);
    final s = ref.watch(customersProvider);
    void setQuery(CustomerQuery n) => ref.read(customerQueryProvider.notifier).set(n);
    final attention = <String?, String>{
      null: l10n.crmAttAll,
      'decline': l10n.crmAttDecline,
      'lost': l10n.crmAttLost,
      'notvisited': l10n.crmAttNotVisited,
      'new': l10n.crmAttNew,
    };
    final sorts = <String?, String>{null: l10n.crmSortSales, 'trend': l10n.crmSortTrend, 'lastvisit': l10n.crmSortLastVisit, 'name': l10n.crmSortName};
    final noAccess = s.failure?.kind == ApiErrorKind.forbidden;

    return ObPage(
      title: l10n.moduleSales,
      controller: _scroll,
      onRefresh: () => ref.read(customersProvider.notifier).refresh(),
      slivers: [
        SliverToBoxAdapter(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              TextField(
                decoration: InputDecoration(hintText: l10n.crmSearch, prefixIcon: const Icon(LucideIcons.search, size: 18), isDense: true),
                onChanged: (v) => _debounce(() => setQuery(q.copyWith(search: v))),
              ),
              const SizedBox(height: 10),
              SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                clipBehavior: Clip.none,
                child: Row(
                  children: [
                    for (final e in attention.entries) ...[
                      FilterPill(label: e.value, chevron: false, active: q.attention == e.key, onTap: () => setQuery(q.copyWith(attention: () => e.key))),
                      const SizedBox(width: 8),
                    ],
                  ],
                ),
              ),
              const SizedBox(height: 8),
              Row(
                children: [
                  for (final st in const ['Active', 'Problem', 'Inactive']) ...[
                    FilterPill(
                      label: statusLabel(l10n, st),
                      chevron: false,
                      active: q.status == st,
                      onTap: () => setQuery(q.copyWith(status: () => q.status == st ? null : st)),
                    ),
                    const SizedBox(width: 8),
                  ],
                  const Spacer(),
                  PopupMenuButton<String?>(
                    initialValue: q.sort,
                    onSelected: (v) => setQuery(q.copyWith(sort: () => v)),
                    itemBuilder: (context) => [for (final e in sorts.entries) PopupMenuItem(value: e.key, child: Text(e.value))],
                    child: Icon(LucideIcons.arrowDownUp, size: 18, color: c.accentStrong),
                  ),
                ],
              ),
              const SizedBox(height: 12),
            ],
          ),
        ),
        if (noAccess)
          SliverToBoxAdapter(
            child: ObCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(l10n.crmNoAccess, style: TextStyle(fontSize: 14, height: 1.4, color: c.ink2)),
                  const SizedBox(height: 12),
                  OutlinedButton(onPressed: () => context.go('/analytics/sales/agents'), child: Text(l10n.anAgents)),
                ],
              ),
            ),
          )
        else if (s.isInitialLoading)
          const SliverToBoxAdapter(child: LoadingView())
        else if (s.items.isEmpty && s.failure != null)
          SliverToBoxAdapter(child: ErrorView(failure: s.failure!, onRetry: () => ref.read(customersProvider.notifier).refresh()))
        else if (s.items.isEmpty && !s.loading)
          SliverToBoxAdapter(child: EmptyView(icon: LucideIcons.store, title: l10n.stateEmpty, compact: true))
        else
          SliverList.separated(
            itemCount: s.items.length + (s.hasMore ? 1 : 0),
            separatorBuilder: (context, i) => const SizedBox(height: 10),
            itemBuilder: (context, i) {
              if (i >= s.items.length) {
                return const Padding(padding: EdgeInsets.all(16), child: Center(child: CircularProgressIndicator.adaptive()));
              }
              final r = s.items[i];
              return ObCard(
                onTap: () => context.push('/home/sales/points/${r.marketId}'),
                padding: const EdgeInsets.all(14),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Expanded(child: Text(r.name, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 15, fontWeight: FontWeight.w600, color: c.ink))),
                        SoftBadge(label: statusLabel(l10n, r.status), tone: statusTone(r.status)),
                      ],
                    ),
                    if (r.address != null) ...[
                      const SizedBox(height: 2),
                      Text(r.address!, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12, color: c.ink3)),
                    ],
                    const SizedBox(height: 8),
                    Row(
                      children: [
                        Expanded(
                          child: Text(
                            [l10n.crmSales30(fmt.compact(r.sales30)), if (r.lastVisitDate != null) l10n.crmLastVisit(_date(r.lastVisitDate)), ?r.agentName].join(' · '),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: TextStyle(fontSize: 12, color: c.ink2),
                          ),
                        ),
                        if (r.trendPct != null)
                          Text('${fmt.signed(r.trendPct!, 0)}%',
                              style: TextStyle(fontSize: 12, fontWeight: FontWeight.w700, color: r.trendPct! >= 0 ? c.ok : c.bad)),
                      ],
                    ),
                    if (r.priority == 'High' || r.priority == 'Urgent') ...[
                      const SizedBox(height: 8),
                      SoftBadge(label: priorityLabel(l10n, r.priority), tone: priorityTone(r.priority)),
                    ],
                  ],
                ),
              );
            },
          ),
      ],
    );
  }
}

// ───────────── Карточка точки ─────────────

/// Точка: статус и приоритет, показатели, ответственный ТП, контакты, продажи месяца (категории, артикулы, кто продавал), отгрузки по
/// месяцам, заказы, визиты, история изменений; правка карточки Sales Base — у кого есть право. Без Sales Base — только продажи месяца.
class ClientScreen extends ConsumerWidget {
  const ClientScreen({super.key, required this.id});

  final int id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final c = context.ob;
    final fmt = context.fmt;
    final card = ref.watch(customerBlock(id));
    final month = ref.watch(salesMonthProvider);
    final store = ref.watch(storeBlock((id: '$id', month: month)));
    final audit = ref.watch(auditBlock(id));
    final name = card.data?.customer.name ?? store.data?.name ?? l10n.anStore;
    final cd = card.data;
    return ObPage(
      title: name,
      subtitle: cd?.customer.address == null ? null : Text(cd!.customer.address!),
      actions: [
        if (cd?.canEdit ?? false)
          IconButton(icon: const Icon(LucideIcons.pencil, size: 20), tooltip: l10n.crmEdit, onPressed: () => _edit(context, ref, cd!)),
      ],
      onRefresh: () async {
        await Future.wait([
          ref.read(customerBlock(id).notifier).refresh(),
          ref.read(storeBlock((id: '$id', month: month)).notifier).refresh(),
          ref.read(auditBlock(id).notifier).refresh(),
        ]);
      },
      slivers: [
        SliverList.list(
          children: [
            if (cd != null) ...[
              Wrap(spacing: 6, runSpacing: 6, children: [
                SoftBadge(label: statusLabel(l10n, cd.customer.status), tone: statusTone(cd.customer.status), size: 12),
                SoftBadge(label: '${l10n.crmPriority}: ${priorityLabel(l10n, cd.customer.priority)}', tone: priorityTone(cd.customer.priority), size: 12),
                if (cd.customer.branch != null) SoftBadge(label: cd.customer.branch!, tone: Tone.neutral, size: 12),
              ]),
              const SizedBox(height: 12),
              ObCard(
                tint: c.kpi,
                child: Column(
                  children: [
                    MetricRow(items: [
                      (l10n.crmMonthToDate, fmt.compact(cd.monthToDate), cd.customer.monthlyTarget == null ? null : '${l10n.crmTarget}: ${fmt.compact(cd.customer.monthlyTarget!)}'),
                      (l10n.crmOrders30, '${cd.orders30}', null),
                      (l10n.crmVisits30, '${cd.visits30}', null),
                    ]),
                    const SizedBox(height: 12),
                    MetricRow(items: [
                      (l10n.crm30d, fmt.compact(cd.customer.sales30), null),
                      (l10n.crm90d, fmt.compact(cd.customer.sales90), null),
                      (l10n.crmTrend, cd.customer.trendPct == null ? '—' : '${fmt.signed(cd.customer.trendPct!, 0)}%', null),
                    ]),
                  ],
                ),
              ),
              const SizedBox(height: 12),
              ObCard(
                child: Column(
                  children: [
                    _Line(icon: LucideIcons.userRound, label: l10n.crmResponsible, value: cd.linkoResponsibleName ?? cd.customer.agentName ?? '—',
                        onTap: cd.linkoResponsibleId == null ? null : () => context.push('/analytics/sales/agents/${cd.linkoResponsibleId}')),
                    if (cd.contactPerson != null) _Line(icon: LucideIcons.contact, label: l10n.crmContact, value: cd.contactPerson!),
                    if (cd.phone != null) _Line(icon: LucideIcons.phone, label: l10n.crmPhone, value: cd.phone!),
                    if (cd.note != null) _Line(icon: LucideIcons.stickyNote, label: l10n.crmNote, value: cd.note!),
                  ],
                ),
              ),
            ] else if (card.failure != null && !card.forbidden && card.failure!.kind != ApiErrorKind.notFound)
              ErrorView(failure: card.failure!, onRetry: () => ref.read(customerBlock(id).notifier).refresh(), compact: true),
            const SizedBox(height: ObSpace.block),
            SectionHeader(title: l10n.crmMonthSales, trailing: const MonthPill()),
            const SizedBox(height: 12),
            blockView(context, store, onRetry: () => ref.read(storeBlock((id: '$id', month: month)).notifier).refresh(), (st) {
              return Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  ObCard(
                    child: MetricRow(items: [
                      (l10n.anFact, fmt.weight(st.factKg), l10n.anWas(fmt.weight(st.prevMonthKg))),
                      (l10n.anRevenue, fmt.compact(st.revenue), l10n.anWas(fmt.compact(st.prevMonthRevenue))),
                      (l10n.anOrders, '${st.orders}', l10n.anCategoriesN(st.categories)),
                    ]),
                  ),
                  if (st.categoryRows.isNotEmpty) ...[
                    const SizedBox(height: 10),
                    ObListCard(children: [
                      for (final r in st.categoryRows)
                        Padding(
                          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                          child: Row(children: [
                            Expanded(child: Text(r.name, style: TextStyle(fontSize: 13, color: c.ink))),
                            Text(fmt.weight(r.kg), style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: c.ink)),
                            SizedBox(width: 52, child: Text(r.share == null ? '' : fmt.percent(r.share!), textAlign: TextAlign.right, style: TextStyle(fontSize: 12, color: c.ink3))),
                          ]),
                        ),
                    ]),
                  ],
                  if (st.agents.isNotEmpty) ...[
                    const SizedBox(height: 8),
                    Text('${l10n.anStoreAgents}: ${st.agents.join(', ')}', style: TextStyle(fontSize: 12, color: c.ink3)),
                  ],
                ],
              );
            }),
            if (cd != null && cd.months.isNotEmpty) ...[
              const SizedBox(height: ObSpace.block),
              SectionHeader(title: l10n.crmMonths),
              const SizedBox(height: 12),
              ObCard(child: DailyBars(values: [for (final m in cd.months) m.sum], format: fmt.compact, height: 80)),
            ],
            if (cd != null && cd.orders.isNotEmpty) ...[
              const SizedBox(height: ObSpace.block),
              SectionHeader(title: l10n.crmOrders),
              const SizedBox(height: 12),
              ObListCard(children: [
                for (final o in cd.orders.take(15))
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                    child: Row(children: [
                      SizedBox(width: 48, child: Text(_date(o.date), style: TextStyle(fontSize: 13, color: c.ink2))),
                      Expanded(child: Text(o.agent ?? '', maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12, color: c.ink3))),
                      Text(fmt.compact(o.sum), style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: c.ink)),
                    ]),
                  ),
              ]),
            ],
            if (cd != null && cd.visits.isNotEmpty) ...[
              const SizedBox(height: ObSpace.block),
              SectionHeader(title: l10n.crmVisits),
              const SizedBox(height: 12),
              ObListCard(children: [
                for (final v in cd.visits.take(15))
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                    child: Row(children: [
                      SizedBox(width: 48, child: Text(_date(v.at), style: TextStyle(fontSize: 13, color: c.ink2))),
                      Expanded(
                        child: Text([v.status, ?v.result, ?v.agent].join(' · '), maxLines: 2, style: TextStyle(fontSize: 12, color: c.ink3)),
                      ),
                    ]),
                  ),
              ]),
            ],
            if (audit.data != null && audit.data!.isNotEmpty) ...[
              const SizedBox(height: ObSpace.block),
              SectionHeader(title: l10n.crmHistory),
              const SizedBox(height: 12),
              ObListCard(children: [
                for (final e in audit.data!.take(20))
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                    child: Row(children: [
                      SizedBox(width: 48, child: Text(_date(e.timestamp), style: TextStyle(fontSize: 13, color: c.ink2))),
                      Expanded(
                        child: Text(
                          '${e.action.endsWith('agent_changed') ? l10n.auditAgent : l10n.auditUpdated} · ${e.actor}',
                          style: TextStyle(fontSize: 12, color: c.ink3),
                        ),
                      ),
                    ]),
                  ),
              ]),
            ],
          ],
        ),
      ],
    );
  }

  Future<void> _edit(BuildContext context, WidgetRef ref, FieldCustomerCard cd) async {
    final saved = await showObSheet<bool>(context, title: context.l10n.crmEditTitle, builder: (context) => _EditForm(card: cd));
    if (saved == true) {
      await ref.read(customerBlock(id).notifier).refresh();
      await ref.read(auditBlock(id).notifier).refresh();
      if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(context.l10n.crmSaved)));
    }
  }
}

class _Line extends StatelessWidget {
  const _Line({required this.icon, required this.label, required this.value, this.onTap});

  final IconData icon;
  final String label;
  final String value;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    return Pressable(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 7),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(icon, size: 16, color: c.ink3),
            const SizedBox(width: 10),
            SizedBox(width: 140, child: Text(label, style: TextStyle(fontSize: 13, color: c.ink3))),
            Expanded(child: Text(value, style: TextStyle(fontSize: 13, color: onTap == null ? c.ink : c.accentStrong))),
          ],
        ),
      ),
    );
  }
}

/// Правка карточки Sales Base: статус, приоритет, план на месяц, контакт, телефон, заметка. Сохранение — только при связи, с ответом
/// сервера (без оптимистичного интерфейса: правку видят руководители и ТП).
class _EditForm extends ConsumerStatefulWidget {
  const _EditForm({required this.card});

  final FieldCustomerCard card;

  @override
  ConsumerState<_EditForm> createState() => _EditFormState();
}

class _EditFormState extends ConsumerState<_EditForm> {
  late String _status = widget.card.customer.status;
  late String _priority = widget.card.customer.priority;
  late final _target = TextEditingController(text: widget.card.customer.monthlyTarget?.round().toString() ?? '');
  late final _contact = TextEditingController(text: widget.card.contactPerson ?? '');
  late final _phone = TextEditingController(text: widget.card.phone ?? '');
  late final _note = TextEditingController(text: widget.card.note ?? '');
  bool _saving = false;
  AppFailure? _error;

  @override
  void dispose() {
    for (final c in [_target, _contact, _phone, _note]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _save() async {
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      await ref.read(crmApiProvider).update(widget.card.customer.marketId, {
        'status': _status,
        'priority': _priority,
        'monthlyTarget': double.tryParse(_target.text.replaceAll(' ', '')) ?? 0,
        'contactPerson': _contact.text.trim(),
        'phone': _phone.text.trim(),
        'note': _note.text.trim(),
      });
      if (mounted) Navigator.pop(context, true);
    } on Object catch (e) {
      setState(() {
        _saving = false;
        _error = AppFailure.from(e);
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final c = context.ob;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(l10n.crmStatus, style: TextStyle(fontSize: 12, color: c.ink3)),
        const SizedBox(height: 6),
        Wrap(spacing: 8, children: [
          for (final s in const ['Active', 'Problem', 'Inactive'])
            FilterPill(label: statusLabel(l10n, s), chevron: false, active: _status == s, onTap: () => setState(() => _status = s)),
        ]),
        const SizedBox(height: 14),
        Text(l10n.crmPriority, style: TextStyle(fontSize: 12, color: c.ink3)),
        const SizedBox(height: 6),
        Wrap(spacing: 8, runSpacing: 8, children: [
          for (final p in const ['Low', 'Medium', 'High', 'Urgent'])
            FilterPill(label: priorityLabel(l10n, p), chevron: false, active: _priority == p, onTap: () => setState(() => _priority = p)),
        ]),
        const SizedBox(height: 14),
        TextField(controller: _target, keyboardType: TextInputType.number, decoration: InputDecoration(labelText: l10n.crmTarget)),
        const SizedBox(height: 10),
        TextField(controller: _contact, maxLength: 200, decoration: InputDecoration(labelText: l10n.crmContact, counterText: '')),
        const SizedBox(height: 10),
        TextField(controller: _phone, maxLength: 32, keyboardType: TextInputType.phone, decoration: InputDecoration(labelText: l10n.crmPhone, counterText: '')),
        const SizedBox(height: 10),
        TextField(controller: _note, maxLength: 2000, maxLines: 3, decoration: InputDecoration(labelText: l10n.crmNote, counterText: '')),
        const SizedBox(height: 8),
        Text(l10n.crmLinkoReadonly, style: TextStyle(fontSize: 11, height: 1.4, color: c.ink3)),
        if (_error != null) ...[const SizedBox(height: 8), Text(_error!.describe(l10n), style: TextStyle(fontSize: 13, color: c.bad))],
        const SizedBox(height: 14),
        FilledButton(
          onPressed: _saving ? null : _save,
          child: _saving
              ? const SizedBox.square(dimension: 20, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
              : Text(l10n.actionSave),
        ),
      ],
    );
  }
}
