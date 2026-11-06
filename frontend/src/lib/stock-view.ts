/**
 * «Снабжение → Склад» без React: подпись даты движения, номера страниц, переключение сортировки, сохранённые колонки, подсказка о
 * минимумах, цвет плашки «Доступно». Проверяется тестами (stock-view.test.mjs).
 */

/** День и время по Ташкенту: { day: "2026-11-06", time: "12:36" }. */
function tashkent(at: Date): { day: string; time: string } {
  const parts = Object.fromEntries(
    new Intl.DateTimeFormat("en-GB", { timeZone: "Asia/Tashkent", year: "numeric", month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit", hourCycle: "h23" })
      .formatToParts(at)
      .map((p) => [p.type, p.value]),
  );
  return { day: `${parts.year}-${parts.month}-${parts.day}`, time: `${parts.hour}:${parts.minute}` };
}

/**
 * Когда было движение — для строки склада: сегодня и вчера — со временем («вчера, 12:36»: when выбирает ключ перевода, time — значение),
 * раньше — дата «04.11.2026». По Ташкенту.
 */
export function movedWhen(iso: string, now: Date = new Date()): { when: "today" | "yesterday" | "date"; time: string; date: string } {
  const at = tashkent(new Date(iso));
  const today = tashkent(now).day;
  const yesterday = tashkent(new Date(now.getTime() - 86_400_000)).day;
  const [y, m, d] = at.day.split("-");
  return { when: at.day === today ? "today" : at.day === yesterday ? "yesterday" : "date", time: at.time, date: `${d}.${m}.${y}` };
}

/** Номера страниц с пропусками, как на макете: 1 2 3 … 11; 1 … 5 6 7 … 11; пропуск в одну страницу — сама страница. */
export function pageList(page: number, pages: number): (number | "gap")[] {
  if (pages <= 7) return Array.from({ length: pages }, (_, i) => i + 1);
  let around: number[];
  if (page <= 3) around = page === 3 ? [1, 2, 3, 4] : [1, 2, 3];
  else if (page >= pages - 2) around = page === pages - 2 ? [pages - 3, pages - 2, pages - 1, pages] : [pages - 2, pages - 1, pages];
  else around = [page - 1, page, page + 1];
  const set = [...new Set([1, ...around, pages])].sort((a, b) => a - b);
  const out: (number | "gap")[] = [];
  set.forEach((n, i) => {
    const prev = set[i - 1];
    if (prev !== undefined && n - prev === 2) out.push(prev + 1);
    else if (prev !== undefined && n - prev > 2) out.push("gap");
    out.push(n);
  });
  return out;
}

/** Колонки, где сначала нужны большие значения и свежие даты: первое нажатие — по убыванию. */
const descFirst = new Set(["onHand", "reserved", "available", "moved"]);

/** Нажатие на заголовок: та же колонка — сменить направление, другая — её привычное направление. sort "" — по названию. */
export function nextSort(sort: string, desc: boolean, key: string): { sort: string; desc: boolean } {
  if (sort === key) return { sort, desc: !desc };
  return { sort: key, desc: descFirst.has(key) };
}

export type StockColumn = "reserved" | "min" | "moved";
export const allColumns: StockColumn[] = ["reserved", "min", "moved"];

/** Колонки из браузера: неизвестные отбрасываются, нет записи или испорчена — все. */
export function readColumns(raw: string | null): StockColumn[] {
  if (raw === null) return allColumns;
  try {
    const list = JSON.parse(raw);
    return Array.isArray(list) ? allColumns.filter((c) => list.includes(c)) : allColumns;
  } catch {
    return allColumns;
  }
}

/**
 * Подсказка о минимумах: most — у большинства активных позиций нет минимума (текст макета), moving — есть ходовые без минимума, null —
 * подсказки нет.
 */
export function minimumHint(s: { items: number; noMin: number; noMinMoving: number }): "most" | "moving" | null {
  if (s.items > 0 && s.noMin * 2 > s.items) return "most";
  return s.noMinMoving > 0 ? "moving" : null;
}

/** Плашка «Доступно»: нет на складе — empty, ниже минимума — low, всё в резерве или не выдаётся — none, иначе ok. */
export function availableTone(row: { onHand: number; available: number; low: boolean }): "empty" | "low" | "none" | "ok" {
  if (row.onHand <= 0) return "empty";
  if (row.low) return "low";
  return row.available <= 0 ? "none" : "ok";
}
