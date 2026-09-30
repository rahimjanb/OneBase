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
  createdAt: string | null;
};

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
