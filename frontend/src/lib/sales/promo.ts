// «Акции» (GET /api/sales/promo): ответ сервера (PromoView) — все блоки считает сервер (PromoCompute), страница рисует.

export type PromoMonthOption = { key: string; year: number; month: number; days: number; partial: boolean };

export type PromoTiles = {
  total: number;
  byPromos: number;
  manual: number;
  promos: number;
  out: number;
  pending: number;
  sumTo: number;
  sales: number | null;
  toSales: number | null;
  salesDay: number;
  days: number;
};

export type PromoKind = "discount" | "bonus" | "manual" | "none";

export type PromoByPromo = {
  id: number;
  name: string;
  kind: PromoKind;
  begin: string | null;
  till: string | null;
  regions: number;
  shops: number;
  qty: number;
  sum: number;
  share: number;
  out: number;
  selected: boolean;
};

export type PromoEffect = {
  id: number;
  name: string;
  status: "ok" | "long" | "nobase";
  regions: number;
  skus: number;
  baseKgDay: number | null;
  curKgDay: number | null;
  growth: number | null;
  controlGrowth: number | null;
  extraKg: number | null;
  discount: number;
  returnPerSum: number | null;
};

export type PromoShopCell = { id: number | null; name: string; purchase: number | null; promoSale: number; share: number | null; cost: number; discountPct: number | null };

export type PromoAgentPromo = {
  id: number;
  name: string;
  participants: number;
  avg: number;
  max: number;
  promoSale: number;
  cost: number;
  discountPct: number | null;
  shops: PromoShopCell[];
};

export type PromoAgentRow = {
  id: number | null;
  name: string;
  sales: number | null;
  promoSale: number;
  share: number | null;
  noPromo: number | null;
  mogul: number;
  akbPromo: number;
  cost: number;
  costPct: number | null;
  promos: PromoAgentPromo[];
};

export type PromoRegionRow = {
  name: string;
  rm: string | null;
  sales: number | null;
  growth: number | null;
  promoSale: number;
  share: number | null;
  noPromo: number | null;
  mogul: number;
  akbPromo: number;
  cost: number;
  costPct: number | null;
  agents: PromoAgentRow[];
};

export type PromoCalendarRow = {
  key: string;
  name: string;
  sub: string | null;
  sale: number[];
  discount: number[];
  qty: number[];
  akb: number[];
  akbMonth: number;
  children: PromoCalendarRow[];
};

export type PromoView = {
  asOf: string | null;
  months: PromoMonthOption[];
  month: PromoMonthOption;
  rmOptions: string[];
  rm: string | null;
  selected: number[];
  tiles: PromoTiles;
  attention: { agent: string; region: string; sum: number; pct: number; monthsWith: number; monthsTotal: number }[];
  conclusions: { text: string; args: Record<string, string> }[];
  byPromo: PromoByPromo[];
  selectedTotals: { regions: number; shops: number; qty: number; sum: number; share: number; out: number } | null;
  promoTotal: number;
  effectDay: number;
  effects: PromoEffect[];
  salesDay: number;
  regions: PromoRegionRow[];
  regionTotals: { sales: number; promoSale: number; share: number | null; mogul: number; akbPromo: number; cost: number; costPct: number | null };
  manualMonths: string[];
  manual: { agent: string; region: string; sum: number; sales: number | null; pct: number | null; shops: number; history: number[]; monthsWith: number }[];
  calendar: { days: number; sundays: number[]; byRegion: PromoCalendarRow[]; byPromo: PromoCalendarRow[]; total: PromoCalendarRow };
  agents: { agent: string; region: string; sum: number; manual: number; shops: number; share: number }[];
  agentCount: number;
  shops: { id: number | null; name: string; region: string; agent: string | null; sum: number; promos: string[] }[];
  shopCount: number;
  waitingMonths: string[];
  waiting: {
    id: number | null;
    name: string;
    region: string;
    agent: string | null;
    kind: "only" | "wait";
    monthsDiscount: number;
    monthsBuy: number;
    avgDiscountMonth: number;
    avgNoDiscount: number;
    discountToPurchase: number | null;
    purchases: (number | null)[];
    discountMonths: boolean[];
  }[];
  waitingOnly: number;
  waitingWait: number;
  products: { name: string; qty: number; sum: number; share: number }[];
  productCount: number;
  byMonth: { key: string; year: number; month: number; sales: number | null; sum: number; toSales: number | null; manual: number; current: boolean }[];
};

export const promoKindLabel: Record<PromoKind, string> = {
  discount: "скидка",
  bonus: "бонус товаром",
  manual: "ручная",
  none: "без акции",
};
