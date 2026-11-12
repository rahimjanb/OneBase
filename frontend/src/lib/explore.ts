// «Аналитик → Срезы»: типы универсального движка (api/analytik/explore) и чистая логика экрана — запрос в адресе, периоды, переход вглубь,
// CSV, модель графика, группировка с подытогами. Все числа считает сервер; здесь только показ.

import { num } from "./analytik.ts";

export type AnalyticsFilter = { dimension: string; op?: "in" | "not_in" | "contains"; values: string[] };
export type AnalyticsSort = { by: string; dir?: "asc" | "desc" };

export type AnalyticsQuery = {
  metrics: string[];
  dimensions?: string[];
  dateFrom?: string | null;
  dateTo?: string | null;
  grain?: string | null;
  filters?: AnalyticsFilter[];
  sort?: AnalyticsSort[];
  limit?: number | null;
  offset?: number | null;
  compare?: string | null;
  compareFrom?: string | null;
  compareTo?: string | null;
  includeTotals?: boolean;
  fillZeros?: boolean;
  segment?: string | null;
  planSource?: string | null;
  planKind?: string | null;
  coverDays?: number | null;
};

export type AnalyticsColumn = { code: string; title: string; kind: "dimension" | "metric" | "compare" | "delta" | "delta_pct"; unit: string | null; format: string };
export type EvidenceCheck = { code: string; ok: boolean; text: string };
export type AnalyticsSource = { system: string; title: string; tables: string; syncedAt: string | null; dataThrough: string | null; rules: string };

export type AnalyticsResult = {
  queryId: string;
  query: AnalyticsQuery;
  columns: AnalyticsColumn[];
  rows: (string | number | null)[][];
  keys: (string | number | null)[][];
  totals: Record<string, number | null>;
  compareTotals: Record<string, number | null> | null;
  totalRows: number;
  hasMore: boolean;
  complete: boolean;
  checks: EvidenceCheck[];
  sources: AnalyticsSource[];
  notes: string[];
  scope: string | null;
  elapsedMs: number;
  from: string;
  to: string;
  compareFrom: string | null;
  compareTo: string | null;
};

export type MetricDef = { code: string; fact: string; title: string; unit: string; formula: string; source: string; permission: string; note: string | null; requires: string[] | null };
export type DimensionDef = { code: string; title: string; facts: string[]; note: string; aliases: string[] | null };
export type CapabilityFact = {
  code: string;
  title: string;
  source: string;
  detail: string;
  metrics: string[];
  dimensions: string[];
  grains: string[];
  first: string | null;
  last: string | null;
  syncedAt: string | null;
  status: "ok" | "stale" | "missing" | "error";
  limits: string | null;
  allowed: boolean;
};
export type CapabilityTool = { area: string; title: string; tools: string; detail: string };
export type SyncRunInfo = { mode: string; startedAt: string; finishedAt: string; status: string; rows: number; earliestChanged: string | null; error: string | null };
export type CapabilityView = {
  metrics: MetricDef[];
  dimensions: DimensionDef[];
  grains: string[];
  facts: CapabilityFact[];
  tools: CapabilityTool[];
  missing: string[];
  syncRuns: SyncRunInfo[];
  at: string;
};

export const factTitles: Record<string, string> = { sales: "Продажи", plan: "План", payments: "Оплаты", stock: "Остатки" };
export const grainTitles: Record<string, string> = { day: "День", week: "Неделя", month: "Месяц", quarter: "Квартал", year: "Год" };

// ---------- запрос в адресе (та же ссылка, что строит AI: /analytik/explore?q=base64url JSON) ----------

