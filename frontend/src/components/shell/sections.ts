import {
  Archive,
  ArrowLeftRight,
  Building,
  CalendarClock,
  ChartColumn,
  DoorOpen,
  FileBarChart,
  FolderKanban,
  Gavel,
  KanbanSquare,
  LayoutDashboard,
  LayoutGrid,
  ListChecks,
  PackageCheck,
  PackageOpen,
  ShoppingCart,
  Sparkles,
  Table2,
  Tags,
  UserRound,
  Users,
  UsersRound,
  Warehouse,
  type LucideIcon,
} from "lucide-react";
import { activeSalesTab, salesTabs } from "@/components/sales/tabs";

/**
 * Кнопка раздела в верхней панели. short — подпись на телефоне и планшете, если полная длинная; permission — видна только с этим правом;
 * headsOnly — только руководителям отделов (Задачи).
 */
export type SectionTab = {
  key: string;
  label: string;
  short?: string;
  href: string;
  icon: LucideIcon;
  permission?: string;
  headsOnly?: boolean;
  /** Только роли «Директор» (Согласования) — признак из /api/procurement/nav, не право. */
  approverOnly?: boolean;
};

/**
 * Раздел с кнопками в верхней панели (как в «Продажах»): какие кнопки и какая из них открыта для адреса.
 * keepPeriod — переносить период и план между кнопками (только «Продажи»).
 */
export type SectionDef = { id: "sales" | "hr" | "tasks" | "requests" | "procurement"; label: string; tabs: SectionTab[]; active: (pathname: string) => string | null; keepPeriod?: boolean };

const under = (pathname: string, href: string) => pathname === href || pathname.startsWith(`${href}/`);

const hrTabs: SectionTab[] = [
  { key: "overview", label: "Обзор", href: "/hr", icon: LayoutGrid },
  { key: "employees", label: "Сотрудники", href: "/hr/employees", icon: Users },
  { key: "attendance", label: "Посещаемость", href: "/hr/attendance", icon: DoorOpen },
  { key: "deviations", label: "Опоздания и отсутствия", short: "Опоздания", href: "/hr/deviations", icon: CalendarClock },
  { key: "timesheets", label: "Табели", href: "/hr/timesheets", icon: Table2 },
  { key: "reports", label: "Отчёты", href: "/hr/reports", icon: ChartColumn },
];

export const taskTabs: SectionTab[] = [
  { key: "overview", label: "Обзор", href: "/tasks", icon: LayoutGrid },
  { key: "all", label: "Все задачи", short: "Все", href: "/tasks/all", icon: ListChecks },
  { key: "board", label: "Канбан", href: "/tasks/board", icon: KanbanSquare },
  { key: "my", label: "Мои задачи", short: "Мои", href: "/tasks/my", icon: UserRound },
  { key: "department", label: "Задачи отдела", short: "Отдел", href: "/tasks/department", icon: UsersRound },
  { key: "archive", label: "Архив", href: "/tasks/archive", icon: Archive },
  { key: "suggestions", label: "Предложения AI", short: "AI", href: "/tasks/suggestions", icon: Sparkles, headsOnly: true },
  { key: "projects", label: "Проекты", href: "/tasks/projects", icon: FolderKanban },
];

/**
 * «Заявки» (снабжение): обзор, все заявки — снабжению и директору (procurement.read), мои, заявки отдела — руководителям отделов,
 * категории — тем, кто их настраивает. Задачи снабжения — в «Задачах» (отдел «Снабжение»).
 */
export const requestTabs: SectionTab[] = [
  { key: "overview", label: "Обзор", href: "/requests", icon: LayoutGrid },
  { key: "all", label: "Все заявки", short: "Все", href: "/requests/all", icon: ListChecks, permission: "procurement.read" },
  { key: "my", label: "Мои заявки", short: "Мои", href: "/requests/my", icon: UserRound },
  { key: "department", label: "Заявки отдела", short: "Отдел", href: "/requests/department", icon: UsersRound, headsOnly: true },
  { key: "approvals", label: "Согласования", href: "/requests/approvals", icon: Gavel, approverOnly: true },
  { key: "categories", label: "Настройки", href: "/requests/categories", icon: Tags, permission: "procurement.settings" },
];

/**
 * «Снабжение»: заказы поставщикам и поставщики (снабжение, директор — procurement.read; кладовщику — склад), склад (остатки и каталог),
 * заявки к выдаче, журнал движений.
 */
export const procurementTabs: SectionTab[] = [
  { key: "overview", label: "Обзор", href: "/procurement", icon: LayoutDashboard },
  { key: "orders", label: "Заказы", href: "/procurement/orders", icon: ShoppingCart, permission: "procurement.read" },
  { key: "suppliers", label: "Поставщики", href: "/procurement/suppliers", icon: Building, permission: "procurement.read" },
  { key: "receipts", label: "Приёмка", href: "/procurement/receipts", icon: PackageOpen },
  { key: "stock", label: "Склад", href: "/procurement/stock", icon: Warehouse },
  { key: "issue", label: "К выдаче", href: "/procurement/issue", icon: PackageCheck },
  { key: "movements", label: "Движения", href: "/procurement/movements", icon: ArrowLeftRight },
  { key: "reports", label: "Отчёты", href: "/procurement/reports", icon: FileBarChart, permission: "procurement.read" },
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
    // Карточка задачи — «Все задачи» (SectionNav подсвечивает список, откуда её открыли); страница отдела (/tasks/{код}) — «Задачи отдела».
    active: (pathname) => {
      if (pathname === "/tasks") return "overview";
      if (pathname.startsWith("/tasks/task/")) return "all";
      const fixed = taskTabs.find((t) => t.key !== "overview" && under(pathname, t.href));
      return fixed?.key ?? (pathname.startsWith("/tasks/") ? "department" : null);
    },
  },
  {
    id: "requests",
    label: "Разделы заявок",
    tabs: requestTabs,
    // Новая заявка — «Мои заявки»; карточка заявки — без подсветки (открывают из любого списка и из уведомления).
    active: (pathname) => {
      if (pathname === "/requests") return "overview";
      if (pathname === "/requests/new") return "my";
      return requestTabs.find((t) => t.key !== "overview" && under(pathname, t.href))?.key ?? null;
    },
  },
  {
    id: "procurement",
    label: "Разделы снабжения",
    tabs: procurementTabs,
    // «Обзор» — сама страница раздела и журнал сводок; остальные — по началу адреса.
    active: (pathname) =>
      pathname === "/procurement" || under(pathname, "/procurement/digests") ? "overview" : (procurementTabs.find((t) => t.key !== "overview" && under(pathname, t.href))?.key ?? null),
  },
];

const sectionBase: Record<SectionDef["id"], string> = { sales: "/sales", hr: "/hr", tasks: "/tasks", requests: "/requests", procurement: "/procurement" };

/** Раздел страницы: «Продажи», «HR», «Задачи», «Заявки» — или null (в остальных разделах кнопок в шапке нет). */
export const sectionOf = (pathname: string): SectionDef | null =>
  sections.find((s) => (s.id === "sales" ? pathname.startsWith("/sales") : under(pathname, sectionBase[s.id]))) ?? null;
