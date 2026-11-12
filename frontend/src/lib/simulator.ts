// «Аналитик → Симулятор» (What-If): типы ответов API и чистая логика экрана (параметры, черновик в адресе, оформление).
// Все расчёты делает сервер (ScenarioEngine) — здесь только состояние формы и показ.

import { compact, num } from "./analytik.ts";

export type ScenarioKind = "sales" | "production" | "materials" | "procurement" | "finance" | "workforce" | "expansion" | "combined";
export type ParameterMode = "change" | "value" | "input";
export type TargetKind = "region" | "product" | "item" | "item_supplier" | "division";

export type ParameterDefinition = {
  code: string;
  title: string;
  unit: string;
  mode: ParameterMode;
  min: number;
  max: number;
  step: number;
  default: number;
  node: string;
  targetKind: TargetKind | null;
  kinds: ScenarioKind[];
  description: string;
};

export type KindView = { code: ScenarioKind; title: string; description: string; focus: string[] };
export type GraphNode = { code: string; title: string; dependsOn: string[] };

export type SimulatorCatalog = {
  kinds: KindView[];
  parameters: ParameterDefinition[];
  presets: string[];
  nodes: GraphNode[];
  minHorizonDays: number;
  maxHorizonDays: number;
  maxCompare: number;
  formulaVersion: string;
};

export type ScenarioParameter = { code: string; value: number; target?: string | null };

export type ChoiceItem = { id: string; name: string; hint: string | null };
export type BaselineSource = { code: string; title: string; status: string; updatedAt: string | null; note: string | null };

export type BaselineSummary = {
  revenue: number | null;
  kg: number | null;
  discountShare: number | null;
  products: number;
  productsWithRecipe: number;
  recipeShare: number | null;
  regions: number;
  planKgMonth: number | null;
  planSource: string | null;
  salesThrough: string | null;
  materials: number;
  materialsPriced: number;
  materialsLeadAssumed: number;
  capacityKgDay: number | null;
  capacitySource: string | null;
  shiftsPerDay: number | null;
  defectRate: number | null;
  payments: number | null;
  collectionRate: number | null;
  paymentLagDays: number | null;
  procurementSpend: number | null;
  employees: number | null;
  divisions: number;
};

export type BaselineView = {
  id: string;
  from: string;
  to: string;
  days: number;
  asOf: string;
  createdAt: string;
  formulaVersion: string;
  sections: string[];
  salesScope: string | null;
  sources: BaselineSource[];
  notes: string[];
  summary: BaselineSummary;
  choices: { regions: ChoiceItem[]; products: ChoiceItem[]; items: ChoiceItem[]; suppliers: ChoiceItem[]; divisions: ChoiceItem[] };
};

export type SimMetric = {
  code: string;
  group: string;
  title: string;
  unit: string;
  node: string;
  base: number | null;
  scenario: number | null;
  delta: number | null;
  deltaPercent: number | null;
  kind: "fact" | "calculated" | "input";
  note: string | null;
};

export type SimProductRow = {
  id: number;
  name: string;
  hasRecipe: boolean;
  baseKg: number;
  requestedKg: number;
  producedKg: number | null;
  soldKg: number;
  baseRevenue: number;
  revenue: number;
  baseCostPerKg: number | null;
  costPerKg: number | null;
  grossProfit: number | null;
  coverage: number;
  limit: string | null;
};

export type SimRegionRow = { id: string; name: string; new: boolean; baseKg: number; kg: number; baseRevenue: number; revenue: number; planKg: number | null; execution: number | null };

export type SimMaterialRow = {
  itemId: string;
  name: string;
  unit: string;
  baseDaily: number;
  daily: number;
  available: number;
  incoming: number;
  purchase: number;
  purchaseCost: number | null;
  basePrice: number | null;
  price: number | null;
  shortageDate: string | null;
  lostDays: number;
  orderBy: string | null;
  orderLate: boolean;
  leadTimeDays: number;
  endStock: number;
  endValue: number | null;
  surplus: boolean;
  coverage: number;
  risk: "critical" | "high" | "medium" | "low" | "none";
  supplier: string | null;
};

export type SimSeries = { code: string; title: string; unit: string; base: (number | null)[]; scenario: (number | null)[] };
export type SimConstraint = { code: string; severity: "critical" | "warning" | "info"; title: string; text: string; target: string | null };
export type SimRisk = { code: string; level: "critical" | "high" | "medium" | "low"; title: string; text: string };
export type ScenarioIssue = { severity: "error" | "warning"; code: string; text: string; parameter: string | null; target: string | null };
export type SimInput = { code: string; title: string; value: number | null; unit: string; origin: string };

export type SimExpansion = {
  name: string;
  reference: string | null;
  kg: number;
  revenue: number;
  cost: number | null;
  fixedMonth: number | null;
  profitMonth: number | null;
  breakEvenKgMonth: number | null;
  paybackMonths: number | null;
  staffNeed: number | null;
  staff: number;
  notes: string[];
};

export type SimConclusion = { headline: string; changes: string[]; constraints: string[]; risks: string[]; recommendations: string[]; sources: string[]; assumptions: string[] };

