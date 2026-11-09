import { notFound } from "next/navigation";
import { requireProduction } from "@/components/production/guard";
import { RecipeDetailView } from "@/components/production/RecipeDetailView";
import { getT } from "@/i18n/server";
import type { WarehousesView } from "@/lib/procurement";
import type { RecipeDetail, Requirement } from "@/lib/production";
import { apiGetOrNull, apiTry } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Техкарта · OneBase") };
}

/** Технологическая карта (версия рецептуры): цепочка с массами на выход рецептуры, состав по этапам, брак, расчёт, версии, история. */
export default async function RecipePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const path = `/production/recipes/${id}`;
  await requireProduction(path);
  const recipe = await apiGetOrNull<RecipeDetail>(`/api/production/recipes/${id}`, path);
  if (!recipe) notFound();
  // Расчёт на выход рецептуры — для схемы цепочки и колонки «на 1 000 кг»; карта с ошибками не считается.
  const [stores, base] = await Promise.all([
    apiTry<WarehousesView>("/api/procurement/warehouses"),
    recipe.lines.length > 0 && recipe.problems.length === 0
      ? apiTry<Requirement>(`/api/production/recipes/${id}/requirement?quantity=${recipe.outputQuantity}`)
      : Promise.resolve(null),
  ]);
  return <RecipeDetailView recipe={recipe} warehouses={stores?.warehouses ?? []} base={base?.calculation ?? null} />;
}
