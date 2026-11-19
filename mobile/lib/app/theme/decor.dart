import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../../shared/extensions/context_ext.dart';
import 'tokens.dart';

/// Фон экрана как у мобильного веба: тёмный #060A14 со свечением из правого верхнего угла (`radial-gradient(130% 28% at 100% 0%)`),
/// в светлой теме — ровный #F5F6F8.
class ObBackground extends StatelessWidget {
  const ObBackground({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    return DecoratedBox(
      decoration: BoxDecoration(color: c.page),
      child: c.glow.a == 0
          ? child
          : Stack(
              children: [
                Positioned.fill(
                  child: IgnorePointer(
                    child: LayoutBuilder(
                      builder: (context, box) => DecoratedBox(
                        decoration: BoxDecoration(
                          gradient: RadialGradient(
                            center: Alignment.topRight,
                            radius: 1.3,
                            colors: [c.glow, c.glow.withValues(alpha: 0)],
                            transform: _Ellipse(box.maxWidth, box.maxHeight),
                          ),
                        ),
                      ),
                    ),
                  ),
                ),
                child,
              ],
            ),
    );
  }
}

/// Круглый градиент → эллипс: по горизонтали 130% ширины, по вертикали 28% высоты экрана, центр — правый верхний угол.
class _Ellipse extends GradientTransform {
  const _Ellipse(this.width, this.height);

  final double width;
  final double height;

  @override
  Matrix4 transform(Rect bounds, {TextDirection? textDirection}) {
    final shortest = math.min(bounds.width, bounds.height);
    final rx = 1.3 * shortest;
    final sx = 1.3 * bounds.width / rx;
    final sy = 0.28 * bounds.height / rx;
    return Matrix4.identity()
      ..translateByDouble(bounds.right, bounds.top, 0, 1)
      ..scaleByDouble(sx, sy, 1, 1)
      ..translateByDouble(-bounds.right, -bounds.top, 0, 1);
  }
}

/// Карточка с акцентным градиентом (главный показатель, «Фокус дня», AI-отчёт): градиент поверх основы и цветная рамка.
BoxDecoration tintDecoration(CardTint tint, {double radius = ObRadius.lg}) {
  final rad = tint.angle * math.pi / 180;
  // Направление CSS: 0deg — вверх, 90deg — вправо.
  final dx = math.sin(rad);
  final dy = -math.cos(rad);
  return BoxDecoration(
    color: tint.base,
    borderRadius: BorderRadius.circular(radius),
    border: Border.all(color: tint.border),
    gradient: LinearGradient(
      begin: Alignment(-dx, -dy),
      end: Alignment(dx, dy),
      colors: [
        Color.alphaBlend(tint.from, tint.base),
        if (tint.via != null) Color.alphaBlend(tint.via!, tint.base),
        tint.base,
      ],
      stops: [0, if (tint.via != null) 0.5, tint.stop],
    ),
  );
}

/// Обычная карточка веба: поверхность, рамка 1 px, скругление 20, без теней.
BoxDecoration cardDecoration(ObColors c, {double radius = ObRadius.lg, Color? color, Color? border}) => BoxDecoration(
      color: color ?? c.surface,
      borderRadius: BorderRadius.circular(radius),
      border: Border.all(color: border ?? c.line),
    );