export type SimulationResult = {
  formulaVersion: string;
  kind: ScenarioKind;
  valid: boolean;
  horizonDays: number;
  from: string;
  to: string;
  metrics: SimMetric[];
  products: SimProductRow[];
  regions: SimRegionRow[];
  materials: SimMaterialRow[];
  series: SimSeries[];
  constraints: SimConstraint[];
  risks: SimRisk[];
  issues: ScenarioIssue[];
  chain: string[];
  conclusion: SimConclusion;
  inputs: SimInput[];
  expansion: SimExpansion | null;
};

export type ScenarioHistoryEntry = { revision: number; at: string; baselineId: string; from: string; to: string; asOf: string; formulaVersion: string; headline: string | null; revenue: number | null; revenueScenario: number | null };

export type ScenarioView = {
  id: string;
  name: string;
  kind: ScenarioKind;
  preset: string;
  status: "active" | "archived";
  horizonDays: number;
  newRegionName: string | null;
  parameters: ScenarioParameter[];
  result: SimulationResult | null;
  aiAnalysis: string | null;
  aiAt: string | null;
  calculatedAt: string | null;
  formulaVersion: string;
  formulaOutdated: boolean;
  revision: number;
  history: ScenarioHistoryEntry[];
  own: boolean;
  copiedFromId: string | null;
  createdAt: string;
  updatedAt: string | null;
  baseline: BaselineView;
};

export type ScenarioRow = {
  id: string;
  name: string;
  kind: ScenarioKind;
  preset: string;
  status: string;
  headline: string | null;
  calculatedAt: string | null;
  baseFrom: string;
  baseTo: string;
  asOf: string;
  horizonDays: number;
  own: boolean;
  revision: number;
  createdAt: string;
  updatedAt: string | null;
  revenueDeltaPercent: number | null;
  critical: number;
  hasAi: boolean;
};

export type ComparisonColumn = { id: string; name: string; preset: string; kind: ScenarioKind; horizonDays: number; asOf: string; valid: boolean; headline: string | null };
export type ComparisonRow = { code: string; group: string; title: string; unit: string; values: (number | null)[]; delta: (number | null)[]; deltaPercent: (number | null)[] };
export type ScenarioComparison = { columns: ComparisonColumn[]; rows: ComparisonRow[]; notes: string[] };

// ---------- параметры ----------

/** Параметры, которые показываются в виде сценария: изменения вида (без исходных данных и без точечных — они отдельным списком). */
export function panelParameters(catalog: ParameterDefinition[], kind: ScenarioKind): ParameterDefinition[] {
  return catalog.filter((p) => p.kinds.includes(kind) && p.mode !== "input" && p.targetKind == null);
}

/** Точечные параметры вида (регион, товар, позиция, поставщик, подразделение). */
export function targetedParameters(catalog: ParameterDefinition[], kind: ScenarioKind): ParameterDefinition[] {
  return catalog.filter((p) => p.kinds.includes(kind) && p.mode !== "input" && p.targetKind != null);
}

/** Исходные данные, которых нет в OneBase (вводит пользователь). */
export function inputParameters(catalog: ParameterDefinition[]): ParameterDefinition[] {
  return catalog.filter((p) => p.mode === "input");
}

const sameKey = (a: ScenarioParameter, code: string, target?: string | null) => a.code === code && (a.target ?? null) === (target ?? null);

/**
 * Записать значение параметра. Изменение 0 и пустое значение — удаляют его (так меньше «шума» в сценарии и в сравнении). Исходные данные
 * (input) сохраняются и с нулём — ноль у них тоже значение («денег на начало 0»); убрать их — null.
 */
export function setParameter(list: ScenarioParameter[], def: ParameterDefinition, value: number | null, target?: string | null): ScenarioParameter[] {
  const rest = list.filter((p) => !sameKey(p, def.code, target));
  if (value == null || !Number.isFinite(value)) return rest;
  if (def.mode !== "input" && value === 0 && !(def.targetKind != null && (def.code === "procurement.supplier" || def.code === "expansion.reference"))) return rest;
  const clamped = def.min === 0 && def.max === 0 ? value : Math.min(def.max, Math.max(def.min, value));
  const next = { code: def.code, value: clamped, target: target ?? null };
  // Уже заданный параметр остаётся на своём месте — порядок ввода задаёт имя сценария («Цена +5%, спрос −3%…»).
  const at = list.findIndex((p) => sameKey(p, def.code, target));
  return at < 0 ? [...rest, next] : list.map((p, i) => (i === at ? next : p));
}

export function valueOf(list: ScenarioParameter[], code: string, target?: string | null): number | null {
  return list.find((p) => sameKey(p, code, target))?.value ?? null;
}

/** Параметры, относящиеся к виду сценария (остальные при смене вида сохраняются — комбинированный покажет все). */
export function changedCount(list: ScenarioParameter[], catalog: ParameterDefinition[]): number {
  return list.filter((p) => catalog.find((d) => d.code === p.code)?.mode !== "input").length;
}

// ---------- черновик в адресе (ссылка AI-аналитика «Открыть в симуляторе») ----------

export type Draft = { kind: ScenarioKind; horizonDays: number; parameters: ScenarioParameter[]; newRegionName: string | null; from: string | null; to: string | null };

