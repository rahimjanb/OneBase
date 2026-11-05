"use client";

import { usePathname, useSearchParams } from "next/navigation";
import { PrimaryStrip, PrimaryStripActions } from "./primary/PrimaryFilters";
import { SalesToolbar } from "./SalesToolbar";
import { SyncControls } from "./SyncButton";
import { activeSalesTab, usesPlan, type SalesTab } from "./tabs";
import { planOf } from "@/lib/sales/query";
import type { SalesMonth, SyncStatus } from "@/lib/sales/types";

/**
 * На остатках и в описании период не нужен: остаток — снимок на момент загрузки (истории остатков Linko не отдаёт).
 * Свой выбор периода: у «Аутстока» — идущий месяц и шесть закрытых (по умолчанию последний закрытый), у «Продаж по SKU» — с … по … месяцам,
 * у «P&L дилеров» — месяцы загруженного P&L, у «Акций» — месяцы года. «ДМП» — на дату данных.
 */
const withoutPeriod: (SalesTab | null)[] = [null, "stock", "outstock", "method", "skuSales", "pnl", "promo", "akt", "dso", "dmp"];

/**
 * Полоса раздела «Продажи» под верхней панелью: период, план (на страницах с планом подразделений), свежесть данных и «Обновить».
 * Кнопки разделов — в верхней панели (ModuleNav). Живёт в layout раздела — при переходах не перерисовывается.
 */
export function SalesNav({ months, status }: { months: SalesMonth[]; status: SyncStatus }) {
  const pathname = usePathname();
  const params = useSearchParams();
  const active = activeSalesTab(pathname);

  const year = Number(params.get("year") ?? months[0]?.year ?? new Date().getFullYear());
  const month = Number(params.get("month") ?? months[0]?.month ?? new Date().getMonth() + 1);
  // Обзор «Первички» — свой период (месяц, квартал, с начала года), единицы и «Excel»; на телефоне фильтры — в самой странице.
  const primaryOverview = pathname === "/sales/primary";
  const through = status.dataAsOf ? status.dataAsOf.slice(0, 10) : null;

  return (
    <div className="flex flex-wrap items-center justify-between gap-3">
      {primaryOverview ? (
        <PrimaryStrip years={[...new Set(months.map((m) => m.year))]} through={through} />
      ) : !withoutPeriod.includes(active) ? (
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
      <div className="flex items-center gap-2">
        {primaryOverview && <PrimaryStripActions through={through} />}
        <SyncControls initial={status} />
      </div>
    </div>
  );
}
