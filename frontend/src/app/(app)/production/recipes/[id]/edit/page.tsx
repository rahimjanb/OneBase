import { notFound, redirect } from "next/navigation";
import { requireProduction } from "@/components/production/guard";
import { RecipeForm } from "@/components/production/RecipeForm";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { RequestFormData } from "@/lib/procurement";
import type { RecipeDetail } from "@/lib/production";
import { apiGet, apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Изменить рецептуру · OneBase") };
}

/** Правка черновика рецептуры; действующую и архивную не правят — для них «Новая версия». */
export default async function EditRecipePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const path = `/production/recipes/${id}/edit`;
  await requireProduction(path);
  const recipe = await apiGetOrNull<RecipeDetail>(`/api/production/recipes/${id}`, path);
  if (!recipe) notFound();
  if (!recipe.can.edit) redirect(`/production/recipes/${id}`);
  const t = await getT();
  const form = await apiGet<RequestFormData>("/api/procurement/form", path);
  return (
    <>
      <PageHeader
        title={t("Изменить черновик")}
        subtitle={`${recipe.code} · ${t("версия {n}", { n: recipe.versionNo })} · ${recipe.name}`}
        back={`/production/recipes/${id}`}
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Отделы"), href: "/departments" },
          { label: t("Производство"), href: "/production" },
          { label: t("Рецептуры"), href: "/production/recipes" },
          { label: `${recipe.code} v${recipe.versionNo}`, href: `/production/recipes/${id}` },
          { label: t("Изменить") },
        ]}
      />
      <PageBody>
        <RecipeForm initial={recipe} units={form.units} />
      </PageBody>
    </>
  );
}
