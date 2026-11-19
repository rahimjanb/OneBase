import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/cache/cached_block.dart';
import '../../../core/cache/paged.dart';
import '../../../core/core_providers.dart';
import '../../../l10n/gen/app_localizations.dart';
import '../../../shared/extensions/context_ext.dart';
import '../../../shared/widgets/detail_sheet.dart';
import '../../../shared/widgets/ob_app_bar.dart';
import '../../../shared/widgets/ob_card.dart';
import '../../../shared/widgets/pills.dart';
import '../../../shared/widgets/pressable.dart';
import '../../../shared/widgets/rings.dart';
import '../../../shared/widgets/section_header.dart';
import '../../../shared/widgets/segmented.dart';
import '../../../shared/widgets/states.dart';
import '../../analytics/presentation/analytics_screen.dart';
import '../../analytics/presentation/widgets/sales_widgets.dart';
import '../../dashboard/data/dashboard_models.dart';
import '../../dashboard/domain/home_view.dart';
import '../data/finance_api.dart';
import '../data/finance_models.dart';

// ───────────── Фильтр: период и организация 1С ─────────────

enum FinPeriod { mtd, prev, quarter, year }

/// Период по времени Ташкента: этот месяц до сегодня, прошлый месяц, квартал до сегодня, с начала года.
({String from, String to}) finRange(FinPeriod p, DateTime now) {
  final t = tashkentNow(now);
  final today = DateTime.utc(t.year, t.month, t.day);
  return switch (p) {
    FinPeriod.mtd => (from: isoDate(DateTime.utc(t.year, t.month)), to: isoDate(today)),
    FinPeriod.prev => (from: isoDate(DateTime.utc(t.year, t.month - 1)), to: isoDate(DateTime.utc(t.year, t.month, 0))),
    FinPeriod.quarter => (from: isoDate(DateTime.utc(t.year, ((t.month - 1) ~/ 3) * 3 + 1)), to: isoDate(today)),
    FinPeriod.year => (from: isoDate(DateTime.utc(t.year)), to: isoDate(today)),
  };
}

const _orgKey = 'finance_org';

class FinStateController extends Notifier<({FinPeriod period, String? org})> {
  @override
  ({FinPeriod period, String? org}) build() => (period: FinPeriod.mtd, org: ref.watch(initialSettingsProvider)[_orgKey]);

  void setPeriod(FinPeriod p) => state = (period: p, org: state.org);

  /// Организация 1С — настройка телефона (сервер её не хранит), как фильтр веба.
  Future<void> setOrg(String? id) async {
    state = (period: state.period, org: id);
    await ref.read(settingsStoreProvider).write(_orgKey, id);
  }
}

final finStateProvider = NotifierProvider<FinStateController, ({FinPeriod period, String? org})>(FinStateController.new);

final finFilterProvider = Provider<FinFilter>((ref) {
  final s = ref.watch(finStateProvider);
  final r = finRange(s.period, ref.watch(clockProvider)());
  return (from: r.from, to: r.to, organization: s.org);
});

FinanceApi _api(Ref ref) => ref.read(financeApiProvider);
String _k(FinFilter f) => '${f.from}_${f.to}_${f.organization ?? 'all'}';

class FinOrgsBlock extends CachedBlock<List<FinanceOrganization>> {
  @override
  String get cacheKey => 'finance/orgs';

  @override
  Future<List<FinanceOrganization>> fetch(CancelToken cancel) => _api(ref).organizations(cancel);

  @override
  List<FinanceOrganization> decode(Object? json) => [for (final e in json! as List<Object?>) FinanceOrganization.fromJson(e! as Map<String, Object?>)];

  @override
  Object? encode(List<FinanceOrganization> data) => [for (final o in data) o.toJson()];
}

final finOrgsBlock = NotifierProvider.autoDispose<FinOrgsBlock, BlockState<List<FinanceOrganization>>>(FinOrgsBlock.new);

class _FinBlock<T> extends CachedBlock<T> {
  _FinBlock(this.name, this.filter, this.load, this.fromJson, this.toJson, {this.save = true});

  final String name;
  final FinFilter filter;
  final Future<T> Function(FinanceApi api, FinFilter f, CancelToken c) load;
  final T Function(Map<String, Object?>) fromJson;
  final Map<String, Object?> Function(T) toJson;
  final bool save;

  @override
  String get cacheKey => 'finance/$name/${_k(filter)}';

  @override
  bool get persist => save;

  @override
  Future<T> fetch(CancelToken cancel) => load(_api(ref), filter, cancel);

  @override
  T decode(Object? json) => fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(T data) => toJson(data);
}

