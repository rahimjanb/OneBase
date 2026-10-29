/**
 * «Обзор HR»: итоги дня, цвета статусов, подписи динамики и форматирование — общее для всех блоков страницы.
 * Тексты — русские ключи перевода (t(...) в компонентах); числа — ru-RU («1 284», «78,6%»), время — «HH:mm» по часовому поясу компании.
 */
import type { T } from "@/i18n/core";
import { plural } from "@/lib/format";
import { addDays, weekday, type HrAttention, type HrOverview, type HrSyncBrief } from "@/lib/hr";

export type Tone = "success" | "warning" | "danger" | "violet" | "accent" | "subtle";

/** Классы цвета статуса — одинаковые в полосе, легенде, пилюлях и прогресс-барах. */
export const toneDot: Record<Tone, string> = {
  success: "bg-(--ts-success)",
  warning: "bg-(--ts-warning)",
  danger: "bg-(--ts-danger)",
  violet: "bg-(--ts-violet)",
  accent: "bg-(--ts-accent)",
  subtle: "bg-(--ts-subtle)",
};

export const toneText: Record<Tone, string> = {
  success: "text-(--ts-success)",
  warning: "text-(--ts-warning)",
  danger: "text-(--ts-danger)",
  violet: "text-(--ts-violet)",
  accent: "text-(--ts-accent)",
  subtle: "text-(--ts-muted)",
};

export const toneSoft: Record<Tone, string> = {
  success: "bg-(--ts-success-soft)",
  warning: "bg-(--ts-warning-soft)",
  danger: "bg-(--ts-danger-soft)",
  violet: "bg-(--ts-violet-soft)",
  accent: "bg-(--ts-accent-soft)",
  subtle: "bg-(--ts-surface-2)",
};

export type StatusKey = "onTime" | "late" | "absent" | "byDocument" | "expected" | "unclear";

/** Шесть итогов дня в порядке полосы и легенды; filter — статус в «Посещаемости». */
export const STATUSES: { key: StatusKey; label: string; short: string; tone: Tone; filter: string }[] = [
  { key: "onTime", label: "Пришли вовремя", short: "Вовремя", tone: "success", filter: "ontime" },
  { key: "late", label: "Опоздали", short: "Опоздали", tone: "warning", filter: "late" },
  { key: "absent", label: "Отсутствуют", short: "Отсутствуют", tone: "danger", filter: "absent" },
  { key: "byDocument", label: "По документу", short: "По документу", tone: "violet", filter: "leave" },
  { key: "expected", label: "Ожидаются", short: "Ожидаются", tone: "accent", filter: "expected" },
  { key: "unclear", label: "Не ясно", short: "Не ясно", tone: "subtle", filter: "unknown" },
];

export type DayCounts = Record<StatusKey, number>;

/** Итоги дня: вовремя — пришедшие без опоздания (present включает опоздавших). */
export function dayCounts(o: HrOverview): DayCounts {
  return {
    onTime: o.present - o.late,
    late: o.late,
    absent: o.absent,
    byDocument: o.onLeave,
    expected: o.expected,
    unclear: o.unknown,
  };
}

export const sumCounts = (c: DayCounts) => STATUSES.reduce((sum, s) => sum + c[s.key], 0);

const numberFormat = new Intl.NumberFormat("ru-RU");
export const num = (n: number) => numberFormat.format(n);

/** «78,6%»; digits — знаков после запятой. */
export const pct = (value: number, digits = 1) =>
  `${value.toLocaleString("ru-RU", { minimumFractionDigits: digits, maximumFractionDigits: digits })}%`;

/** Доля в процентах или null, если делить не на что. */
export const share = (part: number, total: number): number | null => (total > 0 ? (100 * part) / total : null);

/** Цвет явки подразделения: от 80% — зелёный, 70–79% — жёлтый, ниже — красный. */
export const attendanceTone = (value: number): Tone => (value >= 80 ? "success" : value >= 70 ? "warning" : "danger");

/** Минуты от полуночи из «HH:mm[:ss]». */
export const minutesOf = (time: string) => Number(time.slice(0, 2)) * 60 + Number(time.slice(3, 5));

/** «HH:mm» из «HH:mm:ss» или локальной даты-времени «2026-10-30T08:56:00». */
export const hhmm = (value: string | null | undefined) => (!value ? "" : value.length > 8 ? value.slice(11, 16) : value.slice(0, 5));

/** Понедельник недели даты «yyyy-mm-dd». */
export const mondayOf = (date: string) => addDays(date, -((new Date(`${date}T12:00:00Z`).getUTCDay() + 6) % 7));

