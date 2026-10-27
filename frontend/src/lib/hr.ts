/** Раздел HR: данные Verifix (сотрудники, отметки, табель) — типы ответов /api/hr и /api/integrations/verifix, подписи и форматирование. */

export type HrDayStatus = "Present" | "Absent" | "OnLeave" | "DayOff" | "Expected" | "NoSchedule" | "Unknown" | "NotEmployed";

export type HrDivision = { id: number; name: string; parentId: number | null; departmentCode: string | null; missing: boolean; employees: number };

export type HrSyncBrief = {
  configured: boolean;
  enabled: boolean;
  running: boolean;
  lastSuccessAt: string | null;
  lastRunAt: string | null;
  lastStatus: HrSyncStatus | null;
  lastMessage: string | null;
  tracksThrough: string | null;
  timesheetFrom: string | null;
  timesheetTo: string | null;
};

export type HrContext = {
  hasAccess: boolean;
  seesAll: boolean;
  managesIntegration: boolean;
  canEditNotes: boolean;
  departments: string[];
  divisions: HrDivision[];
  sync: HrSyncBrief;
};

export type HrGroupStat = { id: number | null; name: string; active: number; present: number; late: number; absent: number; onLeave: number; unknown: number; events: number };

export type HrOverview = {
  date: string;
  now: string;
  active: number;
  present: number;
  late: number;
  absent: number;
  onLeave: number;
  expected: number;
  dayOff: number;
  noSchedule: number;
  unknown: number;
  eventsToday: number;
  missingInVerifix: number;
  divisions: HrGroupStat[];
  locations: { id: number | null; name: string; employees: number; events: number }[];
  sync: HrSyncBrief;
  seesAll: boolean;
};

export type HrEmployeeRow = {
  id: string;
  verifixId: number;
  fullName: string;
  phone: string | null;
  status: "W" | "D" | "U" | string;
  hiringDate: string | null;
  dismissalDate: string | null;
  divisionId: number | null;
  division: string | null;
  job: string | null;
  schedule: string | null;
  missingSince: string | null;
  lastEventAt: string | null;
  today: HrDayStatus | null;
  todayLate: boolean;
};

export type HrDay = {
  date: string;
  status: HrDayStatus;
  dayKind: string | null;
  planBegin: string | null;
  planEnd: string | null;
  planMinutes: number | null;
  firstIn: string | null;
  lastOut: string | null;
  events: number;
  late: boolean;
  lateMinutes: number | null;
  earlyLeave: boolean;
  earlyLeaveMinutes: number | null;
  workedMinutes: number | null;
  noExit: boolean;
  workedOnDayOff: boolean;
  leaveName: string | null;
  leaveCode: string | null;
  source: "timesheet" | "events";
  reason: string | null;
};

export type HrSummary = {
  planDays: number;
  presentDays: number;
  lateDays: number;
  lateMinutes: number;
  earlyDays: number;
  earlyMinutes: number;
  absentDays: number;
  leaveDays: number;
  unknownDays: number;
  noExitDays: number;
  workedMinutes: number;
  planMinutes: number;
};

export type HrEvent = {
  id: number;
  trackId: number;
  employeeId: string | null;
  employee: string | null;
  division: string | null;
  location: string | null;
  trackType: string;
  markType: string | null;
  rawTime: string;
  localTime: string;
  occurredAt: string;
  importedAt: string;
  missingSince: string | null;
};

export type HrEmployeeCard = {
  employee: HrEmployeeRow;
  middleName: string | null;
  email: string | null;
  orgUnit: string | null;
  note: string | null;
  noteUpdatedAt: string | null;
  firstSyncedAt: string;
  lastSyncedAt: string;
  canEditNote: boolean;
  from: string;
  to: string;
  days: HrDay[];
  events: HrEvent[];
  summary: HrSummary;
};

export type HrDayRow = { employeeId: string; employee: string; division: string | null; job: string | null; day: HrDay };

