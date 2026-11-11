import type { ItemUnit, Page, RequestEvent } from "./procurement";

/** Вкладки «Производства»: кто что видит; open — открытые проверки качества, orders — задания к выдаче (выпущены и в работе). */
export type ProductionNav = { sees: boolean; recipes: boolean; produces: boolean; warehouse: boolean; quality: boolean; analytics: boolean; open: number; orders: number };

/** Версия карты: черновик → на утверждении → утверждена (действующая) → архив. */
export type RecipeStatus = "Draft" | "PendingApproval" | "Active" | "Archived";

export const recipeStatusLabel: Record<RecipeStatus, string> = { Draft: "Черновик", PendingApproval: "На утверждении", Active: "Утверждена", Archived: "Архив" };

export const recipeStatusTone: Record<RecipeStatus, string> = {
  Draft: "bg-warn-soft text-warn",
  PendingApproval: "bg-violet-soft text-violet-light",
  Active: "bg-ok-soft text-ok",
  Archived: "bg-muted text-ink-3",
};

/** Тип операции этапа. Замес — компоненты на замес, только первым этапом; упаковка — материалы на штуку, упаковку, коробку. */
export type StageKind = "Mixing" | "Baking" | "Filling" | "Glazing" | "Adding" | "Cooling" | "Packing" | "Other";

export const stageKinds: StageKind[] = ["Mixing", "Baking", "Filling", "Glazing", "Adding", "Cooling", "Packing", "Other"];

export const stageKindLabel: Record<StageKind, string> = {
  Mixing: "Замес",
  Baking: "Выпечка",
  Filling: "Начинка",
  Glazing: "Глазурь",
  Adding: "Добавки",
  Cooling: "Охлаждение",
  Packing: "Упаковка",
  Other: "Другое",
};

/** База норматива брака этапа: от обработанного (компоненты этапа потрачены), от поступившего (отсеян до обработки), от готовой. */
export type DefectBase = "Output" | "Input" | "Finished";

export const defectBaseLabel: Record<DefectBase, string> = { Output: "от обработанного на этапе", Input: "от поступившего на этап", Finished: "от готовой продукции" };

/** База нормы компонента. */
export type LineBasis = "Output" | "Batch" | "Piece" | "Pack" | "Box";

export const lineBasisLabel: Record<LineBasis, string> = { Output: "на выход", Batch: "на замес", Piece: "на штуку", Pack: "на упаковку", Box: "на коробку" };

/** Базы нормы для типа этапа (как проверяет сервер): замес — только на замес; упаковка — на упаковку, коробку, штуку, выход; прочие — на выход и штуку. */
export function basesFor(kind: StageKind | null): LineBasis[] {
  if (kind === "Mixing") return ["Batch"];
  if (kind === "Packing") return ["Pack", "Box", "Piece", "Output"];
  return ["Output", "Piece"];
}

/** Кг в единице массы (кг, г, т); не масса — null. */
export const massFactor = (unit: string): number | null => ({ кг: 1, г: 0.001, т: 1000 })[unit as "кг"] ?? null;

/** Кг в единице строки: масса; единица закупки позиции в кг-позиции («мешок = 50 кг»); иначе — масса единицы из карты. Как на сервере. */
export function lineKg(unit: string, itemUnit: string, itemUnits: ItemUnit[], unitKg: number | null): number | null {
  const mass = massFactor(unit);
  if (mass !== null) return mass;
  const purchase = itemUnits.find((u) => u.unitCode === unit);
  const itemMass = massFactor(itemUnit);
  if (purchase && itemMass !== null) return purchase.factor * itemMass;
  return unitKg;
}

/** Единицы, в которых можно задать норму: единица позиции, другие единицы массы (если позиция в массе) и её единицы закупки. */
export function unitChoices(itemUnit: string, itemUnits: ItemUnit[]): string[] {
  const list = [itemUnit];
  if (massFactor(itemUnit) !== null) list.push(...["кг", "г", "т"].filter((u) => u !== itemUnit));
  list.push(...itemUnits.map((u) => u.unitCode).filter((u) => !list.includes(u)));
  return list;
}

