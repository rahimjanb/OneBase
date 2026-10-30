import { formatDate, type T } from "@/i18n/core";
import { plural } from "@/lib/format";
import { addDays, type HrReportGroup, type HrSummary, type HrSyncBrief } from "@/lib/hr";

/**
 * «Отчёт по посещаемости» (HR → Отчёты): пороги цвета, форматы, периоды, топы и итоги. Формулы явки, опозданий и отсутствий считает
 * backend (HrService); здесь — только представление и производные значения (топ-3 подразделений, средневзвешенный итог, среднее опоздание).
 */

/** Пороги цвета — одни на всю страницу (десктоп и телефон). Поменять правило — здесь. */
export const REPORT_RULES = {
  /** Явка: от good — зелёная, от warn — жёлтая, ниже — красная (и строка таблицы подсвечена). */
  attendance: { good: 85, warn: 70 },
  /** «Без опозданий» ниже этого процента — красным. */
  onTimeLow: 15,
  /** Опозданий у подразделения от стольких — жёлтым и жирнее. */
  lateMany: 150,
  /** Отсутствий у подразделения от стольких — красным и жирнее. */
  absentMany: 50,
  /** Общее время опозданий сотрудника от стольких часов — красным. */
  lateHoursHigh: 40,
  /** Среднее опоздание от стольких минут — красным. */
  lateAvgHigh: 120,
  /** Среднее опоздание больше стольких минут — похоже на чужой график или вторую смену: значок «Проверьте график сотрудника». */
  lateAvgSuspicious: 240,
} as const;

export type Tone = "success" | "warning" | "danger" | "violet" | "accent" | "text" | "text2" | "subtle";

export const toneText: Record<Tone, string> = {
  success: "text-(--ts-success)",
  warning: "text-(--ts-warning)",
  danger: "text-(--ts-danger)",
  violet: "text-(--ts-violet)",
  accent: "text-(--ts-accent)",
  text: "text-(--ts-text)",
  text2: "text-(--ts-text-2)",
  subtle: "text-(--ts-subtle)",
};

export const toneBar: Record<Tone, string> = {
  success: "bg-(--ts-success)",
  warning: "bg-(--ts-warning)",
  danger: "bg-(--ts-danger)",
  violet: "bg-(--ts-violet)",
  accent: "bg-(--ts-accent)",
  text: "bg-(--ts-text)",
  text2: "bg-(--ts-text-2)",
  subtle: "bg-(--ts-subtle)",
};

/** Явка: ≥ 85 — success, 70–84,9 — warning, < 70 — danger; нет данных — subtle. */
export const attendanceTone = (pct: number | null): Tone =>
  pct == null ? "subtle" : pct >= REPORT_RULES.attendance.good ? "success" : pct >= REPORT_RULES.attendance.warn ? "warning" : "danger";

export const lowAttendance = (pct: number | null) => pct != null && pct < REPORT_RULES.attendance.warn;

export const onTimeTone = (pct: number | null): Tone => (pct == null ? "subtle" : pct < REPORT_RULES.onTimeLow ? "danger" : "text2");

/** Количество: ноль — subtle; от порога — свой цвет (и жирнее, см. strongTone); иначе — text-2. */
export const lateTone = (n: number): Tone => (n === 0 ? "subtle" : n >= REPORT_RULES.lateMany ? "warning" : "text2");
export const absentTone = (n: number): Tone => (n === 0 ? "subtle" : n >= REPORT_RULES.absentMany ? "danger" : "text2");
export const docsTone = (n: number): Tone => (n > 0 ? "violet" : "subtle");
export const countTone = (n: number): Tone => (n === 0 ? "subtle" : "text2");
/** Цветное значение по правилу — полужирное (600). */
export const strongTone = (tone: Tone) => tone === "warning" || tone === "danger" || tone === "violet";

// ---------- Форматы ----------

const intFormat = new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 0 });
const oneFormat = new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 1, maximumFractionDigits: 1 });
const upToOne = new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 1 });

