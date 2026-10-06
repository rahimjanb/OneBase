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

/** Вид плана вторички: «План РОП» (по умолчанию) или «План «Завод»» — параметр plan в адресе и в API. */
export type PlanKind = "rop" | "factory";

export type Period = {
  year: number;
  month: number;
  /** Отчётный день — последний полный день с данными (вчера для идущего месяца). */
  dataThrough: string;
  workedDays: number;
  daysInMonth: number;
  previousCutoff: string;
  /** Месяц закрыт: данные за все дни — прогнозов нет, чип «данные по» не показывается. */
  closed: boolean;
  /** По какому плану посчитан ответ. */
  plan: PlanKind;
  /** Какие планы регионов заведены на месяц: остальные переключатель плана не предлагает. */
  availablePlans: PlanKind[];
};

export type TargetValue = { value: number | null; target: number; ratio: number | null; level: TargetLevel | null };

/**
 * Откуда план подразделений: rop — «План РОП», factory — «План «Завод»» (планы регионов × категорий, переключатель plan=rop|factory);
 * linko — планов регионов выбранного вида на месяц нет, план — сумма планов ТП из Linko.
 */
export type PlanSource = "rop" | "factory" | "linko";

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
  /** Визиты ТП − их заказы, принятые в месяце (может быть меньше нуля). */
  visitsWithoutOrder: number;
  /** Выполненные визиты ТП подразделения. */
  visitsDone: number;
  /** ТП месяца без вакансий (продажи, визиты или план) — знаменатель «АКБ на агента». */
  activeAgents: number;
  revenuePlan: RevenuePlanTile | null;
  /** Факт, с которым сравнивается план: весь факт регионов с планом РОП / «Завод» или факт ТП с планом из Linko. */
  planFactKg: number | null;
  planForecastKg: number | null;
  planAgents: number;
  /** Выполненные визиты не ТП (операторы, супервайзеры, вне справочника): в конверсию не входят. */
  visitsOutsideTeam: number;
  /** Откуда план: РОП, «Завод» или — если планов регионов на месяц нет — планы ТП из Linko. */
  planSource: PlanSource;
  /** Цвет выполнения — с сервера (от 90% — зелёный, от 60% — оранжевый, ниже — красный). */
  executionLevel: TargetLevel | null;
};

/** План по выручке: только агенты с планом в Linko; fact — их выручка. */
export type RevenuePlanTile = {
  plan: number;
  fact: number;
  execution: number | null;
  forecast: number | null;
  forecastExecution: number | null;
  agents: number;
  executionLevel: TargetLevel | null;
};

export type IndicatorPlan = { indicatorId: number; name: string; planType: string; plan: number; fact: number; execution: number | null; executionLevel: TargetLevel | null };

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
  weightExecutionLevel: TargetLevel | null;
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
  regions: {
    id: string;
    name: string;
    agents: number;
    weightPlan: number | null;
    weightFact: number;
    weightExecution: number | null;
    revenuePlan: number | null;
    revenueFact: number;
    weightExecutionLevel: TargetLevel | null;
  }[];
  agents: PlanPersonRow[];
  teamPlans: PlanPersonRow[];
  weightExecutionLevel: TargetLevel | null;
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
  executionLevel: TargetLevel | null;
  forecastExecutionLevel: TargetLevel | null;
};

/**
 * Строка календаря визитов (ТП, регион, итог). Заказы — принятые в выбранные дни; «с визита по маршруту / вне маршрута» — по визиту
 * в магазин в день ввода заказа, ordersNoVisit — без такого визита. notVisited и planShare у региона и итога — по их суммам (сервер).
 * visitsOutsideTeam — выполненные визиты не ТП (операторы, супервайзеры): в строки не входят.
 */
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
  ordersNoVisit: number;
  visitsOutsideTeam: number;
  planShareLevel: TargetLevel | null;
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
  /** В регионе за месяц нет ни одной покупки — дыра в данных, доли нет («нет данных»). У итога — нет данных хотя бы у одной строки. */
  noData: boolean;
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
  categoryPlans: CategoryPlanFact[];
  nextMonth: NextMonthPlan | null;
  /** Итог «Ещё не купили» — с сервера. */
  notBoughtTotal: NotBoughtRow;
  /** Итог «План и факт по категориям» по строкам с планом — с сервера; null — строк с планом нет. */
  categoryPlanTotal: CategoryPlanFact | null;
  /** Итог календаря визитов по строкам регионов — с сервера. */
  visitCalendarTotal: VisitCalendarRow;
  /** Календарь месяца по регионам (metric и category в адресе — как у региона). */
  calendar: MonthCalendar;
};

