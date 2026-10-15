import { bff } from "./bff";

/** Поле OneBase, которое можно обновлять из Google Sheets: plan — регион × категория × месяц, profile — регион → текст, pnl — P&L. */
export type DataSourceField = { key: string; label: string; kind: "plan" | "profile" | "pnl"; description: string };

export type SheetInfo = { name: string; rows: number; columns: number };

export type ConnectionView = {
  id: string;
  name: string;
  url: string;
  spreadsheetId: string;
  access: "link" | "service_account";
  serviceAccountEmail: string | null;
  lastCheckedAt: string | null;
  lastCheckOk: boolean | null;
  lastCheckMessage: string | null;
  sheets: SheetInfo[];
  bindings: number;
};

export type PlanMapping = {
  regionColumn: string;
  categoryColumn: string | null;
  valueColumn: string | null;
  valueColumns: { column: string; category: string | null }[] | null;
  monthColumn: string | null;
  yearColumn: string | null;
  year: number | null;
  month: number | null;
  multiplier: number;
};

export type ProfileMapping = { regionColumn: string; valueColumn: string };

/** Длинный реестр P&L: строка — контрагент × статья × месяц. Лист и строку заголовков подставляет привязка. */
export type PnlMapping = {
  sheet: string | null;
  headerRow: number;
  dealerColumn: string | null;
  valueColumns: null;
  lineColumn: string | null;
  valueColumn: string | null;
  yearColumn: string | null;
  monthColumn: string | null;
  year: number | null;
  month: number | null;
  multiplier: number;
};

/**
 * Месяц: monthFromSheet — из названия листа («Сентябрь 2026», «09.2026»): лист на месяц; currentMonth — текущий месяц по Ташкенту на
 * момент синхронизации: с 1-го числа значения идут в новый месяц (те же цифры, что за прошлый, по расписанию не копируются).
 */
export type FieldBinding = {
  field: string;
  plan?: PlanMapping | null;
  profile?: ProfileMapping | null;
  pnl?: PnlMapping | null;
  replace: boolean;
  monthFromSheet?: boolean;
  currentMonth?: boolean;
};

/** «Все листы таблицы» в списке листов привязки — и те, что появятся позже. */
export const AllSheets = "*";

export const sheetsText = (sheets: string[]) => (sheets.includes(AllSheets) ? "все листы" : sheets.map((s) => `«${s}»`).join(", "));

export type Schedule = "manual" | "interval" | "daily";
export type SyncStatus = "ok" | "unchanged" | "conflicts" | "waiting" | "error";

export type BindingView = {
  id: string;
  section: string;
  connectionId: string;
  connectionName: string;
  sheets: string[];
  headerRow: number;
  fields: FieldBinding[];
  schedule: Schedule;
  intervalMinutes: number | null;
  dailyAt: string | null;
  enabled: boolean;
  lastRunAt: string | null;
  lastStatus: SyncStatus | null;
  lastMessage: string | null;
  nextRunAt: string | null;
  running: boolean;
};

export type SectionView = {
  key: string;
  name: string;
  href: string;
  facts: string;
  fields: DataSourceField[];
  bindings: BindingView[];
  boundElsewhere: { field: string; label: string; section: string; sectionName: string; bindingId: string }[];
};

export type DataSourcesOverview = {
  linko: { configured: boolean; enabled: boolean; host: string | null; lastSyncAt: string | null };
  connections: ConnectionView[];
  sections: SectionView[];
  fields: DataSourceField[];
  /** Есть право «AI Data Manager»: без него привязки не сохранить и не запустить. */
  canApply: boolean;
  /** Планы на текущий месяц: сколько регионов с планом сейчас и было в прошлом месяце. */
  monthPlans: { field: string; label: string; period: string; regions: number; previousRegions: number }[];
};

export type SheetPreview = { sheet: string; rows: number; columns: number; letters: string[]; cells: string[][] };

export type FieldResult = {
  field: string;
  label: string;
  status: SyncStatus;
  message: string;
  changeSetId: string | null;
  created: number;
  updated: number;
  deleted: number;
  conflicts: number;
  problems: string[] | null;
};

export type RunView = {
  id: string;
  trigger: "manual" | "schedule";
  startedAt: string;
  finishedAt: string | null;
  status: SyncStatus;
  message: string | null;
  fields: FieldResult[];
  userName: string | null;
};

export type ConnectionInput = { name: string; url: string; access: "link" | "service_account"; serviceAccountKey: string | null };

export type BindingInput = {
  section: string;
  connectionId: string;
  sheets: string[];
  headerRow: number;
  fields: FieldBinding[];
  schedule: Schedule;
  intervalMinutes: number | null;
  dailyAt: string | null;
  enabled: boolean;
};

const base = "settings/data-sources";

export const dataSourcesApi = {
  overview: () => bff<DataSourcesOverview>(base),
  createConnection: (input: ConnectionInput) => bff<ConnectionView>(`${base}/connections`, { method: "POST", body: JSON.stringify(input) }),
  updateConnection: (id: string, input: ConnectionInput) => bff<ConnectionView>(`${base}/connections/${id}`, { method: "PUT", body: JSON.stringify(input) }),
  deleteConnection: (id: string) => bff<void>(`${base}/connections/${id}`, { method: "DELETE" }),
  testConnection: (id: string) => bff<ConnectionView>(`${base}/connections/${id}/test`, { method: "POST" }),
  preview: (connectionId: string, sheet: string) => bff<SheetPreview>(`${base}/preview`, { method: "POST", body: JSON.stringify({ connectionId, sheet }) }),
  saveBinding: (id: string | null, input: BindingInput) =>
    bff<BindingView>(id ? `${base}/bindings/${id}` : `${base}/bindings`, { method: id ? "PUT" : "POST", body: JSON.stringify(input) }),
  deleteBinding: (id: string) => bff<void>(`${base}/bindings/${id}`, { method: "DELETE" }),
  sync: (id: string) => bff<RunView>(`${base}/bindings/${id}/sync`, { method: "POST" }),
  runs: (id: string) => bff<RunView[]>(`${base}/bindings/${id}/runs`),
};

export const statusLabel: Record<SyncStatus, { label: string; className: string }> = {
  ok: { label: "Обновлено", className: "bg-ok-soft text-ok" },
  unchanged: { label: "Без изменений", className: "bg-muted text-ink-2" },
  conflicts: { label: "Ошибки в таблице", className: "bg-warn-soft text-warn" },
  waiting: { label: "Ждёт новый месяц", className: "bg-warn-soft text-warn" },
  error: { label: "Ошибка", className: "bg-bad-soft text-bad" },
};

export function scheduleText(b: { schedule: Schedule; intervalMinutes: number | null; dailyAt: string | null }): string {
  if (b.schedule === "interval" && b.intervalMinutes) {
    const m = b.intervalMinutes;
    return m % 60 === 0 ? `каждые ${m / 60} ч` : `каждые ${m} мин`;
  }
  if (b.schedule === "daily" && b.dailyAt) return `ежедневно в ${b.dailyAt}`;
  return "вручную";
}
