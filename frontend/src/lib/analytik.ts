/**
 * Раздел «Аналитик» (OneBase Intelligence): типы ответов API и чистые функции оформления (числа, периоды, статусы). Без React — проверяется
 * тестами (analytik.test.mjs).
 */

export type PeriodKind = "today" | "yesterday" | "week" | "month" | "quarter" | "year" | "custom";

export const periodKinds: { kind: PeriodKind; label: string }[] = [
  { kind: "today", label: "Сегодня" },
  { kind: "yesterday", label: "Вчера" },
  { kind: "week", label: "Неделя" },
  { kind: "month", label: "Месяц" },
  { kind: "quarter", label: "Квартал" },
  { kind: "year", label: "Год" },
  { kind: "custom", label: "Период" },
];

export type Period = { kind: PeriodKind; from: string; to: string; previousFrom: string; previousTo: string; label: string; previousLabel: string; days: number };

export type KpiKind = "fact" | "calculated" | "forecast";

export type Kpi = {
  code: string;
  title: string;
  value: number | null;
  unit: string;
  previous: number | null;
  changePercent: number | null;
  kind: KpiKind;
  source: string;
  href: string | null;
  note: string | null;
};

export type Block = { kpis: Kpi[]; notes: string[]; sources: string[] };

export type SalesRegionRow = {
  id: string;
  name: string;
  revenue: number;
  kg: number;
  akb: number;
  orders: number;
  previousRevenue: number | null;
  changePercent: number | null;
  planKg: number | null;
  execution: number | null;
  forecastExecution: number | null;
};

export type SalesDay = { date: string; revenue: number; kg: number; orders: number; akb: number };

export type SalesMonthPlan = {
  year: number;
  month: number;
  dataThrough: string;
  factKg: number;
  planKg: number | null;
  execution: number | null;
  forecastKg: number | null;
  forecastExecution: number | null;
  revenue: number;
  planSource: string;
  regions: SalesRegionRow[];
  planFactKg: number | null;
};

export type SalesBlock = Block & {
  period: Period;
  dataThrough: string | null;
  complete: boolean;
  daily: SalesDay[];
  regions: SalesRegionRow[];
  plan: SalesMonthPlan | null;
  scope: string | null;
};

export type ProductionOrderRow = { number: string; recipe: string; plannedDate: string; status: string; plannedQty: number; outputQty: number | null; completionPercent: number | null; unit: string };
export type ProducibleComponent = { item: string; required: number; unit: string; available: number; cost: number | null; sharePercent: number | null };
export type ProducibleRow = {
  recipeId: string;
  code: string;
  name: string;
  outputUnit: string;
  baseQuantity: number;
  producible: number;
  limitingItem: string | null;
  materialCost: number | null;
  costComplete: boolean;
  lines: number;
  linesWithoutPrice: number;
  topComponents: ProducibleComponent[];
};
export type DeviationLine = { orderNumber: string; item: string; unitCode: string; norm: number; actual: number; deviation: number; percent: number | null; overconsumed: boolean };
export type PriceChange = { name: string; unitCode: string; firstPrice: number; lastPrice: number; changePercent: number | null };
export type DaysOfStock = { sku: string; name: string; unitCode: string; onHand: number; perDay: number; days: number };

export type ProductionBlock = Block & {
  period: Period;
  orders: ProductionOrderRow[];
  producible: ProducibleRow[];
  deviations: DeviationLine[];
  prices: PriceChange[];
  lowDays: DaysOfStock[];
};

export type HorizonNeed = { days: number; demand: number; safetyStock: number; incoming: number; required: number; cost: number | null };
export type Risk = "critical" | "high" | "medium" | "low" | "none";

export type SupplyRecommendation = {
  itemId: string;
  sku: string;
  name: string;
  unit: string;
  onHand: number;
  reserved: number;
  unusable: number;
  available: number;
  minQty: number;
  avgDailyConsumption: number;
  historyDays: number;
  activeDays: number;
  safetyStock: number;
  daysOfCover: number | null;
  shortageDate: string | null;
  orderBy: string | null;
  orderNow: boolean;
  leadTimeDays: number;
  leadTimeAssumed: boolean;
  leadTimeSource: string;
  unitPrice: number | null;
  priceSource: string | null;
  supplier: string | null;
  incomingTotal: number;
  committedTotal: number;
  horizons: HorizonNeed[];
  risk: Risk;
  deferrable: boolean;
  reasons: string[];
  assumptions: string[];
};

