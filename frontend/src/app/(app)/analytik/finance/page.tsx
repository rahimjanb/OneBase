import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { requireAnalytik, periodParams, type SearchParams } from "@/components/analytik/guard";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import { FinanceView } from "@/components/analytik/Departments";
import type { Block, Period } from "@/lib/analytik";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Финансовая аналитика · OneBase") };
}

/** «Аналитик → Финансы». */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireAnalytik();
  const t = await getT();
  const sp = await searchParams;
  const data = await apiGet<Block & { period: Period; payments: { type: string; amount: number; count: number; previousAmount: number | null }[] }>(`/api/analytik/finance?${periodParams(sp)}`, "/analytik/finance");
  return (
    <>
      <PageHeader title={t("Финансы")} subtitle={t("Оплаты точек, оплаты к выручке, расходы на закупки — и каких финансовых данных в OneBase нет")} breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Аналитик"), href: "/analytik" }, { label: t("Финансы") }]} />
      <PageBody>
        <FinanceView data={data} />
      </PageBody>
    </>
  );
}
