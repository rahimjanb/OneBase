import { notFound } from "next/navigation";
import { GroupPage } from "@/components/sales/GroupPage";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { getT } from "@/i18n/server";
import { apiGetOrNull } from "@/lib/server-api";
import { visibleBack, visibleCrumbs } from "@/lib/sales/access";
import { mySalesScope } from "@/lib/sales/scope-server";
import { apiQuery, periodQuery, queryWith, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { GroupView } from "@/lib/sales/types";

export default async function DirectionPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<SalesSearchParams>;
}) {
  const t = await getT();
  const scope = await mySalesScope(); // крошки и «Назад» — без страниц, закрытых охвату
  const [{ id }, sp] = await Promise.all([params, searchParams]);
  const path = `/sales/directions/${id}`;
  const data = await apiGetOrNull<GroupView>(`/api/sales/directions/${id}${apiQuery(sp, ["from", "to", "metric", "category"])}`, path);
  if (!data) notFound();
  const q = periodQuery(sp);
  // Служебная группа «Без направления» (id none): имя и подпись — тексты сервера, переводятся; у направления это данные.
  const name = id === "none" ? t(data.name) : data.name;
  const subtitle = data.subtitle ? (id === "none" ? t(data.subtitle) : data.subtitle) : undefined;

  return (
    <SalesFrame
      title={name}
      subtitle={subtitle}
      crumbs={visibleCrumbs([{ label: t("Республика"), href: withQuery("/sales/republic", q) }, { label: name }], scope, t("Мои регионы"))}
      back={visibleBack(withQuery("/sales/republic", q), scope)}
      sp={sp}
      period={data.period}
    >
      <GroupPage
        data={data}
        sp={sp}
        query={q}
        categoryQuery={queryWith(q, { direction: id })}
        regionsTitle={t("Сравнение регионов")}
        scopeName={t("направлении")}
        republicWide
      />
    </SalesFrame>
  );
}
