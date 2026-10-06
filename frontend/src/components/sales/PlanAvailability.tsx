"use client";

import { createContext, useContext, useEffect, useMemo, useState } from "react";
import type { PlanKind } from "@/lib/sales/types";

/** Планы регионов, заведённые на месяц (ключ — «год-месяц»). */
type Known = { month: string; plans: PlanKind[] };

const PlansContext = createContext<{ known: Known | null; report: (known: Known) => void } | null>(null);

/**
 * Какие планы регионов заведены на месяц открытой страницы. Переключатель плана — в полосе раздела (layout), а знает это ответ страницы
 * (period.availablePlans): страница сообщает (ReportPlans в SalesFrame), переключатель читает (useAvailablePlans).
 */
export function PlanAvailabilityProvider({ children }: { children: React.ReactNode }) {
  const [known, setKnown] = useState<Known | null>(null);
  const value = useMemo(() => ({ known, report: setKnown }), [known]);
  return <PlansContext.Provider value={value}>{children}</PlansContext.Provider>;
}

/** Страница сообщает планы своего месяца; ничего не рисует. plans нет (старый ответ API) — ничего не сообщает. */
export function ReportPlans({ year, month, plans }: { year: number; month: number; plans: PlanKind[] | undefined }) {
  const report = useContext(PlansContext)?.report;
  const key = `${year}-${month}`;
  const list = plans ? plans.join(",") : null;
  useEffect(() => {
    if (list != null) report?.({ month: key, plans: list ? (list.split(",") as PlanKind[]) : [] });
  }, [report, key, list]);
  return null;
}

/** Планы месяца, если их уже сообщила страница этого месяца; null — пока неизвестно (переключатель ничего не запрещает). */
export function useAvailablePlans(year: number, month: number): PlanKind[] | null {
  const known = useContext(PlansContext)?.known;
  return known && known.month === `${year}-${month}` ? known.plans : null;
}
