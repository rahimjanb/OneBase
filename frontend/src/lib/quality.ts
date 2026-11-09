// Контроль качества сырья: проверки партий, спецификации материалов. Чистые расчёты — с тестами (quality.test.mjs).
import type { BatchQuality, ProcDocument, QualityCheckStatus, QualityControl, RequestEvent } from "./procurement";

/** Число из поля формы: пробелы по разрядам и запятая — как в снабжении; не число — null. */
function num(text: string): number | null {
  const clean = text.replace(/\s/g, "").replace(",", ".");
  if (clean === "") return null;
  const n = Number(clean);
  return Number.isFinite(n) ? n : null;
}

/** Вкладка «Качество»: видна ли, вносит ли пользователь проверки (лаборатория), решает ли (назначенный), счётчики открытых проверок. */
export type QualityNav = { sees: boolean; quality: boolean; decides: boolean; configures: boolean; open: number; documents: number; lab: number; decision: number };

/**
 * Проверка в очереди: шаг, контроль, партия, позиция, поставщик, склад прихода, пришло и остаток, дней ждёт, итоги шагов, решение. Проверка
 * поставки (приезд машины) до приёмки — без партии: пришло — по накладной, поставка — номер AR-.
 */
export type QualityCheckRow = {
  id: string;
  number: string;
  status: QualityCheckStatus;
  control: QualityControl;
  batchId: string | null;
  batchNumber: string | null;
  supplierBatchNo: string | null;
  expiresOn: string | null;
  itemId: string;
  sku: string;
  itemName: string;
  unitCode: string;
  supplierName: string | null;
  warehouseId: string;
  warehouseName: string;
  received: number;
  onHand: number;
  createdAt: string;
  daysWaiting: number;
  documentsOk: boolean | null;
  labOk: boolean | null;
  decision: BatchQuality | null;
  decidedByName: string | null;
  decidedAt: string | null;
  arrivalId?: string | null;
  arrivalNumber?: string | null;
};

export type SpecKind = "Document" | "Parameter";

export type QualitySpec = {
  id: string;
  lineNo: number;
  kind: SpecKind;
  name: string;
  unit: string | null;
  min: number | null;
  max: number | null;
  expected: string | null;
  method: string | null;
  required: boolean;
};

export type QualityResult = {
  specId: string | null;
  kind: SpecKind;
  lineNo: number;
  name: string;
  unit: string | null;
  min: number | null;
  max: number | null;
  expected: string | null;
  value: number | null;
  text: string | null;
  passed: boolean | null;
};

/** Карточка проверки: шаги (кто и когда), проба, спецификация, результаты, документы, история; can — что можно сделать (сервер проверяет то же). */
export type QualityCheckCard = {
  check: QualityCheckRow;
  version: number;
  storageConditions: string | null;
  documentsNote: string | null;
  documentsByName: string | null;
  documentsAt: string | null;
  sampleQty: number | null;
  sampleDocumentNumber: string | null;
  labNote: string | null;
  labByName: string | null;
  labAt: string | null;
  decisionComment: string | null;
  specs: QualitySpec[];
  results: QualityResult[];
  documents: ProcDocument[];
  batchDocuments: ProcDocument[];
  history: RequestEvent[];
  warehouses: { warehouseId: string; name: string; onHand: number }[];
  can: { documents: boolean; lab: boolean; approve: boolean; reject: boolean; attach: boolean };
};

export type QualitySpecsView = {
  itemId: string;
  sku: string;
  name: string;
  unitCode: string;
  control: QualityControl;
  tracksBatches: boolean;
  lines: QualitySpec[];
  canEdit: boolean;
};

export type QualitySettings = { approvers: { id: string; name: string }[]; candidates: { id: string; name: string }[]; canEdit: boolean };

/** Шаги проверки для полосы прогресса: документы, лаборатория (если нужна), решение. */
export function qualitySteps(control: QualityControl): QualityCheckStatus[] {
  return control === "Lab" ? ["Documents", "Lab", "Decision"] : ["Documents", "Decision"];
}

/** Состояние шага в полосе: done — пройден, current — сейчас, todo — впереди. Решение принято или проверка отменена — все пройдены. */
export function stepState(control: QualityControl, status: QualityCheckStatus, step: QualityCheckStatus): "done" | "current" | "todo" {
  if (status === "Decided" || status === "Cancelled") return "done";
  const steps = qualitySteps(control);
  const at = steps.indexOf(status);
  const i = steps.indexOf(step);
  return i < at ? "done" : i === at ? "current" : "todo";
}

/** Показатель в норме: не меньше «от» и не больше «до» (границы входят) — как на сервере. */
export function withinNorm(min: number | null, max: number | null, value: number): boolean {
  return (min === null || value >= min) && (max === null || value <= max);
}

/** Норма для подписи: «≤ 14 %», «10–12 %», «≥ 5», качественный — ожидаемое. */
export function normText(spec: Pick<QualitySpec, "min" | "max" | "expected" | "unit">): string {
  const unit = spec.unit ? ` ${spec.unit}` : "";
  const n = (v: number) => String(v).replace(".", ",");
  if (spec.min !== null && spec.max !== null) return `${n(spec.min)}–${n(spec.max)}${unit}`;
  if (spec.max !== null) return `≤ ${n(spec.max)}${unit}`;
  if (spec.min !== null) return `≥ ${n(spec.min)}${unit}`;
  return spec.expected ?? "—";
}

/** Количественный показатель (с нормой «от» или «до»): «соответствует» считается по значению, а не отмечается. */
export const measured = (spec: Pick<QualitySpec, "min" | "max">) => spec.min !== null || spec.max !== null;

/**
 * Итог показателя в форме лаборатории: количественный — по введённому значению (null — не введено или не число), качественный — отметка.
 */
export function parameterPassed(spec: Pick<QualitySpec, "min" | "max">, value: string, mark: boolean | null): boolean | null {
  if (!measured(spec)) return mark;
  const v = num(value);
  return v === null ? null : withinNorm(spec.min, spec.max, v);
}

/** Строка спецификации в форме (числа — как ввёл пользователь). */
export type SpecDraft = { key: string; id: string | null; kind: SpecKind; name: string; unit: string; min: string; max: string; expected: string; method: string; required: boolean };

/** Проверка строки спецификации до отправки: название, норма у показателя, «от» не больше «до»; ошибка — текстом, иначе null (как на сервере). */
export function specProblem(d: SpecDraft): string | null {
  if (!d.name.trim()) return "У строки нет названия";
  if (d.kind === "Document") return null;
  const min = d.min.trim() ? num(d.min) : null;
  const max = d.max.trim() ? num(d.max) : null;
  if ((d.min.trim() && min === null) || (d.max.trim() && max === null)) return "Норма — числом";
  if (min !== null && max !== null && min > max) return "Норма «от» больше нормы «до»";
  if (min === null && max === null && !d.expected.trim()) return "Укажите норму — от и до или каким должен быть показатель";
  return null;
}

/** Дней ждёт: «сегодня», «1 дн.», «5 дн.»; долго (больше 3 дней) — warn. */
export const waitingTone = (days: number) => (days > 3 ? "text-bad" : days > 1 ? "text-warn" : "text-ink-2");
