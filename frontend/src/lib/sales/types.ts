// Типы ответов /api/sales/* (зеркало OneBase.Application.Sales.Metrics.Results).
// Доли — от 0 до 1; null — нет данных («—»).

export type TargetLevel = "Good" | "Warning" | "Bad";
export type FlagSeverity = "Critical" | "Risk" | "Info";
export type FlagKind =
  | "LowConversion"
  | "VisitsNoSales"
  | "SmallCheck"
  | "NarrowAssortment"
  | "TempoDrop"
  | "DataMismatch"
  | "LowData";

export type Period = {
  year: number;
  month: number;
  dataThrough: string;
  workedDays: number;
  daysInMonth: number;
  previousCutoff: string;
};

export type TargetValue = { value: number | null; target: number; ratio: number | null; level: TargetLevel | null };

export type KpiTiles = {
  factKg: number;
  planKg: number | null;
  execution: number | null;
  forecastKg: number | null;
  forecastExecution: number | null;
  revenue: number;
  akb: number;
  conversion: TargetValue;
  revenuePerOutlet: TargetValue;
  akbPerAgent: TargetValue;
  visitsWithoutOrder: number;
  visitsDone: number;
  activeAgents: number;
  revenuePlan: RevenuePlanTile | null;
  /** Факт, с которым сравнивается план: ТП с планом (или весь регион, если у него ручной план). */
  planFactKg: number | null;
  planForecastKg: number | null;
  planAgents: number;
};

/** План по выручке: только агенты с планом в Linko; fact — их выручка. */
export type RevenuePlanTile = {
  plan: number;
  fact: number;
  execution: number | null;
  forecast: number | null;
  forecastExecution: number | null;
  agents: number;
};

export type IndicatorPlan = { indicatorId: number; name: string; planType: string; plan: number; fact: number; execution: number | null };

export type PlanPersonRow = {
  agentId: number;
  name: string;
  job: string | null;
  regionId: string | null;
  regionName: string | null;
  isTeamPlan: boolean;
  weightPlan: number | null;
  weightFact: number;
  weightExecution: number | null;
  revenuePlan: number | null;
  revenueFact: number;
  akbPlan: number | null;
  akbFact: number;
  indicators: IndicatorPlan[];
};

export type PlansView = {
  period: Period;
  weightPlan: number | null;
  weightFact: number;
  weightExecution: number | null;
  revenuePlan: number | null;
  revenueFact: number;
  agentsWithPlan: number;
  indicators: number;
  regions: { id: string; name: string; agents: number; weightPlan: number | null; weightFact: number; weightExecution: number | null; revenuePlan: number | null; revenueFact: number }[];
  agents: PlanPersonRow[];
  teamPlans: PlanPersonRow[];
};

export type FlagCounts = { critical: number; risk: number };

export type UnitRow = {
  id: string;
  name: string;
  subtitle: string | null;
  planKg: number | null;
  factKg: number;
  execution: number | null;
  forecastKg: number | null;
  forecastExecution: number | null;
  revenue: number;
  akb: number;
  strike: number | null;
  visitsWithoutOrder: number;
  agents: number;
  regionCount: number;
  regionNames: string[];
  flags: FlagCounts;
  planFactKg: number | null;
  kind: "republic" | "direction" | "region";
};

export type VisitCalendarRow = {
  id: string;
  name: string;
  subtitle: string | null;
  plan: number;
  doneInPlan: number;
  doneOffPlan: number;
  doneAll: number;
  ordersInPlan: number;
  ordersInPlanSum: number;
  ordersOffPlan: number;
  ordersOffPlanSum: number;
  ordersTotal: number;
  ordersTotalSum: number;
  notVisited: number;
  planShare: number | null;
  children: VisitCalendarRow[];
};

export type SameDaysRow = {
  id: string;
  name: string;
  subtitle: string | null;
  kgBefore: number;
  kgNow: number;
  kgDelta: number | null;
  sumBefore: number;
  sumNow: number;
  sumDelta: number | null;
  akbNow: number;
  akbDelta: number | null;
};

