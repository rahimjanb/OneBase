import { redirect } from "next/navigation";
import { ReportsView } from "@/components/procurement/ReportsView";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { ReportInfo, ReportPreview, RequestDepartment, RequestFormData, RequestNav, SupplierRow } from "@/lib/procurement";
import { apiGet } from "@/lib/server-api";
import { qs } from "@/lib/tasks";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Отчёты снабжения · OneBase") };
}

type SearchParams = Record<string, string | string[] | undefined>;
const one = (sp: SearchParams, key: string) => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

/** «Снабжение → Отчёты»: отчёт, период и фильтры (отдел, категория, сотрудник, поставщик), предпросмотр, Excel и PDF. Снабжение и директор. */
export default async function ReportsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireRequests();
  const t = await getT();
  const sp = await searchParams;
  const nav = await apiGet<RequestNav>("/api/procurement/nav", "/procurement/reports");
  if (!nav.readsAll && !nav.isDirector) redirect("/procurement");
  const kind = one(sp, "kind") ?? "kpi";
  const filter = { from: one(sp, "from"), to: one(sp, "to"), department: one(sp, "department"), category: one(sp, "category"), employee: one(sp, "employee"), supplier: one(sp, "supplier") };
  const [kinds, preview, form, departments, employees, suppliers] = await Promise.all([
    apiGet<ReportInfo[]>("/api/procurement/reports", "/procurement/reports"),
    apiGet<ReportPreview>(`/api/procurement/reports/${encodeURIComponent(kind)}${qs(filter)}`, "/procurement/reports"),
    apiGet<RequestFormData>("/api/procurement/form", "/procurement/reports"),
    apiGet<RequestDepartment[]>("/api/procurement/stock/departments", "/procurement/reports"),
    apiGet<{ id: string; name: string }[]>("/api/procurement/orders/responsibles", "/procurement/reports"),
    apiGet<SupplierRow[]>("/api/procurement/suppliers/options", "/procurement/reports"),
  ]);
  return (
    <>
      <PageHeader
        title={t("Отчёты")}
        subtitle={t("Закупки, поставки, приёмки и поставщики за период — с выгрузкой в Excel и PDF")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Отделы"), href: "/departments" }, { label: t("Снабжение"), href: "/procurement" }, { label: t("Отчёты") }]}
      />
      <PageBody>
        <ReportsView
          kinds={kinds}
          kind={kind}
          preview={preview}
          categories={form.categories}
          departments={departments}
          employees={employees}
          suppliers={suppliers.map((s) => ({ id: s.id, name: s.name }))}
        />
      </PageBody>
    </>
  );
}
