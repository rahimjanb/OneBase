import { Boxes, ChartColumn, Factory, LayoutGrid, PackageX, Target, TriangleAlert } from "lucide-react";

export type SalesTab = "analytics" | "assortment" | "primary" | "stock" | "outstock" | "plans" | "problems" | "method";

/** Разделы продаж — кнопки в верхней панели, каждая открывает свою страницу. Настройки продаж — в «Настройки». */
export const salesTabs: { key: SalesTab; label: string; href: string; icon: typeof ChartColumn }[] = [
  { key: "analytics", label: "Вторичка", href: "/sales", icon: ChartColumn },
  { key: "assortment", label: "Ассортимент", href: "/sales/assortment", icon: LayoutGrid },
  { key: "primary", label: "Первичка", href: "/sales/primary", icon: Factory },
  { key: "stock", label: "Рек. остаток", href: "/sales/stock", icon: Boxes },
  { key: "outstock", label: "Аутсток", href: "/sales/outstock", icon: PackageX },
  { key: "plans", label: "Планы", href: "/sales/plans", icon: Target },
  { key: "problems", label: "Проблемные агенты", href: "/sales/problems", icon: TriangleAlert },
  // «Как считается» (/sales/method) без кнопки: страница открывается по адресу.
];

/** Раздел, к которому относится страница: регион, агент, магазин, экспорт — это «Вторичка», категория и артикул — «Ассортимент». */
export function activeSalesTab(pathname: string): SalesTab | null {
  if (!pathname.startsWith("/sales")) return null;
  for (const key of ["assortment", "primary", "stock", "outstock", "plans", "problems", "method"] as const) {
    if (pathname.startsWith(`/sales/${key}`)) return key;
  }
  if (pathname.startsWith("/sales/setup")) return null;
  return "analytics";
}
