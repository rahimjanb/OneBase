import 'package:flutter/material.dart';

import '../../app/theme/theme.dart';
import '../../app/theme/tokens.dart';
import '../extensions/context_ext.dart';
import 'ob_card.dart';

/// Плитка показателя: подпись с иконкой, крупное число (цифры одинаковой ширины), строка пояснения или изменения.
/// Нажатие — детализация.
class KpiTile extends StatelessWidget {
  const KpiTile({
    super.key,
    required this.icon,
    required this.label,
    required this.value,
    this.caption,
    this.footer,
    this.onTap,
    this.accent,
  });

  final IconData icon;
  final String label;
  final String value;

  /// Пояснение под числом (серым).
  final String? caption;

  /// Вместо пояснения — свой виджет (изменение, уровень).
  final Widget? footer;
  final VoidCallback? onTap;

  /// Цвет иконки (по умолчанию — акцент).
  final Color? accent;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final color = accent ?? c.accentStrong;
    return ObCard(
      onTap: onTap,
      semanticLabel: '$label: $value${caption == null ? '' : ', $caption'}',
      padding: const EdgeInsets.fromLTRB(14, 14, 14, 12),
      child: ExcludeSemantics(
        child: ConstrainedBox(
          constraints: const BoxConstraints(minHeight: 96),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Container(
                    width: 26,
                    height: 26,
                    decoration: BoxDecoration(color: color.withValues(alpha: 0.12), borderRadius: BorderRadius.circular(8)),
                    child: Icon(icon, size: 16, color: color),
                  ),
                  const SizedBox(width: ObSpace.sm),
                  Expanded(
                    child: Text(label, style: context.text.labelMedium, maxLines: 1, overflow: TextOverflow.ellipsis),
                  ),
                  if (onTap != null) Icon(Icons.chevron_right_rounded, size: 18, color: c.ink3),
                ],
              ),
              const SizedBox(height: 10),
              FittedBox(
                fit: BoxFit.scaleDown,
                alignment: Alignment.centerLeft,
                child: Text(
                  value,
                  maxLines: 1,
                  style: context.text.headlineSmall!.copyWith(fontFeatures: tabularFigures),
                ),
              ),
              const SizedBox(height: 4),
              if (footer != null)
                footer!
              else if (caption != null)
                Text(caption!, style: context.text.bodySmall, maxLines: 2, overflow: TextOverflow.ellipsis),
            ],
          ),
        ),
      ),
    );
  }
}

/// Изменение со стрелкой: рост — зелёный, падение — красный (или наоборот, если [goodWhenDown] — расходы, долги).
/// Цвет никогда не единственный признак: есть стрелка и знак.
class DeltaBadge extends StatelessWidget {
  const DeltaBadge({super.key, required this.ratio, required this.label, this.goodWhenDown = false});

  /// Изменение долей: +0,04 = +4 %.
  final double ratio;

  /// Подпись после значения («к тем же дням прошлого месяца»).
  final String label;
  final bool goodWhenDown;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final up = ratio > 0;
    final flat = ratio.abs() < 0.0005;
    final good = flat ? null : (up != goodWhenDown);
    final color = good == null ? c.ink2 : (good ? c.ok : c.bad);
    final icon = flat ? Icons.trending_flat_rounded : (up ? Icons.trending_up_rounded : Icons.trending_down_rounded);
    return Text.rich(
      TextSpan(
        children: [
          WidgetSpan(alignment: PlaceholderAlignment.middle, child: Icon(icon, size: 15, color: color)),
          TextSpan(
            text: ' ${context.fmt.signedPercent(ratio)} ',
            style: context.text.labelMedium!.copyWith(color: color, fontWeight: FontWeight.w600),
          ),
          TextSpan(text: label, style: context.text.bodySmall),
        ],
      ),
      maxLines: 2,
      overflow: TextOverflow.ellipsis,
    );
  }
}

/// Пометка статуса: иконка + текст (цвет — не единственный признак).
class StatusChip extends StatelessWidget {
  const StatusChip({super.key, required this.label, required this.color, required this.background, this.icon});

  final String label;
  final Color color;
  final Color background;
  final IconData? icon;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
        decoration: BoxDecoration(color: background, borderRadius: BorderRadius.circular(ObRadius.sm)),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (icon != null) ...[Icon(icon, size: 14, color: color), const SizedBox(width: 4)],
            Text(label, style: context.text.labelMedium!.copyWith(color: color, fontWeight: FontWeight.w600)),
          ],
        ),
      );
}