export type HrDeviation = {
  date: string;
  employeeId: string;
  employee: string;
  division: string | null;
  kind: "late" | "early" | "absent" | "noexit";
  minutes: number | null;
  planBegin: string | null;
  planEnd: string | null;
  firstIn: string | null;
  lastOut: string | null;
  reason: string | null;
};

export type HrTimesheetRow = { employeeId: string; employee: string; division: string | null; job: string | null; summary: HrSummary; codes: string[] };

export type HrTimesheet = { from: string; to: string; dates: string[]; rows: HrTimesheetRow[]; total: HrSummary; sync: HrSyncBrief };

export type HrReport = {
  from: string;
  to: string;
  employees: number;
  total: HrSummary;
  attendanceRate: number | null;
  punctuality: number | null;
  divisions: { divisionId: number | null; division: string; employees: number; summary: HrSummary; attendanceRate: number | null; punctuality: number | null }[];
  topLate: HrTimesheetRow[];
  topAbsent: HrTimesheetRow[];
  sync: HrSyncBrief;
};

/** «Лидеры месяца» (/api/hr/leaders): ни разу не опоздавшие за месяц — по отработанным часам. */
export type HrLeader = { place: number; employeeId: string; employee: string; division: string | null; job: string | null; workedMinutes: number; planMinutes: number; presentDays: number; planDays: number };

export type HrLeaders = { from: string; to: string; employees: number; onTime: number; leaders: HrLeader[]; sync: HrSyncBrief };

export type PageResult<T> = { items: T[]; total: number; page: number; pageSize: number };

export type HrSyncStatus = "Running" | "Succeeded" | "Partial" | "Failed";

export type HrSyncStep = {
  step: string;
  title: string;
  status: "ok" | "failed" | "skipped";
  rows: number;
  created: number;
  updated: number;
  skipped: number;
  errors: number;
  pages: number;
  message: string | null;
};

export type HrSyncRun = {
  id: string;
  trigger: "auto" | "manual" | "retry";
  startedBy: string | null;
  retryOfId: string | null;
  startedAt: string;
  finishedAt: string | null;
  heartbeatAt: string;
  status: HrSyncStatus;
  created: number;
  updated: number;
  skipped: number;
  errors: number;
  periodFrom: string | null;
  periodTo: string | null;
  message: string | null;
  steps: HrSyncStep[];
};

export type VerifixDetails = {
  code: string;
  name: string;
  description: string;
  departmentCode: string;
  departmentName: string;
  status: "connected" | "error" | "disabled" | "not_configured";
  baseUrl: string;
  clientId: string;
  clientIdFromEnvironment: boolean;
  hasSecret: boolean;
  secretHint: string | null;
  secretSource: "OneBase" | "Environment" | "None";
  filialId: number | null;
  enabled: boolean;
  autoSync: boolean;
  intervalMinutes: number;
  historyDays: number;
  overlapDays: number;
  lateGraceMinutes: number;
  earlyLeaveGraceMinutes: number;
  timeZone: string;
  updatedAt: string | null;
  lastTest: { at: string; ok: boolean | null; message: string | null } | null;
  sync: {
    running: boolean;
    dataAsOf: string | null;
    entities: { entity: string; title: string; lastSuccessAt: string | null; lastRows: number; lastError: string | null; coveredFrom: string | null; coveredThrough: string | null }[];
  };
  employees: { working: number; dismissed: number; notHired: number; missing: number };
  divisions: HrDivision[];
  departments: { code: string; name: string }[];
};

export type VerifixTestResult = { ok: boolean; message: string; organization: string | null; checks: { method: string; ok: boolean; message: string }[]; elapsedMs: number };

export const dayStatusLabel: Record<HrDayStatus, string> = {
  Present: "Пришёл",
  Absent: "Отсутствует",
  OnLeave: "По документу",
  DayOff: "Выходной",
  Expected: "Ожидается",
  NoSchedule: "Нет графика",
  Unknown: "Не ясно",
  NotEmployed: "Не работал",
};

