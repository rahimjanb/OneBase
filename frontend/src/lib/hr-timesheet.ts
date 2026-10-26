/**
 * «Рабочее время и табели»: модель экрана поверх ответа /api/hr/timesheet (HrTimesheet) — коды дней, имена, периоды, сортировка.
 * Коды дней — те же, что отдаёт HrService.DayCode на сервере.
 */
import type { HrDay, HrTimesheet, HrTimesheetRow } from "@/lib/hr";

/** Данные страницы: ответ API; employeesTotal — только у демо-набора (в API число сотрудников = число строк). */
export type TimesheetData = HrTimesheet & { employeesTotal?: number };

export type DayKind = "present" | "late" | "early" | "absent" | "dayoffWork" | "unknown" | "dayoff" | "holiday" | "document" | "none";

/** Смысл буквы дня. Пусто, «—» (нет графика) и «·» (день ещё не начался) — пустая ячейка; ОТ, Б, К, О, НО — документ. */
export function dayKind(code: string): DayKind {
  switch (code) {
    case "Я":
      return "present";
    case "ОП":
      return "late";
    case "РУ":
      return "early";
    case "ОТС":
      return "absent";
    case "РВ":
      return "dayoffWork";
    case "?":
      return "unknown";
    case "В":
      return "dayoff";
    case "П":
      return "holiday";
    case "":
    case "—":
    case "·":
      return "none";
    default:
      return "document";
  }
}

export const dayKindLabel: Record<DayKind, string> = {
  present: "Явка",
  late: "Опоздание",
  early: "Ранний уход",
  absent: "Отсутствие",
  dayoffWork: "Работа в выходной",
  unknown: "Нет отметки",
  dayoff: "Выходной",
  holiday: "Праздник",
  document: "По документу",
  none: "",
};

/** Нарушения для «Только с нарушениями»: опоздание, ранний уход, отсутствие, нет отметки. */
export const isViolation = (code: string) => code === "ОП" || code === "РУ" || code === "ОТС" || code === "?";

/** Фильтр по ячейке сводки: «Явка», «Опоздания», «Отсутствия». */
export type SummaryFilter = "present" | "late" | "absent";

export function matchesSummary(row: HrTimesheetRow, filter: SummaryFilter): boolean {
  return filter === "present" ? row.summary.presentDays > 0 : filter === "late" ? row.summary.lateDays > 0 : row.summary.absentDays > 0;
}

export type TimesheetSort = "name" | "present" | "late" | "absent" | "leave" | "worked";

export function sortRows(rows: HrTimesheetRow[], sort: TimesheetSort, dir: "asc" | "desc"): HrTimesheetRow[] {
  const value = (r: HrTimesheetRow): number | string =>
    sort === "name"
      ? r.employee
      : sort === "present"
        ? r.summary.presentDays
        : sort === "late"
          ? r.summary.lateDays
          : sort === "absent"
            ? r.summary.absentDays
            : sort === "leave"
              ? r.summary.leaveDays
              : r.summary.workedMinutes;
  const sign = dir === "asc" ? 1 : -1;
  return [...rows].sort((a, b) => {
    const x = value(a);
    const y = value(b);
    const c = typeof x === "string" ? x.localeCompare(String(y), "ru") : x - (y as number);
    return c !== 0 ? c * sign : a.employee.localeCompare(b.employee, "ru");
  });
}

