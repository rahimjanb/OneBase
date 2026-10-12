import { Activity, Boxes, CalendarDays, Layers, LayoutGrid, Package, TrendingUp, TriangleAlert, Wallet } from "lucide-react";

export type SalesTab = "analytics" | "assortment" | "skuSales" | "primary" | "stock" | "outstock" | "plans" | "problems" | "pnl" | "method";

/**
 * Разделы продаж — кнопки в верхней панели, каждая открывает свою страницу. Настройки продаж — в «Настройки».
 * permission — кнопка видна только с этим правом (право на сами «Продажи» проверяет layout раздела).
 */
export const salesTabs: { key: SalesTab; label: string; href: string; icon: typeof TrendingUp; permission?: string }[] = [
  { key: "analytics", label: "Вторичка", href: "/sales", icon: TrendingUp },
  { key: "assortment", label: "Ассортимент", href: "/sales/assortment", icon: LayoutGrid },
  { key: "skuSales", label: "Продажи по SKU", href: "/sales/sku-sales", icon: Package },
  { key: "primary", label: "Первичка", href: "/sales/primary", icon: Boxes },
  { key: "stock", label: "Рек. остаток", href: "/sales/stock", icon: Layers },
  { key: "outstock", label: "Аутсток", href: "/sales/outstock", icon: Activity },
  { key: "plans", label: "Планы", href: "/sales/plans", icon: CalendarDays },
  { key: "problems", label: "Проблемные агенты", href: "/sales/problems", icon: TriangleAlert },
  { key: "pnl", label: "P&L дилеров", href: "/sales/pnl", icon: Wallet, permission: "finance.read" },
  // «Как считается» (/sales/method) без кнопки: страница открывается по адресу.
];

/**
 * Страницы с планом подразделений — старт вторички, республика, РМ, регион: на них переключатель «План РОП / План «Завод»».
 * У ТП план свой (из Linko), на остальных страницах плана нет.
 */
export function usesPlan(pathname: string): boolean {
  return pathname === "/sales" || pathname === "/sales/republic" || pathname.startsWith("/sales/directions/") || pathname.startsWith("/sales/regions/");
}

/** Раздел, к которому относится страница: регион, агент, магазин, экспорт — это «Вторичка», категория и артикул — «Ассортимент». */
export function activeSalesTab(pathname: string): SalesTab | null {
  if (!pathname.startsWith("/sales")) return null;
  if (pathname.startsWith("/sales/sku-sales")) return "skuSales";
  for (const key of ["assortment", "primary", "stock", "outstock", "plans", "problems", "pnl", "method"] as const) {
    if (pathname.startsWith(`/sales/${key}`)) return key;
  }
  if (pathname.startsWith("/sales/setup")) return null;
  return "analytics";
}
