// Раздел «Финансы» и интеграция 1С: типы ответов API (/api/finance, /api/integrations/onec) и подписи. Все суммы — факт 1С в валюте учёта (UZS),
// без пересчёта; валютные суммы — отдельными полями с кодом валюты.

export type FinanceMeta = {
  source: string;
  kind: string;
  demo: boolean;
  syncedAt: string | null;
  infobase: string | null;
  organizationId: string | null;
  organizationName: string | null;
  from: string;
  to: string;
  dataThrough: string | null;
  hasData: boolean;
  note: string | null;
  /** Применённые отборы: контрагент, номенклатура, склад, подразделение — с названиями из 1С. */
  dimensions?: FinanceDimension[] | null;
};

export type FinanceDimension = { kind: string; id: string; name: string };

/** Отборы отчётов «Финансов» (параметры адреса — те же ключи). */
export const dimensionKinds: { key: string; label: string }[] = [
  { key: "counterparty", label: "Контрагент" },
  { key: "item", label: "Номенклатура" },
  { key: "warehouse", label: "Склад" },
  { key: "department", label: "Подразделение" },
];

export type FinanceKpi = { code: string; title: string; day: number; month: number; year: number };

export type FinanceOrganization = { id: string; name: string; inn: string | null; currency: string };
export type PnlValue = { code: string; title: string; kind: "income" | "expense" | "total"; value: number; previous: number | null };
export type MonthPoint = { month: string; revenue: number; expenses: number; netProfit: number };
export type AmountRow = { key: string; title: string; amount: number; previous?: number | null; quantity?: number | null; currency?: string | null; currencyAmount?: number | null };

export type FinanceOverview = {
  meta: FinanceMeta;
  pnl: PnlValue[];
  months: MonthPoint[];
  cashTotal: number;
  cash: AmountRow[];
  receivables: number;
  payables: number;
  advancesGiven: number;
  advancesReceived: number;
  inventory: AmountRow[];
  balancesMonth: string | null;
  expenseGrowth: AmountRow[];
  /** Продажи, валовая прибыль, закупки, производство — за день, с начала месяца и с начала года на дату asOf. */
  kpis?: { asOf: string; items: FinanceKpi[] } | null;
};

export type PnlLineView = { code: string; title: string; kind: "income" | "expense" | "total"; months: number[]; total: number; previous: number | null };
export type FinancePnl = { meta: FinanceMeta; months: string[]; lines: PnlLineView[]; expensesByAccount: AmountRow[]; expensesByDepartment: AmountRow[] };

export type CashFlowRow = { code: string; title: string; inflow: boolean; months: number[]; total: number };
export type FinanceCash = { meta: FinanceMeta; months: string[]; flows: CashFlowRow[]; inflow: number; outflow: number; balances: AmountRow[]; balancesMonth: string | null };

export type SettlementRow = {
  counterpartyId: string;
  name: string;
  inn: string | null;
  balance: number;
  overdue: number;
  oldestDue: string | null;
  maxOverdueDays: number | null;
  buckets: Record<string, number>;
  termsKnown: boolean;
};
export type FinanceSettlements = {
  meta: FinanceMeta;
  kind: "receivables" | "payables";
  balancesMonth: string | null;
  total: number;
  overdue: number;
  buckets: Record<string, number>;
  advances: number;
  rows: SettlementRow[];
  method: string;
};

export type TrialRow = { account: string; name: string; openingDebit: number; openingCredit: number; turnoverDebit: number; turnoverCredit: number; closingDebit: number; closingCredit: number };
export type FinanceTrialBalance = { meta: FinanceMeta; rows: TrialRow[]; total: TrialRow; method: string };

export type SubcontoBalance = { kind: string; id: string | null; name: string; closing: number; quantity: number | null; currency: string | null; currencyAmount: number | null };
export type FinanceAccount = {
  meta: FinanceMeta;
  account: string;
  name: string;
  type: string;
  subconto: string[];
  totals: TrialRow | null;
  breakdown: SubcontoBalance[];
  monthly: { month: string; debit: number; credit: number }[];
};

