import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../../../app/theme/tokens.dart';
import '../../../../core/localization/formatters.dart';
import '../../../../l10n/gen/app_localizations.dart';
import '../../../../shared/extensions/context_ext.dart';
import '../../../../shared/widgets/ob_card.dart';
import '../../../../shared/widgets/pills.dart';
import '../../../../shared/widgets/pressable.dart';
import '../../../../shared/widgets/rings.dart';
import '../../../dashboard/data/dashboard_models.dart';
import '../../data/sales_models.dart';

// Блоки продаж в языке карточек веба («Вторичка», страницы групп и регионов): уровни цвета (Good/Warning/Bad) и пороги — с сервера.

Tone levelTone(String? level) => switch (level) {
      'Good' => Tone.ok,
      'Warning' => Tone.warn,
      'Bad' => Tone.bad,
      _ => Tone.neutral,
    };

/// Статус плана как у веба: плана нет; месяц идёт — по темпу (уровень темпа сервера); закрыт — выполнен / почти / не выполнен.
(String, Tone) planStatus(AppLocalizations l10n, SalesKpi k, SalesPeriod p) {
  if (k.planKg == null || k.execution == null) return (l10n.anPlanNone, Tone.neutral);
  if (p.closed) {
    final e = k.execution!;
    if (e >= 1) return (l10n.anPlanDone, Tone.ok);
    if (e >= 0.9) return (l10n.anPlanAlmost, Tone.warn);
    return (l10n.anPlanFailed, Tone.bad);
  }
  return switch (k.pace?.level ?? k.executionLevel) {
    'Good' => (l10n.anPlanOnTrack, Tone.ok),
    'Warning' => (l10n.anPlanClose, Tone.warn),
    'Bad' => (l10n.anPlanBehind, Tone.bad),
    _ => (l10n.anPlanOnTrack, Tone.accent),
  };
}

/// «Выполнение плана»: статус, факт крупно, полоса выполнения с отметкой «должно быть», темп, нужно в день, прогноз.
class PlanCard extends StatelessWidget {
  const PlanCard({super.key, required this.kpi, required this.period});

  final SalesKpi kpi;
  final SalesPeriod period;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final l10n = context.l10n;
    final fmt = context.fmt;
    final (status, tone) = planStatus(l10n, kpi, period);
    final execution = kpi.execution;
    final expected = kpi.pace?.expected;
    Widget row(String label, String value) => Padding(
          padding: const EdgeInsets.only(top: 8),
          child: Row(
            children: [
              Expanded(child: Text(label, style: TextStyle(fontSize: 13, color: c.ink3))),
              Text(value, style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: c.ink)),
            ],
          ),
        );
    return ObCard(
      tint: c.kpi,
      radius: ObRadius.xl,
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(child: Text(l10n.anPlanCard, style: TextStyle(fontSize: 13, fontWeight: FontWeight.w500, color: c.ink3))),
              SoftBadge(label: status, tone: tone, size: 12),
            ],
          ),
          const SizedBox(height: 8),
          FittedBox(
            fit: BoxFit.scaleDown,
            alignment: Alignment.centerLeft,
            child: Text(fmt.weight(kpi.factKg), style: context.text.displayMedium),
          ),
          Text(l10n.anFactFor(fmt.month(period.year, period.month).toLowerCase()), style: TextStyle(fontSize: 12, color: c.ink3)),
          if (execution != null) ...[
            const SizedBox(height: 14),
            Row(
              children: [
                Text(fmt.percent(execution), style: TextStyle(fontSize: 20, fontWeight: FontWeight.w700, color: c.toneFg(levelTone(kpi.executionLevel)))),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    l10n.anPlanOf(fmt.weight(kpi.planFactKg ?? kpi.factKg), fmt.weight(kpi.planKg ?? 0)),
                    style: TextStyle(fontSize: 12, color: c.ink3),
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            _PlanBar(value: execution, expected: expected, forecast: kpi.forecastExecution, color: c.toneSolid(levelTone(kpi.executionLevel))),
          ],
          if (!period.closed) ...[
            if (kpi.pace?.paceKg != null) row(l10n.anPace, l10n.anPerDay(fmt.weight(kpi.pace!.paceKg!))),
            if (kpi.pace?.requiredKg != null) row(l10n.anRequired, l10n.anPerDay(fmt.weight(kpi.pace!.requiredKg!))),
            if (kpi.forecastKg != null) row(l10n.anForecast, '≈ ${fmt.weight(kpi.forecastKg!)}'),
          ],
        ],
      ),
    );
  }
}