/** «1 150» */
export const num = (n: number) => intFormat.format(n);
/** «82,6%», «71%»; нет данных — «—». */
export const pct = (v: number | null) => (v == null ? "—" : `${upToOne.format(v)}%`);
/** Часы с одной цифрой после запятой: «2 580,5», «65,0». */
export const hours1 = (minutes: number) => oneFormat.format(minutes / 60);
/** Целые часы: «55 343». */
export const hoursInt = (minutes: number) => intFormat.format(Math.round(minutes / 60));

/** Длительность: «6 106 ч 11 мин», «53 ч 9 мин», «26 ч», «17 мин» — без «0 мин» и «0 ч». */
export function durationText(minutes: number, t: T): string {
  const total = Math.max(0, Math.round(minutes));
  const h = Math.floor(total / 60);
  const m = total % 60;
  if (h === 0) return t("{m} мин", { m });
  return m === 0 ? t("{h} ч", { h: num(h) }) : t("{h} ч {m} мин", { h: num(h), m });
}

/** «26 раз», «24 раза», «1 раз». */
export const timesText = (n: number, t: T) => t(plural(n, ["{n} раз", "{n} раза", "{n} раз"]), { n: num(n) });
/** «281 сотрудник», «4 сотрудника». */
export const employeesText = (n: number, t: T) => t(plural(n, ["{n} сотрудник", "{n} сотрудника", "{n} сотрудников"]), { n: num(n) });
/** «30 подразделений», «1 подразделение». */
export const divisionsText = (n: number, t: T) => t(plural(n, ["{n} подразделение", "{n} подразделения", "{n} подразделений"]), { n: num(n) });
/** «в 30 подразделениях», «в 1 подразделении». */
export const inDivisionsText = (n: number, t: T) => t(n % 10 === 1 && n % 100 !== 11 ? "в {n} подразделении" : "в {n} подразделениях", { n: num(n) });
/** «30 отделов» (коротко, телефон). */
export const departmentsText = (n: number, t: T) => t(plural(n, ["{n} отдел", "{n} отдела", "{n} отделов"]), { n: num(n) });
/** «+1 день по документам». */
export const docDaysText = (n: number, t: T) => t(plural(n, ["+{n} день по документам", "+{n} дня по документам", "+{n} дней по документам"]), { n: num(n) });

/** Среднее опоздание сотрудника, минуты (до целой). */
export const lateAverage = (s: HrSummary) => (s.lateDays > 0 ? Math.round(s.lateMinutes / s.lateDays) : 0);
export const suspiciousLate = (s: HrSummary) => s.lateDays > 0 && s.lateMinutes / s.lateDays > REPORT_RULES.lateAvgSuspicious;
/**
 * Не приходил весь период (как HrService.AbsentWholePeriod): работал в компании все дни периода и ни в один свой рабочий день не пришёл —
 * по буквам табеля только «ОТС», выходные, «нет графика» и не начавшийся день. Принят или уволен внутри периода («» в днях) — не в счёт.
 */
const WHOLE_OK = new Set(["ОТС", "В", "П", "·", "—"]);
export const absentWhole = (row: { codes: string[] }) => row.codes.includes("ОТС") && row.codes.every((c) => WHOLE_OK.has(c));

// ---------- Итоги ----------

const ZERO: HrSummary = {
  planDays: 0,
  presentDays: 0,
  lateDays: 0,
  lateMinutes: 0,
  earlyDays: 0,
  earlyMinutes: 0,
  absentDays: 0,
  leaveDays: 0,
  unknownDays: 0,
  noExitDays: 0,
  workedMinutes: 0,
  planMinutes: 0,
};

export function sumSummaries(list: HrSummary[]): HrSummary {
  const total = { ...ZERO };
  for (const s of list) for (const key of Object.keys(total) as (keyof HrSummary)[]) total[key] += s[key];
  return total;
}