export type SubcontoValue = { side: "debit" | "credit"; index: number; type: string; name: string | null };
export type EntryView = {
  id: string;
  date: string;
  debitAccount: string;
  debitName: string | null;
  creditAccount: string;
  creditName: string | null;
  amount: number;
  content: string | null;
  counterparty: string | null;
  currency: string | null;
  currencyAmount: number | null;
  quantity: number | null;
  documentId: string | null;
  registrarType: string;
  registrarNumber: string | null;
  registrarSourceType: string | null;
  subconto: SubcontoValue[];
};
export type FinanceEntries = { meta: FinanceMeta; items: EntryView[]; total: number; page: number; pageSize: number; amount: number };

export type FinanceDocument = {
  id: string;
  type: string;
  sourceType: string;
  number: string;
  date: string;
  posted: boolean;
  deleted: boolean;
  organization: string | null;
  counterparty: string | null;
  contract: string | null;
  warehouse: string | null;
  currency: string;
  amount: number;
  vatAmount: number | null;
  amountAccounting: number;
  rate: number | null;
  rateMultiplier: number | null;
  rateDate: string | null;
  rateSource: string | null;
  dueDate: string | null;
  purpose: string | null;
  comment: string | null;
  lines: { line: number; item: string | null; quantity: number; unit: string | null; price: number | null; amount: number; vatAmount: number | null; costAmount: number | null; account: string | null }[];
  entries: EntryView[];
  version: string | null;
  importedAt: string;
  updatedAt: string;
  changes: number;
};

export type UnitCostRow = { itemId: string; item: string; unit: string | null; months: (number | null)[] };
export type RateRow = { currency: string; first: number | null; last: number | null; lastDate: string | null; changePercent: number | null; source: string | null };
export type FinanceOperations = {
  meta: FinanceMeta;
  months: string[];
  sales: number[];
  purchases: number[];
  costOfSales: number[];
  productionCosts: number[];
  topCustomers: AmountRow[];
  topSuppliers: AmountRow[];
  topItems: AmountRow[];
  salesByCurrency: AmountRow[];
  production: AmountRow[];
  inventoryByWarehouse: AmountRow[];
  inventory: AmountRow[];
  unitCosts: UnitCostRow[];
  rates: RateRow[];
  balancesMonth: string | null;
};

export type Anomaly = { kind: string; title: string; detail: string; amount: number | null; date: string | null; link: string | null; severity: number };
export type FinanceAnomalies = { meta: FinanceMeta; items: Anomaly[]; checks: string[] };

// ---------- Интеграция 1С ----------

export type DiagnosticStep = { code: string; ok: boolean; message: string; durationMs: number };
export type ConnectionReport = {
  checkedAt: string;
  ok: boolean;
  baseUrl: string | null;
  host: string | null;
  port: number | null;
  https: boolean;
  tlsValid: boolean | null;
  certificateSubject: string | null;
  certificateExpires: string | null;
  authOk: boolean | null;
  infobaseId: string | null;
  infobaseName: string | null;
  apiVersion: string | null;
  configuration: string | null;
  message: string | null;
  steps: DiagnosticStep[];
  /** Откуда служба взяла подключение: env — переменные службы, onebase — эта страница, none — не задано, mock — тестовый источник. */
  source?: string | null;
  /** Как отслеживаются изменения 1С: versions — сверкой версий (только чтение), changes — по плану обмена. */
  changeTracking?: string | null;
};

export type OneCSettings = {
  paused: boolean;
  intervalMinutes: number;
  batchSize: number;
  aiEnabled: boolean;
  acceptedInfobaseId: string | null;
  acceptedInfobaseName: string | null;
  pendingInfobaseId: string | null;
  pendingInfobaseName: string | null;
  demoData: boolean;
  hasToken: boolean;
  tokenHint: string | null;
  tokenCreatedAt: string | null;
  updatedAt: string | null;
  /** Подключение к 1С со страницы (пароль и API Secret — только признаки). */
  baseUrl: string | null;
  username: string | null;
  hasPassword: boolean;
  hasApiSecret: boolean;
  apiSecretHint: string | null;
  caCertificatePem: string | null;
  connectionUpdatedAt: string | null;
  connectionVersion: string | null;
  /** Организации для загрузки; пусто — все. */
  organizationIds: string[];
  /** Включённые модули (все — по умолчанию). */
  modules: string[];
  historyFrom: string | null;
};

