/**
 * Показатели главной «Обзор» до подключения API: типы повторяют будущие ответы сервера, числа — из макета ТЗ. Людей, компаний и событий
 * здесь нет: выдуманных имён на странице не показываем — такие блоки берут реальные данные (задачи, уведомления, находки консультанта)
 * или остаются пустыми. Цвета — только имена токенов темы (globals.css), не hex.
 */

/** Цветовой тон карточки, плитки и статуса — имя токена темы. */
export type Tone = "accent" | "ok" | "warn" | "bad" | "violet" | "pink" | "cyan" | "muted";

export type DashboardSummary = {
  /** Выполнение плана компанией, % (0–100). */
  planShare: number;
  /** Отделов, которым нужно внимание. */
  attention: number;
  /** Сколько минут назад обновлены данные. */
  updatedMinutesAgo: number;
  departments: number;
  activeTasks: number;
};

export const summary: DashboardSummary = { planShare: 92.1, attention: 2, updatedMinutesAgo: 5, departments: 6, activeTasks: 128 };

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

export const kpis: Kpi[] = [
  { id: "kpi", label: "Общий KPI", value: "89,4%", tone: "accent", icon: "gauge", change: { tone: "ok", icon: "up", text: "+2,6%", hint: "к прошлому периоду" }, bars: [52, 58, 55, 61, 60, 66, 64, 70, 72, 69, 76, 80] },
  { id: "plans", label: "Выполнение планов", value: "92,1%", tone: "ok", icon: "target", change: { tone: "ok", icon: "up", text: "+4,2%", hint: "к прошлому периоду" }, bars: [60, 62, 66, 63, 70, 72, 71, 78, 80, 79, 84, 88] },
  { id: "tasks", label: "Активные задачи", value: "128", tone: "violet", icon: "tasks", change: { tone: "accent", icon: "plus", text: "24", hint: "поставлено сегодня" }, bars: [40, 55, 48, 62, 58, 50, 66, 72, 60, 68, 74, 64] },
  { id: "overdue", label: "Просрочено", value: "07", tone: "warn", icon: "alarm", change: { tone: "ok", icon: "down", text: "−3", hint: "за неделю" }, bars: [80, 72, 76, 64, 60, 66, 52, 48, 44, 40, 36, 30] },
];

export type TrendSeries = { key: "kpi" | "revenue" | "tasks"; label: string; weeks: string[]; values: number[]; target: number };

/** Динамика по неделям: KPI, выручка (к плану) и задачи (доля закрытых) — в процентах, шкала 60–100. */
export const trendSeries: TrendSeries[] = [
  { key: "kpi", label: "KPI", weeks: ["Н32", "Н33", "Н34", "Н35", "Н36", "Н37", "Н38", "Н39", "Н40", "Н41", "Н42", "Н43"], values: [78, 81, 84, 83, 87, 91, 88, 90.5, 86, 92, 94.2, 89.4], target: 90 },
  { key: "revenue", label: "Выручка", weeks: ["Н32", "Н33", "Н34", "Н35", "Н36", "Н37", "Н38", "Н39", "Н40", "Н41", "Н42", "Н43"], values: [82, 85, 83, 88, 90, 93, 91, 89, 92, 95, 96, 92.1], target: 90 },
  { key: "tasks", label: "Задачи", weeks: ["Н32", "Н33", "Н34", "Н35", "Н36", "Н37", "Н38", "Н39", "Н40", "Н41", "Н42", "Н43"], values: [70, 72, 75, 74, 78, 80, 79, 83, 81, 85, 88, 86], target: 90 },
];

export type TaskStatusSlice = { key: string; label: string; count: number; tone: Tone };

export const taskStatuses: { total: number; done: number; slices: TaskStatusSlice[] } = {
  total: 523,
  done: 342,
  slices: [
    { key: "done", label: "Выполнено", count: 342, tone: "ok" },
    { key: "progress", label: "В работе", count: 128, tone: "accent" },
    { key: "review", label: "На проверке", count: 46, tone: "violet" },
    { key: "overdue", label: "Просрочено", count: 7, tone: "bad" },
  ],
};

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

/** Отделы — те же демо-отделы, что в разделе «Отделы» (lib/demo-data); показатели — из макета. */
export const departmentRows: DepartmentRow[] = [
  { code: "production", name: "Производство", icon: "factory", tone: "accent", manager: null, metric: "Выпуск продукции", value: 92, trend: [70, 74, 72, 80, 84, 86, 90, 92], tasks: null, overdue: 0, status: "done", href: "/base/production" },
  { code: "finance", name: "Финансы", icon: "landmark", tone: "violet", manager: null, metric: "Платёжная дисциплина", value: 87, trend: [90, 89, 88, 86, 88, 85, 86, 87], tasks: null, overdue: 0, status: "attention", href: "/base/finance" },
  { code: "sales", name: "Продажи", icon: "handshake", tone: "ok", manager: null, metric: "Выручка за период", value: 94, trend: [78, 80, 85, 84, 88, 90, 93, 94], tasks: null, overdue: 0, status: "ok", href: "/sales" },
  { code: "marketing", name: "Маркетинг", icon: "megaphone", tone: "pink", manager: null, metric: "Квалифицированные лиды", value: 81, trend: [88, 86, 84, 85, 82, 80, 82, 81], tasks: null, overdue: 0, status: "attention", href: "/base/marketing" },
  { code: "supply", name: "Снабжение", icon: "truck", tone: "warn", manager: null, metric: "Поставки в срок", value: 90, trend: [80, 82, 83, 86, 85, 88, 89, 90], tasks: null, overdue: 0, status: "ok", href: "/base/supply" },
  { code: "hr", name: "HR", icon: "users", tone: "cyan", manager: null, metric: "Удержание команды", value: 88, trend: [85, 86, 86, 87, 86, 88, 87, 88], tasks: null, overdue: 0, status: "ok", href: "/base/hr" },
];

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

/** Лидер рейтинга — появится вместе с KPI сотрудников; пока список пуст. */
export type Leader = { name: string; department: string; kpi: string; initials: string; tone: Tone };

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
