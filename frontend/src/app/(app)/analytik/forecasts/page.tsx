import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { requireAnalytik } from "@/components/analytik/guard";
import { getT } from "@/i18n/server";
import { ForecastsView } from "@/components/analytik/Insights";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Прогнозы · OneBase") };
}

/** «Аналитик → Прогнозы»: прогноз считается на экране (меняются показатель, шаг, горизонт, сценарий). */
export default async function Page() {
  const me = await requireAnalytik();
  const t = await getT();
  return (
    <>
      <PageHeader title={t("Прогнозы")} subtitle={t("Продажи, оплаты и расход сырья: прогноз с диапазоном, сценарии «что если», точность на истории и прошлых прогнозов")} breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Аналитик"), href: "/analytik" }, { label: t("Прогнозы") }]} />
      <PageBody>
        <ForecastsView initial={null} canSales={!!me?.permissions.includes("sales.read")} />
      </PageBody>
    </>
  );
}
