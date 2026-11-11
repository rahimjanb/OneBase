import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { requireAnalytik, periodParams, type SearchParams } from "@/components/analytik/guard";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import { ProductionView } from "@/components/analytik/Departments";
import type { ProductionBlock } from "@/lib/analytik";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Аналитика производства · OneBase") };
}

/** «Аналитик → Производство». */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireAnalytik();
  const t = await getT();
  const sp = await searchParams;
  const data = await apiGet<ProductionBlock>(`/api/analytik/production?${periodParams(sp)}`, "/analytik/production");
  return (
    <>
      <PageHeader title={t("Производство")} subtitle={t("План и факт выпуска, расход против нормы, брак, рост цен сырья, сколько можно произвести из остатков")} breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Аналитик"), href: "/analytik" }, { label: t("Производство") }]} />
      <PageBody>
        <ProductionView data={data} />
      </PageBody>
    </>
  );
}
