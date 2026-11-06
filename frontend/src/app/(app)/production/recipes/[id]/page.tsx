import { notFound } from "next/navigation";
import { requireProduction } from "@/components/production/guard";
import { RecipeDetailView } from "@/components/production/RecipeDetailView";
import { getT } from "@/i18n/server";
import type { WarehousesView } from "@/lib/procurement";
import type { RecipeDetail } from "@/lib/production";
import { apiGetOrNull, apiTry } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Рецептура · OneBase") };
}

/** Карточка версии рецептуры: состав, потребность на выпуск, версии, история. */
export default async function RecipePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const path = `/production/recipes/${id}`;
  await requireProduction(path);
  const recipe = await apiGetOrNull<RecipeDetail>(`/api/production/recipes/${id}`, path);
  if (!recipe) notFound();
  const stores = await apiTry<WarehousesView>("/api/procurement/warehouses");
  return <RecipeDetailView recipe={recipe} warehouses={stores?.warehouses ?? []} />;
}