export type SilentMarket = { marketId: number; name: string; prevKg: number; prevRevenue: number; sku: number };
export type NewMarket = { marketId: number; name: string; kg: number; revenue: number };

export type NotBoughtRow = {
  id: string;
  name: string;
  subtitle: string | null;
  base: number;
  silent: number;
  share: number | null;
  silentPrevRevenue: number;
  new: number;
  silentMarkets: SilentMarket[];
};

export type AgentFlag = { kind: FlagKind; severity: FlagSeverity; label: string; title: string; explanation: string };

export type OverviewView = {
  period: Period;
  kpi: KpiTiles;
  activeAgents: number;
  flags: FlagCounts;
  vacancies: number;
  republic: UnitRow;
  excluded: ExcludedSummary | null;
};

export type GroupView = {
  period: Period;
  name: string;
  subtitle: string | null;
  kpi: KpiTiles;
  unassigned: { kg: number; share: number | null };
  cards: UnitRow[];
  regions: UnitRow[];
  visitCalendar: VisitCalendarRow[];
  sameDays: SameDaysRow[];
  notBought: NotBoughtRow[];
  categoryCards: CategoryCard[];
  akbMonths: AkbByMonth;
  quality: DataQuality;
  excluded: ExcludedSummary | null;
};

/** АКБ по месяцам года: итог и по категориям. null — данных за месяц нет. */
export type AkbByMonth = {
  year: number;
  months: number[];
  lastPartial: boolean;
  total: (number | null)[];
  categories: { id: string; name: string; values: (number | null)[] }[];
};

export type SkuStatus = "selling" | "silent" | "lost";

export type SkuRow = {
  productId: number;
  name: string;
  code: string | null;
  factKg: number;
  revenue: number;
  akb: number;
  distribution: number | null;
  prevMonthKg: number;
  status: SkuStatus;
};

/** Карточка категории: «продаётся N из M SKU», факт, доля, АКБ, дистрибуция, прогноз, «молчат / пропало». */
export type CategoryCard = {
  id: string;
  name: string;
  skuSold: number;
  skuTotal: number;
  factKg: number;
  weightShare: number | null;
  revenue: number;
  akb: number;
  distribution: number | null;
  forecastKg: number | null;
  forecastRevenue: number | null;
  prevMonthKg: number;
  vsPrevMonth: number | null;
  silent: number;
  lost: number;
  skus: SkuRow[];
};

export type TeamRow = {
  agentId: number;
  name: string;
  planKg: number | null;
  factKg: number;
  execution: number | null;
  forecastKg: number | null;
  forecastExecution: number | null;
  revenue: number;
  visits: number;
  orders: number;
  strike: number | null;
  sumPerVisit: number | null;
  categories: number;
  isVacancy: boolean;
  inDirectory: boolean;
  flags: AgentFlag[];
};

export type MonthCalendar = {
  metric: "kg" | "sum" | "akb";
  categoryId: number | null;
  sundays: boolean[];
  rows: { id: string; name: string; days: (number | null)[]; total: number }[];
  totalDays: (number | null)[];
  total: number;
};

export type RegionView = {
  period: Period;
  id: string;
  name: string;
  directionId: string | null;
  directionName: string | null;
  supervisor: string | null;
  dealer: string | null;
  kpi: KpiTiles;
  unassigned: { kg: number; share: number | null };
  months: { month: number; planKg: number | null; factKg: number | null }[];
  categories: { categoryId: number | null; name: string; revenue: number; share: number | null }[];
  calendar: MonthCalendar;
  visitCalendar: VisitCalendarRow[];
  team: TeamRow[];
  sameDays: SameDaysRow[];
  notBought: NotBoughtRow[];
  notInDirectory: { agentId: number; name: string; kg: number; revenue: number }[];
  categoryCards: CategoryCard[];
  akbMonths: AkbByMonth;
  quality: DataQuality;
};

