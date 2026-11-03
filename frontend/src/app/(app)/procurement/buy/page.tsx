import { redirect } from "next/navigation";
import { ToBuyView } from "@/components/procurement/ToBuyView";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { RequestNav, ToBuyView as ToBuyData } from "@/lib/procurement";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("К закупке · OneBase") };
}

/** К закупке: согласованные заявки, по которым снабжению осталось купить; «Куплено» — сразу на склад к выдаче. Видят снабжение и директор. */
export default async function ToBuyPage() {
  await requireRequests();
  const t = await getT();
  const nav = await apiGet<RequestNav>("/api/procurement/nav", "/procurement/buy");
  if (!nav.readsAll && !nav.isDirector) redirect("/procurement");
  const data = await apiGet<ToBuyData>("/api/procurement/to-buy", "/procurement/buy");
  return (
    <>
      <PageHeader
        title={t("К закупке")}
        subtitle={t("Согласованные заявки: купите и отметьте «Куплено» — товар поступит на склад и будет ждать выдачи сотруднику")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Снабжение"), href: "/procurement" }, { label: t("К закупке") }]}
      />
      <PageBody>
        <ToBuyView items={data.requests} canBuy={data.canBuy} />
      </PageBody>
    </>
  );
}
