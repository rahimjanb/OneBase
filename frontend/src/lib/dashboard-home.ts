import type { T } from "@/i18n/core";
import type { DepartmentRow, Kpi, Tone, TrendSeries } from "@/lib/dashboard.mock";
import type { HomeFast, HomeTrends } from "@/lib/dashboard-data";
import { fmt, signed, type MobileDepartment, type MobileTasks } from "@/lib/dashboard-mobile";
import type { DepartmentTasks, WorkOverview } from "@/lib/tasks";

/**
 * Вид главной из настоящих данных (lib/dashboard-data): что показывают блоки телефона и компьютера. Главный показатель — выполнение
 * плана продаж; нет доступа к продажам — явка; три колонки под ним — из того, что есть (прогноз, явка, задачи). Отделы — те, по которым
 * есть данные: «Продажи» (план месяца) и HR (явка за месяц). Строки уже переведены (t) — клиентские блоки выводят их как есть.
 */

export type HomeTone = "success" | "warning" | "danger";
export type HomeFormat = "pct1" | "int" | "money";

export type HomeHero = {
  label: string;
  value: number;
  /** Изменение к прошлому периоду: «+2,0%» / «−1,4 п.п.»; знак — цвет и стрелка. */
  delta: { value: number; text: string; hint: string } | null;
  /** Кольцо цели: значение, цель и цвет по правилам раздела. */
  ring: { value: number; target: number; tone: HomeTone; label: string };
  href: string;
};

export type HomeStat = {
  key: string;
  label: string;
  value: number;
  format: HomeFormat;
  tone: "text" | "warning";
  sub: string | null;
  subTone: "success" | "danger" | "link" | "muted";
  href: string;
};

export type HomeHeadline = { kind: "sales" | "attendance"; label: string; value: number; tone: HomeTone };

export type HomeView = {
  hero: HomeHero | null;
  stats: HomeStat[];
  /** «План выполнен на 60,6%» / «Явка сегодня 71,4%» в приветствии. */
  headline: HomeHeadline | null;
  /** Задачи: null — раздел недоступен. */
  tasks: MobileTasks | null;
  activeTasks: number | null;
};

/** Уровень продаж с сервера: от 90% — зелёный, от 60% — оранжевый, ниже — красный. */
const salesTone = (level: string | null, value: number): HomeTone =>
  level === "Good" ? "success" : level === "Warning" ? "warning" : level === "Bad" ? "danger" : value >= 90 ? "success" : value >= 60 ? "warning" : "danger";
/** Явка — как в HR: от 80% — зелёный, 70–79% — жёлтый, ниже — красный. */
export const attendanceTone = (value: number): HomeTone => (value >= 80 ? "success" : value >= 70 ? "warning" : "danger");

/** Задачи для кольца: выполненные за месяц, в работе (новые и в работе), на проверке; просроченные — часть открытых. */
export function homeTasks(tasks: WorkOverview | null): MobileTasks | null {
  return tasks
    ? {
        total: tasks.doneThisMonth + tasks.open,
        done: tasks.doneThisMonth,
        overdue: tasks.overdue,
        slices: [
          { key: "done", label: "Выполнено", count: tasks.doneThisMonth, tone: "ok" },
          { key: "progress", label: "В работе", count: Math.max(0, tasks.open - tasks.review), tone: "accent" },
          { key: "review", label: "На проверке", count: tasks.review, tone: "violet" },
        ],
        departments: tasks.departments.map((d) => ({ code: d.code, name: d.name, overdue: d.overdue })),
      }
    : null;
}

