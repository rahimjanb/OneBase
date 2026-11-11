import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { requireAnalytik } from "@/components/analytik/guard";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import { ReportsView } from "@/components/analytik/Insights";
import type { ReportListRow } from "@/lib/analytik";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Отчёты · OneBase") };
}

/** «Аналитик → Отчёты». */
export default async function Page() {
  await requireAnalytik();
  const t = await getT();
  const reports = await apiGet<ReportListRow[]>("/api/analytik/reports", "/analytik/reports");
  return (
    <>
      <PageHeader title={t("Отчёты")} subtitle={t("Управленческие отчёты по данным OneBase: ежедневный, недельный, месячный и отчёты отделов — Excel и PDF")} breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Аналитик"), href: "/analytik" }, { label: t("Отчёты") }]} />
      <PageBody>
        <ReportsView reports={reports} />
      </PageBody>
    </>
  );
}
