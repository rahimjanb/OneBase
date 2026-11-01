/**
 * «Мой день» сотрудника (ТЗ employee-dashboard): типы ответов /api/me/* и календаря, форматирование времени, сроков и статусов. Без
 * зависимостей — проверяется node --test (src/lib/my-day.test.mjs). Тексты — ключи перевода (русский) с подстановками {n}; t — функция
 * перевода интерфейса. Время рабочего дня приходит местным (Ташкент) без пояса: «2026-10-31T08:52:00».
 */

import { formatDate } from "../i18n/core.ts";

type T = (text: string, vars?: Record<string, string | number>) => string;

export const TIME_ZONE = "Asia/Tashkent";

// ---------- Рабочий день ----------

export type WorkdayStatus =
  | "not_started"
  | "at_work"
  | "on_break"
  | "out"
  | "finished"
  | "day_off"
  | "on_leave"
  | "absent"
  | "no_schedule"
  | "unclear"
  | "not_employed";

export type WeekStatus = "ok" | "late" | "early_leave" | "absent" | "leave" | "day_off" | "not_started" | "no_schedule" | "unclear" | "not_employed" | "future";

export type MyBreak = { start: string; end: string; minutes: number; fromSchedule: boolean };

export type MyToday = {
  status: WorkdayStatus;
  shiftStart: string | null;
  shiftEnd: string | null;
  arrival: string | null;
  departure: string | null;
  lastMarkAt: string | null;
  workedMinutes: number | null;
  plannedMinutes: number | null;
  lateMinutes: number | null;
  earlyLeaveMinutes: number | null;
  break: MyBreak | null;
  leaveName: string | null;
  reason: string | null;
};

export type MyWeekDay = {
  date: string;
  status: WeekStatus;
  arrival: string | null;
  departure: string | null;
  workedMinutes: number | null;
  lateMinutes: number | null;
  shiftStart: string | null;
  shiftEnd: string | null;
};

export type MyWorkday = {
  available: boolean;
  unavailableReason: "not_connected" | "no_employee_link" | null;
  now: string;
  today: MyToday | null;
  week: MyWeekDay[];
  totals: { workedMinutes: number; lateCount: number; lateMinutes: number } | null;
  syncedAt: string | null;
  tracksThrough: string | null;
};

// ---------- Задачи ----------

export type TaskStatus = "New" | "InProgress" | "Review" | "Done" | "Cancelled";

export type MyTask = {
  id: string;
  title: string;
  status: TaskStatus;
  isBlocked: boolean;
  priority: string;
  dueDate: string | null;
  dueTime: string | null;
  overdue: boolean;
};

export type MyEfficiency = {
  onTimePct: number | null;
  onTime: number;
  closedWithDue: number;
  doneThisWeek: number;
  doneLastWeek: number;
  weeks: { monday: string; number: number; count: number }[];
};

export type MyTasks =
  | {
      available: true;
      total: number;
      new: number;
      inProgress: number;
      waiting: number;
      overdue: number;
      done: number;
      upcoming: MyTask[];
      efficiency: MyEfficiency;
    }
  | { available: false };

// ---------- Календарь ----------

export type CalendarType = "meeting" | "report_due" | "event" | "task_due";

export type CalendarItem = {
  id: string;
  type: CalendarType;
  title: string;
  date: string;
  start: string | null;
  end: string | null;
  allDay: boolean;
  location: string | null;
  participants: number;
  organizer: string | null;
  href: string;
  overdue: boolean;
};

// ---------- Документы и AI ----------

export type DocumentsTab = "recent" | "reports" | "mine" | "assigned";

export type MyDocument = {
  id: string;
  source: "file" | "attachment";
  name: string;
  contentType: string;
  sizeBytes: number;
  updatedAt: string;
  relation: "uploaded" | "report" | "shared" | "assigned";
  by: string | null;
  place: string | null;
  taskId: string | null;
};

export type PlanTone = "danger" | "warning" | "info" | "ok";
export type DailyPlan = {
  status: "ready" | "stale" | "generating" | "unavailable";
  generatedAt: string | null;
  items: { text: string; tone: PlanTone; href: string | null }[];
  refreshAt: string | null;
  message: string | null;
};

