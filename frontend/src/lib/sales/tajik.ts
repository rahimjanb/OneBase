/**
 * «Продажи → Экспорт → Таджикистан»: продажи дилера по его 1С (API /sales и /sklad) — типы ответов /api/sales/tajik и страницы интеграции
 * «1С Таджикистан». Суммы — сомони, вес — по весу единицы 1С (коробка или пачка), регион точки — по словам в названии, ТП или вручную.
 */

export type TajikMonth = { year: number; month: number };

export type TajikPeriod = { year: number; month: number; dataThrough: string | null; closed: boolean; dataFrom: string | null; months: TajikMonth[] };

export type TajikSyncInfo = {
  configured: boolean;
  enabled: boolean;
  lastSuccessAt: string | null;
  lastRunAt: string | null;
  lastStatus: string | null;
  lastError: string | null;
};

export type TajikTotals = { amount: number; kg: number; qty: number; discount: number; documents: number; stores: number; agents: number; products: number };

export type TajikCompare = { from: string; to: string; kg: number; amount: number; vsKg: number | null; vsAmount: number | null };

export type TajikCategoryShare = { name: string; kg: number; share: number | null };

export type TajikCategoryRow = { name: string; kg: number; amount: number; qty: number; stores: number; share: number | null; prevKg: number };

export type TajikRegionRow = {
  id: string;
  name: string;
  kg: number;
  amount: number;
  documents: number;
  stores: number;
  agents: number;
  share: number | null;
  prevKg: number;
  vsPrev: number | null;
  categories: TajikCategoryShare[];
};

export type TajikAgentRow = { name: string; kg: number; amount: number; documents: number; stores: number; regions: string[] };

export type TajikProductStore = { id: string; name: string; regionName: string; qty: number; kg: number; amount: number };

/** Откуда вес единицы: из названия, коробка или штука по цене, вес Linko, двояко (штука), неизвестен. */
export type WeightSource = "name" | "box" | "piece" | "ambiguous" | "linko" | "none";

export type TajikProductRow = {
  code: string;
  name: string;
  category: string | null;
  linkoName: string | null;
  unitKg: number | null;
  weightSource: WeightSource;
  qty: number;
  kg: number;
  amount: number;
  discount: number;
  stores: number;
  avgPrice: number | null;
  where: TajikProductStore[] | null;
};

/** Откуда регион точки: закреплена вручную, слово в названии, регион ТП, не определён. */
export type RegionSource = "manual" | "name" | "agent" | "none";

export type TajikStoreRow = {
  id: string;
  name: string;
  agent: string | null;
  regionId: string;
  regionName: string;
  regionSource: RegionSource;
  kg: number;
  amount: number;
  documents: number;
  lastDate: string;
  categories: TajikCategoryShare[];
};

export type TajikStockRow = { code: string; name: string; warehouse: string; category: string | null; qty: number; kg: number | null; soldQty30: number; daysOfCover: number | null };

export type TajikOverview = {
  period: TajikPeriod;
  sync: TajikSyncInfo;
  totals: TajikTotals;
  prev: TajikCompare | null;
  categories: TajikCategoryRow[];
  regions: TajikRegionRow[];
  agents: TajikAgentRow[];
  products: TajikProductRow[];
  stockDate: string | null;
  stock: TajikStockRow[];
  unresolved: { stores: number; kg: number; share: number | null };
  unknownWeightProducts: number;
};

export type TajikRegionView = {
  period: TajikPeriod;
  id: string;
  name: string;
  keywords: string[];
  category: string | null;
  totals: TajikTotals;
  prev: TajikCompare | null;
  categories: TajikCategoryRow[];
  stores: TajikStoreRow[];
  products: TajikProductRow[];
  agents: TajikAgentRow[];
};

export type TajikDocumentLine = {
  code: string;
  name: string;
  category: string | null;
  qty: number;
  price: number;
  amount: number;
  discount: number;
  discountPercent: number | null;
  kg: number | null;
};

export type TajikDocument = { number: string; date: string; agent: string | null; amount: number; kg: number; discount: number; lines: TajikDocumentLine[] };

