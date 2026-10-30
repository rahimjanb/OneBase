import type { T } from "@/i18n/core";
import { alertText, categoryNames, type AiAlertsView } from "@/lib/ai";
import { departmentRows, kpis, summary, taskStatuses, trendSeries, type DepartmentRow, type TaskStatusSlice, type TrendSeries } from "@/lib/dashboard.mock";
import { TIME_ZONE } from "@/lib/format";
import type { DepartmentTasks } from "@/lib/tasks";

/**
 * Мобильный «Обзор» (ТЗ 1base-mobile-home): те же источники, что у десктопа, — показатели из lib/dashboard.mock до подключения API,
 * «Фокус дня» и AI-отчёт — из находок консультанта (/api/ai/alerts), задачи отделов — из /api/work/departments. Здесь только
 * преобразование в вид блоков телефона и форматирование чисел (ru-RU, минус — «−»).
 */

export type Severity = "critical" | "warning" | "info";

export type FocusItem = {
  id: string;
  severity: Severity;
  /** Код отдела (finance, sales …) — для фильтра «Отделы». */
  category: string;
  department: string;
  text: string;
  /** Ключевая цифра для плашки слева («44%» / «прогноз»); нет числа в находке — null, в плашке значок. */
  metric: { value: string; caption: string } | null;
  href: string;
};

export type MobileDepartment = {
  code: string;
  name: string;
  icon: DepartmentRow["icon"];
  metric: string;
  value: number;
  /** Изменение за последнюю неделю, п. п. */
  trendWeek: number;
  status: "ok" | "attention";
  overdue: number;
  href: string;
};

export type MobileKpi = {
  plan: { value: number; delta: number; target: number };
  kpi: { value: number; delta: number };
  tasks: { active: number; createdToday: number; overdue: number; overdueDeltaWeek: number };
};

export type MobileTasks = { total: number; done: number; slices: TaskStatusSlice[] };

export type WeeklyReport = { id: string; risks: number; recommendations: number };

/** «+4,2%» → 4.2, «−3» → −3, «07» → 7: числа макета хранятся строками для десктопных карточек. */
const parseNum = (text: string) => Number(text.replace(/[−–]/g, "-").replace(/[^\d.,-]/g, "").replace(",", "."));

/** 92.1 → «92,1»; знак минуса — «−». */
export const fmt = (value: number, digits = 0) =>
  value.toLocaleString("ru-RU", { minimumFractionDigits: digits, maximumFractionDigits: digits }).replace("-", "−");

/** Со знаком: «+4,2», «−3», «0». */
export const signed = (value: number, digits = 0) => `${value > 0 ? "+" : value < 0 ? "−" : ""}${fmt(Math.abs(value), digits)}`;

export function mobileKpi(): MobileKpi {
  const byId = Object.fromEntries(kpis.map((k) => [k.id, k]));
  return {
    plan: { value: summary.planShare, delta: parseNum(byId.plans.change.text), target: trendSeries[0]?.target ?? 90 },
    kpi: { value: parseNum(byId.kpi.value), delta: parseNum(byId.kpi.change.text) },
    tasks: {
      active: summary.activeTasks,
      createdToday: parseNum(byId.tasks.change.text),
      overdue: parseNum(byId.overdue.value),
      overdueDeltaWeek: parseNum(byId.overdue.change.text),
    },
  };
}

export const mobileTasks = (): MobileTasks => ({ total: taskStatuses.total, done: taskStatuses.done, slices: taskStatuses.slices });

export const mobileTrends = (): TrendSeries[] => trendSeries;

/** Отделы как на десктопе: показатели макета, задачи и просрочки — из /api/work/departments, если раздел «Задачи» доступен. */
export function mobileDepartments(real: DepartmentTasks[] | null): MobileDepartment[] {
  return departmentRows.map((r) => {
    const tasks = real?.find((d) => d.code === r.code);
    const n = r.trend.length;
    return {
      code: r.code,
      name: r.name,
      icon: r.icon,
      metric: r.metric,
      value: r.value,
      trendWeek: n > 1 ? r.trend[n - 1] - r.trend[n - 2] : 0,
      status: r.status === "attention" ? "attention" : "ok",
      overdue: tasks?.overdueTasks ?? r.overdue,
      href: r.href,
    };
  });
}

