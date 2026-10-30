import type { T } from "@/i18n/core";
import { alertText, categoryNames, type AiAlertsView } from "@/lib/ai";
import type { TaskStatusSlice } from "@/lib/dashboard.mock";
import { TIME_ZONE } from "@/lib/format";

/**
 * Мобильный «Обзор» (ТЗ 1base-mobile-home): форматирование чисел (ru-RU, минус — «−»), «Фокус дня» и AI-отчёт из находок консультанта
 * (/api/ai/alerts), фильтры главной в адресе. Показатели продаж, HR и задач — lib/dashboard-data и lib/dashboard-home.
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
  metric: string;
  value: number;
  /** Изменение за последнюю неделю, п. п.; недель с данными меньше двух — null. */
  trendWeek: number | null;
  status: "ok" | "attention";
  overdue: number;
  href: string;
};

/** Задачи: кольцо — выполненные за месяц и открытые (в работе, на проверке); просроченные — часть открытых, отдельной строкой. */
export type MobileTasks = {
  total: number;
  done: number;
  overdue: number;
  slices: TaskStatusSlice[];
  departments: { code: string; name: string; overdue: number }[];
};

export type WeeklyReport = { id: string; risks: number; recommendations: number };

/** 92.1 → «92,1»; знак минуса — «−». */
export const fmt = (value: number, digits = 0) =>
  value.toLocaleString("ru-RU", { minimumFractionDigits: digits, maximumFractionDigits: digits }).replace("-", "−");

/** Со знаком: «+4,2», «−3», «0». */
export const signed = (value: number, digits = 0) => `${value > 0 ? "+" : value < 0 ? "−" : ""}${fmt(Math.abs(value), digits)}`;

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

/** Отделы компании — для фильтра и поиска; показатели есть пока у «Продаж» и HR. */
export const DEPARTMENTS: { code: string; name: string; href: string }[] = Object.entries(categoryNames).map(([code, name]) => ({
  code,
  name,
  href: code === "sales" ? "/sales" : code === "hr" ? "/hr" : "/departments",
}));

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