const kinds: ScenarioKind[] = ["sales", "production", "materials", "procurement", "finance", "workforce", "expansion", "combined"];

export function encodeDraft(d: Draft): string {
  const json = JSON.stringify(d);
  const bytes = new TextEncoder().encode(json);
  let binary = "";
  bytes.forEach((b) => (binary += String.fromCharCode(b)));
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

/** Черновик из base64url JSON; неверный — null (страница откроется пустой, без ошибки). */
export function decodeDraft(value: string | null | undefined): Draft | null {
  if (!value) return null;
  try {
    const padded = value.replace(/-/g, "+").replace(/_/g, "/") + "===".slice((value.length + 3) % 4);
    const binary = atob(padded);
    const bytes = Uint8Array.from(binary, (c) => c.charCodeAt(0));
    const raw = JSON.parse(new TextDecoder().decode(bytes));
    const kind = kinds.includes(raw.kind) ? raw.kind : "combined";
    const parameters = Array.isArray(raw.parameters)
      ? raw.parameters
          .filter((p: unknown): p is { code: string; value: number; target?: string } => typeof p === "object" && p != null && typeof (p as { code: unknown }).code === "string" && typeof (p as { value: unknown }).value === "number")
          .map((p: { code: string; value: number; target?: string | null }) => ({ code: p.code, value: p.value, target: p.target ?? null }))
      : [];
    const date = (v: unknown) => (typeof v === "string" && /^\d{4}-\d{2}-\d{2}$/.test(v) ? v : null);
    return {
      kind,
      horizonDays: Number.isInteger(raw.horizonDays) ? raw.horizonDays : 30,
      parameters,
      newRegionName: typeof raw.newRegionName === "string" ? raw.newRegionName : null,
      from: date(raw.from),
      to: date(raw.to),
    };
  } catch {
    return null;
  }
}

// ---------- оформление ----------

const nbsp = "\u00a0";

/**
 * Суммы симулятора: точнее общего compact — сценарии отличаются на доли процента, и «46,2 млн» против «46,6 млн» не должны выглядеть
 * одинаково: млрд — 2 знака, млн — 1 знак, тысячи — целые.
 */
export function simMoney(value: number): string {
  const abs = Math.abs(value);
  const sign = value < 0 ? "−" : "";
  if (abs >= 1e9) return `${sign}${num(abs / 1e9, 2)}${nbsp}млрд`;
  if (abs >= 1e6) return `${sign}${num(abs / 1e6, 1)}${nbsp}млн`;
  if (abs >= 1e4) return `${sign}${num(abs / 1e3, 0)}${nbsp}тыс.`;
  return compact(value);
}

/** Значение показателя симулятора: суммы — компактно, кг — с разрядами, проценты и дни — с одним знаком. */
export function simValue(value: number | null | undefined, unit: string): string {
  if (value == null || !Number.isFinite(value)) return "—";
  if (unit === "сум") return `${simMoney(value)}${nbsp}сум`;
  if (unit === "%") return `${num(value, 1)}${nbsp}%`;
  if (unit === "кг") return `${Math.abs(value) >= 1e6 ? simMoney(value) : num(value, Math.abs(value) >= 100 ? 0 : 1)}${nbsp}кг`;
  return `${num(value, Number.isInteger(value) ? 0 : 2)}${unit ? `${nbsp}${unit}` : ""}`;
}

/** Разница со знаком: «+1,2 млн сум», «−340 кг»; у процентных показателей — процентные пункты («+3,7 п.п.»). */
export function simDelta(value: number | null | undefined, unit: string): string | null {
  if (value == null || !Number.isFinite(value) || value === 0) return null;
  if (unit === "%") return `${value > 0 ? "+" : "−"}${num(Math.abs(value), 1)}${nbsp}п.п.`;
  return `${value > 0 ? "+" : "−"}${simValue(Math.abs(value), unit)}`;
}

/** Показатели, у которых рост — плохо (расходы, дефицит, загрузка выше 100 %, потребность в финансировании, недополученная выручка). */
const inverse = /lost_revenue|items_short|max_lost_days|unpriced|surplus_items|financing_need|gap_days|cogs|cost_per_kg|payroll|opex|logistics|cash_out|procurement\.budget|need_value|risks\.count|constraints\.critical/;

/** Тон изменения: хорошо, плохо, нейтрально (без изменения или показатель без оценки). */
export function deltaTone(code: string, delta: number | null | undefined): "ok" | "bad" | "muted" {
  if (delta == null || Math.abs(delta) < 1e-9) return "muted";
  if (code === "production.utilization" || code === "production.capacity" || code === "workforce.headcount" || code === "sales.plan_kg") return "muted";
  return (delta > 0) !== inverse.test(code) ? "ok" : "bad";
}

export const kindIcons: Record<ScenarioKind, string> = {
  sales: "ChartLine",
  production: "Factory",
  materials: "Wheat",
  procurement: "Truck",
  finance: "Wallet",
  workforce: "Users",
  expansion: "MapPinned",
  combined: "Layers",
};

export const presetLabel: Record<string, string> = { base: "Базовый", optimistic: "Оптимистичный", pessimistic: "Пессимистичный", custom: "Свой" };

export const groupLabel: Record<string, string> = {
  sales: "Продажи",
  production: "Производство",
  materials: "Сырьё",
  procurement: "Закупки",
  finance: "Финансы",
  workforce: "Персонал",
  risks: "Риски",
};

/** Главные показатели вида сценария — вверху результатов (по каталогу), остальные — в таблице групп. */
export function focusMetrics(result: SimulationResult, kind: KindView | undefined): SimMetric[] {
  const codes = kind?.focus ?? ["sales.revenue", "finance.gross_profit", "production.output", "procurement.budget"];
  const list = codes.map((c) => result.metrics.find((m) => m.code === c)).filter((m): m is SimMetric => m != null);
  for (const code of ["sales.revenue", "sales.kg", "finance.gross_profit", "procurement.budget", "production.utilization", "finance.net_cash"]) {
    if (list.length >= 6) break;
    const m = result.metrics.find((x) => x.code === code);
    if (m && !list.includes(m)) list.push(m);
  }
  return list;
}

/** Цепочка влияния кодами узлов → названия (по графу каталога). */
export function chainTitles(chain: string[], nodes: GraphNode[]): string[] {
  return chain.map((c) => nodes.find((n) => n.code === c)?.title ?? c);
}

/** Базовый период по умолчанию: последние 3 полных месяца до сегодня (как на сервере). */
export function defaultPeriod(today: Date): { from: string; to: string } {
  const y = today.getFullYear();
  const m = today.getMonth();
  const start = new Date(Date.UTC(y, m - 3, 1));
  const end = new Date(Date.UTC(y, m, 0));
  const iso = (d: Date) => d.toISOString().slice(0, 10);
  return { from: iso(start), to: iso(end) };
}

/** Быстрый выбор базового периода: последний полный месяц, 3 и 6 полных месяцев, последние 30 и 90 дней. */
export function quickPeriods(today: Date): { key: string; label: string; from: string; to: string }[] {
  const y = today.getFullYear();
  const m = today.getMonth();
  const iso = (d: Date) => d.toISOString().slice(0, 10);
  const months = (n: number) => ({ from: iso(new Date(Date.UTC(y, m - n, 1))), to: iso(new Date(Date.UTC(y, m, 0))) });
  const yesterday = new Date(Date.UTC(y, m, today.getDate() - 1));
  const days = (n: number) => ({ from: iso(new Date(Date.UTC(y, m, today.getDate() - n))), to: iso(yesterday) });
  return [
    { key: "m1", label: "Последний месяц", ...months(1) },
    { key: "m3", label: "3 месяца", ...months(3) },
    { key: "m6", label: "6 месяцев", ...months(6) },
    { key: "d30", label: "30 дней", ...days(30) },
    { key: "d90", label: "90 дней", ...days(90) },
  ];
}

/** Простой Markdown AI-анализа: **жирный** и строки → абзацы (без HTML от модели). */
export function aiParagraphs(text: string): { bold: boolean; text: string }[][] {
  return text
    .split(/\n+/)
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line) => line.split(/(\*\*[^*]+\*\*)/).filter(Boolean).map((part) => (part.startsWith("**") && part.endsWith("**") ? { bold: true, text: part.slice(2, -2) } : { bold: false, text: part })));
}