/** Явка как на сервере: дни с приходом среди дней с ясным итогом (пришёл или отсутствовал). Средневзвешенная — по суммам дней. */
export const attendanceOf = (s: HrSummary) => (s.presentDays + s.absentDays > 0 ? Math.round((1000 * s.presentDays) / (s.presentDays + s.absentDays)) / 10 : null);
/** Доля приходов без опоздания. */
export const onTimeOf = (s: HrSummary) => (s.presentDays > 0 ? Math.round((1000 * (s.presentDays - s.lateDays)) / s.presentDays) / 10 : null);
/** Отработано от графика, %. */
export const workedShare = (s: HrSummary) => (s.planMinutes > 0 ? (100 * s.workedMinutes) / s.planMinutes : null);

/** Итог по строкам подразделений: суммы и средневзвешенные проценты (не среднее процентов). */
export function totalOf(groups: HrReportGroup[]) {
  const summary = sumSummaries(groups.map((g) => g.summary));
  return { employees: groups.reduce((n, g) => n + g.employees, 0), summary, attendanceRate: attendanceOf(summary), punctuality: onTimeOf(summary) };
}

// ---------- Топ-3 подразделений ----------

export type TopKind = "attendance" | "late" | "absent";
export type TopRow = { group: HrReportGroup; value: number };

/**
 * Топ-3: с минимальной явкой, больше всего опозданий и отсутствий. При равенстве — выше больший отдел (отдел из одного человека с той же
 * явкой — ниже). Без нарушений — пусто (карточка «Нарушений нет»).
 */
export function topDivisions(groups: HrReportGroup[], kind: TopKind): TopRow[] {
  const rows =
    kind === "attendance"
      ? groups.filter((g) => g.attendanceRate != null).map((group) => ({ group, value: group.attendanceRate! }))
      : groups.map((group) => ({ group, value: kind === "late" ? group.summary.lateDays : group.summary.absentDays })).filter((r) => r.value > 0);
  return rows.sort((a, b) => (kind === "attendance" ? a.value - b.value : b.value - a.value) || b.group.employees - a.group.employees).slice(0, 3);
}

// ---------- Сортировка таблицы ----------

export type SortKey = "name" | "employees" | "days" | "attendance" | "onTime" | "late" | "early" | "absent" | "docs" | "worked";

const sortValue: Record<Exclude<SortKey, "name">, (g: HrReportGroup) => number> = {
  employees: (g) => g.employees,
  days: (g) => g.summary.planDays,
  attendance: (g) => g.attendanceRate ?? -1,
  onTime: (g) => g.punctuality ?? -1,
  late: (g) => g.summary.lateDays,
  early: (g) => g.summary.earlyDays,
  absent: (g) => g.summary.absentDays,
  docs: (g) => g.summary.leaveDays,
  worked: (g) => g.summary.workedMinutes,
};

/** Названия: цифры, латиница, затем кириллица («3-этаж», «HR», «АХО»…) — как в Verifix; регистр не важен. */
const names = new Intl.Collator("en", { numeric: true, sensitivity: "base" });

export function sortGroups(groups: HrReportGroup[], key: SortKey, dir: "asc" | "desc"): HrReportGroup[] {
  const byName = (a: HrReportGroup, b: HrReportGroup) => names.compare(a.division, b.division);
  const sign = dir === "asc" ? 1 : -1;
  if (key === "name") return [...groups].sort((a, b) => sign * byName(a, b));
  const value = sortValue[key];
  return [...groups].sort((a, b) => sign * (value(a) - value(b)) || byName(a, b));
}

/** Поиск подразделения: без регистра и ё/е. */
export const matchesName = (name: string, query: string) => {
  const norm = (s: string) => s.toLocaleLowerCase("ru").replace(/ё/g, "е").trim();
  return !query.trim() || norm(name).includes(norm(query));
};

// ---------- Период ----------

export type PeriodKind = "week" | "month" | "quarter" | "custom";

/** Предел периода отчёта на сервере (HrService.MaxReportDays) — квартал. */
export const MAX_REPORT_DAYS = 92;

