import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { requireAnalytik, periodParams, type SearchParams } from "@/components/analytik/guard";
import { DataGaps } from "@/components/analytik/ui";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import { SalesView } from "@/components/analytik/Departments";
import type { SalesBlock, Readiness } from "@/lib/analytik";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Аналитика продаж · OneBase") };
}

/** «Аналитик → Продажи». */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireAnalytik();
  const t = await getT();
  const sp = await searchParams;
  const data = await apiGet<SalesBlock>(`/api/analytik/sales?${periodParams(sp)}`, "/analytik/sales");
  // Данные периода загружены не полностью (обмен с источником отстаёт) — баннер с причиной, принятыми мерами и последним месяцем с данными.
  const readiness = await apiGet<Readiness>(`/api/analytik/readiness?${periodParams(sp)}&module=sales`, "/analytik/sales").catch(() => null);
  return (
    <>
      <PageHeader title={t("Продажи")} subtitle={t("Выручка, кг, АКБ и средний чек за период к прошлому, регионы, план и прогноз месяца — по правилам отчётов продаж")} breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Аналитик"), href: "/analytik" }, { label: t("Продажи") }]} />
      <PageBody>
        <div className="space-y-4">
          {readiness && <DataGaps gaps={readiness.gaps} />}
          <SalesView data={data} />
        </div>
      </PageBody>
    </>
  );
}
