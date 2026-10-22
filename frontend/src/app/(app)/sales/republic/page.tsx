import { GroupPage } from "@/components/sales/GroupPage";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import { apiQuery, periodQuery, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { GroupView } from "@/lib/sales/types";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Республика · Продажи") };
}

export default async function RepublicPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const t = await getT();
  const sp = await searchParams;
  const data = await apiGet<GroupView>(`/api/sales/republic${apiQuery(sp, ["from", "to", "metric", "category"])}`, "/sales/republic");
  const q = periodQuery(sp);

  return (
    <SalesFrame
      title={t("Республика")}
      subtitle={data.subtitle ? t(data.subtitle) : undefined}
      crumbs={[{ label: t("Республика") }]}
      back={withQuery("/sales", q)}
      sp={sp}
      period={data.period}
    >
      <GroupPage data={data} sp={sp} query={q} categoryQuery={q} regionsTitle={t("Все регионы")} scopeName={t("республике")} />
    </SalesFrame>
  );
}