// ---------- Время и даты ----------

/** «08:52» из местного времени сервера («2026-10-31T08:52:00») или «15:00:00». */
export function hhmm(value: string | null | undefined): string {
  if (!value) return "";
  const time = value.includes("T") ? value.slice(11, 16) : value.slice(0, 5);
  return time;
}

/** «1 ч 32 м» — кольцо и ячейки недели; ноль часов — «32 м». */
export function hm(minutes: number, t: T): string {
  const h = Math.floor(minutes / 60);
  const m = Math.round(minutes % 60);
  return h > 0 ? t("{h} ч {m} м", { h, m }) : t("{m} м", { m });
}

/** «47 ч 2 мин» — итог недели. */
export function hmLong(minutes: number, t: T): string {
  const h = Math.floor(minutes / 60);
  const m = Math.round(minutes % 60);
  if (h === 0) return t("{m} мин", { m });
  return m === 0 ? t("{h} ч", { h }) : t("{h} ч {m} мин", { h, m });
}

/** «1:32» — кольцо и неделя на телефоне. */
export function clock(minutes: number): string {
  const h = Math.floor(minutes / 60);
  const m = Math.round(minutes % 60);
  return `${h}:${String(m).padStart(2, "0")}`;
}

/** Сегодня по Ташкенту: «2026-10-31». */
export function todayIn(now: Date): string {
  return new Intl.DateTimeFormat("en-CA", { timeZone: TIME_ZONE, year: "numeric", month: "2-digit", day: "2-digit" }).format(now);
}

/** Разница в днях между датами «YYYY-MM-DD» (b − a). */
export function daysBetween(a: string, b: string): number {
  return Math.round((Date.UTC(+b.slice(0, 4), +b.slice(5, 7) - 1, +b.slice(8, 10)) - Date.UTC(+a.slice(0, 4), +a.slice(5, 7) - 1, +a.slice(8, 10))) / 86_400_000);
}

const asDate = (iso: string) => new Date(Date.UTC(+iso.slice(0, 4), +iso.slice(5, 7) - 1, +iso.slice(8, 10), 12));

/** «пн, 2 ноября» на языке интерфейса. */
export function dayLabel(iso: string, locale: string): string {
  const d = asDate(iso);
  const weekday = formatDate(d, locale, { weekday: "short", timeZone: "UTC" }).replace(".", "");
  const day = formatDate(d, locale, { day: "numeric", month: "long", timeZone: "UTC" });
  return `${weekday}, ${day}`;
}

/** «Пн 26» — ячейка недели. */
export function weekdayCell(iso: string, locale: string): { weekday: string; day: number } {
  const weekday = formatDate(asDate(iso), locale, { weekday: "short", timeZone: "UTC" }).replace(".", "");
  return { weekday: weekday.charAt(0).toUpperCase() + weekday.slice(1, 2), day: +iso.slice(8, 10) };
}

/** «сб · 31 октября» — строка даты «Моего дня» на телефоне (заглавными — стилем). */
export function shortDayLabel(date: Date, locale: string): string {
  const weekday = formatDate(date, locale, { weekday: "short", timeZone: TIME_ZONE }).replace(".", "");
  const day = formatDate(date, locale, { day: "numeric", month: "long", timeZone: TIME_ZONE });
  return `${weekday} · ${day}`;
}

/** «30 окт» — дата документа и уведомления. */
export function shortDate(iso: string, locale: string): string {
  return formatDate(new Date(iso), locale, { day: "numeric", month: "short", timeZone: TIME_ZONE }).replace(".", "");
}

// ---------- Статусы ----------

export type Tone = "success" | "warning" | "danger" | "accent" | "violet" | "muted";

