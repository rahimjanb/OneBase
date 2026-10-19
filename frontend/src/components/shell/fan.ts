/**
 * Раскладка веера нижней панели: позиции круглых кнопок над синей кнопкой меню (как на макете — дуга вверх, выше всех средняя).
 * Кнопки стоят на половине эллипса с центром чуть выше кнопки меню, на равном расстоянии друг от друга по дуге (не по углу: у узкого
 * эллипса равные углы сбивают кнопки наверху в кучу). Высота дуги подбирается: самая низкая, при которой ни кнопки, ни подписи
 * не пересекаются, не заходят на панель и на края экрана; на узком экране кнопки меньше.
 */

export type FanSlot = { x: number; y: number };
export type FanLayout = { slots: FanSlot[]; size: number };

/** Ширина подписи на плашке (шрифт 11,5px) и её строки: длинная подпись переносится на вторую строку. */
export const FAN_LABEL_MAX = 96;
export function labelBox(label: string) {
  const width = 18 + label.length * 6.4;
  return width <= FAN_LABEL_MAX ? { width, height: 22 } : { width: FAN_LABEL_MAX, height: 36 };
}

type Rect = { l: number; r: number; t: number; b: number };
const GAP = 4;
const LABEL_GAP = 6;

const overlaps = (a: Rect, b: Rect) => a.l < b.r + GAP && b.l < a.r + GAP && a.t < b.b + GAP && b.t < a.b + GAP;

/**
 * labels — подписи по порядку слева направо; width — ширина экрана, fabX — центр кнопки меню по горизонтали, fabTop — расстояние от её центра
 * до верха экрана, barTop — на сколько верх панели выше центра кнопки (подписи должны оставаться над панелью).
 */
export function fanLayout(labels: string[], width: number, fabX: number, fabTop: number, barTop: number): FanLayout {
  const n = labels.length;
  if (n === 0) return { slots: [], size: 62 };

  const boxes = labels.map(labelBox);
  const attempt = (size: number) => {
    const half = size / 2;
    // Края веера: подпись крайней кнопки не уходит за экран (отступ 10px).
    const endHalf = Math.max(half, boxes[0].width / 2, boxes[n - 1].width / 2);
    const room = Math.min(fabX, width - fabX) - 10 - endHalf;
    const rx = Math.max(70, Math.min(175, room));
    const lift = 40; // центр эллипса выше центра кнопки: нижние кнопки не садятся на панель
    const maxRy = Math.max(120, fabTop - 28 - half - lift);

    let last: FanSlot[] = [];
    for (let ry = Math.min(170, maxRy); ry <= maxRy; ry += 8) {
      last = place(n, rx, ry, lift);
      if (fits(last, size)) return { slots: last, ok: true };
    }
    return { slots: last.length ? last : place(n, rx, maxRy, lift), ok: false };
  };

  const fits = (slots: FanSlot[], size: number) => {
    const half = size / 2;
    const rects = slots.map((p, i) => ({
      button: { l: p.x - half, r: p.x + half, t: p.y - half, b: p.y + half },
      label: { l: p.x - boxes[i].width / 2, r: p.x + boxes[i].width / 2, t: p.y + half + LABEL_GAP, b: p.y + half + LABEL_GAP + boxes[i].height },
    }));
    const fab: Rect = { l: -44, r: 44, t: -44, b: 44 };
    for (let i = 0; i < rects.length; i++) {
      const a = rects[i];
      if (a.label.b > barTop - 4) return false; // подпись над панелью
      if (overlaps(a.button, fab) || overlaps(a.label, fab)) return false;
      if (fabX + Math.min(a.button.l, a.label.l) < 8 || fabX + Math.max(a.button.r, a.label.r) > width - 8) return false;
      for (let j = i + 1; j < rects.length; j++) {
        const b = rects[j];
        if (overlaps(a.button, b.button) || overlaps(a.button, b.label) || overlaps(a.label, b.button) || overlaps(a.label, b.label)) return false;
      }
    }
    return true;
  };

  const regular = attempt(width < 360 ? 56 : 62);
  if (regular.ok || width < 360) return { slots: regular.slots, size: width < 360 ? 56 : 62 };
  const compact = attempt(56);
  return compact.ok ? { slots: compact.slots, size: 56 } : { slots: regular.slots, size: 62 };
}

/** n точек на верхней половине эллипса от 170° до 10°, на равном расстоянии по дуге; одна точка — сверху. */
function place(n: number, rx: number, ry: number, lift: number): FanSlot[] {
  const at = (deg: number) => {
    const t = (deg * Math.PI) / 180;
    return { x: rx * Math.cos(t), y: -(lift + ry * Math.sin(t)) };
  };
  if (n === 1) return [at(90)];

  const from = 170;
  const to = 10;
  const steps = 300;
  const points = Array.from({ length: steps + 1 }, (_, i) => at(from - ((from - to) * i) / steps));
  const lengths = [0];
  for (let i = 1; i < points.length; i++) {
    lengths.push(lengths[i - 1] + Math.hypot(points[i].x - points[i - 1].x, points[i].y - points[i - 1].y));
  }
  const total = lengths[lengths.length - 1];
  return Array.from({ length: n }, (_, k) => {
    const target = (total * k) / (n - 1);
    const i = Math.max(1, lengths.findIndex((l) => l >= target));
    const span = lengths[i] - lengths[i - 1] || 1;
    const f = (target - lengths[i - 1]) / span;
    return { x: points[i - 1].x + (points[i].x - points[i - 1].x) * f, y: points[i - 1].y + (points[i].y - points[i - 1].y) * f };
  });
}