/** Масса замеса — сумма рецептуры замеса в кг; строка без массы — null (масса неизвестна). */
export function batchMass(lines: { quantity: number | null; kg: number | null }[]): number | null {
  let total = 0;
  for (const l of lines) {
    if (l.quantity === null || l.kg === null) return null;
    total += l.quantity * l.kg;
  }
  return Math.round(total * 1000) / 1000;
}

/** Типовая цепочка для начала — технолог меняет, убирает и добавляет этапы сам. */
export const typicalChain: { name: string; kind: StageKind; yieldPercent: number }[] = [
  { name: "Замес", kind: "Mixing", yieldPercent: 100 },
  { name: "Выпечка", kind: "Baking", yieldPercent: 87.5 },
  { name: "Начинка", kind: "Filling", yieldPercent: 100 },
  { name: "Глазурь", kind: "Glazing", yieldPercent: 100 },
  { name: "Охлаждение", kind: "Cooling", yieldPercent: 100 },
  { name: "Упаковка", kind: "Packing", yieldPercent: 100 },
];

/**
 * Фото продукта техкарты — одно на код (у всех версий): id — адрес картинки (/api/production/recipe-photos/{id}/480|960|1600), размер кадра 4:3,
 * средний цвет — подложка, пока картинка грузится.
 */
export type RecipePhoto = { id: string; width: number; height: number; tint: string | null };

/** Сколько карточек в каждом фильтре состояния (с учётом поиска). */
export type RecipeCounts = { all: number; active: number; draft: number; pending: number; archived: number };

/** Карточка продукта в списке: по коду — действующая версия (а нет — последняя); draftId и draftStatus — неутверждённая версия (черновик или на утверждении). */
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
  techCardNo: string | null;
  technologistName: string | null;
  effectiveFrom: string | null;
  stages: number;
  draftStatus: RecipeStatus | null;
  photo?: RecipePhoto | null;
  /** Последнее изменение любой версии кода — для «Сначала новые». */
  updatedAt?: string | null;
};

/** canCreate — ведёт техкарты: создаёт, импортирует, меняет фото. */
export type RecipeList = { items: RecipeRow[]; total: number; canCreate: boolean; counts?: RecipeCounts | null };

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
  stageId: string | null;
  basis: LineBasis;
  group: string | null;
  /** Единица нормы (по умолчанию — единица позиции). */
  lineUnit: string;
  unitKg: number | null;
  category: string | null;
  itemUnits: ItemUnit[] | null;
};

/** Этап версии: норматив брака — сумма видов брака продукта этапа. */
export type RecipeStage = {
  id: string;
  stageNo: number;
  name: string;
  kind: StageKind;
  area: string | null;
  yieldPercent: number;
  yieldMinPercent: number | null;
  yieldMaxPercent: number | null;
  defectBase: DefectBase;
  defectPercent: number;
  moistureNorm: number | null;
  moistureUnit: string | null;
  moistureTolerance: number | null;
  reworkable: boolean;
  comment: string | null;
};

/** Вид брака: брак продукта (itemId пусто) или материала этапа. */
export type DefectType = { id: string; stageId: string; name: string; normPercent: number; reworkable: boolean; itemId: string | null; itemName: string | null; comment: string | null };

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
  /** photo — менять фото продукта (ведёт техкарты), в любом статусе версии. */
  can: { edit: boolean; submit: boolean; approve: boolean; return: boolean; newVersion: boolean; archive: boolean; order: boolean; photo?: boolean };
  orders: number;
  techCardNo: string | null;
  technologistId: string | null;
  technologistName: string | null;
  effectiveFrom: string | null;
  submittedAt: string | null;
  submittedByName: string | null;
  batchMinKg: number | null;
  batchMaxKg: number | null;
  partialBatches: boolean;
  pieceKg: number | null;
  packKg: number | null;
  packsPerBox: number | null;
  stages: RecipeStage[];
  defectTypes: DefectType[];
  /** Ошибки карты: пока они есть, расчёт невозможен и на утверждение не отправить. */
  problems: string[];
  standardBatchKg: number | null;
  productCode: string | null;
  productGroup: string | null;
  /** Кто может утвердить версию на утверждении (кроме автора). */
  approvers: string[];
  /** Позиция склада выхода: выход приходуется на склад OneBase при закрытии задания (полуфабрикат; готовая продукция — по решению). */
  outputItemId: string | null;
  outputItemName: string | null;
  outputItemUnit: string | null;
  photo?: RecipePhoto | null;
};

