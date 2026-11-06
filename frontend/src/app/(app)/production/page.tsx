import { requireProduction } from "@/components/production/guard";
import { OverviewView } from "@/components/production/OverviewView";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { WarehousesView } from "@/lib/procurement";
import type { ProductionOverview } from "@/lib/production";
import { apiGet, apiTry } from "@/lib/server-api";
import { qs } from "@/lib/tasks";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Производство · OneBase") };
}

type SearchParams = Record<string, string | string[] | undefined>;
const one = (sp: SearchParams, key: string) => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

/** «Производство → Обзор»: сырьё и производство на сегодня и за период — запас, качество, сроки, расход и норма, дни запаса, цены. */
export default async function ProductionPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const path = "/production";
  await requireProduction(path);
  const t = await getT();
  const sp = await searchParams;
  const [data, stores] = await Promise.all([
    apiGet<ProductionOverview>(`/api/production/overview${qs({ from: one(sp, "from"), to: one(sp, "to"), warehouse: one(sp, "warehouse") })}`, path),
    apiTry<WarehousesView>("/api/procurement/warehouses"),
  ]);
  return (
    <>
      <PageHeader
        title={t("Производство")}
        subtitle={t("Сырьё и производство: запас, контроль качества, сроки годности, расход и норма")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Отделы"), href: "/departments" }, { label: t("Производство") }]}
      />
      <PageBody>
        <OverviewView data={data} warehouses={stores?.warehouses ?? []} />
      </PageBody>
    </>
  );
}
