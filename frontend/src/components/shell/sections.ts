import {
  Archive,
  ArrowLeftRight,
  ChartLine,
  History,
  MessageSquareText,
  Network,
  TrendingUp,
  TriangleAlert,
  Wallet,
  FlaskConical,
  SlidersHorizontal,
  ClipboardList,
  Building,
  Factory,
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
  ShieldCheck,
  ShoppingBag,
  Sparkles,
  Table2,
  Truck,
  Tags,
  UserPlus,
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
  /** Право кнопки; список — достаточно любого из них. */
  permission?: string | string[];
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
  id: "sales" | "hr" | "tasks" | "requests" | "procurement" | "production" | "analytik";
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

// Данные Verifix — тем, у кого есть доступ к HR (hr.department — признак из /api/auth/me); заявки на сотрудника — руководителям отделов и HR
// (hr.requests); подбор — по праву «Подбор». «Заявки» раньше «Подбора»: путь /hr/recruitment/requests — вкладка заявок.
const hrData = ["hr.department"];
const hrTabs: SectionTab[] = [
  { key: "overview", label: "Обзор", href: "/hr", icon: LayoutGrid, permission: hrData, description: "сводка дня и посещаемость" },
  { key: "employees", label: "Сотрудники", href: "/hr/employees", icon: Users, permission: hrData, description: "список и карточки сотрудников" },
  { key: "attendance", label: "Посещаемость", href: "/hr/attendance", icon: DoorOpen, permission: hrData, description: "приходы и уходы по дням" },
  { key: "deviations", label: "Опоздания и отсутствия", short: "Опоздания", href: "/hr/deviations", icon: CalendarClock, permission: hrData, description: "кто опоздал или не пришёл" },
  { key: "timesheets", label: "Табели", href: "/hr/timesheets", icon: Table2, permission: hrData, description: "рабочее время за период" },
  { key: "reports", label: "Отчёты", href: "/hr/reports", icon: ChartColumn, permission: hrData, description: "отчёты по дисциплине" },
  { key: "requests", label: "Заявки на сотрудника", short: "Заявки", href: "/hr/recruitment/requests", icon: ClipboardList, permission: ["hr.requests"], description: "отделы просят новых сотрудников" },
  { key: "recruitment", label: "Подбор", href: "/hr/recruitment", icon: UserPlus, permission: ["hr.recruitment.read", "hr.recruitment.manage"], description: "вакансии, кандидаты, Telegram, HH, собеседования" },
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
  // Тендеры видит и кладовщик: принимает поставку по тендеру на склад.
  {
    key: "tenders",
    label: "Тендеры",
    href: "/procurement/tenders",
    icon: ScrollText,
    permission: ["procurement.read", "procurement.warehouse"],
    description: "журнал тендерных закупок",
  },
  // Поставки — приезд машин по тендерам: кладовщик отмечает и принимает, лаборатория проверяет, снабжение и аналитик следят.
  {
    key: "arrivals",
    label: "Поставки",
    href: "/procurement/arrivals",
    icon: Truck,
    permission: ["procurement.read", "procurement.warehouse", "quality.check"],
    description: "машины: качество, взвешивание, приёмка",
  },
  { key: "suppliers", label: "Поставщики", href: "/procurement/suppliers", icon: Building, permission: "procurement.read", description: "реестр и рейтинг" },
  { key: "stock", label: "Склад", href: "/procurement/stock", icon: Warehouse, description: "остатки и каталог" },
  { key: "batches", label: "Партии", href: "/procurement/batches", icon: CalendarClock, description: "сроки годности и остатки партий" },
  { key: "issue", label: "К выдаче", href: "/procurement/issue", icon: PackageCheck, description: "выдача со склада по заявкам" },
  { key: "movements", label: "Движения", href: "/procurement/movements", icon: ArrowLeftRight, description: "журнал прихода и расхода" },
  { key: "reports", label: "Отчёты", href: "/procurement/reports", icon: FileBarChart, permission: "procurement.read", description: "выгрузки по снабжению" },
];

/** Кто видит производство (рецептуры, задания, аналитику): производство, технолог, кладовщик, снабжение, директор, лаборатория. */
const productionViewers = ["production.read", "production.manage", "production.recipes", "production.operate", "procurement.warehouse", "procurement.read", "procurement.manage", "quality.check"];

/**
 * «Производство» (D5): обзор сырья и производства, задания на выпуск (выдача и возврат сырья по партиям, закрытие со сверкой нормы),
 * рецептуры с версиями, контроль качества сырья, отчёты.
 */
export const productionTabs: SectionTab[] = [
  { key: "overview", label: "Обзор", href: "/production", icon: LayoutDashboard, permission: productionViewers, description: "сырьё и производство на сегодня" },
  { key: "orders", label: "Задания", href: "/production/orders", icon: Factory, permission: productionViewers, description: "задания на выпуск и расход сырья" },
  { key: "recipes", label: "Технолог", href: "/production/technologist", icon: FlaskConical, permission: productionViewers, description: "техкарты, этапы, брак и расчёт сырья" },
  { key: "shifts", label: "Смены", href: "/production/shifts", icon: ClipboardList, permission: productionViewers, description: "сменные отчёты: замесы, выпечка, брак, упаковка" },
  { key: "quality", label: "Качество", href: "/production/quality", icon: ShieldCheck, description: "контроль качества партий сырья" },
  { key: "reports", label: "Отчёты", href: "/production/reports", icon: FileBarChart, permission: productionViewers, description: "партии, качество, расход и норма" },
];

/**
 * «Аналитик» (OneBase Intelligence): обзор и AI-аналитик — всем с правом раздела; разделы отделов — тем, кто видит данные отдела (без права
 * раздел пустой: данные решает backend); «Карта данных», прогнозы, риски, отчёты и история — всем с правом раздела.
 */
export const analytikTabs: SectionTab[] = [
  { key: "overview", label: "Обзор", href: "/analytik", icon: LayoutDashboard, description: "состояние компании, показатели, риски и рекомендации" },
  { key: "chat", label: "AI-аналитик", short: "AI", href: "/analytik/chat", icon: MessageSquareText, description: "вопросы о любых данных OneBase на обычном языке" },
  { key: "explore", label: "Срезы", href: "/analytik/explore", icon: Table2, description: "любой показатель по дням, регионам, товарным группам, агентам и точкам" },
  { key: "sales", label: "Продажи", href: "/analytik/sales", icon: ChartLine, permission: "sales.read", description: "выручка, план и прогноз, регионы" },
  { key: "production", label: "Производство", href: "/analytik/production", icon: Factory, permission: ["production.read", "production.manage", "procurement.read", "procurement.manage"], description: "план и факт, расход и норма, сколько можно произвести" },
  { key: "warehouse", label: "Склад", href: "/analytik/warehouse", icon: Warehouse, permission: ["procurement.read", "procurement.manage", "procurement.warehouse", "production.read", "production.manage"], description: "остатки сырья, расход, дни запаса, дефицит" },
  { key: "procurement", label: "Снабжение", href: "/analytik/procurement", icon: Truck, permission: ["procurement.read", "procurement.manage", "procurement.warehouse", "production.read", "production.manage"], description: "что, сколько и когда закупить" },
  { key: "finance", label: "Финансы", href: "/analytik/finance", icon: Wallet, permission: "finance.read", description: "оплаты, расходы на закупки" },
  { key: "hr", label: "Персонал", href: "/analytik/hr", icon: Users, permission: ["hr.read", "analytik.manage"], description: "посещаемость, отклонения, задачи отделов" },
  { key: "forecasts", label: "Прогнозы", href: "/analytik/forecasts", icon: TrendingUp, description: "прогнозы со сценариями и точностью" },
  { key: "simulator", label: "Симулятор", href: "/analytik/simulator", icon: SlidersHorizontal, description: "сценарии «что если»: продажи, производство, сырьё, закупки, деньги" },
  { key: "risks", label: "Риски", href: "/analytik/risks", icon: TriangleAlert, description: "риски, отклонения, качество данных" },
  { key: "reports", label: "Отчёты", href: "/analytik/reports", icon: FileBarChart, description: "управленческие отчёты Excel и PDF" },
  { key: "data", label: "Карта данных", short: "Данные", href: "/analytik/data", icon: Network, description: "каталог всей базы: таблицы, связи, смысл" },
  { key: "history", label: "История", href: "/analytik/history", icon: History, description: "исследования и журнал запросов" },
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
  {
    id: "production",
    label: "Разделы производства",
    title: "Производство",
    back: toDepartments,
    tabs: productionTabs,
    // Техкарты (/production/recipes) — внутри «Технолога».
    active: (pathname) =>
      pathname === "/production" ? "overview" : under(pathname, "/production/recipes") ? "recipes" : (productionTabs.find((t) => t.key !== "overview" && under(pathname, t.href))?.key ?? null),
  },
  {
    id: "analytik",
    label: "Разделы Аналитика",
    title: "Аналитик",
    tabs: analytikTabs,
    // Чат (и его история), карточка таблицы и отчёта — внутри своих вкладок.
    active: (pathname) => (pathname === "/analytik" ? "overview" : (analytikTabs.find((t) => t.key !== "overview" && under(pathname, t.href))?.key ?? null)),
  },
];

const sectionBase: Record<SectionDef["id"], string> = {
  analytik: "/analytik",
  sales: "/sales",
  hr: "/hr",
  tasks: "/tasks",
  requests: "/requests",
  procurement: "/procurement",
  production: "/production",
};

/** Раздел страницы: «Продажи», «HR», «Задачи», «Заявки», «Снабжение», «Производство» — или null (в остальных разделах кнопок в шапке нет). */
export const sectionOf = (pathname: string): SectionDef | null =>
  sections.find((s) => (s.id === "sales" ? pathname.startsWith("/sales") : under(pathname, sectionBase[s.id]))) ?? null;