const NUMBER = String.raw`\d[\d\s  ]*(?:[.,]\d+)?`;
// «… — прогноз 44%»: цифра с подписью в конце после тире уходит в плашку, текст — без неё.
const TAIL = new RegExp(String.raw`^(.+?)\s+[—–]\s+(?:(\p{L}+)\s+)?(${NUMBER}\s?%)$`, "u");
// Иначе — первое число в тексте вместе с единицей (% или SKU).
const FIRST = new RegExp(String.raw`(${NUMBER})(\s?%|\s?SKU\b)?`, "u");
const compact = (s: string) => s.replace(/[\s  ]+/g, " ").trim();

/** Ключевая цифра находки для плашки «Фокуса дня»: «44%» / «прогноз», «33» / «SKU»; числа нет — null. */
export function metricOf(text: string): { text: string; metric: FocusItem["metric"] } {
  const tail = text.match(TAIL);
  if (tail) return { text: tail[1], metric: { value: compact(tail[3]).replace(" %", "%"), caption: tail[2] ?? "" } };
  const first = text.match(FIRST);
  if (!first) return { text, metric: null };
  const unit = first[2]?.trim() ?? "";
  return { text, metric: { value: compact(first[1]) + (unit === "%" ? "%" : ""), caption: unit === "SKU" ? "SKU" : "" } };
}

const severityOf = { Critical: "critical", Warning: "warning", Opportunity: "info" } as const;
const rank: Record<Severity, number> = { critical: 0, warning: 1, info: 2 };

/** Все находки консультанта в виде строк «Фокуса дня»: сначала критичные, потом важные, потом возможности. */
export function focusOf(alerts: AiAlertsView, t: T): FocusItem[] {
  return alerts.alerts
    .map((a) => {
      const { text, metric } = metricOf(alertText(a.title, t));
      return {
        id: a.id,
        severity: severityOf[a.severity] ?? "warning",
        category: a.category,
        department: categoryNames[a.category] ?? a.category,
        text,
        metric,
        href: a.href ?? "/ai",
      } satisfies FocusItem;
    })
    .sort((a, b) => rank[a.severity] - rank[b.severity]);
}

/** Номер недели ISO по времени компании: «2026-W44» — отчёт этой недели. */
export function weekId(date: Date): string {
  const [y, m, d] = new Intl.DateTimeFormat("en-CA", { timeZone: TIME_ZONE, year: "numeric", month: "2-digit", day: "2-digit" }).format(date).split("-").map(Number);
  const day = new Date(Date.UTC(y, m - 1, d));
  const weekday = day.getUTCDay() || 7;
  day.setUTCDate(day.getUTCDate() + 4 - weekday);
  const yearStart = Date.UTC(day.getUTCFullYear(), 0, 1);
  const week = Math.ceil(((day.getTime() - yearStart) / 86_400_000 + 1) / 7);
  return `${day.getUTCFullYear()}-W${String(week).padStart(2, "0")}`;
}

/** Еженедельный AI-отчёт — риски и рекомендации из находок консультанта; нечего показать — null. */
export function weeklyReportOf(alerts: AiAlertsView | null, now: Date): WeeklyReport | null {
  if (!alerts) return null;
  const { problems, recommendations } = alerts.summary;
  return problems + recommendations > 0 ? { id: weekId(now), risks: problems, recommendations } : null;
}

/** Сколько дней текущей недели прошло (пн = 1 … вс = 7) — подпись под графиком динамики. */
export function daysIntoWeek(date: Date): number {
  const weekday = new Intl.DateTimeFormat("en-US", { weekday: "short", timeZone: TIME_ZONE }).format(date);
  return ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].indexOf(weekday) + 1;
}

/** Фильтры главной в адресе: ?period=7d&dept=finance,sales&emp=…; по умолчанию — 30 дней, все отделы и сотрудники. */
export type PeriodKey = "7d" | "30d" | "quarter" | "year" | "custom";
export const PERIODS: { key: Exclude<PeriodKey, "custom">; label: string }[] = [
  { key: "7d", label: "7 дней" },
  { key: "30d", label: "30 дней" },
  { key: "quarter", label: "Квартал" },
  { key: "year", label: "Год" },
];

export type HomeFilters = { period: PeriodKey; from: string | null; to: string | null; departments: string[]; employees: string[] };

const list = (value: string | null) => (value ? value.split(",").filter(Boolean) : []);

export function filtersOf(params: { get(name: string): string | null }): HomeFilters {
  const raw = params.get("period");
  const period: PeriodKey = raw === "7d" || raw === "quarter" || raw === "year" || raw === "custom" ? raw : "30d";
  return { period, from: params.get("from"), to: params.get("to"), departments: list(params.get("dept")), employees: list(params.get("emp")) };
}
