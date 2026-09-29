"use client";

import Link from "next/link";
import { usePathname, useSearchParams } from "next/navigation";
import { BookOpen, Boxes, ChartColumn, Factory, LayoutGrid, Target, TriangleAlert } from "lucide-react";
import { SalesToolbar } from "./SalesToolbar";
import { SyncControls } from "./SyncButton";
import type { SalesMonth, SyncStatus } from "@/lib/sales/types";

type Tab = "analytics" | "assortment" | "primary" | "stock" | "plans" | "problems" | "method";

/** Разделы продаж — кнопки, каждая открывает свою страницу. Настройки продаж — в «Настройки». */
const tabs: { key: Tab; label: string; href: string; icon: typeof ChartColumn }[] = [
  { key: "analytics", label: "Вторичка", href: "/sales", icon: ChartColumn },
  { key: "assortment", label: "Ассортимент", href: "/sales/assortment", icon: LayoutGrid },
  { key: "primary", label: "Первичка", href: "/sales/primary", icon: Factory },
  { key: "stock", label: "Остатки", href: "/sales/stock", icon: Boxes },
  { key: "plans", label: "Планы", href: "/sales/plans", icon: Target },
  { key: "problems", label: "Проблемные агенты", href: "/sales/problems", icon: TriangleAlert },
  { key: "method", label: "Как считается", href: "/sales/method", icon: BookOpen },
];

function activeTab(pathname: string): Tab | null {
  for (const key of ["assortment", "primary", "stock", "plans", "problems", "method"] as const) {
    if (pathname.startsWith(`/sales/${key}`)) return key;
  }
  if (pathname.startsWith("/sales/setup")) return null;
  return "analytics";
}

/** На остатках и в описании период не нужен: остаток — снимок на момент загрузки. */
const withoutPeriod: (Tab | null)[] = [null, "stock", "method"];

/**
 * Панель раздела «Продажи»: разделы, период, план, свежесть данных и «Обновить».
 * Живёт в layout раздела — при переходах между страницами не перерисовывается и не пропадает.
 */
export function SalesNav({ months, status }: { months: SalesMonth[]; status: SyncStatus }) {
  const pathname = usePathname();
  const params = useSearchParams();
  const active = activeTab(pathname);

  // Период и план переносятся между разделами.
  const period = new URLSearchParams();
  for (const key of ["year", "month", "plan"]) {
    const value = params.get(key);
    if (value) period.set(key, value);
  }
  const query = period.toString();

  const year = Number(params.get("year") ?? months[0]?.year ?? new Date().getFullYear());
  const month = Number(params.get("month") ?? months[0]?.month ?? new Date().getMonth() + 1);

  return (
    <div className="flex flex-wrap items-center justify-between gap-3">
      <nav className="flex flex-wrap gap-2" aria-label="Разделы продаж">
        {tabs.map((t) => {
          const current = active === t.key;
          return (
            <Link
              key={t.key}
              href={query ? `${t.href}?${query}` : t.href}
              aria-current={current ? "page" : undefined}
              className={`inline-flex h-9 items-center gap-2 whitespace-nowrap rounded-lg border px-3 text-sm font-medium shadow-sm transition-colors ${
                current ? "border-accent bg-accent text-white hover:bg-accent-strong" : "border-line bg-surface text-ink hover:border-accent/40 hover:bg-muted"
              }`}
            >
              <t.icon className="size-4" />
              {t.label}
            </Link>
          );
        })}
      </nav>
      <div className="flex flex-wrap items-center gap-2">
        {!withoutPeriod.includes(active) && <SalesToolbar months={months} year={year} month={month} plan={params.get("plan") ?? "Rop"} />}
        <SyncControls initial={status} />
      </div>
    </div>
  );
}
