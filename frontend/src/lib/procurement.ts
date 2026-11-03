/** Раздел «Заявки» (снабжение): заявки на закупку с позициями, категориями, приоритетом, вложениями и историей (API /api/procurement/*). */

export type RequestStatus =
  | "Draft"
  | "Submitted"
  | "HeadApproval"
  | "FinanceApproval"
  | "DirectorApproval"
  | "Approved"
  | "Rejected"
  | "RevisionRequired"
  | "PartiallyFulfilled"
  | "Fulfilled"
  | "Cancelled";

export type RequestPriority = "Low" | "Normal" | "High" | "Urgent";

export type ItemStatus = "Pending" | "Cancelled" | "Reserved" | "ToPurchase" | "PartiallyIssued" | "Issued" | "Ordered" | "Received";

export type RequestRow = {
  id: string;
  number: string;
  title: string;
  status: RequestStatus;
  priority: RequestPriority;
  departmentId: string;
  departmentCode: string;
  departmentName: string;
  initiatorId: string;
  initiatorName: string;
  itemsCount: number;
  firstItem: string | null;
  categories: string[];
  estimatedTotal: number;
  desiredDate: string | null;
  createdAt: string;
  submittedAt: string | null;
  attachments: number;
  mine: boolean;
  /** Сумма к закупке (часть «к закупке» × цена) — то, что согласует директор. */
  purchaseTotal: number;
  /** Раунд согласования: 2+ — повторное после существенной правки. */
  approvalRound: number;
  forwardedAt: string | null;
  reason: string | null;
};

export type RequestItem = {
  id: string;
  lineNo: number;
  name: string;
  categoryId: string;
  categoryName: string;
  unitCode: string;
  unitName: string;
  quantity: number;
  estimatedPrice: number | null;
  sum: number | null;
  comment: string | null;
  status: ItemStatus;
  /** Товар каталога склада; null — своё наименование (всё к закупке). */
  itemId: string | null;
  sku: string | null;
  fromStock: number;
  toPurchase: number;
  issuedQty: number;
  purchaseReason: string | null;
  /** Доступно на складе сейчас (только у товара каталога). */
  available: number | null;
  /** Ещё в резерве под эту позицию (не выдано). */
  reservedLeft: number;
  /** Заказано у поставщиков (часть «к закупке»). */
  orderedQty: number;
  /** Закупленное, принятое на склад (подтверждённые приёмки). */
  receivedQty: number;
};

export type RequestDocument = { id: string; fileName: string; contentType: string; sizeBytes: number; uploadedByName: string | null; createdAt: string; canDelete: boolean };

export type RequestEvent = {
  id: string;
  action: string;
  text: string;
  actorName: string;
  fromStatus: RequestStatus | null;
  toStatus: RequestStatus | null;
  comment: string | null;
  createdAt: string;
};

export type RequestAbilities = {
  edit: boolean;
  submit: boolean;
  forward: boolean;
  return: boolean;
  cancel: boolean;
  attach: boolean;
  /** Решение согласующего текущего шага (руководитель отдела, финансист, директор): согласовать, отклонить, вернуть. */
  approve: boolean;
  /** Финансист при согласовании уточняет цены позиций к закупке. */
  price: boolean;
  /** Выдача со склада (кладовщик). */
  issue: boolean;
  /** Правка утверждённой заявки снабжением — существенное изменение уйдёт директору повторно. */
  approvedEdit: boolean;
  /** Добавлять и сравнивать предложения поставщиков (снабжение). */
  offer: boolean;
  /** Заказать у поставщика: есть утверждённое, ещё не заказанное (прежний путь; на экране не показывается). */
  order: boolean;
  /** «Куплено» (снабжение): заявка утверждена, есть что купить. */
  purchase: boolean;
};

export type ApprovalDecision = "Approved" | "Rejected" | "Revision";
export type ApprovalStage = "head" | "finance" | "director";
export type RequestApproval = {
  id: string;
  round: number;
  decision: ApprovalDecision;
  comment: string | null;
  deciderName: string;
  purchaseTotal: number;
  createdAt: string;
  stage: ApprovalStage;
};

/**
 * Шаг маршрута в карточке: руководитель отдела, финансист, директор, снабжение. state: done — согласовал, current — решает сейчас, pending —
 * впереди, skipped — не нужен (reason: initiatorHead, noHeads, noPurchase, underLimit), rejected, returned. who — кто решил или кто решает;
 * пусто у финансиста и директора — не выбран в настройках.
 */
export type RouteStep = {
  key: ApprovalStage | "supply";
  state: "done" | "current" | "pending" | "skipped" | "rejected" | "returned";
  who: string[];
  at: string | null;
  comment: string | null;
  reason: "initiatorHead" | "noHeads" | "noPurchase" | "underLimit" | null;
};

export type RequestDetail = {
  id: string;
  number: string;
  title: string;
  status: RequestStatus;
  priority: RequestPriority;
  departmentId: string;
  departmentCode: string;
  departmentName: string;
  initiatorId: string;
  initiatorName: string;
  initiatorPosition: string | null;
  desiredDate: string | null;
  reason: string | null;
  comment: string | null;
  estimatedTotal: number;
  createdAt: string;
  updatedAt: string | null;
  submittedAt: string | null;
  forwardedAt: string | null;
  cancelledAt: string | null;
  version: number;
  revisionComment: string | null;
  items: RequestItem[];
  documents: RequestDocument[];
  history: RequestEvent[];
  can: RequestAbilities;
  purchaseTotal: number;
  approvalRound: number;
  approvedAt: string | null;
  approvedByName: string | null;
  approvals: RequestApproval[];
  offersCount: number;
  orders: RequestOrder[];
  /** Маршрут согласования текущего раунда; у заявок прежнего порядка (через снабжение) — null. */
  route: RouteStep[] | null;
  /** Лимит покупки без директора, сум (из «Настройки → Отделы → Снабжение»). */
  directorLimit: number;
};