export type WarehouseItem = {
  itemId: string;
  sku: string;
  name: string;
  category: string;
  unit: string;
  onHand: number;
  reserved: number;
  unusable: number;
  available: number;
  minQty: number;
  avgDailyConsumption: number;
  consumed30: number;
  daysOfCover: number | null;
  status: WarehouseStatus;
  unitPrice: number | null;
  value: number | null;
  lastMovement: string | null;
};

export type WarehouseStatus = "out" | "deficit" | "below_min" | "excess" | "dead" | "ok" | "empty";

export type SupplyPlan = {
  asOf: string;
  items: SupplyRecommendation[];
  horizons: { days: number; items: number; budget: number; itemsWithoutPrice: number }[];
  risks: Partial<Record<Risk, number>>;
  suppliers: { supplier: string; items: number; budget30: number; critical: number }[];
  warehouse: WarehouseItem[];
  warehouseStatus: Partial<Record<WarehouseStatus, number>>;
  stockValue: number;
  itemsWithoutPrice: number;
  dataNotes: string[];
  sources: string[];
};

export type Severity = "critical" | "warning" | "info";

export type Insight = {
  id: string;
  kind: "risk" | "anomaly" | "quality" | "opportunity";
  module: string;
  rule: string;
  severity: Severity;
  title: string;
  description: string;
  cause: string | null;
  causeConfirmed: boolean;
  hypotheses: string[];
  impact: number | null;
  impactUnit: string | null;
  consequences: string | null;
  recommendations: string[];
  evidence: unknown;
  href: string | null;
  detectedAt: string;
  lastSeenAt: string;
  resolvedAt: string | null;
  occurrences: number;
  source: "analytik" | "proactive";
};

export type ForecastPoint = { date: string; value: number; lower: number; upper: number };
export type SeriesPoint = { date: string; value: number };

export type ForecastView = {
  metric: string;
  title: string;
  unit: string;
  granularity: "day" | "week" | "month";
  history: SeriesPoint[];
  result: {
    granularity: string;
    method: string;
    points: ForecastPoint[];
    total: number;
    totalLower: number;
    totalUpper: number;
    wape: number | null;
    mape: number | null;
    holdoutPoints: number;
    methods: { method: string; wape: number | null; mape: number | null }[];
    history: number;
    seasonality: number;
    confidence: number;
    reliable: boolean;
    assumptions: string[];
  };
  past: { evaluated: number; wape: number | null; from: string | null; to: string | null } | null;
  scope: string | null;
  sources: string[];
};

export type Health = {
  score: number;
  level: "good" | "attention" | "critical";
  components: { title: string; points: number; reason: string }[];
  /** Не null — пользователь видит не всю компанию: оценка только по его разделам и области продаж. */
  coverage: string | null;
};

export type Dashboard = {
  period: Period;
  at: string;
  health: Health;
  sales: SalesBlock | null;
  finance: (Block & { payments: { type: string; amount: number; count: number; previousAmount: number | null }[] }) | null;
  production: ProductionBlock | null;
  supply: {
    risks: Partial<Record<Risk, number>>;
    horizons: SupplyPlan["horizons"];
    top: SupplyRecommendation[];
    warehouseStatus: Partial<Record<WarehouseStatus, number>>;
    stockValue: number;
    itemsWithoutPrice: number;
    notes: string[];
  } | null;
  hr: (Block & { divisions: HrDivision[] }) | null;
  work: (Block & { departments: WorkDepartment[] }) | null;
  risks: Insight[];
  recommendations: string[];
  forecast: { metric: string; title: string; unit: string; total: number; lower: number; upper: number; steps: number; method: string; reliable: boolean; points: ForecastPoint[] } | null;
  freshness: FreshnessRow[];
  recent: { id: string; title: string; at: string }[];
  scope: string | null;
  errors: string[];
  gaps: DataGap[];
  /** Активные находки по критичности (на панели — только первые 12, счётчики — по всем). Нет — старый сервер. */
  riskCounts?: RiskCounts | null;
};

export type RiskCounts = { critical: number; warning: number; info: number; total: number };

/** Состояние набора данных: свежие, обновляются, устарели, ошибка источника, интеграция не подключена, планы не заведены. */
export type ReadinessStatus = "ok" | "updating" | "stale" | "error" | "not_connected" | "missing";

