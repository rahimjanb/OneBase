// «Производство → Технолог» — чистые функции экрана (без запросов): период в адресе, форматы кг и процентов, подписи кнопок быстрого перехода,
// счётчики, итоги разделов, тон пунктов «Требует внимания». Тест: node --test src/lib/technologist-view.test.mjs.
import { periodOf, quantity } from "./procurement.ts";

/** Период в адресе страницы (?period=…), чтобы ссылкой можно было поделиться. */
export type TechPeriod = "this_month" | "last_month" | "3_months" | "year";

export const TECH_PERIODS: { key: TechPeriod; label: string; short: string }[] = [
  { key: "this_month", label: "Этот месяц", short: "Этот месяц" },
  { key: "last_month", label: "Прошлый месяц", short: "Прошлый" },
  { key: "3_months", label: "3 месяца", short: "3 мес." },
  { key: "year", label: "Год", short: "Год" },
];

const preset: Record<TechPeriod, "month" | "prev" | "quarter" | "year"> = { this_month: "month", last_month: "prev", "3_months": "quarter", year: "year" };

export const parsePeriod = (value: string | null | undefined): TechPeriod | null =>
  TECH_PERIODS.some((p) => p.key === value) ? (value as TechPeriod) : null;

/** Даты периода (по Ташкенту): этот месяц — с 1-го по сегодня, прошлый — весь, 3 месяца и год — по сегодня. */
export const periodRange = (period: TechPeriod, today: string) => periodOf(preset[period], today);

/** Какой период из переключателя соответствует датам (ссылка со старыми from/to); никакой — null. */
export function periodOfRange(from: string, to: string, today: string): TechPeriod | null {
  return TECH_PERIODS.find((p) => {
    const r = periodRange(p.key, today);
    return r.from === from && r.to === to;
  })?.key ?? null;
}

/** Килограммы: «1 250» и единица отдельно (до 0,1). */
export const kg = (value: number) => quantity(Math.round(value * 10) / 10);

/** Проценты с одним знаком: «96,4%»; нет данных — null (на экране — тире). */
export const pct = (value: number | null | undefined): string | null =>
  value == null ? null : `${value.toLocaleString("ru-RU", { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`;

/** Расход к норме — факт в процентах нормы: отклонение +1,2 % → 101,2 %. */
export const consumptionRatio = (deviationPercent: number | null) => (deviationPercent == null ? null : Math.round((100 + deviationPercent) * 10) / 10);

export type Tone = "ok" | "warn" | "bad" | null;

/** Цвет значения KPI: план ниже 90 % — жёлтый; перерасход больше 5 % и брак — красный (как и раньше на панели). */
export const completionTone = (percent: number | null): Tone => (percent != null && percent < 90 ? "warn" : null);
export const consumptionTone = (deviation: number | null): Tone => (deviation != null && deviation > 5 ? "bad" : null);
export const defectTone = (defectKg: number): Tone => (defectKg > 0 ? "bad" : null);

type Counts = { activeOrders: number; openShifts: number; closedShifts: number; pendingRecipes: number };

/** Подпись под названием: ключ перевода и подстановки; warn — есть что утверждать (подпись жёлтая). */
export type Caption = { key: string; vars?: Record<string, number>; warn: boolean };

export type QuickLink = { key: "recipes" | "shifts" | "reports"; title: string; href: string; caption: Caption; mobileCaption: Caption };

export function quickLinks(c: Counts | null): QuickLink[] {
  const recipes: Caption = { key: "на утверждении: {n}", vars: { n: c?.pendingRecipes ?? 0 }, warn: (c?.pendingRecipes ?? 0) > 0 };
  const reports: Caption = { key: "выпуск, брак, расход", warn: false };
  return [
    { key: "recipes", title: "Техкарты", href: "/production/recipes", caption: recipes, mobileCaption: recipes },
    {
      key: "shifts",
      title: "Смены",
      href: "/production/shifts",
      // На компьютере — только открытые; есть ждущие утверждения — они дописываются, и подпись жёлтая.
      caption:
        (c?.closedShifts ?? 0) > 0
          ? { key: "открытых: {n} · ждут утверждения: {m}", vars: { n: c?.openShifts ?? 0, m: c?.closedShifts ?? 0 }, warn: true }
          : { key: "открытых: {n}", vars: { n: c?.openShifts ?? 0 }, warn: false },
      mobileCaption: { key: "открытых: {n} · ждут утверждения: {m}", vars: { n: c?.openShifts ?? 0, m: c?.closedShifts ?? 0 }, warn: (c?.closedShifts ?? 0) > 0 },
    },
    { key: "reports", title: "Отчёты", href: "/production/reports", caption: reports, mobileCaption: reports },
  ];
}

/** Счётчики под KPI: при N > 0 — ссылка в отфильтрованный список. */
export type Counter = { key: "orders" | "open" | "approval" | "recipes"; label: string; value: number; href: string | null };

export function counters(c: Counts): Counter[] {
  const link = (n: number, href: string) => (n > 0 ? href : null);
  return [
    { key: "orders", label: "Заданий в работе", value: c.activeOrders, href: link(c.activeOrders, "/production/orders") },
    { key: "open", label: "Открытых смен", value: c.openShifts, href: link(c.openShifts, "/production/shifts") },
    { key: "approval", label: "Ждут утверждения", value: c.closedShifts, href: link(c.closedShifts, "/production/shifts?status=Closed") },
    { key: "recipes", label: "Техкарт на утверждении", value: c.pendingRecipes, href: link(c.pendingRecipes, "/production/recipes?status=pending") },
  ];
}

/** Полный список раздела — отчёт производства за тот же период. */
export const reportHref = (kind: "output" | "defects" | "packaging" | "check", from: string, to: string) =>
  `/production/reports?kind=${kind}&from=${from}&to=${to}`;

/** Итог раздела для строки на телефоне: число строк и сумма в килограммах (строки в штуках в сумму не входят). */
export function barsTotal(rows: { value: number; unit: string | null }[]): { count: number; kg: number } {
  return { count: rows.length, kg: rows.filter((r) => r.unit == null || r.unit === "кг").reduce((s, r) => s + r.value, 0) };
}

/** Доля строки от наибольшей — для мини-полосы (не меньше 2 %, чтобы была видна). */
export const share = (value: number, max: number) => (max > 0 ? Math.max(2, Math.round((value / max) * 100)) : 0);

/** «Требует внимания»: ждёт утверждения и нехватка — жёлтые, перерасход и брак выше норматива — красные. */
export const issueTone = (kind: string): "warn" | "bad" => (kind === "approval" || kind === "shortage" ? "warn" : "bad");

/** Подпись дня на оси и в подсказке: «10.11». */
export const shortDay = (iso: string) => `${iso.slice(8, 10)}.${iso.slice(5, 7)}`;

/** Деления оси графика: 0, середина и максимум, округлённые до «красивого» шага. */
export function axisTicks(max: number): number[] {
  if (max <= 0) return [0];
  const raw = max / 2;
  const power = 10 ** Math.floor(Math.log10(raw));
  const step = [1, 2, 2.5, 5, 10].map((m) => m * power).find((s) => s >= raw) ?? raw;
  return [0, step, step * 2];
}
