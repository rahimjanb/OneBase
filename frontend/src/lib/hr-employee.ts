/**
 * Карточка сотрудника HR: имя, стаж, итог дня бейджами, отметки по сменам, подсказки (insights) и период — общее для всех блоков страницы.
 * Статусы дня считает backend (HrAttendance) — здесь только представление и производные показатели. Тексты — русские ключи перевода.
 */
import type { T } from "@/i18n/core";
import { plural } from "@/lib/format";
import { addDays, clock, duration, type HrDay, type HrEmployeeCard, type HrEvent } from "@/lib/hr";
import type { Tone } from "@/lib/hr-overview";

// ---------- Имя ----------

/** Узбекские и русские частицы отчества пишутся строчными: «Baxtiyor o'g'li», «Karim qizi». */
const PARTICLES = new Set(["o'g'li", "og'li", "o'gli", "ogli", "ugli", "qizi", "kizi", "оглы", "кызы", "угли", "ўғли", "қизи"]);

/**
 * Имя в нормальном регистре: «QAXRAMONOV SARDOR BAXTIYOR O'G'LI» → «Qaxramonov Sardor Baxtiyor o'g'li». Только если имя записано целиком
 * заглавными — иначе как в Verifix (там могут быть осознанные «Мак-Дональд», «ibn»). Хранится исходное значение, меняется только показ.
 */
