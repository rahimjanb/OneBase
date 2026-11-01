import { redirect } from "next/navigation";
import { ToIssueView } from "@/components/procurement/ToIssueView";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { RequestNav, ToIssue } from "@/lib/procurement";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("К выдаче · OneBase") };
}

/** Заявки к выдаче со склада: резерв по позициям, выдача кладовщиком (можно частями). */
export default async function ToIssuePage() {
  await requireRequests();
  const t = await getT();
  const nav = await apiGet<RequestNav>("/api/procurement/nav", "/procurement/issue");
  if (!nav.seesStock) redirect("/requests");
  const items = await apiGet<ToIssue[]>("/api/procurement/stock/to-issue", "/procurement/issue");
  return (
    <>
      <PageHeader
        title={t("К выдаче")}
        subtitle={t("Заявки, где товар взят со склада: он зарезервирован и ждёт выдачи отделу")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Снабжение"), href: "/procurement" }, { label: t("К выдаче") }]}
      />
      <PageBody>
        <ToIssueView items={items} warehouse={nav.warehouse} />
      </PageBody>
    </>
  );
}
