// «Аналитик → Прогнозы» (макет untitled.pen: UVDBc — компьютер, CBnGo — телефон): параметры в адресе, сценарии, шкала ошибки, точки
// графика, таблица и CSV, передача в Симулятор. Прогноз считает сервер (GET /api/analytik/forecast, …/forecast/backtest) — здесь только
// показ. Тест: node --test src/lib/forecasts.test.mjs.
import { compact, num, type ForecastView } from "./analytik.ts";
import { encodeDraft, type ScenarioKind } from "./simulator.ts";

export type ForecastMetric = "sales.revenue" | "sales.kg" | "sales.orders" | "finance.payments" | "procurement.consumption";
export type Step = "day" | "week" | "month";
export type Horizon = 30 | 60 | 90;
export type Scenario = "pessimistic" | "realistic" | "optimistic";

export type ForecastParams = {
  metric: ForecastMetric;
  /** Срез расхода сырья — позиция склада; null — всё сырьё. У продаж и оплат срез один. */
  item: string | null;
  step: Step;
  horizon: Horizon;
  /** «Что если», % к прогнозу: −50…+50. */
  whatIf: number;
  scenario: Scenario;
  /** Линия «Прошлые прогнозы» на графике. */
  past: boolean;
};

/** Показатели: подпись на компьютере, короткая — на телефоне, иконка (lucide) — в компоненте. */
export const METRICS: { metric: ForecastMetric; label: string; short: string }[] = [
  { metric: "sales.revenue", label: "Выручка", short: "Выручка" },
  { metric: "sales.kg", label: "Продажи, кг", short: "Кг" },
  { metric: "sales.orders", label: "Заказы", short: "Заказы" },
  { metric: "finance.payments", label: "Оплаты", short: "Оплаты" },
  { metric: "procurement.consumption", label: "Расход сырья", short: "Сырьё" },
];

export const STEPS: { step: Step; label: string; mobile: string }[] = [
  { step: "day", label: "День", mobile: "По дням" },
  { step: "week", label: "Неделя", mobile: "По неделям" },
  { step: "month", label: "Месяц", mobile: "По месяцам" },
];

export const HORIZONS: Horizon[] = [30, 60, 90];
export const WHAT_IF = { min: -50, max: 50, quick: [-10, 10, 20] } as const;
export const SCENARIOS: Scenario[] = ["pessimistic", "realistic", "optimistic"];

export const DEFAULTS: Omit<ForecastParams, "metric"> = { item: null, step: "day", horizon: 30, whatIf: 0, scenario: "realistic", past: true };