/**
 * Свежесть набора данных: code — набор (sales, payments, stock, plans, hr, onebase, catalog, risks), origin — откуда (linko, verifix, onebase),
 * note — почему отстаёт, action — что система уже сделала, canRefresh — можно запустить обновление, href — настройки подключения.
 */
export type FreshnessRow = {
  source: string;
  at: string | null;
  status: ReadinessStatus;
  note: string | null;
  action: string | null;
  code: string | null;
  origin: string | null;
  canRefresh: boolean;
  href: string | null;
};

/** Нехватка данных в выбранном периоде: что не загружено и почему, что уже сделано, где исправить, какой месяц показать вместо. */
export type DataGap = {
  source: string;
  text: string;
  action: string | null;
  href: string | null;
  latest: { label: string; from: string; to: string } | null;
};

export type Readiness = { rows: FreshnessRow[]; gaps: DataGap[] };

/** Подпись статуса свежести (ok — без подписи). */
export function readinessLabel(status: ReadinessStatus): string | null {
  switch (status) {
    case "updating":
      return "обновляется";
    case "stale":
      return "устарели";
    case "error":
      return "ошибка обмена";
    case "not_connected":
      return "не подключено";
    case "missing":
      return "не заведены";
    default:
      return null;
  }
}

/** Название источника данных для кнопки и подписи. */
export function originLabel(origin: string | null): string {
  return origin === "linko" ? "Linko" : origin === "verifix" ? "Verifix" : "OneBase";
}

export type HrDivision = { division: string; late: number; lateMinutes: number; earlyLeave: number; absent: number; employees: number };
export type WorkDepartment = { department: string; open: number; overdue: number; doneInPeriod: number; blocked: number; onTimePercent: number | null };

export type CatalogTableRow = {
  schema: string;
  name: string;
  fullName: string;
  module: string;
  title: string | null;
  description: string | null;
  descriptionSource: "knowledge" | "manual" | "auto";
  rows: number;
  rowsExact: boolean;
  sizeBytes: number;
  columns: number;
  access: "open" | "personal" | "closed";
  requiredPermission: string | null;
  dateColumn: string | null;
  available: boolean;
  unavailable: string | null;
  relations: number;
  firstSeenAt: string;
  changedAt: string | null;
};

export type CatalogColumnRow = {
  name: string;
  dataType: string;
  nullable: boolean;
  primaryKey: boolean;
  indexed: boolean;
  unique: boolean;
  role: string;
  title: string | null;
  description: string | null;
  sensitivity: "none" | "personal" | "secret" | "internal";
};

export type CatalogRelationRow = {
  direction: "in" | "out";
  from: string;
  fromColumn: string;
  to: string;
  toColumn: string;
  kind: "fk" | "inferred" | "manual";
  status: "confirmed" | "unverified" | "rejected";
  matchRatio: number | null;
  checked: number | null;
  targetFilter: string | null;
  otherTitle: string | null;
};

export type MetricKnowledge = { code: string; title: string; module: string; definition: string; authoritativeTool: string; tables: string };

export type CatalogTableDetail = { table: CatalogTableRow; columns: CatalogColumnRow[]; relations: CatalogRelationRow[]; metrics: MetricKnowledge[]; scopeRule: string | null };

export type CatalogGraph = {
  modules: { module: string; tables: number; rows: number; available: number }[];
  edges: { from: string; to: string; relations: number }[];
  tables: number;
  relations: number;
  confirmedRelations: number;
  refreshedAt: string | null;
};

export type AnalytikStatus = {
  catalogRefreshedAt: string | null;
  tables: number;
  relations: number;
  confirmedRelations: number;
  availableTables: number;
  dedicatedReadOnlyRole: boolean;
  manager: boolean;
  salesScope: string;
};

export type OutputColumn = { name: string; kind: string; source: string; role: string; dataType: string };

export type QueryOutcome = {
  queryId: string;
  status: "ok" | "denied" | "invalid" | "too_heavy" | "timeout" | "error";
  error: string | null;
  result: {
    queryId: string;
    columns: OutputColumn[];
    rows: unknown[][];
    hasMore: boolean;
    offset: number;
    elapsedMs: number;
    tables: string[];
    scope: string | null;
    notes: string[];
    sql: string;
    at: string;
  } | null;
};

