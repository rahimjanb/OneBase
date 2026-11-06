import { redirect } from "next/navigation";
import { requireProduction } from "@/components/production/guard";
import { RecipeForm } from "@/components/production/RecipeForm";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { RequestFormData } from "@/lib/procurement";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Новая рецептура · OneBase") };
}

/** Новая рецептура — черновик версии 1 (технолог). */
export default async function NewRecipePage() {
  const path = "/production/recipes/new";
  const nav = await requireProduction(path);
  if (!nav.recipes) redirect("/production/recipes");
  const t = await getT();
  const form = await apiGet<RequestFormData>("/api/procurement/form", path);
  return (
    <>
      <PageHeader
        title={t("Новая рецептура")}
        subtitle={t("Состав продукции: позиции склада, норма на выход и потери. Сохраняется черновиком — в действие вводится отдельно.")}
        back="/production/recipes"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Отделы"), href: "/departments" },
          { label: t("Производство"), href: "/production" },
          { label: t("Рецептуры"), href: "/production/recipes" },
          { label: t("Новая рецептура") },
        ]}
      />
      <PageBody>
        <RecipeForm units={form.units} />
      </PageBody>
    </>
  );
}