const monthStart = (d: string) => `${d.slice(0, 8)}01`;
const monthEnd = (d: string) => addDays(`${addDays(monthStart(d), 32).slice(0, 8)}01`, -1);
/** Первый месяц квартала даты: 1, 4, 7, 10. */
const quarterMonth = (d: string) => Math.floor((Number(d.slice(5, 7)) - 1) / 3) * 3 + 1;
const quarterStart = (d: string) => `${d.slice(0, 5)}${String(quarterMonth(d)).padStart(2, "0")}-01`;
const quarterEnd = (d: string) => monthEnd(`${d.slice(0, 5)}${String(quarterMonth(d) + 2).padStart(2, "0")}-01`);
const min = (a: string, b: string) => (a < b ? a : b);
const days = (from: string, to: string) => Math.round((Date.parse(`${to}T12:00:00Z`) - Date.parse(`${from}T12:00:00Z`)) / 86_400_000) + 1;

/** Период сегмента вокруг даты anchor (конец текущего периода): неделя — 7 дней по anchor, месяц и квартал — календарные, не дальше сегодня. */
export function periodRange(kind: Exclude<PeriodKind, "custom">, anchor: string, today: string): { from: string; to: string } {
  if (kind === "week") return { from: addDays(anchor, -6), to: anchor };
  if (kind === "month") return { from: monthStart(anchor), to: min(monthEnd(anchor), today) };
  return { from: quarterStart(anchor), to: min(quarterEnd(anchor), today) };
}

/**
 * Какой сегмент выбран. hint — выбор из адреса (?period=): нужен, когда квартал и месяц совпадают (первый месяц квартала). Без подсказки —
 * месяц, квартал, неделя; иначе — «Свой период».
 */
export function periodOf(from: string, to: string, today: string, hint?: string | null): PeriodKind {
  const fits = (kind: Exclude<PeriodKind, "custom">) => {
    if (kind === "week") return days(from, to) === 7;
    const start = kind === "month" ? monthStart(from) : quarterStart(from);
    const end = kind === "month" ? monthEnd(from) : quarterEnd(from);
    return from === start && (to === end || (to === today && to < end && to >= from));
  };
  if ((hint === "week" || hint === "month" || hint === "quarter") && fits(hint)) return hint;
  return (["month", "quarter", "week"] as const).find(fits) ?? "custom";
}

/** «1 – 30 октября 2026», «28 сентября – 4 октября 2026», «28 декабря 2025 – 3 января 2026». */
export function rangeLabel(from: string, to: string, locale: string): string {
  // formatDate, не Intl напрямую: узбекские названия месяцев у сервера и браузера иначе разные (ошибка гидратации).
  const f = (d: string, o: Intl.DateTimeFormatOptions) => formatDate(new Date(`${d}T12:00:00Z`), locale, { ...o, timeZone: "UTC" });
  const year = to.slice(0, 4);
  if (from === to) return `${f(to, { day: "numeric", month: "long" })} ${year}`;
  if (from.slice(0, 7) === to.slice(0, 7)) return `${Number(from.slice(8, 10))} – ${f(to, { day: "numeric", month: "long" })} ${year}`;
  if (from.slice(0, 4) === year) return `${f(from, { day: "numeric", month: "long" })} – ${f(to, { day: "numeric", month: "long" })} ${year}`;
  return `${f(from, { day: "numeric", month: "long" })} ${from.slice(0, 4)} – ${f(to, { day: "numeric", month: "long" })} ${year}`;
}

/** Коротко для телефона: «1–30 окт 2026», «28 сен – 4 окт 2026». */
export function rangeShort(from: string, to: string, locale: string): string {
  const mon = (d: string) => formatDate(new Date(`${d}T12:00:00Z`), locale, { month: "short", timeZone: "UTC" }).replace(".", "");
  const day = (d: string) => Number(d.slice(8, 10));
  const year = to.slice(0, 4);
  if (from.slice(0, 7) === to.slice(0, 7)) return from === to ? `${day(to)} ${mon(to)} ${year}` : `${day(from)}–${day(to)} ${mon(to)} ${year}`;
  if (from.slice(0, 4) === year) return `${day(from)} ${mon(from)} – ${day(to)} ${mon(to)} ${year}`;
  return `${day(from)} ${mon(from)} ${from.slice(0, 4)} – ${day(to)} ${mon(to)} ${year}`;
}

