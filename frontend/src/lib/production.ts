import type { Page, RequestEvent } from "./procurement";

/** Вкладки «Производства»: кто что видит; open — открытые проверки качества, orders — задания к выдаче (выпущены и в работе). */
export type ProductionNav = { sees: boolean; recipes: boolean; produces: boolean; warehouse: boolean; quality: boolean; analytics: boolean; open: number; orders: number };

export type RecipeStatus = "Draft" | "Active" | "Archived";

export const recipeStatusLabel: Record<RecipeStatus, string> = { Draft: "Черновик", Active: "Действующая", Archived: "Архив" };

export const recipeStatusTone: Record<RecipeStatus, string> = {
  Draft: "bg-warn-soft text-warn",
  Active: "bg-ok-soft text-ok",
  Archived: "bg-muted text-ink-3",
};

/** Рецептура в списке: по коду — действующая версия (а нет — последняя); draftId — есть черновик новой версии. */
export type RecipeRow = {
  id: string;
  code: string;
  versionNo: number;
  name: string;
  outputName: string;
  outputUnit: string;
  outputQuantity: number;
  status: RecipeStatus;
  lines: number;
  activatedAt: string | null;
  draftId: string | null;
  versions: number;
};

export type RecipeList = { items: RecipeRow[]; total: number; canCreate: boolean };

export type RecipeLine = {
  id: string;
  lineNo: number;
  itemId: string;
  sku: string;
  name: string;
  unitCode: string;
  quantity: number;
  lossPercent: number;
  comment: string | null;
  itemActive: boolean;
  tracksBatches: boolean;
};

export type RecipeVersion = { id: string; versionNo: number; status: RecipeStatus; activatedAt: string | null; activatedByName: string | null; createdAt: string };

export type RecipeDetail = {
  id: string;
  code: string;
  versionNo: number;
  name: string;
  linkoProductId: number | null;
  linkoProductName: string | null;
  outputName: string;
  outputUnit: string;
  outputQuantity: number;
  status: RecipeStatus;
  comment: string | null;
  activatedAt: string | null;
  activatedByName: string | null;
  createdAt: string;
  version: number;
  lines: RecipeLine[];
  versions: RecipeVersion[];
  history: RequestEvent[];
  can: { edit: boolean; activate: boolean; newVersion: boolean; archive: boolean; order: boolean };
  orders: number;
};

export type RequirementLine = {
  itemId: string;
  sku: string;
  name: string;
  unitCode: string;
  norm: number;
  lossPercent: number;
  required: number;
  available: number;
  shortage: number;
};

export type Requirement = { recipeId: string; quantity: number; outputUnit: string; warehouseId: string | null; warehouseName: string; lines: RequirementLine[] };

/** Товар Linko для выхода рецептуры. */
export type ProductOption = { id: number; name: string; code: string | null; unit: string | null };

export type RecipeOption = { id: string; code: string; versionNo: number; name: string; outputName: string; outputUnit: string; outputQuantity: number };

export type ProductionOrderStatus = "Draft" | "Released" | "InProgress" | "Completed" | "Cancelled";

export const productionOrderStatusLabel: Record<ProductionOrderStatus, string> = {
  Draft: "Черновик",
  Released: "Выпущено",
  InProgress: "В работе",
  Completed: "Закрыто",
  Cancelled: "Отменено",
};

export const productionOrderStatusTone: Record<ProductionOrderStatus, string> = {
  Draft: "bg-muted text-ink-2",
  Released: "bg-accent-soft text-accent-strong",
  InProgress: "bg-warn-soft text-warn",
  Completed: "bg-ok-soft text-ok",
  Cancelled: "bg-muted text-ink-3",
};

export type ProductionOrderRow = {
  id: string;
  number: string;
  recipeId: string;
  recipeCode: string;
  recipeVersion: number;
  recipeName: string;
  outputUnit: string;
  plannedQty: number;
  outputQty: number | null;
  plannedDate: string;
  status: ProductionOrderStatus;
  departmentName: string;
  warehouseName: string;
  responsibleName: string | null;
  overconsumed: boolean;
};

export type ProductionOrderList = Page<ProductionOrderRow> & { canCreate: boolean; drafts: number; released: number; inProgress: number };

export type ProductionOrderLine = {
  id: string;
  lineNo: number;
  itemId: string;
  sku: string;
  name: string;
  unitCode: string;
  tracksBatches: boolean;
  norm: number;
  lossPercent: number;
  required: number;
  issued: number;
  returned: number;
  consumed: number;
  left: number;
  available: number;
  actualNorm: number | null;
  deviation: number | null;
  deviationPercent: number | null;
  overconsumed: boolean;
};