final finOverviewBlock = NotifierProvider.autoDispose.family<_FinBlock<FinOverview>, BlockState<FinOverview>, FinFilter>(
  (f) => _FinBlock('overview', f, (a, f, c) => a.overview(f, c), FinOverview.fromJson, (v) => v.toJson()),
);
final finPnlBlock = NotifierProvider.autoDispose.family<_FinBlock<FinPnl>, BlockState<FinPnl>, FinFilter>(
  (f) => _FinBlock('pnl', f, (a, f, c) => a.pnl(f, c), FinPnl.fromJson, (v) => v.toJson()),
);
final finCashBlock = NotifierProvider.autoDispose.family<_FinBlock<FinCash>, BlockState<FinCash>, FinFilter>(
  (f) => _FinBlock('cash', f, (a, f, c) => a.cash(f, c), FinCash.fromJson, (v) => v.toJson()),
);
final finReceivablesBlock = NotifierProvider.autoDispose.family<_FinBlock<FinSettlements>, BlockState<FinSettlements>, FinFilter>(
  // Контрагенты — по названиям; кэш только в памяти.
  (f) => _FinBlock('receivables', f, (a, f, c) => a.settlements(f, 'receivables', c), FinSettlements.fromJson, (v) => v.toJson(), save: false),
);
final finPayablesBlock = NotifierProvider.autoDispose.family<_FinBlock<FinSettlements>, BlockState<FinSettlements>, FinFilter>(
  (f) => _FinBlock('payables', f, (a, f, c) => a.settlements(f, 'payables', c), FinSettlements.fromJson, (v) => v.toJson(), save: false),
);
final finAnomaliesBlock = NotifierProvider.autoDispose.family<_FinBlock<FinAnomalies>, BlockState<FinAnomalies>, FinFilter>(
  (f) => _FinBlock('anomalies', f, (a, f, c) => a.anomalies(f, c), FinAnomalies.fromJson, (v) => v.toJson(), save: false),
);
final finAccountsBlock = NotifierProvider.autoDispose.family<_FinBlock<FinAccounts>, BlockState<FinAccounts>, FinFilter>(
  (f) => _FinBlock('accounts', f, (a, f, c) => a.accounts(f, c), FinAccounts.fromJson, (v) => v.toJson()),
);

class FinDocumentBlock extends CachedBlock<FinDocument> {
  FinDocumentBlock(this.id);

  final String id;

  @override
  String get cacheKey => 'finance/document/$id';

  @override
  bool get persist => false;

  @override
  Future<FinDocument> fetch(CancelToken cancel) => _api(ref).document(id, cancel);

  @override
  FinDocument decode(Object? json) => FinDocument.fromJson(json! as Map<String, Object?>);

  @override
  Object? encode(FinDocument data) => data.toJson();
}

final finDocumentBlock = NotifierProvider.autoDispose.family<FinDocumentBlock, BlockState<FinDocument>, String>(FinDocumentBlock.new);

typedef EntryArgs = ({FinFilter filter, String? account, String? counterparty, String? registrar});

class EntriesController extends PagedController<EntryView> {
  EntriesController(this.args);

  final EntryArgs args;

  @override
  Future<PageChunk<EntryView>> fetchPage(int page, String? cursor, CancelToken cancel) => _api(ref).entries(
        args.filter,
        {'account': ?args.account, 'counterparty': ?args.counterparty, 'registrar': ?args.registrar},
        page,
        cancel,
      );
}

final entriesProvider = NotifierProvider.autoDispose.family<EntriesController, PagedState<EntryView>, EntryArgs>(EntriesController.new);

/// Ссылки отклонений и строк (пути веба) → экраны раздела.
String financeRoute(String? link) {
  if (link == null) return '/home/finance';
  final u = Uri.parse(link);
  final s = u.pathSegments;
  if (s.length >= 3 && s[1] == 'documents') return '/home/finance/documents/${s[2]}';
  if (s.length >= 2 && s[1] == 'entries') return Uri(path: '/home/finance/entries', queryParameters: u.queryParameters.isEmpty ? null : u.queryParameters).toString();
  if (s.length >= 3 && s[1] == 'accounts') return Uri(path: '/home/finance/entries', queryParameters: {'account': s[2]}).toString();
  if (s.length >= 2 && s[1] == 'accounts') return '/home/finance/accounts';
  return '/home/finance';
}

// ───────────── Общее ─────────────

