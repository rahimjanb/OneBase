import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { requireAnalytik } from "@/components/analytik/guard";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import { WarehouseView } from "@/components/analytik/Supply";
import type { SupplyPlan } from "@/lib/analytik";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Склад сырья · OneBase") };
}

/** «Аналитик → Склад». */
export default async function Page() {
  await requireAnalytik();
  const t = await getT();
  const data = await apiGet<SupplyPlan>(`/api/analytik/supply`, "/analytik/warehouse");
  return (
    <>
      <PageHeader title={t("Склад")} subtitle={t("Остатки сырья и материалов: физический, резерв, непригодный и доступный остаток, расход, дни запаса, дефицит и излишки")} breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Аналитик"), href: "/analytik" }, { label: t("Склад") }]} />
      <PageBody>
        <WarehouseView data={data} />
      </PageBody>
    </>
  );
}