export type QueryLogRow = {
  id: string;
  createdAt: string;
  userId: string | null;
  origin: string;
  agentCode: string | null;
  status: string;
  tables: string | null;
  scope: string | null;
  rowCount: number;
  truncated: boolean;
  durationMs: number;
  error: string | null;
  plan: string;
  sql: string | null;
};

export type ReportListRow = { id: string; kind: string; title: string; from: string; to: string; createdAt: string; scheduled: boolean };

export type ReportTable = { title: string; columns: { header: string; format: string; sum: boolean }[]; rows: unknown[][]; note: string | null };
export type ReportSection = { key: string; title: string; module: string; requiredPermission: string | null; salesScoped: boolean; kpis: Kpi[]; tables: ReportTable[]; notes: string[]; sources: string[] };
export type ReportDoc = { title: string; kind: string; periodLabel: string; from: string; to: string; generatedAt: string; summary: string; sections: ReportSection[] };

export const reportKinds: { kind: string; label: string }[] = [
  { kind: "daily", label: "Ежедневный управленческий" },
  { kind: "weekly", label: "Еженедельный" },
  { kind: "monthly", label: "Ежемесячный управленческий" },
  { kind: "health", label: "Состояние компании" },
  { kind: "sales", label: "Продажи" },
  { kind: "production", label: "Производство" },
  { kind: "inventory", label: "Склад сырья" },
  { kind: "procurement", label: "Снабжение" },
  { kind: "finance", label: "Финансы" },
  { kind: "hr", label: "Персонал" },
];

// ---------- оформление ----------

const nbsp = " ";

/** Число с разрядами (пробел) и до digits знаков после запятой; null — «—». */
export function num(value: number | null | undefined, digits = 0): string {
  if (value == null || !Number.isFinite(value)) return "—";
  const fixed = Math.abs(value).toFixed(digits);
  const [int, frac] = fixed.split(".");
  const grouped = int.replace(/\B(?=(\d{3})+(?!\d))/g, nbsp);
  const trimmed = frac ? frac.replace(/0+$/, "") : "";
  return `${value < 0 ? "−" : ""}${grouped}${trimmed ? `,${trimmed}` : ""}`;
}

/** Крупные суммы — «12,4 млрд», «845 млн», «12,3 тыс.»; меньше тысячи — как есть. */
export function compact(value: number | null | undefined): string {
  if (value == null || !Number.isFinite(value)) return "—";
  const abs = Math.abs(value);
  const sign = value < 0 ? "−" : "";
  if (abs >= 1e9) return `${sign}${num(abs / 1e9, abs >= 1e10 ? 1 : 2)}${nbsp}млрд`;
  if (abs >= 1e6) return `${sign}${num(abs / 1e6, abs >= 1e7 ? 0 : 1)}${nbsp}млн`;
  if (abs >= 1e4) return `${sign}${num(abs / 1e3, 0)}${nbsp}тыс.`;
  return `${sign}${num(abs, abs < 10 ? 2 : 0)}`;
}

/** Значение показателя с единицей: суммы — компактно, проценты — с одним знаком. */
export function kpiValue(value: number | null | undefined, unit: string): string {
  if (value == null) return "—";
  if (unit === "%") return `${num(value, 1)}${nbsp}%`;
  if (unit === "сум") return `${compact(value)}${nbsp}сум`;
  if (unit === "кг") return `${Math.abs(value) >= 1e4 ? compact(value) : num(value, 1)}${nbsp}кг`;
  return `${num(value, Number.isInteger(value) ? 0 : 1)}${unit ? `${nbsp}${unit}` : ""}`;
}

/** «+12,3 %» / «−4 %» / null. */
export function change(percent: number | null | undefined): string | null {
  if (percent == null || !Number.isFinite(percent)) return null;
  return `${percent > 0 ? "+" : percent < 0 ? "−" : ""}${num(Math.abs(percent), Math.abs(percent) < 10 ? 1 : 0)}${nbsp}%`;
}

/**
 * Тон изменения показателя: для «хороших» показателей рост — зелёный; для «плохих» (возвраты, опоздания, отсутствия, перерасход, брак,
 * просрочки) — наоборот. Без изменения — нейтральный.
 */
export function changeTone(code: string, percent: number | null | undefined): "ok" | "bad" | "muted" {
  if (percent == null || Math.abs(percent) < 0.05) return "muted";
  const inverse = /returns|late|absent|early|overconsumption|defect|overdue/.test(code);
  return (percent > 0) !== inverse ? "ok" : "bad";
}

