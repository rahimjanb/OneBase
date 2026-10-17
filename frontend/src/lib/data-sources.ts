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
 * headers — снимок заголовков при сопоставлении (столбец → заголовок): переименовали или переставили столбцы — синхронизация не пишет.
 */
/** Какие месяцы листа брать: all — все (не больше 24), current — текущий месяц, year — текущий год, last — последние months, range — from…to («2026-01»). */
export type PeriodFilter = { mode: "all" | "current" | "year" | "last" | "range"; months?: number | null; from?: string | null; to?: string | null };

export const periodText = (p: PeriodFilter | null | undefined) =>
  !p || p.mode === "all" ? "все месяцы" : p.mode === "current" ? "текущий месяц" : p.mode === "year" ? "текущий год" : p.mode === "last" ? `последние ${p.months} мес.` : `${p.from} — ${p.to}`;

export type FieldBinding = {
  field: string;
  plan?: PlanMapping | null;
  profile?: ProfileMapping | null;
  pnl?: PnlMapping | null;
  replace: boolean;
  monthFromSheet?: boolean;
  currentMonth?: boolean;
  headers?: Record<string, string> | null;
  period?: PeriodFilter | null;
};

/** Статус предложения ИИ: accepted — выбрано сразу, review — проверьте, unmatched — не сопоставлено, rejected — поле или столбец не существуют, blocked — поле ведёт другая привязка. */
export type MappingStatus = "accepted" | "review" | "unmatched" | "rejected" | "blocked";

export type MappingSuggestion = {
  sourceColumn: string;
  header: string;
  targetField: string | null;
  fieldLabel: string | null;
  roleLabel: string | null;
  category: string | null;
  confidence: number;
  reason: string;
  transformType: string;
  requiresConfirmation: boolean;
  status: MappingStatus;
  warnings: string[];
};

export type MappingFieldSetting = { field: string; monthSource: "column" | "sheet" | "current" | "fixed"; year: number | null; month: number | null; period?: PeriodFilter | null };

export type MappingTargetView = { key: string; field: string; label: string; role: string; blocked: string | null };

/** Предложение ИИ после проверки бэкендом. fields — привязка из принятых сразу соответствий; stale — устаревшие сохранённые сопоставления. */
export type SheetMappingProposal = {
  sheet: string;
  suggestions: MappingSuggestion[];
  fieldSettings: MappingFieldSetting[];
  fields: FieldBinding[];
  fieldErrors: string[];
  targets: MappingTargetView[];
  categories: string[];
  notes: string[];
  stale: string[];
  model: string;
  acceptConfidence: number;
  reviewConfidence: number;
};

export type MappingDecision = { sourceColumn: string; targetField: string; category: string | null; transformType: string | null };

export type AutoMapInput = { section: string; connectionId: string; sheets: string[]; sheet: string | null; headerRow: number; bindingId: string | null };

export type AutoMapConfirmInput = AutoMapInput & {
  decisions: MappingDecision[];
  settings: MappingFieldSetting[];
  schedule: Schedule;
  intervalMinutes: number | null;
  dailyAt: string | null;
  enabled: boolean;
  dryRun: boolean;
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
  /** Кто ведёт какие данные. */
  ownership: OwnershipRow[];
  /** Конфликтов, которые ждут исправления таблицы. */
  openConflicts: number;
};

/**
 * Строка карты владения: linko — факт из Linko, google_sheets — поле ведёт привязка Google Sheets (меняет только она), onebase — правится
 * в OneBase (AI Data Manager, настройки продаж), calculated — считается при показе и не хранится.
 */
export type OwnershipRow = {
  key: string;
  label: string;
  group: string;
  owner: "linko" | "google_sheets" | "onebase" | "calculated";
  source: string;
  detail: string | null;
  updatedAt: string | null;
  updatedNote: string | null;
};

/** Конфликт источников: строка таблицы, которую не записали (row), или попытка изменить чужое поле (owner). */
export type ConflictView = {
  id: string;
  source: string;
  sourceLabel: string;
  field: string;
  fieldLabel: string;
  entity: string | null;
  period: string | null;
  kind: "row" | "owner";
  kindLabel: string;
  message: string;
  sourceRef: string | null;
  rule: string | null;
  decision: string;
  needsAction: boolean;
  firstSeenAt: string;
  lastSeenAt: string;
  occurrences: number;
  resolvedAt: string | null;
  binding: string | null;
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
  trigger: "manual" | "schedule" | "chat";
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
  conflicts: () => bff<ConflictView[]>(`${base}/conflicts`),
  autoMap: (input: AutoMapInput) => bff<SheetMappingProposal>(`${base}/auto-map`, { method: "POST", body: JSON.stringify(input) }),
  confirmAutoMap: (input: AutoMapConfirmInput) =>
    bff<{ fields: FieldBinding[]; binding: BindingView | null }>(`${base}/auto-map/confirm`, { method: "POST", body: JSON.stringify(input) }),
  closeConflict: (id: string) => bff<void>(`${base}/conflicts/${id}/close`, { method: "POST" }),
};

export const ownerLabel: Record<OwnershipRow["owner"], { label: string; className: string }> = {
  linko: { label: "Linko", className: "bg-accent-soft text-accent-strong" },
  google_sheets: { label: "Google Sheets", className: "bg-ok-soft text-ok" },
  onebase: { label: "OneBase", className: "bg-muted text-ink-2" },
  calculated: { label: "Расчёт", className: "bg-warn-soft text-warn" },
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