/** Мера «по месяцам» (кг или сум): итог, те же категории, что у АКБ, среднее за месяц — с сервера. */
export type MonthMetric = {
  total: (number | null)[];
  categories: { id: string; name: string; values: (number | null)[]; average: number | null }[];
  average: number | null;
};

/**
 * АКБ по месяцам года: итог и по категориям (без скрытых — Sales:AkbChartHiddenCategories — и не больше семи по объёму). null — данных
 * за месяц нет. average — среднее за месяц; kg и sum — те же строки в кг и сумах (переключатель АКБ / кг / сум). У «Первички» kg и sum нет.
 */
export type AkbByMonth = {
  year: number;
  months: number[];
  lastPartial: boolean;
  total: (number | null)[];
  categories: { id: string; name: string; values: (number | null)[]; average?: number | null }[];
  average?: number | null;
  kg?: MonthMetric;
  sum?: MonthMetric;
};

/** elsewhere — «не возят»: здесь ноль, а по республике в этом месяце идёт. */
export type SkuStatus = "selling" | "silent" | "lost" | "elsewhere";

/**
 * Артикул категории. akb — ТТ с положительной строкой артикула; weightShare — доля в весе категории; solo — «Только он»: точки, где куплен
 * только этот SKU, soloShare — их доля от ТТ артикула; isTop — товар из списка ТОП. Всё считает сервер.
 */
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
  weightShare: number | null;
  solo: number;
  soloShare: number | null;
  isTop: boolean;
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
  executionLevel: TargetLevel | null;
  forecastExecutionLevel: TargetLevel | null;
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
  /** Регион-канал (Урикзор, Сети): СВР и дилера у него нет. */
  isChannel: boolean;
  kpi: KpiTiles;
  unassigned: { kg: number; share: number | null };
  /** planSource — откуда план месяца (null — плана нет). */
  months: { month: number; planKg: number | null; factKg: number | null; planSource: PlanSource | null }[];
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
  categoryPlans: CategoryPlanFact[];
  nextMonth: NextMonthPlan | null;
  /** Итог «Ещё не купили» по ТП региона — с сервера. */
  notBoughtTotal: NotBoughtRow;
  /** Итог «План и факт по категориям» по строкам с планом — с сервера; null — строк с планом нет. */
  categoryPlanTotal: CategoryPlanFact | null;
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
  /** Для справки: визиты, в день которых ТП ввёл заказ в этой точке. */
  visitsWithOrder: number;
  /** Заказы ТП, принятые в месяце: конверсия = orders ÷ visits. */
  orders: number;
  conversion: MedianValue;
  sumPerVisit: MedianValue;
  avgCheck: MedianValue;
  categories: number;
  planCategories: number | null;
  categoryTarget: number;
  tempo: number | null;
  flags: AgentFlag[];
  categoryPlan: CategoryPlanFact[];
  indicators: IndicatorPlan[];
  sameDays: SameDaysRow;
  silentBase: number;
  silentPrevRevenue: number;
  silent: SilentMarket[];
  newMarkets: NewMarket[];
  assortment: AgentAssortment | null;
  /** Точек с чистой покупкой у агента за месяц. */
  akb: number;
  akbMonths: AkbByMonth | null;
  /** Выручка и кг на точку с покупкой (÷ akb) — с сервера; null — покупок нет. */
  revenuePerOutlet: number | null;
  kgPerOutlet: number | null;
  executionLevel: TargetLevel | null;
  revenueExecutionLevel: TargetLevel | null;
};

/** score — тяжесть замечаний (сервер): список отсортирован по ней, rank — место в списке. */
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
  score: number;
};

