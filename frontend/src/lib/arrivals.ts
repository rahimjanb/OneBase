// Поставки (приезд машины): ожидаемые поставки по тендерам и приёмка в порядке «качество → склад». Чистые расчёты — с тестами
// (arrivals.test.mjs).
import type { BatchQuality, Page, QualityCheckStatus, QualityControl, RequestEvent } from "./procurement";

/** Поставка: на проверке качества, готово к приёмке, принято на склад, возврат поставщику (забраковано всё), отменена. */
export type ArrivalStatus = "Quality" | "Ready" | "Accepted" | "Returned" | "Cancelled";

export const arrivalStatusLabel: Record<ArrivalStatus, string> = {
  Quality: "На проверке качества",
  Ready: "Готово к приёмке",
  Accepted: "Принято на склад",
  Returned: "Возврат поставщику",
  Cancelled: "Отменена",
};

export const arrivalStatusTone: Record<ArrivalStatus, string> = {
  Quality: "bg-warn-soft text-warn",
  Ready: "bg-accent-soft text-accent-strong",
  Accepted: "bg-ok-soft text-ok",
  Returned: "bg-bad-soft text-bad",
  Cancelled: "bg-muted text-ink-3",
};

/**
 * Позиция ожидаемой поставки: заказано, поставлено, уже на приёмке (привезли, ещё не принято), осталось — в единице тендера; карточка
 * склада, коэффициент к её единице (null — не пересчитать), весовая ли (кг, г, т), партии, срок хранения, контроль качества.
 */
export type ExpectedItem = {
  /** Позиция тендера или заказа «Куплено». */
  lineId: string;
  lineNo: number;
  name: string;
  unitCode: string;
  quantity: number;
  received: number;
  onArrival: number;
  left: number;
  price: number;
  itemId: string | null;
  sku: string | null;
  itemUnitCode: string | null;
  factor: number | null;
  weighed: boolean;
  tracksBatches: boolean;
  shelfLifeDays: number | null;
  control: QualityControl;
};

/** Откуда поставка: тендер или покупка «Куплено — привезёт поставщик» (заказ PO-). */
export type ArrivalSource = "tender" | "order";

/**
 * Ждёт поставки — тендер или покупка «Куплено»: срок, просрочено, дней до срока, склад, остаток к поставке (сум), позиции, машины на
 * приёмке, может ли отметить приезд; у покупки — заявка.
 */
export type ExpectedDelivery = {
  kind: ArrivalSource;
  sourceId: string;
  number: string;
  title: string;
  supplierId: string;
  supplierName: string;
  deliveryDate: string | null;
  overdue: boolean;
  daysLeft: number | null;
  warehouseId: string;
  warehouseName: string;
  leftAmount: number;
  responsibleName: string;
  items: ExpectedItem[];
  openArrivals: number;
  canArrive: boolean;
  requestId?: string | null;
  requestNumber?: string | null;
};

export type ArrivalRow = {
  id: string;
  number: string;
  status: ArrivalStatus;
  sourceKind: ArrivalSource;
  sourceId: string;
  sourceNumber: string;
  sourceTitle: string;
  supplierId: string;
  supplierName: string;
  warehouseId: string;
  warehouseName: string;
  vehicleNo: string | null;
  reference: string | null;
  arrivedAt: string;
  registeredByName: string;
  lines: number;
  pending: number;
  rejected: number;
  acceptedAt: string | null;
  acceptedByName: string | null;
  receiptId: string | null;
  receiptNumber: string | null;
  acceptedTotal: number;
  summary: string;
};

/** Строка поставки: позиция и её карточка, по накладной, партия и сроки, проверка и решение, цена тендера, приёмка (места, вес, на склад, партия). */
export type ArrivalLine = {
  id: string;
  lineNo: number;
  lineId: string;
  itemId: string;
  sku: string;
  name: string;
  unitCode: string;
  itemUnitCode: string;
  factor: number | null;
  weighed: boolean;
  tracksBatches: boolean;
  declared: number;
  declaredBase: number;
  supplierBatchNo: string | null;
  manufacturedOn: string | null;
  expiresOn: string | null;
  control: QualityControl;
  qualityCheckId: string | null;
  qualityCheckNumber: string | null;
  checkStatus: QualityCheckStatus | null;
  quality: BatchQuality;
  decisionComment: string | null;
  sampleQty: number | null;
  price: number;
  left: number;
  acceptedQty: number | null;
  acceptedWeight: number | null;
  stockQty: number | null;
  batchId: string | null;
  batchNumber: string | null;
  comment: string | null;
};

export type ArrivalDetail = {
  arrival: ArrivalRow;
  driverName: string | null;
  comment: string | null;
  readyAt: string | null;
  cancelledAt: string | null;
  cancelReason: string | null;
  version: number;
  requestId: string | null;
  requestNumber: string | null;
  lines: ArrivalLine[];
  history: RequestEvent[];
  can: { accept: boolean; cancel: boolean };
};

export type ArrivalPage = Page<ArrivalRow>;

/** Когда ждать: просрочено, сегодня, завтра, в ближайшие 7 дней, позже, срок не указан. */
export type DueGroup = "overdue" | "today" | "tomorrow" | "week" | "later" | "none";

export const dueGroupLabel: Record<DueGroup, string> = {
  overdue: "Просрочено",
  today: "Сегодня",
  tomorrow: "Завтра",
  week: "На этой неделе",
  later: "Позже",
  none: "Срок не указан",
};

