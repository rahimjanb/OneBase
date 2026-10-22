// «Акт сверки» и «DSO» (GET /api/sales/akt, /api/sales/dso): взаиморасчёты с дилерами — расчёт на сервере (AktCompute, DsoCompute).

export type AktRow = {
  key: string;
  name: string;
  open: number;
  ship: number;
  return: number;
  cash: number;
  bank: number;
  expenses: Record<string, number>;
  total: number;
  close: number;
  anchored: boolean;
  cashbackPending: number | null;
};

export type AktLine = { date: string; kind: string; amount: number; note: string | null; code: string | null };

export type AktView = {
  asOf: string;
  months: { key: string; year: number; month: number; partial: boolean }[];
  month: { key: string; year: number; month: number; partial: boolean };
  arts: { key: string; label: string }[];
  groups: { key: string; name: string; rows: AktRow[]; totals: AktRow }[];
  cards: { key: string; name: string; norm: number | null; docs: AktLine[]; payments: AktLine[]; expenses: AktLine[]; cashback: AktLine[] }[];
  /** Проверки: текст страница собирает по kind (text — для API); date — последняя строка реестра расходов. */
  checks: { kind: string; text: string; items: string[]; date: string | null }[];
  anchorDate: string | null;
  paymentsTo: string | null;
  expensesTo: string | null;
  hasPayments: boolean;
  hasExpenses: boolean;
  hasBalances: boolean;
};

export type DsoOpen = {
  date: string;
  amount: number;
  remaining: number;
  age: number;
  due: string | null;
  expected: string;
  late: number;
  lateExpected: number;
  doc: string;
};

export type DsoDealer = {
  key: string;
  name: string;
  status: "active" | "noterm" | "gone";
  norm: number | null;
  defaultNorm: boolean;
  fact: number | null;
  lateShare: number | null;
  debt: number;
  overdue: number;
  paidMonth: number;
  shippedMonth: number;
  paid8Weeks: number;
  lastPay: string | null;
  daysSincePay: number | null;
  cashShare: number;
  open: DsoOpen[];
  /** Прогноз оплат дилера по неделям (как общий); у «не работают с нами» — нет. */
  weeks: DsoWeek[] | null;
};

export type DsoWeek = { key: string; from: string | null; to: string | null; byNorm: number; expected: number; cash: number; bank: number };

export type DsoPayMonth = { key: string; year: number; month: number; cash: number; bank: number };

export type DsoView = {
  asOf: string;
  monthFrom: string;
  factDays: number;
  defaultNorm: number;
  debt: number;
  openCount: number;
  debtDealers: number;
  overdue: number;
  overdueShare: number;
  overdueDealers: number;
  dueWeek: number;
  expectedWeek: number;
  paidMonth: number;
  shippedMonth: number;
  dealers: DsoDealer[];
  weeks: DsoWeek[];
  payMonths: DsoPayMonth[];
  payDealers: { name: string; group: string; months: DsoPayMonth[] }[];
  channels: { key: string; name: string; months: { key: string; year: number; month: number; shipped: number[]; paid: number[] }[] }[];
  hasPayments: boolean;
  hasTerms: boolean;
  paymentsTo: string | null;
};