class _PlanBar extends StatelessWidget {
  const _PlanBar({required this.value, required this.color, this.expected, this.forecast});

  final double value;
  final double? expected;
  final double? forecast;
  final Color color;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    return LayoutBuilder(
      builder: (context, box) {
        final w = box.maxWidth;
        return SizedBox(
          height: 18,
          child: Stack(
            clipBehavior: Clip.none,
            children: [
              Positioned(
                left: 0,
                right: 0,
                top: 5,
                child: Container(height: 8, decoration: BoxDecoration(color: c.track, borderRadius: BorderRadius.circular(4))),
              ),
              if (forecast != null)
                Positioned(
                  left: 0,
                  top: 5,
                  child: Container(
                    width: w * forecast!.clamp(0.0, 1.0),
                    height: 8,
                    decoration: BoxDecoration(color: color.withValues(alpha: 0.25), borderRadius: BorderRadius.circular(4)),
                  ),
                ),
              Positioned(left: 0, top: 5, child: ProgressBarFill(width: w * value.clamp(0.0, 1.0), color: color)),
              if (expected != null)
                Positioned(
                  left: (w * expected!.clamp(0.0, 1.0) - 1).clamp(0.0, w - 2),
                  top: 0,
                  child: Container(width: 2, height: 18, decoration: BoxDecoration(color: c.ink, borderRadius: BorderRadius.circular(1))),
                ),
            ],
          ),
        );
      },
    );
  }
}

class ProgressBarFill extends StatelessWidget {
  const ProgressBarFill({super.key, required this.width, required this.color});

  final double width;
  final Color color;

  @override
  Widget build(BuildContext context) => TweenAnimationBuilder<double>(
        tween: Tween(begin: 0, end: width),
        duration: MediaQuery.disableAnimationsOf(context) ? Duration.zero : const Duration(milliseconds: 500),
        curve: Curves.easeOut,
        builder: (context, v, _) => Container(width: v, height: 8, decoration: BoxDecoration(color: color, borderRadius: BorderRadius.circular(4))),
      );
}

/// Плитка показателя с целью: значение, полоса к цели, «N% цели · прогноз».
class TargetTile extends StatelessWidget {
  const TargetTile({super.key, required this.label, required this.value, this.target, this.sub, this.onTap});

  final String label;
  final String value;
  final TargetValue? target;
  final String? sub;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final t = target;
    final tone = levelTone(t?.level);
    return ObCard(
      onTap: onTap,
      padding: const EdgeInsets.all(14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12, fontWeight: FontWeight.w500, color: c.ink3)),
          const SizedBox(height: 6),
          FittedBox(
            fit: BoxFit.scaleDown,
            alignment: Alignment.centerLeft,
            child: Text(value, style: TextStyle(fontSize: 20, fontWeight: FontWeight.w700, height: 1.1, color: tone == Tone.neutral ? c.ink : c.toneFg(tone))),
          ),
          if (t?.ratio != null) ...[
            const SizedBox(height: 8),
            ProgressLine(value: t!.ratio!.clamp(0.0, 1.0), color: c.toneSolid(tone == Tone.neutral ? Tone.accent : tone), height: 5),
          ],
          if (sub != null) ...[
            const SizedBox(height: 6),
            Text(sub!, maxLines: 2, style: TextStyle(fontSize: 11, height: 1.3, color: c.ink3)),
          ],
        ],
      ),
    );
  }
}

/// Шесть плиток KPI в две колонки (как KpiRow веба): выручка с АКБ, конверсия, выручка на ТТ, АКБ на агента, визиты без заказа.
class KpiGrid extends StatelessWidget {
  const KpiGrid({super.key, required this.kpi, this.onAgents});