export type RequestCategory = { id: string; name: string; code: string | null; sortOrder: number; isActive: boolean; used: number };
export type RequestUnit = { code: string; name: string };
export type RequestDepartment = { id: string; code: string; name: string };

export type RequestFormData = { departments: RequestDepartment[]; defaultDepartmentId: string | null; categories: RequestCategory[]; units: RequestUnit[] };

export type RequestOverview = {
  byStatus: Record<RequestStatus, number>;
  open: number;
  urgentOpen: number;
  monthRequests: number;
  monthTotal: number;
  monthCategories: { name: string; requests: number; total: number }[];
  needsAction: RequestRow[];
  recent: RequestRow[];
};

export type RequestNav = {
  canCreate: boolean;
  readsAll: boolean;
  manages: boolean;
  configures: boolean;
  heads: boolean;
  needsAction: number;
  /** Роль «Директор»: утверждает заявки. */
  isDirector: boolean;
  /** Кладовщик: операции склада. */
  warehouse: boolean;
  /** Раздел «Снабжение» (склад) открыт. */
  seesStock: boolean;
  awaitingApproval: number;
  toIssue: number;
  /** Кладовщику: заказов, которые ждут приёмки. */
  toReceive: number;
  /** Решает шаг маршрута: руководитель отдела, финансист или директор — видит «Ждут моего согласования». */
  decides: boolean;
};

// ---------- Склад ----------

export type StockRow = {
  itemId: string;
  sku: string;
  name: string;
  categoryId: string;
  categoryName: string;
  unitCode: string;
  onHand: number;
  reserved: number;
  available: number;
  minQty: number;
  low: boolean;
  isActive: boolean;
  barcode: string | null;
  description: string | null;
  lastMovementAt: string | null;
};

export type StockSummary = { items: number; low: number; empty: number; withReserve: number; toIssueRequests: number };

export type CatalogOption = { id: string; sku: string; name: string; categoryId: string; categoryName: string; unitCode: string; onHand: number; available: number };

export type MovementKind = "Receipt" | "Issue" | "WriteOff" | "Adjustment";
export type StockDocumentKind = "Receipt" | "Issue" | "WriteOff" | "Inventory";

export type MovementRow = {
  id: string;
  createdAt: string;
  documentId: string;
  documentNumber: string;
  documentKind: StockDocumentKind;
  itemId: string;
  sku: string;
  itemName: string;
  unitCode: string;
  kind: MovementKind;
  quantity: number;
  balanceBefore: number;
  balanceAfter: number;
  unitCost: number | null;
  actorName: string | null;
  departmentName: string | null;
  requestId: string | null;
  requestNumber: string | null;
  receiver: string | null;
  reference: string | null;
  comment: string | null;
  /** Приход по заказу поставщику (приёмка): документ — приёмка, заказ — источник. */
  orderId: string | null;
  orderNumber: string | null;
};

export type ItemCard = {
  item: StockRow;
  movements: MovementRow[];
  reservations: { requestId: string; requestNumber: string; requestTitle: string; departmentName: string; quantity: number; createdAt: string }[];
};

export type ToIssue = {
  requestId: string;
  number: string;
  title: string;
  status: RequestStatus;
  departmentName: string;
  initiatorName: string;
  desiredDate: string | null;
  submittedAt: string | null;
  lines: { requestItemId: string; itemId: string; sku: string; name: string; unitCode: string; left: number; onHand: number }[];
};

export type Page<T> = { items: T[]; total: number; page: number; pageSize: number };

export const movementKindLabel: Record<MovementKind, string> = { Receipt: "Приход", Issue: "Выдача", WriteOff: "Списание", Adjustment: "Инвентаризация" };

export const approvalDecisionLabel: Record<ApprovalDecision, string> = { Approved: "Согласована", Rejected: "Отклонена", Revision: "Возвращена на доработку" };

export type RequestPage = { items: RequestRow[]; total: number; page: number; pageSize: number };

export const requestStatuses: RequestStatus[] = [
  "Draft",
  "HeadApproval",
  "FinanceApproval",
  "DirectorApproval",
  "Submitted",
  "RevisionRequired",
  "Approved",
  "PartiallyFulfilled",
  "Fulfilled",
  "Rejected",
  "Cancelled",
];

export const requestStatusLabel: Record<RequestStatus, string> = {
  Draft: "Черновик",
  Submitted: "У снабжения",
  HeadApproval: "Ожидает руководителя",
  FinanceApproval: "Ожидает финансиста",
  DirectorApproval: "Ожидает директора",
  Approved: "Утверждена",
  Rejected: "Отклонена",
  RevisionRequired: "Требуется доработка",
  PartiallyFulfilled: "Частично выполнена",
  Fulfilled: "Выполнена",
  Cancelled: "Отменена",
};

export const requestPriorities: RequestPriority[] = ["Low", "Normal", "High", "Urgent"];

export const requestPriorityLabel: Record<RequestPriority, string> = { Low: "Низкий", Normal: "Обычный", High: "Высокий", Urgent: "Срочный" };

