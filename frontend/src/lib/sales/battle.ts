/**
 * «Продажи → Поле боя» (GET /api/sales/battle…): «Цели на маршрут» v2 (ТЗ v2 от 24.11.2026) — цели Могул и Twizos на маршруте ТП, два баланса
 * кешбека, квоты стоек, карточки. Даты — «ГГГГ-ММ-ДД». Охват проверяет сервер.
 */

export type BattleBalance = { issued: number; pending: number; confirmed: number; paid: number; toPay: number; canEarn: number | null };
export type BattleBalances = { mogul: BattleBalance; twizos: BattleBalance };
export type BattleQuota = { region: string; stock: number | null; target: number; agents: number; perAgent: number; manual: boolean };
export type BattleRegion = { name: string; agents: number; listed: number; woken: number; stands: BattleQuota | null; balances: BattleBalances };
export type BattleAgent = { id: number; name: string; region: string };

export type BattleOverview = {
  program: string;
  name: string;
  date: string;
  start: string;
  end: string;
  enabled: boolean;
  dataAsOf: string | null;
  ledgerRows: number;
  balances: BattleBalances;
  listed: number;
  woken: number;
  twizosClients: number;
  twizosNew: number;
  stands: number;
  regions: BattleRegion[];
  agents: BattleAgent[];
  scope: string;
};

export type BattleMogulGoal = { avgKg: number; months: number; targetKg: number; box: string; boxKg: number; boxes: number; kg: number; sum: number; afterDiscount: number; cashback: number };
export type BattleTwizosGoal = {
  segment: Segment;
  percent: number;
  avgInvoice: number | null;
  avgCheck: number;
  set: string;
  sum: number;
  kg: number;
  gift: string | null;
  cashback: number;
  stand: boolean;
};
export type BattleFact = {
  mogulStatus: Mark | null;
  mogulBoxes: number;
  mogulCashback: number;
  twizosStatus: Mark | null;
  twizosSum: number;
  twizosKg: number;
  twizosCashback: number;
  twizosSegment: Segment | null;
  gifts: string[];
  stand: boolean;
};
export type BattleStore = {
  marketId: number;
  name: string;
  order: number;
  status: Mark;
  avgCheck: number;
  hasOrder: boolean;
  visited: boolean;
  goal: { mogul: BattleMogulGoal | null; twizos: BattleTwizosGoal | null } | null;
  fact: BattleFact | null;
};
export type BattleOutside = { marketId: number; name: string; kg: number; sum: number; gifts: string[]; cashback: number; fact: BattleFact | null };
export type BattleTotals = {
  stores: number;
  mogulGoals: number;
  twizosGoals: number;
  plain: number;
  both: number;
  woken: number;
  twizosClients: number;
  twizosNew: number;
  twizosKg: number;
  mogulCashback: number;
  twizosCashback: number;
  mogulGoalCashback: number;
  twizosGoalCashback: number;
  mogulLeft: number;
  twizosLeft: number;
  gifts: Record<string, number>;
  stands: number;
  passed: number;
  refused: number[];
  share: number;
  cashback: number;
};
export type BattleRoute = {
  agent: BattleAgent;
  date: string;
  route: { stores: BattleStore[]; outOfRoute: BattleOutside[] | null; routePoints: number; source: string | null };
  totals: BattleTotals;
  balances: BattleBalances;
};
export type BattleTeamRow = { agent: BattleAgent; totals: BattleTotals };
export type BattleLedgerRow = {
  orderId: number;
  orderDate: string;
  agentId: number;
  agent: string;
  marketId: number;
  market: string;
  kind: "mogul" | "twizos";
  segment: string;
  percent: number;
  listSum: number;
  netSum: number;
  issued: number;
  amount: number;
  status: "issued" | "confirmed" | "cancelled";
  deliveredOn: string | null;
  stand: boolean;
};

export type Segment = "existing" | "new" | "stand";
export type Mark = "ahead" | "taken" | "partial" | "refused";
export type CardSlot = "morning" | "midday" | "evening" | "week" | "month" | "team" | "zone";

/** Сегмент Twizos: подпись и цвет плашки (оранжевый 5%, зелёный 8%, фиолетовый 10% — как на карточках). */
export const SEGMENTS: Record<Segment, { label: string; className: string }> = {
  existing: { label: "действующий", className: "bg-[#E8731A] text-white" },
  new: { label: "новый", className: "bg-[#2E9E4F] text-white" },
  stand: { label: "новый + стойка", className: "bg-[#7B3FE4] text-white" },
};

/** Отметка цели: ✓ взял, ½ меньше цели, × отказ, «впереди». */
export const MARKS: Record<Mark, { label: string; className: string }> = {
  taken: { label: "✓ взял", className: "text-ok" },
  partial: { label: "½ меньше цели", className: "text-warn" },
  refused: { label: "× отказ", className: "text-bad" },
  ahead: { label: "впереди", className: "text-ink-3" },
};

/** Статус начисления кешбека. */
export const LEDGER_STATUS: Record<BattleLedgerRow["status"], string> = { issued: "ждёт доставки", confirmed: "доставлено", cancelled: "отменено" };

/** Набор «140x8+35x2» → «8 кор. 140 г + 2 кор. 35 г». */
export function setText(set: string): string {
  return set
    .split("+")
    .map((part) => part.trim().split(/[xх×*]/))
    .filter((bits) => bits.length === 2 && Number(bits[1]) > 0)
    .map(([box, count]) => `${Number(count)} кор. ${box.trim()} г`)
    .join(" + ");
}

/** Доля в процентах без знаков после запятой; 0 из 0 — 0. */
export function share(part: number, whole: number): number {
  return whole > 0 ? Math.round((part / whole) * 100) : 0;
}

/** Карточки, которые показываются выбранному ТП, и карточки руководителей. */
export const AGENT_CARDS: CardSlot[] = ["morning", "midday", "evening", "week", "month"];
export const TEAM_CARDS: CardSlot[] = ["team", "zone"];