export function homeView(data: HomeFast, t: T): HomeView {
  const { sales, hr, tasks } = data;
  const execution = sales?.execution != null ? 100 * sales.execution : null;
  const present = hr?.presentPct ?? null;
  const presentDelta = present != null && hr?.comparePct != null ? present - hr.comparePct : null;

  let hero: HomeHero | null = null;
  if (sales && execution != null) {
    const vs = sales.vsPrevMonth != null ? 100 * sales.vsPrevMonth : null;
    hero = {
      label: t("Выполнение плана продаж"),
      value: execution,
      delta: vs != null ? { value: vs, text: `${signed(vs, 1)}%`, hint: t("к тем же дням прошлого месяца") } : null,
      ring: { value: execution, target: 90, tone: salesTone(sales.level, execution), label: t("цель") },
      href: "/sales",
    };
  } else if (present != null) {
    hero = {
      label: t("Явка сегодня"),
      value: present,
      delta: presentDelta != null ? { value: presentDelta, text: `${signed(presentDelta, 1)} ${t("п.п.")}`, hint: t("к прошлому рабочему дню") } : null,
      ring: { value: present, target: 80, tone: attendanceTone(present), label: t("норма") },
      href: "/hr",
    };
  }

  const candidates: (HomeStat | null)[] = [
    sales?.forecastExecution != null && hero?.href === "/sales"
      ? {
          key: "forecast",
          label: t("Прогноз"),
          value: 100 * sales.forecastExecution,
          format: "pct1",
          tone: "text",
          sub: t("к концу месяца"),
          subTone: "muted",
          href: "/sales",
        }
      : null,
    present != null && hero?.href !== "/hr"
      ? {
          key: "attendance",
          label: t("Явка сегодня"),
          value: present,
          format: "pct1",
          tone: attendanceTone(present) === "success" ? "text" : "warning",
          sub: presentDelta != null ? `${signed(presentDelta, 1)} ${t("п.п.")}` : null,
          subTone: presentDelta == null ? "muted" : presentDelta >= 0 ? "success" : "danger",
          href: "/hr",
        }
      : null,
    tasks
      ? {
          key: "tasks",
          label: t("Задачи"),
          value: tasks.open,
          format: "int",
          tone: "text",
          sub: tasks.overdue > 0 ? t("{n} просрочено", { n: tasks.overdue }) : tasks.dueThisWeek > 0 ? t("{n} срок на неделе", { n: tasks.dueThisWeek }) : null,
          subTone: tasks.overdue > 0 ? "danger" : "link",
          href: tasks.overdue > 0 ? "/tasks/all?status=overdue" : "/tasks",
        }
      : null,
    hr?.monthRate != null
      ? { key: "month", label: t("Явка за месяц"), value: hr.monthRate, format: "pct1", tone: "text", sub: null, subTone: "muted", href: "/hr/reports" }
      : null,
    sales ? { key: "revenue", label: t("Выручка"), value: sales.revenue, format: "money", tone: "text", sub: t("за месяц"), subTone: "muted", href: "/sales" } : null,
  ];

  return {
    hero,
    stats: candidates.filter((s): s is HomeStat => s !== null).slice(0, 3),
    headline:
      execution != null
        ? { kind: "sales", label: t("План выполнен на"), value: execution, tone: salesTone(sales?.level ?? null, execution) }
        : present != null
          ? { kind: "attendance", label: t("Явка сегодня"), value: present, tone: attendanceTone(present) }
          : null,
    tasks: homeTasks(tasks),
    activeTasks: tasks?.open ?? null,
  };
}

/** Последнее изменение ряда за неделю (последняя неделя к предыдущей, обе с данными), п. п. */
function weekChange(values: (number | null)[] | null): number | null {
  if (!values) return null;
  const known = values.filter((v): v is number => v != null);
  return known.length > 1 ? known[known.length - 1] - known[known.length - 2] : null;
}