String periodName(AppLocalizations l10n, FinPeriod p) => switch (p) {
      FinPeriod.mtd => l10n.finPeriodMtd,
      FinPeriod.prev => l10n.finPeriodPrev,
      FinPeriod.quarter => l10n.finPeriodQuarter,
      FinPeriod.year => l10n.finPeriodYear,
    };

/// Пилюли фильтра: период и организация 1С (если организаций больше одной).
class FinFilterBar extends ConsumerWidget {
  const FinFilterBar({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final s = ref.watch(finStateProvider);
    final orgs = ref.watch(finOrgsBlock).data ?? const <FinanceOrganization>[];
    final org = orgs.where((o) => o.id == s.org).firstOrNull;
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      clipBehavior: Clip.none,
      child: Row(
        children: [
          FilterPill(
            icon: LucideIcons.calendar,
            label: periodName(l10n, s.period),
            onTap: () => showObSheet<void>(
              context,
              title: l10n.periodTitle,
              builder: (context) => Column(children: [
                for (final p in FinPeriod.values)
                  _Option(label: periodName(l10n, p), selected: p == s.period, onTap: () {
                    ref.read(finStateProvider.notifier).setPeriod(p);
                    Navigator.pop(context);
                  }),
              ]),
            ),
          ),
          if (orgs.length > 1) ...[
            const SizedBox(width: 8),
            FilterPill(
              icon: LucideIcons.building,
              label: org?.name ?? l10n.finAllOrgs,
              active: org != null,
              onTap: () => showObSheet<void>(
                context,
                title: l10n.organization1c,
                builder: (context) => Column(children: [
                  _Option(label: l10n.finAllOrgs, selected: org == null, onTap: () {
                    ref.read(finStateProvider.notifier).setOrg(null);
                    Navigator.pop(context);
                  }),
                  for (final o in orgs)
                    _Option(label: o.name, selected: o.id == s.org, onTap: () {
                      ref.read(finStateProvider.notifier).setOrg(o.id);
                      Navigator.pop(context);
                    }),
                ]),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _Option extends StatelessWidget {
  const _Option({required this.label, required this.selected, required this.onTap});

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    return Pressable(
      onTap: onTap,
      child: Container(
        height: 48,
        padding: const EdgeInsets.symmetric(horizontal: 12),
        alignment: Alignment.centerLeft,
        decoration: BoxDecoration(color: selected ? c.accentSoft : null, borderRadius: BorderRadius.circular(ObRadius.md)),
        child: Text(label, style: TextStyle(fontSize: 15, fontWeight: selected ? FontWeight.w600 : FontWeight.w400, color: selected ? c.accentStrong : c.ink)),
      ),
    );
  }
}

/// Данных 1С нет — пояснение сервера (а не нули); демо-источник — предупреждение.
Widget? metaNotice(BuildContext context, FinanceMeta m) {
  final c = context.ob;
  if (!m.hasData) {
    return ObCard(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const IconTile(icon: LucideIcons.landmark, size: 38),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(context.l10n.financeNoData, style: TextStyle(fontSize: 15, fontWeight: FontWeight.w600, color: c.ink)),
                if (m.note != null) ...[const SizedBox(height: 4), Text(m.note!, style: TextStyle(fontSize: 13, height: 1.4, color: c.ink3))],
              ],
            ),
          ),
        ],
      ),
    );
  }
  if (m.demo) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(color: c.warnSoft, borderRadius: BorderRadius.circular(12)),
      child: Text(context.l10n.finDemo, style: TextStyle(fontSize: 13, color: c.warn)),
    );
  }
  return null;
}

String? _delta(BuildContext context, double value, double? previous) {
  if (previous == null || previous == 0) return null;
  return context.l10n.finVsPrev(context.fmt.signedPercent(value / previous.abs() - 1));
}

class _AmountList extends StatelessWidget {
  const _AmountList({required this.rows, this.showPrevious = false});

  final List<AmountRow> rows;
  final bool showPrevious;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final fmt = context.fmt;
    return ObListCard(children: [
      for (final r in rows)
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 11),
          child: Row(children: [
            Expanded(child: Text(r.title, style: TextStyle(fontSize: 13, color: c.ink))),
            Column(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                Text(fmt.compact(r.amount), style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: c.ink)),
                if (showPrevious && r.previous != null) Text(fmt.compact(r.previous!), style: TextStyle(fontSize: 11, color: c.ink3)),
                if (r.currency != null && r.currency != 'UZS' && r.currencyAmount != null)
                  Text('${fmt.decimal(r.currencyAmount!, digits: 2)} ${r.currency}', style: TextStyle(fontSize: 11, color: c.ink3)),
              ],
            ),
          ]),
        ),
    ]);
  }
}