export function displayName(raw: string): string {
  if (/\p{Ll}/u.test(raw)) return raw;
  return raw
    .split(/\s+/)
    .filter(Boolean)
    .map((word) => {
      const lower = word.toLocaleLowerCase("ru");
      if (PARTICLES.has(lower.replace(/[’ʼ‘`]/g, "'"))) return lower;
      return lower.replace(/(^|-)(\p{L})/gu, (_, sep: string, ch: string) => sep + ch.toLocaleUpperCase("ru"));
    })
    .join(" ");
}

/** «Фамилия Имя» для крошек. */
export const shortName = (name: string) => name.split(/\s+/).slice(0, 2).join(" ");

export const initialsOf = (name: string) =>
  name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((w) => w[0]?.toLocaleUpperCase("ru") ?? "")
    .join("");

/** Стаж от приёма до сегодня (или до увольнения): «1 г. 1 мес.», «8 мес.», «2 г.», «меньше месяца». */
export function tenure(hired: string, until: string, t: T): string {
  const [hy, hm, hd] = hired.slice(0, 10).split("-").map(Number);
  const [uy, um, ud] = until.slice(0, 10).split("-").map(Number);
  let months = (uy - hy) * 12 + (um - hm) - (ud < hd ? 1 : 0);
  if (months < 1) return t("меньше месяца");
  const years = Math.floor(months / 12);
  months %= 12;
  return [years > 0 && t("{n} г.", { n: years }), months > 0 && t("{n} мес.", { n: months })].filter(Boolean).join(" ");
}

// ---------- День ----------

export type Badge = { label: string; tone: Tone };

/** Рабочий день по графику — по нему считаются опоздания и ранние уходы. */
export const isWorkday = (d: HrDay) => d.dayKind === "W";

/**
 * Итог дня бейджами — статус никогда не передаётся только цветом. Пол сотрудника Verifix не присылает, поэтому «Опоздание», а не «Опоздал».
 * У дня может быть несколько бейджей: «Опоздание» + «Ранний уход».
 */
export function dayBadges(d: HrDay): Badge[] {
  switch (d.status) {
    case "NotEmployed":
      return [{ label: "Не работал", tone: "subtle" }];
    case "OnLeave":
      return [{ label: d.leaveName ?? "По документу", tone: "violet" }];
    case "Absent":
      return [{ label: "Отсутствовал", tone: "danger" }];
    case "Unknown":
      return [{ label: "Не ясно", tone: "subtle" }];
    case "Expected":
      return [{ label: "Ожидается", tone: "accent" }];
    case "DayOff":
      return [{ label: "Выходной", tone: "subtle" }];
    case "NoSchedule":
      return [{ label: "Нет графика", tone: "subtle" }];
  }
  if (d.workedOnDayOff) return [{ label: "Выходной · пришёл", tone: "accent" }];
  const list: Badge[] = [];
  if (d.late) list.push({ label: "Опоздание", tone: "warning" });
  if (d.earlyLeave) list.push({ label: "Ранний уход", tone: "danger" });
  if (list.length === 0) list.push({ label: d.dayKind ? "Вовремя" : "Пришёл", tone: "success" });
  if (d.noExit) list.push({ label: "Нет ухода", tone: "subtle" });
  if (d.leaveName) list.push({ label: d.leaveName, tone: "violet" });
  return list;
}

/** Цвета индикатора в полосе дней (1–2 сегмента); выходной без прихода и день, который ещё не начался, — пустой индикатор. */
export function dayTones(d: HrDay): Tone[] {
  if (d.status === "DayOff" || d.status === "NoSchedule" || d.status === "NotEmployed" || d.status === "Expected") return [];
  return dayBadges(d)
    .filter((b) => b.label !== "Нет ухода")
    .slice(0, 2)
    .map((b) => b.tone);
}

/** Пояснение под бейджами — текст расчёта с сервера («есть отметки без подтверждённого прихода — проверьте в Verifix»). */
export const dayNote = (d: HrDay, t: T): string | null => (d.reason ? capitalize(t(d.reason)) : null);

/** График дня в колонке «Дата»: «07:00–07:00», «нерабочий», «гибкий график», «нет графика». */
export function scheduleLabel(d: HrDay): string {
  if (d.planBegin) return `${clock(d.planBegin)}–${clock(d.planEnd)}`;
  if (d.dayKind && d.dayKind !== "W") return "нерабочий";
  return d.dayKind === "W" ? "гибкий график" : "нет графика";
}

/** Уход на следующий календарный день (смена через полночь) — метка «+1». */
export const leavesNextDay = (d: HrDay) => !!d.lastOut && d.lastOut.slice(0, 10) > d.date.slice(0, 10);

/** Минуты ухода от начала дня смены: 00:15 следующего дня — 24:15 (для медианы). */
const leaveMinutes = (d: HrDay) => {
  const time = Number(d.lastOut!.slice(11, 13)) * 60 + Number(d.lastOut!.slice(14, 16));
  return leavesNextDay(d) ? time + 24 * 60 : time;
};

export const capitalize = (s: string) => (s ? s[0].toLocaleUpperCase("ru") + s.slice(1) : s);

/**
 * Отметки по сменам: отметка после полуночи до ухода предыдущей смены (по табелю) относится к ней — иначе ночная смена «теряла» бы уход.
 * Остальные — к своему календарному дню. Ключ — дата «yyyy-mm-dd».
 */
export function marksByDay(days: HrDay[], events: HrEvent[]): Map<string, HrEvent[]> {
  const byDate = new Map(days.map((d) => [d.date.slice(0, 10), d]));
  const result = new Map<string, HrEvent[]>(days.map((d) => [d.date.slice(0, 10), []]));
  for (const e of [...events].sort((a, b) => a.localTime.localeCompare(b.localTime))) {
    if (e.missingSince) continue;
    const date = e.localTime.slice(0, 10);
    const previous = byDate.get(addDays(date, -1));
    const owner = previous?.lastOut && previous.lastOut.slice(0, 10) === date && e.localTime.slice(0, 16) <= previous.lastOut.slice(0, 16) ? previous.date.slice(0, 10) : date;
    result.get(owner)?.push(e);
  }
  return result;
}

// ---------- Итоги периода ----------

export type PeriodStats = {
  worked: number;
  daysWithArrival: number;
  workingDays: number;
  dayOffWorked: number;
  /** Все выходы в нерабочие дни — воскресенья («+2 выхода в воскресенье»). */
  dayOffSundays: boolean;
  lateDays: number;
  lateMinutes: number;
  avgLate: number | null;
  earlyDays: number;
  earlyMinutes: number;
  /** Обычное время ухода в дни с ранним уходом — медиана, «HH:mm». */
  typicalLeave: string | null;
  absentDays: number;
  leaveDays: number;
  unclearDays: number;
  marks: number;
};

/** Итоги периода — те же, что сумма по строкам таблицы (сводка backend + производные показатели). */
export function periodStats(card: HrEmployeeCard, marks: Map<string, HrEvent[]>): PeriodStats {
  const s = card.summary;
  const offDays = card.days.filter((d) => d.workedOnDayOff);
  const early = card.days.filter((d) => d.earlyLeave && d.lastOut).map(leaveMinutes).sort((a, b) => a - b);
  const median = early.length ? early[Math.floor((early.length - 1) / 2)] : null;
  return {
    worked: s.workedMinutes,
    daysWithArrival: s.presentDays,
    workingDays: s.planDays,
    dayOffWorked: offDays.length,
    dayOffSundays: offDays.length > 0 && offDays.every((d) => new Date(`${d.date.slice(0, 10)}T12:00:00Z`).getUTCDay() === 0),
    lateDays: s.lateDays,
    lateMinutes: s.lateMinutes,
    avgLate: s.lateDays > 0 ? Math.round(s.lateMinutes / s.lateDays) : null,
    earlyDays: s.earlyDays,
    earlyMinutes: s.earlyMinutes,
    typicalLeave: median == null ? null : `${String(Math.floor(median / 60) % 24).padStart(2, "0")}:${String(median % 60).padStart(2, "0")}`,
    absentDays: s.absentDays,
    leaveDays: s.leaveDays,
    unclearDays: s.unknownDays,
    marks: [...marks.values()].reduce((n, list) => n + list.length, 0),
  };
}

// ---------- Фильтры таблицы ----------

export type DayFilter = "all" | "violations" | "unclear" | "late" | "early" | "absent";

export const matchesFilter = (d: HrDay, f: DayFilter) =>
  f === "all"
    ? true
    : f === "violations"
      ? d.late || d.earlyLeave || d.status === "Absent"
      : f === "unclear"
        ? d.status === "Unknown"
        : f === "late"
          ? d.late
          : f === "early"
            ? d.earlyLeave
            : d.status === "Absent";

export const FILTER_LABEL: Record<DayFilter, string> = {
  all: "Все",
  violations: "С нарушениями",
  unclear: "Нужна проверка",
  late: "Опоздания",
  early: "Ранние уходы",
  absent: "Отсутствия",
};

// ---------- Подсказки ----------

export type InsightType = "schedule_mismatch" | "frequent_late" | "frequent_early_leave" | "unclear_days";
export type Insight = { type: InsightType; title: string; text: string };

const RANGE = /(\d{1,2}):(\d{2})\s*[–—-]\s*(\d{1,2}):(\d{2})/;
const hm = (h: string, m: string) => `${h.padStart(2, "0")}:${m}`;

/** «24 ч», «12 ч», «8 ч 30 мин» — длина смены по табелю. */
const shiftLength = (d: HrDay, t: T) => {
  if (!d.planBegin || !d.planEnd) return null;
  const minutes = Math.round((Date.parse(`${d.planEnd}Z`) - Date.parse(`${d.planBegin}Z`)) / 60000);
  return minutes > 0 ? duration(minutes, t) : null;
};

/**
 * Одна, самая важная подсказка периода (или null):
 * schedule_mismatch — график в карточке (время из названия) не совпадает с графиком дней табеля в ≥ 50 % дней (вместе с частыми опозданиями —
 * одним блоком); frequent_late и frequent_early_leave — опоздания или ранние уходы в ≥ 70 % рабочих дней (от 5 рабочих дней);
 * unclear_days — есть дни «не ясно».
 */
export function insightOf(card: HrEmployeeCard, stats: PeriodStats, t: T): Insight | null {
  const workdays = card.days.filter(isWorkday);
  const work = workdays.length;
  const total = `${work} ${t(plural(work, ["рабочего дня", "рабочих дней", "рабочих дней"]))}`;
  const frequentLate = work >= 5 && stats.lateDays / work >= 0.7;
  const frequentEarly = work >= 5 && stats.earlyDays / work >= 0.7;

  // График из карточки против графика дней табеля.
  const range = card.employee.schedule?.match(RANGE);
  let mismatch: { plan: string; hours: string | null; card: string } | null = null;
  if (range) {
    const cardRange = `${hm(range[1], range[2])}–${hm(range[3], range[4])}`;
    const planned = workdays.filter((d) => d.planBegin);
    const differ = planned.filter((d) => `${clock(d.planBegin)}–${clock(d.planEnd)}` !== cardRange);
    if (planned.length >= 3 && differ.length * 2 >= planned.length) {
      const counts = new Map<string, HrDay[]>();
      for (const d of differ) {
        const key = `${clock(d.planBegin)}–${clock(d.planEnd)}`;
        counts.set(key, [...(counts.get(key) ?? []), d]);
      }
      const [plan, sample] = [...counts.entries()].sort((a, b) => b[1].length - a[1].length)[0];
      mismatch = { plan, hours: shiftLength(sample[0], t), card: cardRange };
    }
  }

  const check = t("Проверьте график в Verifix.");
  if (mismatch) {
    const vars = { plan: mismatch.hours ? `${mismatch.plan} (${mismatch.hours})` : mismatch.plan, card: mismatch.card, leave: stats.typicalLeave ?? "" };
    const why =
      stats.earlyDays > 0 && stats.typicalLeave
        ? t("В табеле смена {plan}, а в карточке — {card}, поэтому уход в ~{leave} считается ранним.", vars)
        : t("В табеле смена {plan}, а в карточке — {card}.", vars);
    return frequentLate
      ? { type: "schedule_mismatch", title: t("Опоздания почти каждую смену"), text: `${t("{n} из {total} с опозданием.", { n: stats.lateDays, total })} ${why} ${check}` }
      : { type: "schedule_mismatch", title: t("График в табеле не совпадает с карточкой"), text: `${why} ${check}` };
  }
  if (frequentLate) {
    const avg = stats.avgLate != null ? ` ${t("В среднем {n}.", { n: duration(stats.avgLate, t) })}` : "";
    return { type: "frequent_late", title: t("Опоздания почти каждую смену"), text: `${t("{n} из {total} с опозданием.", { n: stats.lateDays, total })}${avg}` };
  }
  if (frequentEarly) {
    const usual = stats.typicalLeave ? ` ${t("Обычно уходит ~{time}.", { time: stats.typicalLeave })}` : "";
    return { type: "frequent_early_leave", title: t("Ранний уход почти каждую смену"), text: `${t("{n} из {total} с ранним уходом.", { n: stats.earlyDays, total })}${usual}` };
  }
  if (stats.unclearDays > 0) {
    const n = stats.unclearDays;
    return {
      type: "unclear_days",
      title: t("Есть дни, которые нужно проверить"),
      text: t(plural(n, ["{n} день требует проверки в Verifix.", "{n} дня требуют проверки в Verifix.", "{n} дней требуют проверки в Verifix."]), { n }),
    };
  }
  return null;
}

// ---------- Период ----------

export type Preset = "week" | "2weeks" | "month" | "custom";

export const daysBetween = (from: string, to: string) => Math.round((Date.parse(`${to}T12:00:00Z`) - Date.parse(`${from}T12:00:00Z`)) / 86_400_000) + 1;
const monthStart = (date: string) => `${date.slice(0, 8)}01`;
const monthEnd = (date: string) => addDays(`${addDays(monthStart(date), 32).slice(0, 8)}01`, -1);
const min = (a: string, b: string) => (a < b ? a : b);

/** Какой сегмент выбран для периода: календарный месяц, 7 дней, 14 дней или свой. */
export function presetOf(from: string, to: string, last: string): Preset {
  if (from === monthStart(from) && to.slice(0, 7) === from.slice(0, 7) && (to === monthEnd(from) || to === last)) return "month";
  const n = daysBetween(from, to);
  return n === 7 ? "week" : n === 14 ? "2weeks" : "custom";
}

/** Период сегмента, заканчивающийся днём end: неделя и 2 недели — по end, месяц — календарный месяц end (по сегодня). */
export function presetRange(preset: Exclude<Preset, "custom">, end: string, last: string): [string, string] {
  if (preset === "month") return [monthStart(end), min(monthEnd(end), last)];
  return [addDays(end, preset === "week" ? -6 : -13), end];
}

/** Сдвиг периода стрелками: месяц — на календарный месяц, остальное — на свою длину; дальше сегодня не уходит. */
export function shiftRange(from: string, to: string, preset: Preset, dir: -1 | 1, last: string): [string, string] {
  if (preset === "month") {
    const start = dir < 0 ? monthStart(addDays(from, -1)) : addDays(monthEnd(from), 1);
    return [start, min(monthEnd(start), last)];
  }
  const n = daysBetween(from, to);
  let end = addDays(to, dir * n);
  if (end > last) end = last;
  return [addDays(end, -(n - 1)), end];
}
