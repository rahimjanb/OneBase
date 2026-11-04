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
  ScrollText,
  ShoppingBag,
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
  /** Короткое пояснение — в меню «Ещё» и в поиске разделов. */
  description?: string;
};

/**
 * Раздел с кнопками в верхней панели (как в «Продажах»): какие кнопки и какая из них открыта для адреса.
 * keepPeriod — переносить период и план между кнопками (только «Продажи»).
 */
export type SectionDef = {
  id: "sales" | "hr" | "tasks" | "requests" | "procurement";
  label: string;
  /** Название модуля в верхней панели: «Продажи», «HR»… */
  title: string;
  /** Кнопка «назад» слева от названия: «Продажи», «Снабжение» и HR открываются из «Отделов». */
  back?: { href: string; label: string };
  tabs: SectionTab[];
  active: (pathname: string) => string | null;
  keepPeriod?: boolean;
};

const toDepartments = { href: "/departments", label: "Назад к отделам" };

const under = (pathname: string, href: string) => pathname === href || pathname.startsWith(`${href}/`);

const hrTabs: SectionTab[] = [
  { key: "overview", label: "Обзор", href: "/hr", icon: LayoutGrid, description: "сводка дня и посещаемость" },
  { key: "employees", label: "Сотрудники", href: "/hr/employees", icon: Users, description: "список и карточки сотрудников" },
  { key: "attendance", label: "Посещаемость", href: "/hr/attendance", icon: DoorOpen, description: "приходы и уходы по дням" },
  { key: "deviations", label: "Опоздания и отсутствия", short: "Опоздания", href: "/hr/deviations", icon: CalendarClock, description: "кто опоздал или не пришёл" },
  { key: "timesheets", label: "Табели", href: "/hr/timesheets", icon: Table2, description: "рабочее время за период" },
  { key: "reports", label: "Отчёты", href: "/hr/reports", icon: ChartColumn, description: "отчёты по дисциплине" },
];

export const taskTabs: SectionTab[] = [
  { key: "overview", label: "Обзор", href: "/tasks", icon: LayoutGrid, description: "сводка по задачам" },
  { key: "all", label: "Все задачи", short: "Все", href: "/tasks/all", icon: ListChecks, description: "все задачи, которые вам видны" },
  { key: "board", label: "Канбан", href: "/tasks/board", icon: KanbanSquare, description: "задачи по статусам" },
  { key: "my", label: "Мои задачи", short: "Мои", href: "/tasks/my", icon: UserRound, description: "назначенные вам" },
  { key: "department", label: "Задачи отдела", short: "Отдел", href: "/tasks/department", icon: UsersRound, description: "задачи вашего отдела" },
  { key: "archive", label: "Архив", href: "/tasks/archive", icon: Archive, description: "выполненные и отменённые" },
  { key: "suggestions", label: "Предложения AI", short: "AI", href: "/tasks/suggestions", icon: Sparkles, headsOnly: true, description: "задачи, которые предлагает AI" },
  { key: "projects", label: "Проекты", href: "/tasks/projects", icon: FolderKanban, description: "группы связанных задач" },
];

/**
 * «Заявки» (снабжение): обзор, все заявки — снабжению и директору (procurement.read), мои, заявки отдела — руководителям отделов,
 * категории — тем, кто их настраивает. Задачи снабжения — в «Задачах» (отдел «Снабжение»).
 */
export const requestTabs: SectionTab[] = [
  { key: "overview", label: "Обзор", href: "/requests", icon: LayoutGrid, description: "сводка по заявкам" },
  { key: "all", label: "Все заявки", short: "Все", href: "/requests/all", icon: ListChecks, permission: "procurement.read", description: "заявки всех отделов" },
  { key: "my", label: "Мои заявки", short: "Мои", href: "/requests/my", icon: UserRound, description: "ваши заявки на закупку" },
  { key: "department", label: "Заявки отдела", short: "Отдел", href: "/requests/department", icon: UsersRound, headsOnly: true, description: "заявки сотрудников отдела" },
  { key: "approvals", label: "Согласования", href: "/requests/approvals", icon: Gavel, approverOnly: true, description: "ждут вашего решения" },
  { key: "categories", label: "Настройки", href: "/requests/categories", icon: Tags, permission: "procurement.settings", description: "категории закупок" },
];

/**
 * «Снабжение»: к закупке (согласованные заявки — снабжение покупает и отмечает «Куплено»), тендеры (журнал закупок сырья через тендер) и
 * поставщики (снабжение, директор —
 * procurement.read), склад (остатки и каталог), заявки к выдаче, журнал движений, отчёты. Заказы поставщикам и приёмка — прежний путь:
 * в навигации их нет, старые заказы открываются по ссылкам.
 */
export const procurementTabs: SectionTab[] = [
  { key: "overview", label: "Обзор", href: "/procurement", icon: LayoutDashboard, description: "путь закупки и показатели" },
  { key: "buy", label: "К закупке", href: "/procurement/buy", icon: ShoppingBag, permission: "procurement.read", description: "согласованные заявки" },
  { key: "tenders", label: "Тендеры", href: "/procurement/tenders", icon: ScrollText, permission: "procurement.read", description: "журнал тендерных закупок" },
  { key: "suppliers", label: "Поставщики", href: "/procurement/suppliers", icon: Building, permission: "procurement.read", description: "реестр и рейтинг" },
  { key: "stock", label: "Склад", href: "/procurement/stock", icon: Warehouse, description: "остатки и каталог" },
  { key: "issue", label: "К выдаче", href: "/procurement/issue", icon: PackageCheck, description: "выдача со склада по заявкам" },
  { key: "movements", label: "Движения", href: "/procurement/movements", icon: ArrowLeftRight, description: "журнал прихода и расхода" },
  { key: "reports", label: "Отчёты", href: "/procurement/reports", icon: FileBarChart, permission: "procurement.read", description: "выгрузки по снабжению" },
];

export const sections: SectionDef[] = [
  { id: "sales", label: "Разделы продаж", title: "Продажи", back: toDepartments, tabs: salesTabs, active: activeSalesTab, keepPeriod: true },
  {
    id: "hr",
    label: "Разделы HR",
    title: "HR",
    back: toDepartments,
    tabs: hrTabs,
    // Карточка сотрудника — «Сотрудники».
    active: (pathname) => (pathname === "/hr" ? "overview" : hrTabs.find((t) => t.key !== "overview" && under(pathname, t.href))?.key ?? null),
  },
  {
    id: "tasks",
    label: "Разделы задач",
    title: "Задачи",
    tabs: taskTabs,
    // Карточка задачи — «Все задачи» (ModuleNav подсвечивает список, откуда её открыли); страница отдела (/tasks/{код}) — «Задачи отдела».
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
    title: "Заявки",
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
    title: "Снабжение",
    back: toDepartments,
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