// ───────────── Раздел ─────────────

enum _FinTab { overview, pnl, cash, debts, anomalies }

/// «Финансы»: факт 1С — обзор, доходы и расходы, деньги, долги, отклонения; проводки, документы и счета — вглубь.
class FinanceScreen extends ConsumerStatefulWidget {
  const FinanceScreen({super.key});

  @override
  ConsumerState<FinanceScreen> createState() => _FinanceScreenState();
}

class _FinanceScreenState extends ConsumerState<FinanceScreen> {
  _FinTab _tab = _FinTab.overview;
  bool _payables = false;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final f = ref.watch(finFilterProvider);
    String tabLabel(_FinTab t) => switch (t) {
          _FinTab.overview => l10n.finTabOverview,
          _FinTab.pnl => l10n.finTabPnl,
          _FinTab.cash => l10n.finTabCash,
          _FinTab.debts => l10n.finTabDebts,
          _FinTab.anomalies => l10n.finTabAnomalies,
        };
    Future<void> refresh() => switch (_tab) {
          _FinTab.overview => ref.read(finOverviewBlock(f).notifier).refresh(),
          _FinTab.pnl => ref.read(finPnlBlock(f).notifier).refresh(),
          _FinTab.cash => ref.read(finCashBlock(f).notifier).refresh(),
          _FinTab.debts => ref.read((_payables ? finPayablesBlock(f) : finReceivablesBlock(f)).notifier).refresh(),
          _FinTab.anomalies => ref.read(finAnomaliesBlock(f).notifier).refresh(),
        };
    return ObPage(
      title: l10n.moduleFinance,
      actions: [
        IconButton(icon: const Icon(LucideIcons.listTree, size: 20), tooltip: l10n.finAccounts, onPressed: () => context.push('/home/finance/accounts')),
        IconButton(icon: const Icon(LucideIcons.bookOpen, size: 20), tooltip: l10n.finEntries, onPressed: () => context.push('/home/finance/entries')),
      ],
      onRefresh: refresh,
      slivers: [
        SliverToBoxAdapter(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const FinFilterBar(),
              const SizedBox(height: 12),
              SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                clipBehavior: Clip.none,
                child: Row(children: [
                  for (final t in _FinTab.values) ...[
                    FilterPill(label: tabLabel(t), chevron: false, active: t == _tab, onTap: () => setState(() => _tab = t)),
                    const SizedBox(width: 8),
                  ],
                ]),
              ),
              const SizedBox(height: 16),
              switch (_tab) {
                _FinTab.overview => _Overview(filter: f),
                _FinTab.pnl => _Pnl(filter: f),
                _FinTab.cash => _Cash(filter: f),
                _FinTab.debts => _Debts(filter: f, payables: _payables, onKind: (v) => setState(() => _payables = v)),
                _FinTab.anomalies => _Anomalies(filter: f),
              },
            ],
          ),
        ),
      ],
    );
  }
}

class _Overview extends ConsumerWidget {
  const _Overview({required this.filter});

  final FinFilter filter;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final fmt = context.fmt;
    final c = context.ob;
    final s = ref.watch(finOverviewBlock(filter));
    return blockView(context, s, onRetry: () => ref.read(finOverviewBlock(filter).notifier).refresh(), (o) {
      final notice = metaNotice(context, o.meta);
      if (!o.meta.hasData) return notice!;
      final revenue = o.line('net_revenue');
      final gross = o.line('gross_profit');
      final profit = o.line('net_profit');
      Widget tile(String label, PnlValue? v) => Expanded(
            child: TargetTile(label: label, value: fmt.compact(v?.value ?? 0), sub: v == null ? null : _delta(context, v.value, v.previous)),
          );
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          ?notice,
          if (o.meta.dataThrough != null) ...[
            Text(l10n.finDataThrough(o.meta.dataThrough!), style: TextStyle(fontSize: 12, color: c.ink3)),
            const SizedBox(height: 10),
          ],
          IntrinsicHeight(
            child: Row(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              tile(l10n.financeNetRevenue, revenue),
              const SizedBox(width: 10),
              tile(l10n.finGrossProfit, gross),
            ]),
          ),
          const SizedBox(height: 10),
          IntrinsicHeight(
            child: Row(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              tile(l10n.financeNetProfit, profit),
              const SizedBox(width: 10),
              Expanded(child: TargetTile(label: l10n.finCashTotal, value: fmt.compact(o.cashTotal))),
            ]),
          ),
          if (o.months.isNotEmpty) ...[
            const SizedBox(height: ObSpace.block),
            SectionHeader(title: l10n.finMonths),
            const SizedBox(height: 12),
            ObCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  DailyBars(values: [for (final m in o.months) m.revenue], format: fmt.compact, height: 90),
                  const SizedBox(height: 8),
                  DotLabel(color: c.accent, label: l10n.financeNetRevenue),
                ],
              ),
            ),
          ],
          const SizedBox(height: ObSpace.block),
          ObCard(
            child: MetricRow(items: [
              (l10n.finReceivables, fmt.compact(o.receivables), null),
              (l10n.finPayables, fmt.compact(o.payables), null),
              (l10n.finAdvances, fmt.compact(o.advancesGiven), null),
            ]),
          ),
          if (o.inventory.isNotEmpty) ...[
            const SizedBox(height: ObSpace.block),
            SectionHeader(title: l10n.finInventory),
            const SizedBox(height: 12),
            _AmountList(rows: o.inventory),
          ],
          if (o.expenseGrowth.isNotEmpty) ...[
            const SizedBox(height: ObSpace.block),
            SectionHeader(title: l10n.finExpenseGrowth),
            const SizedBox(height: 12),
            _AmountList(rows: o.expenseGrowth, showPrevious: true),
          ],
        ],
      );
    });
  }
}