export type TajikStoreView = {
  period: TajikPeriod;
  id: string;
  name: string;
  agent: string | null;
  regionId: string;
  regionName: string;
  regionSource: RegionSource;
  totals: TajikTotals;
  categories: TajikCategoryRow[];
  products: TajikProductRow[];
  documents: TajikDocument[];
  months: { year: number; month: number; kg: number; amount: number; documents: number }[];
};

export type TajikSetupView = {
  regions: { id: string; name: string; keywords: string[]; stores: number }[];
  agents: { name: string; regionId: string | null; stores: number; unresolved: number }[];
  stores: { id: string; name: string; agent: string | null; regionId: string; regionName: string; source: RegionSource; manualRegionId: string | null; kg: number; lastDate: string }[];
};

export type TajikSettings = {
  baseUrl: string;
  hasToken: boolean;
  tokenHint: string | null;
  tokenSource: "onebase" | "env" | "none";
  enabled: boolean;
  intervalMinutes: number;
  historyFrom: string;
  updatedAt: string | null;
  lastTestAt: string | null;
  lastTestOk: boolean | null;
  lastTestMessage: string | null;
  configured: boolean;
  isReady: boolean;
  insecure: boolean;
};

export type TajikRun = {
  id: number;
  startedAt: string;
  finishedAt: string | null;
  trigger: "schedule" | "manual" | "full";
  status: "running" | "ok" | "partial" | "failed";
  from: string | null;
  lines: number;
  documents: number;
  added: number;
  changed: number;
  removed: number;
  rejected: number;
  stockRows: number;
  error: string | null;
};

export type TajikIntegrationDetails = {
  code: string;
  name: string;
  description: string;
  departmentCode: string;
  departmentName: string;
  status: "connected" | "error" | "disabled" | "not_configured";
  dataAsOf: string | null;
  settings: TajikSettings;
  running: boolean;
  stats: { lines: number; documents: number; stores: number; from: string | null; through: string | null; stockDate: string | null; removedLines: number };
  runs: TajikRun[];
};

export const NO_REGION = "none";

export const TAJIK_BASE = "/sales/export/tajikistan";

/** Подписи источника веса (ключи словаря — русский текст). */
export const weightSourceLabel: Record<WeightSource, string> = {
  name: "из названия",
  box: "коробка — по цене",
  piece: "штука — по цене",
  ambiguous: "штука — проверить",
  linko: "вес Linko",
  none: "вес неизвестен",
};

/** Подписи источника региона точки. */
export const regionSourceLabel: Record<RegionSource, string> = {
  manual: "закреплена вручную",
  name: "по названию точки",
  agent: "регион ТП",
  none: "регион не определён",
};

export const runTriggerLabel: Record<TajikRun["trigger"], string> = { schedule: "по расписанию", manual: "вручную", full: "вся история" };

export const runStatusLabel: Record<TajikRun["status"], string> = { running: "идёт", ok: "успешно", partial: "частично", failed: "ошибка" };

/** Сомони: до 1000 — с копейками (дирамами), дальше — целыми. */
export function somoni(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return "—";
  const digits = Math.abs(value) < 1000 ? 2 : 0;
  return new Intl.NumberFormat("ru-RU", { minimumFractionDigits: digits, maximumFractionDigits: digits }).format(value);
}

/** Количество в единицах 1С: целое — без дробной части, иначе до трёх знаков. */
export function qty(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return "—";
  return new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 3 }).format(value);
}

/** «Кекс 60% · Бамбук 40%» — первые n категорий по весу. */
export function sharesText(shares: TajikCategoryShare[], n = 3): string {
  return shares
    .slice(0, n)
    .map((s) => `${s.name} ${s.share == null ? "" : `${Math.round(s.share * 100)}%`}`.trim())
    .join(" · ");
}

/** Адрес с периодом и дополнительными параметрами (пустые не пишутся). */
export function tajikHref(path: string, query: string, extra: Record<string, string | null | undefined> = {}): string {
  const q = new URLSearchParams(query);
  for (const [k, v] of Object.entries(extra)) {
    if (v) q.set(k, v);
    else q.delete(k);
  }
  const s = q.toString();
  return s ? `${path}?${s}` : path;
}
