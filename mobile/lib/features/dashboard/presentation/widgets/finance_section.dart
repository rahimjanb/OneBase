import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../app/theme/theme.dart';
import '../../../../app/theme/tokens.dart';
import '../../../../shared/extensions/context_ext.dart';
import '../../../../shared/widgets/detail_sheet.dart';
import '../../../../shared/widgets/kpi_tile.dart';
import '../../../../shared/widgets/states.dart';
import '../../data/dashboard_models.dart';
import '../dashboard_providers.dart';
import 'block_section.dart';
import 'charts.dart';

/// Финансы по факту 1С: чистая выручка, расходы, чистая прибыль (с изменением к прошлому периоду — его считает сервер) и динамика
/// за 12 месяцев. Если данных 1С нет — пояснение сервера, а не нули.
class FinanceSection extends ConsumerWidget {
  const FinanceSection({super.key, required this.args});

  final FinanceArgs args;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(financeBlock(args));
    final l10n = context.l10n;
    final hasData = state.data?.meta.hasData ?? false;
    return BlockSection<FinanceOverview>(
      title: l10n.financeTitle,
      state: state,
      skeletonHeight: 300,
      moreLabel: l10n.actionDetails,
      onMore: hasData ? () => showFinanceDetail(context, state.data!) : null,
      onRetry: () => ref.read(financeBlock(args).notifier).refresh(),
      builder: (context, f) => f.meta.hasData
          ? _FinanceBody(f: f)
          : EmptyView(
              compact: true,
              icon: Icons.account_balance_outlined,
              title: l10n.financeNoData,
              message: f.meta.note,
            ),
    );
  }
}

class _FinanceBody extends StatelessWidget {
  const _FinanceBody({required this.f});

  final FinanceOverview f;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final fmt = context.fmt;
    final revenue = f.line('net_revenue');
    final profit = f.line('net_profit');
    final expenses = f.lastMonth?.expenses;
    final from = DateTime.tryParse(f.meta.from);
    final to = DateTime.tryParse(f.meta.dataThrough ?? f.meta.to);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (from != null && to != null)
          Text(
            [
              l10n.periodRange(fmt.dayMonth(from), fmt.dayMonth(to)),
              if (f.meta.organizationName != null) f.meta.organizationName!,
            ].join(' · '),
            style: context.text.bodySmall,
          ),
        const SizedBox(height: ObSpace.md),
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(child: _Metric(label: l10n.financeNetRevenue, value: revenue?.value, previous: revenue?.previous)),
            Expanded(child: _Metric(label: l10n.financeExpenses, value: expenses, goodWhenDown: true)),
            Expanded(child: _Metric(label: l10n.financeNetProfit, value: profit?.value, previous: profit?.previous, signed: true)),
          ],
        ),
        if (f.months.length > 1) ...[
          const SizedBox(height: ObSpace.lg),
          Divider(color: context.ob.line, height: 1),
          const SizedBox(height: ObSpace.lg),
          Text(l10n.financeTrendTitle, style: context.text.titleSmall),
          const SizedBox(height: ObSpace.sm),
          FinanceMonthsChart(months: f.months, revenueLabel: l10n.financeNetRevenue, expensesLabel: l10n.financeExpenses),
        ],
      ],
    );
  }
}

/// Показатель периода: значение и изменение к прошлому периоду (если сервер его дал).
class _Metric extends StatelessWidget {
  const _Metric({required this.label, required this.value, this.previous, this.goodWhenDown = false, this.signed = false});

  final String label;
  final double? value;
  final double? previous;
  final bool goodWhenDown;
  final bool signed;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final v = value;
    final p = previous;
    final change = v != null && p != null && p.abs() > 0 ? (v - p) / p.abs() : null;
    return Padding(
      padding: const EdgeInsets.only(right: ObSpace.sm),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: context.text.labelMedium, maxLines: 2),
          const SizedBox(height: 4),
          FittedBox(
            fit: BoxFit.scaleDown,
            alignment: Alignment.centerLeft,
            child: Text(
              v == null ? '—' : context.fmt.money(v, withUnit: false),
              style: context.text.titleLarge!.copyWith(
                fontFeatures: tabularFigures,
                color: signed && v != null && v < 0 ? c.bad : null,
              ),
            ),
          ),
          if (change != null) DeltaBadge(ratio: change, label: '', goodWhenDown: goodWhenDown),
        ],
      ),
    );
  }
}

/// Детализация финансов: отчёт о прибылях и убытках (строки 1С с прошлым периодом), деньги и долги, таблица по месяцам.
void showFinanceDetail(BuildContext context, FinanceOverview f) {
  final l10n = context.l10n;
  final fmt = context.fmt;
  final c = context.ob;
  final from = DateTime.tryParse(f.meta.from);
  final to = DateTime.tryParse(f.meta.dataThrough ?? f.meta.to);
  showDetailSheet(
    context,
    title: l10n.financeTitle,
    subtitle: from == null || to == null ? null : l10n.periodRange(fmt.dayMonth(from), fmt.dayMonth(to)),
    children: (context) => [
      DetailGroup(
        title: l10n.financePnl,
        children: [
          for (final line in f.pnl)
            DetailRow(
              label: line.title,
              value: fmt.money(line.value),
              valueColor: line.kind == 'total' && line.value < 0 ? c.bad : null,
              hint: line.previous == null ? null : l10n.financePrevious(fmt.money(line.previous!)),
            ),
        ],
      ),
      DetailGroup(
        title: l10n.financeBalances,
        children: [
          DetailRow(label: l10n.financeCash, value: fmt.money(f.cashTotal)),
          DetailRow(label: l10n.financeReceivables, value: fmt.money(f.receivables)),
          DetailRow(label: l10n.financePayables, value: fmt.money(f.payables)),
        ],
      ),
      DetailGroup(
        title: l10n.financeByMonth,
        children: [
          for (final m in f.months.reversed)
            DetailRow(
              label: () {
                final d = DateTime.tryParse(m.month);
                return d == null ? m.month : fmt.monthYear(d.year, d.month);
              }(),
              value: fmt.money(m.revenue),
              hint: '${l10n.financeExpenses}: ${fmt.money(m.expenses)} · ${l10n.financeNetProfit}: ${fmt.money(m.netProfit)}',
            ),
        ],
      ),
    ],
  );
}
