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
  return { title: t("Техкарты · OneBase") };
}

type SearchParams = Record<string, string | string[] | undefined>;
const one = (sp: SearchParams, key: string) => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

/** «Производство → Технолог»: технологические карты продуктов по кодам — утверждённые версии, черновики, на утверждении, архив. Правит технолог. */
export default async function RecipesPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const path = "/production/recipes";
  await requireProduction(path);
  const t = await getT();
  const sp = await searchParams;
  const data = await apiGet<RecipeList>(`/api/production/recipes${qs({ q: one(sp, "q"), status: one(sp, "status") })}`, path);
  return (
    <>
      <PageHeader
        title={t("Техкарты")}
        subtitle={t("Технологические карты продуктов: этапы, рецептура замеса, нормы, брак и упаковка — по версиям с утверждением")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Отделы"), href: "/departments" }, { label: t("Производство"), href: "/production" }, { label: t("Технолог"), href: "/production/technologist" }, { label: t("Техкарты") }]}
        actions={
          data.canCreate ? (
            <Link href="/production/recipes/new" className="inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-accent-strong">
              <Plus className="size-4" aria-hidden /> {t("Новая техкарта")}
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
