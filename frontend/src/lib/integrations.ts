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
  hasPlanToken: boolean;
  planTokenHint: string | null;
  planTokenSource: "OneBase" | "Environment" | "None";
  updatedAt: string | null;
  lastTest: { at: string; ok: boolean | null; message: string | null } | null;
  sync: {
    isRunning: boolean;
    isCancelling?: boolean;
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
  plansOk: boolean | null;
  plansMessage: string | null;
  plansLastDate: string | null;
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
  staff_balance: "Планы агентов (пересчёт Linko)",
  stocks: "Склады",
  product_balances: "Остатки (штуки)",
  stock_transfers: "Перемещения между складами",
  payments: "Платежи",
  price_lists: "Прайс-листы",
  price_list_items: "Цены прайс-листов",
  providers: "Поставщики",
  currencies: "Валюты",
  contracts: "Договоры",
};

export const statusView: Record<IntegrationStatus, { label: string; className: string }> = {
  connected: { label: "Подключено", className: "bg-ok-soft text-ok" },
  error: { label: "Ошибка синхронизации", className: "bg-warn-soft text-warn" },
  disabled: { label: "Выключено", className: "bg-muted text-ink-2" },
  not_configured: { label: "Не настроено", className: "bg-muted text-ink-2" },
};

/** «Заказы · История · 12 000 из 30 000 строк». */
export function progressText(progress: SyncProgress | null): string {
  if (!progress) return "Обновляется…";
  const n = (v: number) => new Intl.NumberFormat("ru-RU").format(v);
  const entity = progress.entity ? linkoEntityLabels[progress.entity] ?? progress.entity : null;
  const rows =
    progress.rows > 0 ? ` · ${n(progress.rows)}${progress.expected ? ` из ${n(progress.expected)}` : ""} строк` : progress.expected ? ` · ${n(progress.expected)} строк` : "";
  return [entity, progress.phase].filter(Boolean).join(" · ") + rows;
}

/** «осталось ~3 мин»; null — оценивать ещё рано. */
export function remainingText(progress: SyncProgress | null): string | null {
  const s = progress?.remainingSeconds;
  if (s == null) return null;
  if (s < 60) return "осталось меньше минуты";
  const minutes = Math.round(s / 60);
  return minutes < 60 ? `осталось ~${minutes} мин` : `осталось ~${Math.floor(minutes / 60)} ч ${minutes % 60} мин`;
}

/** Коротко: «42% · осталось ~3 мин». */
export function progressShort(progress: SyncProgress | null): string {
  if (!progress || progress.percent == null || !progress.stepsTotal) return "Обновляется…";
  return [`${progress.percent}%`, remainingText(progress)].filter(Boolean).join(" · ");
}