/** found — ТП с замечаниями в списке; directions — РМ для фильтра; fact — факт месяца: без агента и остальной (по нему рейтинг). */
export type ProblemsView = {
  period: Period;
  vacancies: number;
  agents: ProblemAgent[];
  found: number;
  directions: { id: string; name: string }[];
  fact: { factKg: number; unassignedKg: number; unassignedShare: number | null; assignedKg: number };
};

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
  /** Проданные заказы этого месяца, принятые после отчётного дня (сегодня или с приёмкой в будущем); у закрытого месяца — 0. */
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
  /** Прогноз месяца к факту прошлого месяца — как у карточек категорий, только у идущего месяца. */
  vsPrevMonth: number | null;
};

/** Товар категорий отчёта: share — доля выручки, akb — ТТ с положительной строкой, distribution — их доля от АКБ набора; isTop — ТОП. */
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
  isTop: boolean;
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
  /** Товаров вне категорий отчёта (бонус, подарки): в «Товары» не входят. */
  productsOutsideReport: number;
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
  /** «Доля в объёме ТП» — по весу: кг магазина ÷ кг ТП. */
  shareOfAgent: number | null;
  prevMonthKg: number;
  prevMonthRevenue: number;
  categoryRows: { name: string; kg: number; revenue: number; share: number | null }[];
  products: { productId: number; name: string; code: string | null; category: string; kg: number; revenue: number; isTop: boolean }[];
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
  /** Товаров вне категорий отчёта: в «Товары» не входят. */
  productsOutsideReport: number;
};

export type MatrixLevel = "none" | "low" | "ok";

/** Регион в «По регионам»; noData — за месяц в регионе ни одной покупки («нет данных», счётчики SKU — нули). */
export type AssortmentRegionRow = {
  id: string;
  name: string;
  kg: number;
  revenue: number;
  skuSelling: number;
  skuNotCarried: number;
  skuLost: number;
  akb: number;
  noData: boolean;
};

/** Охват страниц категории и артикула — откуда пришли. */
export type ScopeKind = "republic" | "direction" | "region" | "agent" | "export";

/** Категория в охвате: плитки и артикулы (card), для охвата из двух и больше регионов — регионы; mono — «Только он», точек охвата всего. */
export type CategoryView = {
  period: Period;
  scope: ScopeKind;
  scopeName: string;
  categoryId: string;
  name: string;
  card: CategoryCard | null;
  regions: AssortmentRegionRow[];
  mono: number;
};

/** noData — в регионе за месяц нет ни одной покупки («нет данных»). */
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
  noData: boolean;
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
  /** «Только он»: точки, где куплен только этот SKU; soloShare — их доля от ТТ артикула; mono — всего таких точек в охвате. */
  solo: number;
  soloShare: number | null;
  mono: number;
  isTop: boolean;
};

/** Плитки над категориями «Ассортимента»; mono — «Только он»: точек с положительной строкой ровно по одному SKU. */
export type AssortmentSummary = {
  factKg: number;
  revenue: number;
  prevMonthKg: number;
  skuSold: number;
  skuTotal: number;
  skuLost: number;
  outlets: number;
  mono: number;
};

export type AssortmentView = {
  period: Period;
  scopeName: string;
  summary: AssortmentSummary;
  categories: CategoryCard[];
  akbMonths: AkbByMonth;
  regions: AssortmentRegionRow[];
  products: ProductRow[];
  /** Товаров вне категорий отчёта (бонус, подарки): в «Товары» не входят. */
  productsOutsideReport: number;
  matrixRegions: { id: string; name: string }[];
  /** Матрица «товар × регион»: уровень цвета клетки считает сервер; averageDistribution — дистрибуция по всему охвату. */
  matrix: {
    productId: number;
    name: string;
    category: string;
    revenue: number;
    averageDistribution: number | null;
    isTop: boolean;
    cells: { regionId: string; distribution: number | null; level: MatrixLevel; tt: number }[];
  }[];
  quality: DataQuality;
};

// ---------- Продажи по SKU ----------

export type SkuSalesTop = "all" | "only" | "not";
export type SkuSalesAbc = "all" | "A" | "B" | "C";

/** Месяц отрезка: days — дней с данными (у идущего месяца — по отчётный день), partial — месяц не закрыт. */
export type SkuSalesMonth = { year: number; month: number; days: number; partial: boolean };

