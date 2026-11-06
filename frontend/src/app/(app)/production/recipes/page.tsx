import Link from "next/link";
import { Plus } from "lucide-react";
import { requireProduction } from "@/components/production/guard";
import { RecipesView } from "@/components/production/RecipesView";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { RecipeList } from "@/lib/production";
import { apiGet } from "@/lib/server-api";
import { qs } from "@/lib/tasks";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Рецептуры · OneBase") };
}

type SearchParams = Record<string, string | string[] | undefined>;
const one = (sp: SearchParams, key: string) => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

/** «Производство → Рецептуры»: состав продукции по кодам — действующие версии, черновики, архив. Правит технолог. */
export default async function RecipesPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const path = "/production/recipes";
  await requireProduction(path);
  const t = await getT();
  const sp = await searchParams;
  const data = await apiGet<RecipeList>(`/api/production/recipes${qs({ q: one(sp, "q"), status: one(sp, "status") })}`, path);
  return (
    <>
      <PageHeader
        title={t("Рецептуры")}
        subtitle={t("Состав продукции: позиции склада, нормы на выход и потери — по версиям")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Отделы"), href: "/departments" }, { label: t("Производство"), href: "/production" }, { label: t("Рецептуры") }]}
        actions={
          data.canCreate ? (
            <Link href="/production/recipes/new" className="inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-accent-strong">
              <Plus className="size-4" aria-hidden /> {t("Новая рецептура")}
            </Link>
          ) : undefined
        }
      />
      <PageBody>
        <RecipesView data={data} />
      </PageBody>
    </>
  );
}
