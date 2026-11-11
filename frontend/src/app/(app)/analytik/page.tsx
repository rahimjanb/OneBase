import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { requireAnalytik, periodParams, type SearchParams } from "@/components/analytik/guard";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import { Overview } from "@/components/analytik/Overview";
import type { Dashboard } from "@/lib/analytik";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Аналитик · OneBase") };
}

/** «Аналитик → Обзор»: Executive Intelligence Dashboard за период (без прогноза — он грузится на экране отдельно). */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const me = await requireAnalytik();
  const t = await getT();
  const sp = await searchParams;
  const data = await apiGet<Dashboard>(`/api/analytik/dashboard?${periodParams(sp)}`, "/analytik");
  return (
    <>
      <PageHeader title={t("Аналитик")} subtitle={t("OneBase Intelligence: состояние компании, риски, прогнозы и AI-аналитик по данным всех отделов")} breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Аналитик"), href: "/analytik" }, { label: t("Аналитик") }]} />
      <PageBody>
        <Overview data={data} canSales={!!me?.permissions.includes("sales.read")} />
      </PageBody>
    </>
  );
}
