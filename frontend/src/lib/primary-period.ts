/**
 * Период и единицы обзора «Первички» (/sales/primary): разбор адреса (?period=ytd&year=2026&unit=t), адреса API, кнопок и детальных
 * страниц, период словами. Без импортов — проверяется тестами (primary-period.test.mjs); названия месяцев и перевод передаёт страница.
 */

export type PrimaryPeriodType = "month" | "quarter" | "ytd";
export type PrimaryUnit = "t" | "sum";

/** Что выбрано в адресе: тип периода, год, месяц, квартал (необязательны — по умолчанию сервер берёт последние данные), единицы. */
export type PrimaryChoice = { period: PrimaryPeriodType; year?: number; month?: number; quarter?: number; unit: PrimaryUnit };

/** Период из ответа сервера: границы и по какой день есть данные. */
export type PrimaryPeriodView = { type: PrimaryPeriodType; year: number; month: number | null; quarter: number | null; from: string; to: string; through: string | null };

type Params = Record<string, string | string[] | undefined>;
const one = (sp: Params, key: string) => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};
const int = (value: string | undefined, min: number, max: number) => {
  const n = Number(value);
  return Number.isInteger(n) && n >= min && n <= max ? n : undefined;
};

export function readChoice(sp: Params): PrimaryChoice {
  const period = one(sp, "period");
  return {
    period: period === "month" || period === "quarter" ? period : "ytd",
    year: int(one(sp, "year"), 2000, 2100),
    month: int(one(sp, "month"), 1, 12),
    quarter: int(one(sp, "quarter"), 1, 4),
    unit: one(sp, "unit") === "sum" ? "sum" : "t",
  };
}

/** Адрес страницы (и запроса к API — те же параметры): только нужное выбранному периоду; единицы «т» — по умолчанию, в адрес не пишутся. */
export function choiceQuery(c: PrimaryChoice, withUnit = true): string {
  const q = new URLSearchParams({ period: c.period });
  if (c.year) q.set("year", String(c.year));
  if (c.period === "month" && c.month) q.set("month", String(c.month));
  if (c.period === "quarter" && c.quarter) q.set("quarter", String(c.quarter));
  if (withUnit && c.unit === "sum") q.set("unit", "sum");
  return q.toString();
}

/** Выбор после переключения: тип периода, год, месяц или квартал (остальное сохраняется). */
export const choose = (c: PrimaryChoice, patch: Partial<PrimaryChoice>): PrimaryChoice => ({ ...c, ...patch });

/** Квартал месяца. */
export const quarterOf = (month: number) => Math.floor((month - 1) / 3) + 1;

const roman = ["I", "II", "III", "IV"];
export const quarterName = (q: number) => roman[q - 1] ?? String(q);

type Names = { month: (m: number) => string; short: (m: number) => string; t: (s: string, vars?: Record<string, string | number>) => string };

/** Период словами: «2026 с начала года», «ноябрь 2026», «IV квартал 2026». */
export function periodWords(p: Pick<PrimaryPeriodView, "type" | "year" | "month" | "quarter">, n: Names): string {
  if (p.type === "month" && p.month) return `${n.month(p.month)} ${p.year}`;
  if (p.type === "quarter" && p.quarter) return n.t("{q} квартал {year}", { q: quarterName(p.quarter), year: p.year });
  return n.t("{year} с начала года", { year: p.year });
}

const dayMonth = (iso: string, n: Names) => `${Number(iso.slice(8, 10))} ${n.short(Number(iso.slice(5, 7))).toLowerCase()}`;

/**
 * Кнопка уточнения периода: «2026 · янв – 5 ноя» (с начала года), «IV квартал 2026 · окт – 5 ноя», «Ноябрь 2026 · 1–5 ноя».
 * through — по какой день данные; если период закончился — до конца периода.
 */
export function rangeLabel(c: { period: PrimaryPeriodType; year: number; month?: number; quarter?: number }, through: string | null, n: Names): string {
  const first = c.period === "month" ? (c.month ?? 1) : c.period === "quarter" ? 3 * (c.quarter ?? 1) - 2 : 1;
  const last = c.period === "month" ? (c.month ?? 1) : c.period === "quarter" ? 3 * (c.quarter ?? 1) : 12;
  const end = new Date(Date.UTC(c.year, last, 0)).toISOString().slice(0, 10);
  const start = `${c.year}-${String(first).padStart(2, "0")}-01`;
  const head = c.period === "month" ? `${capital(n.month(first))} ${c.year}` : c.period === "quarter" ? n.t("{q} квартал {year}", { q: quarterName(c.quarter ?? 1), year: c.year }) : String(c.year);
  if (!through || through < start) return head;
  const to = through < end ? through : end;
  const from = c.period === "month" ? `1` : n.short(first).toLowerCase();
  const tail = c.period === "month" ? `${from}–${dayMonth(to, n)}` : to === end ? `${from} – ${n.short(last).toLowerCase()}` : `${from} – ${dayMonth(to, n)}`;
  return `${head} · ${tail}`;
}

const capital = (s: string) => (s ? s[0].toUpperCase() + s.slice(1) : s);

/**
 * Месяц детальной страницы направления (она — за месяц и с начала года): у месяца — он сам, у квартала и года — последний месяц с
 * данными в периоде (или последний месяц периода, если он закончился).
 */
export function detailMonth(p: Pick<PrimaryPeriodView, "type" | "year" | "month" | "to" | "through">): { year: number; month: number } {
  if (p.type === "month" && p.month) return { year: p.year, month: p.month };
  const day = p.through && p.through < p.to ? p.through : p.to;
  return { year: Number(day.slice(0, 4)), month: Number(day.slice(5, 7)) };
}