export const kindLabel: Record<KpiKind, string> = { fact: "факт", calculated: "расчёт", forecast: "прогноз" };

export const severityLabel: Record<Severity, string> = { critical: "Критично", warning: "Внимание", info: "К сведению" };

export const riskLabel: Record<Risk, string> = { critical: "Критично", high: "Высокий", medium: "Средний", low: "Низкий", none: "Нет" };

export const warehouseStatusLabel: Record<WarehouseStatus, string> = {
  out: "Закончилось",
  deficit: "Дефицит",
  below_min: "Ниже минимума",
  excess: "Излишек",
  dead: "Без расхода 90 дней",
  ok: "Норма",
  empty: "Нет на складе",
};

export const moduleLabel: Record<string, string> = {
  sales: "Продажи",
  finance: "Финансы",
  field: "Sales Base",
  procurement: "Снабжение и склад",
  production: "Производство",
  hr: "Персонал",
  work: "Задачи",
  telegram: "Telegram",
  ai: "AI",
  system: "Система",
  twin: "Цифровой двойник",
  analytik: "Аналитик",
  other: "Другое",
};

export const roleLabel: Record<string, string> = {
  id: "ключ",
  reference: "ссылка",
  date: "дата",
  datetime: "дата и время",
  money: "сумма",
  quantity: "количество",
  weight: "вес",
  percent: "процент",
  count: "число",
  flag: "да/нет",
  status: "статус",
  name: "название",
  code: "код",
  text: "текст",
  json: "JSON",
  number: "число",
  other: "другое",
};

/** Порядок критичности для сортировки: критично → внимание → к сведению. */
export function severityRank(s: string): number {
  return s === "critical" ? 0 : s === "warning" || s === "high" ? 1 : s === "medium" ? 2 : 3;
}

/** Дата «2026-11-11» → «11.11.2026»; время ISO → «11.11.2026 16:30» (Ташкент). */
export function date(iso: string | null | undefined): string {
  if (!iso) return "—";
  const [y, m, d] = iso.slice(0, 10).split("-");
  return d && m && y ? `${d}.${m}.${y}` : iso;
}

export function dateTime(iso: string | null | undefined): string {
  if (!iso) return "—";
  const t = new Date(iso);
  if (Number.isNaN(t.getTime())) return iso;
  const local = new Date(t.getTime() + 5 * 3600_000);
  const p = (n: number) => String(n).padStart(2, "0");
  return `${p(local.getUTCDate())}.${p(local.getUTCMonth() + 1)}.${local.getUTCFullYear()} ${p(local.getUTCHours())}:${p(local.getUTCMinutes())}`;
}

/** Размер таблицы: «12,4 МБ». */
export function bytes(value: number): string {
  if (value >= 1 << 30) return `${num(value / (1 << 30), 1)}${nbsp}ГБ`;
  if (value >= 1 << 20) return `${num(value / (1 << 20), 1)}${nbsp}МБ`;
  if (value >= 1 << 10) return `${num(value / (1 << 10), 0)}${nbsp}КБ`;
  return `${value}${nbsp}Б`;
}

/** Строка запроса периода: ?period=month или ?period=custom&from=…&to=…; плюс прочие параметры. */
export function periodQuery(kind: string | undefined, from?: string, to?: string, extra: Record<string, string | undefined> = {}): string {
  const params = new URLSearchParams();
  params.set("period", kind || "month");
  if (kind === "custom") {
    if (from) params.set("from", from);
    if (to) params.set("to", to);
  }
  for (const [k, v] of Object.entries(extra)) if (v) params.set(k, v);
  return `?${params.toString()}`;
}

/** Точки ломаной SVG по значениям: ширина width, высота height, отступ pad; min и max — общие для нескольких линий. */
export function polyline(values: number[], width: number, height: number, min: number, max: number, pad = 4): string {
  if (values.length === 0) return "";
  const span = max - min || 1;
  const step = values.length > 1 ? (width - pad * 2) / (values.length - 1) : 0;
  return values.map((v, i) => `${(pad + i * step).toFixed(1)},${(height - pad - ((v - min) / span) * (height - pad * 2)).toFixed(1)}`).join(" ");
}

