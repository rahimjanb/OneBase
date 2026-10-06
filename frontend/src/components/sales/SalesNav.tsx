"use client";

import { usePathname, useSearchParams } from "next/navigation";
import { SalesToolbar } from "./SalesToolbar";
import { SyncControls } from "./SyncButton";
import { activeSalesTab, usesPlan, type SalesTab } from "./tabs";
import { planOf } from "@/lib/sales/query";
import type { SalesMonth, SyncStatus } from "@/lib/sales/types";

/**
 * На остатках и в описании период не нужен: остаток — снимок на момент загрузки; аутсток — всегда последний закрытый месяц (DOC-filters §0, §5).
 * У «Продаж по SKU» свой период — с … по … месяцам.
 */
const withoutPeriod: (SalesTab | null)[] = [null, "stock", "outstock", "method", "skuSales"];

/**
 * Полоса раздела «Продажи» под верхней панелью: период, план (на страницах с планом подразделений), свежесть данных и «Обновить».
 * Кнопки разделов — в верхней панели (SectionNav). Живёт в layout раздела — при переходах не перерисовывается.
 */
export function SalesNav({ months, status }: { months: SalesMonth[]; status: SyncStatus }) {
  const pathname = usePathname();
  const params = useSearchParams();
  const active = activeSalesTab(pathname);

  const year = Number(params.get("year") ?? months[0]?.year ?? new Date().getFullYear());
  const month = Number(params.get("month") ?? months[0]?.month ?? new Date().getMonth() + 1);

  return (
    <div className="flex flex-wrap items-center justify-between gap-3">
      {!withoutPeriod.includes(active) ? (
        <SalesToolbar
          months={months}
          year={year}
          month={month}
          plan={planOf(params.get("plan"))}
          showPlan={usesPlan(pathname)}
        />
      ) : (
        <span />
      )}
      <SyncControls initial={status} />
    </div>
  );
}
