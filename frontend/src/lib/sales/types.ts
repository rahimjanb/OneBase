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
  categoryPlans?: CategoryPlanFact[] | null;
  nextMonth?: NextMonthPlan | null;
};

/** АКБ по месяцам года: итог и по категориям. null — данных за месяц нет. */
export type AkbByMonth = {
  year: number;
  months: number[];
  lastPartial: boolean;
  total: (number | null)[];
  categories: { id: string; name: string; values: (number | null)[] }[];
};

/** elsewhere — «не возят»: здесь ноль, а по республике в этом месяце идёт. */
export type SkuStatus = "selling" | "silent" | "lost" | "elsewhere";

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
  /** ТП по должности Linko (Sales:SalesRepJobs) или по оргструктуре; иначе — оператор, супервайзер и т.п. */
  isSalesRep: boolean;
  job: string | null;
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
  categoryPlans?: CategoryPlanFact[] | null;
  nextMonth?: NextMonthPlan | null;
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
  categoryPlan: CategoryPlanFact[];
  indicators: { indicatorId: number; name: string; planType: string; plan: number; fact: number; execution: number | null }[];
  sameDays: SameDaysRow;
  silentBase: number;
  silentPrevRevenue: number;
  silent: SilentMarket[];
  newMarkets: NewMarket[];
  assortment: AgentAssortment | null;
  /** Точек с чистой покупкой у агента за месяц. */
  akb: number;
  akbMonths: AkbByMonth | null;
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

export type SyncProgress = {
  mode: string;
  phase: string;
  entity: string | null;
  rows: number;
  startedAt: string;
  /** Оценка выполненного, 0–100. */
  percent?: number;
  stepsDone?: number;
  stepsTotal?: number;
  /** Сколько строк ожидается в текущем шаге (счётчик Linko). */
  expected?: number | null;
  /** Сколько примерно осталось; null — пока рано оценивать. */
  remainingSeconds?: number | null;
};