const SUFFIXES = new Set(["o'g'li", "og'li", "o'gli", "ogli", "ugli", "qizi", "kizi", "оглы", "угли", "кызы", "кизи"]);
const APOSTROPHES = /[ʻʼ’‘`´]/g;

/** «FAMILIYEV» → «Familiyev», «TO'XTA» → «To'xta». */
const titleWord = (word: string) => {
  const lower = word.toLocaleLowerCase("ru").replace(APOSTROPHES, "'");
  return lower.charAt(0).toLocaleUpperCase("ru") + lower.slice(1);
};

/**
 * ФИО из Verifix («ФАМИЛИЯ ИМЯ ОТЧЕСТВО O'G'LI», часто заглавными) → «Фамилия Имя» и вторая строка «Отчество o'g'li»; инициалы — для аватара.
 * «O'G'LI», «QIZI» и подобные — строчными.
 */
export function nameParts(full: string): { name: string; patronymic: string | null; initials: string } {
  const words = full.trim().split(/\s+/).filter(Boolean);
  if (words.length === 0) return { name: full, patronymic: null, initials: "?" };
  const main = words.slice(0, 2).map(titleWord);
  const rest = words.slice(2).map((w) => {
    const lower = w.toLocaleLowerCase("ru").replace(APOSTROPHES, "'");
    return SUFFIXES.has(lower) ? lower : titleWord(w);
  });
  const initials = main.map((w) => w.charAt(0)).join("").toLocaleUpperCase("ru");
  return { name: main.join(" "), patronymic: rest.length > 0 ? rest.join(" ") : null, initials: initials || "?" };
}

export type PeriodPreset = "month" | "prevMonth" | "week";

const iso = (d: Date) => d.toISOString().slice(0, 10);
const dateOf = (value: string) => new Date(`${value}T12:00:00Z`);

/** Период пресета от сегодняшней даты организации: «Этот месяц» — с 1-го по сегодня, «Прошлый месяц» — целиком, «Неделя» — 7 дней по сегодня. */
export function presetRange(preset: PeriodPreset, today: string): { from: string; to: string } {
  const t = dateOf(today);
  if (preset === "month") return { from: `${today.slice(0, 8)}01`, to: today };
  if (preset === "week") {
    const from = new Date(t);
    from.setUTCDate(from.getUTCDate() - 6);
    return { from: iso(from), to: today };
  }
  const first = new Date(Date.UTC(t.getUTCFullYear(), t.getUTCMonth() - 1, 1, 12));
  const last = new Date(Date.UTC(t.getUTCFullYear(), t.getUTCMonth(), 0, 12));
  return { from: iso(first), to: iso(last) };
}

export function presetOf(from: string, to: string, today: string): PeriodPreset | null {
  for (const p of ["month", "prevMonth", "week"] as const) {
    const r = presetRange(p, today);
    if (r.from === from && r.to === to) return p;
  }
  return null;
}

/** Предел периода табеля на сервере (HrService.MaxPeriodDays). */
export const MAX_PERIOD_DAYS = 62;

export const daysBetween = (from: string, to: string) => Math.round((dateOf(to).getTime() - dateOf(from).getTime()) / 86400000) + 1;

export const shiftDate = (value: string, days: number) => {
  const d = dateOf(value);
  d.setUTCDate(d.getUTCDate() + days);
  return iso(d);
};

/** «01.10.2026» */
export const fullDate = (value: string) => `${value.slice(8, 10)}.${value.slice(5, 7)}.${value.slice(0, 4)}`;

/**
 * Выходные колонки — по данным: день, где у большинства сотрудников с данными «В» или «П» (или работа в выходной). Если данных нет — воскресенье.
 */
export function weekendColumns(data: HrTimesheet): boolean[] {
  return data.dates.map((date, i) => {
    let off = 0;
    let known = 0;
    for (const row of data.rows) {
      const k = dayKind(row.codes[i] ?? "");
      if (k === "none") continue;
      known++;
      if (k === "dayoff" || k === "holiday" || k === "dayoffWork") off++;
    }
    return known > 0 ? off * 2 >= known : dateOf(date).getUTCDay() === 0;
  });
}

/** Подробности дня для всплывающей карточки: из итога дня (карточка сотрудника /api/hr/employees/{id}). */
export type DayDetail = Pick<HrDay, "firstIn" | "lastOut" | "planBegin" | "planEnd" | "workedMinutes" | "lateMinutes" | "earlyLeaveMinutes" | "leaveName" | "reason" | "events">;

export const toDetail = (day: HrDay): DayDetail => ({
  firstIn: day.firstIn,
  lastOut: day.lastOut,
  planBegin: day.planBegin,
  planEnd: day.planEnd,
  workedMinutes: day.workedMinutes,
  lateMinutes: day.lateMinutes,
  earlyLeaveMinutes: day.earlyLeaveMinutes,
  leaveName: day.leaveName,
  reason: day.reason,
  events: day.events,
});

/** Число с десятичной запятой: 47120.9 → «47 120,9». */
export const decimal = (value: number, digits = 1) =>
  value.toLocaleString("ru-RU", { minimumFractionDigits: 0, maximumFractionDigits: digits });

export const percent = (part: number, whole: number) => (whole > 0 ? (100 * part) / whole : null);