export function encodeQuery(q: AnalyticsQuery): string {
  const clean = Object.fromEntries(Object.entries({ ...q, limit: undefined, offset: undefined }).filter(([, v]) => v != null && !(Array.isArray(v) && v.length === 0)));
  const bytes = new TextEncoder().encode(JSON.stringify(clean));
  let binary = "";
  bytes.forEach((b) => (binary += String.fromCharCode(b)));
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

const strings = (v: unknown): string[] => (Array.isArray(v) ? v.filter((x): x is string => typeof x === "string" && x.trim().length > 0) : []);
const date = (v: unknown): string | null => (typeof v === "string" && /^\d{4}-\d{2}-\d{2}$/.test(v) ? v : null);

/** Запрос из адреса; неверный — null (экран откроется со срезом по умолчанию). */
export function decodeQuery(value: string | null | undefined): AnalyticsQuery | null {
  if (!value) return null;
  try {
    const padded = value.replace(/-/g, "+").replace(/_/g, "/") + "===".slice((value.length + 3) % 4);
    const bytes = Uint8Array.from(atob(padded), (c) => c.charCodeAt(0));
    const raw = JSON.parse(new TextDecoder().decode(bytes));
    const metrics = strings(raw.metrics);
    if (metrics.length === 0) return null;
    const filters = Array.isArray(raw.filters)
      ? raw.filters
          .filter((f: unknown): f is { dimension: string } => typeof f === "object" && f != null && typeof (f as { dimension: unknown }).dimension === "string")
          .map((f: { dimension: string; op?: unknown; values?: unknown }) => ({
            dimension: f.dimension,
            op: f.op === "not_in" || f.op === "contains" ? f.op : "in",
            values: strings(f.values),
          }))
          .filter((f: AnalyticsFilter) => f.values.length > 0)
      : [];
    const sort = Array.isArray(raw.sort)
      ? raw.sort.filter((s: unknown): s is { by: string } => typeof s === "object" && s != null && typeof (s as { by: unknown }).by === "string")
          .map((s: { by: string; dir?: unknown }) => ({ by: s.by, dir: s.dir === "asc" ? "asc" : "desc" }))
      : [];
    return {
      metrics,
      dimensions: strings(raw.dimensions),
      dateFrom: date(raw.dateFrom),
      dateTo: date(raw.dateTo),
      grain: typeof raw.grain === "string" ? raw.grain : null,
      filters,
      sort,
      compare: typeof raw.compare === "string" ? raw.compare : null,
      compareFrom: date(raw.compareFrom),
      compareTo: date(raw.compareTo),
      fillZeros: raw.fillZeros === true,
      segment: typeof raw.segment === "string" ? raw.segment : null,
      planSource: typeof raw.planSource === "string" ? raw.planSource : null,
      planKind: typeof raw.planKind === "string" ? raw.planKind : null,
      coverDays: Number.isInteger(raw.coverDays) ? raw.coverDays : null,
    };
  } catch {
    return null;
  }
}

// ---------- периоды ----------

const iso = (d: Date) => d.toISOString().slice(0, 10);

/** Быстрые периоды по сегодняшней дате: месяц, прошлый месяц, 30 и 90 дней, 3 полных месяца, год. */
export function quickPeriods(today: Date): { key: string; label: string; from: string; to: string }[] {
  const y = today.getFullYear();
  const m = today.getMonth();
  const d = today.getDate();
  const day = (offset: number) => iso(new Date(Date.UTC(y, m, d + offset)));
  return [
    { key: "month", label: "Этот месяц", from: iso(new Date(Date.UTC(y, m, 1))), to: day(0) },
    { key: "last_month", label: "Прошлый месяц", from: iso(new Date(Date.UTC(y, m - 1, 1))), to: iso(new Date(Date.UTC(y, m, 0))) },
    { key: "d30", label: "30 дней", from: day(-29), to: day(0) },
    { key: "d90", label: "90 дней", from: day(-89), to: day(0) },
    { key: "m3", label: "3 месяца", from: iso(new Date(Date.UTC(y, m - 3, 1))), to: iso(new Date(Date.UTC(y, m, 0))) },
    { key: "year", label: "С начала года", from: iso(new Date(Date.UTC(y, 0, 1))), to: day(0) },
  ];
}

/** Последний день шага, который начинается в from: день, неделя (пн–вс), месяц, квартал, год. */
export function bucketEnd(from: string, grain: string): string {
  const [y, m, d] = from.split("-").map(Number);
  const end =
    grain === "week" ? new Date(Date.UTC(y, m - 1, d + 6))
    : grain === "month" ? new Date(Date.UTC(y, m, 0))
    : grain === "quarter" ? new Date(Date.UTC(y, m + 2, 0))
    : grain === "year" ? new Date(Date.UTC(y, 12, 0))
    : new Date(Date.UTC(y, m - 1, d));
  return iso(end);
}

// ---------- переход вглубь ----------

/** Куда спускаться с измерения: регион → агент, группа → товар, агент → точка, месяц → дни и т.д. */
export const drillNext: Record<string, string> = {
  direction: "region",
  manager: "region",
  supervisor: "region",
  dealer: "region",
  sales_channel: "region",
  region: "agent",
  branch: "agent",
  agent: "market",
  market: "product",
  product_group: "product",
  product_type: "product",
  product: "market",
  sku: "market",
  market_type: "market",
  stock: "product",
};

const finer: Record<string, string> = { year: "month", quarter: "month", month: "day", week: "day", day: "day" };

/**
 * Переход вглубь по значению измерения: фильтр по этому значению (по id ключа, прежний фильтр того же измерения заменяется) и следующее
 * измерение на его месте; у даты — период этого шага и шаг мельче. null — глубже некуда.
 */
export function drill(q: AnalyticsQuery, dimension: string, key: string | number | null): AnalyticsQuery | null {
  const dims = q.dimensions ?? [];
  if (dimension === "date") {
    if (typeof key !== "string") return null;
    const grain = q.grain ?? "day";
    if (grain === "day") return null;
    return { ...q, dateFrom: key, dateTo: bucketEnd(key, grain), grain: finer[grain], compare: null, offset: 0 };
  }

  const next = drillNext[dimension];
  if (!next) return null;
  const value = key == null ? "Без региона" : String(key);
  // Ключ артикула — id товара: фильтр по товару (код артикула мог бы совпасть с чужим id).
  const filterDim = dimension === "sku" ? "product" : dimension;
  const filters = [...(q.filters ?? []).filter((f) => f.dimension !== filterDim), { dimension: filterDim, op: "in" as const, values: [key == null ? emptyWord(dimension) : value] }];
  const replaced = dims.includes(next) ? dims.filter((d) => d !== dimension) : dims.map((d) => (d === dimension ? next : d));
  return { ...q, dimensions: replaced, filters, offset: 0 };
}

const emptyWord = (dimension: string) =>
  dimension === "region" ? "Без региона" : dimension === "agent" ? "Без агента" : dimension === "market" ? "Без точки" : dimension === "product_group" ? "Без группы" : "∅";

// ---------- оформление ----------

const nbsp = " ";

/** Значение ячейки: суммы и кг — с разрядами, проценты — один знак, даты — дд.мм.гггг. */
export function cellText(value: string | number | null, format: string): string {
  if (value == null) return "—";
  if (typeof value === "string") {
    if (format === "date" && /^\d{4}-\d{2}-\d{2}$/.test(value)) {
      const [y, m, d] = value.split("-");
      return `${d}.${m}.${y}`;
    }
    return value;
  }
  if (format === "percent") return `${num(value, 1)}${nbsp}%`;
  if (format === "money" || format === "integer") return num(value, 0);
  if (format === "kg") return num(value, Math.abs(value) >= 100 ? 0 : 2);
  return num(value, 2);
}

/** Компактно для осей и итогов: «13,51 млрд», «354,0 млн», «12 тыс.». */
export function compactValue(value: number | null, format: string): string {
  if (value == null) return "—";
  if (format === "percent") return `${num(value, 1)}${nbsp}%`;
  const abs = Math.abs(value);
  const sign = value < 0 ? "−" : "";
  if (abs >= 1e9) return `${sign}${num(abs / 1e9, 2)}${nbsp}млрд`;
  if (abs >= 1e6) return `${sign}${num(abs / 1e6, 1)}${nbsp}млн`;
  if (abs >= 1e4) return `${sign}${num(abs / 1e3, 0)}${nbsp}тыс.`;
  return num(value, format === "money" || format === "integer" ? 0 : 2);
}

/** CSV (разделитель «;», UTF-8 с BOM — открывается в Excel с кириллицей): заголовки и строки как в таблице. */
export function toCsv(columns: AnalyticsColumn[], rows: (string | number | null)[][]): string {
  const cell = (v: string | number | null) => {
    if (v == null) return "";
    const s = typeof v === "number" ? String(v).replace(".", ",") : v;
    return /[";\n\r]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  };
  return "﻿" + [columns.map((c) => cell(c.title)).join(";"), ...rows.map((r) => r.map(cell).join(";"))].join("\r\n");
}

// ---------- график ----------

export type ChartModel =
  | { kind: "line"; labels: string[]; series: { name: string; values: (number | null)[] }[]; format: string; metric: string }
  | { kind: "bars"; bars: { label: string; value: number; key: (string | number | null)[] }[]; format: string; metric: string }
  | null;

/**
 * Модель графика: есть дата — линии по дням (неделям, месяцам), серии — значения второго измерения (до 6 крупнейших, остальное — «Прочие»)
 * или одна линия; без даты — столбцы первых 15 строк по показателю.
 */
export function chartModel(result: AnalyticsResult, metric?: string): ChartModel {
  const metricCol = result.columns.findIndex((c) => c.kind === "metric" && (metric == null || c.code === metric));
  if (metricCol < 0 || result.rows.length === 0) return null;
  const column = result.columns[metricCol];
  const dateCol = result.columns.findIndex((c) => c.kind === "dimension" && c.code === "date");
  const otherCol = result.columns.findIndex((c, i) => c.kind === "dimension" && i !== dateCol);
  const value = (r: (string | number | null)[]) => (typeof r[metricCol] === "number" ? (r[metricCol] as number) : null);
  if (dateCol >= 0) {
    const labels = [...new Set(result.rows.map((r) => String(r[dateCol])))].sort();
    if (otherCol < 0) {
      const byDate = new Map(result.rows.map((r) => [String(r[dateCol]), value(r)]));
      return { kind: "line", labels, series: [{ name: column.title, values: labels.map((l) => byDate.get(l) ?? null) }], format: column.format, metric: column.code };
    }
    const totals = new Map<string, number>();
    for (const r of result.rows) totals.set(String(r[otherCol]), (totals.get(String(r[otherCol])) ?? 0) + (value(r) ?? 0));
    const top = [...totals.entries()].sort((a, b) => b[1] - a[1]).slice(0, 6).map(([k]) => k);
    const series = top.map((name) => {
      const byDate = new Map(result.rows.filter((r) => String(r[otherCol]) === name).map((r) => [String(r[dateCol]), value(r)]));
      return { name, values: labels.map((l) => byDate.get(l) ?? null) };
    });
    if (totals.size > top.length) {
      const rest = labels.map((l) => result.rows.filter((r) => String(r[dateCol]) === l && !top.includes(String(r[otherCol]))).reduce((s, r) => s + (value(r) ?? 0), 0));
      series.push({ name: "Прочие", values: rest });
    }
    return { kind: "line", labels, series, format: column.format, metric: column.code };
  }
  const labelCols = result.columns.map((c, i) => (c.kind === "dimension" ? i : -1)).filter((i) => i >= 0);
  const bars = result.rows
    .map((r, i) => ({ label: labelCols.map((c) => String(r[c])).join(" · "), value: value(r) ?? 0, key: result.keys[i] ?? [] }))
    .sort((a, b) => b.value - a.value)
    .slice(0, 15);
  return bars.length ? { kind: "bars", bars, format: column.format, metric: column.code } : null;
}

// ---------- группировка с подытогами ----------

/** Подытоги по первому измерению: суммируются показатели в сумах, кг, единицах и штуках; проценты, средние и АКБ не складываются. */
export function groupRows(result: AnalyticsResult): { label: string; rows: number[]; subtotal: (number | null)[] }[] {
  const first = result.columns.findIndex((c) => c.kind === "dimension");
  if (first < 0) return [];
  const additive = result.columns.map((c) => (c.kind === "metric" || c.kind === "compare" || c.kind === "delta") && c.format !== "percent" && !nonAdditive.has(c.code.split("__")[0]));
  const groups = new Map<string, number[]>();
  result.rows.forEach((r, i) => {
    const key = String(r[first]);
    groups.set(key, [...(groups.get(key) ?? []), i]);
  });
  return [...groups.entries()].map(([label, idx]) => ({
    label,
    rows: idx,
    subtotal: result.columns.map((_, c) => (additive[c] ? idx.reduce((s, i) => s + (typeof result.rows[i][c] === "number" ? (result.rows[i][c] as number) : 0), 0) : null)),
  }));
}

const nonAdditive = new Set(["outlets", "orders", "avg_check", "avg_price_kg", "cover_days", "revenue_share", "plan_execution", "forecast_execution"]);

/** Сортировка: по колонке (код), направление; пустые значения — в конец. */
export function sortRows(result: AnalyticsResult, by: string, dir: "asc" | "desc"): number[] {
  const col = result.columns.findIndex((c) => c.code === by);
  const idx = result.rows.map((_, i) => i);
  if (col < 0) return idx;
  const sign = dir === "asc" ? 1 : -1;
  return idx.sort((a, b) => {
    const x = result.rows[a][col];
    const y = result.rows[b][col];
    if (x == null && y == null) return 0;
    if (x == null) return 1;
    if (y == null) return -1;
    return typeof x === "number" && typeof y === "number" ? (x - y) * sign : String(x).localeCompare(String(y), "ru") * sign;
  });
}
