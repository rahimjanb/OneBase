import Link from "next/link";
import { Alert, KpiTile } from "@/components/sales/bits";
import { OtherStocksTable, StockTable } from "@/components/sales/assortment-tables";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { ScopeSelect } from "@/components/sales/ScopeSelect";
import { apiGet } from "@/lib/server-api";
import { date, dateTime, kg, num } from "@/lib/sales/format";
import { param, type SalesSearchParams } from "@/lib/sales/query";
import type { StockStatus, StockView } from "@/lib/sales/types";

export const metadata = { title: "Остатки · Продажи" };

const units = [
  { key: "kg", label: "кг" },
  { key: "boxes", label: "коробки" },
  { key: "pieces", label: "штуки" },
] as const;

const statuses: { key: StockStatus | "all"; label: string }[] = [
  { key: "all", label: "Все" },
  { key: "deficit", label: "Дефицит" },
  { key: "overstock", label: "Затоварка" },
  { key: "dead", label: "Не продаётся" },
  { key: "unknown", label: "Без веса" },
];

function Chips<T extends string>({ items, current, href }: { items: readonly { key: T; label: string }[]; current: T; href: (key: T) => string }) {
  return (
    <div className="flex overflow-hidden rounded-lg border border-line text-sm">
      {items.map((i) => (
        <Link key={i.key} href={href(i.key)} className={`px-3 py-1.5 ${current === i.key ? "bg-accent text-white" : "bg-surface text-ink hover:bg-muted"}`}>
          {i.label}
        </Link>
      ))}
    </div>
  );
}

export default async function StockPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const sp = await searchParams;
  const region = param(sp, "region");
  const data = await apiGet<StockView>(`/api/sales/stock${region ? `?region=${encodeURIComponent(region)}` : ""}`, "/sales/stock");

  const unit = (units.find((u) => u.key === param(sp, "unit"))?.key ?? "kg") as (typeof units)[number]["key"];
  const status = (statuses.find((s) => s.key === param(sp, "status"))?.key ?? "all") as StockStatus | "all";
  const category = param(sp, "category") ?? "";
  const href = (changes: Record<string, string | null>) => {
    const next = new URLSearchParams();
    for (const [k, v] of Object.entries({ region, unit, status, category, ...changes })) {
      if (v && v !== "all" && !(k === "unit" && v === "kg")) next.set(k, v);
    }
    const s = next.toString();
    return s ? `/sales/stock?${s}` : "/sales/stock";
  };

  const categories = [...new Set(data.items.filter((i) => i.inReport).map((i) => i.category))].sort((a, b) => a.localeCompare(b, "ru"));
  const items = data.items.filter((i) => (status === "all" || i.status === status) && (!category || i.category === category));
  const regionName = data.regions.find((r) => r.id === region)?.name;
  const t = data.totals;

  return (
    <SalesFrame
      title="Остатки"
      subtitle={`Рекомендуемый остаток: запас дилеров по складам регионов${regionName ? ` · ${regionName}` : ""}`}
      crumbs={[{ label: "Остатки" }]}
      sp={sp}
    >
      <div className="flex flex-wrap items-center gap-3">
        <ScopeSelect
          label="Склад"
          keys={["region"]}
          options={[{ value: "", label: "Вся страна (склады регионов)" }, ...data.regions.map((r) => ({ value: `region:${r.id}`, label: r.name }))]}
        />
        <Chips items={units} current={unit} href={(k) => href({ unit: k })} />
        <Chips items={statuses} current={status} href={(k) => href({ status: k })} />
        <div className="flex flex-wrap gap-1 text-xs">
          <Link href={href({ category: null })} className={`rounded-full px-2.5 py-1 ${!category ? "bg-accent-soft font-semibold text-ink" : "text-ink-2 hover:bg-muted"}`}>
            все категории
          </Link>
          {categories.map((c) => (
            <Link key={c} href={href({ category: c })} className={`rounded-full px-2.5 py-1 ${category === c ? "bg-accent-soft font-semibold text-ink" : "text-ink-2 hover:bg-muted"}`}>
              {c}
            </Link>
          ))}
        </div>
      </div>

      {!data.syncedAt && (
        <Alert>Остатки ещё не загружены из Linko — они появятся после ближайшей синхронизации (или нажмите «Обновить»).</Alert>
      )}

      <div className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <KpiTile label="Остаток" value={kg(t.kg)} unit="кг">
          {t.boxes ? `${num(t.boxes)} коробок (где вес коробки подтверждён)` : "коробки — где вес коробки подтверждён"}
        </KpiTile>
        <KpiTile label="Хватит на" value={num(t.daysOfCover)} unit="дн.">
          продажи {kg(t.kgPerDay)} кг в день
        </KpiTile>
        <KpiTile label="SKU в дефиците" value={num(t.deficit)}>
          затоварка — {num(t.overstock)}, не продаётся — {num(t.dead)}
        </KpiTile>
        <KpiTile label="Остаток завода" value={kg(data.factoryTotals?.kg ?? null)} unit="кг">
          {data.factory ? `склад «${data.factory.name}» — в итог страны не входит` : "склад завода не найден"}
        </KpiTile>
      </div>
      <p className="mt-3 text-xs text-ink-3">
        Остатки на {dateTime(data.syncedAt)} (последняя загрузка из Linko). Скорость продаж — вторичка за {data.velocityDays} дн. ({date(data.velocityFrom)} –{" "}
        {date(data.velocityTo)}). {t.withoutWeight > 0 && `${num(t.withoutWeight)} SKU без веса штуки — в кг не пересчитаны.`}
      </p>

      <StockTable data={{ ...data, items }} unit={unit} />
      <OtherStocksTable rows={data.otherStocks} />
    </SalesFrame>
  );
}