/** Строка SKU: ранг, доли и ABC — по отфильтрованному набору (до фильтра ABC); months — кг по месяцам отрезка. */
export type SkuSalesRow = {
  productId: number;
  name: string;
  code: string | null;
  category: string;
  isTop: boolean;
  rank: number;
  kg: number;
  revenue: number;
  pricePerKg: number | null;
  share: number | null;
  cumulative: number | null;
  abc: "A" | "B" | "C";
  akb: number;
  regions: number;
  firstKgPerDay: number | null;
  lastKgPerDay: number | null;
  dynamics: number | null;
  isNew: boolean;
  months: number[];
};

export type SkuSalesRegionRow = {
  productId: number;
  name: string;
  code: string | null;
  category: string;
  isTop: boolean;
  abc: "A" | "B" | "C";
  kg: number;
  revenue: number;
  countryShare: number | null;
  regionKg: number[];
  regionRevenue: number[];
  regionAkb: number[];
  regionShare: (number | null)[];
};

export type SkuSalesView = {
  from: string;
  to: string;
  dataThrough: string | null;
  months: SkuSalesMonth[];
  regionId: string | null;
  regionName: string | null;
  top: SkuSalesTop;
  abc: SkuSalesAbc;
  selectedCategories: string[];
  topConfigured: boolean;
  regions: { id: string; name: string }[];
  totals: { kg: number; revenue: number; pricePerKg: number | null; skus: number; skusA: number; regions: number; top5Share: number | null; shareOfAll: number | null };
  categories: { name: string; kg: number; revenue: number; skus: number; kgShare: number | null; revenueShare: number | null; pricePerKg: number | null; selected: boolean }[];
  /** Первый и последний квартал отрезка для «динамики»; null — отрезок внутри одного квартала. */
  quarters: { first: string; last: string; firstDays: number; lastDays: number } | null;
  rows: SkuSalesRow[];
  monthTotals: { year: number; month: number; kg: number; kgPerDay: number | null; partial: boolean }[];
  regionMatrix: { rows: SkuSalesRegionRow[]; totalKg: number[]; totalRevenue: number[]; kg: number; revenue: number };
  categoryRegions: { rows: { name: string; kg: number[]; share: (number | null)[]; totalKg: number; totalShare: number | null }[]; totalKg: number[]; kg: number };
  topRegions: { id: string; name: string; kg: number; items: { productId: number; name: string; isTop: boolean; kg: number; share: number | null }[] }[];
  facts: {
    skus: number;
    kg: number;
    revenue: number;
    regions: number;
    topName: string | null;
    topKg: number;
    topRevenue: number;
    topKgShare: number | null;
    topRevenueShare: number | null;
    top5Share: number | null;
    top10Share: number | null;
    top25Share: number | null;
    skusA: number;
    skusB: number;
    skusC: number;
    groupCShare: number | null;
  };
  /** Товаров вне категорий отчёта (бонус, подарки) с продажами за отрезок: в отчёт не входят. */
  outsideReport: number;
};

export type StockStatus = "deficit" | "overstock" | "dead" | "ok" | "unknown" | "none";

/**
 * Клетка склада: штуки как в Linko, кг, коробки, стоимость; скорость с поправкой на аутсток (kgPerDay; rawKgPerDay — без поправки,
 * zeroDays — зачтённые дни в нуле закрытого месяца) и та же скорость в коробках и деньгах; запас на 15 дней в трёх единицах;
 * рекомендуемый заказ (у завода — заказ у завода). Всё посчитано сервером — страница только показывает.
 */
export type StockCell = {
  pieces: number;
  kg: number | null;
  boxes: number | null;
  valueSum: number | null;
  kgPerDay: number | null;
  rawKgPerDay: number | null;
  boxesPerDay: number | null;
  sumPerDay: number | null;
  zeroDays: number;
  daysOfCover: number | null;
  need15Kg: number | null;
  need15Boxes: number | null;
  need15Sum: number | null;
  orderKg: number;
  orderBoxes: number | null;
  orderPieces: number | null;
  orderSum: number | null;
  /** Статус клетки по её остатку и скорости; у экспорта — none. */
  status: StockStatus;
};

