import 'package:flutter/material.dart';

import '../../app/theme/decor.dart';
import '../../app/theme/tokens.dart';
import '../extensions/context_ext.dart';
import 'pressable.dart';

/// Карточка OneBase по вебу: поверхность, рамка 1 px, скругление 20, без теней. [tint] — акцентный градиент (главный показатель,
/// «Фокус дня», AI-отчёт). С [onTap] — нажимается целиком (сжатие, как в вебе).
class ObCard extends StatelessWidget {
  const ObCard({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.all(18),
    this.onTap,
    this.color,
    this.borderColor,
    this.tint,
    this.radius = ObRadius.lg,
    this.semanticLabel,
    this.clip = false,
  });

  final Widget child;
  final EdgeInsetsGeometry padding;
  final VoidCallback? onTap;
  final Color? color;
  final Color? borderColor;
  final CardTint? tint;
  final double radius;
  final String? semanticLabel;

  /// Обрезать содержимое по скруглению (списки со строками во всю ширину).
  final bool clip;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final decoration = tint != null ? tintDecoration(tint!, radius: radius) : cardDecoration(c, radius: radius, color: color, border: borderColor);
    Widget body = DecoratedBox(
      decoration: decoration,
      child: Padding(padding: padding, child: child),
    );
    if (clip) body = ClipRRect(borderRadius: BorderRadius.circular(radius), child: body);
    if (onTap != null) return Pressable(onTap: onTap, scale: 0.98, semanticLabel: semanticLabel, child: body);
    return Semantics(container: true, label: semanticLabel, child: body);
  }
}

/// Карточка-список: строки во всю ширину с разделителями.
class ObListCard extends StatelessWidget {
  const ObListCard({super.key, required this.children, this.dividerColor});

  final List<Widget> children;
  final Color? dividerColor;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    return ObCard(
      padding: EdgeInsets.zero,
      clip: true,
      child: Column(
        children: [
          for (var i = 0; i < children.length; i++) ...[
            if (i > 0) Divider(height: 1, thickness: 1, color: dividerColor ?? c.line),
            children[i],
          ],
        ],
      ),
    );
  }
}
