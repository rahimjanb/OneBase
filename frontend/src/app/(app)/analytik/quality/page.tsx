import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { Empty } from "@/components/analytik/ui";
import { one, requireAnalytik, type SearchParams } from "@/components/analytik/guard";
import { QualityView } from "@/components/analytik/intelligence/QualityView";
import { getT } from "@/i18n/server";
import type { QualityReport } from "@/lib/intelligence";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Качество аналитика · OneBase") };
}

/** «Аналитик → Качество»: насколько хорошо AI-аналитик решает вопросы и где его слабые места — управляющему Аналитиком. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const me = await requireAnalytik();
  const t = await getT();
  const days = [7, 30, 90].includes(Number(one(await searchParams, "days"))) ? Number(one(await searchParams, "days")) : 30;
  const manager = !!me?.permissions.includes("analytik.manage");
  const report = manager ? await apiGet<QualityReport>(`/api/analytik/quality?days=${days}`, "/analytik/quality") : null;
  return (
    <>
      <PageHeader
        title={t("Качество аналитика")}
        subtitle={t("Доля решённых вопросов, неполные данные, ошибки срезов, расчётов и инструментов, исследования, проверки и набор сложных вопросов")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Аналитик"), href: "/analytik" }, { label: t("Качество") }]}
      />
      <PageBody>
        {report ? <QualityView report={report} days={days} /> : <Empty text="Раздел — для управляющего Аналитиком" hint="Нужно право «Аналитик: управление»" />}
      </PageBody>
    </>
  );
}
