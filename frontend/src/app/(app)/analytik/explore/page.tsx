import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { one, requireAnalytik, type SearchParams } from "@/components/analytik/guard";
import { ExploreView } from "@/components/analytik/explore/ExploreView";
import { getT } from "@/i18n/server";
import { decodeQuery, type CapabilityView } from "@/lib/explore";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Срезы данных · OneBase") };
}

/**
 * «Аналитик → Срезы»: универсальный движок (те же расчёты, что у AI-аналитика). ?q — срез из ссылки ответа AI или из адреса (base64url JSON).
 * Права и область продаж проверяет сервер при каждом запросе.
 */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireAnalytik();
  const t = await getT();
  const capabilities = await apiGet<CapabilityView>("/api/analytik/explore/capabilities", "/analytik/explore");
  const initial = decodeQuery(one(await searchParams, "q"));
  return (
    <>
      <PageHeader
        title={t("Срезы данных")}
        subtitle={t("Любой показатель по любым измерениям: продажи, план, оплаты, остатки — по дням, регионам, товарным группам, агентам и точкам, с проверкой полноты и источником каждой цифры")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Аналитик"), href: "/analytik" }, { label: t("Срезы") }]}
      />
      <PageBody>
        <ExploreView capabilities={capabilities} initial={initial} />
      </PageBody>
    </>
  );
}