export const dayStatusTone: Record<HrDayStatus, string> = {
  Present: "bg-ok-soft text-ok",
  Absent: "bg-bad-soft text-bad",
  OnLeave: "bg-violet/15 text-violet-light",
  DayOff: "bg-muted text-ink-3",
  Expected: "bg-accent/10 text-accent-strong",
  NoSchedule: "bg-muted text-ink-3",
  Unknown: "bg-warn-soft text-warn",
  NotEmployed: "bg-muted text-ink-3",
};

export const employeeStatusLabel: Record<string, string> = { W: "Работает", D: "Уволен", U: "Не принят" };

/** track_type Verifix: «Вид отметки на момент отправки запроса». P в документации не описан — показываем как есть. */
export const trackTypeLabel: Record<string, string> = {
  I: "Приход",
  O: "Уход",
  C: "Отметка присутствия",
  M: "Продолжение рабочего дня",
  T: "Уход на перерыв",
  N: "Приход с перерыва",
};

export const markTypeLabel: Record<string, string> = {
  P: "пароль",
  T: "отпечаток",
  R: "карта",
  Q: "QR-код",
  F: "лицо",
  M: "веб",
  A: "автоуход",
  C: "авто",
  S: "Smartup",
  J: "корректировка",
  X: "внешний API",
  O: "другое",
};

export const deviationLabel: Record<HrDeviation["kind"], string> = { late: "Опоздание", early: "Ранний уход", absent: "Отсутствие", noexit: "Нет ухода" };

export const syncStatusLabel: Record<HrSyncStatus, string> = { Running: "Идёт", Succeeded: "Успешно", Partial: "Частично", Failed: "Ошибка" };

export const syncStatusTone: Record<HrSyncStatus, string> = {
  Running: "bg-accent/10 text-accent-strong",
  Succeeded: "bg-ok-soft text-ok",
  Partial: "bg-warn-soft text-warn",
  Failed: "bg-bad-soft text-bad",
};

export const triggerLabel: Record<HrSyncRun["trigger"], string> = { auto: "по расписанию", manual: "вручную", retry: "повтор" };

/** Буквы табеля: как в HrService.DayCode. */
export const dayCodeLegend: [code: string, label: string][] = [
  ["Я", "явка"],
  ["ОП", "опоздание"],
  ["РУ", "ранний уход"],
  ["РВ", "работа в нерабочий день"],
  ["ОТС", "отсутствие"],
  ["ОТ, Б, К, О, НО", "отпуск, больничный, командировка, отгул — по документу"],
  ["В / П", "выходной / праздник"],
  ["·", "день ещё не начался"],
  ["—", "нет графика"],
  ["?", "данных недостаточно"],
];

export const dayCodeTone = (code: string): string =>
  code === "ОТС"
    ? "bg-bad-soft text-bad"
    : code === "ОП" || code === "РУ"
      ? "bg-warn-soft text-warn"
      : code === "Я" || code === "РВ"
        ? "bg-ok-soft text-ok"
        : code === "?"
          ? "bg-warn-soft/60 text-warn"
          : code === "В" || code === "П" || code === "—" || code === "·" || code === ""
            ? "text-ink-3"
            : "bg-violet/15 text-violet-light";

/** «HH:mm» из локального времени организации ("2026-10-21T08:55:00") — без перевода в пояс браузера. */
export const clock = (value: string | null | undefined): string => (value ? value.slice(11, 16) : "—");

/** «21.10» или «21.10.2026» из даты ("2026-10-21"). */
export const dayLabel = (value: string | null | undefined, year = false): string => {
  if (!value) return "—";
  const [y, m, d] = value.slice(0, 10).split("-");
  return year ? `${d}.${m}.${y}` : `${d}.${m}`;
};