  final SalesKpi kpi;
  final VoidCallback? onAgents;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final fmt = context.fmt;
    final c = context.ob;
    String targetSub(TargetValue? t, String Function(double) f) {
      if (t == null) return '';
      final parts = [l10n.anTarget(f(t.target)), if (t.ratio != null) l10n.anOfTarget(fmt.percent(t.ratio!))];
      return parts.join(' · ');
    }

    final visitsShare = kpi.visitsDone > 0 ? kpi.visitsWithoutOrder / kpi.visitsDone : null;
    final tiles = [
      TargetTile(label: l10n.anRevenue, value: fmt.compact(kpi.revenue), sub: '${l10n.kpiClients}: ${fmt.count(kpi.akb)}'),
      TargetTile(
        label: l10n.anConversion,
        value: kpi.conversion?.value == null ? '—' : fmt.percent(kpi.conversion!.value!),
        target: kpi.conversion,
        sub: targetSub(kpi.conversion, fmt.percent),
      ),
      TargetTile(
        label: l10n.anRevenuePerOutlet,
        value: kpi.revenuePerOutlet?.value == null ? '—' : fmt.compact(kpi.revenuePerOutlet!.value!),
        target: kpi.revenuePerOutlet,
        sub: targetSub(kpi.revenuePerOutlet, fmt.compact),
      ),
      TargetTile(
        label: l10n.anAkbPerAgent,
        value: kpi.akbPerAgent?.value == null ? '—' : fmt.decimal(kpi.akbPerAgent!.value!),
        target: kpi.akbPerAgent,
        sub: targetSub(kpi.akbPerAgent, fmt.decimal),
        onTap: onAgents,
      ),
    ];
    return Column(
      children: [
        for (var i = 0; i < tiles.length; i += 2) ...[
          if (i > 0) const SizedBox(height: 10),
          IntrinsicHeight(
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [Expanded(child: tiles[i]), const SizedBox(width: 10), Expanded(child: tiles[i + 1])],
            ),
          ),
        ],
        const SizedBox(height: 10),
        ObCard(
          padding: const EdgeInsets.all(14),
          color: visitsShare != null && visitsShare > 0.4 ? Color.alphaBlend(c.warnSoft, c.surface) : null,
          child: Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(l10n.anVisitsNoOrder, style: TextStyle(fontSize: 12, fontWeight: FontWeight.w500, color: c.ink3)),
                    const SizedBox(height: 4),
                    Text(l10n.anVisitsNoOrderOf(fmt.count(kpi.visitsDone)), style: TextStyle(fontSize: 11, color: c.ink3)),
                  ],
                ),
              ),
              Text(fmt.count(kpi.visitsWithoutOrder), style: TextStyle(fontSize: 20, fontWeight: FontWeight.w700, color: c.ink)),
              if (visitsShare != null) ...[
                const SizedBox(width: 8),
                SoftBadge(label: fmt.percent(visitsShare), tone: visitsShare > 0.4 ? Tone.warn : Tone.neutral),
              ],
            ],
          ),
        ),
      ],
    );
  }
}

/// Направление или регион: название, подпись, полоса выполнения, план / факт / прогноз / страйк, замечания.
class UnitCard extends StatelessWidget {
  const UnitCard({super.key, required this.row, required this.onTap});