export type MedianValue = { value: number | null; regionMedian: number | null };

export type AgentView = {
  period: Period;
  agentId: number;
  name: string;
  regionId: string | null;
  regionName: string | null;
  directionId: string | null;
  directionName: string | null;
  isVacancy: boolean;
  planKg: number | null;
  execution: number | null;
  factKg: number;
  revenue: number;
  revenuePlan: number | null;
  revenueExecution: number | null;
  visits: number;
  visitsWithOrder: number;
  conversion: MedianValue;
  sumPerVisit: MedianValue;
  avgCheck: MedianValue;
  categories: number;
  planCategories: number | null;
  categoryTarget: number;
  tempo: number | null;
  flags: AgentFlag[];
  categoryPlan: { categoryId: number | null; name: string; planKg: number | null; factKg: number; revenue: number; execution: number | null }[];
  indicators: { indicatorId: number; name: string; planType: string; plan: number; fact: number; execution: number | null }[];
  sameDays: SameDaysRow;
  silentBase: number;
  silentPrevRevenue: number;
  silent: SilentMarket[];
  newMarkets: NewMarket[];
  assortment: AgentAssortment | null;
};

export type ProblemAgent = {
  agentId: number;
  name: string;
  regionId: string | null;
  regionName: string | null;
  directionName: string | null;
  rank: number | null;
  conversion: number | null;
  visits: number;
  revenue: number;
  isVacancy: boolean;
  flags: AgentFlag[];
};

export type ProblemsView = { period: Period; vacancies: number; agents: ProblemAgent[] };

export type SyncProgress = { mode: string; phase: string; entity: string | null; rows: number; startedAt: string };

export type SyncStatus = {
  configured: boolean;
  isRunning: boolean;
  progress: SyncProgress | null;
  dataAsOf: string | null;
  hasErrors: boolean;
  entities: { entity: string; lastRunAt: string | null; lastSuccessAt: string | null; lastRows: number; lastError: string | null }[];
};

export type SalesMonth = { year: number; month: number };

// ---------- Качество данных, «Завод», магазин, ассортимент, остатки, первичка ----------

export type UncategorizedType = { id: string; name: string; kg: number; revenue: number; orders: number };

export type DataQuality = {
  deliveredWithoutAcceptance: number;
  zeroHeaderReturns: number;
  zeroHeaderReturnsKg: number;
  returnsWithoutLines: number;
  returnsWithoutLinesHeaderKg: number;
  uncategorized: UncategorizedType[];
  acceptedInFuture: number;
  otherCurrency: CurrencyTotal[] | null;
};

/** Выручка в другой валюте: курса нет, с сумами не складывается. */
export type CurrencyTotal = { currency: string; amount: number; orders: number };

/** Филиал «Завод» (экспорт и опт) — отдельно от вторички. */
export type ExcludedSummary = {
  factKg: number;
  revenue: number;
  orders: number;
  akb: number;
  forecastKg: number | null;
  prevMonthKg: number;
  vsPrevMonth: number | null;
};

export type ProductRow = {
  productId: number;
  name: string;
  code: string | null;
  category: string;
  inReport: boolean;
  kg: number;
  revenue: number;
  share: number | null;
  akb: number;
  distribution: number | null;
};

export type AgentAssortment = {
  categories: CategoryCard[];
  stores: { marketId: number; name: string; kg: number; revenue: number; categories: number; positions: number; share: number | null }[];
  products: ProductRow[];
  lagging: {
    productId: number;
    name: string;
    category: string;
    agentAkb: number;
    agentDistribution: number | null;
    regionDistribution: number | null;
    regionRevenue: number;
  }[];
};