export const itemStatusLabel: Record<ItemStatus, string> = {
  Pending: "Ожидает",
  Cancelled: "Отменена",
  Reserved: "Зарезервирована",
  ToPurchase: "К закупке",
  PartiallyIssued: "Выдана частично",
  Issued: "Выдана",
  Ordered: "Заказана",
  Received: "Поступила на склад",
};

/** Этапы заявки для полосы в карточке: черновик → согласование (любой шаг маршрута и доработка) → утверждена → выполнена. */
export const requestStages = ["Черновик", "Согласование", "Утверждена", "Выполнена"] as const;

/** Шаги согласования: заявка ждёт решения руководителя отдела, финансиста или директора (и прежний шаг «у снабжения»). */
export const approvalStatuses: RequestStatus[] = ["HeadApproval", "FinanceApproval", "DirectorApproval", "Submitted"];

/** Номер этапа статуса на полосе (0…3); -1 — заявка вне полосы (отклонена, отменена). */
export function stageIndex(status: RequestStatus): number {
  if (status === "Draft") return 0;
  if (status === "RevisionRequired" || approvalStatuses.includes(status)) return 1;
  if (status === "Approved" || status === "PartiallyFulfilled") return 2;
  if (status === "Fulfilled") return 3;
  return -1;
}

/** Группы статусов фильтра списка — то же, что принимает /api/procurement/requests. */
export const requestStatusGroups = [
  { value: "", label: "Все статусы" },
  { value: "open", label: "В работе" },
  { value: "draft", label: "Черновики" },
  { value: "closed", label: "Закрытые" },
] as const;

/** Параметры списка заявок из адреса страницы. */
export const requestFilterKeys = ["q", "status", "priority", "category", "department", "from", "to", "sort", "desc", "page"] as const;

/** «1 250 000» — число с пробелами по разрядам (до двух знаков после запятой). */
export function money(value: number | null | undefined): string {
  if (value === null || value === undefined) return "—";
  const rounded = Math.round(value * 100) / 100;
  const [int, frac] = Math.abs(rounded).toFixed(2).split(".");
  const grouped = int.replace(/\B(?=(\d{3})+(?!\d))/g, " ");
  const tail = frac === "00" ? "" : `,${frac.replace(/0$/, "")}`;
  return `${rounded < 0 ? "−" : ""}${grouped}${tail}`;
}

/** Количество: до трёх знаков после запятой, без лишних нулей («2,5», «20»). */
export function quantity(value: number): string {
  const rounded = Math.round(value * 1000) / 1000;
  const [int, frac] = String(rounded).split(".");
  return `${int.replace(/\B(?=(\d{3})+(?!\d))/g, " ")}${frac ? `,${frac}` : ""}`;
}

/** Число из поля ввода: запятая или точка, пробелы между разрядами; пусто или не число — null. */
export function parseNumber(text: string): number | null {
  const clean = text.replace(/\s/g, "").replace(",", ".");
  if (clean === "") return null;
  const n = Number(clean);
  return Number.isFinite(n) ? n : null;
}

/**
 * Строка позиции в форме. itemId — товар каталога склада (категория и единица — из каталога); available — доступно на складе, когда
 * товар выбрали; fromStock — сколько взять со склада (fromStockTouched — пользователь менял сам, тогда не подставляем); purchaseReason —
 * почему купить, хотя на складе есть.
 */
export type ItemDraft = {
  key: string;
  id: string | null;
  name: string;
  categoryId: string;
  unitCode: string;
  quantity: string;
  price: string;
  comment: string;
  itemId?: string | null;
  sku?: string | null;
  available?: number | null;
  fromStock?: string;
  fromStockTouched?: boolean;
  purchaseReason?: string;
  /** Уже выдано по позиции — меньше не сделать, товар не сменить. */
  issued?: number;
};

/** Сколько можно взять со склада для позиции: не больше доступного и количества. */
export function stockCover(item: Pick<ItemDraft, "quantity" | "available" | "itemId">): number {
  if (!item.itemId || item.available === null || item.available === undefined) return 0;
  const q = parseNumber(item.quantity) ?? 0;
  return Math.max(0, Math.min(item.available, q));
}

/** Части позиции: со склада и к закупке (по введённым числам). */
export function split(item: Pick<ItemDraft, "quantity" | "fromStock" | "itemId">): { fromStock: number; toPurchase: number } {
  const q = parseNumber(item.quantity) ?? 0;
  const s = item.itemId ? Math.min(q, Math.max(0, parseNumber(item.fromStock ?? "") ?? 0)) : 0;
  return { fromStock: s, toPurchase: Math.max(0, Math.round((q - s) * 1000) / 1000) };
}

/** Сумма позиции формы: количество × цена; нет цены или количества — null. */
export function lineSum(item: Pick<ItemDraft, "quantity" | "price">): number | null {
  const q = parseNumber(item.quantity);
  const p = parseNumber(item.price);
  return q === null || p === null ? null : Math.round(q * p * 100) / 100;
}

/** Сумма к закупке формы: часть «к закупке» × цена. */
export function purchaseTotal(items: Pick<ItemDraft, "quantity" | "price" | "fromStock" | "itemId">[]): number {
  return Math.round(items.reduce((sum, item) => sum + split(item).toPurchase * (parseNumber(item.price) ?? 0), 0) * 100) / 100;
}

