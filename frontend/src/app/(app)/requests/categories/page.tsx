import { redirect } from "next/navigation";
import { CategoriesView } from "@/components/requests/CategoriesView";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { RequestCategory, RequestNav } from "@/lib/procurement";

type Settings = { reapprovalTolerancePercent: number; escalateHeadDays: number; escalateDirectorDays: number };
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Категории закупок · OneBase") };
}

/** Категории закупок — настраивают директор и администратор (procurement.settings); остальным — в обзор раздела. */
export default async function RequestCategoriesPage() {
  await requireRequests();
  const t = await getT();
  const nav = await apiGet<RequestNav>("/api/procurement/nav", "/requests/categories");
  if (!nav.configures) redirect("/requests");
  const [categories, settings] = await Promise.all([
    apiGet<RequestCategory[]>("/api/procurement/categories?all=true", "/requests/categories"),
    apiGet<Settings>("/api/procurement/settings", "/requests/categories"),
  ]);
  return (
    <>
      <PageHeader
        title={t("Настройки заявок")}
        subtitle={t("Правила повторного согласования и категории закупок")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Заявки"), href: "/requests" }, { label: t("Настройки") }]}
      />
      <PageBody>
        <CategoriesView categories={categories} settings={settings} />
      </PageBody>
    </>
  );
}