export type SyncStatus = {
  configured: boolean;
  isRunning: boolean;
  /** Отмена запрошена — синхронизация сворачивается. */
  isCancelling?: boolean;
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

export type AssortmentRegionRow = { id: string; name: string; kg: number; revenue: number; skuSelling: number; skuNotCarried: number; skuLost: number; akb: number };

/** Охват страниц категории и артикула — откуда пришли. */
export type ScopeKind = "republic" | "direction" | "region" | "agent" | "export";

/** Категория в охвате: плитки и артикулы (card), для республики и направления — регионы. */
export type CategoryView = {
  period: Period;
  scope: ScopeKind;
  scopeName: string;
  categoryId: string;
  name: string;
  card: CategoryCard | null;
  regions: AssortmentRegionRow[];
};

export type ProductBreakdownRow = {
  id: string;
  name: string;
  sub: string | null;
  status: SkuStatus;
  kg: number;
  revenue: number;
  tt: number;
  outlets: number;
  distribution: number | null;
  prevMonthKg: number;
};

/** Артикул в охвате: где идёт, а где нет — по регионам, ТП региона или магазинам ТП / экспорта. */
export type ProductView = {
  period: Period;
  scope: ScopeKind;
  scopeName: string;
  productId: number;
  name: string;
  code: string | null;
  categoryId: string;
  category: string;
  status: SkuStatus;
  factKg: number;
  revenue: number;
  tt: number;
  outlets: number;
  distribution: number | null;
  pricePerKg: number | null;
  prevMonthKg: number;
  breakdown: "regions" | "agents" | "stores";
  rows: ProductBreakdownRow[];
};

/** Плитки над категориями «Ассортимента». */
export type AssortmentSummary = {
  factKg: number;
  revenue: number;
  prevMonthKg: number;
  skuSold: number;
  skuTotal: number;
  skuLost: number;
  outlets: number;
};

export type AssortmentView = {
  period: Period;
  scopeName: string;
  summary: AssortmentSummary;
  categories: CategoryCard[];
  akbMonths: AkbByMonth;
  regions: AssortmentRegionRow[];
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
  /** orders — по строкам заказов; name — из фасовки в названии (кг и коробки — оценка, ≈); none — веса нет. */
  unitKgSource: "orders" | "name" | "none";
  boxKg: number | null;
  boxNote: string;
  pieces: number;
  kg: number | null;
  boxes: number | null;
  kgPerDay: number | null;
  daysOfCover: number | null;
  need15Kg: number | null;
  need30Kg: number | null;
  /** Входная цена дилера за единицу учёта; null — товара нет в прайсе. */
  price: number | null;
  /** Остаток × входная цена. */
  valueSum: number | null;
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
  /** Стоимость запаса по входной цене — только SKU с ценой. */
  valueSum: number;
  withoutPrice: number;
  /** SKU, у которых вес единицы взят из названия. */
  approxWeight: number;
};

export type StockView = {
  syncedAt: string | null;
  velocityDays: number;
  velocityFrom: string;
  velocityTo: string;
  /** Прайс входной цены дилера; null — не найден в Linko. */
  priceList: string | null;
  regions: { id: string; name: string; stockId: number }[];
  factory: { id: string; name: string; stockId: number } | null;
  items: StockItem[];
  otherStocks: { stockId: number; name: string; pieces: number; kg: number | null; items: number }[];
  totals: StockTotals;
  factoryTotals: StockTotals | null;
};

/** Пара «товар × регион» в аутстоке; days — флаги по дням периода: «1» — товар утром был, «0» — нет; received — «1» в дни прихода с завода. */
export type OutstockPair = {
  regionId: string;
  region: string;
  productId: number;
  product: string;
  code: string | null;
  category: string;
  top: boolean;
  periodKg: number;
  periodSum: number;
  perDayKg: number;
  avgPrice: number | null;
  zeroDays: number;
  dealerDays: number;
  factoryDays: number;
  negativeDays: number;
  lostKg: number;
  lostSum: number;
  dealerLossSum: number;
  factoryLossSum: number;
  core: boolean;
  chronic: boolean;
  snapshotKg: number;
  days: string;
  received: string;
};

/** Карточка категории: share — доля во всех потерях области, lossShare — упущенные кг к проданным («к факту»). */
export type OutstockCategory = {
  name: string;
  lostKg: number;
  lostSum: number;
  soldKg: number;
  share: number;
  lossShare: number | null;
  pairs: number;
  zeroDays: number;
  selected: boolean;
};

export type OutstockTopProduct = { productId: number; name: string; code: string | null; top: boolean; lostSum: number; share: number };

export type OutstockRegion = {
  id: string;
  name: string;
  dealer: string | null;
  soldKg: number;
  lostKg: number;
  lostSum: number;
  lossShare: number | null;
  zeroDays: number;
  pairs: number;
  corePairs: number;
  chronic: number;
  dealerLossSum: number;
  factoryLossSum: number;
  top: OutstockTopProduct[];
};

export type OutstockProduct = {
  id: number;
  name: string;
  code: string | null;
  category: string;
  top: boolean;
  soldKg: number;
  zeroShare: number | null;
  lostKg: number;
  lostSum: number;
  regions: number;
  regionsSold: number;
  corePairs: number;
  chronic: number;
  dealerLossSum: number;
  factoryLossSum: number;
};

export type OutstockMatrixRow = { id: string; name: string; dealer: string | null; sum: number[]; kg: number[]; totalSum: number; totalKg: number };

export type OutstockMatrix = { categories: string[]; rows: OutstockMatrixRow[]; totalSum: number[]; totalKg: number[] };

export type OutstockTotals = {
  lostKg: number;
  lostSum: number;
  soldKg: number;
  soldSum: number;
  lossShare: number | null;
  pairs: number;
  pairsWithLoss: number;
  productsWithLoss: number;
  regionsWithLoss: number;
  zeroDays: number;
  dealerDays: number;
  factoryDays: number;
  corePairs: number;
  coreSum: number;
  chronic: number;
  dealerLossSum: number;
  factoryLossSum: number;
  cells: number;
  negativeCells: number;
  negativeSharePct: number | null;
};

export type OutstockScope = "top" | "all" | "rest";

export type OutstockView = {
  year: number;
  month: number;
  from: string;
  to: string;
  /** Дней периода, по которым восстановлен остаток; 0 — снимок раньше начала месяца. */
  days: number;
  daysInMonth: number;
  snapshotDate: string;
  syncedAt: string | null;
  regionId: string | null;
  scope: OutstockScope;
  selectedCategories: string[];
  topConfigured: boolean;
  topHint: string;
  factoryKnown: boolean;
  regions: { id: string; name: string; dealer: string | null }[];
  totals: OutstockTotals;
  categories: OutstockCategory[];
  pairs: OutstockPair[];
  byRegion: OutstockRegion[];
  byProduct: OutstockProduct[];
  matrix: OutstockMatrix;
};

/** Отгрузка в четырёх единицах: кг, коробки (где фасовка известна), сумма завода, сумма дилера. */
export type PrimaryAmounts = { kg: number; boxes: number; sumFactory: number; sumDealer: number };

/** Строка разреза первички (категория или дилер): месяц, 12 месяцев года, план в кг (null — плана нет). */
export type PrimaryRow = {
  id: string;
  name: string;
  sub: string | null;
  month: PrimaryAmounts;
  months: PrimaryAmounts[];
  planMonthKg: number | null;
  planMonths: (number | null)[];
};

export type PrimaryCard = { kg: number; sumFactory: number; counterparties: number; transfers: number };

export type PrimaryView = {
  year: number;
  month: number;
  dataThrough: string | null;
  daysInMonth: number;
  workedDays: number;
  syncedAt: string | null;
  factoryStock: string | null;
  exportStock: string | null;
  dealerPriceList: string | null;
  republic: PrimaryCard;
  export: PrimaryCard;
  monthTotal: PrimaryAmounts;
  planMonthKg: number | null;
  forecastKg: number | null;
  monthTransfers: number;
  monthsWithData: number[];
  months: PrimaryAmounts[];
  planMonths: (number | null)[];
  ytd: PrimaryAmounts;
  ytdArticles: number;
  ytdReturnsKg: number;
  ytdReturnLines: number;
  planYtdKg: number | null;
  boxesUnknownKg: number;
  categories: PrimaryRow[];
  dealers: PrimaryRow[];
  items: { productId: number; name: string; code: string | null; category: string; ytd: PrimaryAmounts; boxesKnown: boolean }[];
  monthLines: { day: number; dealerId: string; productId: number | null; kg: number; boxes: number; sumFactory: number; sumDealer: number }[];
  productNames: Record<string, string>;
  notes?: string[];
};
/** План и факт по категории или паре категорий показателя Linko; факт — ТП с этим планом, scopeFactKg — весь факт. */
export type CategoryPlanFact = {
  categoryId: number | null;
  name: string;
  planKg: number | null;
  factKg: number;
  revenue: number;
  execution: number | null;
  scopeFactKg?: number | null;
};

export type NextMonthPlan = {
  year: number;
  month: number;
  planKg: number;
  currentPlanKg: number | null;
  agents: number;
  rows: { id: string; name: string; planKg: number; currentPlanKg: number | null }[];
};