export function dueGroup(daysLeft: number | null): DueGroup {
  if (daysLeft === null) return "none";
  if (daysLeft < 0) return "overdue";
  if (daysLeft === 0) return "today";
  if (daysLeft === 1) return "tomorrow";
  return daysLeft <= 7 ? "week" : "later";
}

/** Ожидаемые поставки по группам срока — в порядке групп, внутри — как пришли с сервера (по сроку). Пустые группы не возвращаются. */
/** Карточка источника поставки: тендер или покупка (заказ). */
export const sourceHref = (kind: ArrivalSource, id: string) => (kind === "tender" ? `/procurement/tenders/${id}` : `/procurement/orders/${id}`);

export function byDue<T extends { daysLeft: number | null }>(rows: T[]): { group: DueGroup; rows: T[] }[] {
  const order: DueGroup[] = ["overdue", "today", "tomorrow", "week", "later", "none"];
  return order.map((group) => ({ group, rows: rows.filter((r) => dueGroup(r.daysLeft) === group) })).filter((g) => g.rows.length > 0);
}

const round3 = (n: number) => Math.round(n * 1000) / 1000;

/**
 * Сколько пойдёт на склад (в единице карточки): у весовой строки — вес нетто, иначе места × коэффициент (больше трёх знаков — null, как на
 * сервере). Мест 0 — ничего; не введено — null.
 */
export function stockOf(line: Pick<ArrivalLine, "weighed" | "factor">, places: number | null, weight: number | null): number | null {
  if (places === null || places < 0) return null;
  if (places === 0) return 0;
  if (line.weighed) return weight !== null && weight > 0 ? round3(weight) : null;
  const value = places * (line.factor ?? 1);
  return Math.abs(value - round3(value)) < 1e-9 ? round3(value) : null;
}

/** Расхождение с накладной (в единице карточки): минус — недостача, плюс — излишек, 0 — совпало; не посчитать — null. */
export function shortfall(declaredBase: number, stock: number | null): number | null {
  return stock === null ? null : round3(stock - declaredBase);
}

/** Сумма приёмки по цене тендера: на склад × цена за единицу карточки (цена тендера ÷ коэффициент, до двух знаков) — как на сервере. */
export function acceptedSum(rows: { stock: number | null; price: number; factor: number | null }[]): number {
  const money = (n: number) => Math.round(n * 100) / 100;
  return money(rows.reduce((sum, r) => sum + (r.stock ? money(r.stock * money(r.price / (r.factor ?? 1))) : 0), 0));
}

/** Кто пользователь в снабжении и производстве — какие блоки главной показывать. */
export type HomeRoles = {
  warehouse: boolean;
  quality: boolean;
  decidesQuality: boolean;
  supply: boolean;
  analyst: boolean;
  head: boolean;
  director: boolean;
  financier: boolean;
};

export type HomeCheck = {
  id: string;
  number: string;
  status: QualityCheckStatus;
  itemName: string;
  supplierName: string | null;
  arrivalId: string | null;
  arrivalNumber: string | null;
  batchNumber: string | null;
  received: number;
  unitCode: string;
  createdAt: string;
  atMyStep: boolean;
};

export type HomeApproval = { id: string; number: string; title: string; amount: number; department: string; initiator: string; submittedAt: string | null };

/**
 * Главная по ролям (/api/procurement/home): ближайшие поставки, машины на приёмке, принято на склад за неделю, качество (лаборатории и
 * назначенным), склад (кладовщику), заявки — согласования, отдел, свои, к закупке. Блок null — не по роли.
 */
export type HomeView = {
  roles: HomeRoles;
  seesDeliveries: boolean;
  upcoming: ExpectedDelivery[];
  upcomingTotal: number;
  dueToday: number;
  overdue: number;
  arrivals: ArrivalRow[];
  accepted: ArrivalRow[];
  acceptedCount: number;
  acceptedAmount: number;
  quality: { open: number; documents: number; lab: number; decision: number; inTrucks: number; atMyStep: number; checks: HomeCheck[] } | null;
  stock: { toIssue: number; belowMinimum: number; expiring: number; expired: number; onControl: number } | null;
  requests: {
    awaitingMe: number;
    approvals: HomeApproval[];
    departmentOpen: number;
    departmentToPickUp: number;
    mineToPickUp: number;
    toBuy: number | null;
    toBuyAmount: number | null;
  };
};

/** Подпись ролей пользователя на рабочем столе: «Кладовщик · Лаборатория». */
export function roleNames(r: HomeRoles): string[] {
  return [
    r.warehouse && "Кладовщик",
    (r.quality || r.decidesQuality) && "Лаборатория",
    r.supply && "Снабжение",
    r.analyst && "Аналитик",
    r.head && "Руководитель отдела",
    r.financier && "Финансист",
    r.director && "Директор",
  ].filter((x): x is string => Boolean(x));
}

/** Есть ли что показать над «Обзором» (руководителю, директору, финансисту): ждут решения, можно получить, принято на склад за неделю. */
export const hasAttention = (h: HomeView) =>
  h.requests.awaitingMe > 0 || h.requests.departmentToPickUp > 0 || h.requests.mineToPickUp > 0 || ((h.roles.director || h.roles.financier) && h.acceptedCount > 0);
