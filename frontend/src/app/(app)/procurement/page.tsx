import { redirect } from "next/navigation";
import { DashboardView } from "@/components/procurement/DashboardView";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { Dashboard, RequestNav } from "@/lib/procurement";
import { apiGet } from "@/lib/server-api";
import { qs } from "@/lib/tasks";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Снабжение · OneBase") };
}

/**
 * «Снабжение → Обзор»: показатели и блоки по роли — директору закупки и расходы, руководителю снабжения работа сотрудников, снабжению
 * свои заказы и сроки, кладовщику поступления и остатки. Период — в адресе (from, to); без него — текущий месяц.
 */
export default async function ProcurementPage({ searchParams }: { searchParams: Promise<{ from?: string; to?: string }> }) {
  await requireRequests();
  const t = await getT();
  const { from, to } = await searchParams;
  const nav = await apiGet<RequestNav>("/api/procurement/nav", "/procurement");
  if (!nav.seesStock && !nav.isDirector) redirect("/requests");
  const data = await apiGet<Dashboard>(`/api/procurement/dashboard${qs({ from, to })}`, "/procurement");
  return (
    <>
      <PageHeader
        title={t("Снабжение")}
        subtitle={t("Обзор закупок, поставок и склада — по вашей роли")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Снабжение") }]}
      />
      <PageBody>
        <DashboardView data={data} />
      </PageBody>
    </>
  );
}
