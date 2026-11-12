import { notFound } from "next/navigation";
import { GroupPage } from "@/components/sales/GroupPage";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { getT } from "@/i18n/server";
import { apiGetOrNull } from "@/lib/server-api";
import { apiQuery, periodQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { GroupView } from "@/lib/sales/types";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Мои регионы · Продажи") };
}

/**
 * «Мои регионы» — своя страница РМ и супервайзера с несколькими регионами: все регионы охвата одним экраном (у РМ — и регионы его
 * направлений), те же блоки, что у направления. Вся организация и ТП — 404 (у них своя страница).
 */
export default async function MyRegionsPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const t = await getT();
  const sp = await searchParams;
  const data = await apiGetOrNull<GroupView>(`/api/sales/my${apiQuery(sp, ["from", "to", "metric", "category"])}`, "/sales/my");
  if (!data) notFound();
  const q = periodQuery(sp);
  const regions = data.cards.filter((c) => c.kind !== "direction").length;

  return (
    <SalesFrame title={t("Мои регионы")} subtitle={t("Регионов: {n}", { n: regions })} crumbs={[{ label: t("Мои регионы") }]} sp={sp} period={data.period}>
      <GroupPage data={data} sp={sp} query={q} categoryQuery={q} regionsTitle={t("Сравнение регионов")} scopeName={t("моих регионах")} republicWide />
    </SalesFrame>
  );
}