/** Пилюля статуса дня в шапке: «На работе · пришли вовремя», «Опоздание 12 мин», «Рабочий день не начат», «Выходной». */
export function dayPill(today: MyToday | null, t: T): { label: string; tone: Tone } | null {
  if (!today) return null;
  const late = today.lateMinutes ?? 0;
  switch (today.status) {
    case "at_work":
      return late > 0 ? { label: t("Опоздание {n} мин", { n: late }), tone: "warning" } : { label: t("На работе · пришли вовремя"), tone: "success" };
    case "on_break":
      return { label: t("На перерыве"), tone: "accent" };
    case "out":
      return { label: t("Вышли в {time}", { time: hhmm(today.lastMarkAt) }), tone: "muted" };
    case "finished":
      return { label: t("Рабочий день завершён"), tone: "success" };
    case "not_started":
      return { label: t("Рабочий день не начат"), tone: "muted" };
    case "day_off":
      return { label: t("Выходной"), tone: "muted" };
    case "on_leave":
      return { label: today.leaveName ?? t("Отсутствие по документу"), tone: "accent" };
    case "absent":
      return { label: t("Нет прихода"), tone: "danger" };
    default:
      return null;
  }
}

/** Ячейка недели: цвет точки и подпись (статус — не только цветом). */
export function weekTone(status: WeekStatus): { tone: Tone; label: string } {
  switch (status) {
    case "ok":
      return { tone: "success", label: "вовремя" };
    case "late":
      return { tone: "warning", label: "опоздание" };
    case "early_leave":
      return { tone: "warning", label: "ранний уход" };
    case "absent":
      return { tone: "danger", label: "нет прихода" };
    case "leave":
      return { tone: "accent", label: "по документу" };
    case "day_off":
      return { tone: "muted", label: "выходной" };
    case "not_started":
      return { tone: "muted", label: "не начат" };
    case "future":
      return { tone: "muted", label: "впереди" };
    default:
      return { tone: "muted", label: "нет данных" };
  }
}

/** Статус задачи: в процессе — warning, ожидает (на проверке, заблокирована) — accent, новая — accent, просрочена — danger. */
export function taskStatus(task: Pick<MyTask, "status" | "isBlocked">): { label: string; tone: Tone } {
  if (task.isBlocked && task.status !== "Done" && task.status !== "Cancelled") return { label: "Заблокирована", tone: "accent" };
  switch (task.status) {
    case "InProgress":
      return { label: "В процессе", tone: "warning" };
    case "Review":
      return { label: "На проверке", tone: "accent" };
    case "Done":
      return { label: "Выполнена", tone: "success" };
    case "Cancelled":
      return { label: "Отменена", tone: "muted" };
    default:
      return { label: "Новая", tone: "accent" };
  }
}

/**
 * Пилюля срока: просрочен — danger («просрочено на 1 дн.»), сегодня и завтра — warning («сегодня до 15:00»), позже — нейтральная
 * («пн, 2 ноября»).
 */
export function duePill(task: Pick<MyTask, "dueDate" | "dueTime" | "overdue">, today: string, locale: string, t: T): { label: string; tone: "danger" | "warning" | "neutral" } | null {
  if (!task.dueDate) return null;
  const time = task.dueTime ? hhmm(task.dueTime) : null;
  const days = daysBetween(today, task.dueDate);
  if (task.overdue) {
    return days < 0
      ? { label: t("просрочено на {n} дн.", { n: -days }), tone: "danger" }
      : { label: time ? t("просрочено · было до {time}", { time }) : t("просрочено"), tone: "danger" };
  }
  if (days === 0) return { label: time ? t("сегодня до {time}", { time }) : t("сегодня"), tone: "warning" };
  if (days === 1) return { label: time ? t("завтра до {time}", { time }) : t("завтра"), tone: "warning" };
  const day = dayLabel(task.dueDate, locale);
  return { label: time ? `${day}, ${time}` : day, tone: "neutral" };
}

// ---------- Календарь ----------

/** Вид строки календаря: подпись и цвет полосы (встреча — accent, отчёт — warning, задача — violet, событие — pink). */
export const CALENDAR_TYPES: Record<CalendarType, { label: string; bar: string }> = {
  meeting: { label: "Встреча", bar: "accent" },
  report_due: { label: "Отчёт", bar: "warning" },
  event: { label: "Важное событие", bar: "pink" },
  task_due: { label: "Задача", bar: "violet" },
};

