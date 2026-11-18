import 'package:flutter/material.dart';

import '../../app/theme/tokens.dart';
import '../extensions/context_ext.dart';

/// Мерцание заготовок: один контроллер анимации на группу, а не на каждый прямоугольник.
class SkeletonGroup extends StatefulWidget {
  const SkeletonGroup({super.key, required this.child});

  final Widget child;

  @override
  State<SkeletonGroup> createState() => _SkeletonGroupState();
}

class _SkeletonGroupState extends State<SkeletonGroup> with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(vsync: this, duration: const Duration(milliseconds: 1100))
    ..repeat(reverse: true);
  late final Animation<double> _opacity = Tween(begin: 0.45, end: 1.0).animate(CurvedAnimation(parent: _c, curve: Curves.easeInOut));

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Semantics(
        label: context.l10n.stateLoading,
        child: ExcludeSemantics(child: FadeTransition(opacity: _opacity, child: widget.child)),
      );
}

/// Заготовка-прямоугольник на месте будущего текста, числа или графика.
class Skeleton extends StatelessWidget {
  const Skeleton({super.key, this.width, this.height = 14, this.radius = 6});

  final double? width;
  final double height;
  final double radius;

  @override
  Widget build(BuildContext context) => Container(
        width: width,
        height: height,
        decoration: BoxDecoration(color: context.ob.muted, borderRadius: BorderRadius.circular(radius)),
      );
}

/// Заготовка карточки блока: заголовок, крупное число, строка.
class SkeletonCard extends StatelessWidget {
  const SkeletonCard({super.key, this.height = 120});

  final double height;

  @override
  Widget build(BuildContext context) => SkeletonGroup(
        child: Container(
          height: height,
          padding: const EdgeInsets.all(ObSpace.lg),
          decoration: BoxDecoration(
            color: context.ob.surface,
            borderRadius: BorderRadius.circular(ObRadius.lg),
            border: Border.all(color: context.ob.line),
          ),
          child: const Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Skeleton(width: 90, height: 12),
              SizedBox(height: ObSpace.md),
              Skeleton(width: 140, height: 26),
              Spacer(),
              Skeleton(height: 10),
            ],
          ),
        ),
      );
}