class _Pnl extends ConsumerWidget {
  const _Pnl({required this.filter});

  final FinFilter filter;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final fmt = context.fmt;
    final c = context.ob;
    final s = ref.watch(finPnlBlock(filter));
    return blockView(context, s, onRetry: () => ref.read(finPnlBlock(filter).notifier).refresh(), (p) {
      if (!p.meta.hasData) return metaNotice(context, p.meta)!;
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          ObListCard(children: [
            for (final line in p.lines)
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                child: Row(children: [
                  Expanded(
                    child: Text(line.title,
                        style: TextStyle(fontSize: 13, fontWeight: line.kind == 'total' ? FontWeight.w700 : FontWeight.w400, color: c.ink)),
                  ),
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      Text(fmt.compact(line.total),
                          style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: line.kind == 'total' && line.total < 0 ? c.bad : c.ink)),
                      if (line.previous != null) Text(fmt.compact(line.previous!), style: TextStyle(fontSize: 11, color: c.ink3)),
                    ],
                  ),
                ]),
              ),
          ]),
          if (p.expensesByAccount.isNotEmpty) ...[
            const SizedBox(height: ObSpace.block),
            SectionHeader(title: l10n.finExpensesByAccount),
            const SizedBox(height: 12),
            _AmountList(rows: p.expensesByAccount, showPrevious: true),
          ],
          if (p.expensesByDepartment.isNotEmpty) ...[
            const SizedBox(height: ObSpace.block),
            SectionHeader(title: l10n.finExpensesByDepartment),
            const SizedBox(height: 12),
            _AmountList(rows: p.expensesByDepartment, showPrevious: true),
          ],
        ],
      );
    });
  }
}

class _Cash extends ConsumerWidget {
  const _Cash({required this.filter});

  final FinFilter filter;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final fmt = context.fmt;
    final c = context.ob;
    final s = ref.watch(finCashBlock(filter));
    return blockView(context, s, onRetry: () => ref.read(finCashBlock(filter).notifier).refresh(), (cash) {
      if (!cash.meta.hasData) return metaNotice(context, cash.meta)!;
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          ObCard(
            child: MetricRow(items: [
              (l10n.finInflow, fmt.compact(cash.inflow), null),
              (l10n.finOutflow, fmt.compact(cash.outflow), null),
              ('Δ', fmt.compact(cash.inflow - cash.outflow), null),
            ]),
          ),
          const SizedBox(height: 12),
          ObListCard(children: [
            for (final f in cash.flows)
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                child: Row(children: [
                  Icon(f.inflow ? LucideIcons.arrowDownLeft : LucideIcons.arrowUpRight, size: 16, color: f.inflow ? c.ok : c.bad),
                  const SizedBox(width: 10),
                  Expanded(child: Text(f.title, style: TextStyle(fontSize: 13, color: c.ink))),
                  Text(fmt.compact(f.total), style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: c.ink)),
                ]),
              ),
          ]),
          if (cash.balances.isNotEmpty) ...[
            const SizedBox(height: ObSpace.block),
            SectionHeader(title: l10n.finBalances),
            const SizedBox(height: 12),
            _AmountList(rows: cash.balances),
          ],
        ],
      );
    });
  }
}

class _Debts extends ConsumerWidget {
  const _Debts({required this.filter, required this.payables, required this.onKind});