/** Отделы с данными: «Продажи» — выполнение плана месяца, HR — явка с начала месяца. Без доступа к разделу — строки нет. */
export function homeDepartments(data: HomeFast, trends: HomeTrends | null, t: T): MobileDepartment[] {
  const rows: MobileDepartment[] = [];
  const overdue = (code: string) => data.tasks?.departments.find((d) => d.code === code)?.overdue ?? 0;
  const execution = data.sales?.execution;
  if (data.sales && execution != null) {
    const value = 100 * execution;
    rows.push({
      code: "sales",
      name: t("Продажи"),
      metric: t("Выполнение плана месяца"),
      value,
      trendWeek: weekChange(trends?.sales ?? null),
      status: salesTone(data.sales.level, value) === "success" ? "ok" : "attention",
      overdue: overdue("sales"),
      href: "/sales",
    });
  }
  if (data.hr?.monthRate != null) {
    rows.push({
      code: "hr",
      name: t("HR"),
      metric: t("Явка с начала месяца"),
      value: data.hr.monthRate,
      trendWeek: weekChange(trends?.attendance ?? null),
      status: data.hr.monthRate >= 80 ? "ok" : "attention",
      overdue: overdue("hr"),
      href: "/hr",
    });
  }
  return rows;
}

/** Недельные ряды для графика динамики: продажи к плану (цель 90%), выручка (без цели), явка (норма 80%). Ряда нет — вкладки нет. */
export function homeSeries(trends: HomeTrends, t: T): TrendSeries[] {
  const weeks = trends.weeks.map((w) => t("Н{n}", { n: w.week }));
  const series: TrendSeries[] = [];
  if (trends.sales?.some((v) => v != null)) series.push({ key: "sales", label: t("Продажи"), weeks, values: trends.sales, target: 90, unit: "pct" });
  if (trends.revenue?.some((v) => v != null)) series.push({ key: "revenue", label: t("Выручка"), weeks, values: trends.revenue, unit: "money" });
  if (trends.attendance?.some((v) => v != null)) series.push({ key: "attendance", label: t("Явка"), weeks, values: trends.attendance, target: 80, unit: "pct" });
  return series;
}

// ---------- Компьютер: те же данные в виде карточек и таблицы десктопного «Обзора» ----------

const toneOf: Record<HomeTone, Tone> = { success: "ok", warning: "warn", danger: "bad" };
const bars = (values: (number | null)[] | null | undefined) => (values ?? []).map((v) => Math.max(0, Math.min(100, v ?? 0)));

/** Четыре карточки KPI: план продаж, явка сегодня, активные задачи, просрочено — те, по которым есть данные. */
export function desktopKpis(data: HomeFast, trends: HomeTrends | null, t: T): Kpi[] {
  const list: Kpi[] = [];
  const { sales, hr, tasks } = data;
  if (sales?.execution != null) {
    const value = 100 * sales.execution;
    const vs = sales.vsPrevMonth != null ? 100 * sales.vsPrevMonth : null;
    list.push({
      id: "plans",
      label: t("Выполнение плана продаж"),
      value: `${fmt(value, 1)}%`,
      tone: toneOf[salesTone(sales.level, value)],
      icon: "target",
      change:
        vs != null
          ? { tone: vs >= 0 ? "ok" : "bad", icon: vs >= 0 ? "up" : "down", text: `${signed(vs, 1)}%`, hint: t("к тем же дням прошлого месяца") }
          : { tone: "muted", icon: "plus", text: "—", hint: t("нет данных прошлого месяца") },
      bars: bars(trends?.sales),
    });
  }
  if (hr?.presentPct != null) {
    const delta = hr.comparePct != null ? hr.presentPct - hr.comparePct : null;
    list.push({
      id: "attendance",
      label: t("Явка сегодня"),
      value: `${fmt(hr.presentPct, 1)}%`,
      tone: toneOf[attendanceTone(hr.presentPct)],
      icon: "gauge",
      change:
        delta != null
          ? { tone: delta >= 0 ? "ok" : "bad", icon: delta >= 0 ? "up" : "down", text: `${signed(delta, 1)} ${t("п.п.")}`, hint: t("к прошлому рабочему дню") }
          : { tone: "muted", icon: "plus", text: "—", hint: t("сравнить не с чем") },
      bars: bars(trends?.attendance),
    });
  }
  if (tasks) {
    list.push({
      id: "tasks",
      label: t("Активные задачи"),
      value: fmt(tasks.open),
      tone: "violet",
      icon: "tasks",
      change: { tone: "accent", icon: "plus", text: fmt(tasks.dueThisWeek), hint: t("срок на этой неделе") },
      bars: [],
    });
    list.push({
      id: "overdue",
      label: t("Просрочено"),
      value: fmt(tasks.overdue),
      tone: tasks.overdue > 0 ? "warn" : "ok",
      icon: "alarm",
      change: { tone: tasks.blocked > 0 ? "bad" : "muted", icon: "plus", text: fmt(tasks.blocked), hint: t("заблокировано") },
      bars: [],
    });
  }
  return list;
}

