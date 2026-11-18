import 'dart:math' as math;

import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';

import '../../../../app/theme/theme.dart';
import '../../../../shared/extensions/context_ext.dart';
import '../../data/dashboard_models.dart';

/// «Круглый» шаг сетки: 1, 2 или 5 × 10ⁿ, не меньше [raw].
double niceStep(double raw) {
  if (raw <= 0) return 1;
  final exp = math.pow(10, (math.log(raw) / math.ln10).floor()).toDouble();
  final f = raw / exp;
  final nice = f <= 1 ? 1 : (f <= 2 ? 2 : (f <= 5 ? 5 : 10));
  return nice * exp;
}

/// Продажи по дням месяца (кг): один ряд — один цвет, без легенды (название — в заголовке блока).
///
/// Оформление по правилам графиков:
/// - тонкие столбики со скруглённым верхом от нулевой линии, зазор 2 px;
/// - бледная сетка, подписи осей — серым;
/// - сегодняшний неполный день — светлее;
/// - подсказка по касанию. Таблица значений — в детализации.
class SalesDailyChart extends StatelessWidget {
  const SalesDailyChart({super.key, required this.daily, this.height = 170});

  final SalesDaily daily;
  final double height;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final fmt = context.fmt;
    final values = daily.days;
    final maxValue = values.whereType<double>().fold<double>(0, math.max);
    final step = niceStep(maxValue / 3);
    final maxY = maxValue <= 0 ? 1.0 : (maxValue / step).ceil() * step;
    final today = daily.today == null || daily.closed ? null : DateTime.tryParse(daily.today!);
    final isTodayMonth = today != null && today.year == daily.year && today.month == daily.month;
    final labelStyle = context.text.labelSmall!.copyWith(fontFeatures: tabularFigures);

    return SizedBox(
      height: height,
      child: LayoutBuilder(
        builder: (context, box) {
          const left = 34.0;
          final n = math.max(1, daily.daysInMonth);
          final barWidth = ((box.maxWidth - left) / n - 2).clamp(3.0, 14.0);
          final radius = Radius.circular(math.min(4, barWidth / 2));
          return BarChart(
            BarChartData(
              alignment: BarChartAlignment.spaceBetween,
              maxY: maxY,
              minY: 0,
              borderData: FlBorderData(show: false),
              gridData: FlGridData(
                drawVerticalLine: false,
                horizontalInterval: step,
                getDrawingHorizontalLine: (_) => FlLine(color: c.line, strokeWidth: 1),
              ),
              titlesData: FlTitlesData(
                topTitles: const AxisTitles(),
                rightTitles: const AxisTitles(),
                leftTitles: AxisTitles(
                  sideTitles: SideTitles(
                    showTitles: true,
                    reservedSize: left,
                    interval: step,
                    getTitlesWidget: (v, meta) => v == 0 || v > maxY
                        ? const SizedBox.shrink()
                        : SideTitleWidget(meta: meta, space: 4, child: Text(fmt.weightAxis(v), style: labelStyle)),
                  ),
                ),
                bottomTitles: AxisTitles(
                  sideTitles: SideTitles(
                    showTitles: true,
                    reservedSize: 22,
                    getTitlesWidget: (v, meta) {
                      final d = v.toInt();
                      final show = d == 1 || d % 5 == 0 && d <= n;
                      return show ? SideTitleWidget(meta: meta, space: 4, child: Text('$d', style: labelStyle)) : const SizedBox.shrink();
                    },
                  ),
                ),
              ),
              barTouchData: BarTouchData(
                touchTooltipData: BarTouchTooltipData(
                  getTooltipColor: (_) => c.ink,
                  tooltipBorderRadius: BorderRadius.circular(8),
                  tooltipPadding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                  fitInsideHorizontally: true,
                  fitInsideVertically: true,
                  getTooltipItem: (group, _, rod, _) {
                    final value = values[group.x - 1];
                    if (value == null) return null;
                    final date = DateTime(daily.year, daily.month, group.x);
                    return BarTooltipItem(
                      '${fmt.dayMonth(date)}\n',
                      context.text.labelSmall!.copyWith(color: c.surface),
                      children: [
                        TextSpan(
                          text: fmt.weight(value),
                          style: context.text.labelLarge!.copyWith(color: c.surface, fontWeight: FontWeight.w700),
                        ),
                      ],
                    );
                  },
                ),
              ),
              barGroups: [
                for (var i = 0; i < n; i++)
                  BarChartGroupData(
                    x: i + 1,
                    barRods: [
                      BarChartRodData(
                        toY: i < values.length ? (values[i] ?? 0) : 0,
                        width: barWidth,
                        color: isTodayMonth && i + 1 == today.day ? c.series1.withValues(alpha: 0.45) : c.series1,
                        borderRadius: BorderRadius.only(topLeft: radius, topRight: radius),
                      ),
                    ],
                  ),
              ],
            ),
            duration: const Duration(milliseconds: 250),
          );
        },
      ),
    );
  }
}

/// Выручка и расходы за 12 месяцев (сум): два ряда на одной оси. Легенда с последним значением каждого ряда стоит над графиком;
/// линии 2 px, перекрестье и подсказка по касанию.
class FinanceMonthsChart extends StatelessWidget {
  const FinanceMonthsChart({super.key, required this.months, required this.revenueLabel, required this.expensesLabel, this.height = 160});

