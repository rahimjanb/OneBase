import { requireProduction } from "@/components/production/guard";
import { RecipesView } from "@/components/production/RecipesView";
import { getT } from "@/i18n/server";
import type { RecipeList } from "@/lib/production";
import { apiGet } from "@/lib/server-api";
import { qs } from "@/lib/tasks";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Техкарты · OneBase") };
}

type SearchParams = Record<string, string | string[] | undefined>;
const one = (sp: SearchParams, key: string) => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

/**
 * «Производство → Технолог → Техкарты»: технологические карты продуктов по кодам с фото — утверждённые версии, черновики, на утверждении,
 * архив. Шапку, поиск, фильтры и карточки рисует RecipesView (своя шапка на телефоне, как у «Технолога»). Правит технолог.
 */
export default async function RecipesPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const path = "/production/recipes";
  await requireProduction(path);
  const sp = await searchParams;
  const data = await apiGet<RecipeList>(`/api/production/recipes${qs({ q: one(sp, "q"), status: one(sp, "status") })}`, path);
  return <RecipesView data={data} />;
}