export type StoreView = {
  period: Period;
  marketId: number;
  name: string;
  regionId: string | null;
  regionName: string | null;
  agentId: number | null;
  agentName: string | null;
  factKg: number;
  revenue: number;
  orders: number;
  categories: number;
  positions: number;
  shareOfAgent: number | null;
  prevMonthKg: number;
  prevMonthRevenue: number;
  categoryRows: { name: string; kg: number; revenue: number; share: number | null }[];
  products: { productId: number; name: string; code: string | null; category: string; kg: number; revenue: number }[];
  agents: string[];
};

export type ExportView = {
  period: Period;
  summary: ExcludedSummary | null;
  categories: CategoryCard[];
  markets: { marketId: number; name: string; kg: number; revenue: number; orders: number; prevMonthKg: number }[];
  agents: { agentId: number | null; name: string; kg: number; revenue: number; markets: number }[];
  products: ProductRow[];
  otherCurrency: CurrencyTotal[];
};

export type MatrixLevel = "none" | "low" | "ok";

export type AssortmentView = {
  period: Period;
  scopeName: string;
  categories: CategoryCard[];
  akbMonths: AkbByMonth;
  regions: { id: string; name: string; kg: number; revenue: number; skuSelling: number; skuNotCarried: number; skuLost: number; akb: number }[];
  products: ProductRow[];
  matrixRegions: { id: string; name: string }[];
  matrix: {
    productId: number;
    name: string;
    category: string;
    revenue: number;
    averageDistribution: number | null;
    cells: { regionId: string; distribution: number | null; level: MatrixLevel }[];
  }[];
  quality: DataQuality;
};

export type StockStatus = "deficit" | "overstock" | "dead" | "ok" | "unknown";

export type StockCell = { pieces: number; kg: number | null; kgPerDay: number | null; daysOfCover: number | null };

export type StockItem = {
  productId: number;
  name: string;
  code: string | null;
  category: string;
  inReport: boolean;
  unitKg: number | null;
  unitKgSource: "orders" | "none";
  boxKg: number | null;
  boxNote: string;
  pieces: number;
  kg: number | null;
  boxes: number | null;
  kgPerDay: number | null;
  daysOfCover: number | null;
  need15Kg: number | null;
  status: StockStatus;
  regions: Record<string, StockCell>;
  factory: StockCell | null;
};

export type StockTotals = {
  kg: number | null;
  boxes: number | null;
  kgPerDay: number | null;
  daysOfCover: number | null;
  deficit: number;
  overstock: number;
  dead: number;
  withoutWeight: number;
};

export type StockView = {
  syncedAt: string | null;
  velocityDays: number;
  velocityFrom: string;
  velocityTo: string;
  regions: { id: string; name: string; stockId: number }[];
  factory: { id: string; name: string; stockId: number } | null;
  items: StockItem[];
  otherStocks: { stockId: number; name: string; pieces: number; kg: number | null; items: number }[];
  totals: StockTotals;
  factoryTotals: StockTotals | null;
};

export type PrimaryView = {
  year: number;
  month: number;
  dataThrough: string | null;
  daysInMonth: number;
  syncedAt: string | null;
  factoryStock: string | null;
  kg: number;
  sum: number;
  shipments: number;
  dealers: number;
  shipmentDays: number;
  forecastKg: number | null;
  prevMonthKg: number;
  toFactoryKg: number;
  toFactoryTransfers: number;
  toExportKg: number;
  exportStock: string | null;
  monthsAvailable: number[];
  months: { month: number; kg: number; sum: number; toFactoryKg: number; toExportKg: number }[];
  dealerRows: {
    stockId: number;
    name: string;
    regionId: string | null;
    kg: number;
    sum: number;
    shipments: number;
    share: number | null;
    days: (number | null)[];
    months: number[];
  }[];
  categories: { name: string; inReport: boolean; kg: number; sum: number; share: number | null; months: number[] }[];
  items: { productId: number; name: string; code: string | null; category: string; pieces: number; kg: number; sum: number; sumPerKg: number | null }[];
};