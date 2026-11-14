/**
 * Advanced Intelligence: исследования сложных вопросов, гипотезы, память решений, качество аналитика, набор сложных вопросов. Типы — как отдаёт
 * /api/analytik/{investigations,decisions,quality,benchmark}; подписи и форматирование — без запросов (тестируются node --test).
 */

export type InvestigationRow = {
  id: string;
  question: string;
  goal: string | null;
  status: string;
  complexity: number;
  depth: number;
  stepCount: number;
  hypotheses: number;
  confirmed: number;
  rejected: number;
  reviewRejections: number;
  replans: number;
  llmCalls: number;
  toolCalls: number;
  costUsd: number | null;
  durationMs: number;
  startedAt: string;
  conversationId: string | null;
  author: string | null;
};

export type PlanStep = { id: string; question: string; analyst: string; dependsOn: string[]; purpose?: string | null; round: number };
export type InvestigationPlan = { goal: string; depth: string; fallback: boolean; dataNeeded: string[]; steps: PlanStep[]; waves: string[][] };
export type StepView = PlanStep & {
  status: string;
  summary: string;
  findings: string[];
  metrics: { name: string; value: string; unit?: string | null }[];
  sources: { title: string; period?: string | null; href?: string | null }[];
  toolCalls: number;
  unverified: string[];
  ms: number;
};
export type HypothesisView = {
  id: string;
  statement: string;
  origin: string;
  test: { method: string } | null;
  status: string;
  evidenceClass: string | null;
  confidence: number;
  supporting: string[];
  refuting: string[];
  needed: string[];
  impact: number | null;
  impactUnit: string | null;
  computationId: string | null;
  summary: string | null;
};
export type ReviewIssue = { kind: string; severity: string; text: string; action: string };
export type ReviewCycle = { round: number; by: string; verdict: string; issues: ReviewIssue[]; action: string | null };
export type TraceEvent = { atMs: number; kind: string; text: string };
export type InvestigationDetail = {
  row: InvestigationRow;
  roles: string[] | null;
  plan: InvestigationPlan | null;
  steps: StepView[] | null;
  hypotheses: HypothesisView[] | null;
  contradictions: { kind: string; text: string; parties: string[] }[] | null;
  reviews: { cycles: ReviewCycle[]; objections: string[]; grounding?: { checked: number; unsupported: string[] } } | null;
  trace: TraceEvent[] | null;
  limitations: string[] | null;
  summary: string | null;
  inputTokens: number;
  outputTokens: number;
  failureReason: string | null;
};

export type DecisionMetric = { metric: string; filters?: { dimension: string; op: string; values: string[] }[] | null; segment?: string | null };
export type DecisionView = {
  id: string;
  title: string;
  description: string | null;
  reason: string | null;
  module: string;
  decidedOn: string;
  authorId: string;
  authorName: string | null;
  metric: DecisionMetric;
  metricTitle: string | null;
  baselineFrom: string;
  baselineTo: string;
  baselineValue: number | null;
  expectedDirection: "up" | "down" | "keep";
  expectedChangePct: number | null;
  expectedText: string | null;
  reviewOn: string;
  status: string;
  outcome: string | null;
  actualValue: number | null;
  actual: { changePct?: number | null; text?: string; days?: number; evidenceClass?: string; notes?: string[]; error?: string } | null;
  evaluatedAt: string | null;
  links: { investigations?: string[] | null } | null;
  notes: string | null;
  createdAt: string;
  due: boolean;
};

export type CountItem = { key: string; count: number };
export type QualityReport = {
  from: string;
  to: string;
  chat: { answers: number; failed: number; withUnverified: number; successRate: number; incompleteRate: number; avgDurationMs: number; avgCostUsd: number | null; investigated: number };
  investigations: {
    total: number;
    completed: number;
    partial: number;
    failed: number;
    avgSteps: number;
    avgDepth: number;
    avgDurationMs: number;
    avgCostUsd: number | null;
    avgLlmCalls: number;
    avgToolCalls: number;
    reviewRejectionRate: number;
    replanRate: number;
    failureReasons: CountItem[];
  };
  hypotheses: { total: number; byStatus: CountItem[]; byClass: CountItem[] };
  queries: { total: number; errors: number; errorRate: number; topErrors: CountItem[]; byStatus: CountItem[] };
  computations: { total: number; byStatus: CountItem[]; topErrors: CountItem[]; byMethod: CountItem[] };
  toolErrors: CountItem[];
  reviewIssues: CountItem[];
  benchmarks: { id: string; mode: string; status: string; total: number; passed: number; score: number; startedAt: string; model: string | null; costUsd: number | null }[];
  weakSpots: { area: string; text: string; count: number; severity: string }[];
  research?: ResearchQuality | null;
};