export type StockItem = {
  productId: number;
  name: string;
  code: string | null;
  category: string;
  inReport: boolean;
  /** Товар из списка ТОП (Sales:TopProducts). */
  top: boolean;
  unitKg: number | null;
  /** orders — по строкам заказов с 1-го числа закрытого месяца; ordersYear — за год; name — из фасовки в названии (кг и коробки — оценка, ≈); none — веса нет. */
  unitKgSource: "orders" | "ordersYear" | "name" | "none";
  boxKg: number | null;
  boxNote: string;
  /** Фасовка из названия (последний вес), кг — фильтр «Вес фасовки»; null — веса в названии нет. */
  packKg: number | null;
  pieces: number;
  kg: number | null;
  boxes: number | null;
  kgPerDay: number | null;
  rawKgPerDay: number | null;
  boxesPerDay: number | null;
  sumPerDay: number | null;
  daysOfCover: number | null;
  need15Kg: number | null;
  need15Boxes: number | null;
  need15Sum: number | null;
  need30Kg: number | null;
  /** Входная цена дилера за единицу учёта на начало месяца; null — товара нет в прайсе. */
  price: number | null;
  /** Остаток × входная цена. */
  valueSum: number | null;
  /** Рекомендуемый заказ дилера (у охвата «Завод» — заказ у завода): до запаса на 15 дней, вверх до целой коробки. */
  orderKg: number;
  orderBoxes: number | null;
  orderPieces: number | null;
  orderSum: number | null;
  status: StockStatus;
  regions: Record<string, StockCell>;
  factory: StockCell | null;
};

/** Товар, которого в таблице нет: вне категорий отчёта (бонус, подарки, импорт) или без веса единицы. */
export type StockExcluded = {
  productId: number;
  name: string;
  code: string | null;
  category: string;
  reason: "outsideReport" | "withoutWeight";
  pieces: number;
  kg: number | null;
  factoryPieces: number;
};

/** Плитка категории по отфильтрованным строкам (до фильтра по категориям). */
export type StockCategoryTile = {
  name: string;
  skus: number;
  kg: number;
  valueSum: number;
  kgPerDay: number;
  daysOfCover: number | null;
  deficit: number;
  orderKg: number;
  selected: boolean;
};

export type StockTotals = {
  pieces: number;
  kg: number | null;
  boxes: number | null;
  kgPerDay: number | null;
  rawKgPerDay: number | null;
  boxesPerDay: number | null;
  sumPerDay: number | null;
  daysOfCover: number | null;
  need15Kg: number | null;
  need15Boxes: number | null;
  need15Sum: number | null;
  skus: number;
  deficit: number;
  overstock: number;
  dead: number;
  withoutWeight: number;
  /** Стоимость запаса по входной цене — только SKU с ценой. */
  valueSum: number;
  withoutPrice: number;
  /** SKU, у которых вес единицы взят из названия. */
  approxWeight: number;
  orderKg: number;
  orderBoxes: number;
  orderSum: number;
  orderSkus: number;
};

/** Поправка скорости на аутсток по стране: скорость до и после, пары с зачтёнными днями в нуле, изменение в долях. */
export type StockCorrection = { year: number; month: number; rawKgPerDay: number; kgPerDay: number; pairs: number; change: number | null };

export type StockRegion = { id: string; name: string; stockId: number; directionId: string | null };

