import { ReportsView } from "@/components/procurement/ReportsView";
import { requireProduction } from "@/components/production/guard";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { ReportInfo, ReportPreview, RequestDepartment, WarehousesView } from "@/lib/procurement";
import { apiGet, apiTry } from "@/lib/server-api";
import { qs } from "@/lib/tasks";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Отчёты производства · OneBase") };
}

type SearchParams = Record<string, string | string[] | undefined>;
const one = (sp: SearchParams, key: string) => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

/** «Производство → Отчёты»: партии и сроки, контроль качества, расход и норма, движения по партиям, сверка — предпросмотр, Excel и PDF. */
export default async function ProductionReportsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const path = "/production/reports";
  await requireProduction(path);
  const t = await getT();
  const sp = await searchParams;
  const kind = one(sp, "kind") ?? "batches";
  const filter = { from: one(sp, "from"), to: one(sp, "to"), department: one(sp, "department"), warehouse: one(sp, "warehouse") };
  const [kinds, preview, departments, stores] = await Promise.all([
    apiGet<ReportInfo[]>("/api/production/reports", path),
    apiGet<ReportPreview>(`/api/production/reports/${encodeURIComponent(kind)}${qs(filter)}`, path),
    apiTry<RequestDepartment[]>("/api/procurement/stock/departments"),
    apiTry<WarehousesView>("/api/procurement/warehouses"),
  ]);
  return (
    <>
      <PageHeader
        title={t("Отчёты производства")}
        subtitle={t("Партии и сроки, контроль качества, расход и норма, движения по партиям, сверка позиций и партий")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Отделы"), href: "/departments" }, { label: t("Производство"), href: "/production" }, { label: t("Отчёты") }]}
      />
      <PageBody>
        <ReportsView
          base="production"
          kinds={kinds}
          kind={kind}
          preview={preview}
          categories={[]}
          departments={kind === "consumption" ? (departments ?? []) : []}
          employees={[]}
          suppliers={[]}
          warehouses={(stores?.warehouses ?? []).filter((w) => w.isActive).map((w) => ({ id: w.id, name: w.name }))}
        />
      </PageBody>
    </>
  );
}