/** Табель отдела за тот же период: «Табели» показывают не больше 62 дней — длиннее период берём по его концу. */
export function timesheetRange(from: string, to: string): { from: string; to: string } {
  return days(from, to) > 62 ? { from: addDays(to, -61), to } : { from, to };
}

// ---------- Фильтры в адресе ----------

/** Подразделения фильтра: ?dept=1,2 — id через запятую. */
export const parseDepts = (value: string | null | undefined): number[] =>
  [...new Set((value ?? "").split(",").map((v) => Number(v.trim())).filter((n) => Number.isInteger(n) && n > 0))];

// ---------- Свежесть данных ----------

const tashkent = (iso: string) => {
  const parts = new Intl.DateTimeFormat("ru-RU", { timeZone: "Asia/Tashkent", day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" }).formatToParts(new Date(iso));
  const get = (type: string) => parts.find((p) => p.type === type)?.value ?? "";
  return `${get("day")}.${get("month")} ${get("hour")}:${get("minute")}`;
};
/** Локальное время организации без пояса ("2026-10-30T17:23:00") → «30.10 17:23». */
const local = (value: string) => `${value.slice(8, 10)}.${value.slice(5, 7)} ${value.slice(11, 16)}`;
const dm = (value: string) => `${value.slice(8, 10)}.${value.slice(5, 7)}`;

export type ReportSync = {
  tone: "success" | "warning" | "danger" | "accent";
  text: string;
  vars?: Record<string, string>;
  details: { text: string; vars: Record<string, string> }[];
};

/**
 * Чип Verifix: «Verifix · отметки до 30.10 17:23» и подробности для подсказки (синхронизация, отметки, табель). Ошибка последней
 * синхронизации — красный «Данные могут быть неактуальны · обновлено …»; не подключён, выключен, давно не обновлялся — жёлтый.
 */
export function reportSync(sync: HrSyncBrief, now: number): ReportSync {
  const details: ReportSync["details"] = [];
  if (sync.lastSuccessAt) details.push({ text: "синхронизация {at}", vars: { at: tashkent(sync.lastSuccessAt) } });
  if (sync.tracksThrough) details.push({ text: "отметки загружены до {at}", vars: { at: local(sync.tracksThrough) } });
  if (sync.timesheetFrom && sync.timesheetTo) details.push({ text: "табель с {from} по {to}", vars: { from: dm(sync.timesheetFrom), to: dm(sync.timesheetTo) } });

  if (!sync.configured) return { tone: "warning", text: "Verifix не подключён", details };
  if (!sync.enabled) return { tone: "warning", text: "Verifix · интеграция выключена", details };
  if (sync.lastStatus === "Failed") {
    const at = sync.lastSuccessAt ? tashkent(sync.lastSuccessAt) : null;
    return { tone: "danger", text: at ? "Данные могут быть неактуальны · обновлено {at}" : "Данные могут быть неактуальны", vars: at ? { at } : undefined, details };
  }
  if (sync.running) return { tone: "accent", text: "Verifix · идёт синхронизация", details };
  if (!sync.lastSuccessAt) return { tone: "warning", text: "Verifix · синхронизации ещё не было", details };
  const stale = now - Date.parse(sync.lastSuccessAt) > 3 * 60 * 60 * 1000 || sync.lastStatus === "Partial";
  const through = sync.tracksThrough ? local(sync.tracksThrough) : tashkent(sync.lastSuccessAt);
  return { tone: stale ? "warning" : "success", text: "Verifix · отметки до {at}", vars: { at: through }, details };
}
