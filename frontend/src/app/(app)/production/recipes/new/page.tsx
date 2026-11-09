import { redirect } from "next/navigation";
import { requireProduction } from "@/components/production/guard";
import { RecipeForm } from "@/components/production/RecipeForm";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { RecipeFormData } from "@/lib/production";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Новая техкарта · OneBase") };
}

/** Новая технологическая карта — черновик версии 1 (технолог). */
export default async function NewRecipePage() {
  const path = "/production/recipes/new";
  const nav = await requireProduction(path);
  if (!nav.recipes) redirect("/production/recipes");
  const t = await getT();
  const form = await apiGet<RecipeFormData>("/api/production/recipes/form", path);
  return (
    <>
      <PageHeader
        title={t("Новая технологическая карта")}
        subtitle={t("Продукт, этапы изготовления, рецептура замеса, нормы, брак и упаковка. Сохраняется черновиком — действует после утверждения.")}
        back="/production/recipes"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Отделы"), href: "/departments" },
          { label: t("Производство"), href: "/production" },
          { label: t("Технолог"), href: "/production/technologist" },
          { label: t("Техкарты"), href: "/production/recipes" },
          { label: t("Новая техкарта") },
        ]}
      />
      <PageBody>
        <RecipeForm form={form} />
      </PageBody>
    </>
  );
}
