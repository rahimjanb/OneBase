import {
  Archive,
  CalendarClock,
  ChartColumn,
  DoorOpen,
  FolderKanban,
  KanbanSquare,
  LayoutGrid,
  ListChecks,
  Sparkles,
  Table2,
  UserRound,
  Users,
  UsersRound,
  type LucideIcon,
} from "lucide-react";
import { activeSalesTab, salesTabs } from "@/components/sales/tabs";

/** Кнопка раздела в верхней панели. permission — видна только с этим правом; headsOnly — только руководителям отделов (Задачи). */
export type SectionTab = { key: string; label: string; href: string; icon: LucideIcon; permission?: string; headsOnly?: boolean };

/**
 * Раздел с кнопками в верхней панели (как в «Продажах»): какие кнопки и какая из них открыта для адреса.
 * keepPeriod — переносить период и план между кнопками (только «Продажи»).
 */
export type SectionDef = { id: "sales" | "hr" | "tasks"; label: string; tabs: SectionTab[]; active: (pathname: string) => string | null; keepPeriod?: boolean };

const under = (pathname: string, href: string) => pathname === href || pathname.startsWith(`${href}/`);

const hrTabs: SectionTab[] = [
  { key: "overview", label: "Обзор HR", href: "/hr", icon: LayoutGrid },
  { key: "employees", label: "Сотрудники", href: "/hr/employees", icon: Users },
  { key: "attendance", label: "Посещаемость", href: "/hr/attendance", icon: DoorOpen },
  { key: "deviations", label: "Опоздания и отсутствия", href: "/hr/deviations", icon: CalendarClock },
  { key: "timesheets", label: "Рабочее время и табели", href: "/hr/timesheets", icon: Table2 },
  { key: "reports", label: "Отчёты", href: "/hr/reports", icon: ChartColumn },
];

const taskTabs: SectionTab[] = [
  { key: "overview", label: "Обзор", href: "/tasks", icon: LayoutGrid },
  { key: "all", label: "Все задачи", href: "/tasks/all", icon: ListChecks },
  { key: "board", label: "Канбан", href: "/tasks/board", icon: KanbanSquare },
  { key: "my", label: "Мои задачи", href: "/tasks/my", icon: UserRound },
  { key: "department", label: "Задачи отдела", href: "/tasks/department", icon: UsersRound },
  { key: "archive", label: "Архив", href: "/tasks/archive", icon: Archive },
  { key: "suggestions", label: "Предложения AI", href: "/tasks/suggestions", icon: Sparkles, headsOnly: true },
  { key: "projects", label: "Проекты", href: "/tasks/projects", icon: FolderKanban },
];

export const sections: SectionDef[] = [
  { id: "sales", label: "Разделы продаж", tabs: salesTabs, active: activeSalesTab, keepPeriod: true },
  {
    id: "hr",
    label: "Разделы HR",
    tabs: hrTabs,
    // Карточка сотрудника — «Сотрудники».
    active: (pathname) => (pathname === "/hr" ? "overview" : hrTabs.find((t) => t.key !== "overview" && under(pathname, t.href))?.key ?? null),
  },
  {
    id: "tasks",
    label: "Разделы задач",
    tabs: taskTabs,
    // Карточка задачи — «Все задачи»; страница отдела (/tasks/{код}) — «Задачи отдела».
    active: (pathname) => {
      if (pathname === "/tasks") return "overview";
      if (pathname.startsWith("/tasks/task/")) return "all";
      const fixed = taskTabs.find((t) => t.key !== "overview" && under(pathname, t.href));
      return fixed?.key ?? (pathname.startsWith("/tasks/") ? "department" : null);
    },
  },
];

/** Раздел страницы: «Продажи», «HR», «Задачи» — или null (в остальных разделах кнопок в шапке нет). */
export const sectionOf = (pathname: string): SectionDef | null =>
  sections.find((s) => (s.id === "sales" ? pathname.startsWith("/sales") : under(pathname, s.id === "hr" ? "/hr" : "/tasks"))) ?? null;