/** Модули загрузки 1С (как OneCModules на сервере): документы по типам; «Бухгалтерия» — проводки и ведомость. */
export const oneCModules: { key: string; label: string; hint: string }[] = [
  { key: "sales", label: "Продажи", hint: "реализация, возвраты от покупателей, счета-фактуры выданные, заказы покупателей" },
  { key: "purchases", label: "Закупки", hint: "поступления, возвраты поставщикам, счета-фактуры полученные, заказы поставщикам" },
  { key: "warehouse", label: "Склад", hint: "перемещения, списания, инвентаризации" },
  { key: "money", label: "Деньги", hint: "касса и банк: поступления и списания" },
  { key: "production", label: "Производство", hint: "выпуск продукции, передача материалов в производство" },
  { key: "accounting", label: "Бухгалтерия", hint: "проводки всех документов, ручные операции и оборотно-сальдовая ведомость — из них строятся «Финансы»" },
  { key: "other", label: "Прочие документы", hint: "документы других видов с проводками" },
];

export const connectionSourceLabel: Record<string, string> = {
  env: "переменные службы на сервере",
  onebase: "эта страница",
  none: "не задано",
  mock: "тестовый источник",
};

export const changeTrackingLabel: Record<string, string> = {
  versions: "сверка версий — только чтение, в 1С ничего не пишется",
  changes: "план обмена 1С (/changes)",
};

export type OneCRun = {
  id: string;
  kind: string;
  trigger: string;
  status: string;
  provider: string;
  startedAt: string;
  finishedAt: string | null;
  durationSeconds: number | null;
  step: string | null;
  created: number;
  updated: number;
  unchanged: number;
  deleted: number;
  rejected: number;
  batches: number;
  errors: number;
  message: string | null;
};

export type PeriodCheck = { period: string; organizationId: string; stream: string; expectedCount: number | null; expectedAmount: number | null; importedCount: number; importedAmount: number; ok: boolean; note: string | null };
export type OneCHistorical = {
  id: string;
  from: string;
  to: string;
  batchSize: number;
  status: string;
  requestedAt: string;
  startedAt: string | null;
  finishedAt: string | null;
  periodsTotal: number;
  periodsDone: number;
  records: number;
  currentPeriod: string | null;
  currentStream: string | null;
  referencesDone: boolean;
  message: string | null;
  checks: PeriodCheck[];
};

export type OneCCommand = { id: string; kind: string; status: string; requestedAt: string; doneAt: string | null; message: string | null };
export type OneCCounts = { organizations: number; accounts: number; counterparties: number; contracts: number; items: number; references: number; rates: number; documents: number; entries: number; balances: number; changes: number };

export type OneCStatus = {
  state: "not_configured" | "waiting_worker" | "offline" | "error" | "paused" | "mismatch" | "connected";
  stateText: string;
  settings: OneCSettings;
  worker: { workerId: string; provider: string; version: string | null; online: boolean; enabled: boolean; busy: boolean; lastSeenAt: string; nextSyncAt: string | null; baseUrl: string | null; connection: ConnectionReport | null } | null;
  running: OneCRun | null;
  lastRun: OneCRun | null;
  lastSuccess: OneCRun | null;
  nextSyncAt: string | null;
  openFailures: number;
  rejectedRecords: number;
  historical: OneCHistorical | null;
  counts: OneCCounts;
  commands: OneCCommand[];
  organizations?: { id: string; name: string; inn: string | null; deleted: boolean }[];
};

export type OneCFailure = { id: string; kind: string; stream: string; organizationId: string | null; period: string | null; entityType: string | null; sourceId: string | null; error: string; attempts: number; status: string; firstAt: string; lastAt: string; resolvedAt: string | null; hasPayload: boolean };
export type OneCChange = { at: string; entityType: string; sourceEntityId: string; action: string; changes: string | null; runId: string | null };
export type Page<T> = { items: T[]; total: number; page: number; pageSize: number };
export type RunStep = { stream: string; organizationId: string | null; rows: number; batches: number; error: string | null; durationMs: number };

export const ONEC_SETTINGS = "/settings/integrations/finance/onec";

export const stateTone: Record<OneCStatus["state"], string> = {
  connected: "bg-ok-soft text-ok",
  paused: "bg-muted text-ink-2",
  waiting_worker: "bg-muted text-ink-2",
  not_configured: "bg-muted text-ink-2",
  offline: "bg-warn-soft text-warn",
  error: "bg-bad-soft text-bad",
  mismatch: "bg-bad-soft text-bad",
};

