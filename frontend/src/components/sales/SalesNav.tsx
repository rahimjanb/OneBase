"use client";

import { usePathname, useSearchParams } from "next/navigation";
import { SalesToolbar } from "./SalesToolbar";
import { SyncControls } from "./SyncButton";
import { activeSalesTab, type SalesTab } from "./tabs";
import type { SalesMonth, SyncStatus } from "@/lib/sales/types";

/** На остатках и в описании период не нужен: остаток — снимок на момент загрузки. */
const withoutPeriod: (SalesTab | null)[] = [null, "stock", "method"];

/**
 * Полоса раздела «Продажи» под верхней панелью: период, свежесть данных и «Обновить».
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
        />
      ) : (
        <span />
      )}
      <SyncControls initial={status} />
    </div>
  );
}