  final UnitRow row;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final l10n = context.l10n;
    final fmt = context.fmt;
    final tone = levelTone(row.executionLevel ?? row.forecastExecutionLevel);
    final subtitle = row.subtitle ?? (row.agents > 0 ? '${row.agents} ТП' : null);
    return ObCard(
      onTap: onTap,
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(row.name, style: TextStyle(fontSize: 15, fontWeight: FontWeight.w600, color: c.ink)),
                    if (subtitle != null) Text(subtitle, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12, color: c.ink3)),
                  ],
                ),
              ),
              if (row.execution != null)
                Text(fmt.percent(row.execution!), style: TextStyle(fontSize: 17, fontWeight: FontWeight.w700, color: c.toneFg(tone == Tone.neutral ? Tone.accent : tone)))
              else if (onTap != null)
                Icon(LucideIcons.chevronRight, size: 16, color: c.subtle),
            ],
          ),
          const SizedBox(height: 10),
          ProgressLine(
            value: (row.execution ?? row.forecastExecution ?? 0).clamp(0.0, 1.0),
            color: c.toneSolid(tone == Tone.neutral ? Tone.accent : tone),
            height: 5,
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              _Mini(label: l10n.anFact, value: fmt.weight(row.factKg)),
              _Mini(label: row.planKg == null ? l10n.anForecastShort : l10n.anPlanKg, value: fmt.weight(row.planKg ?? row.forecastKg ?? 0)),
              _Mini(label: l10n.anRevenue, value: fmt.compact(row.revenue)),
              _Mini(label: l10n.anStrike, value: row.strike == null ? '—' : fmt.percent(row.strike!)),
            ],
          ),
          if (row.flags.critical + row.flags.risk > 0) ...[
            const SizedBox(height: 10),
            Wrap(spacing: 6, children: [
              if (row.flags.critical > 0) SoftBadge(label: '${l10n.anCritical} ${row.flags.critical}', tone: Tone.bad),
              if (row.flags.risk > 0) SoftBadge(label: '${l10n.anRisk} ${row.flags.risk}', tone: Tone.warn),
            ]),
          ],
        ],
      ),
    );
  }
}

class _Mini extends StatelessWidget {
  const _Mini({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    return Expanded(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 10.5, color: c.ink3)),
          const SizedBox(height: 2),
          FittedBox(
            fit: BoxFit.scaleDown,
            alignment: Alignment.centerLeft,
            child: Text(value, style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: c.ink)),
          ),
        ],
      ),
    );
  }
}

Tone flagTone(String severity) => switch (severity) {
      'Critical' => Tone.bad,
      'Risk' => Tone.warn,
      _ => Tone.neutral,
    };

/// Отметки замечаний ТП (без «Мало данных»); нет замечаний — зелёная «Замечаний нет».
class FlagPills extends StatelessWidget {
  const FlagPills({super.key, required this.flags, this.showNone = true});

  final List<AgentFlag> flags;
  final bool showNone;

  @override
  Widget build(BuildContext context) {
    final shown = flags.where((f) => f.severity != 'Info').toList();
    if (shown.isEmpty) return showNone ? SoftBadge(label: context.l10n.anNoRemarks, tone: Tone.ok) : const SizedBox.shrink();
    return Wrap(spacing: 6, runSpacing: 6, children: [for (final f in shown) SoftBadge(label: f.label, tone: flagTone(f.severity))]);
  }
}

/// Столбики по дням (продажи месяца): тап — значение дня; отрицательные дни (возвраты) — у нуля.
class DailyBars extends StatefulWidget {
  const DailyBars({super.key, required this.values, required this.format, this.height = 110});

  final List<double?> values;
  final String Function(double) format;
  final double height;

  @override
  State<DailyBars> createState() => _DailyBarsState();
}

class _DailyBarsState extends State<DailyBars> {
  int? _tip;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final values = widget.values;
    final known = values.whereType<double>().toList();
    if (known.isEmpty || known.every((v) => v <= 0)) return const SizedBox.shrink();
    final max = known.fold<double>(0, math.max);
    final last = values.lastIndexWhere((v) => v != null);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SizedBox(
          height: 20,
          child: _tip == null || values[_tip!] == null
              ? null
              : Text('${_tip! + 1}: ${widget.format(values[_tip!]!)}', style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: c.ink)),
        ),
        SizedBox(
          height: widget.height,
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              for (var i = 0; i < values.length; i++)
                Expanded(
                  child: GestureDetector(
                    behavior: HitTestBehavior.opaque,
                    onTap: () => setState(() => _tip = _tip == i ? null : i),
                    child: Align(
                      alignment: Alignment.bottomCenter,
                      child: Container(
                        margin: const EdgeInsets.symmetric(horizontal: 1),
                        height: values[i] == null || max <= 0 ? 0 : math.max(2, widget.height * (values[i]!.clamp(0, max) / max)),
                        decoration: BoxDecoration(
                          borderRadius: const BorderRadius.vertical(top: Radius.circular(3)),
                          color: i == last ? c.accent : c.accent.withValues(alpha: _tip == i ? 0.8 : 0.45),
                        ),
                      ),
                    ),
                  ),
                ),
            ],
          ),
        ),
        const SizedBox(height: 6),
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Text('1', style: TextStyle(fontSize: 10, color: c.ink3)),
            Text('${values.length}', style: TextStyle(fontSize: 10, color: c.ink3)),
          ],
        ),
      ],
    );
  }
}

