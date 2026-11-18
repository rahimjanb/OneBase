import 'package:flutter/material.dart';

import '../../app/theme/tokens.dart';
import '../extensions/context_ext.dart';

/// Карточка OneBase: ровная поверхность, тонкая линия, скругление 18. С [onTap] — нажимается целиком.
class ObCard extends StatelessWidget {
  const ObCard({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.all(ObSpace.lg),
    this.onTap,
    this.color,
    this.borderColor,
    this.semanticLabel,
  });

  final Widget child;
  final EdgeInsetsGeometry padding;
  final VoidCallback? onTap;
  final Color? color;
  final Color? borderColor;
  final String? semanticLabel;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final radius = BorderRadius.circular(ObRadius.lg);
    Widget body = Padding(padding: padding, child: child);
    if (onTap != null) {
      body = InkWell(onTap: onTap, borderRadius: radius, child: body);
    }
    return Semantics(
      container: true,
      button: onTap != null,
      label: semanticLabel,
      child: Material(
        color: color ?? c.surface,
        shape: RoundedRectangleBorder(borderRadius: radius, side: BorderSide(color: borderColor ?? c.line)),
        clipBehavior: Clip.antiAlias,
        child: body,
      ),
    );
  }
}
