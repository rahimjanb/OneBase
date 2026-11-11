import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { requireAnalytik } from "@/components/analytik/guard";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import { ProcurementView } from "@/components/analytik/Supply";
import type { SupplyPlan } from "@/lib/analytik";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("План закупок · OneBase") };
}

/** «Аналитик → Снабжение». */
export default async function Page() {
  await requireAnalytik();
  const t = await getT();
  const data = await apiGet<SupplyPlan>(`/api/analytik/supply`, "/analytik/procurement");
  return (
    <>
      <PageHeader title={t("Снабжение")} subtitle={t("Что, сколько и когда закупить на 7–90 дней: спрос, страховой запас, доступный остаток, подтверждённые поставки, бюджет и риск")} breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Аналитик"), href: "/analytik" }, { label: t("Снабжение") }]} />
      <PageBody>
        <ProcurementView data={data} />
      </PageBody>
    </>
  );
}
