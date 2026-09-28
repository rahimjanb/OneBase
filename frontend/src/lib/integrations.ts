import type { SyncProgress } from "@/lib/sales/types";

export type IntegrationStatus = "connected" | "error" | "disabled" | "not_configured";

export type IntegrationSummary = {
  code: string;
  name: string;
  description: string;
  status: IntegrationStatus;
  dataAsOf: string | null;
};

export type DepartmentIntegrations = { code: string; name: string; integrations: IntegrationSummary[] };

export type LinkoDetails = {
  code: string;
  name: string;
  description: string;
  departmentCode: string;
  departmentName: string;
  status: IntegrationStatus;
  baseUrl: string;
  baseUrlFromEnvironment: boolean;
  enabled: boolean;
  hasToken: boolean;
  tokenHint: string | null;
  tokenSource: "OneBase" | "Environment" | "None";
  updatedAt: string | null;
  lastTest: { at: string; ok: boolean | null; message: string | null } | null;
  sync: {
    isRunning: boolean;
    progress: SyncProgress | null;
    dataAsOf: string | null;
    entities: { entity: string; lastSuccessAt: string | null; lastRows: number; lastError: string | null }[];
  };
};

export type LinkoTestResult = {
  ok: boolean;
  message: string;
  users: number | null;
  markets: number | null;
  orders: number | null;
  elapsedMs: number;
};

/** Названия данных Linko для людей. */
export const linkoEntityLabels: Record<string, string> = {
  orders: "Заказы",
  order_returns: "Возвраты",
  visits: "Визиты",
  markets: "Торговые точки",
  users: "Агенты и пользователи",
  products: "Товары",
  product_types: "Категории товаров",
  borders: "Территории",
  market_users: "Закрепление ТТ за агентами",
  kpi_plans: "Планы KPI",
  visits_month: "Визиты за месяц (полное обновление)",
  source: "Источник данных",
};

export const statusView: Record<IntegrationStatus, { label: string; className: string }> = {
  connected: { label: "Подключено", className: "bg-ok-soft text-ok" },
  error: { label: "Ошибка синхронизации", className: "bg-warn-soft text-warn" },
  disabled: { label: "Выключено", className: "bg-muted text-ink-2" },
  not_configured: { label: "Не настроено", className: "bg-muted text-ink-2" },
};

/** «Заказы · текущий и прошлый месяц · 12 000 строк». */
export function progressText(progress: SyncProgress | null): string {
  if (!progress) return "Обновляется…";
  const entity = progress.entity ? linkoEntityLabels[progress.entity] ?? progress.entity : null;
  const rows = progress.rows > 0 ? ` · ${new Intl.NumberFormat("ru-RU").format(progress.rows)} строк` : "";
  return [entity, progress.phase].filter(Boolean).join(" · ") + rows;
}