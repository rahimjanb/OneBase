import { GroupPage } from "@/components/sales/GroupPage";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { apiGet } from "@/lib/server-api";
import { apiQuery, periodQuery, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { GroupView } from "@/lib/sales/types";

export const metadata = { title: "Республика · Продажи" };

export default async function RepublicPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const sp = await searchParams;
  const data = await apiGet<GroupView>(`/api/sales/republic${apiQuery(sp, ["from", "to"])}`, "/sales/republic");
  const q = periodQuery(sp);

  return (
    <SalesFrame
      title="Республика"
      subtitle={data.subtitle ?? undefined}
      crumbs={[{ label: "Республика" }]}
      back={withQuery("/sales", q)}
      sp={sp}
    >
      <GroupPage data={data} sp={sp} query={q} regionsTitle="Все регионы" scopeName="республике" />
    </SalesFrame>
  );
}