/** Быстрые вопросы AI-аналитику — по правам пользователя. */
/** Режим общего экрана чата для «Аналитика»: свои чаты (api/analytik), свои вопросы, без Telegram-бота консультанта. */
export function analytikChatMode(permissions: readonly string[]): import("./consultant").ChatMode {
  return {
    base: "/analytik/chat",
    api: "analytik",
    title: "AI-аналитик",
    questions: analytikQuestions(permissions),
    offline: { title: "Аналитик пока не работает", input: "AI-аналитик не настроен" },
    home: { href: "/analytik", label: "Обзор компании" },
    telegram: false,
  };
}

export function analytikQuestions(permissions: readonly string[]): { text: string; icon: "trending-up" | "target" | "trending-down" | "compare" | "package" | "user-x" | "wallet" | "lightbulb"; tone: string }[] {
  const has = (...p: string[]) => p.some((x) => permissions.includes(x));
  const all = [
    { text: "Проанализируй всю компанию за текущий месяц", icon: "lightbulb" as const, tone: "accent", ok: true },
    { text: "Какие проблемы сейчас требуют вмешательства директора?", icon: "target" as const, tone: "danger", ok: true },
    { text: "Почему снизились продажи?", icon: "trending-down" as const, tone: "danger", ok: has("sales.read") },
    { text: "Сколько сырья необходимо закупить на следующие 30 дней?", icon: "package" as const, tone: "warning", ok: has("procurement.read", "procurement.manage", "procurement.warehouse", "production.read") },
    { text: "Какой объём продукции можно произвести из текущих остатков?", icon: "package" as const, tone: "accent", ok: has("production.read", "production.manage", "procurement.read") },
    { text: "Сравни продажи, производство и расходы за последние три месяца", icon: "compare" as const, tone: "accent", ok: has("sales.read") },
    { text: "Какие отделы не выполняют свои показатели?", icon: "user-x" as const, tone: "warning", ok: true },
    { text: "Найди все риски, которые могут привести к финансовым потерям", icon: "wallet" as const, tone: "danger", ok: true },
  ];
  return all.filter((q) => q.ok).slice(0, 6).map(({ text, icon, tone }) => ({ text, icon, tone }));
}

// ---------- «Обзор» (редизайн по макету «Аналитик · Обзор») ----------

/** Форма слова по числу: 1 позиция, 2 позиции, 5 позиций. */
export function plural(n: number, one: string, few: string, many: string): string {
  const a = Math.abs(n) % 100;
  const b = a % 10;
  return a >= 11 && a <= 14 ? many : b === 1 ? one : b >= 2 && b <= 4 ? few : many;
}

/**
 * Значение показателя и единица раздельно — крупное число и мелкая подпись по макету: «4,69» + «млрд сум», «124» + «тыс. кг», «24,3» + «%»,
 * «1» + «позиция». Нет значения — null («Нет данных» с причиной, а не ноль).
 */
export function kpiParts(value: number | null | undefined, unit: string): { value: string; unit: string } | null {
  if (value == null || !Number.isFinite(value)) return null;
  const split = (text: string) => {
    const i = text.lastIndexOf(nbsp);
    return i < 0 ? { value: text, unit: "" } : { value: text.slice(0, i), unit: text.slice(i + 1) };
  };
  if (unit === "%") return { value: num(value, 1), unit: "%" };
  if (unit === "сум") return Math.abs(value) >= 1e4 ? (({ value: v, unit: u }) => ({ value: v, unit: `${u}${nbsp}сум` }))(split(compact(value))) : { value: num(value), unit: "сум" };
  if (unit === "кг") return Math.abs(value) >= 1e4 ? (({ value: v, unit: u }) => ({ value: v, unit: `${u}${nbsp}кг` }))(split(compact(value))) : { value: num(value, 1), unit: "кг" };
  if (unit === "позиций") return { value: num(value), unit: plural(value, "позиция", "позиции", "позиций") };
  return { value: num(value, Number.isInteger(value) ? 0 : 1), unit };
}

/** Сколько дней месяца плана прошло (по дню данных): «прошло 12 из 30 дней», «прошло 40% месяца». */
export function planDays(plan: Pick<SalesMonthPlan, "year" | "month" | "dataThrough">): { day: number; days: number; percent: number } {
  const days = new Date(Date.UTC(plan.year, plan.month, 0)).getUTCDate();
  const [y, m, d] = (plan.dataThrough ?? "").slice(0, 10).split("-").map(Number);
  const day = y === plan.year && m === plan.month && d ? Math.min(d, days) : y && (y > plan.year || (y === plan.year && m > plan.month)) ? days : 0;
  return { day, days, percent: Math.round((day / days) * 1000) / 10 };
}

