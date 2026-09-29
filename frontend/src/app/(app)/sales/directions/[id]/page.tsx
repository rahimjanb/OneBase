import { notFound } from "next/navigation";
import { GroupPage } from "@/components/sales/GroupPage";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { apiGetOrNull } from "@/lib/server-api";
import { apiQuery, periodQuery, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { GroupView } from "@/lib/sales/types";

export default async function DirectionPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<SalesSearchParams>;
}) {
  const [{ id }, sp] = await Promise.all([params, searchParams]);
  const path = `/sales/directions/${id}`;
  const data = await apiGetOrNull<GroupView>(`/api/sales/directions/${id}${apiQuery(sp, ["from", "to"])}`, path);
  if (!data) notFound();
  const q = periodQuery(sp);

  return (
    <SalesFrame
      title={data.name}
      subtitle={data.subtitle ?? undefined}
      crumbs={[{ label: "Республика", href: withQuery("/sales/republic", q) }, { label: data.name }]}
      back={withQuery("/sales/republic", q)}
      sp={sp}
    >
      <GroupPage data={data} sp={sp} query={q} regionsTitle="Сравнение регионов" scopeName="направлении" />
    </SalesFrame>
  );
}