/** Исследования без модели за период: итоги, время (всего, срезы, расчёты), операции, кэш, повторы, шаблоны, статусы и падающие операции. */
export type ResearchQuality = {
  total: number;
  completed: number;
  partial: number;
  failed: number;
  cancelled: number;
  avgDurationMs: number;
  avgSqlMs: number;
  avgStatisticsMs: number;
  avgOperations: number;
  cacheHitRatio: number;
  retries: number;
  byKind: CountItem[];
  operationStatuses: CountItem[];
  failedOperations: CountItem[];
};

/** Разбор ответа: почему AI ответил так или не ответил. */
export type AnswerExplanation = {
  conversationId: string | null;
  messageId: string | null;
  at: string;
  question: string;
  status: string;
  error: string | null;
  agents: string | null;
  tools: string | null;
  durationMs: number;
  unsupportedNumbers: number;
  items: { area: string; status: string; text: string }[];
  verdict: string;
};

export const hypothesisStatus: Record<string, { label: string; tone: string }> = {
  confirmed: { label: "Подтверждена", tone: "bg-ok/15 text-ok" },
  partially_confirmed: { label: "Подтверждена частично", tone: "bg-warn/15 text-warn" },
  rejected: { label: "Отклонена", tone: "bg-bad/15 text-bad" },
  inconclusive: { label: "Не доказана", tone: "bg-muted text-ink-3" },
};

export const evidenceClass: Record<string, string> = {
  observed: "Наблюдаемая зависимость",
  statistical: "Статистически подтверждённая связь",
  tested_causal: "Проверенная причинная гипотеза",
  assumption: "Предположение, требует проверки",
};

export const investigationStatus: Record<string, { label: string; tone: string }> = {
  running: { label: "Идёт", tone: "bg-accent-soft text-accent-strong" },
  completed: { label: "Завершено", tone: "bg-ok/15 text-ok" },
  partial: { label: "Частично", tone: "bg-warn/15 text-warn" },
  failed: { label: "Без данных", tone: "bg-bad/15 text-bad" },
  planned: { label: "Запланировано", tone: "bg-muted text-ink-3" },
  cancelled: { label: "Отменено", tone: "bg-muted text-ink-3" },
};

export const roleTitle: Record<string, string> = {
  planner: "Планировщик",
  investigator: "Исследователь",
  domain_expert: "Эксперт отдела",
  statistician: "Статистик",
  hypothesis_analyst: "Аналитик гипотез",
  skeptic: "Скептик",
  reviewer: "Ревизор",
  decision_advisor: "Советник по решениям",
};

export const stepStatus: Record<string, string> = {
  completed: "готово",
  partial: "частично",
  no_data: "нет данных",
  failed: "сбой",
  forbidden: "нет доступа",
};

export const outcomeLabel: Record<string, { label: string; tone: string }> = {
  achieved: { label: "Ожидание выполнено", tone: "bg-ok/15 text-ok" },
  partial: { label: "Выполнено частично", tone: "bg-warn/15 text-warn" },
  missed: { label: "Не выполнено", tone: "bg-bad/15 text-bad" },
  inconclusive: { label: "Пока не ясно", tone: "bg-muted text-ink-3" },
};

export const directionLabel: Record<string, string> = { up: "рост", down: "снижение", keep: "без изменений" };

/** Замечания проверки ответа (Reviewer) — словами. */
export const reviewIssue: Record<string, string> = {
  numbers: "Числа не подтверждены данными",
  rejected_cause: "Причиной названа отклонённая гипотеза",
  unproven_cause: "Причина без подтверждённой гипотезы",
  limitations: "Не названы ограничения данных",
  period: "Период вывода не совпадает с данными",
  recalc: "Пересчёт финансового вывода не совпал",
};

export function reviewIssueLabel(kind: string): string {
  return reviewIssue[kind] ?? (kind.startsWith("model:") ? `Независимая проверка: ${kind.slice(6)}` : kind);
}

