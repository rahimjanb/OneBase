import { AkbChart } from "@/components/sales/AkbChart";
import { AssortmentMatrix, AssortmentRegionsTable, ProductsTable } from "@/components/sales/assortment-tables";
import { CollapsedSections } from "@/components/sales/bits";
import { DataQualityNotes } from "@/components/sales/blocks";
import { CategoryCards } from "@/components/sales/categories";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { ScopeSelect } from "@/components/sales/ScopeSelect";
import { apiGet } from "@/lib/server-api";
import { apiQuery, param, periodQuery, queryWith, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { AssortmentView, GroupView } from "@/lib/sales/types";

export const metadata = { title: "Ассортимент · Продажи" };

export default async function AssortmentPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const sp = await searchParams;
  const [data, republic] = await Promise.all([
    apiGet<AssortmentView>(`/api/sales/assortment${apiQuery(sp, ["region", "direction"])}`, "/sales/assortment"),
    apiGet<GroupView>(`/api/sales/republic${apiQuery(sp)}`, "/sales/assortment"),
  ]);
  const q = periodQuery(sp);

  const options = [
    { value: "", label: "Вся республика" },
    ...republic.cards.filter((c) => c.kind === "direction").map((c) => ({ value: `direction:${c.id}`, label: `${c.name} — целиком`, group: "Региональные менеджеры" })),
    ...republic.regions
      .filter((r) => r.factKg !== 0 || r.akb > 0)
      .map((r) => ({ value: `region:${r.id}`, label: r.name, group: "Регионы" })),
  ];
  const scopeLabel = data.scopeName === "Республика" ? "республике" : data.scopeName;

  return (
    <SalesFrame
      title="Ассортимент"
      subtitle={`Что и где продаётся: категории, артикулы, дистрибуция · ${data.scopeName}`}
      crumbs={[{ label: "Ассортимент" }]}
      sp={sp}
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <ScopeSelect options={options} />
      </div>
      <CategoryCards cards={data.categories} scope={scopeLabel} query={queryWith(q, { direction: param(sp, "direction"), region: param(sp, "region") })} />
      <CollapsedSections>
        <AkbChart data={data.akbMonths} />
        <AssortmentRegionsTable rows={data.regions} query={q} />
        <AssortmentMatrix data={data} />
        <ProductsTable rows={data.products} hint={`по выручке за месяц · ${data.scopeName}`} />
        <DataQualityNotes quality={data.quality} />
      </CollapsedSections>
      <p className="mt-4 text-xs text-ink-3">
        Регион подробнее — на его странице во «Вторичке»:{" "}
        <a className="text-accent hover:underline" href={withQuery("/sales/republic", q)}>
          все регионы
        </a>
        .
      </p>
    </SalesFrame>
  );
}