  final FinFilter filter;
  final bool payables;
  final ValueChanged<bool> onKind;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final fmt = context.fmt;
    final c = context.ob;
    final provider = payables ? finPayablesBlock(filter) : finReceivablesBlock(filter);
    final s = ref.watch(provider);
    String bucket(String k) => switch (k) {
          'not_due' => l10n.finBucket_not_due,
          '1_30' => l10n.finBucket_1_30,
          '31_60' => l10n.finBucket_31_60,
          '61_90' => l10n.finBucket_61_90,
          'over_90' => l10n.finBucket_over_90,
          _ => l10n.finBucket_no_term,
        };
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        ObSegmented<bool>(
          values: const [false, true],
          selected: payables,
          label: (v) => v ? l10n.finPayables : l10n.finReceivables,
          onChanged: onKind,
        ),
        const SizedBox(height: 12),
        blockView(context, s, onRetry: () => ref.read(provider.notifier).refresh(), (d) {
          final notice = metaNotice(context, d.meta);
          if (!d.meta.hasData || d.rows.isEmpty) return notice ?? EmptyView(icon: LucideIcons.handCoins, title: l10n.stateEmpty, compact: true);
          final max = d.buckets.values.fold<double>(0, (a, b) => b > a ? b : a);
          return Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              ObCard(
                child: Column(children: [
                  MetricRow(items: [
                    (payables ? l10n.finPayables : l10n.finReceivables, fmt.compact(d.total), null),
                    (l10n.finOverdue, fmt.compact(d.overdue), null),
                    (l10n.finAdvances, fmt.compact(d.advances), null),
                  ]),
                  const SizedBox(height: 12),
                  for (final e in d.buckets.entries.where((e) => e.value > 0))
                    Padding(
                      padding: const EdgeInsets.symmetric(vertical: 4),
                      child: Row(children: [
                        SizedBox(width: 130, child: Text(bucket(e.key), style: TextStyle(fontSize: 12, color: c.ink2))),
                        Expanded(child: ProgressLine(value: max == 0 ? 0 : e.value / max, color: e.key == 'not_due' ? c.ok : c.warn, height: 5)),
                        SizedBox(width: 72, child: Text(fmt.compact(e.value), textAlign: TextAlign.right, style: TextStyle(fontSize: 12, color: c.ink))),
                      ]),
                    ),
                ]),
              ),
              const SizedBox(height: 12),
              ObListCard(children: [
                for (final r in d.rows.take(100))
                  Pressable(
                    onTap: r.counterpartyId == '-'
                        ? null
                        : () => context.push(Uri(path: '/home/finance/entries', queryParameters: {'counterparty': r.counterpartyId, 'account': payables ? '60' : '40'}).toString()),
                    child: Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 11),
                      child: Row(children: [
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(r.name, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 14, color: c.ink)),
                              if (r.maxOverdueDays != null && r.maxOverdueDays! > 0)
                                Text(l10n.finOverdueDays(r.maxOverdueDays!), style: TextStyle(fontSize: 11, color: c.warn)),
                            ],
                          ),
                        ),
                        Text(fmt.compact(r.balance), style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: c.ink)),
                      ]),
                    ),
                  ),
              ]),
              const SizedBox(height: 8),
              Text(d.method, style: TextStyle(fontSize: 11, height: 1.4, color: c.ink3)),
            ],
          );
        }),
      ],
    );
  }
}

class _Anomalies extends ConsumerWidget {
  const _Anomalies({required this.filter});

  final FinFilter filter;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final fmt = context.fmt;
    final c = context.ob;
    final s = ref.watch(finAnomaliesBlock(filter));
    return blockView(context, s, onRetry: () => ref.read(finAnomaliesBlock(filter).notifier).refresh(), (a) {
      final notice = metaNotice(context, a.meta);
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (!a.meta.hasData) ...[notice!, const SizedBox(height: 12)],
          if (a.items.isEmpty && a.meta.hasData)
            ObCard(child: Row(children: [
              Icon(LucideIcons.circleCheck, size: 18, color: c.ok),
              const SizedBox(width: 10),
              Text(l10n.finNoAnomalies, style: TextStyle(fontSize: 14, color: c.ink)),
            ])),
          for (final it in a.items) ...[
            ObCard(
              onTap: it.link == null ? null : () => context.push(financeRoute(it.link)),
              borderColor: (it.severity >= 3 ? c.bad : it.severity == 2 ? c.warn : c.line).withValues(alpha: it.severity >= 2 ? 0.4 : 1),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(children: [
                    SoftBadge(label: it.severity >= 3 ? l10n.severityCritical : it.severity == 2 ? l10n.severityWarning : l10n.severityOpportunity,
                        tone: it.severity >= 3 ? Tone.bad : it.severity == 2 ? Tone.warn : Tone.neutral),
                    const Spacer(),
                    if (it.amount != null) Text(fmt.compact(it.amount!), style: TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: c.ink)),
                  ]),
                  const SizedBox(height: 8),
                  Text(it.title, style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: c.ink)),
                  if (it.detail.isNotEmpty) ...[const SizedBox(height: 4), Text(it.detail, style: TextStyle(fontSize: 13, height: 1.4, color: c.ink2))],
                ],
              ),
            ),
            const SizedBox(height: 10),
          ],
          if (a.checks.isNotEmpty) ...[
            const SizedBox(height: 8),
            Text(l10n.finChecks, style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: c.ink3)),
            for (final ch in a.checks) Padding(padding: const EdgeInsets.only(top: 4), child: Text('· $ch', style: TextStyle(fontSize: 12, color: c.ink3))),
          ],
        ],
      );
    });
  }
}

