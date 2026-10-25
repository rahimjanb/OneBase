import type { T } from "@/i18n/core";
import type { CorrectionReview, GroundingSummary } from "./consultant";

export type AiProviderStatus = "connected" | "error" | "disabled" | "not_configured";

export type AiProviderView = {
  code: string;
  name: string;
  defaultBaseUrl: string;
  supportsEmbeddings: boolean;
  baseUrl: string | null;
  enabled: boolean;
  hasKey: boolean;
  keyHint: string | null;
  keySource: "OneBase" | "Environment" | "None";
  status: AiProviderStatus;
  updatedAt: string | null;
  lastTest: { at: string; ok: boolean | null; message: string | null } | null;
  modelsRefreshedAt: string | null;
  modelsAvailable: number;
  modelsEnabled: number;
};

export type AiProviderTestResult = { ok: boolean; message: string; models: number | null; elapsedMs: number };

export type AiModelView = {
  id: string;
  provider: string;
  model: string;
  displayName: string | null;
  available: boolean;
  enabled: boolean;
  inputPricePerMillion: number | null;
  outputPricePerMillion: number | null;
  cacheReadPricePerMillion: number | null;
  cacheWritePricePerMillion: number | null;
  createdAt: string | null;
};

export type AiModelPriceKey = "inputPricePerMillion" | "outputPricePerMillion" | "cacheReadPricePerMillion" | "cacheWritePricePerMillion";

export type AiModelRef = { provider: string; model: string };

export type AiSettingsView = {
  enabled: boolean;
  primary: AiModelRef | null;
  primaryFromEnvironment: boolean;
  fallback: AiModelRef | null;
  router: AiModelRef | null;
  embedding: AiModelRef | null;
  temperature: number | null;
  maxOutputTokens: number;
  /** low | medium | high; null — по умолчанию модели. */
  reasoningEffort: string | null;
  /** Бюджет на AI, USD; null — без ограничения. */
  dailyBudgetUsd: number | null;
  monthlyBudgetUsd: number | null;
  budgetWarnPercent: number;
  /** Одновременных вызовов моделей; 0 — без ограничения. */
  maxConcurrentCalls: number;
  /** Отдельные модели для простых задач (общий флаг) и режимы категорий: off | shadow | on. */
  taskRoutingEnabled: boolean;
  routerMode: string;
  extract: AiModelRef | null;
  extractMode: string;
  ready: boolean;
  readinessMessage: string | null;
  updatedAt: string | null;
};

export type AiModelTestResult = {
  ok: boolean;
  message: string;
  reply: string | null;
  usage: { inputTokens: number; outputTokens: number } | null;
  elapsedMs: number;
  usedFallback: boolean;
  model: string | null;
};

export const aiProviderStatus: Record<AiProviderStatus, { label: string; className: string }> = {
  connected: { label: "Подключён", className: "bg-ok-soft text-ok" },
  error: { label: "Ошибка проверки", className: "bg-bad-soft text-bad" },
  disabled: { label: "Выключен", className: "bg-muted text-ink-2" },
  not_configured: { label: "Нет ключа", className: "bg-warn-soft text-warn" },
};

/** Разделы «Настройки → AI». */
export const aiSettingsTabs = [
  { href: "/settings/ai", label: "Общие" },
  { href: "/settings/ai/providers", label: "Провайдеры" },
  { href: "/settings/ai/models", label: "Модели" },
  { href: "/settings/ai/agents", label: "Агенты" },
  { href: "/settings/ai/tools", label: "Инструменты" },
  { href: "/settings/ai/knowledge", label: "База знаний" },
  { href: "/settings/ai/usage", label: "Использование" },
  { href: "/settings/ai/routing", label: "Модели по задачам" },
  { href: "/settings/ai/logs", label: "Журнал" },
] as const;

export const modelLabel = (m: AiModelView) => m.displayName ?? m.model;

export const aiCrumbs = [
  { label: "OneBase", href: "/" },
  { label: "Настройки", href: "/settings" },
];

export type AiAgentListItem = {
  code: string;
  name: string;
  role: string | null;
  description: string | null;
  enabled: boolean;
  isConsultant: boolean;
  model: AiModelRef | null;
  requiredPermission: string | null;
  sources: number;
  tools: number;
  promptIsDefault: boolean;
};

export type AiAgentPublic = {
  code: string;
  name: string;
  role: string | null;
  description: string | null;
  enabled: boolean;
  isConsultant: boolean;
  available: boolean;
};

