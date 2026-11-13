import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { one, requireAnalytik, type SearchParams } from "@/components/analytik/guard";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import { RisksView } from "@/components/analytik/Insights";
import type { Insight } from "@/lib/analytik";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Риски · OneBase") };
}

/** «Аналитик → Риски»; ?risk=… — находка из «Обзора»: раскрыта и прокручена к себе. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireAnalytik();
  const t = await getT();
  const focus = one(await searchParams, "risk") ?? null;
  const [active, resolved] = await Promise.all([
    apiGet<Insight[]>("/api/analytik/risks", "/analytik/risks"),
    apiGet<Insight[]>("/api/analytik/risks?resolved=true", "/analytik/risks"),
  ]);
  return (
    <>
      <PageHeader title={t("Риски и отклонения")} subtitle={t("Что требует внимания: риски, отклонения, качество данных и возможности — с причинами, гипотезами и рекомендациями")} breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Аналитик"), href: "/analytik" }, { label: t("Риски и отклонения") }]} />
      <PageBody>
        <RisksView active={active} resolved={resolved} focus={focus} />
      </PageBody>
    </>
  );
}
