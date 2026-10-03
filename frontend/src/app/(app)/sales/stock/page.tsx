import Link from "next/link";
import { Alert, KpiTile, Note, Section } from "@/components/sales/bits";
import { OtherStocksTable, StockTable } from "@/components/sales/assortment-tables";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { ScopeSelect } from "@/components/sales/ScopeSelect";
import { apiGet } from "@/lib/server-api";
import { date, dateTime, kg, money, num } from "@/lib/sales/format";
import { param, type SalesSearchParams } from "@/lib/sales/query";
import type { StockStatus, StockView } from "@/lib/sales/types";

export const metadata = { title: "Рекомендуемый остаток · Продажи" };

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
    <div className="no-scrollbar flex max-w-full overflow-x-auto rounded-lg border border-line text-sm">
      {items.map((i) => (
        <Link key={i.key} href={href(i.key)} className={`shrink-0 whitespace-nowrap px-3 py-1.5 max-lg:py-2.5 ${current === i.key ? "bg-accent text-white" : "bg-surface text-ink hover:bg-muted"}`}>
          {i.label}
        </Link>
      ))}
    </div>
  );
}

/**
 * «Рекомендуемый остаток»: запас дилеров по складам регионов на дату последней загрузки из Linko — хватит ли его
 * до следующей поставки и сколько он стоит по входной цене. Это снимок на дату, а не итог месяца, поэтому периода нет.
 */
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
  const f = data.factoryTotals;
  const priceList = data.priceList ?? "вход дилеру";

  return (
    <SalesFrame
      title="Рекомендуемый остаток"
      subtitle={`Запас дилеров по складам регионов: хватит ли до поставки и сколько он стоит${regionName ? ` · ${regionName}` : ""}`}
      crumbs={[{ label: "Рек. остаток" }]}
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
          <Link href={href({ category: null })} className={`rounded-full px-2.5 py-1 max-lg:py-2 ${!category ? "bg-accent-soft font-semibold text-ink" : "text-ink-2 hover:bg-muted"}`}>
            все категории
          </Link>
          {categories.map((c) => (
            <Link key={c} href={href({ category: c })} className={`rounded-full px-2.5 py-1 max-lg:py-2 ${category === c ? "bg-accent-soft font-semibold text-ink" : "text-ink-2 hover:bg-muted"}`}>
              {c}
            </Link>
          ))}
        </div>
      </div>

      {!data.syncedAt && (
        <Alert>Остатки ещё не загружены из Linko — они появятся после ближайшей синхронизации (или нажмите «Обновить»).</Alert>
      )}

      <div className="mt-4 grid grid-cols-2 gap-3 lg:grid-cols-3 min-[87.5rem]:grid-cols-5">
        <KpiTile label="Остаток" value={kg(t.kg)} unit="кг">
          {t.boxes ? `${num(t.boxes)} коробок (где вес коробки подтверждён)` : "коробки — где вес коробки подтверждён"}
        </KpiTile>
        <KpiTile label="Стоимость запаса" value={money(t.valueSum)} unit="сум" title={`Штуки × цена за единицу учёта из прайса «${priceList}», последняя по времени`}>
          по входной цене дилера{t.withoutPrice > 0 ? ` · без цены ${num(t.withoutPrice)} SKU` : ""}
        </KpiTile>
        <KpiTile label="Хватит на" value={num(t.daysOfCover)} unit="дн." tone={t.daysOfCover == null ? "muted" : t.daysOfCover < 15 ? "bad" : t.daysOfCover > 30 ? "warn" : "ok"}>
          продажи {kg(t.kgPerDay)} кг в день
        </KpiTile>
        <KpiTile label="SKU в дефиците" value={num(t.deficit)} tone={t.deficit > 0 ? "bad" : "ok"}>
          затоварка — {num(t.overstock)}, не продаётся — {num(t.dead)}
        </KpiTile>
        <KpiTile label="Остаток завода" value={kg(f?.kg ?? null)} unit="кг" title={f?.valueSum ? `${money(f.valueSum)} сум по входной цене` : undefined}>
          {data.factory
            ? f?.daysOfCover != null
              ? `хватит на ${num(f.daysOfCover)} дн. по скорости всей страны · в итог страны не входит`
              : `склад «${data.factory.name}» — в итог страны не входит`
            : "склад завода не найден"}
        </KpiTile>
      </div>
      <p className="mt-3 text-xs text-ink-3">
        Остатки на {dateTime(data.syncedAt)} (последняя загрузка из Linko). Скорость продаж — вторичка за {data.velocityDays} дн. ({date(data.velocityFrom)} –{" "}
        {date(data.velocityTo)}). {t.withoutWeight > 0 && `${num(t.withoutWeight)} SKU без веса штуки — в кг не пересчитаны. `}
        {t.approxWeight > 0 && `У ${num(t.approxWeight)} SKU продаж за год не было — вес взят из названия, кг помечены «≈». `}
        {t.withoutPrice > 0 && `${num(t.withoutPrice)} SKU нет в прайсе «${priceList}» — в стоимость не вошли.`}
      </p>

      <StockTable data={{ ...data, items }} unit={unit} />
      <OtherStocksTable rows={data.otherStocks} />

      <Section title="Как считается" hint="все данные — из Linko">
        <ul className="space-y-2 text-sm text-ink-2">
          <li>
            <b className="text-ink">Остаток</b> — положение дел на дату последней загрузки, а не итог месяца, поэтому переключателя периода на этой вкладке нет.
            Склад региона — склад Linko с тем же названием, что у региона.
          </li>
          <li>
            <b className="text-ink">Продажи в день</b> — средние по вторичке за последние {data.velocityDays} дн. в регионе склада: те же заказы, что кормят «Ассортимент».
            Запас на 15 и 30 дней — скорость × горизонт; «Хватит» — остаток ÷ скорость. Дефицит — меньше 15 дней, затоварка — больше 30,
            «не продаётся» — остаток лежит, а продаж за базу не было вовсе.
          </li>
          <li>
            <b className="text-ink">Остаток завода</b> во «Всю страну» не входит: он ещё не отгружен дилерам, и сложение посчитало бы один и тот же товар дважды.
            Своей скорости продаж у завода нет, поэтому его запас меряется тем, что уходит по стране целиком.
          </li>
          <li>
            <b className="text-ink">Единица учёта и цена.</b> Linko хранит и остаток, и цену за единицу учёта: у весового товара это килограмм, у штучного — штука или
            шоубокс, и вес единицы у каждого SKU свой (0,42 кг у помадки, 0,5 у шоколада, 0,23 у микса). Стоимость запаса — штуки × входная цена дилера из прайса
            «{priceList}» (последняя по времени), без пересчёта через вес, поэтому перепутать цену за кг с ценой за штуку здесь нельзя. Вес единицы нужен только для
            килограммов и коробок: он вычисляется по строкам заказов за год (Σ веса ÷ Σ количества); там, где продаж не было, вес взят из названия и помечен «≈».
          </li>
        </ul>
        <Note>Умножать штуки на вес коробки нельзя — это завышает остаток во столько раз, сколько штук в коробке. Коробки считаются только там, где в коробке целое число единиц учёта.</Note>
      </Section>
    </SalesFrame>
  );
}