/**
 * AI-анализ по разделам: строка, которая начинается с **заголовка** («**Основной вывод** — …», «**Риски**:»), открывает раздел; остальные
 * строки — его абзацы (маркер списка «- » снимается). Текст до первого заголовка — раздел без названия.
 */
export function aiSections(text: string): { title: string | null; paragraphs: { bold: boolean; text: string }[][] }[] {
  const sections: { title: string | null; paragraphs: { bold: boolean; text: string }[][] }[] = [];
  for (const raw of text.split(/\n+/)) {
    const trimmed = raw.trim();
    // Пункт списка — всегда абзац, даже если начинается с жирного слова («- **цена** выше рынка»).
    const item = /^[-•*]\s+/.test(trimmed);
    const line = item ? trimmed.replace(/^[-•*]\s+/, "") : trimmed;
    if (!line) continue;
    const head = item ? null : /^(?:#+\s*)?\*\*([^*]{1,60}?)[.:]?\*\*\s*(?:[—–:-]\s*)?(.*)$/.exec(line);
    if (head) {
      sections.push({ title: head[1].trim(), paragraphs: head[2] ? aiParagraphs(head[2]) : [] });
      continue;
    }
    if (sections.length === 0) sections.push({ title: null, paragraphs: [] });
    sections[sections.length - 1].paragraphs.push(...aiParagraphs(line));
  }
  return sections.filter((s) => s.title || s.paragraphs.length > 0);
}

// ---------- экран: группы параметров, ползунки, имя сценария ----------

/** Русское склонение по числу: 1 параметр, 2 параметра, 5 параметров. */
export function plural(n: number, one: string, few: string, many: string): string {
  const a = Math.abs(n) % 100;
  const b = a % 10;
  if (a > 10 && a < 20) return many;
  if (b === 1) return one;
  if (b >= 2 && b <= 4) return few;
  return many;
}

export type ParamGroupKey = "sales" | "finance" | "production" | "materials" | "costs" | "expansion";

/**
 * Группы параметров на экране (аккордеоны). Один смысл — один параметр: отсрочка поставщикам — в «Финансах и деньгах», зарплаты и
 * численность — в «Расходах и персонале», производительность — в «Производстве». Параметр каталога вне списков попадает в группу по
 * разделу кода — новый параметр сервера не потеряется.
 */
export const paramGroups: { key: ParamGroupKey; title: string; codes: string[] }[] = [
  { key: "sales", title: "Продажи и цены", codes: ["sales.demand", "sales.price", "sales.discount", "sales.elasticity", "sales.plan"] },
  { key: "finance", title: "Финансы и деньги", codes: ["procurement.supplier_terms", "finance.customer_terms", "finance.revenue"] },
  { key: "production", title: "Производство", codes: ["production.volume", "production.capacity", "production.utilization", "production.shifts", "production.defect", "workforce.productivity"] },
  { key: "materials", title: "Сырьё и закупки", codes: ["materials.price", "materials.stock", "materials.loss", "production.norms", "materials.lead_time", "procurement.volume", "procurement.safety"] },
  { key: "costs", title: "Расходы и персонал", codes: ["finance.expenses", "finance.cost", "workforce.salary", "workforce.headcount"] },
  { key: "expansion", title: "Новый регион", codes: ["expansion.plan", "expansion.demand", "expansion.reference", "expansion.staff", "expansion.opex", "expansion.logistics", "expansion.investment"] },
];

const groupByPrefix: Record<string, ParamGroupKey> = { sales: "sales", finance: "finance", production: "production", materials: "materials", procurement: "materials", workforce: "costs", expansion: "expansion" };

/** Общие параметры (без цели): численность — и общая, и по подразделению; регион-образец — выбор внутри группы «Новый регион». */
const isGeneral = (p: ParameterDefinition) => p.mode !== "input" && (p.targetKind == null || p.code === "workforce.headcount" || p.code === "expansion.reference");

/** Параметры вида сценария по группам экрана (пустые группы не возвращаются). */
export function groupedParameters(catalog: ParameterDefinition[], kind: ScenarioKind): { key: ParamGroupKey; title: string; params: ParameterDefinition[] }[] {
  const list = catalog.filter((p) => p.kinds.includes(kind) && isGeneral(p));
  return paramGroups
    .map((g) => {
      const known = g.codes.map((c) => list.find((p) => p.code === c)).filter((p): p is ParameterDefinition => p != null);
      const extra = list.filter((p) => !paramGroups.some((x) => x.codes.includes(p.code)) && groupByPrefix[p.code.split(".")[0]] === g.key);
      return { key: g.key, title: g.title, params: [...known, ...extra] };
    })
    .filter((g) => g.params.length > 0);
}

/** Точечные изменения вида: регион, товар, позиция, поставщик, подразделение (регион-образец — в группе «Новый регион»). */
export function targetedChanges(catalog: ParameterDefinition[], kind: ScenarioKind): ParameterDefinition[] {
  return catalog.filter((p) => p.kinds.includes(kind) && p.mode !== "input" && p.targetKind != null && p.code !== "expansion.reference");
}

/** Задан ли общий параметр (без цели); у регион-образца цель — сам выбор. */
export function isSet(list: ScenarioParameter[], def: ParameterDefinition): boolean {
  return def.code === "expansion.reference" ? list.some((p) => p.code === def.code) : list.some((p) => p.code === def.code && (p.target ?? null) === null);
}

const sliderUnits = new Set(["%", "п.п.", "дн.", "смен"]);
const comfortRange: Record<string, number> = { "%": 50, "п.п.": 20, "дн.": 60 };

/**
 * Пределы ползунка. Изменения (−…+) — симметрично вокруг нуля в центре: ±50 %, ±20 п.п., ±60 дн., но не шире пределов сервера; поле
 * числа принимает весь диапазон сервера, ползунок тогда упирается в край. Значения (загрузка, смены) — от минимума до максимума.
 * Без ползунка — суммы, кг, коэффициенты и выборы.
 */
export function sliderRange(def: ParameterDefinition): { min: number; max: number; centered: boolean } | null {
  if (!sliderUnits.has(def.unit) || !(def.max > def.min)) return null;
  if (def.mode === "change" && def.min < 0 && def.max > 0) {
    const r = Math.min(comfortRange[def.unit] ?? Math.min(-def.min, def.max), -def.min, def.max);
    return { min: -r, max: r, centered: true };
  }
  return { min: def.min, max: def.max, centered: false };
}

const nbspUnit = (unit: string) => (unit === "%" ? "%" : `${nbsp}${unit}`);

/** Значение параметра со знаком: «+5%», «−3%», «+15 дн.», «+2 п.п.»; значения (не изменения) — без плюса. */
export function paramValue(value: number, def: Pick<ParameterDefinition, "unit" | "mode">): string {
  const sign = def.mode === "change" ? (value > 0 ? "+" : value < 0 ? "−" : "") : value < 0 ? "−" : "";
  const abs = Math.abs(value);
  const digits = Number.isInteger(abs) ? 0 : Math.abs(abs * 10 - Math.round(abs * 10)) < 1e-9 ? 1 : 2;
  return `${sign}${num(abs, digits)}${def.unit ? nbspUnit(def.unit) : ""}`;
}

/** Короткие названия параметров для имени сценария («Цена +5%, спрос −3%, отсрочка +15 дн.») и подписей точечных изменений. */
export const shortParamName: Record<string, string> = {
  "sales.demand": "спрос",
  "sales.price": "цена",
  "sales.discount": "скидки",
  "sales.elasticity": "эластичность",
  "sales.plan": "план",
  "sales.region": "продажи",
  "sales.product": "продажи",
  "production.volume": "выпуск",
  "production.capacity": "мощность",
  "production.utilization": "загрузка",
  "production.shifts": "смены",
  "production.defect": "брак",
  "production.norms": "нормы",
  "production.norm_item": "норма",
  "materials.price": "сырьё",
  "materials.price_item": "цена",
  "materials.stock": "запас сырья",
  "materials.stock_item": "запас",
  "materials.loss": "потери сырья",
  "materials.lead_time": "поставка",
  "procurement.supplier": "поставщик",
  "procurement.min_lot": "партия",
  "procurement.volume": "закупки",
  "procurement.safety": "страховой запас",
  "procurement.supplier_terms": "отсрочка",
  "finance.revenue": "выручка",
  "finance.expenses": "расходы",
  "finance.cost": "себестоимость",
  "finance.customer_terms": "срок оплаты",
  "workforce.headcount": "персонал",
  "workforce.salary": "зарплаты",
  "workforce.productivity": "производительность",
};

type Translate = (text: string, vars?: Record<string, string | number>) => string;

/**
 * Имя сценария из изменённых параметров в порядке ввода: «Цена +5%, спрос −3%, отсрочка +15 дн.»; больше трёх — «и ещё N». Новый регион —
 * одной частью; исходные данные (input) в имя не входят. Без изменений — «Базовый сценарий».
 */
export function autoName(list: ScenarioParameter[], catalog: ParameterDefinition[], t: Translate, targetName: (def: ParameterDefinition, id: string) => string, regionName?: string | null): string {
  const parts: string[] = [];
  let region = false;
  for (const p of list) {
    const def = catalog.find((d) => d.code === p.code);
    if (!def || def.mode === "input") continue;
    if (def.code.startsWith("expansion.")) {
      if (!region) parts.push(regionName?.trim() ? t("новый регион «{name}»", { name: regionName.trim() }) : t("новый регион"));
      region = true;
      continue;
    }
    const short = t(shortParamName[def.code] ?? def.title.toLowerCase());
    if (def.min === 0 && def.max === 0) parts.push(`${short} «${p.target ? targetName(def, p.target) : ""}»`);
    else if (p.target) parts.push(`${targetName(def, p.target)}: ${short} ${paramValue(p.value, def)}`);
    else parts.push(`${short} ${paramValue(p.value, def)}`);
  }
  if (parts.length === 0) return t("Базовый сценарий");
  const shown = parts.slice(0, 3).join(", ") + (parts.length > 3 ? ` ${t("и ещё {n}", { n: parts.length - 3 })}` : "");
  return shown.charAt(0).toUpperCase() + shown.slice(1);
}

// ---------- экран: результат ----------

/** Число с разрядами и ровно digits знаками после запятой (без обрезки нулей): «354,0», «13,50». */
export function fixed(value: number, digits: number): string {
  const [int, frac] = Math.abs(value).toFixed(digits).split(".");
  return `${value < 0 ? "−" : ""}${int.replace(/\B(?=(\d{3})+(?!\d))/g, nbsp)}${frac ? `,${frac}` : ""}`;
}

/** Процент изменения со знаком и одним знаком после запятой: «+1,9%», «−3,0%», «0%». */
export function pct(value: number | null | undefined): string {
  if (value == null || !Number.isFinite(value)) return "—";
  if (Math.abs(value) < 0.05) return "0%";
  return `${value > 0 ? "+" : "−"}${fixed(Math.abs(value), 1)}%`;
}

/** Число и единица отдельно (крупная цифра + мелкая подпись): «13,76» + «млрд сум», «367 026» + «кг», «37 489» + «сум/кг». */
export function valueParts(value: number | null | undefined, unit: string): { value: string; unit: string } {
  if (value == null || !Number.isFinite(value)) return { value: "—", unit: "" };
  const sign = value < 0 ? "−" : "";
  const abs = Math.abs(value);
  if (unit === "сум") {
    if (abs >= 1e9) return { value: `${sign}${fixed(abs / 1e9, 2)}`, unit: "млрд сум" };
    if (abs >= 1e6) return { value: `${sign}${fixed(abs / 1e6, 1)}`, unit: "млн сум" };
    return { value: `${sign}${num(abs, 0)}`, unit: "сум" };
  }
  if (unit === "%") return { value: `${sign}${num(abs, 1)}`, unit: "%" };
  return { value: `${sign}${num(abs, abs >= 100 || Number.isInteger(abs) ? 0 : 1)}`, unit };
}

/** Короткая сумма для подписей («13,51 млрд», «354,0 млн») — без «сум», в таблицах и полосах; остальные единицы — как simValue. */
export function shortValue(value: number | null | undefined, unit: string): string {
  if (value == null || !Number.isFinite(value)) return "—";
  if (unit !== "сум") return simValue(value, unit);
  const abs = Math.abs(value);
  const sign = value < 0 ? "−" : "";
  if (abs >= 1e9) return `${sign}${fixed(abs / 1e9, 2)}${nbsp}млрд`;
  if (abs >= 1e6) return `${sign}${fixed(abs / 1e6, 1)}${nbsp}млн`;
  return `${sign}${num(abs, 0)}${nbsp}сум`;
}

/** Нет данных для показателя: ни базы, ни сценария (причина — в note). */
export const isMissing = (m: SimMetric) => m.base == null && m.scenario == null;

/** Изменился ли показатель (для «Только изменённые» и жирного значения сценария). */
export const isChanged = (m: SimMetric) => m.delta != null && Math.abs(m.delta) > 1e-9;

/** Короткая причина «Нужны данные» по пояснению движка (полный текст — в подсказке). */
export function missingReason(m: Pick<SimMetric, "note">): string {
  const n = m.note ?? "";
  if (/Нужны себестоимость всех товаров/.test(n)) return "нужны себестоимость, зарплата и расходы";
  if (/Себестоимость неизвестна/.test(n)) return "нет техкарт с ценами сырья";
  if (/техкарт/i.test(n)) return "свяжите техкарты с товарами Linko";
  if (/цен[аы] прихода|Цен прихода нет/i.test(n)) return "нет цен прихода сырья";
  if (/Планов продаж/.test(n)) return "планов продаж на месяц в OneBase нет";
  if (/зарплата не введена/i.test(n)) return "введите среднюю зарплату";
  if (/Расходы компании не введены/.test(n)) return "введите расходы компании";
  if (/Логистика за кг не введена/.test(n)) return "введите логистику за кг";
  if (/деньги на начало/i.test(n)) return "введите деньги на начало";
  if (/смен|мощност/i.test(n)) return "мощность не известна";
  return n ? n.replace(/\.$/, "") : "нет данных";
}

/** KPI-карточки: «Всё сразу» — как в макете; другой вид — сначала его главные показатели, затем общие (всего 6). */
export const defaultKpis = ["sales.revenue", "sales.kg", "sales.avg_price", "finance.net_cash", "finance.gross_profit", "procurement.budget"];

export function kpiMetrics(result: SimulationResult, kind: KindView | undefined): SimMetric[] {
  const codes = kind && kind.code !== "combined" ? [...kind.focus, ...defaultKpis] : defaultKpis;
  const list: SimMetric[] = [];
  for (const c of codes) {
    const m = result.metrics.find((x) => x.code === c);
    if (m && !list.includes(m)) list.push(m);
    if (list.length === 6) break;
  }
  return list;
}

/** Строки таблицы «Показатели»: показатель или объединённая строка «Нужны данные» (соседние показатели группы с одной причиной). */
export type MetricRow = { type: "metric"; metric: SimMetric } | { type: "missing"; metrics: SimMetric[]; reason: string; note: string };

export function metricRows(metrics: SimMetric[]): { group: string; rows: MetricRow[] }[] {
  const groups: { group: string; rows: MetricRow[] }[] = [];
  for (const m of metrics) {
    let g = groups.find((x) => x.group === m.group);
    if (!g) groups.push((g = { group: m.group, rows: [] }));
    const last = g.rows[g.rows.length - 1];
    if (isMissing(m)) {
      // Та же причина или без своей причины («Выполнение плана» после «Плана продаж») — в одну строку с предыдущей.
      if (last?.type === "missing" && (!m.note || m.note === last.note)) {
        last.metrics.push(m);
        continue;
      }
      g.rows.push({ type: "missing", metrics: [m], reason: missingReason(m), note: m.note ?? "" });
      continue;
    }
    g.rows.push({ type: "metric", metric: m });
  }
  return groups;
}

/** Строки, видимые до «Показать все»: главные показатели и всё изменившееся. */
const keyRowCodes = new Set([
  "sales.requested_kg",
  "sales.revenue",
  "sales.avg_price",
  "sales.lost_revenue",
  "sales.plan_kg",
  "production.required",
  "production.output",
  "production.utilization",
  "materials.items_short",
  "procurement.budget",
  "procurement.surplus_items",
  "finance.cost_per_kg",
  "finance.cash_in",
  "finance.min_cash",
  "finance.working_capital",
  "finance.operating_profit",
  "workforce.headcount",
]);

export function isKeyRow(row: MetricRow): boolean {
  return row.type === "metric" ? keyRowCodes.has(row.metric.code) || isChanged(row.metric) : row.metrics.some((m) => keyRowCodes.has(m.code));
}

/** Полнота расчёта: что не считается и почему — одна плашка «Расчёт неполный» вместо одинаковых пояснений в каждой строке. */
export type Completeness = {
  complete: boolean;
  /** Что не считается: «себестоимость», «прибыль», «производство», «закупки». */
  missing: string[];
  issues: { code: "recipes" | "prices" | "capacity" | "sources"; text: string; vars?: Record<string, string | number> }[];
  /** Ограничения проверены не полностью (производство и сырьё без техкарт, мощность неизвестна). */
  unchecked: boolean;
};

export function completeness(baseline: BaselineView | null, result: SimulationResult | null, parameters: ScenarioParameter[]): Completeness {
  const metric = (code: string) => result?.metrics.find((m) => m.code === code);
  const gone = (code: string) => {
    const m = metric(code);
    return m != null && m.scenario == null;
  };
  const missing: string[] = [];
  if (gone("finance.cost_per_kg")) missing.push("себестоимость");
  if (gone("finance.gross_profit")) missing.push("прибыль");
  if (gone("production.required")) missing.push("производство");
  const s = baseline?.summary;
  const issues: Completeness["issues"] = [];
  const recipes = s != null && s.products > 0 && (s.recipeShare ?? 0) < 0.995 && baseline!.sections.includes("production");
  if (recipes) issues.push({ code: "recipes", text: "Техкартами покрыто {p}% продаж", vars: { p: num((s!.recipeShare ?? 0) * 100, 0) } });
  if (s != null && s.materials > 0 && s.materialsPriced < s.materials) {
    issues.push(s.materialsPriced === 0 ? { code: "prices", text: "у приходов сырья нет цен" } : { code: "prices", text: "цены прихода есть у {a} из {b} позиций сырья", vars: { a: s.materialsPriced, b: s.materials } });
  }
  const capacityInput = parameters.some((p) => p.code === "input.capacity");
  const capacity = s != null && s.capacityKgDay == null && !capacityInput && !recipes && metric("production.required")?.scenario != null;
  if (capacity) issues.push({ code: "capacity", text: "мощность не известна — нет утверждённых смен" });
  const unchecked = recipes || capacity;
  const complete = missing.length === 0 && !unchecked;
  const stale = baseline?.sources.filter((x) => x.status !== "ok").length ?? 0;
  if (!complete && stale > 0) issues.push({ code: "sources", text: stale === 1 ? "{n} источник отстаёт" : stale < 5 ? "{n} источника отстают" : "{n} источников отстают", vars: { n: stale } });
  return { complete, missing, issues, unchecked };
}

/** Период прогноза: со следующего дня после даты данных на горизонт (как у движка). */
export function forecastRange(asOf: string, horizonDays: number): { from: string; to: string } {
  const d = new Date(`${asOf}T00:00:00Z`);
  const from = new Date(d.getTime() + 86400000);
  const to = new Date(d.getTime() + horizonDays * 86400000);
  const iso = (x: Date) => x.toISOString().slice(0, 10);
  return { from: iso(from), to: iso(to) };
}

/** Дней в периоде включительно. */
export function periodDays(from: string, to: string): number {
  return Math.round((Date.parse(`${to}T00:00:00Z`) - Date.parse(`${from}T00:00:00Z`)) / 86400000) + 1;
}

/**
 * Пояснение к выводу — по числам движка, без догадок о причинах: как сложилась выручка из объёма и цены, сколько выручки не обеспечено,
 * как изменились минимум денег и валовая прибыль. Тексты — ключи перевода с подстановками.
 */
export function explainResult(result: SimulationResult): { text: string; vars?: Record<string, string> }[] {
  const m = (code: string) => result.metrics.find((x) => x.code === code);
  const p = (code: string) => {
    const x = m(code);
    return x && isChanged(x) && x.deltaPercent != null ? x.deltaPercent : 0;
  };
  const v = p("sales.kg");
  const price = p("sales.avg_price");
  const revenue = m("sales.revenue");
  const out: { text: string; vars?: Record<string, string> }[] = [];
  const abs = (x: number) => `${fixed(Math.abs(x), 1)}%`;
  if (v !== 0 && price !== 0 && Math.sign(v) !== Math.sign(price)) {
    const up = (revenue?.delta ?? 0) >= 0;
    if (price > 0) out.push(up ? { text: "Рост цены на {p} перекрывает падение объёма на {v}.", vars: { p: abs(price), v: abs(v) } } : { text: "Рост цены на {p} не перекрывает падение объёма на {v}.", vars: { p: abs(price), v: abs(v) } });
    else out.push(up ? { text: "Рост объёма на {v} перекрывает снижение цены на {p}.", vars: { p: abs(price), v: abs(v) } } : { text: "Рост объёма на {v} не перекрывает снижение цены на {p}.", vars: { p: abs(price), v: abs(v) } });
  } else if (v !== 0 && price !== 0) out.push({ text: "Объём продаж {v}, средняя цена {p}.", vars: { v: pct(v), p: pct(price) } });
  else if (v !== 0) out.push({ text: "Объём продаж {v}.", vars: { v: pct(v) } });
  else if (price !== 0) out.push({ text: "Средняя цена {p}.", vars: { p: pct(price) } });
  const lost = m("sales.lost_revenue");
  if (lost?.scenario != null && lost.scenario > 0) out.push({ text: "Ограничения не дают продать на {sum}.", vars: { sum: simValue(lost.scenario, "сум") } });
  const minCash = m("finance.min_cash");
  if (minCash && isChanged(minCash)) out.push({ text: "Минимум накопленного потока {d}.", vars: { d: simDelta(minCash.delta, "сум") ?? "" } });
  const gross = m("finance.gross_profit");
  if (gross && isChanged(gross) && gross.deltaPercent != null) out.push({ text: "Валовая прибыль {p}.", vars: { p: pct(gross.deltaPercent) } });
  return out.slice(0, 3);
}