export type AiAgentSettings = {
  code: string;
  name: string;
  role: string | null;
  description: string | null;
  isConsultant: boolean;
  systemPrompt: string;
  defaultPrompt: string | null;
  promptIsDefault: boolean;
  model: AiModelRef | null;
  temperature: number | null;
  maxOutputTokens: number | null;
  enabled: boolean;
  requiredPermission: string | null;
  updatedAt: string | null;
  permissions: { code: string; label: string }[];
  sources: { code: string; name: string; description: string; tables: string; reports: string; requiredPermission: string | null; enabled: boolean }[];
  tools: { name: string; title: string; description: string; source: string; requiredPermission: string | null; enabled: boolean; requiresApproval: boolean }[];
};

export type AiToolView = {
  name: string;
  title: string;
  description: string;
  source: string;
  sourceName: string | null;
  requiredPermission: string | null;
  inputSchema: { properties?: Record<string, { type: string; description?: string }> };
  agents: string[];
};

export type KnowledgeDocumentView = {
  id: string;
  title: string;
  fileName: string;
  sizeBytes: number;
  departmentCode: string | null;
  requiredPermission: string | null;
  status: "Indexing" | "Indexed" | "Failed";
  error: string | null;
  chunks: number;
  characters: number;
  embeddingModel: string | null;
  createdAt: string;
};

export type KnowledgeList = {
  extensions: string[];
  maxBytes: number;
  departments: { code: string; name: string }[];
  permissions: string[];
  documents: KnowledgeDocumentView[];
};

export const permissionLabels: Record<string, string> = {
  "sales.read": "Просмотр продаж",
  "finance.read": "Финансовые данные",
  "hr.read": "Кадровые данные",
  "files.read": "Просмотр файлов",
};

export type AiAlertSeverity = "Critical" | "Warning" | "Opportunity";

export type AiAlert = {
  id: string;
  category: string;
  rule: string;
  severity: AiAlertSeverity;
  title: string;
  message: string;
  recommendation: string | null;
  href: string | null;
  detectedAt: string;
  lastSeenAt: string;
  resolvedAt: string | null;
};

export type AiAlertsView = {
  summary: {
    problems: number;
    critical: number;
    recommendations: number;
    opportunities: number;
    byCategory: { category: string; problems: number; critical: number; opportunities: number }[];
  };
  lastRun: { at: string; active: number; created: number; resolved: number; errors: string[] } | null;
  alerts: AiAlert[];
};

export const categoryNames: Record<string, string> = {
  finance: "Финансы",
  sales: "Продажи",
  marketing: "Маркетинг",
  hr: "HR",
  production: "Производство",
  supply: "Снабжение",
};

/** inputTokens — вход без кэша; cacheReadTokens / cacheWriteTokens — кэш промпта (тариф другой); promptTokens — весь промпт. */
type UsageTotals = {
  calls: number;
  inputTokens: number;
  cacheReadTokens: number;
  cacheWriteTokens: number;
  promptTokens: number;
  outputTokens: number;
  costUsd: number;
  costIncomplete: boolean;
};

type UsageSlice = { calls: number; tokens: number; cacheReadTokens: number; costUsd: number };

export type AiUsageView = {
  from: string;
  days: number;
  budget: {
    dailyUsd: number | null;
    monthlyUsd: number | null;
    warnPercent: number;
    spentToday: number;
    spentMonth: number;
    exceeded: boolean;
    warning: boolean;
  };
  /** fromCache — ответы из кэша ответов консультанта, без вызовов модели. */
  today: { requests: number; fromCache: number; usage: UsageTotals };
  period: { requests: number; failedRequests: number; fromCache: number; avgResponseMs: number; failedCalls: number; usage: UsageTotals };
  byDay: { date: string; requests: number; inputTokens: number; cacheReadTokens: number; cacheWriteTokens: number; outputTokens: number; costUsd: number }[];
  byModel: {
    model: string;
    calls: number;
    failed: number;
    inputTokens: number;
    cacheReadTokens: number;
    cacheWriteTokens: number;
    outputTokens: number;
    costUsd: number;
    priceMissing: boolean;
    avgMs: number;
  }[];
  byUser: ({ user: string; requests: number } & UsageSlice)[];
  byDepartment: ({ department: string; requests: number } & UsageSlice)[];
  byAgent: ({ agent: string } & UsageSlice)[];
  byPurpose: ({ purpose: string } & UsageSlice)[];
};

export type AiLogRow = {
  id: string;
  createdAt: string;
  user: string;
  kind: "consultant" | "agent";
  conversationId: string | null;
  question: string;
  agents: string[];
  model: string | null;
  tools: string[];
  sources: { title: string; period: string | null; href: string | null }[] | null;
  inputTokens: number;
  outputTokens: number;
  costUsd: number | null;
  durationMs: number;
  status: "completed" | "failed";
  responsePreview: string | null;
  error: string | null;
  /** Проверка ответа по данным: числа (check) и перепроверка после возражения (correction). */
  grounding?: { check: GroundingSummary | null; correction: CorrectionReview | null } | null;
  correctionVerdict?: CorrectionReview["verdict"] | null;
  correctionOfMessageId?: string | null;
};