export const stateLabel: Record<OneCStatus["state"], string> = {
  connected: "Подключено",
  paused: "Пауза",
  waiting_worker: "Ждём службу",
  not_configured: "Не настроено",
  offline: "Служба не на связи",
  error: "Ошибка подключения",
  mismatch: "Другая база 1С",
};

export const runKindLabel: Record<string, string> = {
  incremental: "Синхронизация",
  historical: "История",
  test: "Тестовая",
  retry: "Повтор ошибок",
};

export const runStatusView: Record<string, { label: string; className: string }> = {
  Running: { label: "Идёт", className: "bg-accent-soft text-accent-strong" },
  Succeeded: { label: "Успешно", className: "bg-ok-soft text-ok" },
  Partial: { label: "Частично", className: "bg-warn-soft text-warn" },
  Failed: { label: "Ошибка", className: "bg-bad-soft text-bad" },
  Cancelled: { label: "Остановлено", className: "bg-muted text-ink-2" },
  Pending: { label: "В очереди", className: "bg-muted text-ink-2" },
};

export const failureKindLabel: Record<string, string> = {
  fetch: "Страница 1С не получена",
  batch: "Пакет не записан",
  record: "Запись отклонена OneBase",
  validation: "Запись не прошла проверку",
};

export const changeActionLabel: Record<string, string> = {
  updated: "Изменено",
  deleted: "Удалено",
  restored: "Восстановлено",
  cancelled: "Отмена проведения",
  reposted: "Перепроведено",
};

export const commandLabel: Record<string, string> = {
  test_connection: "Проверка подключения",
  test_sync: "Тестовая синхронизация",
  sync_now: "Синхронизация",
  historical_import: "Историческая загрузка",
  retry_failures: "Повтор ошибок",
};

export const entityLabel: Record<string, string> = {
  organization: "Организации",
  account: "План счетов",
  counterparty: "Контрагенты",
  contract: "Договоры",
  currency: "Валюты",
  exchange_rate: "Курсы",
  item: "Номенклатура",
  warehouse: "Склады",
  department: "Подразделения",
  document: "Документы",
  entry: "Проводки",
  balance: "Остатки",
  "reference:cash_flow_items": "Статьи ДДС",
  "reference:cost_items": "Статьи затрат",
  "reference:bank_accounts": "Банковские счета",
  "reference:cash_desks": "Кассы",
  "reference:batches": "Партии",
  "reference:fixed_assets": "Основные средства",
  "reference:other_income_expense": "Прочие доходы и расходы",
};

/** Поток синхронизации для людей: «ref:account» → «План счетов», «history:entries» → «История · проводки». */
export function streamLabel(stream: string): string {
  if (stream.startsWith("ref:")) return entityLabel[stream.slice(4)] ?? stream;
  if (stream.startsWith("history:")) return `История · ${({ rates: "курсы", documents: "документы", entries: "проводки", balances: "остатки" } as Record<string, string>)[stream.slice(8)] ?? stream.slice(8)}`;
  if (stream.startsWith("retry:")) return `Повтор · ${streamLabel(stream.slice(6))}`;
  if (stream.startsWith("balances")) return stream.includes(":") ? `Остатки ${stream.split(":")[1]}` : "Остатки";
  if (stream === "changes") return "Изменения 1С";
  if (stream.startsWith("versions:")) {
    const rest = stream.slice(9);
    if (rest === "full") return "Сверка версий · ночная";
    if (rest.startsWith("ref:")) return `Сверка версий · ${entityLabel[rest.slice(4)] ?? rest.slice(4)}`;
    if (rest.startsWith("document")) return `Сверка версий · документы${rest.includes(":") ? ` ${rest.slice(rest.indexOf(":") + 1)}` : ""}`;
    return `Сверка версий · ${entityLabel[rest] ?? rest}`;
  }
  return stream;
}

export const docTypeLabel: Record<string, string> = {
  sale: "Реализация",
  sale_return: "Возврат от покупателя",
  purchase: "Поступление",
  purchase_return: "Возврат поставщику",
  invoice_out: "Счёт-фактура выданный",
  invoice_in: "Счёт-фактура полученный",
  cash_in: "Приходный кассовый ордер",
  cash_out: "Расходный кассовый ордер",
  bank_in: "Поступление на расчётный счёт",
  bank_out: "Списание с расчётного счёта",
  production: "Выпуск продукции",
  material_issue: "Передача материалов в производство",
  transfer: "Перемещение",
  write_off: "Списание",
  inventory: "Инвентаризация",
  manual: "Операция",
  other: "Прочий документ",
  order_customer: "Заказ покупателя",
  order_supplier: "Заказ поставщику",
};

