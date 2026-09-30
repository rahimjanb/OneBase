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
] as const;

export const modelLabel = (m: AiModelView) => m.displayName ?? m.model;

export const aiCrumbs = [
  { label: "OneBase", href: "/" },
  { label: "Настройки", href: "/settings" },
];