/** Группы по дням: «СЕГОДНЯ», «ЗАВТРА», «ПН, 2 НОЯБРЯ» (заглавными — стилем). */
export function calendarGroups(items: CalendarItem[], today: string, locale: string, t: T): { date: string; label: string; items: CalendarItem[] }[] {
  const groups = new Map<string, CalendarItem[]>();
  for (const item of items) groups.set(item.date, [...(groups.get(item.date) ?? []), item]);
  return [...groups.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([date, list]) => {
      const days = daysBetween(today, date);
      const label = days === 0 ? t("Сегодня") : days === 1 ? t("Завтра") : dayLabel(date, locale);
      return { date, label, items: list };
    });
}

/** Подпись под названием: «Встреча · 30 мин», «Встреча · Бахтиёр Н.», «Отчёт», «Задача · просрочена». */
export function calendarSubtitle(item: CalendarItem, t: T): string {
  const kind = t(CALENDAR_TYPES[item.type].label);
  if (item.type === "task_due") return item.overdue ? `${kind} · ${t("просрочена")}` : kind;
  if (item.type === "meeting" && item.start && item.end) {
    const [sh, sm] = item.start.split(":").map(Number);
    const [eh, em] = item.end.split(":").map(Number);
    const minutes = eh * 60 + em - (sh * 60 + sm);
    if (minutes > 0) return `${kind} · ${minutes >= 60 && minutes % 60 === 0 ? t("{h} ч", { h: minutes / 60 }) : minutes > 60 ? hmLong(minutes, t) : t("{m} мин", { m: minutes })}`;
  }
  return item.location ? `${kind} · ${item.location}` : kind;
}

// ---------- Документы ----------

export type FileKind = "sheet" | "pdf" | "doc" | "image" | "slides" | "archive" | "other";

export function fileKind(name: string, contentType = ""): FileKind {
  const ext = name.includes(".") ? name.slice(name.lastIndexOf(".") + 1).toLowerCase() : "";
  if (["xlsx", "xls", "csv", "ods"].includes(ext) || contentType.includes("spreadsheet")) return "sheet";
  if (ext === "pdf" || contentType === "application/pdf") return "pdf";
  if (["doc", "docx", "odt", "rtf", "txt"].includes(ext)) return "doc";
  if (["ppt", "pptx", "odp"].includes(ext)) return "slides";
  if (["png", "jpg", "jpeg", "gif", "webp", "heic", "svg"].includes(ext) || contentType.startsWith("image/")) return "image";
  if (["zip", "rar", "7z"].includes(ext)) return "archive";
  return "other";
}

/** «назначен вам · 30 окт», «отчёт · 28 окт», «загружен вами · 27 окт», «Продажи · 30 окт». */
export function documentNote(doc: MyDocument, locale: string, t: T): string {
  const date = shortDate(doc.updatedAt, locale);
  const what = doc.relation === "assigned" ? t("назначен вам") : doc.relation === "report" ? t("отчёт") : doc.relation === "uploaded" ? t("загружен вами") : doc.place ?? t("файл");
  return `${what} · ${date}`;
}

// ---------- Эффективность ----------

/** «+2 к прошлой неделе», «−1 к прошлой неделе», «как на прошлой неделе». */
export function weekDelta(current: number, previous: number, t: T): { label: string; tone: Tone } {
  const diff = current - previous;
  if (diff === 0) return { label: t("как на прошлой неделе"), tone: "muted" };
  return diff > 0 ? { label: t("+{n} к прошлой неделе", { n: diff }), tone: "success" } : { label: t("−{n} к прошлой неделе", { n: -diff }), tone: "warning" };
}

/** Высота столбика в процентах: от 8 % (ноль — тонкая линия) до 100 % у максимума. */
export function barHeights(counts: number[]): number[] {
  const max = Math.max(...counts, 0);
  return counts.map((c) => (max === 0 ? 8 : Math.max(8, Math.round((c / max) * 100))));
}

/** Доля отработанного от плана для кольца, 0–1. */
export function ringShare(worked: number | null, planned: number | null): number {
  if (!worked || !planned || planned <= 0) return 0;
  return Math.min(1, worked / planned);
}
