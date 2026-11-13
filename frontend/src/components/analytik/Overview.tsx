"use client";

import { useT } from "@/i18n/client";
import { analytikQuestions, num, type Dashboard } from "@/lib/analytik";
import { canOpen } from "@/lib/navigation";
import { HealthCard, KpiPanel } from "./overview/state";
import { RevenueCard } from "./overview/revenue";
import { PlanCard, PurchaseCard, RecommendationsCard, RisksCard, UrgentPurchase } from "./overview/lists";
import { AskBar, MobileHeader, PeriodBar } from "./overview/top";
import { DataGaps } from "./ui";

/**
 * Вопросы «Часто спрашивают» — по текущим отклонениям: падение или рост выручки, что закупить, регион с самым большим падением, опоздания;
 * не хватает — общие вопросы по правам пользователя. Не больше четырёх.
 */
function askChips(d: Dashboard, permissions: readonly string[], t: (s: string, p?: Record<string, string | number>) => string): string[] {
  const out: string[] = [];
  const revenue = d.sales?.kpis.find((k) => k.code === "sales.revenue")?.changePercent;
  if (revenue != null && Math.abs(revenue) >= 0.5) {
    const p = `${num(Math.abs(revenue), Math.abs(revenue) < 10 ? 1 : 0)}%`;
    out.push(revenue < 0 ? t("Почему выручка упала на {p}?", { p }) : t("Почему выручка выросла на {p}?", { p }));
  }
  if (d.supply) out.push(t("Что закупить на неделю?"));
  const region = (d.sales?.regions ?? []).filter((r) => r.changePercent != null && r.changePercent <= -10).sort((a, b) => (a.changePercent ?? 0) - (b.changePercent ?? 0))[0];
  if (region) out.push(t("Почему {region} −{p}?", { region: region.name, p: `${num(Math.abs(region.changePercent!), 0)}%` }));
  if ((d.hr?.kpis.find((k) => k.code === "hr.late")?.value ?? 0) > 0) out.push(t("Кто чаще опаздывает?"));
  for (const q of analytikQuestions(permissions)) {
    if (out.length >= 4) break;
    out.push(t(q.text));
  }
  return [...new Set(out)].slice(0, 4);
}

/**
 * «Аналитик → Обзор» по макету (Laptop oy7PN, Mobile PIFPr): период, вопрос AI-аналитику, состояние компании и 12 показателей, выручка
 * с прогнозом и риски, план продаж, что закупить и рекомендации. Раскладка — по ширине области содержимого (container queries: слева меню
 * приложения, макет нарисован без него): от 1340 px — как макет 1440 (колонки 340 / 460); 1100–1339 — как 1280 (300 / 400); уже — как
 * 1024–1279: состояние над показателями, график и риски друг под другом, внизу 2 + 1. Планшет (768–1023) — одна колонка, показатели в 2
 * колонки; телефон — своя шапка и порядок макета.
 */
export function Overview({ data, permissions }: { data: Dashboard; permissions: readonly string[] }) {
  const t = useT();
  const canSales = permissions.includes("sales.read");
  const canRequest = canOpen("/requests", [...permissions]);
  const canTasks = permissions.includes("work.tasks");
  const chips = askChips(data, permissions, t);
  const bottom = [!!data.sales?.plan, !!data.supply, true].filter(Boolean).length;
  return (
    <div className="@container space-y-4 max-md:space-y-3.5">
      <MobileHeader at={data.at} freshness={data.freshness} permissions={permissions} />

      <div className="flex flex-wrap items-center gap-3">
        <PeriodBar period={data.period} />
        {data.scope && <span className="rounded-lg bg-accent-soft px-2.5 py-1 text-[12px] text-accent-strong">{t("Ваша область продаж")}: {data.scope}</span>}
      </div>

      <DataGaps gaps={data.gaps ?? []} />

      <AskBar chips={chips} periodLabel={data.period.label} />

      <div className="grid gap-4 max-md:gap-3.5 @min-[1100px]:grid-cols-[300px_minmax(0,1fr)] @min-[1340px]:grid-cols-[340px_minmax(0,1fr)]">
        <HealthCard data={data} />
        <KpiPanel data={data} />
      </div>

      <div className={`grid gap-4 max-md:gap-3.5 ${canSales ? "@min-[1100px]:grid-cols-[minmax(0,1fr)_400px] @min-[1340px]:grid-cols-[minmax(0,1fr)_460px]" : ""}`}>
        {canSales && <RevenueCard data={data} />}
        <RisksCard risks={data.risks} counts={data.riskCounts ?? null} at={data.at} />
      </div>

      <div className="md:hidden">
        <UrgentPurchase data={data} canRequest={canRequest} />
      </div>

      <div className={`grid gap-4 max-md:gap-3.5 ${bottom >= 2 ? "lg:grid-cols-2" : ""} ${bottom === 3 ? "@min-[1100px]:grid-cols-3" : ""}`}>
        {data.sales?.plan && (
          <div className="max-md:hidden">
            <PlanCard data={data} />
          </div>
        )}
        {data.supply && (
          <div className="max-md:hidden">
            <PurchaseCard data={data} canRequest={canRequest} />
          </div>
        )}
        <div className={bottom === 3 ? "lg:@max-[1099px]:col-span-2" : undefined}>
          <RecommendationsCard data={data} canRequest={canRequest} canTasks={canTasks} />
        </div>
      </div>
    </div>
  );
}