/** Форма карты: единицы и технологи. */
export type RecipeFormData = { units: { code: string; name: string }[]; technologists: Named[] };

/** Этап в расчёте: обработано, годное и брак — в единицах готовой продукции; массы, кг (null — масса потока неизвестна). */
export type CalcStage = {
  no: number;
  name: string;
  kind: StageKind;
  yieldPercent: number;
  defectPercent: number;
  defectBase: DefectBase;
  processed: number;
  good: number;
  rejected: number;
  inputKg: number | null;
  addedKg: number | null;
  outputKg: number | null;
  defectKg: number | null;
  hasActual: boolean;
  /** Потери массы на этапе (влага, налипание) и годное дальше — посчитаны сервером до округления: «дальше» = вход следующего этапа. */
  lossKg?: number | null;
  nextKg?: number | null;
};

export type CalcBatchPlan = {
  standardKg: number;
  minKg: number;
  maxKg: number;
  needKg: number;
  batches: { no: number; massKg: number; partial: boolean }[];
  totalKg: number;
  surplusKg: number;
  fromActual: boolean;
};

/** Компонент в расчёте: база (выход, замесов, штук, упаковок, коробок), чистый расход, потери, норма (в единице строки и склада). */
export type CalcLine = {
  key: string;
  itemId: string;
  name: string;
  stageNo: number | null;
  basis: LineBasis;
  unit: string;
  base: number;
  net: number;
  loss: number;
  gross: number;
  itemUnit: string;
  itemGross: number;
  materialDefect: number | null;
  withDefects: number | null;
};

export type CalcResult = {
  quantity: number;
  outputUnit: string;
  byResults: boolean;
  stages: CalcStage[];
  batches: CalcBatchPlan | null;
  lines: CalcLine[];
  items: { itemId: string; name: string; unit: string; quantity: number }[];
  problems: string[];
  missing: string[];
};

/** Отличие версий: раздел, что, поле, было и стало. */
export type RecipeChange = { section: string; subject: string; field: string; before: string | null; after: string | null };

export type RecipeComparison = { leftId: string; leftVersion: number; rightId: string; rightVersion: number; changes: RecipeChange[] };

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

export type Requirement = {
  recipeId: string;
  quantity: number;
  outputUnit: string;
  warehouseId: string | null;
  warehouseName: string;
  lines: RequirementLine[];
  calculation: CalcResult | null;
};

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
  can: { edit: boolean; release: boolean; issue: boolean; return: boolean; complete: boolean; cancel: boolean; receive: boolean };
  /** Снимок планового расчёта на план задания (этапы, замесы, компоненты). */
  plan: CalcResult | null;
  /** Сверка «план — норма на факт — по результатам смен — факт склада — взвешено — отклонение»; нет выхода — null. */
  check: ProductionCheck | null;
  /** Выход на склад OneBase (позиция склада выхода версии): сколько, документ прихода и партия. */
  /** Выход на склад; unitCost — цена партии: материалы задания по цене выданных партий на единицу выхода. */
  output: { itemId: string; itemName: string; unitCode: string; quantity: number | null; documentNumber: string | null; batchNumber: string | null; unitCost: number | null } | null;
  shiftsOpen: number;
  shiftsGoodOutput: number | null;
};

export type ProductionCheckLine = {
  itemId: string;
  name: string;
  unitCode: string;
  plan: number;
  normOnFact: number | null;
  byResults: number | null;
  fact: number;
  weighed: number | null;
  deviation: number | null;
  deviationPercent: number | null;
};