/** Тон прогноза выполнения плана — по тем же порогам, что снижают «Состояние компании»: от 95 % — норма, 80–95 % — внимание, ниже — плохо. */
export function planTone(percent: number | null | undefined): "ok" | "warn" | "bad" | "muted" {
  if (percent == null || !Number.isFinite(percent)) return "muted";
  return percent >= 95 ? "ok" : percent >= 80 ? "warn" : "bad";
}

/** Тон запаса сырья в днях: меньше 14 дней — внимание, от 14 — норма; нет данных — нейтральный. */
export function stockTone(days: number | null | undefined): "ok" | "warn" | "muted" {
  if (days == null || !Number.isFinite(days)) return "muted";
  return days < 14 ? "warn" : "ok";
}

export type HealthFactor = { label: string; sub: string; penalty: number; severity: "danger" | "warning"; share: number; href: string };

/** Что снижает оценку: штраф положительным числом, доля полосы — от самого большого штрафа, куда ведёт строка. */
export function healthFactors(health: Pick<Health, "components">): HealthFactor[] {
  const max = Math.max(1, ...health.components.map((c) => Math.abs(c.points)));
  const hrefOf = (title: string) =>
    /риск|предупрежд/i.test(title) ? "/analytik/risks" : /план продаж/i.test(title) ? "/analytik/sales" : /сырь/i.test(title) ? "/analytik/procurement" : /производ/i.test(title) ? "/analytik/production" : "/analytik/risks";
  return health.components.map((c) => ({
    label: c.title,
    sub: c.reason,
    penalty: Math.abs(c.points),
    severity: /критическ/i.test(c.title) ? "danger" : "warning",
    share: Math.abs(c.points) / max,
    href: hrefOf(c.title),
  }));
}

/** «Главное влияние — критические риски и план продаж»: два самых больших снижения (названия переводит экран); без снижений — пусто. */
export function healthLead(health: Pick<Health, "components">): string[] {
  return [...health.components].sort((a, b) => a.points - b.points).slice(0, 2).map((c) => c.title);
}

/** Точность прогноза: 100 % минус ошибка — сверенная с фактом (прошлые прогнозы), иначе на отложенной истории; нет — null. */
export function forecastAccuracy(f: Pick<ForecastView, "past" | "result">): number | null {
  const wape = f.past?.wape ?? f.result.wape;
  return wape == null || !Number.isFinite(wape) ? null : Math.max(0, Math.round((1 - wape) * 100));
}

/**
 * Время находки коротко (Ташкент): day — «today» / «yesterday» (слово переводит экран) и время «12:16», иначе «09.11 00:36», другой год —
 * «09.11.2025».
 */
export function shortWhen(iso: string | null | undefined, now: Date = new Date()): { day: "today" | "yesterday" | null; text: string } {
  if (!iso) return { day: null, text: "—" };
  const t = new Date(iso);
  if (Number.isNaN(t.getTime())) return { day: null, text: iso };
  const shift = (d: Date) => new Date(d.getTime() + 5 * 3600_000);
  const a = shift(t);
  const b = shift(now);
  const p = (n: number) => String(n).padStart(2, "0");
  const time = `${p(a.getUTCHours())}:${p(a.getUTCMinutes())}`;
  const dayKey = (d: Date) => Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate());
  const diff = Math.round((dayKey(b) - dayKey(a)) / 86_400_000);
  if (diff === 0) return { day: "today", text: time };
  if (diff === 1) return { day: "yesterday", text: time };
  const day = `${p(a.getUTCDate())}.${p(a.getUTCMonth() + 1)}`;
  return { day: null, text: a.getUTCFullYear() === b.getUTCFullYear() ? `${day} ${time}` : `${day}.${a.getUTCFullYear()}` };
}

/** «1.11» — день и месяц без нулей впереди (подписи «Факт с 1.11»). */
export function dayMonth(iso: string | null | undefined): string {
  if (!iso) return "—";
  const [, m, d] = iso.slice(0, 10).split("-").map(Number);
  return d && m ? `${d}.${String(m).padStart(2, "0")}` : iso;
}