/** На руках у цеха по заданию: позиция и партия, выдано, возвращено, можно вернуть. */
export type ProductionHolding = {
  itemId: string;
  name: string;
  unitCode: string;
  batchId: string | null;
  batchNumber: string | null;
  expiresOn: string | null;
  issued: number;
  returned: number;
  left: number;
  supplierBatchNo: string | null;
};

export type ProductionDocument = {
  id: string;
  number: string;
  kind: "ProductionIssue" | "ProductionReturn";
  at: string;
  actorName: string | null;
  comment: string | null;
  receiver: string | null;
  moves: { itemName: string; unitCode: string; batchNumber: string | null; quantity: number }[];
};

export type ProductionOrderDetail = {
  id: string;
  number: string;
  recipeId: string;
  recipeCode: string;
  recipeVersion: number;
  recipeName: string;
  outputName: string;
  outputUnit: string;
  recipeOutput: number;
  departmentId: string;
  departmentName: string;
  warehouseId: string;
  warehouseName: string;
  plannedQty: number;
  plannedDate: string;
  status: ProductionOrderStatus;
  outputQty: number | null;
  responsibleId: string | null;
  responsibleName: string | null;
  comment: string | null;
  createdAt: string;
  releasedAt: string | null;
  completedAt: string | null;
  cancelledAt: string | null;
  version: number;
  thresholdPercent: number;
  lines: ProductionOrderLine[];
  holdings: ProductionHolding[];
  documents: ProductionDocument[];
  history: RequestEvent[];
  can: { edit: boolean; release: boolean; issue: boolean; return: boolean; complete: boolean; cancel: boolean };
};

export type Named = { id: string; name: string };

export type ProductionOrderForm = { recipes: RecipeOption[]; departments: Named[]; warehouses: Named[]; people: Named[] };

export type CountValue = { count: number; value: number };

export type ProductionOverview = {
  from: string;
  to: string;
  warehouseId: string | null;
  stock: { warehouseId: string; name: string; items: number; value: number }[];
  stockValue: number;
  onControl: CountValue;
  oldestControlDays: number;
  expiring7: CountValue;
  expiring30: CountValue;
  expired: CountValue;
  decided: number;
  rejected: number;
  rejectSuppliers: { name: string; decided: number; rejected: number; percent: number }[];
  belowMin: number;
  closedOrders: number;
  normTotal: number;
  actualTotal: number;
  overconsumed: number;
  deviations: { orderId: string; orderNumber: string; item: string; unitCode: string; norm: number; actual: number; deviation: number; percent: number | null; overconsumed: boolean }[];
  lowDays: { itemId: string; sku: string; name: string; unitCode: string; onHand: number; perDay: number; days: number }[];
  supplyApproval: number;
  directorApproval: number;
  approvalAmount: number;
  prices: { name: string; unitCode: string; firstPrice: number; lastPrice: number; changePercent: number | null }[];
  openOrders: number;
  overdueOrders: number;
};

/** Дата отметки времени по Ташкенту «06.11.2026» (dateRu берёт дату как есть — для отметок времени ночью она была бы вчерашней). */
export const dayRu = (iso: string | null | undefined) =>
  iso ? new Intl.DateTimeFormat("ru-RU", { timeZone: "Asia/Tashkent", day: "2-digit", month: "2-digit", year: "numeric" }).format(new Date(iso)) : "—";

/** Потребность на план: норма × (план ÷ выход) × (1 + потери ÷ 100), до 0,001 (половина — вверх). Как на сервере. */
export function requirement(norm: number, planned: number, output: number, lossPercent: number): number {
  if (output <= 0) return 0;
  const value = (norm * planned * (100 + lossPercent)) / (output * 100);
  return Math.round((value + Number.EPSILON) * 1000) / 1000;
}

/** Отклонение факта от нормы: количество (плюс — перерасход) и процент от нормы до 0,1; нормы нет — процента нет. */
export function deviation(actual: number, norm: number): { quantity: number; percent: number | null } {
  const quantity = Math.round((actual - norm) * 1000) / 1000;
  return { quantity, percent: norm === 0 ? null : Math.round(((actual - norm) / norm) * 1000) / 10 };
}

/** Сколько ещё выдать до потребности (не меньше нуля). */
export const leftToIssue = (required: number, issued: number, returned: number) => Math.max(0, Math.round((required - (issued - returned)) * 1000) / 1000);

/** Подпись процента отклонения: «+15,6 %», «−10 %», без нормы — «—». */
export function percentLabel(percent: number | null): string {
  if (percent === null) return "—";
  const text = Math.abs(percent).toLocaleString("ru-RU", { maximumFractionDigits: 1 });
  return `${percent > 0 ? "+" : percent < 0 ? "−" : ""}${text} %`;
}