/** Предполагаемая сумма формы — по позициям с ценой. */
export function draftTotal(items: Pick<ItemDraft, "quantity" | "price">[]): number {
  return Math.round(items.reduce((sum, item) => sum + (lineSum(item) ?? 0), 0) * 100) / 100;
}

/**
 * Проверка формы до отправки на сервер (сервер проверяет то же самое): название, хотя бы одна позиция, у каждой — наименование,
 * категория, единица, количество больше нуля (до трёх знаков), цена не отрицательная (до двух знаков); причина — при отправке.
 * Ошибка — текст на русском (переводится в интерфейсе), иначе null.
 */
export function checkDraft(draft: { title: string; reason: string; items: ItemDraft[] }, submit: boolean): string | null {
  if (!draft.title.trim()) return "Укажите название заявки.";
  const items = draft.items.filter((i) => i.name.trim() || i.quantity.trim() || i.price.trim());
  if (items.length === 0) return "Добавьте хотя бы одну позицию.";
  for (const [index, item] of items.entries()) {
    const n = index + 1;
    if (!item.name.trim()) return `Позиция ${n}: укажите наименование.`;
    if (!item.categoryId) return `Позиция ${n}: выберите категорию.`;
    if (!item.unitCode) return `Позиция ${n}: выберите единицу измерения.`;
    const q = parseNumber(item.quantity);
    if (q === null || q <= 0) return `Позиция ${n}: количество должно быть больше нуля.`;
    if (Math.round(q * 1000) / 1000 !== q) return `Позиция ${n}: количество — не больше трёх знаков после запятой.`;
    if (item.itemId) {
      const s = parseNumber(item.fromStock ?? "") ?? 0;
      if (s < 0 || s > q) return `Позиция ${n}: «со склада» — от 0 до количества позиции.`;
      if (item.available !== null && item.available !== undefined && s > item.available + (item.issued ?? 0))
        return `Позиция ${n}: на складе доступно ${quantity(item.available)} — уменьшите «со склада».`;
      if (submit && s < stockCover(item) && !(item.purchaseReason ?? "").trim())
        return `Позиция ${n}: на складе есть ${quantity(item.available ?? 0)} — возьмите со склада или обоснуйте закупку.`;
    }
    if (item.price.trim()) {
      const p = parseNumber(item.price);
      if (p === null || p < 0) return `Позиция ${n}: цена — не отрицательное число.`;
      if (Math.round(p * 100) / 100 !== p) return `Позиция ${n}: цена — не больше двух знаков после запятой.`;
    }
    // Цена обязательна у части к закупке: по сумме заявки решается, нужен ли директор.
    if (submit && split(item).toPurchase > 0 && !((parseNumber(item.price) ?? 0) > 0))
      return `Позиция ${n}: укажите примерную цену — по сумме заявки решается, нужен ли директор.`;
  }
  if (submit && !draft.reason.trim()) return "Укажите причину закупки — без неё заявку не отправить.";
  return null;
}

/** «12.10.2026» из «2026-10-12…». */
export const dateRu = (iso: string | null | undefined) => (iso ? iso.slice(0, 10).split("-").reverse().join(".") : "—");