const MINUS = "−";
const signed = (n: number) => (n > 0 ? `+${num(n)}` : n < 0 ? `${MINUS}${num(-n)}` : "0");

/** Строка динамики под значением KPI: текст и цвет (null — скрыть строку). */
export type Delta = { text: string; tone: "success" | "danger" | "muted" } | null;

/** С каким днём сравниваем: «вчера» или «в пт» (после выходных — последний рабочий день). */
export function compareWhen(o: HrOverview, t: T): string | null {
  if (!o.compare) return null;
  return o.compare.date === addDays(o.date, -1) ? t("вчера") : t("в {day}", { day: t(weekday(o.compare.date)) });
}

/** Подпись под «на месте»: «вчера в это время — 74,2%» (сегодня) или «вчера — 74,2%» (прошлый день). */
export function compareLine(o: HrOverview, present: number, t: T, short = false): Delta {
  const when = compareWhen(o, t);
  const now = share(present, o.scheduled);
  if (!o.compare || !when || now == null) return null;
  const text =
    o.isToday && !short ? t("{when} в это время — {pct}", { when, pct: pct(o.compare.presentPct) }) : t("{when} — {pct}", { when, pct: pct(o.compare.presentPct) });
  return { text, tone: now >= o.compare.presentPct ? "success" : "danger" };
}

/** Средний приход к дню сравнения: «на 4 мин раньше, чем вчера» / коротко «−4 мин к вчера». */
export function arrivalDelta(o: HrOverview, t: T, short = false): Delta {
  const when = compareWhen(o, t);
  if (!o.avgArrival || !o.compare?.avgArrival || !when) return null;
  const diff = minutesOf(o.avgArrival) - minutesOf(o.compare.avgArrival);
  if (diff === 0) return { text: t("как {when}", { when }), tone: "muted" };
  if (short) return { text: t("{n} мин к {when}", { n: signed(diff), when }), tone: diff < 0 ? "success" : "danger" };
  return diff < 0
    ? { text: t("на {n} мин раньше, чем {when}", { n: -diff, when }), tone: "success" }
    : { text: t("на {n} мин позже, чем {when}", { n: diff, when }), tone: "danger" };
}

/** Опоздания с понедельника к тем же дням прошлой недели: рост — красный. */
export function lateWeekDelta(o: HrOverview, t: T, short = false): Delta {
  const diff = o.lateWeek - o.lateLastWeek;
  if (diff === 0) return { text: t(short ? "как на прошлой" : "как на прошлой неделе"), tone: "muted" };
  return { text: t(short ? "{n} к прошлой" : "{n} к прошлой неделе", { n: signed(diff) }), tone: diff > 0 ? "danger" : "success" };
}

/** Штат за месяц: «+3 за месяц»; без дат приёма в Verifix — строки нет. */
export function activeDelta(o: HrOverview, t: T): Delta {
  if (o.activeDeltaMonth == null) return null;
  if (o.activeDeltaMonth === 0) return { text: t("без изменений за месяц"), tone: "muted" };
  return { text: t("{n} за месяц", { n: signed(o.activeDeltaMonth) }), tone: o.activeDeltaMonth > 0 ? "success" : "danger" };
}

/** «5 сотрудников», «1 филиал»: число и слово в нужной форме (по-узбекски форма одна — переводится каждое слово). */
export const counted = (n: number, forms: [string, string, string], t: T) => `${num(n)} ${t(plural(n, forms))}`;

export const capitalize = (s: string) => (s ? s[0].toUpperCase() + s.slice(1) : s);

/** Часы диаграммы: 06–18, шире — если проходы были раньше или позже. */
export function hourRange(byHour: number[]): number[] {
  let first = 6;
  let last = 18;
  byHour.forEach((count, hour) => {
    if (count > 0) {
      first = Math.min(first, hour);
      last = Math.max(last, hour);
    }
  });
  return Array.from({ length: last - first + 1 }, (_, i) => first + i);
}

/** Час с наибольшим числом проходов или null, если проходов нет. */
export function peakHour(byHour: number[]): number | null {
  let peak: number | null = null;
  byHour.forEach((count, hour) => {
    if (count > 0 && (peak == null || count > byHour[peak])) peak = hour;
  });
  return peak;
}

export const pad2 = (n: number) => String(n).padStart(2, "0");

// ---------- «Требует внимания» ----------

export const attentionMeta: Record<HrAttention["kind"], { label: string; tone: Tone }> = {
  absent: { label: "Отсутствует", tone: "danger" },
  late: { label: "Опоздание", tone: "warning" },
  unknown: { label: "Не ясно", tone: "subtle" },
};