export const traceKind: Record<string, string> = {
  triage: "Сложность",
  plan: "План",
  step: "Шаг",
  hypothesis: "Гипотеза",
  contradiction: "Противоречие",
  replan: "Перепланирование",
  skeptic: "Скептик",
  review: "Проверка",
  recalc: "Пересчёт",
  model: "Модель",
};

/** Длительность: 850 мс, 12,4 с, 2 мин 05 с. */
export function duration(ms: number | null | undefined): string {
  if (ms == null || !Number.isFinite(ms)) return "—";
  if (ms < 1000) return `${Math.round(ms)} мс`;
  const s = ms / 1000;
  if (s < 60) return `${s.toFixed(1).replace(".", ",")} с`;
  const m = Math.floor(s / 60);
  return `${m} мин ${String(Math.round(s - m * 60)).padStart(2, "0")} с`;
}

/** Стоимость в долларах: нет цены — «—», меньше цента — «< $0,01». */
export function usd(value: number | null | undefined): string {
  if (value == null || !Number.isFinite(value)) return "—";
  if (value > 0 && value < 0.01) return "< $0,01";
  return `$${value.toFixed(2).replace(".", ",")}`;
}

/** Доля 0–1 → «72 %». */
export function share(value: number | null | undefined): string {
  if (value == null || !Number.isFinite(value)) return "—";
  return `${Math.round(value * 100)} %`;
}

/**
 * Волны плана (шаги, которые идут параллельно), если сервер не прислал их сам: по раундам (шаги перепланирования — после исходного плана),
 * внутри раунда — по зависимостям; цикл — следующим шагом.
 */
export function waves(steps: PlanStep[]): string[][] {
  const rounds = [...new Set(steps.map((s) => s.round ?? 0))].sort((a, b) => a - b);
  if (rounds.length > 1) return rounds.flatMap((r) => waves(steps.filter((s) => (s.round ?? 0) === r).map((s) => ({ ...s, round: 0 }))));
  const done = new Set<string>();
  const left = [...steps];
  const out: string[][] = [];
  while (left.length) {
    let wave = left.filter((s) => s.dependsOn.every((d) => done.has(d)));
    if (!wave.length) wave = [left[0]];
    out.push(wave.map((s) => s.id));
    for (const s of wave) {
      done.add(s.id);
      left.splice(left.indexOf(s), 1);
    }
  }
  return out;
}

/** Черновик решения из ссылки AI (?draft=base64url JSON); испорченный — null. */
export type DecisionDraft = {
  title: string;
  reason?: string | null;
  metric: string;
  filters?: { dimension: string; op: string; values: string[] }[];
  expectedDirection: "up" | "down" | "keep";
  expectedChangePct?: number | null;
  decidedOn: string;
  reviewOn: string;
};

export function decodeDraft(value: string | null | undefined): DecisionDraft | null {
  if (!value) return null;
  try {
    const b64 = value.replace(/-/g, "+").replace(/_/g, "/");
    const padded = b64 + "=".repeat((4 - (b64.length % 4)) % 4);
    const bytes = Uint8Array.from(atob(padded), (c) => c.charCodeAt(0));
    const draft = JSON.parse(new TextDecoder().decode(bytes));
    if (!draft || typeof draft.metric !== "string" || !["up", "down", "keep"].includes(draft.expectedDirection)) return null;
    return draft as DecisionDraft;
  } catch {
    return null;
  }
}

/** Решения по разделам: ждут проверки (срок наступил), активные, оценённые, отменённые. */
export function groupDecisions(list: DecisionView[]): { due: DecisionView[]; active: DecisionView[]; evaluated: DecisionView[]; cancelled: DecisionView[] } {
  return {
    due: list.filter((d) => d.status === "active" && d.due),
    active: list.filter((d) => d.status === "active" && !d.due),
    evaluated: list.filter((d) => d.status === "evaluated"),
    cancelled: list.filter((d) => d.status === "cancelled"),
  };
}

/** Счётчик по ключу из списка CountItem (нет — 0). */
export function count(items: CountItem[] | null | undefined, key: string): number {
  return items?.find((x) => x.key === key)?.count ?? 0;
}

// ---------- Исследования-шаблоны (Phase 2): план операций, выводы с уровнем доказательности, таблицы и матрицы ----------

