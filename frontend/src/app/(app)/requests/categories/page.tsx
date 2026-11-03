import { redirect } from "next/navigation";
import { CategoriesView } from "@/components/requests/CategoriesView";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { RequestCategory, RequestNav } from "@/lib/procurement";
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
  const categories = await apiGet<RequestCategory[]>("/api/procurement/categories?all=true", "/requests/categories");
  return (
    <>
      <PageHeader
        title={t("Настройки заявок")}
        subtitle={t("Категории закупок; маршрут согласования и правила — в «Настройки → Отделы → Снабжение»")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Заявки"), href: "/requests" }, { label: t("Настройки") }]}
      />
      <PageBody>
        <CategoriesView categories={categories} />
      </PageBody>
    </>
  );
}