/** Время справа в строке: «09:47 · +47 мин», «смена с 08:00», «1 отметка · 08:02»; short — для телефона. */
export function attentionTime(a: HrAttention, t: T, short = false): string {
  if (a.kind === "late") {
    const late = t("+{n} мин", { n: a.lateMinutes ?? 0 });
    return short || !a.firstIn ? late : `${hhmm(a.firstIn)} · ${late}`;
  }
  if (a.kind === "absent") {
    if (!a.planBegin) return t("нет отметок");
    return t(short ? "с {time}" : "смена с {time}", { time: hhmm(a.planBegin) });
  }
  if (a.events === 0) return t("нет данных");
  const marks = counted(a.events, ["отметка", "отметки", "отметок"], t);
  return short || !a.firstEvent ? marks : `${marks} · ${hhmm(a.firstEvent)}`;
}

/** Инициалы для аватара: «Азизов Тимур» → «АТ». */
export const initials = (name: string) =>
  name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((w) => w[0]?.toUpperCase() ?? "")
    .join("");

// ---------- Синхронизация Verifix ----------

export type SyncChipState = {
  tone: "success" | "warning" | "danger" | "accent";
  text: string;
  short: string;
  vars?: Record<string, string | number>;
  /** Причина ошибки — в подсказке. */
  error?: string;
  failed: boolean;
};

/** «HH:mm» момента (UTC) по Ташкенту. */
const tashkentTime = (iso: string) =>
  new Intl.DateTimeFormat("ru-RU", { timeZone: "Asia/Tashkent", hour: "2-digit", minute: "2-digit" }).format(new Date(iso));
const tashkentDay = (iso: string) => new Intl.DateTimeFormat("ru-RU", { timeZone: "Asia/Tashkent", day: "2-digit", month: "2-digit" }).format(new Date(iso));

/**
 * Чип статуса Verifix: синхронизировано недавно — зелёный «Verifix · синхр. 2 мин назад»; старше часа, частично, выключено, не подключено
 * или ещё не было — жёлтый; последняя закончилась ошибкой — красный «Ошибка синхронизации · 10:02» (данные прошлой успешной остаются);
 * идёт — синий.
 */
export function syncChip(sync: HrSyncBrief, now: number): SyncChipState {
  if (!sync.configured) return { tone: "warning", text: "Verifix не подключён", short: "Verifix не подключён", failed: false };
  if (!sync.enabled) return { tone: "warning", text: "Verifix · интеграция выключена", short: "Verifix выключен", failed: false };
  if (sync.running) return { tone: "accent", text: "Verifix · идёт синхронизация", short: "Verifix · синхр.", failed: false };
  if (sync.lastStatus === "Failed") {
    const time = sync.lastRunAt ? tashkentTime(sync.lastRunAt) : "";
    return {
      tone: "danger",
      text: time ? "Ошибка синхронизации · {time}" : "Ошибка синхронизации",
      short: time ? "Ошибка · {time}" : "Ошибка синхронизации",
      vars: { time },
      error: sync.lastMessage ?? "синхронизация не прошла",
      failed: true,
    };
  }
  if (!sync.lastSuccessAt) return { tone: "warning", text: "Verifix · синхронизации ещё не было", short: "Verifix · нет данных", failed: false };

  const minutes = Math.max(0, Math.floor((now - Date.parse(sync.lastSuccessAt)) / 60000));
  const tone = minutes > 60 || sync.lastStatus === "Partial" ? "warning" : "success";
  const error = sync.lastStatus === "Partial" ? (sync.lastMessage ?? undefined) : undefined;
  if (minutes < 1) return { tone, text: "Verifix · синхр. только что", short: "Verifix · сейчас", error, failed: false };
  if (minutes < 60) return { tone, text: "Verifix · синхр. {n} мин назад", short: "Verifix · {n} мин", vars: { n: minutes }, error, failed: false };
  if (minutes < 24 * 60) {
    const n = Math.floor(minutes / 60);
    return { tone, text: "Verifix · синхр. {n} ч назад", short: "Verifix · {n} ч", vars: { n }, error, failed: false };
  }
  const date = tashkentDay(sync.lastSuccessAt);
  return { tone, text: "Verifix · синхр. {date} в {time}", short: "Verifix · {date}", vars: { date, time: tashkentTime(sync.lastSuccessAt) }, error, failed: false };
}

/** До какого времени данные дня: отметки загружены до… (если это сегодня), иначе — время ответа сервера. */
export function dataAsOf(o: HrOverview): string {
  const through = o.sync.tracksThrough;
  return through && through.slice(0, 10) === o.date && through <= o.now ? hhmm(through) : hhmm(o.now);
}