export type ResearchOperation = {
  id: string;
  type: string;
  title: string;
  source: string;
  query?: { metrics?: string[]; dimensions?: string[] | null; grain?: string | null; dateFrom?: string | null; dateTo?: string | null } | null;
  compute?: { method: string; data?: { metrics?: string[]; dimension?: string | null; grain?: string | null; from?: string | null; to?: string | null }; params?: Record<string, unknown> | null } | null;
  dependsOn: string[];
  optional?: boolean;
  status: string;
  resultReference?: string | null;
  errorReason?: string | null;
  attempts: number;
  fromCache: boolean;
  elapsedMs: number;
  summary?: string | null;
  note?: string | null;
};
export type ResearchPlanJson = {
  kind: string;
  userQuestion: string;
  objective: string;
  from: string;
  to: string;
  compareFrom?: string | null;
  compareTo?: string | null;
  requiredMetrics: string[];
  requiredSources: string[];
  expectedResults: string[];
  validationRules: string[];
  accessScope: string;
  executionStatus: string;
};
export type ResearchFinding = { section: string; level: string; text: string; value?: number | null; unit?: string | null; evidence?: string[] | null };
export type ResearchHypothesis = { id: string; statement: string; status: string; level: string; basis: string; evidence: string[] };
export type ResearchTable = { id: string; title: string; columns: { code: string; title: string; unit?: string | null }[]; rows: (string | number | null)[][] };
export type ResearchMatrix = { id: string; title: string; titles: string[]; r: (number | null)[][]; q: (number | null)[][]; n: number };
export type ResearchTimings = {
  planMs: number;
  sqlMs: number;
  statisticsMs: number;
  evidenceMs: number;
  totalMs: number;
  operations: number;
  queries: number;
  computations: number;
  cacheHits: number;
  retries: number;
  cacheHitRatio: number;
};
export type ResearchDetail = {
  kind: string;
  plan: ResearchPlanJson;
  operations: ResearchOperation[];
  findings: ResearchFinding[];
  hypotheses: ResearchHypothesis[];
  tables: ResearchTable[];
  matrices: ResearchMatrix[];
  timings: ResearchTimings | null;
  sources: { title: string; dataThrough?: string | null; syncedAt?: string | null }[];
  limitations: string[];
};

export const researchKinds: { kind: string; title: string; hint: string }[] = [
  { kind: "correlations", title: "Корреляции и закономерности", hint: "Все пары показателей продаж и визитов, товарные группы и регионы против итога и остальных" },
  { kind: "decline_drivers", title: "Причины изменения продаж", hint: "Где и за счёт чего: регионы, группы, ТП, точки, цена, объём, ассортимент, отток, визиты" },
  { kind: "agent_efficiency", title: "Эффективность ТП", hint: "Визиты, маршрут, страйк и выручка на визит по каждому ТП" },
  { kind: "visits_vs_sales", title: "Визиты и продажи по регионам", hint: "Где визитов больше, а продаж меньше — с проверкой трендов" },
  { kind: "group_influence", title: "Влияние товарных групп", hint: "Доля, вклад в колебания и в изменение итога, связь с остальными" },
  { kind: "anomalies", title: "Аномалии показателей", hint: "Все показатели с учётом дня недели, сдвиги уровня" },
  { kind: "promo_effect", title: "Эффективность акций", hint: "Прирост к контрольной группе, скидки и отдача" },
  { kind: "company_health", title: "Состояние компании", hint: "Показатели, план, тренд, аномалии, отток, запасы и открытые риски" },
];

export const evidenceLevel: Record<string, { label: string; tone: string }> = {
  FACT: { label: "Факт", tone: "bg-muted text-ink-2" },
  CALCULATED: { label: "Расчёт", tone: "bg-accent-soft text-accent-strong" },
  STATISTICALLY_SUPPORTED: { label: "Статистически подтверждено", tone: "bg-ok/15 text-ok" },
  CORRELATION: { label: "Корреляция", tone: "bg-accent/10 text-accent-strong" },
  HYPOTHESIS: { label: "Гипотеза", tone: "bg-warn/15 text-warn" },
  FORECAST: { label: "Прогноз", tone: "bg-muted text-ink-3" },
  UNVERIFIED: { label: "Не проверено", tone: "bg-bad/10 text-bad" },
};