type Query = Record<string, string | string[] | undefined> | URLSearchParams;
const read = (q: Query, key: string): string | undefined => {
  if (q instanceof URLSearchParams) return q.get(key) ?? undefined;
  const v = q[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

/** Параметры из адреса; чужие и неверные значения — по умолчанию, показатель — первый открытый пользователю. */
export function parseParams(q: Query, available: readonly ForecastMetric[]): ForecastParams {
  const metric = available.find((m) => m === read(q, "metric")) ?? available[0] ?? "sales.revenue";
  const step = STEPS.find((s) => s.step === read(q, "step"))?.step ?? DEFAULTS.step;
  const horizon = HORIZONS.find((h) => String(h) === read(q, "h")) ?? DEFAULTS.horizon;
  const raw = Number(read(q, "whatif"));
  const whatIf = Number.isFinite(raw) ? Math.max(WHAT_IF.min, Math.min(WHAT_IF.max, Math.round(raw))) : 0;
  const scenario = SCENARIOS.find((s) => s === read(q, "scenario")) ?? DEFAULTS.scenario;
  const item = metric === "procurement.consumption" ? (read(q, "item") ?? null) : null;
  return { metric, item, step, horizon, whatIf, scenario, past: read(q, "past") !== "0" };
}

/** Адрес страницы: только то, что отличается от умолчаний (показатель — всегда). */
export function toQuery(p: ForecastParams): string {
  const q = new URLSearchParams({ metric: p.metric });
  if (p.item) q.set("item", p.item);
  if (p.step !== DEFAULTS.step) q.set("step", p.step);
  if (p.horizon !== DEFAULTS.horizon) q.set("h", String(p.horizon));
  if (p.whatIf) q.set("whatif", String(p.whatIf));
  if (p.scenario !== DEFAULTS.scenario) q.set("scenario", p.scenario);
  if (!p.past) q.set("past", "0");
  return q.toString();
}

/** Горизонт 30 / 60 / 90 дней в шагах ряда: недели — 5 / 9 / 13, месяцы — 1 / 2 / 3. */
export const horizonSteps = (step: Step, days: Horizon): number => (step === "day" ? days : step === "week" ? Math.ceil(days / 7) : Math.round(days / 30));

/** Запрос прогноза к API (что если — в процентах, сервер умножает прогноз и сценарии). */
export function forecastQuery(p: ForecastParams): string {
  const q = new URLSearchParams({ metric: p.metric, granularity: p.step, horizon: String(horizonSteps(p.step, p.horizon)) });
  if (p.whatIf) q.set("scenario", String(p.whatIf));
  if (p.item) q.set("item", p.item);
  return q.toString();
}

export function backtestQuery(p: Pick<ForecastParams, "metric" | "step" | "item">): string {
  const q = new URLSearchParams({ metric: p.metric, granularity: p.step });
  if (p.item) q.set("item", p.item);
  return q.toString();
}

/** «Точность прошлых прогнозов» (GET /api/analytik/forecast/backtest). */
export type BacktestView = {
  metric: string;
  granularity: Step;
  unit: string;
  months: { month: string; forecast: number; fact: number; accuracy: number | null; method: string; saved: boolean }[];
  past: { date: string; value: number }[];
  scope: string | null;
};

// ---- точность ----

export type WapeLevel = { key: "excellent" | "reliable" | "rough" | "unreliable"; label: string; filled: number; tone: "ok" | "warn" | "bad" };

/** Пороги ошибки (WAPE) — конфиг экрана: до 15 % — отличный, до 25 % — надёжный, до 40 % — приблизительный, дальше — ненадёжный. */
export const WAPE_LEVELS: (WapeLevel & { below: number })[] = [
  { key: "excellent", label: "отличный", filled: 1, tone: "ok", below: 0.15 },
  { key: "reliable", label: "надёжный", filled: 2, tone: "ok", below: 0.25 },
  { key: "rough", label: "приблизительный", filled: 3, tone: "warn", below: 0.4 },
  { key: "unreliable", label: "ненадёжный", filled: 4, tone: "bad", below: Number.POSITIVE_INFINITY },
];
/** Цвет делений шкалы: первые два — зелёные, третье — жёлтое, четвёртое — красное. */
export const SEGMENT_TONES = ["ok", "ok", "warn", "bad"] as const;
/** Подсказка у шкалы: граница «хорошо». */
export const WAPE_GOOD = 0.25;

export const wapeLevel = (wape: number | null | undefined): WapeLevel | null => (wape == null ? null : (WAPE_LEVELS.find((l) => wape < l.below) ?? null));

/** Проверенные методы: до 30 % ошибки — жёлтый, дальше — красный; лучший — зелёный (выделен строкой). */
export const methodTone = (wape: number | null, best: boolean): "ok" | "warn" | "bad" | "muted" => (best ? "ok" : wape == null ? "muted" : wape < 0.3 ? "warn" : "bad");

/** Точность месяца: от 90 % — зелёный, от 70 % — жёлтый, ниже — красный. */
export const accuracyTone = (accuracy: number | null): "ok" | "warn" | "bad" | "muted" =>
  accuracy == null ? "muted" : accuracy >= 0.9 ? "ok" : accuracy >= 0.7 ? "warn" : "bad";

/** Методы прогноза: короткое имя (как в макете) и что он учитывает. */
export const METHOD_NAMES: Record<string, { name: string; about: string }> = {
  holt_winters: { name: "Хольт–Винтерс", about: "тренд и сезонность" },
  holt: { name: "Хольт", about: "уровень и тренд" },
  seasonal_naive: { name: "Сезонное наивное", about: "как неделю или год назад" },
  moving_average: { name: "Скользящее среднее", about: "среднее последних точек" },
  linear_trend: { name: "Линейный тренд", about: "прямая по истории" },
  naive: { name: "Последнее значение", about: "последняя точка истории" },
};
export const methodName = (code: string) => METHOD_NAMES[code]?.name ?? code;

// ---- сценарии ----

export type ScenarioCard = { key: Scenario; value: number; delta: number | null };

/**
 * Три сценария за горизонт: пессимистичный — нижняя граница (P10), реалистичный — оценка (P50), оптимистичный — верхняя (P90); изменение —
 * к сумме такого же числа последних шагов истории («к прошлым 30 дн.»). Истории меньше горизонта — изменения нет.
 */
export function scenarioCards(view: Pick<ForecastView, "history" | "result">): ScenarioCard[] {
  const n = view.result.points.length;
  const prev = view.history.length >= n && n > 0 ? view.history.slice(-n).reduce((s, p) => s + p.value, 0) : 0;
  const delta = (v: number) => (prev > 0 ? v / prev - 1 : null);
  return [
    { key: "pessimistic", value: view.result.totalLower, delta: delta(view.result.totalLower) },
    { key: "realistic", value: view.result.total, delta: delta(view.result.total) },
    { key: "optimistic", value: view.result.totalUpper, delta: delta(view.result.totalUpper) },
  ];
}

export const previousTotal = (view: Pick<ForecastView, "history" | "result">): number | null => {
  const n = view.result.points.length;
  return view.history.length >= n && n > 0 ? view.history.slice(-n).reduce((s, p) => s + p.value, 0) : null;
};

/** «к прошлым 30 дн.» / «к прошлым 5 нед.» / «к прошлым 3 мес.» — по шагу и числу точек. */
export const prevPeriodText = (step: Step, n: number) => (step === "day" ? `${n} дн.` : step === "week" ? `${n} нед.` : `${n} мес.`);

/** Знак изменения: «+31%», «−34%», «0%». */
export function pct(value: number | null | undefined, digits = 0): string {
  if (value == null || !Number.isFinite(value)) return "—";
  const v = Math.round(value * 100 * 10 ** digits) / 10 ** digits;
  return `${v > 0 ? "+" : v < 0 ? "−" : ""}${num(Math.abs(v), digits)}%`;
}

/** Число и единица по отдельности: «12,6» + «млрд сум», «412» + «тыс. кг», «830» + «шт». */
export function amount(value: number, unit: string): { head: string; tail: string } {
  const text = compact(value);
  // compact() ставит неразрывный пробел перед «млрд», «млн», «тыс.».
  const i = text.lastIndexOf("\u00a0");
  const [head, scale] = i >= 0 && /^\D/.test(text.slice(i + 1)) ? [text.slice(0, i), text.slice(i + 1)] : [text, ""];
  return { head, tail: [scale, unit].filter(Boolean).join(" ") };
}

// ---- график ----

export type ChartPoint = {
  date: string;
  kind: "fact" | "forecast";
  fact: number | null;
  /** Линия выбранного сценария (P10 / P50 / P90). */
  forecast: number | null;
  lower: number | null;
  upper: number | null;
  /** Прошлый прогноз на эту дату (проверка на истории или сохранённый). */
  past: number | null;
  /** Оценка P50 — для подсказки рядом со сценарием. */
  median: number | null;
};

/** Сколько истории видно на графике: дни — 60, недели — 26, месяцы — 24. */
export const HISTORY_WINDOW: Record<Step, number> = { day: 60, week: 26, month: 24 };

export function chartPoints(view: Pick<ForecastView, "history" | "result" | "granularity">, scenario: Scenario, past: BacktestView | null): ChartPoint[] {
  const pastByDate = new Map((past?.past ?? []).map((p) => [p.date, p.value]));
  const history = view.history.slice(-HISTORY_WINDOW[view.granularity]);
  const pick = (p: { value: number; lower: number; upper: number }) => (scenario === "pessimistic" ? p.lower : scenario === "optimistic" ? p.upper : p.value);
  return [
    ...history.map((h) => ({ date: h.date, kind: "fact" as const, fact: h.value, forecast: null, lower: null, upper: null, past: pastByDate.get(h.date) ?? null, median: null })),
    ...view.result.points.map((p) => ({ date: p.date, kind: "forecast" as const, fact: null, forecast: pick(p), lower: p.lower, upper: p.upper, past: null, median: p.value })),
  ];
}

/** Подсказка прогноза по дням: среднее последних 4 фактов того же дня недели. */
export function sameWeekday(history: { date: string; value: number }[], date: string): number | null {
  const dow = new Date(`${date}T12:00:00Z`).getUTCDay();
  const same = history.filter((h) => new Date(`${h.date}T12:00:00Z`).getUTCDay() === dow).slice(-4);
  return same.length === 0 ? null : same.reduce((s, h) => s + h.value, 0) / same.length;
}

/**
 * Деления оси от 0 «круглым» шагом (1, 2, 2,5, 3, 4, 5 × 10ⁿ): count интервалов (на компьютере 5 подписей), а если верх оси тогда больше
 * максимума в полтора раза — count + 1 интервал помельче (2,05 млрд → 0…2,5, а не 0…4).
 */
export function niceTicks(max: number, count = 4): number[] {
  if (!(max > 0)) return [0, 1, 2, 3, 4].slice(0, count + 1);
  const ticks = (intervals: number) => {
    const raw = max / intervals;
    const pow = 10 ** Math.floor(Math.log10(raw));
    const step = [1, 2, 2.5, 3, 4, 5, 10].map((m) => m * pow).find((s) => s >= raw) ?? 10 * pow;
    const k = Math.max(1, Math.ceil(max / step - 1e-9));
    return Array.from({ length: k + 1 }, (_, i) => Math.round(i * step * 1e6) / 1e6);
  };
  const base = ticks(count);
  return base[base.length - 1] > max * 1.5 ? ticks(count + 1) : base;
}

/** Слова масштаба и единиц из compact() и сервера («млрд», «сум»…) — по словарю языка интерфейса. */
export const localizeUnits = (text: string, t: (s: string) => string) => text.replace(/млрд|млн|тыс\.|сум|кг|шт|ед\./gu, (w) => t(w));

/** Ключ словаря «к прошлым 30 дн.»: «{n} дн.», «{n} нед.», «{n} мес.». */
export const prevPeriodKey = (step: Step) => (step === "day" ? "{n} дн." : step === "week" ? "{n} нед." : "{n} мес.");

// ---- таблица, CSV, симулятор ----

/** Строки «Показать таблицей» и CSV: дата, факт, прогноз выбранного сценария, P10, P90, прошлый прогноз. */
export const tableRows = (points: ChartPoint[]) =>
  points.map((p) => ({ date: p.date, fact: p.fact, forecast: p.forecast, lower: p.lower, upper: p.upper, past: p.past }));

/** CSV для Excel (точка с запятой, запятая в дробях, BOM): заголовки уже переведены. */
export function toCsv(points: ChartPoint[], headers: string[]): string {
  const cell = (v: number | null) => (v == null ? "" : String(Math.round(v * 100) / 100).replace(".", ","));
  const lines = tableRows(points).map((r) => [r.date, cell(r.fact), cell(r.forecast), cell(r.lower), cell(r.upper), cell(r.past)].join(";"));
  return `﻿${[headers.join(";"), ...lines].join("\r\n")}\r\n`;
}

/** Вид сценария в Симуляторе по показателю. */
export const simulatorKind = (metric: ForecastMetric): ScenarioKind =>
  metric === "finance.payments" ? "finance" : metric === "procurement.consumption" ? "materials" : "sales";

/** «Учесть изменения в Симуляторе»: тот же горизонт, «что если» продаж — изменением спроса (sales.demand). */
export function simulatorHref(p: Pick<ForecastParams, "metric" | "horizon" | "whatIf">): string {
  const kind = simulatorKind(p.metric);
  const parameters = kind === "sales" && p.whatIf ? [{ code: "sales.demand", value: p.whatIf, target: null }] : [];
  return `/analytik/simulator?draft=${encodeDraft({ kind, horizonDays: p.horizon, parameters, newRegionName: null, from: null, to: null })}`;
}

/** Край месяца, учтённый в прогнозе по дням (result.calendar): последний день месяца и 1-е число, множитель к обычному дню. */
export type CalendarEffect = { kind: "last" | "first"; factor: number; months: number };

export const calendarOf = (view: { result: object } | null | undefined): CalendarEffect[] =>
  (view?.result as { calendar?: CalendarEffect[] } | undefined)?.calendar ?? [];

/** «×2,0», «×0,61». */
export const factorText = (f: number) => `×${f.toFixed(f < 1 ? 2 : 1).replace(".", ",")}`;

/** Край месяца для даты (YYYY-MM-DD): last — последний день, first — 1-е число, иначе null. */
export function edgeOf(iso: string): CalendarEffect["kind"] | null {
  const d = new Date(`${iso}T12:00:00Z`);
  const next = new Date(d.getTime() + 86_400_000);
  return next.getUTCDate() === 1 ? "last" : d.getUTCDate() === 1 ? "first" : null;
}

/** Последний день факта — «данные на 12.11». */
export const dataAsOf = (view: Pick<ForecastView, "history"> | null): string | null => view?.history.at(-1)?.date ?? null;