/** «12.11» — короткая дата оси графика. */
export function shortDate(iso: string | null | undefined): string {
  if (!iso) return "—";
  const [, m, d] = iso.slice(0, 10).split("-");
  return d && m ? `${d}.${m}` : iso;
}

/** Ровные деления оси: от 0 до «круглого» максимума, count делений (для 1 234 — 0, 400, 800, 1 200, 1 600). */
export function niceTicks(max: number, count = 4): number[] {
  if (!Number.isFinite(max) || max <= 0) return [0, 1];
  const raw = max / count;
  const mag = 10 ** Math.floor(Math.log10(raw));
  const step = [1, 2, 2.5, 4, 5, 10].map((k) => k * mag).find((s) => s >= raw) ?? 10 * mag;
  return Array.from({ length: Math.ceil(max / step) + 1 }, (_, i) => i * step);
}

/** Сырьё в карточке «Что закупить»: сколько купить на 30 дней, сумма (нет цены — null), дни запаса. */
export function purchaseOf(r: Pick<SupplyRecommendation, "horizons" | "daysOfCover" | "unitPrice">): { qty: number; sum: number | null; days: number | null } {
  const need = r.horizons.find((h) => h.days === 30) ?? r.horizons[r.horizons.length - 1];
  const qty = need?.required ?? 0;
  return { qty, sum: need?.cost ?? (r.unitPrice != null ? Math.round(qty * r.unitPrice) : null), days: r.daysOfCover };
}

export type OverviewRecommendation = {
  id: string;
  title: string;
  reason: string;
  module: string;
  severity: Severity;
  impact: string | null;
  href: string | null;
  /** Сырьё находки «запас закончится» — для заявки в снабжение с заполненной позицией. */
  sku: string | null;
};

/** Рекомендации AI-аналитика: из найденных рисков с рекомендацией (кроме «к сведению»), не больше limit. */
export function overviewRecommendations(risks: Insight[], limit = 3): OverviewRecommendation[] {
  return risks
    .filter((r) => r.recommendations.length > 0 && r.severity !== "info")
    .slice(0, limit)
    .map((r) => {
      const evidence = (r.evidence ?? {}) as { sku?: unknown; Sku?: unknown };
      const sku = typeof evidence.sku === "string" ? evidence.sku : typeof evidence.Sku === "string" ? evidence.Sku : null;
      return {
        id: r.id,
        title: r.recommendations[0],
        reason: r.title,
        module: r.module,
        severity: r.severity,
        impact:
          r.impact == null
            ? null
            : r.impactUnit === "сум"
              ? `${compact(r.impact)}${nbsp}сум`
              : `${num(r.impact, Math.abs(r.impact) < 10 ? 1 : 0)}${r.impactUnit ? `${nbsp}${r.impactUnit}` : ""}`,
        href: r.href,
        sku: r.rule === "supply.stockout" ? sku : null,
      };
    });
}

/**
 * Адрес новой заявки на сырьё с заполненной позицией, количеством на 30 дней (вверх до сотых), ценой, сроком «заказать до» и причиной
 * (форма /requests/new?…; всё можно поправить перед отправкой).
 */
export function rawRequestHref(r: Pick<SupplyRecommendation, "itemId" | "name" | "unit" | "orderBy" | "unitPrice" | "horizons" | "daysOfCover">, reason: string): string {
  const { qty } = purchaseOf(r);
  const params = new URLSearchParams({ kind: "raw", item: r.itemId, name: r.name, unit: r.unit, qty: String(Math.ceil(qty * 100) / 100) });
  if (r.unitPrice != null) params.set("price", String(r.unitPrice));
  if (r.orderBy) params.set("by", r.orderBy.slice(0, 10));
  params.set("reason", reason);
  return `/requests/new?${params.toString()}`;
}

/** Диапазон сумм: одна единица — один раз («10,1 – 18,4 млрд»), разные — у каждой («950 млн – 1,2 млрд»). */
export function compactRange(lo: number | null | undefined, hi: number | null | undefined): string {
  const a = compact(lo);
  const b = compact(hi);
  const unit = (s: string) => {
    const i = s.lastIndexOf(nbsp);
    return i >= 0 && /^\D/.test(s.slice(i + 1)) ? { head: s.slice(0, i), unit: s.slice(i + 1) } : { head: s, unit: "" };
  };
  const ua = unit(a);
  const ub = unit(b);
  return ua.unit && ua.unit === ub.unit ? `${ua.head} – ${b}` : `${a} – ${b}`;
}
