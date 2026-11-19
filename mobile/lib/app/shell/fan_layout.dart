import 'dart:math' as math;

/// Раскладка веера над синей кнопкой — перенос `frontend/src/components/shell/fan.ts`, чтобы веер выглядел так же, как в вебе:
/// кнопки на верхней половине эллипса с центром чуть выше кнопки меню, на равном расстоянии по дуге. Высота дуги подбирается —
/// самая низкая, при которой ни кнопки, ни подписи не пересекаются, не заходят на панель и за края экрана; на узком экране кнопки меньше.
class FanSlot {
  const FanSlot(this.x, this.y);

  /// Смещение центра кнопки от центра синей кнопки.
  final double x;
  final double y;
}

class FanLayout {
  const FanLayout(this.slots, this.size);

  final List<FanSlot> slots;

  /// Диаметр круглой кнопки.
  final double size;
}

/// Ширина подписи на плашке (шрифт 11,5): длинная переносится на вторую строку.
const fanLabelMax = 108.0;

({double width, double height}) fanLabelBox(String label) {
  final width = 18 + label.length * 7.0;
  return width <= fanLabelMax ? (width: width, height: 22.0) : (width: fanLabelMax, height: 36.0);
}

class _Rect {
  const _Rect(this.l, this.r, this.t, this.b);
  final double l, r, t, b;
}

const _gap = 4.0;
const _labelGap = 6.0;

bool _overlaps(_Rect a, _Rect b) => a.l < b.r + _gap && b.l < a.r + _gap && a.t < b.b + _gap && b.t < a.b + _gap;

/// [labels] — подписи слева направо; [width] — ширина экрана; [fabX] — центр кнопки меню по горизонтали; [fabTop] — от её центра
/// до верха экрана; [barTop] — на сколько верх панели выше центра кнопки (подписи остаются над панелью).
FanLayout fanLayout(List<String> labels, double width, double fabX, double fabTop, double barTop) {
  final n = labels.length;
  if (n == 0) return const FanLayout([], 62);
  final boxes = labels.map(fanLabelBox).toList();

  bool fits(List<FanSlot> slots, double size) {
    final half = size / 2;
    final rects = [
      for (var i = 0; i < slots.length; i++)
        (
          button: _Rect(slots[i].x - half, slots[i].x + half, slots[i].y - half, slots[i].y + half),
          label: _Rect(
            slots[i].x - boxes[i].width / 2,
            slots[i].x + boxes[i].width / 2,
            slots[i].y + half + _labelGap,
            slots[i].y + half + _labelGap + boxes[i].height,
          ),
        ),
    ];
    const fab = _Rect(-44, 44, -44, 44);
    for (var i = 0; i < rects.length; i++) {
      final a = rects[i];
      if (a.label.b > barTop - 4) return false;
      if (_overlaps(a.button, fab) || _overlaps(a.label, fab)) return false;
      if (fabX + math.min(a.button.l, a.label.l) < 8 || fabX + math.max(a.button.r, a.label.r) > width - 8) return false;
      for (var j = i + 1; j < rects.length; j++) {
        final b = rects[j];
        if (_overlaps(a.button, b.button) || _overlaps(a.button, b.label) || _overlaps(a.label, b.button) || _overlaps(a.label, b.label)) {
          return false;
        }
      }
    }
    return true;
  }

  ({List<FanSlot> slots, bool ok}) attempt(double size) {
    final half = size / 2;
    final endHalf = [half, boxes.first.width / 2, boxes.last.width / 2].reduce(math.max);
    final room = math.min(fabX, width - fabX) - 10 - endHalf;
    final rx = math.max(70.0, math.min(175.0, room));
    const lift = 40.0;
    final maxRy = math.max(120.0, fabTop - 28 - half - lift);
    var last = <FanSlot>[];
    for (var ry = math.min(170.0, maxRy); ry <= maxRy; ry += 8) {
      last = _place(n, rx, ry, lift);
      if (fits(last, size)) return (slots: last, ok: true);
    }
    return (slots: last.isNotEmpty ? last : _place(n, rx, maxRy, lift), ok: false);
  }

  final narrow = width < 360;
  final regular = attempt(narrow ? 56 : 62);
  if (regular.ok || narrow) return FanLayout(regular.slots, narrow ? 56 : 62);
  final compact = attempt(56);
  return compact.ok ? FanLayout(compact.slots, 56) : FanLayout(regular.slots, 62);
}

/// n точек на верхней половине эллипса от 170° до 10°, на равном расстоянии по дуге; одна точка — сверху.
List<FanSlot> _place(int n, double rx, double ry, double lift) {
  FanSlot at(double deg) {
    final t = deg * math.pi / 180;
    return FanSlot(rx * math.cos(t), -(lift + ry * math.sin(t)));
  }

  if (n == 1) return [at(90)];
  const from = 170.0;
  const to = 10.0;
  const steps = 300;
  final points = [for (var i = 0; i <= steps; i++) at(from - (from - to) * i / steps)];
  final lengths = <double>[0];
  for (var i = 1; i < points.length; i++) {
    lengths.add(lengths[i - 1] + math.sqrt(math.pow(points[i].x - points[i - 1].x, 2) + math.pow(points[i].y - points[i - 1].y, 2)));
  }
  final total = lengths.last;
  return [
    for (var k = 0; k < n; k++)
      () {
        final target = total * k / (n - 1);
        final found = lengths.indexWhere((l) => l >= target);
        final i = math.max(1, found < 0 ? lengths.length - 1 : found);
        final span = (lengths[i] - lengths[i - 1]) == 0 ? 1.0 : lengths[i] - lengths[i - 1];
        final f = (target - lengths[i - 1]) / span;
        return FanSlot(
          points[i - 1].x + (points[i].x - points[i - 1].x) * f,
          points[i - 1].y + (points[i].y - points[i - 1].y) * f,
        );
      }(),
  ];
}