/** «12.10.2026 14:05» по Ташкенту. */
export function stampRu(iso: string | null | undefined): string {
  if (!iso) return "—";
  const parts = new Intl.DateTimeFormat("ru-RU", { timeZone: "Asia/Tashkent", day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" }).format(
    new Date(iso),
  );
  return parts.replace(",", "");
}

// ---------- Поставщики, предложения, заказы ----------

export type SupplierRow = {
  id: string;
  name: string;
  inn: string | null;
  contactPerson: string | null;
  phone: string | null;
  categories: string[];
  isActive: boolean;
  orders: number;
  ordersTotal: number;
  overdue: number;
  rating: number | null;
  lastOrderAt: string | null;
  leadTimeDays: number | null;
};

export type SupplierRating = {
  overall: number | null;
  onTime: number | null;
  quality: number | null;
  prices: number | null;
  dueOrders: number;
  overdueOrders: number;
  ratedOffers: number;
  pricedLines: number;
  /** Доля строк приёмки без брака и недостачи (0–100). */
  claims: number | null;
  /** Доля заказанного, что поставлено, по завершённым заказам (0–100). */
  completeness: number | null;
  receiptLines: number;
  claimLines: number;
  finishedOrders: number;
  lateOrders: number;
};

export type OrderStatus = "Draft" | "AwaitingApproval" | "Approved" | "Ordered" | "PartiallyDelivered" | "Delivered" | "Completed" | "Cancelled";

export type SupplierCard = {
  id: string;
  name: string;
  inn: string | null;
  contactPerson: string | null;
  phone: string | null;
  email: string | null;
  address: string | null;
  bankName: string | null;
  bankMfo: string | null;
  bankAccount: string | null;
  paymentTerms: string | null;
  leadTimeDays: number | null;
  notes: string | null;
  isActive: boolean;
  categories: { id: string; name: string }[];
  rating: SupplierRating;
  orders: { id: string; number: string; status: OrderStatus; orderDate: string | null; expectedDate: string | null; total: number; overdue: boolean; lines: number }[];
  prices: { date: string; requestItemId: string; itemName: string; unitCode: string; unitPrice: number; quantity: number; orderNumber: string; orderId: string }[];
  offers: number;
  ordersTotal: number;
  canEdit: boolean;
  claims: Claim[];
};

export type ProcDocument = RequestDocument & { kind: string | null };

export type OfferLine = { requestItemId: string; name: string; unitCode: string; quantity: number; unitPrice: number; grossPrice: number; sum: number; note: string | null; bestPrice: boolean };

export type OfferStatus = "Proposed" | "Selected" | "Rejected";

export type Offer = {
  id: string;
  supplierId: string;
  supplierName: string;
  supplierRating: number | null;
  supplierOrders: number;
  supplierOverdue: number;
  validUntil: string | null;
  expired: boolean;
  deliveryDays: number | null;
  deliveryCost: number;
  vatRate: number;
  pricesIncludeVat: boolean;
  paymentTerms: string | null;
  warranty: string | null;
  quality: number | null;
  comment: string | null;
  status: OfferStatus;
  subtotal: number;
  vat: number;
  total: number;
  covered: number;
  bestTotal: boolean;
  fastest: boolean;
  lines: OfferLine[];
  documents: ProcDocument[];
  createdByName: string | null;
  createdAt: string;
  orders: { id: string; number: string; status: OrderStatus }[];
};

export type Comparison = {
  requestId: string;
  number: string;
  title: string;
  status: RequestStatus;
  items: { requestItemId: string; name: string; unitCode: string; toPurchase: number; orderedQty: number; estimatedPrice: number | null }[];
  offers: Offer[];
  canEdit: boolean;
  canOrder: boolean;
};

export type OrderRow = {
  id: string;
  number: string;
  status: OrderStatus;
  supplierId: string;
  supplierName: string;
  responsibleId: string;
  responsibleName: string;
  lines: number;
  firstItem: string | null;
  requests: string[];
  total: number;
  orderDate: string | null;
  expectedDate: string | null;
  overdue: boolean;
  createdAt: string;
  approvalRound: number;
};

export type OrderSummary = { open: number; draft: number; awaitingApproval: number; overdue: number; expectedWeek: number; partial: number; cancelledMonth: number; openTotal: number };

export type OrderItem = {
  id: string;
  lineNo: number;
  requestItemId: string;
  requestId: string;
  requestNumber: string;
  name: string;
  unitCode: string;
  quantity: number;
  unitPrice: number;
  sum: number;
  receivedQty: number;
  estimatedPrice: number | null;
  /** Закрыто без поставки (недопоставленный остаток). */
  closedQty: number;
};

export type OrderDetail = {
  id: string;
  number: string;
  status: OrderStatus;
  supplierId: string;
  supplierName: string;
  supplierPhone: string | null;
  offerId: string | null;
  responsibleId: string;
  responsibleName: string;
  orderDate: string | null;
  expectedDate: string | null;
  overdue: boolean;
  paymentTerms: string | null;
  discount: number;
  vatRate: number;
  pricesIncludeVat: boolean;
  deliveryCost: number;
  subtotal: number;
  vatAmount: number;
  total: number;
  comment: string | null;
  approvalRound: number;
  approvedAt: string | null;
  approvedByName: string | null;
  createdAt: string;
  createdByName: string | null;
  version: number;
  taskId: string | null;
  items: OrderItem[];
  approvals: { id: string; round: number; decision: ApprovalDecision; comment: string | null; deciderName: string; total: number; createdAt: string }[];
  documents: ProcDocument[];
  history: RequestEvent[];
  can: {
    edit: boolean;
    submit: boolean;
    approve: boolean;
    return: boolean;
    markOrdered: boolean;
    cancel: boolean;
    attach: boolean;
    receive: boolean;
    closeRemainder: boolean;
    complete: boolean;
    resolveClaims: boolean;
  };
  approvalReasons: string | null;
  receipts: OrderReceipt[];
  claims: Claim[];
  deliveredAt: string | null;
  completedAt: string | null;
};

// ---------- Приёмка ----------

export type ReceiptStatus = "Draft" | "Posted";
export type ClaimStatus = "None" | "Open" | "Resolved";

export type OrderReceipt = {
  id: string;
  number: string;
  status: ReceiptStatus;
  reference: string | null;
  actorName: string | null;
  createdAt: string;
  postedAt: string | null;
  problems: boolean;
};

/** Претензия поставщику: строка приёмки с браком или недостачей. */
export type Claim = {
  lineId: string;
  receiptId: string;
  receiptNumber: string;
  orderId: string;
  orderNumber: string;
  name: string;
  unitCode: string;
  defective: number;
  shortage: number;
  amount: number;
  comment: string | null;
  status: ClaimStatus;
  resolution: string | null;
  postedAt: string | null;
  resolvedAt: string | null;
};

/** Заказ, который ждёт приёмки. */
export type ExpectedRow = {
  orderId: string;
  number: string;
  status: OrderStatus;
  supplierId: string;
  supplierName: string;
  responsibleName: string;
  expectedDate: string | null;
  overdue: boolean;
  lines: number;
  waiting: number;
  requests: string[];
  drafts: number;
};

export type ReceiptRow = {
  id: string;
  number: string;
  status: ReceiptStatus;
  orderId: string;
  orderNumber: string;
  supplierId: string;
  supplierName: string;
  reference: string | null;
  actorName: string | null;
  createdAt: string;
  postedAt: string | null;
  lines: number;
  problems: boolean;
  openClaims: number;
};

/** Строка заказа для приёмки: заказано, получено, закрыто, ждёт поставки; карточка склада позиции заявки (если есть). */
export type ReceiptOrderLine = {
  orderItemId: string;
  lineNo: number;
  name: string;
  unitCode: string;
  ordered: number;
  received: number;
  closed: number;
  remaining: number;
  unitPrice: number;
  requestId: string;
  requestNumber: string;
  categoryId: string;
  itemId: string | null;
  itemSku: string | null;
  itemName: string | null;
};

export type ReceiptForm = {
  orderId: string;
  orderNumber: string;
  orderStatus: OrderStatus;
  supplierId: string;
  supplierName: string;
  expectedDate: string | null;
  overdue: boolean;
  canReceive: boolean;
  lines: ReceiptOrderLine[];
};

export type ReceiptLine = {
  id: string;
  lineNo: number;
  orderItemId: string;
  name: string;
  unitCode: string;
  ordered: number;
  unitPrice: number;
  requestId: string;
  requestNumber: string;
  itemId: string | null;
  itemSku: string | null;
  itemName: string | null;
  received: number;
  defective: number;
  shortage: number;
  accepted: number;
  comment: string | null;
  claimStatus: ClaimStatus;
  claimResolution: string | null;
  claimResolvedAt: string | null;
  claimResolvedByName: string | null;
};

export type ReceiptDetail = {
  id: string;
  number: string;
  status: ReceiptStatus;
  orderId: string;
  orderNumber: string;
  orderStatus: OrderStatus;
  supplierId: string;
  supplierName: string;
  reference: string | null;
  comment: string | null;
  actorName: string | null;
  createdAt: string;
  postedAt: string | null;
  version: number;
  lines: ReceiptLine[];
  documents: ProcDocument[];
  history: RequestEvent[];
  can: { edit: boolean; post: boolean; delete: boolean; attach: boolean; resolveClaims: boolean };
  /** Строки заказа — у черновика (для правки). */
  form: ReceiptForm | null;
};

export const receiptStatusLabel: Record<ReceiptStatus, string> = { Draft: "Черновик", Posted: "Подтверждена" };

/**
 * Строка приёмки — как на сервере: принято на склад = пришло − брак; брак не больше пришедшего; принять больше, чем ждёт поставки,
 * нельзя (лишнее не принимается). Возвращает принятое и текст ошибки (или null).
 */
export function receiptLine(name: string, unit: string, received: number, defective: number, shortage: number, remaining: number): { accepted: number; error: string | null } {
  for (const [value, label] of [
    [received, "пришло"],
    [defective, "брак"],
    [shortage, "недостача"],
  ] as const) {
    if (value < 0 || Math.round(value * 1000) / 1000 !== value) return { accepted: 0, error: `«${name}»: ${label} — не меньше нуля, не больше трёх знаков после запятой.` };
  }
  if (defective > received) return { accepted: 0, error: `«${name}»: брак (${quantity(defective)}) больше, чем пришло (${quantity(received)}).` };
  const accepted = Math.round((received - defective) * 1000) / 1000;
  if (accepted > remaining)
    return { accepted: 0, error: `«${name}»: ждёт поставки ${quantity(remaining)} ${unit}, принять ${quantity(accepted)} нельзя — лишнее не принимается.` };
  return { accepted, error: null };
}

export type Need = {
  requestItemId: string;
  requestId: string;
  requestNumber: string;
  requestTitle: string;
  departmentName: string;
  name: string;
  unitCode: string;
  toPurchase: number;
  ordered: number;
  remaining: number;
  estimatedPrice: number | null;
  categoryName: string;
};

/** Заказ поставщику по заявке; direct — покупка «Куплено»: кто купил (responsibleName) и когда (deliveredAt). */
export type RequestOrder = {
  id: string;
  number: string;
  status: OrderStatus;
  supplierName: string;
  total: number;
  expectedDate: string | null;
  direct: boolean;
  deliveredAt: string | null;
  responsibleName: string | null;
};

/** Позиция «к закупке»: сколько осталось купить, согласованная цена за единицу. */
export type ToBuyLine = { requestItemId: string; lineNo: number; name: string; categoryName: string; unitCode: string; left: number; price: number | null; sku: string | null; comment: string | null };

/** Утверждённая заявка, по которой снабжению осталось купить; overdue — желаемая дата прошла; total — остаток к закупке, сум. */
export type ToBuy = {
  requestId: string;
  number: string;
  title: string;
  priority: RequestPriority;
  status: RequestStatus;
  departmentName: string;
  initiatorName: string;
  desiredDate: string | null;
  approvedAt: string | null;
  overdue: boolean;
  total: number;
  lines: ToBuyLine[];
};

export type ToBuyView = { canBuy: boolean; requests: ToBuy[] };

/** Что получилось после «Куплено»: покупка (PO-), приход на склад (RC-), сумма. */
export type PurchaseResult = { orderId: string; orderNumber: string; receiptId: string; receiptNumber: string; total: number };

export const orderStatuses: OrderStatus[] = ["Draft", "AwaitingApproval", "Approved", "Ordered", "PartiallyDelivered", "Delivered", "Completed", "Cancelled"];

export const orderStatusLabel: Record<OrderStatus, string> = {
  Draft: "Черновик",
  AwaitingApproval: "Ждёт директора",
  Approved: "Утверждён",
  Ordered: "Заказан",
  PartiallyDelivered: "Поставлен частично",
  Delivered: "Поставлен",
  Completed: "Закрыт",
  Cancelled: "Отменён",
};

export const documentKindLabel: Record<string, string> = {
  contract: "Договор",
  offer: "Коммерческое предложение",
  invoice: "Счёт",
  waybill: "Накладная",
  act: "Акт",
  payment: "Платёжное поручение",
  protocol: "Протокол тендера",
  other: "Документ",
};

/** Тендерная закупка: договор заключён — ждёт поставки; поставлено; отменена. */
export type TenderStatus = "Contracted" | "Delivered" | "Cancelled";

export const tenderStatusLabel: Record<TenderStatus, string> = { Contracted: "Ждёт поставки", Delivered: "Поставлено", Cancelled: "Отменена" };

/** Строка журнала тендерных закупок; overdue — срок поставки прошёл, а поставки нет. */
export type TenderRow = {
  id: string;
  number: string;
  title: string;
  tenderNo: string | null;
  tenderDate: string;
  supplierId: string;
  supplierName: string;
  categoryName: string;
  status: TenderStatus;
  deliveryDate: string | null;
  deliveredOn: string | null;
  overdue: boolean;
  total: number;
  lines: number;
  firstItem: string | null;
  responsibleName: string;
};

/** Журнал: страница и итоги — сумма по фильтру (без отменённых), ждут поставки и просрочены — по всему журналу. */
export type TenderList = Page<TenderRow> & { amount: number; waiting: number; overdue: number; canCreate: boolean };

export type TenderItem = { id: string; lineNo: number; name: string; unitCode: string; quantity: number; price: number; sum: number };

export type TenderDetail = {
  id: string;
  number: string;
  title: string;
  tenderNo: string | null;
  tenderDate: string;
  supplierId: string;
  supplierName: string;
  categoryId: string;
  categoryName: string;
  contractNo: string | null;
  participants: number | null;
  deliveryDate: string | null;
  status: TenderStatus;
  deliveredOn: string | null;
  cancelledAt: string | null;
  overdue: boolean;
  total: number;
  comment: string | null;
  responsibleName: string;
  createdAt: string;
  version: number;
  items: TenderItem[];
  documents: ProcDocument[];
  history: RequestEvent[];
  can: { edit: boolean; deliver: boolean; cancel: boolean; attach: boolean };
};

/** Итоги заказа или предложения — как на сервере: позиции − скидка (не ниже нуля) + НДС сверху (если цены без НДС) + доставка. */
export function orderTotals(lines: { quantity: number; price: number }[], discount: number, vatRate: number, pricesIncludeVat: boolean, delivery: number) {
  const round = (n: number) => Math.round(n * 100) / 100;
  const subtotal = round(lines.reduce((s, l) => s + round(l.quantity * l.price), 0));
  const net = Math.max(0, subtotal - discount);
  const vat = pricesIncludeVat ? 0 : round((net * vatRate) / 100);
  return { subtotal, vat, total: round(net + vat + delivery) };
}

/** Рейтинг поставщика 0–100 → «4,2 из 5»; нет данных — «—». */
export const ratingText = (rating: number | null) => (rating === null ? "—" : (Math.round((rating / 20) * 10) / 10).toString().replace(".", ","));

// ---------- Обзор, KPI, отчёты, сводки ----------

export type Kpis = {
  avgPurchaseDays: number | null;
  purchasesMeasured: number;
  onTimePercent: number | null;
  deliveriesMeasured: number;
  savings: number;
  savingsPercent: number | null;
  savingsLines: number;
  fulfilledRequests: number;
  claims: number;
  supplierQuality: number | null;
  suppliersRated: number;
};

export type Amount = { key: string; label: string; amount: number; count: number };
export type SupplierScore = { id: string; name: string; rating: number; orders: number };
export type OrderBrief = { id: string; number: string; supplierName: string; responsibleName: string; status: OrderStatus; expectedDate: string | null; total: number; overdue: boolean };

export type DirectorBoard = {
  orderedTotal: number;
  orderedCount: number;
  receivedTotal: number;
  awaitingRequests: number;
  awaitingRequestsTotal: number;
  awaitingOrders: number;
  awaitingOrdersTotal: number;
  activeOrders: number;
  activeTotal: number;
  overdueOrders: number;
  overdue: OrderBrief[];
  byDepartment: Amount[];
  byCategory: Amount[];
  byMonth: Amount[];
  best: SupplierScore[];
  worst: SupplierScore[];
};

export type StaffRow = {
  userId: string;
  name: string;
  openTasks: number;
  overdueTasks: number;
  activeOrders: number;
  overdueOrders: number;
  delivered: number;
  avgToOrderDays: number | null;
  avgDeliveryDays: number | null;
  load: number;
};

export type HeadBoard = {
  waitingOrder: number;
  ordersInWork: number;
  overdueOrders: number;
  overdueTasks: number;
  avgToOrderDays: number | null;
  avgDeliveryDays: number | null;
  staff: StaffRow[];
};

export type Deadline = { kind: "task" | "order"; id: string; title: string; due: string; overdue: boolean; link: string };

export type BuyerBoard = {
  myRequestsOpen: number;
  myOrders: number;
  myOrdersTotal: number;
  myTasks: number;
  myTasksOverdue: number;
  urgent: OrderBrief[];
  deadlines: Deadline[];
};

export type WarehouseBoard = {
  expected: number;
  expectedSoon: number;
  drafts: number;
  receiptsWeek: number;
  stock: StockSummary;
  expectedList: ExpectedRow[];
  recent: ReceiptRow[];
  low: { id: string; sku: string; name: string; unitCode: string; available: number; minQty: number }[];
  toIssue: { requestId: string; number: string; title: string; departmentName: string; lines: number }[];
};

export type DigestRow = { id: string; day: string; title: string; summary: string; createdAt: string };

export type Dashboard = {
  from: string;
  to: string;
  kpis: Kpis | null;
  director: DirectorBoard | null;
  head: HeadBoard | null;
  buyer: BuyerBoard | null;
  warehouse: WarehouseBoard | null;
  digests: DigestRow[] | null;
};

export type DigestView = {
  id: string;
  day: string;
  title: string;
  summary: string;
  createdAt: string;
  recipients: number;
  sections: { title: string; count: number; items: { text: string; link: string | null }[] }[];
  previousId: string | null;
  nextId: string | null;
};

export type ReportInfo = { key: string; title: string; description: string };
export type ReportColumn = { header: string; format: "text" | "integer" | "number" | "kg" | "money" | "percent" | "date"; sum: boolean };
export type ReportSheet = { name: string; columns: ReportColumn[]; rows: (string | number | boolean | null)[][]; totalRows: number; total: boolean; note: string | null };
export type ReportPreview = { title: string; sources: string[]; sheets: ReportSheet[] };

/** Ячейка отчёта для экрана — как в Excel и PDF: деньги и количества с пробелами, проценты «27,8 %», даты «дд.мм.гггг». */
export function reportCell(value: string | number | boolean | null, format: ReportColumn["format"]): string {
  if (value === null || value === undefined || value === "") return "";
  if (typeof value === "boolean") return value ? "✓" : "";
  if (typeof value === "number") {
    if (format === "percent") return `${value.toFixed(1).replace(".", ",")} %`;
    if (format === "integer") return money(Math.round(value));
    return format === "money" ? money(value) : quantity(value);
  }
  return format === "date" ? dateRu(value) : value;
}

/** Обзор снабжения (GET /api/procurement/board): действия, путь закупки, показатели, аналитика, сотрудники — по правам роли; null — не видно. */
export type Board = {
  from: string;
  to: string;
  access: { canApprove: boolean; canCreateRequest: boolean; canCreateOrder: boolean; canManageSuppliers: boolean; canSeeTeam: boolean; seesRequests: boolean; seesMetrics: boolean };
  summary: { pendingApprovalCount: number | null; pendingApprovalAmount: number | null; overdueDeliveries: number | null; overdueTasks: number | null };
  /**
   * Путь закупки: на согласовании (число и сумма), к закупке (requestsAwaitingOrder — заявок, toBuyAmount — остаток по согласованным ценам,
   * overdueToBuy — с прошедшей желаемой датой), на складе к выдаче (toIssue). Заказы до отправки, в пути и приёмка — прежний путь.
   */
  pipeline: {
    requestsAwaitingOrder: number | null;
    onApprovalCount: number | null;
    onApprovalAmount: number | null;
    ordersInProgress: number;
    inTransit: number;
    awaitingReceipt: number;
    toBuyAmount: number | null;
    overdueToBuy: number | null;
    toIssue: number;
  };
  approvals: { id: string; number: string; title: string; amount: number; department: string; initiator: string; submittedAt: string | null }[] | null;
  ordersOnApproval: number;
  upcoming: { id: string; number: string; supplierName: string; expectedDate: string }[] | null;
  metrics: { purchasedAmount: number; ordersCount: number; receivedAmount: number; kpis: Kpis } | null;
  analytics: {
    monthly: Amount[];
    byDepartment: Amount[];
    byCategory: Amount[];
    suppliers: { id: string; name: string; orders: number; total: number; onTime: number | null; claims: number; rating: number | null }[];
  } | null;
  /** Сотрудники снабжения; bought — куплено за период (покупки и поставленные заказы). */
  team: { userId: string; name: string; role: string; tasks: number; orders: number; overdue: number; load: number; loadLevel: "free" | "low" | "high"; bought: number }[] | null;
  digests: DigestRow[] | null;
  empty: boolean;
  errors: string[];
  /** Сколько заявок ждут решения самого пользователя (approvals — первые три из них). */
  myApprovalCount: number;
};

/** Период обзора: этот месяц, прошлый месяц, 3 месяца, год — даты «гггг-мм-дд» (Ташкент). */
export function periodOf(preset: "month" | "prev" | "quarter" | "year", today: string): { from: string; to: string } {
  const [y, m] = today.split("-").map(Number);
  const iso = (d: Date) => `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, "0")}-${String(d.getUTCDate()).padStart(2, "0")}`;
  if (preset === "month") return { from: `${today.slice(0, 7)}-01`, to: today };
  if (preset === "prev") return { from: iso(new Date(Date.UTC(y, m - 2, 1))), to: iso(new Date(Date.UTC(y, m - 1, 0))) };
  if (preset === "quarter") return { from: iso(new Date(Date.UTC(y, m - 3, 1))), to: today };
  return { from: iso(new Date(Date.UTC(y - 1, m - 1, Number(today.slice(8, 10)) + 1))), to: today };
}

/** Крупные суммы компактно: 1 250 000 → «1,3 млн», 12 900 → «12,9 тыс.»; единицы — на языке интерфейса (units). */
export function compactMoney(value: number, units: { bn: string; mn: string; th: string } = { bn: "млрд", mn: "млн", th: "тыс." }): string {
  const abs = Math.abs(value);
  const short = (n: number) => n.toFixed(1).replace(/\.0$/, "").replace(".", ",");
  if (abs >= 1_000_000_000) return `${short(value / 1_000_000_000)} ${units.bn}`;
  if (abs >= 1_000_000) return `${short(value / 1_000_000)} ${units.mn}`;
  if (abs >= 10_000) return `${short(value / 1_000)} ${units.th}`;
  return money(value);
}