export const agingLabel: Record<string, string> = {
  not_due: "Срок не наступил",
  "1_30": "Просрочка 1–30 дней",
  "31_60": "31–60 дней",
  "61_90": "61–90 дней",
  over_90: "Больше 90 дней",
  no_term: "Срок не задан",
};

// ---------- Числа и даты ----------

const ru = (digits = 0) => new Intl.NumberFormat("ru-RU", { maximumFractionDigits: digits, minimumFractionDigits: digits });

/** Полная сумма: 1 234 567. */
export const full = (n: number | null | undefined, digits = 0) => (n == null ? "—" : ru(digits).format(n));

/** Короткая сумма для карточек: «1,23 млрд», «345,6 млн», «12 345». */
export function money(n: number | null | undefined): string {
  if (n == null) return "—";
  const a = Math.abs(n);
  if (a >= 1e9) return `${ru(2).format(n / 1e9)} млрд`;
  if (a >= 1e6) return `${ru(1).format(n / 1e6)} млн`;
  return ru(0).format(n);
}

/** Изменение к прошлому периоду, %; null — сравнить не с чем. */
export function change(now: number, before: number | null | undefined): number | null {
  if (before == null || before === 0) return null;
  return ((now - before) / Math.abs(before)) * 100;
}

export const date = (iso: string | null | undefined) => (iso ? new Date(iso.length === 10 ? `${iso}T00:00:00+05:00` : iso).toLocaleDateString("ru-RU", { timeZone: "Asia/Tashkent" }) : "—");
export const moment = (iso: string | null | undefined) =>
  iso ? new Date(iso).toLocaleString("ru-RU", { timeZone: "Asia/Tashkent", day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" }) : "—";

const monthNames = ["янв", "фев", "мар", "апр", "май", "июн", "июл", "авг", "сен", "окт", "ноя", "дек"];
export const monthShort = (iso: string) => {
  const [y, m] = iso.split("-").map(Number);
  return `${monthNames[m - 1]} ${String(y).slice(2)}`;
};

// ---------- Отбор ----------

export type SearchParams = Record<string, string | string[] | undefined>;
export const one = (sp: SearchParams, key: string): string | undefined => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

/** Организация и период из адреса страницы → строка запроса API. */
/** Параметры отбора, которые переносятся между страницами «Финансов»: организация, период и отборы. */
export const FINANCE_KEYS = ["organization", "from", "to", "counterparty", "item", "warehouse", "department"] as const;

export function financeQuery(sp: SearchParams, extra: Record<string, string | undefined> = {}): string {
  const params = new URLSearchParams();
  for (const key of FINANCE_KEYS) {
    const v = one(sp, key);
    if (v) params.set(key, v);
  }

  for (const [k, v] of Object.entries(extra)) if (v) params.set(k, v);
  return params.toString();
}

/** Текущая дата по Ташкенту, ГГГГ-ММ-ДД. */
export function todayIso(): string {
  return new Date().toLocaleDateString("sv-SE", { timeZone: "Asia/Tashkent" });
}

export function periodPresets(today = todayIso()): { key: string; label: string; from: string; to: string }[] {
  const [y, m] = today.split("-").map(Number);
  const iso = (d: Date) => d.toISOString().slice(0, 10);
  const first = (yy: number, mm: number) => iso(new Date(Date.UTC(yy, mm - 1, 1)));
  const last = (yy: number, mm: number) => iso(new Date(Date.UTC(yy, mm, 0)));
  const q = Math.floor((m - 1) / 3);
  return [
    { key: "month", label: "Этот месяц", from: first(y, m), to: today },
    { key: "prev", label: "Прошлый месяц", from: first(m === 1 ? y - 1 : y, m === 1 ? 12 : m - 1), to: last(m === 1 ? y - 1 : y, m === 1 ? 12 : m - 1) },
    { key: "quarter", label: "Квартал", from: first(y, q * 3 + 1), to: today },
    { key: "year", label: "С начала года", from: first(y, 1), to: today },
    { key: "12m", label: "12 месяцев", from: first(m === 12 ? y : y - 1, m === 12 ? 1 : m + 1), to: today },
  ];
}
