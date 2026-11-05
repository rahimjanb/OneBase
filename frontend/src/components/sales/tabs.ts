import { Activity, Archive, Award, BadgePercent, Box, Boxes, CalendarRange, Clock3, Contact, FileCheck, Gauge, Layers, LayoutGrid, TrendingUp, TriangleAlert, Wallet } from "lucide-react";

export type SalesTab = "analytics" | "assortment" | "skuSales" | "primary" | "stock" | "outstock" | "plans" | "kpi" | "promo" | "problems" | "pnl" | "akt" | "dso" | "dmp" | "system" | "agents" | "method";

/**
 * Разделы продаж — кнопки в верхней панели, каждая открывает свою страницу. Настройки продаж — в «Настройки».
 * permission — кнопка видна только с этим правом (право на сами «Продажи» проверяет layout раздела).
 */
export const salesTabs: { key: SalesTab; label: string; description: string; href: string; icon: typeof TrendingUp; permission?: string }[] = [
  { key: "analytics", label: "Вторичка", description: "план, факт и работа ТП", href: "/sales", icon: TrendingUp },
  { key: "agents", label: "Агенты", description: "карточки и прогресс ТП", href: "/sales/agents", icon: Contact },
  { key: "assortment", label: "Ассортимент", description: "матрица и покрытие SKU", href: "/sales/assortment", icon: LayoutGrid },
  { key: "skuSales", label: "Продажи по SKU", description: "динамика по товарам", href: "/sales/sku-sales", icon: Box },
  { key: "primary", label: "Первичка", description: "отгрузки дистрибьюторам", href: "/sales/primary", icon: Boxes },
  { key: "stock", label: "Рек. остаток", description: "рекомендуемые остатки", href: "/sales/stock", icon: Layers },
  { key: "outstock", label: "Аутсток", description: "нехватка товара на точках", href: "/sales/outstock", icon: Activity },
  { key: "plans", label: "Планы", description: "планы РОП и «Завод»", href: "/sales/plans", icon: CalendarRange },
  { key: "promo", label: "Акции", description: "промо и скидки", href: "/sales/promo", icon: BadgePercent },
  { key: "dmp", label: "ДМП", description: "стеллажи в магазинах", href: "/sales/dmp", icon: Archive },
  { key: "kpi", label: "KPI", description: "показатели агентов", href: "/sales/kpi", icon: Award },
  { key: "problems", label: "Проблемные агенты", description: "отклонения от плана", href: "/sales/problems", icon: TriangleAlert },
  { key: "system", label: "Система", description: "маршрут, дистрибуция и деньги", href: "/sales/system", icon: Gauge },
  { key: "pnl", label: "P&L дилеров", description: "прибыль и убытки", href: "/sales/pnl", icon: Wallet, permission: "finance.read" },
  { key: "akt", label: "Акт сверки", description: "взаиморасчёты", href: "/sales/akt", icon: FileCheck, permission: "finance.read" },
  { key: "dso", label: "DSO", description: "оборачиваемость дебиторки", href: "/sales/dso", icon: Clock3, permission: "finance.read" },
  // «Как считается» (/sales/method) без кнопки: страница открывается по адресу.
];

/**
 * Страницы с планом подразделений — старт вторички, республика, РМ, регион: на них переключатель «План РОП / План «Завод»».
 * У ТП план свой (из Linko), на остальных страницах плана нет.
 */
export function usesPlan(pathname: string): boolean {
  return pathname === "/sales" || pathname === "/sales/republic" || pathname.startsWith("/sales/directions/") || pathname.startsWith("/sales/regions/");
}

/** Раздел, к которому относится страница: агент — «Агенты»; регион, магазин, экспорт — «Вторичка», категория и артикул — «Ассортимент». */
export function activeSalesTab(pathname: string): SalesTab | null {
  if (!pathname.startsWith("/sales")) return null;
  if (pathname.startsWith("/sales/sku-sales")) return "skuSales";
  for (const key of ["assortment", "primary", "stock", "outstock", "plans", "kpi", "promo", "problems", "pnl", "akt", "dso", "dmp", "system", "agents", "method"] as const) {
    if (pathname.startsWith(`/sales/${key}`)) return key;
  }
  if (pathname.startsWith("/sales/setup")) return null;
  return "analytics";
}