export type ProductionCheck = { output: number; outputSource: "shifts" | "completion"; shifts: number; missing: string[]; lines: ProductionCheckLine[]; byResults: CalcResult | null };

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
  /** Заявки на сырьё на согласовании по шагам: у руководителя снабжения (прежний маршрут), аналитика, финансиста, директора. */
  supplyApproval: number;
  directorApproval: number;
  analystApproval?: number;
  financeApproval?: number;
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

/**
 * Поток этапа в кг по расчёту: весь вход (с компонентами), добавлено компонентами, потери массы (выход этапа меньше входа — влага, налипание),
 * брак, дальше — годное на следующий этап. Потери и «дальше» — с сервера (до округления); в старых снимках их нет — считаются из округлённых:
 * брак на входе отсеян до обработки (потери — от обработанного), брак после — из выхода. Масса потока неизвестна — null.
 */
export function stageFlow(s: CalcStage): { inKg: number; addedKg: number; lossKg: number; defectKg: number; nextKg: number } | null {
  if (s.inputKg === null || s.outputKg === null || s.defectKg === null || s.addedKg === null) return null;
  const r = (n: number) => Math.round(n * 1000) / 1000;
  const atInput = s.defectBase === "Input";
  return {
    inKg: s.inputKg,
    addedKg: s.addedKg,
    lossKg: s.lossKg ?? r(s.inputKg - (atInput ? s.defectKg : 0) - s.outputKg),
    defectKg: s.defectKg,
    nextKg: s.nextKg ?? r(atInput ? s.outputKg : s.outputKg - s.defectKg),
  };
}

/** Смена: ввод → закрыта (ждёт утверждения) → утверждена; исправленная корректировкой — заменена; отменена. */
export type ShiftStatus = "Draft" | "Closed" | "Approved" | "Superseded" | "Cancelled";

export const shiftStatusLabel: Record<ShiftStatus, string> = { Draft: "Ввод", Closed: "Закрыта", Approved: "Утверждена", Superseded: "Заменена", Cancelled: "Отменена" };

export const shiftStatusTone: Record<ShiftStatus, string> = {
  Draft: "bg-warn-soft text-warn",
  Closed: "bg-violet-soft text-violet-light",
  Approved: "bg-ok-soft text-ok",
  Superseded: "bg-muted text-ink-3",
  Cancelled: "bg-muted text-ink-3",
};

export type ReworkStatus = "Forbidden" | "Allowed" | "Sent";

export const reworkLabel: Record<ReworkStatus, string> = { Forbidden: "Запрещён к переработке", Allowed: "Разрешён к переработке", Sent: "Направлен на переработку" };

export type ShiftStageInput = {
  stageNo: number;
  done: boolean;
  inputKg: number | null;
  outputKg: number | null;
  moisture: number | null;
  reworkKg: number | null;
  reason: string | null;
  comment: string | null;
};

export type ShiftDefectInput = { stageNo: number; defectTypeId: string | null; name: string | null; quantityKg: number; rework: ReworkStatus | null };

export type ShiftMaterialKind = "Weighed" | "Defect";

export type ShiftMaterial = { itemId: string; name: string; unitCode: string; kind: ShiftMaterialKind; quantity: number; stageNo: number | null };

export type ShiftStageInfo = {
  no: number;
  name: string;
  kind: StageKind;
  yieldPercent: number;
  yieldMinPercent: number | null;
  yieldMaxPercent: number | null;
  defectPercent: number;
  moistureNorm: number | null;
  moistureUnit: string | null;
  moistureTolerance: number | null;
  reworkable: boolean;
  defectTypes: { id: string; name: string; normPercent: number; reworkable: boolean; itemId: string | null; itemName: string | null }[];
  materials: { itemId: string; name: string; unitCode: string; basis: LineBasis }[];
};

