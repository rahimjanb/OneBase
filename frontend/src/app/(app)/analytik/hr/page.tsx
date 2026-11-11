import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { requireAnalytik, periodParams, type SearchParams } from "@/components/analytik/guard";
import { DataGaps } from "@/components/analytik/ui";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import { HrView } from "@/components/analytik/Departments";
import type { Block, HrDivision, Period, WorkDepartment, Readiness } from "@/lib/analytik";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Аналитика персонала · OneBase") };
}

/** «Аналитик → Персонал». */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireAnalytik();
  const t = await getT();
  const sp = await searchParams;
  const data = await apiGet<{ period: Period; hr: (Block & { divisions: HrDivision[] }) | null; work: (Block & { departments: WorkDepartment[] }) | null }>(`/api/analytik/hr?${periodParams(sp)}`, "/analytik/hr");
  // Данные периода загружены не полностью (обмен с источником отстаёт) — баннер с причиной, принятыми мерами и последним месяцем с данными.
  const readiness = await apiGet<Readiness>(`/api/analytik/readiness?${periodParams(sp)}&module=hr`, "/analytik/hr").catch(() => null);
  return (
    <>
      <PageHeader title={t("Персонал")} subtitle={t("Посещаемость, опоздания и отсутствия по подразделениям, задачи отделов и их просрочка")} breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Аналитик"), href: "/analytik" }, { label: t("Персонал") }]} />
      <PageBody>
        <div className="space-y-4">
          {readiness && <DataGaps gaps={readiness.gaps} />}
          <HrView hr={data.hr} work={data.work} period={data.period} />
        </div>
      </PageBody>
    </>
  );
}
