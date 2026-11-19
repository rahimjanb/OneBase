import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../../../core/core_providers.dart';
import '../../../../shared/extensions/context_ext.dart';
import '../../../../shared/widgets/ob_card.dart';
import '../../../../shared/widgets/pills.dart';
import '../../../../shared/widgets/pressable.dart';
import '../../../../shared/widgets/section_header.dart';
import '../../data/dashboard_models.dart';
import '../dashboard_providers.dart';
import 'home_head.dart';

// Блоки главной сверх мобильного «Обзора» веба — из 12 блоков Dashboard мастер-промпта: продажи месяца (продано, клиенты/АКБ,
// выручка), финансы 1С (выручка, расходы, прибыль) и последние события. Тот же визуальный язык, что у карточек веба.

/// Плитка показателя: подпись, крупное число, подпись снизу.
class _Metric extends StatelessWidget {
  const _Metric({required this.label, required this.value, this.sub, this.subColor, this.first = false});

  final String label;
  final String value;
  final String? sub;
  final Color? subColor;
  final bool first;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    return Container(
      padding: first ? const EdgeInsets.only(right: 8) : const EdgeInsets.only(left: 14),
      decoration: first ? null : BoxDecoration(border: Border(left: BorderSide(color: c.divider))),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 11, fontWeight: FontWeight.w500, color: c.ink3)),
          const SizedBox(height: 4),
          FittedBox(
            fit: BoxFit.scaleDown,
            alignment: Alignment.centerLeft,
            child: Text(value, style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700, height: 1.1, letterSpacing: -0.3, color: c.ink)),
          ),
          if (sub != null) ...[
            const SizedBox(height: 4),
            Text(sub!, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: subColor ?? c.ink3)),
          ],
        ],
      ),
    );
  }
}

/// «Продажи месяца»: продано (кг/т), клиенты (АКБ), выручка; тап — аналитика продаж.
class SalesMonthCard extends ConsumerWidget {
  const SalesMonthCard({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final data = ref.watch(homeSalesBlock).data;
    if (data == null) return const SizedBox.shrink();
    final l10n = context.l10n;
    final fmt = context.fmt;
    final c = context.ob;
    final k = data.overview.kpi;
    final p = data.overview.period;
    final vs = data.month?.vsPrevMonth;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(title: l10n.homeMoreSales, onMore: () => context.go('/analytics/sales'), moreLabel: l10n.dynMore),
        const SizedBox(height: 12),
        ObCard(
          onTap: () => context.go('/analytics/sales'),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('${fmt.monthYear(p.year, p.month)} · ${l10n.kpiThrough(fmt.dayMonth(DateTime.parse(p.dataThrough)))}',
                  style: TextStyle(fontSize: 12, color: c.ink3)),
              const SizedBox(height: 14),
              IntrinsicHeight(
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Expanded(
                      child: _Metric(
                        first: true,
                        label: l10n.kpiSold,
                        value: fmt.weight(k.factKg),
                        sub: vs == null ? null : '${fmt.signedPercent(vs)} ${l10n.vsSameDays}',
                        subColor: vs == null ? null : (vs >= 0 ? c.ok : c.bad),
                      ),
                    ),
                    Expanded(child: _Metric(label: l10n.kpiClients, value: fmt.count(k.akb), sub: l10n.homeAgentsN(k.activeAgents))),
                    Expanded(child: _Metric(label: l10n.kpiRevenue, value: fmt.compact(k.revenue), sub: l10n.unitSum)),
                  ],
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

/// «Финансы 1С»: чистая выручка, расходы, чистая прибыль за месяц; данных 1С нет — пояснение сервера, а не нули.
class FinanceCard extends ConsumerWidget {
  const FinanceCard({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(homeFinanceBlock);
    final f = state.data;
    if (f == null) return const SizedBox.shrink();
    final l10n = context.l10n;
    final fmt = context.fmt;
    final c = context.ob;
    final revenue = f.line('net_revenue');
    final profit = f.line('net_profit');
    final expenses = f.lastMonth?.expenses;
    String? delta(PnlValue? v) {
      if (v?.previous == null || v!.previous == 0) return null;
      return fmt.signedPercent(v.value / v.previous! - 1);
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(title: l10n.homeMoreFinance, onMore: () => context.go('/home/finance'), moreLabel: l10n.dynMore),
        const SizedBox(height: 12),
        ObCard(
          onTap: () => context.go('/home/finance'),
          child: !f.meta.hasData
              ? Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const IconTile(icon: LucideIcons.landmark, size: 38),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(l10n.financeNoData, style: TextStyle(fontSize: 15, fontWeight: FontWeight.w600, color: c.ink)),
                          if (f.meta.note != null) ...[
                            const SizedBox(height: 4),
                            Text(f.meta.note!, style: TextStyle(fontSize: 13, height: 1.4, color: c.ink3)),
                          ],
                        ],
                      ),
                    ),
                  ],
                )
              : IntrinsicHeight(
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Expanded(
                        child: _Metric(
                          first: true,
                          label: l10n.financeNetRevenue,
                          value: fmt.compact(revenue?.value ?? 0),
                          sub: delta(revenue),
                        ),
                      ),
                      Expanded(child: _Metric(label: l10n.financeExpenses, value: expenses == null ? '—' : fmt.compact(expenses))),
                      Expanded(
                        child: _Metric(
                          label: l10n.financeNetProfit,
                          value: fmt.compact(profit?.value ?? 0),
                          sub: delta(profit),
                          subColor: (profit?.value ?? 0) >= 0 ? c.ok : c.bad,
                        ),
                      ),
                    ],
                  ),
                ),
        ),
      ],
    );
  }
}