export type StockView = {
  syncedAt: string | null;
  snapshotDate: string;
  velocityDays: number;
  velocityFrom: string;
  velocityTo: string;
  unitWeightFrom: string;
  /** Прайс входной цены дилера; null — не найден в Linko. */
  priceList: string | null;
  priceAsOf: string;
  /** country | rm:<id> | region:<id> | plant | export. */
  scope: string;
  scopeName: string;
  query: string | null;
  selectedCategories: string[];
  selectedPacks: number[];
  top: "all" | "only" | "not";
  topConfigured: boolean;
  status: string;
  regions: StockRegion[];
  /** Склады дилеров охвата; у «Завода» и «Экспорта» — пусто. */
  scopeRegions: StockRegion[];
  directions: { id: string; name: string }[];
  factory: StockRegion | null;
  export: StockRegion | null;
  categories: string[];
  packs: number[];
  categoryTiles: StockCategoryTile[];
  items: StockItem[];
  otherStocks: { stockId: number; name: string; pieces: number; kg: number | null; items: number; region: string | null }[];
  excluded: StockExcluded[];
  excludedOutsideReport: number;
  excludedWithoutWeight: number;
  totals: StockTotals;
  factoryTotals: StockTotals | null;
  /** Итоги по складам охвата — подвал матрицы, по отфильтрованным строкам. */
  regionTotals: Record<string, StockTotals>;
  correction: StockCorrection;
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
  /** Дни в нуле без данных по складу завода — чья потеря, неизвестно. */
  unknownDays: number;
  negativeDays: number;
  lostKg: number;
  lostSum: number;
  dealerLossSum: number;
  factoryLossSum: number;
  unknownLossSum: number;
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
  unknownLossSum: number;
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
  unknownLossSum: number;
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
  unknownDays: number;
  /** Доля дней «потеря дилера» среди всех дней в нуле (с днями без данных по заводу). */
  dealerDaysShare: number | null;
  corePairs: number;
  coreSum: number;
  chronic: number;
  chronicSum: number;
  dealerLossSum: number;
  factoryLossSum: number;
  unknownLossSum: number;
  cells: number;
  negativeCells: number;
  negativeSharePct: number | null;
};

export type OutstockInsightRegion = { id: string; name: string; dealer: string | null; lostSum: number; lossShare: number | null; everyNthKg: number | null };

/** Цифры «Выводов» — считает сервер (OutstockService.Insights); страница только подставляет их в текст. */
export type OutstockInsights = {
  topRegion: OutstockInsightRegion | null;
  /** Худший по доле среди регионов с продажами больше 1 000 кг, не тот же, что topRegion. */
  worstRegion: OutstockInsightRegion | null;
  topCategory: string | null;
  topCategoryShare: number | null;
  topProduct: { id: number; name: string; lostSum: number; regions: number } | null;
  /** Товары, чаще всего попадающие в ядро потерь. */
  coreFrequent: string[];
  dealerDaysShare: number | null;
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
  /** Выбраны категории или область не «ТОП» — есть что сбросить. */
  canReset: boolean;
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
  insights: OutstockInsights;
  /** Регион календаря по дням (параметр calendar); итоги и таблицы при этом остаются по области. */
  calendarRegionId: string | null;
  calendar: OutstockPair[];
};

/**
 * Отгрузка в четырёх единицах: кг, коробки (где фасовка известна; у экспорта коробок нет — null), сумма по цене дилера (sumFactory — цена
 * перемещения или заказа) и сумма по цене продажи дилера (sumDealer — прайс «Дилердан чикиш нарх»). Цены завода в Linko нет. Возврат — с минусом.
 */
export type PrimaryAmounts = { kg: number; boxes: number | null; sumFactory: number; sumDealer: number };

/** Доли в итоге по каждой единице (null — итог 0 или коробок нет). */
export type PrimaryShares = { kg: number | null; boxes: number | null; sumFactory: number | null; sumDealer: number | null };

/** Группа контрагента первички: склад дилера (регион) или точка с заказами завода (базар, сеть, фирменный магазин). */
export type PrimaryGroup = "dealer" | "direct";

/**
 * Строка разреза первички (категория, контрагент или страна): месяц, 12 месяцев года, с начала года (по выбранный месяц), доли, план в кг
 * (null — плана нет), выполнение и «осталось» — всё с сервера.
 */
export type PrimaryRow = {
  id: string;
  name: string;
  sub: string | null;
  month: PrimaryAmounts;
  months: PrimaryAmounts[];
  planMonthKg: number | null;
  planMonths: (number | null)[];
  group: PrimaryGroup | null;
  monthShare: PrimaryShares;
  monthExecution: number | null;
  monthRemainingKg: number | null;
  ytd: PrimaryAmounts;
  ytdShare: PrimaryShares;
  planYtdKg: number | null;
  ytdExecution: number | null;
};

/** Плитка входа в «Первичку»: с начала года; share — доля в отгрузке завода (республика + экспорт) по весу. */
export type PrimaryCard = { kg: number; sumFactory: number; counterparties: number; transfers: number; share: number | null };