/// «К прошлому месяцу за те же дни»: строки с кг сейчас и изменением (по убыванию падения).
class SameDaysList extends StatelessWidget {
  const SameDaysList({super.key, required this.rows, this.onTap, this.limit = 8});

  final List<SameDaysRow> rows;
  final void Function(SameDaysRow)? onTap;
  final int limit;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final fmt = context.fmt;
    final sorted = [...rows]..sort((a, b) => (a.kgDelta ?? 0).compareTo(b.kgDelta ?? 0));
    return ObListCard(
      children: [
        for (final r in sorted.take(limit))
          Pressable(
            onTap: onTap == null ? null : () => onTap!(r),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 11),
              child: Row(
                children: [
                  Expanded(child: Text(r.name, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 14, color: c.ink))),
                  Text(fmt.weight(r.kgNow), style: TextStyle(fontSize: 13, color: c.ink2, fontFeatures: const [FontFeature.tabularFigures()])),
                  const SizedBox(width: 10),
                  SizedBox(
                    width: 64,
                    child: Text(
                      r.kgDelta == null ? '—' : fmt.signedPercent(r.kgDelta!),
                      textAlign: TextAlign.right,
                      style: TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: (r.kgDelta ?? 0) >= 0 ? c.ok : c.bad),
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

/// Категории: факт, доля веса, к прошлому месяцу, «молчат» и «пропало».
class CategoryList extends StatelessWidget {
  const CategoryList({super.key, required this.cards});

  final List<CategoryCard> cards;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final l10n = context.l10n;
    final fmt = context.fmt;
    return ObListCard(
      children: [
        for (final k in cards)
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(child: Text(k.name, style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: c.ink))),
                    Text(fmt.weight(k.factKg), style: TextStyle(fontSize: 14, fontWeight: FontWeight.w700, color: c.ink)),
                    if (k.vsPrevMonth != null) ...[
                      const SizedBox(width: 8),
                      Text(fmt.signedPercent(k.vsPrevMonth!),
                          style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: k.vsPrevMonth! >= 0 ? c.ok : c.bad)),
                    ],
                  ],
                ),
                const SizedBox(height: 6),
                ProgressLine(value: (k.weightShare ?? 0).clamp(0.0, 1.0), color: c.accent, height: 4),
                const SizedBox(height: 6),
                Wrap(
                  spacing: 8,
                  runSpacing: 4,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    Text(l10n.anSkuSold(k.skuSold, k.skuTotal), style: TextStyle(fontSize: 11, color: c.ink3)),
                    if (k.weightShare != null) Text(l10n.anWeightShare(fmt.percent(k.weightShare!)), style: TextStyle(fontSize: 11, color: c.ink3)),
                    if (k.silent > 0) SoftBadge(label: l10n.anSilent(k.silent), tone: Tone.neutral),
                    if (k.lost > 0) SoftBadge(label: l10n.anLost(k.lost), tone: Tone.bad),
                  ],
                ),
              ],
            ),
          ),
      ],
    );
  }
}

/// Плитки «метка — значение» в строку (карточки ТП и точки).
class MetricRow extends StatelessWidget {
  const MetricRow({super.key, required this.items});

  final List<(String, String, String?)> items;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        for (final (label, value, sub) in items)
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(label, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 11, color: c.ink3)),
                const SizedBox(height: 3),
                FittedBox(
                  fit: BoxFit.scaleDown,
                  alignment: Alignment.centerLeft,
                  child: Text(value, style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700, color: c.ink)),
                ),
                if (sub != null) Text(sub, maxLines: 2, style: TextStyle(fontSize: 10.5, color: c.ink3)),
              ],
            ),
          ),
      ],
    );
  }
}

/// Формат «кг/т» для подписей графиков.
String Function(double) weightFormat(Fmt fmt) => (v) => fmt.weight(v);