const weekdays = ["вс", "пн", "вт", "ср", "чт", "пт", "сб"];
export const weekday = (value: string): string => weekdays[new Date(`${value.slice(0, 10)}T12:00:00Z`).getUTCDay()];

/** Момент (UTC) по Ташкенту: «21.10 10:30». */
export const momentLabel = (value: string | null | undefined): string => {
  if (!value) return "—";
  const parts = new Intl.DateTimeFormat("ru-RU", { timeZone: "Asia/Tashkent", day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" }).format(new Date(value));
  return parts.replace(",", "");
};

type Translate = (text: string, vars?: Record<string, string | number>) => string;
const same: Translate = (text, vars) => (vars ? text.replace(/{(w+)}/g, (all, k: string) => (k in vars ? String(vars[k]) : all)) : text);

/** «8 ч 15 мин»; null — «—». t — перевод подписей (узбекский: soat, daqiqa). */
export const duration = (minutes: number | null | undefined, t: Translate = same): string => {
  if (minutes == null) return "—";
  const h = Math.floor(minutes / 60);
  const m = Math.abs(minutes % 60);
  return h > 0 ? (m > 0 ? t("{h} ч {m} мин", { h, m }) : t("{n} ч", { n: h })) : t("{n} мин", { n: m });
};

export const hours = (minutes: number): string => (minutes / 60).toLocaleString("ru-RU", { maximumFractionDigits: 1 });

export const percentText = (value: number | null): string => (value == null ? "—" : `${value.toLocaleString("ru-RU", { maximumFractionDigits: 1 })}%`);

/** Строка запроса без пустых значений. */
export const qs = (params: Record<string, string | number | null | undefined>): string => {
  const entries = Object.entries(params).filter(([, v]) => v !== null && v !== undefined && v !== "");
  return entries.length === 0 ? "" : `?${new URLSearchParams(entries.map(([k, v]) => [k, String(v)])).toString()}`;
};

/** Сегодня по Ташкенту, "2026-10-21". */
export const todayTashkent = (): string => new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Tashkent" }).format(new Date());

export const addDays = (date: string, days: number): string => {
  const d = new Date(`${date}T12:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
};

/** Значение параметра страницы (searchParams): первое, если пришло несколько. */
export const param = (value: string | string[] | undefined): string | undefined => (Array.isArray(value) ? value[0] : value) || undefined;

/** Дата из параметра — только "yyyy-mm-dd", иначе undefined. */
export const dateParam = (value: string | string[] | undefined): string | undefined => {
  const v = param(value);
  return v && /^\d{4}-\d{2}-\d{2}$/.test(v) ? v : undefined;
};

/** Подразделения для фильтра: дочерние — с отступом под родителем. */
export function divisionOptions(divisions: HrDivision[]): { value: string; label: string }[] {
  const children = new Map<number | null, HrDivision[]>();
  const ids = new Set(divisions.map((d) => d.id));
  for (const d of divisions) {
    const parent = d.parentId != null && ids.has(d.parentId) ? d.parentId : null;
    children.set(parent, [...(children.get(parent) ?? []), d]);
  }
  const result: { value: string; label: string }[] = [];
  const walk = (parent: number | null, depth: number) => {
    for (const d of (children.get(parent) ?? []).sort((a, b) => a.name.localeCompare(b.name, "ru"))) {
      result.push({ value: String(d.id), label: `${"  ".repeat(depth)}${depth > 0 ? "└ " : ""}${d.name}` });
      walk(d.id, depth + 1);
    }
  };
  walk(null, 0);
  return result;
}

/** Подключение Verifix и журнал синхронизации — в «Настройки → Интеграции → HR → Verifix» (право «Интеграции»). */
export const VERIFIX_SETTINGS = "/settings/integrations/hr/verifix";
export const VERIFIX_JOURNAL = "/settings/integrations/hr/verifix/journal";