export const operationStatus: Record<string, { label: string; tone: string }> = {
  SUCCESS: { label: "готово", tone: "bg-ok/15 text-ok" },
  PARTIAL_RESULT: { label: "частично", tone: "bg-warn/15 text-warn" },
  DATA_NOT_FOUND: { label: "данных нет", tone: "bg-muted text-ink-3" },
  TOOL_UNSUPPORTED: { label: "не поддерживается", tone: "bg-muted text-ink-3" },
  SOURCE_UNAVAILABLE: { label: "источник недоступен", tone: "bg-bad/15 text-bad" },
  NO_ACCESS: { label: "нет доступа", tone: "bg-bad/10 text-bad" },
  INSUFFICIENT_GRANULARITY: { label: "нет детализации", tone: "bg-muted text-ink-3" },
  CALCULATION_FAILED: { label: "расчёт не выполнен", tone: "bg-bad/15 text-bad" },
  VALIDATION_FAILED: { label: "проверка не пройдена", tone: "bg-bad/15 text-bad" },
  SKIPPED: { label: "пропущено", tone: "bg-muted text-ink-3" },
  CANCELLED: { label: "отменено", tone: "bg-muted text-ink-3" },
  RUNNING: { label: "выполняется", tone: "bg-accent-soft text-accent-strong" },
  PENDING: { label: "ожидает", tone: "bg-muted text-ink-3" },
};

/** Разделы ответа исследования по ТЗ — в этом порядке. */
export const researchSections: { section: string; title: string }[] = [
  { section: "summary", title: "Краткий вывод" },
  { section: "metrics", title: "Основные показатели" },
  { section: "patterns", title: "Найденные закономерности" },
  { section: "correlations", title: "Корреляции" },
  { section: "significance", title: "Статистическая значимость" },
  { section: "causes", title: "Причины и гипотезы" },
  { section: "hypotheses", title: "Исследовательские гипотезы" },
  { section: "differences", title: "Различия" },
  { section: "risks", title: "Риски" },
  { section: "recommendations", title: "Рекомендации" },
  { section: "limitations", title: "Ограничения" },
];

/** Исследование-шаблон (роль research) — из записи исследования; иначе null (исследование модели показывается прежним видом). */
export function researchOf(detail: { roles: string[] | null; plan: unknown; steps: unknown; hypotheses: unknown; limitations: string[] | null }): ResearchDetail | null {
  if (!detail.roles?.includes("research")) return null;
  const plan = (detail.plan ?? {}) as ResearchPlanJson;
  const result = (detail.hypotheses ?? {}) as { findings?: ResearchFinding[]; hypotheses?: ResearchHypothesis[]; data?: { tables?: ResearchTable[]; matrices?: ResearchMatrix[] }; timings?: ResearchTimings; sources?: ResearchDetail["sources"] };
  return {
    kind: detail.roles[1] ?? plan.kind,
    plan,
    operations: Array.isArray(detail.steps) ? (detail.steps as ResearchOperation[]) : [],
    findings: result.findings ?? [],
    hypotheses: result.hypotheses ?? [],
    tables: result.data?.tables ?? [],
    matrices: result.data?.matrices ?? [],
    timings: result.timings ?? null,
    sources: result.sources ?? [],
    limitations: detail.limitations ?? [],
  };
}

/** Цвет ячейки матрицы: положительная связь — акцент, отрицательная — красный; насыщенность — |r|; незначимая (q ≥ 0,05) — бледнее. */
export function matrixCell(r: number | null | undefined, q: number | null | undefined): { background: string; strong: boolean } {
  if (r == null || !Number.isFinite(r)) return { background: "transparent", strong: false };
  const strong = q != null && q < 0.05;
  const alpha = Math.round(Math.min(1, Math.abs(r)) * (strong ? 70 : 30));
  const color = r >= 0 ? "var(--color-accent)" : "var(--color-bad)";
  return { background: `color-mix(in oklab, ${color} ${alpha}%, transparent)`, strong };
}

/** Число ячейки таблицы исследования: доли и % — один знак, деньги и счётчики — без дробей. */
export function cellText(value: string | number | null | undefined, unit?: string | null): string {
  if (value == null) return "—";
  if (typeof value === "string") return value;
  if (!Number.isFinite(value)) return "—";
  const digits = unit === "%" || Math.abs(value) < 10 ? (Math.abs(value) < 1 && value !== 0 ? 3 : 1) : 0;
  const text = value.toLocaleString("ru-RU", { minimumFractionDigits: 0, maximumFractionDigits: digits }).replace(/ /g, " ");
  return unit === "%" ? `${text} %` : text;
}