  final List<MonthPoint> months;
  final String revenueLabel;
  final String expensesLabel;
  final double height;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final fmt = context.fmt;
    final maxValue = months.fold<double>(0, (a, m) => math.max(a, math.max(m.revenue, m.expenses)));
    final step = niceStep(maxValue / 3);
    final maxY = maxValue <= 0 ? 1.0 : (maxValue / step).ceil() * step;
    final labelStyle = context.text.labelSmall!.copyWith(fontFeatures: tabularFigures);
    final last = months.isEmpty ? null : months.last;

    LineChartBarData series(List<double> values, Color color) => LineChartBarData(
          spots: [for (var i = 0; i < values.length; i++) FlSpot(i.toDouble(), values[i])],
          color: color,
          barWidth: 2,
          isCurved: false,
          dotData: FlDotData(
            checkToShowDot: (spot, _) => spot.x == values.length - 1,
            getDotPainter: (spot, _, _, _) => FlDotCirclePainter(radius: 4, color: color, strokeWidth: 2, strokeColor: c.surface),
          ),
        );

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Wrap(
          spacing: ObLegend.gap,
          runSpacing: 6,
          children: [
            ObLegend(color: c.series1, label: revenueLabel, value: last == null ? null : fmt.money(last.revenue)),
            ObLegend(color: c.series2, label: expensesLabel, value: last == null ? null : fmt.money(last.expenses)),
          ],
        ),
        const SizedBox(height: 12),
        SizedBox(
          height: height,
          child: LineChart(
            LineChartData(
              minY: 0,
              maxY: maxY,
              minX: 0,
              maxX: math.max(1, months.length - 1).toDouble(),
              borderData: FlBorderData(show: false),
              gridData: FlGridData(
                drawVerticalLine: false,
                horizontalInterval: step,
                getDrawingHorizontalLine: (_) => FlLine(color: c.line, strokeWidth: 1),
              ),
              titlesData: FlTitlesData(
                topTitles: const AxisTitles(),
                rightTitles: const AxisTitles(),
                leftTitles: AxisTitles(
                  sideTitles: SideTitles(
                    showTitles: true,
                    reservedSize: 40,
                    interval: step,
                    getTitlesWidget: (v, meta) => v == 0 || v > maxY
                        ? const SizedBox.shrink()
                        : SideTitleWidget(meta: meta, space: 4, child: Text(fmt.compact(v), style: labelStyle)),
                  ),
                ),
                bottomTitles: AxisTitles(
                  sideTitles: SideTitles(
                    showTitles: true,
                    reservedSize: 22,
                    interval: 1,
                    getTitlesWidget: (v, meta) {
                      final i = v.toInt();
                      if (i < 0 || i >= months.length || v != i.toDouble() || (months.length - 1 - i).isOdd) {
                        return const SizedBox.shrink();
                      }
                      final d = DateTime.tryParse(months[i].month);
                      return SideTitleWidget(
                        meta: meta,
                        space: 4,
                        child: Text(d == null ? '' : fmt.monthShort(d), style: labelStyle),
                      );
                    },
                  ),
                ),
              ),
              lineTouchData: LineTouchData(
                getTouchedSpotIndicator: (bar, indexes) => [
                  for (final _ in indexes)
                    TouchedSpotIndicatorData(
                      FlLine(color: c.lineStrong, strokeWidth: 1),
                      FlDotData(
                        getDotPainter: (spot, _, b, _) =>
                            FlDotCirclePainter(radius: 4, color: b.color ?? c.series1, strokeWidth: 2, strokeColor: c.surface),
                      ),
                    ),
                ],
                touchTooltipData: LineTouchTooltipData(
                  getTooltipColor: (_) => c.ink,
                  tooltipBorderRadius: BorderRadius.circular(8),
                  fitInsideHorizontally: true,
                  fitInsideVertically: true,
                  maxContentWidth: 180,
                  getTooltipItems: (spots) => [
                    for (final s in spots)
                      LineTooltipItem(
                        '${s.barIndex == 0 ? revenueLabel : expensesLabel}: ${fmt.money(s.y)}',
                        context.text.labelSmall!.copyWith(color: c.surface, fontWeight: FontWeight.w600),
                      ),
                  ],
                ),
              ),
              lineBarsData: [
                series([for (final m in months) m.revenue], c.series1),
                series([for (final m in months) m.expenses], c.series2),
              ],
            ),
            duration: const Duration(milliseconds: 250),
          ),
        ),
      ],
    );
  }
}

/// Элемент легенды: метка цвета ряда + подпись (+ значение). Текст — цветом текста, не цветом ряда.
class ObLegend extends StatelessWidget {
  const ObLegend({super.key, required this.color, required this.label, this.value});

  static const gap = 16.0;

  final Color color;
  final String label;
  final String? value;

  @override
  Widget build(BuildContext context) => Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(width: 10, height: 10, decoration: BoxDecoration(color: color, borderRadius: BorderRadius.circular(3))),
          const SizedBox(width: 6),
          Flexible(
            child: Text.rich(
              TextSpan(
                children: [
                  TextSpan(text: label, style: context.text.labelMedium),
                  if (value != null)
                    TextSpan(
                      text: '  $value',
                      style: context.text.labelMedium!.copyWith(color: context.ob.ink, fontWeight: FontWeight.w600),
                    ),
                ],
              ),
            ),
          ),
        ],
      );
}