/**
 * Единицы в готовом тексте сервера («12,3 т», «5 400 кг», «1,2 млн сум», «3 дн.») — на языке интерфейса.
 * Нужно находкам AI Dashboard: числа с единицами сервер подставляет в текст, и шаблон перевода оставляет их как есть.
 */
export function localizeUnits(text: string, t: T): string {
  return text.replace(/(\d)(\s)(млн сум|сум|кг|т|дн\.)(?=$|[\s.,;:)])/g, (_, digit: string, space: string, unit: string) =>
    `${digit}${space}${unit.split(" ").map((u) => t(u)).join(" ")}`,
  );
}

/** Текст находки AI Dashboard (заголовок, описание, рекомендация) на языке интерфейса. */
export const alertText = (text: string, t: T) => localizeUnits(t(text), t);

/** Служебные «пользователи» журнала и учёта AI (имена людей — данные, их не переводим). */
const systemUsers = new Set(["система", "удалённый пользователь"]);
export const userLabel = (name: string, t: T) => (systemUsers.has(name) ? t(name) : name);

export const purposeNames: Record<string, string> = {
  "consultant.route": "Маршрутизация",
  "consultant.route.fallback": "Маршрутизация — повтор основной моделью",
  "consultant.route.shadow": "Маршрутизация — сравнение в фоне",
  "eval.route": "Маршрутизация — эталонный набор",
  "consultant.answer": "Ответ консультанта",
  "consultant.revise": "Проверка ответа",
  agent: "AI-сотрудники",
  "data-manager": "AI Data Manager",
  "telegram.sent_only": "Telegram-агент",
  "datasources.automap": "Подбор колонок источника",
  "datasources.automap.fallback": "Подбор колонок — повтор основной моделью",
  "datasources.automap.shadow": "Подбор колонок — сравнение в фоне",
  "knowledge.index": "Индексация базы знаний",
  "knowledge.search": "Поиск по базе знаний",
  "settings.test": "Проверка модели",
  chat: "Прочее",
};

type ComparisonSideTotals = {
  inputTokens: number;
  cacheReadTokens: number;
  cacheWriteTokens: number;
  outputTokens: number;
  costUsd: number;
  costIncomplete: boolean;
  avgMs: number;
};

export type EvalModelSummary = {
  model: string;
  cases: number;
  valid: number;
  correct: number;
  errors: number;
  inputTokens: number;
  cacheReadTokens: number;
  cacheWriteTokens: number;
  outputTokens: number;
  avgMs: number;
};

export type EvalRun = {
  category: string;
  status: "running" | "done" | "failed";
  startedAt: string;
  finishedAt: string | null;
  done: number;
  total: number;
  baseline: EvalModelSummary | null;
  candidate: EvalModelSummary | null;
  agree: number;
  cases: { code: string; question: string; expected: string; baseline: string | null; baselineCorrect: boolean; candidate: string | null; candidateCorrect: boolean; candidateError: string | null }[];
  error: string | null;
};

/** «Модели по задачам»: сравнение исходной модели и модели категории (фон и эталонный набор). */
export type AiRoutingView = {
  days: number;
  settings: {
    taskRoutingEnabled: boolean;
    primary: string | null;
    router: string | null;
    routerMode: string;
    extract: string | null;
    extractMode: string;
    route: { model: string | null; fallback: string | null; shadow: string | null; baseline: string | null };
    extractRoute: { model: string | null; fallback: string | null; shadow: string | null; baseline: string | null };
  };
  groups: {
    category: string;
    source: string;
    baselineModel: string;
    candidateModel: string;
    count: number;
    agree: number;
    similarity: number;
    baselineValid: number;
    candidateValid: number;
    graded: number;
    baselineCorrect: number;
    candidateCorrect: number;
    candidateErrors: number;
    baseline: ComparisonSideTotals;
    candidate: ComparisonSideTotals;
  }[];
  recent: {
    createdAt: string;
    category: string;
    source: string;
    caseCode: string | null;
    task: string | null;
    expected: string | null;
    baselineModel: string;
    candidateModel: string;
    baselineResult: string | null;
    candidateResult: string | null;
    agree: boolean;
    similarity: number;
    baselineValid: boolean;
    candidateValid: boolean;
    baselineCorrect: boolean | null;
    candidateCorrect: boolean | null;
    baselineCostUsd: number | null;
    candidateCostUsd: number | null;
    baselineMs: number;
    candidateMs: number;
    candidateError: string | null;
  }[];
  evaluation: EvalRun | null;
  evalCases: number;
};