/// «Последние события»: пять свежих из ленты уведомлений; тап — связанный экран. На телефоне не сохраняются (имена людей).
class EventsCard extends ConsumerWidget {
  const EventsCard({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final page = ref.watch(eventsBlock).data;
    if (page == null || page.items.isEmpty) return const SizedBox.shrink();
    final l10n = context.l10n;
    final c = context.ob;
    final now = ref.watch(clockProvider)();
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(
          title: l10n.eventsTitle,
          trailing: page.unread > 0 ? DotLabel(color: c.accent, label: l10n.eventsUnread(page.unread)) : null,
          onMore: () => context.go('/home/notifications'),
          moreLabel: l10n.actionAll,
        ),
        const SizedBox(height: 12),
        ObListCard(
          children: [
            for (final e in page.items.take(5))
              Pressable(
                onTap: () => openWebLink(context, e.href ?? '/notifications'),
                child: Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 13),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      IconTile(icon: eventIcon(e.category), size: 34, radius: 10, tone: e.severity == 'critical' ? Tone.bad : Tone.neutral),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(e.title,
                                maxLines: 2,
                                overflow: TextOverflow.ellipsis,
                                style: TextStyle(fontSize: 14, height: 1.3, fontWeight: e.read ? FontWeight.w400 : FontWeight.w600, color: c.ink)),
                            const SizedBox(height: 3),
                            Text(context.fmt.ago(DateTime.parse(e.createdAt), now), style: TextStyle(fontSize: 11, color: c.ink3)),
                          ],
                        ),
                      ),
                      if (!e.read) ...[
                        const SizedBox(width: 8),
                        Padding(
                          padding: const EdgeInsets.only(top: 6),
                          child: Container(width: 8, height: 8, decoration: BoxDecoration(color: c.accent, shape: BoxShape.circle)),
                        ),
                      ],
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

IconData eventIcon(String? category) => switch (category) {
      'tasks' => LucideIcons.clipboardList,
      'hr' => LucideIcons.users,
      'ai' => LucideIcons.sparkles,
      'procurement' || 'requests' => LucideIcons.package,
      'sales' => LucideIcons.chartNoAxesColumn,
      _ => LucideIcons.bell,
    };