/** Календарь отгрузок: контрагент × день периода, итоги и разбор — с сервера (параметры from, to, day, dealer). */
export type PrimaryCalendar = {
  monthDays: number[];
  from: number | null;
  to: number | null;
  days: number[];
  rows: { id: string; name: string; sub: string | null; cells: (PrimaryAmounts | null)[]; total: PrimaryAmounts; days: number }[];
  dayTotals: PrimaryAmounts[];
  total: PrimaryAmounts;
  day: number | null;
  dealer: string | null;
  detail:
    | { dealerId: string | null; dealerName: string | null; productId: number | null; name: string; code: string | null; category: string; isReturn: boolean; amounts: PrimaryAmounts }[]
    | null;
  detailTotal: PrimaryAmounts | null;
};

export type PrimaryView = {
  year: number;
  month: number;
  dataThrough: string | null;
  /** Дата данных: последний день с отгрузкой не позже вчера. */
  asOf: string | null;
  /** Выбранный месяц идёт (прогноз только у него); runningMonth — какой месяц года идёт. */
  running: boolean;
  runningMonth: number | null;
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
  monthExecution: number | null;
  monthRemainingKg: number | null;
  monthOverPlanKg: number | null;
  forecastKg: number | null;
  forecastExecution: number | null;
  monthTransfers: number;
  monthCounterparties: number;
  hasPlan: boolean;
  monthsWithData: number[];
  months: PrimaryAmounts[];
  planMonths: (number | null)[];
  monthExecutions: (number | null)[];
  ytd: PrimaryAmounts;
  ytdMonths: number;
  ytdArticles: number;
  ytdReturnsKg: number;
  ytdReturnLines: number;
  ytdReturnsShare: number | null;
  planYtdKg: number | null;
  ytdExecution: number | null;
  ytdPricePerKg: number | null;
  ytdMarkupSum: number | null;
  ytdMarkup: number | null;
  boxesUnknownKg: number;
  categories: PrimaryRow[];
  categoryTotal: PrimaryRow;
  dealers: PrimaryRow[];
  /** Итоги групп контрагентов (дилеры, прямые клиенты завода); у экспорта пусто. */
  dealerGroups: PrimaryRow[];
  dealerTotal: PrimaryRow;
  items: {
    productId: number;
    name: string;
    code: string | null;
    category: string;
    ytd: PrimaryAmounts;
    boxesKnown: boolean;
    pricePerKg: number | null;
    markup: number | null;
  }[];
  /** «Клиенты по месяцам» — АКБ / кг / сум в форме «АКБ по месяцам»; у экспорта null. */
  clients: { akb: AkbByMonth; kg: AkbByMonth; sum: AkbByMonth } | null;
  calendar: PrimaryCalendar | null;
  notes?: string[];
  /** Склады вне справочника регионов за год («Основной», «Нукус (интеграция учун)»): не дилеры, в первичку не входят; у экспорта null. */
  otherStocks?: { name: string; transfers: number; kg: number }[] | null;
};
/**
 * План и факт по категории. План РОП / «Завод» — сумма планов «регион × категория», факт — все продажи категории в подразделении.
 * План из Linko — показатель ТП (бывает на пару категорий), факт — ТП с этим планом, scopeFactKg — весь факт.
 * Осталось, прогноз и прогноз к плану (с цветом) считает сервер; прогноза нет у закрытого месяца.
 */
export type CategoryPlanFact = {
  categoryId: number | null;
  name: string;
  planKg: number | null;
  factKg: number;
  revenue: number;
  execution: number | null;
  scopeFactKg?: number | null;
  remainingKg: number | null;
  forecastKg: number | null;
  forecastExecution: number | null;
  forecastLevel: TargetLevel | null;
  executionLevel: TargetLevel | null;
};

/** План на следующий месяц: планы регионов (РОП / «Завод») или ТП из Linko; change — к текущему месяцу (null — сравнивать не с чем). */
export type NextMonthPlan = {
  year: number;
  month: number;
  planKg: number;
  currentPlanKg: number | null;
  agents: number;
  rows: { id: string; name: string; planKg: number; currentPlanKg: number | null; change: number | null }[];
  change: number | null;
  source: PlanSource;
};