/** Строки таблицы «Состояние отделов»: показатель, тренд по неделям с данными (до 8), задачи и просрочки отдела. */
export function desktopDepartments(rows: MobileDepartment[], trends: HomeTrends | null, work: DepartmentTasks[] | null): DepartmentRow[] {
  const trendOf = (code: string) =>
    ((code === "sales" ? trends?.sales : code === "hr" ? trends?.attendance : null) ?? []).filter((v): v is number => v != null).slice(-8).map((v) => Math.round(v));
  return rows.map((r) => {
    const real = work?.find((d) => d.code === r.code);
    return {
      code: r.code,
      name: r.name,
      icon: r.code === "sales" ? "handshake" : "users",
      tone: r.code === "sales" ? "ok" : "cyan",
      manager: null,
      metric: r.metric,
      value: Math.round(r.value),
      trend: trendOf(r.code),
      tasks: real?.openTasks ?? null,
      overdue: real?.overdueTasks ?? r.overdue,
      status: r.status === "attention" ? "attention" : "ok",
      href: r.href,
    };
  });
}

/** Сколько минут назад обновлены данные HR (последняя синхронизация с Verifix); нет — 0. */
export function minutesSince(iso: string | null | undefined, now: Date): number {
  return iso ? Math.max(0, Math.round((now.getTime() - Date.parse(iso)) / 60_000)) : 0;
}

// ---------- График динамики: общая шкала и итоги для телефона и компьютера ----------

const known = (values: (number | null)[]) => values.filter((v): v is number => v != null);

/** «Круглый» верх шкалы денег: 3,62 млрд → 4 млрд. */
function niceCeil(value: number): number {
  if (value <= 0) return 1;
  const step = 10 ** Math.floor(Math.log10(value)) / 2;
  return Math.ceil(value / step) * step;
}

/**
 * Шкала ряда: проценты — от 60 до 100 (шире, если значения или цель выходят за неё), деньги — от нуля до «круглого» максимума.
 * Без подписей оси на телефоне; компьютер делит её на четыре интервала.
 */
export function trendScale(series: TrendSeries): { lo: number; hi: number } {
  const values = known(series.values);
  if (series.unit === "money") return { lo: 0, hi: niceCeil(Math.max(0, ...values)) };
  const all = series.target != null ? [...values, series.target] : values;
  const lo = Math.max(0, Math.min(60, Math.floor((Math.min(...all) - 5) / 10) * 10));
  const hi = Math.max(100, Math.ceil((Math.max(...all) + 2) / 10) * 10);
  return { lo, hi };
}

/** Итоги ряда по неделям с данными: среднее, лучшая неделя, недель не ниже цели (без цели — сумма за период). */
export function trendStats(series: TrendSeries) {
  const values = known(series.values);
  const total = values.reduce((a, b) => a + b, 0);
  return {
    count: values.length,
    average: values.length ? total / values.length : null,
    best: values.length ? Math.max(...values) : null,
    above: series.target != null ? values.filter((v) => v >= series.target!).length : null,
    total,
  };
}

/** Куда ведёт «Подробнее» у ряда: продажи — в раздел продаж, явка — в отчёты HR. */
export const trendHref = (series: TrendSeries) => (series.key === "attendance" ? "/hr/reports" : "/sales");