// ───────────── Проводки, документ, счета ─────────────

class EntriesScreen extends ConsumerStatefulWidget {
  const EntriesScreen({super.key, this.account, this.counterparty, this.registrar});

  final String? account;
  final String? counterparty;
  final String? registrar;

  @override
  ConsumerState<EntriesScreen> createState() => _EntriesScreenState();
}

class _EntriesScreenState extends ConsumerState<EntriesScreen> {
  final _scroll = ScrollController();

  EntryArgs get _args => (filter: ref.read(finFilterProvider), account: widget.account, counterparty: widget.counterparty, registrar: widget.registrar);

  @override
  void initState() {
    super.initState();
    _scroll.addListener(() {
      if (_scroll.position.extentAfter < 500) ref.read(entriesProvider(_args).notifier).loadMore();
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
    final c = context.ob;
    final fmt = context.fmt;
    final args = (filter: ref.watch(finFilterProvider), account: widget.account, counterparty: widget.counterparty, registrar: widget.registrar);
    final s = ref.watch(entriesProvider(args));
    return ObPage(
      title: l10n.finEntries,
      subtitle: widget.account == null ? null : Text('${l10n.finDebit}/${l10n.finCredit} ${widget.account}'),
      controller: _scroll,
      onRefresh: () => ref.read(entriesProvider(args).notifier).refresh(),
      slivers: [
        const SliverToBoxAdapter(child: Padding(padding: EdgeInsets.only(bottom: 12), child: FinFilterBar())),
        if (s.isInitialLoading)
          const SliverToBoxAdapter(child: LoadingView())
        else if (s.items.isEmpty && s.failure != null)
          SliverToBoxAdapter(child: ErrorView(failure: s.failure!, onRetry: () => ref.read(entriesProvider(args).notifier).refresh()))
        else if (s.items.isEmpty)
          SliverToBoxAdapter(child: EmptyView(icon: LucideIcons.bookOpen, title: l10n.stateEmpty, compact: true))
        else
          SliverList.separated(
            itemCount: s.items.length,
            separatorBuilder: (context, i) => const SizedBox(height: 8),
            itemBuilder: (context, i) {
              final e = s.items[i];
              return ObCard(
                onTap: e.documentId == null ? null : () => context.push('/home/finance/documents/${e.documentId}'),
                padding: const EdgeInsets.all(14),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(children: [
                      Text(e.date.substring(0, 10), style: TextStyle(fontSize: 12, color: c.ink3)),
                      const SizedBox(width: 8),
                      Text('${l10n.finDebit} ${e.debitAccount} · ${l10n.finCredit} ${e.creditAccount}', style: TextStyle(fontSize: 12, color: c.ink2)),
                      const Spacer(),
                      Text(fmt.compact(e.amount), style: TextStyle(fontSize: 14, fontWeight: FontWeight.w700, color: c.ink)),
                    ]),
                    if (e.content != null || e.counterparty != null) ...[
                      const SizedBox(height: 6),
                      Text([?e.counterparty, ?e.content].join(' · '), maxLines: 2, style: TextStyle(fontSize: 13, color: c.ink)),
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

class DocumentScreen extends ConsumerWidget {
  const DocumentScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final c = context.ob;
    final fmt = context.fmt;
    final s = ref.watch(finDocumentBlock(id));
    final d = s.data;
    return ObPage(
      title: d == null ? l10n.finDocument : '${d.sourceType.isEmpty ? d.type : d.sourceType} ${d.number}',
      subtitle: d == null ? null : Text(d.date.substring(0, 10)),
      onRefresh: () => ref.read(finDocumentBlock(id).notifier).refresh(),
      slivers: [
        SliverToBoxAdapter(
          child: blockView(context, s, onRetry: () => ref.read(finDocumentBlock(id).notifier).refresh(), (d) {
            Widget line(String a, String? b) => b == null
                ? const SizedBox.shrink()
                : Padding(
                    padding: const EdgeInsets.symmetric(vertical: 5),
                    child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                      SizedBox(width: 110, child: Text(a, style: TextStyle(fontSize: 13, color: c.ink3))),
                      Expanded(child: Text(b, style: TextStyle(fontSize: 13, color: c.ink))),
                    ]),
                  );
            return Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                ObCard(
                  tint: c.kpi,
                  child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    Row(children: [
                      Text('${fmt.decimal(d.amount, digits: 2)} ${d.currency}', style: context.text.headlineSmall),
                      const Spacer(),
                      SoftBadge(label: d.posted ? l10n.finPosted : l10n.finNotPosted, tone: d.posted ? Tone.ok : Tone.warn),
                    ]),
                    if (d.vatAmount != null) Text('${l10n.finVat}: ${fmt.decimal(d.vatAmount!, digits: 2)}', style: TextStyle(fontSize: 12, color: c.ink3)),
                  ]),
                ),
                const SizedBox(height: 12),
                ObCard(child: Column(children: [
                  line(l10n.finCounterparty, d.counterparty),
                  line(l10n.finContract, d.contract),
                  line(l10n.finWarehouse, d.warehouse),
                  line(l10n.organization1c, d.organization),
                  line(l10n.finPurpose, d.purpose ?? d.comment),
                ])),
                if (d.lines.isNotEmpty) ...[
                  const SizedBox(height: ObSpace.block),
                  SectionHeader(title: l10n.finLines),
                  const SizedBox(height: 12),
                  ObListCard(children: [
                    for (final l in d.lines)
                      Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                        child: Row(children: [
                          Expanded(child: Text(l.item ?? '—', style: TextStyle(fontSize: 13, color: c.ink))),
                          Text('${fmt.decimal(l.quantity)} ${l.unit ?? ''}', style: TextStyle(fontSize: 12, color: c.ink3)),
                          const SizedBox(width: 10),
                          Text(fmt.compact(l.amount), style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: c.ink)),
                        ]),
                      ),
                  ]),
                ],
                if (d.entries.isNotEmpty) ...[
                  const SizedBox(height: ObSpace.block),
                  SectionHeader(title: l10n.finEntries),
                  const SizedBox(height: 12),
                  ObListCard(children: [
                    for (final e in d.entries)
                      Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                        child: Row(children: [
                          Expanded(child: Text('${l10n.finDebit} ${e.debitAccount} · ${l10n.finCredit} ${e.creditAccount}', style: TextStyle(fontSize: 13, color: c.ink))),
                          Text(fmt.compact(e.amount), style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: c.ink)),
                        ]),
                      ),
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

/// Оборотно-сальдовая ведомость: счета с остатками и оборотами; тап — проводки по счёту.
class AccountsScreen extends ConsumerWidget {
  const AccountsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final c = context.ob;
    final fmt = context.fmt;
    final f = ref.watch(finFilterProvider);
    final s = ref.watch(finAccountsBlock(f));
    return ObPage(
      title: l10n.finAccounts,
      onRefresh: () => ref.read(finAccountsBlock(f).notifier).refresh(),
      slivers: [
        SliverToBoxAdapter(
          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            const FinFilterBar(),
            const SizedBox(height: 12),
            blockView(context, s, onRetry: () => ref.read(finAccountsBlock(f).notifier).refresh(), (a) {
              if (!a.meta.hasData) return metaNotice(context, a.meta)!;
              return ObListCard(children: [
                for (final r in a.rows)
                  Pressable(
                    onTap: () => context.push(Uri(path: '/home/finance/entries', queryParameters: {'account': r.account}).toString()),
                    child: Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                        Text('${r.account} ${r.name}', style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: c.ink)),
                        const SizedBox(height: 4),
                        Text(
                          '${l10n.finOpening} ${fmt.compact(r.openingDebit - r.openingCredit)} · ${l10n.finTurnover} ${fmt.compact(r.turnoverDebit)} / ${fmt.compact(r.turnoverCredit)} · ${l10n.finClosing} ${fmt.compact(r.closingDebit - r.closingCredit)}',
                          style: TextStyle(fontSize: 11, color: c.ink3),
                        ),
                      ]),
                    ),
                  ),
              ]);
            }),
          ]),
        ),
      ],
    );
  }
}
