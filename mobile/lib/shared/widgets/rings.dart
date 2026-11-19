import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../extensions/context_ext.dart';

/// Кольцо цели 72×72 (главная карточка веба): прогресс — значение, цвет — по правилам раздела; в центре — подпись и цель.
class GoalRing extends StatelessWidget {
  const GoalRing({super.key, required this.value, required this.color, required this.caption, required this.target, this.size = 72});

  /// 0–100.
  final double value;
  final Color color;
  final String caption;
  final String target;
  final double size;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final share = (value / 100).clamp(0.0, 1.0);
    return SizedBox(
      width: size,
      height: size,
      child: TweenAnimationBuilder<double>(
        tween: Tween(begin: 0, end: share),
        duration: MediaQuery.disableAnimationsOf(context) ? Duration.zero : const Duration(milliseconds: 500),
        curve: Curves.easeOut,
        builder: (context, v, _) => CustomPaint(
          painter: _RingPainter(segments: [(v, color)], track: c.track, stroke: 6, gapDegrees: 0),
          child: Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(caption, style: TextStyle(fontSize: 9, color: c.ink3, height: 1)),
                const SizedBox(height: 3),
                Text(target, style: TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: c.ink, height: 1)),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// Кольцо из долей (задачи: выполнено, в работе, на проверке): зазор 2°, старт сверху по часовой.
class Donut extends StatelessWidget {
  const Donut({super.key, required this.segments, required this.center, this.size = 120, this.stroke = 13});

  /// Доли (значения, не проценты) и цвета.
  final List<(double, Color)> segments;
  final Widget center;
  final double size;
  final double stroke;

  @override
  Widget build(BuildContext context) {
    final sum = segments.fold<double>(0, (a, s) => a + s.$1);
    final shares = [for (final s in segments) (sum > 0 ? s.$1 / sum : 0.0, s.$2)];
    return SizedBox(
      width: size,
      height: size,
      child: TweenAnimationBuilder<double>(
        tween: Tween(begin: 0, end: 1),
        duration: MediaQuery.disableAnimationsOf(context) ? Duration.zero : const Duration(milliseconds: 500),
        curve: Curves.easeOut,
        builder: (context, t, _) => CustomPaint(
          painter: _RingPainter(segments: [for (final s in shares) (s.$1 * t, s.$2)], track: context.ob.track, stroke: stroke, gapDegrees: 2),
          child: Center(child: center),
        ),
      ),
    );
  }
}

class _RingPainter extends CustomPainter {
  _RingPainter({required this.segments, required this.track, required this.stroke, required this.gapDegrees});

  final List<(double, Color)> segments;
  final Color track;
  final double stroke;
  final double gapDegrees;

  @override
  void paint(Canvas canvas, Size size) {
    final r = (math.min(size.width, size.height) - stroke) / 2;
    final rect = Rect.fromCircle(center: size.center(Offset.zero), radius: r);
    canvas.drawCircle(size.center(Offset.zero), r, Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = stroke
      ..color = track);
    final gap = gapDegrees * math.pi / 180;
    var start = -math.pi / 2;
    final multi = segments.length > 1;
    for (final (share, color) in segments) {
      final sweep = share * 2 * math.pi;
      final drawn = multi && sweep > gap ? sweep - gap : sweep;
      if (drawn > 0.001) {
        canvas.drawArc(
          rect,
          start,
          drawn,
          false,
          Paint()
            ..style = PaintingStyle.stroke
            ..strokeWidth = stroke
            ..strokeCap = multi ? StrokeCap.butt : StrokeCap.round
            ..color = color,
        );
      }
      start += sweep;
    }
  }

  @override
  bool shouldRepaint(_RingPainter old) => old.segments != segments || old.track != track;
}

/// Полоса выполнения (отделы): скруглённая, заполняется слева направо.
class ProgressLine extends StatelessWidget {
  const ProgressLine({super.key, required this.value, required this.color, this.height = 6, this.delay = Duration.zero});

  /// 0–1.
  final double value;
  final Color color;
  final double height;
  final Duration delay;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    return ClipRRect(
      borderRadius: BorderRadius.circular(height),
      child: Container(
        height: height,
        color: c.muted,
        alignment: Alignment.centerLeft,
        child: TweenAnimationBuilder<double>(
          tween: Tween(begin: 0, end: value.clamp(0.0, 1.0)),
          duration: MediaQuery.disableAnimationsOf(context) ? Duration.zero : const Duration(milliseconds: 500) + delay,
          curve: Interval(delay.inMilliseconds / (500 + delay.inMilliseconds), 1, curve: Curves.easeOut),
          builder: (context, v, _) => FractionallySizedBox(
            widthFactor: v,
            child: DecoratedBox(decoration: BoxDecoration(color: color, borderRadius: BorderRadius.circular(height))),
          ),
        ),
      ),
    );
  }
}

/// Число при первом показе считает от нуля до значения за 400 мс (ease-out), как `useCountUp` веба.
class CountUp extends StatelessWidget {
  const CountUp({super.key, required this.value, required this.format, this.style});

  final double value;
  final String Function(double) format;
  final TextStyle? style;

  @override
  Widget build(BuildContext context) => TweenAnimationBuilder<double>(
        tween: Tween(begin: 0, end: value),
        duration: MediaQuery.disableAnimationsOf(context) ? Duration.zero : const Duration(milliseconds: 400),
        curve: Curves.easeOutCubic,
        builder: (context, v, _) => Text(format(v), style: style, maxLines: 1, overflow: TextOverflow.ellipsis),
      );
}