export type ShiftDetail = {
  id: string;
  number: string;
  kind: "Regular" | "Correction";
  correctsId: string | null;
  correctsNumber: string | null;
  correctionId: string | null;
  orderId: string;
  orderNumber: string;
  recipeId: string;
  recipeCode: string;
  recipeVersion: number;
  recipeName: string;
  outputUnit: string;
  plannedQty: number;
  departmentName: string;
  shiftDate: string;
  shiftNo: number;
  line: string | null;
  operatorId: string;
  operatorName: string;
  supervisorId: string | null;
  supervisorName: string | null;
  status: ShiftStatus;
  goodOutput: number | null;
  packs: number | null;
  boxes: number | null;
  comment: string | null;
  reason: string | null;
  closedAt: string | null;
  closedByName: string | null;
  approvedAt: string | null;
  approvedByName: string | null;
  version: number;
  standardBatchKg: number | null;
  template: ShiftStageInfo[];
  batches: number[];
  stages: ShiftStageInput[];
  defects: ShiftDefectInput[];
  materials: ShiftMaterial[];
  /** Чего не хватает, чтобы закрыть смену. */
  problems: string[];
  calculation: CalcResult;
  history: RequestEvent[];
  can: { edit: boolean; close: boolean; approve: boolean; return: boolean; correct: boolean; cancel: boolean };
  /** Сырьё рецептуры без этапов — для взвешенного расхода (у этапов материалы — в шаблоне этапа). */
  ingredients?: { itemId: string; name: string; unitCode: string; basis: LineBasis }[];
};

export type ShiftRow = {
  id: string;
  number: string;
  kind: "Regular" | "Correction";
  correctsNumber: string | null;
  orderId: string;
  orderNumber: string;
  recipeName: string;
  outputUnit: string;
  shiftDate: string;
  shiftNo: number;
  line: string | null;
  operatorName: string;
  status: ShiftStatus;
  goodOutput: number | null;
  defectKg: number;
};

export type ShiftList = Page<ShiftRow> & { canCreate: boolean; drafts: number; closed: number };

export type ShiftForm = {
  orders: { id: string; number: string; recipeName: string; outputUnit: string; plannedQty: number; plannedDate: string; departmentName: string }[];
  people: Named[];
};

/** Выпечка: потеря, % потери и выхода (до 0,01), отклонение от нормы в п. п., вне допуска. Как на сервере (ProductionCalc.Bake). */
export function bakeResult(inputKg: number, outputKg: number, norm: number, min: number | null, max: number | null) {
  const r2 = (n: number) => Math.round((n + Number.EPSILON) * 100) / 100;
  const yieldPercent = r2((outputKg / inputKg) * 100);
  return {
    lossKg: Math.round((inputKg - outputKg) * 1000) / 1000,
    lossPercent: r2(((inputKg - outputKg) / inputKg) * 100),
    yieldPercent,
    deviation: r2(yieldPercent - norm),
    outOfRange: (min !== null && yieldPercent < min) || (max !== null && yieldPercent > max),
  };
}

export type DashboardBar = { name: string; value: number; extra: number | null; unit: string | null };

/** Панель «Технолога» за период: только настоящие данные; масса — по продуктам с выходом в единицах массы. */
export type ProductionDashboard = {
  from: string;
  to: string;
  todayKg: number;
  goodKg: number;
  planKg: number;
  completionPercent: number | null;
  defectKg: number;
  defectPercent: number | null;
  /** Стоимость брака продукта и материалов по цене партий, выданных в задания; null — ни у одного материала нет цены партии. */
  defectCost: number | null;
  /** Материалы брака без цены партии (позиция без партий или партия без цены) — их стоимость не посчитана. */
  defectCostUnpriced: string[];
  activeOrders: number;
  openShifts: number;
  closedShifts: number;
  pendingRecipes: number;
  normKg: number;
  factKg: number;
  normDeviationPercent: number | null;
  bakeYieldPercent: number | null;
  pieceOutputs: number;
  days: { date: string; goodKg: number; defectKg: number; bakeYieldPercent: number | null }[];
  byShift: DashboardBar[];
  byProduct: DashboardBar[];
  defectsByStage: DashboardBar[];
  defectsByType: DashboardBar[];
  normFact: { name: string; unit: string; norm: number; fact: number; percent: number | null }[];
  packagingDefects: DashboardBar[];
  orders: { id: string; number: string; product: string; unit: string; planned: number; good: number; status: ProductionOrderStatus; plannedDate: string; shortages: number }[];
  issues: { kind: "shortage" | "overconsumption" | "defect" | "approval"; title: string; detail: string; link: string }[];
};
