/**
 * Типы блоков главной «Обзор» (карточки, график, задачи, отделы, дедлайны, лента) и периоды фильтра. Сами данные — настоящие:
 * lib/dashboard-data (продажи, HR, задачи) и lib/dashboard (дедлайны, лента, находки консультанта). Цвета — только имена токенов темы
 * (globals.css), не hex.
 */

/** Цветовой тон карточки, плитки и статуса — имя токена темы. */
export type Tone = "accent" | "ok" | "warn" | "bad" | "violet" | "pink" | "cyan" | "muted";

export type InsightLevel = "bad" | "warn" | "ok";

/** Инсайт «Фокуса дня» — из находок проактивного анализа консультанта (/api/ai/alerts). */
export type Insight = {
  id: string;
  level: InsightLevel;
  /** «Критично» · «Внимание» · «Возможность». */
  levelLabel: string;
  department: string;
  text: string;
  action: string;
  href: string;
};

export type KpiChange = { tone: Tone; icon: "up" | "down" | "plus"; text: string; hint: string };

export type Kpi = {
  id: string;
  label: string;
  value: string;
  tone: Tone;
  icon: "gauge" | "target" | "tasks" | "alarm";
  change: KpiChange;
  /** 12 столбиков мини-графика (0–100): высота = значение ÷ 2 px. */
  bars: number[];
};

/** Ряд динамики по неделям: проценты (pct) или сумма (money); неделя без данных — null; target — линия цели (нет — без линии). */
export type TrendSeries = { key: string; label: string; weeks: string[]; values: (number | null)[]; target?: number; unit?: "pct" | "money" };

export type TaskStatusSlice = { key: string; label: string; count: number; tone: Tone };

export type DepartmentStatusKey = "done" | "ok" | "attention";

export type DepartmentRow = {
  code: string;
  name: string;
  icon: "factory" | "landmark" | "handshake" | "megaphone" | "truck" | "users";
  tone: Tone;
  /** Руководитель — когда появится в данных отделов; пока пусто. */
  manager: string | null;
  metric: string;
  value: number;
  /** 8 недель, нормируются по минимуму и максимуму ряда. */
  trend: number[];
  /** Активные и просроченные задачи — из /api/work/departments, когда раздел «Задачи» доступен. */
  tasks: number | null;
  overdue: number;
  status: DepartmentStatusKey;
  href: string;
};

export type Priority = "high" | "medium" | "low";

/** Задача «Ближайших дедлайнов» — из задач отделов (/api/work/tasks). */
export type UpcomingTask = {
  id: string;
  title: string;
  department: string;
  href: string;
  priority: Priority;
  /** «Сегодня», «Завтра», «26 окт.». */
  due: string;
  /** Срок горит (сегодня или просрочен), скоро (завтра) или спокойный. */
  dueTone: "bad" | "warn" | "muted";
  /** Исполнитель — инициалы, если назначен. */
  assignee: { initials: string; tone: Tone } | null;
  done?: boolean;
};

/** Событие ленты — из уведомлений пользователя (/api/notifications). */
export type ActivityEvent = {
  id: string;
  icon: "check" | "file" | "alert" | "user-plus" | "comment";
  tone: Tone;
  who: string;
  what: string;
  /** «12 мин», «1 ч». */
  when: string;
  href: string | null;
};

/** Опции фильтров над содержимым. */
export const periods = [
  { key: "7d", label: "7 дн" },
  { key: "30d", label: "30 дн" },
  { key: "quarter", label: "Квартал" },
  { key: "year", label: "Год" },
] as const;

export type PeriodKey = (typeof periods)[number]["key"];
