// «Продажи → ТП → День»: типы ответа /api/sales/agents/{id}/day и правила показа (статусы точек, минуты, соседние дни, ссылка на карту).
// Без интерфейса — проверяется тестами (agent-day.test.mjs).

export type AgentDayLine = { productId: number | null; product: string; qty: number; kg: number; sum: number };

export type AgentDayOrder = { id: number; at: string; status: string; sum: number; kg: number; lines: AgentDayLine[] };

/** done — визит выполнен; missed — точка маршрута прошедшего дня без визита; planned — ещё впереди; no_visit — заказ без визита. */
export type AgentDayStatus = "done" | "missed" | "planned" | "no_visit";

export type AgentDayStop = {
  no: number;
  marketId: number;
  market: string;
  address: string | null;
  marketType: string | null;
  lat: number | null;
  lon: number | null;
  status: AgentDayStatus;
  inPlan: boolean;
  /** «HH:mm:ss» — отметка визита Linko (момент закрытия визита или оформления заказа). */
  at: string | null;
  /** Минут с прошлой отметки — время на прошлой точке вместе с дорогой. */
  gapMinutes: number | null;
  /** Какой по счёту визит ТП в эту точку после её последнего заказа у него. */
  visitNumber: number | null;
  orders: AgentDayOrder[];
  sum: number;
  kg: number;
  lastOrderDay: string | null;
  usual: string[];
  usualNotOrdered: string[];
};

export type AgentDaySummary = {
  planned: number;
  done: number;
  doneInPlan: number;
  offPlan: number;
  missed: number;
  ahead: number;
  withOrder: number;
  strike: number | null;
  orders: number;
  sum: number;
  kg: number;
  ordersWithoutVisit: number;
  cancelled: number;
  firstMark: string | null;
  lastMark: string | null;
  spanMinutes: number | null;
  medianGapMinutes: number | null;
  longestGapMinutes: number | null;
  longestGapTo: string | null;
};

export type AgentDayView = {
  agentId: number;
  agent: string;
  day: string;
  summary: AgentDaySummary;
  stops: AgentDayStop[];
  notes: string[];
  visitsSyncedAt: string | null;
  ordersSyncedAt: string | null;
};

export const STOP_STATUS: Record<AgentDayStatus, { label: string; className: string }> = {
  done: { label: "визит", className: "bg-ok-soft text-ok" },
  missed: { label: "не посетил", className: "bg-bad-soft text-bad" },
  planned: { label: "впереди", className: "bg-muted text-ink-2" },
  no_visit: { label: "заказ без визита", className: "bg-warn-soft text-warn" },
};

/** «09:05» из «09:05:00»; нет — null. */
export const hm = (at: string | null) => (at ? at.slice(0, 5) : null);

/** Минуты по-человечески: «45 мин», «1 ч 05 мин», «2 ч». */
export function minutes(value: number | null | undefined, t: (s: string, v?: Record<string, string | number>) => string = (s, v) => fill(s, v)): string {
  if (value == null) return "—";
  if (value < 60) return t("{n} мин", { n: value });
  const h = Math.floor(value / 60);
  const m = value % 60;
  return m === 0 ? t("{h} ч", { h }) : t("{h} ч {m} мин", { h, m: String(m).padStart(2, "0") });
}

const fill = (s: string, v?: Record<string, string | number>) => (v ? s.replace(/\{(\w+)\}/g, (_, k) => String(v[k] ?? "")) : s);

/** Соседний день yyyy-mm-dd (без часовых поясов: календарная арифметика). */
export function shiftDay(day: string, by: number): string {
  const [y, m, d] = day.split("-").map(Number);
  const date = new Date(Date.UTC(y, m - 1, d + by));
  return date.toISOString().slice(0, 10);
}

/** «пн, 16.11.2026». */
export function dayTitle(day: string, t: (s: string) => string = (s) => s): string {
  const [y, m, d] = day.split("-").map(Number);
  const weekday = ["вс", "пн", "вт", "ср", "чт", "пт", "сб"][new Date(Date.UTC(y, m - 1, d)).getUTCDay()];
  return `${t(weekday)}, ${String(d).padStart(2, "0")}.${String(m).padStart(2, "0")}.${y}`;
}

/** Точка на Яндекс Картах; координат нет — null. */
export const mapUrl = (s: Pick<AgentDayStop, "lat" | "lon">) => (s.lat != null && s.lon != null ? `https://yandex.uz/maps/?pt=${s.lon},${s.lat}&z=17&l=map` : null);

/** Позже отчётного дня (сегодня по Ташкенту) день не открывается: ссылки «вперёд» нет. */
export const canGoForward = (day: string, today: string) => day < today;
