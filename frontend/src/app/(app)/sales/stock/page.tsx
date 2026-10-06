import Link from "next/link";
import { Alert, CollapsedSections, KpiTile, Note, Section } from "@/components/sales/bits";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { apiGet } from "@/lib/server-api";
import { date, dateTime, kg, money, monthLabel, num, pct } from "@/lib/sales/format";
import { param, queryWith, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { StockView } from "@/lib/sales/types";
import { PackFilter, StockScopeSelect, StockSearch } from "./filters";
import { OtherStocksTable, StockExcludedTable, StockMatrix, StockTable, type StockUnit } from "./tables";

export const metadata = { title: "Рекомендуемый остаток · Продажи" };

const units: { key: StockUnit; label: string }[] = [
  { key: "kg", label: "кг" },
  { key: "boxes", label: "коробки" },
  { key: "sum", label: "деньги" },
  { key: "pieces", label: "штуки" },
];

const views = [
  { key: "list", label: "Список" },
  { key: "matrix", label: "По регионам" },
] as const;

const tops = [
  { key: "all", label: "Все SKU" },
  { key: "only", label: "Только ТОП" },
  { key: "not", label: "Кроме ТОПа" },
] as const;

const statuses = [
  { key: "all", label: "Все" },
  { key: "deficit", label: "Дефицит" },
  { key: "ok", label: "Норма" },
  { key: "overstock", label: "Затоварка" },
  { key: "dead", label: "Не продаётся" },
] as const;

function Chips<T extends string>({ items, current, href, title }: { items: readonly { key: T; label: string }[]; current: T; href: (key: T) => string; title?: string }) {
  return (
    <div className="no-scrollbar flex max-w-full overflow-x-auto rounded-lg border border-line text-sm" title={title}>
      {items.map((i) => (
        <Link key={i.key} href={href(i.key)} className={`shrink-0 whitespace-nowrap px-3 py-1.5 max-lg:py-2.5 ${current === i.key ? "bg-accent text-white" : "bg-surface text-ink hover:bg-muted"}`}>
          {i.label}
        </Link>
      ))}
    </div>
  );
}

function Fact({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0">
      <div className="text-[10px] font-medium uppercase tracking-[0.08em] text-ink-3">{label}</div>
      <div className="font-medium tabular-nums text-ink">{value}</div>
    </div>
  );
}

/** Изменение скорости поправкой на аутсток: «+1,9%». */
const signedPct = (share: number | null) => (share == null ? "—" : `${share >= 0 ? "+" : ""}${pct(share, 1)}`);

/**
 * «Рекомендуемый остаток» (DOC-rules §9.4, DOC-filters §4): запас дилеров по складам на дату снимка Linko — хватит ли до поставки, сколько
 * заказать и сколько он стоит. Это снимок на дату, а не итог месяца, поэтому периода нет. Охват, поиск, категории, фасовка, ТОП, статус,
 * вид и единица — параметры адреса; строки, плитки и итоги по отфильтрованному набору считает сервер.
 */
export default async function StockPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const sp = await searchParams;
  const legacyRegion = param(sp, "region");
  const scope = param(sp, "scope") ?? (legacyRegion ? `region:${legacyRegion}` : undefined);
  const api = queryWith("", { scope, q: param(sp, "q"), cat: param(sp, "cat"), pack: param(sp, "pack"), top: param(sp, "top"), status: param(sp, "status") });
  const data = await apiGet<StockView>(withQuery("/api/sales/stock", api), "/sales/stock");

  const unit: StockUnit = units.find((u) => u.key === param(sp, "unit"))?.key ?? "kg";
  const view = views.find((v) => v.key === param(sp, "view"))?.key ?? "list";

  // Ссылки фильтров — с нормализованными значениями из ответа: умолчания в адрес не пишутся.
  const base = {
    scope: data.scope === "country" ? null : data.scope,
    q: data.query,
    cat: data.selectedCategories.join(",") || null,
    pack: data.selectedPacks.join(",") || null,
    top: data.top === "all" ? null : data.top,
    status: data.status === "all" ? null : data.status,
    unit: unit === "kg" ? null : unit,
    view: view === "list" ? null : view,
  };
  const href = (changes: Partial<Record<keyof typeof base, string | null>>) => withQuery("/sales/stock", queryWith("", { ...base, ...changes }));
  const toggleCategory = (name: string) =>
    href({ cat: (data.selectedCategories.includes(name) ? data.selectedCategories.filter((c) => c !== name) : [...data.selectedCategories, name]).join(",") || null });
  // Плитка категории — одна категория: клик по единственной выбранной снимает выбор.
  const tileHref = (name: string) => href({ cat: data.selectedCategories.length === 1 && data.selectedCategories[0] === name ? null : name });
  const filtered = Boolean(base.q || base.cat || base.pack || base.top || base.status);
  // Раскрытая строка и сортировка сбрасываются при смене охвата, категории, ТОПа, статуса, фасовки и вида — поиск их не трогает (DOC-filters §12).
  const tableKey = [data.scope, base.cat, base.top, base.status, base.pack, view].join("|");

  const t = data.totals;
  const f = data.factoryTotals;
  const c = data.correction;
  const plant = data.scope === "plant";
  const exportScope = data.scope === "export";
  const priceList = data.priceList ?? "вход дилеру";
  const corrected = t.rawKgPerDay != null && t.kgPerDay != null && t.rawKgPerDay !== t.kgPerDay;

  return (
    <SalesFrame
      title="Рекомендуемый остаток"
      subtitle={`Запас дилеров по складам: хватит ли до поставки, что заказать и сколько он стоит · ${data.scopeName}`}
      crumbs={[{ label: "Рек. остаток" }]}
      sp={sp}
    >
      <div className="flex flex-wrap items-center gap-3">
        <StockSearch value={data.query ?? ""} />
        <StockScopeSelect data={data} />
        <Chips items={views} current={view} href={(k) => href({ view: k === "list" ? null : k })} />
        <Chips items={units} current={unit} href={(k) => href({ unit: k === "kg" ? null : k })} title="Единица: кг, коробки (где вес коробки известен), деньги по входу дилера, штуки как в Linko" />
      </div>
      <div className="mt-3 flex flex-wrap items-center gap-3">
        {data.topConfigured ? (
          <Chips items={tops} current={data.top} href={(k) => href({ top: k === "all" ? null : k })} title="ТОП — коды товаров из настройки Sales:TopProducts" />
        ) : (
          <span className="rounded-lg border border-dashed border-line px-3 py-1.5 text-xs text-ink-3">ТОП-товары не настроены (Sales:TopProducts)</span>
        )}
        <Chips items={statuses} current={data.status as (typeof statuses)[number]["key"]} href={(k) => href({ status: k === "all" ? null : k })} />
        <PackFilter packs={data.packs} selected={data.selectedPacks} />
        {filtered && (
          <Link href={href({ q: null, cat: null, pack: null, top: null, status: null })} className="ml-auto rounded-lg border border-line px-3 py-1.5 text-sm text-ink hover:bg-muted">
            Сбросить фильтры
          </Link>
        )}
      </div>
      {data.categories.length > 0 && (
        <div className="mt-3 flex flex-wrap gap-1 text-xs">
          <Link href={href({ cat: null })} className={`rounded-full px-2.5 py-1 max-lg:py-2 ${data.selectedCategories.length === 0 ? "bg-accent-soft font-semibold text-ink" : "text-ink-2 hover:bg-muted"}`}>
            все категории
          </Link>
          {data.categories.map((name) => {
            const on = data.selectedCategories.includes(name);
            return (
              <Link key={name} href={toggleCategory(name)} aria-pressed={on} className={`rounded-full px-2.5 py-1 max-lg:py-2 ${on ? "bg-accent-soft font-semibold text-ink" : "text-ink-2 hover:bg-muted"}`}>
                {name}
              </Link>
            );
          })}
        </div>
      )}

      {!data.syncedAt && <Alert>Остатки ещё не загружены из Linko — они появятся после ближайшей синхронизации (или нажмите «Обновить»).</Alert>}

      <div className={`mt-4 grid grid-cols-2 gap-3 lg:grid-cols-3 ${plant ? "min-[87.5rem]:grid-cols-5" : "min-[87.5rem]:grid-cols-6"}`}>
        <KpiTile label="Остаток" value={kg(t.kg)} unit="кг" title="Штуки Linko × вес единицы; коробки — где вес коробки известен">
          {num(t.boxes)} коробок · {num(t.skus)} SKU
        </KpiTile>
        <KpiTile label="Стоимость запаса" value={money(t.valueSum)} unit="сум" title={`Штуки × цена за единицу учёта из прайса «${priceList}», действовавшая на ${date(data.priceAsOf)}`}>
          по входу дилера на {date(data.priceAsOf)}
          {t.withoutPrice > 0 ? ` · без цены ${num(t.withoutPrice)} SKU` : ""}
        </KpiTile>
        <KpiTile
          label="Хватит на"
          value={num(t.daysOfCover, 1)}
          unit="дн."
          tone={exportScope || t.daysOfCover == null ? "muted" : t.daysOfCover < 15 ? "bad" : t.daysOfCover > 30 ? "warn" : "ok"}
          title={plant ? "Скорость завода — продажи всей страны" : "Остаток ÷ продажи в день с поправкой на аутсток"}
        >
          {exportScope ? "у экспорта своей скорости продаж нет" : `${kg(t.kgPerDay)} кг в день${corrected ? ` (без поправки на аутсток ${kg(t.rawKgPerDay)})` : ""}`}
        </KpiTile>
        <KpiTile label="SKU в дефиците" value={num(t.deficit)} tone={exportScope ? "muted" : t.deficit > 0 ? "bad" : "ok"} title="Хватит меньше чем на 15 дней продаж">
          затоварка — {num(t.overstock)}, не продаётся — {num(t.dead)}
        </KpiTile>
        <KpiTile
          label={plant ? "Не хватает на заводе" : "Рек. заказ дилерам"}
          value={kg(t.orderKg)}
          unit="кг"
          tone={exportScope ? "muted" : t.orderKg > 0 ? "accent" : "ok"}
          title={plant ? "Сумма рекомендуемых заказов всех дилеров минус остаток на заводе" : "Скорость с поправкой на аутсток × 15 дней − остаток, вверх до целой коробки; 0 — хватает"}
        >
          {exportScope ? "у экспорта заказа нет" : `${money(t.orderSum)} сум · ${num(t.orderSkus)} SKU${plant ? "" : " · до запаса на 15 дней"}`}
        </KpiTile>
        {!plant && (
          <KpiTile label="Остаток завода" value={kg(f?.kg ?? null)} unit="кг" title={f?.valueSum ? `${money(f.valueSum)} сум по входной цене` : undefined}>
            {data.factory && f
              ? `не хватает ${kg(f.orderKg)} кг (${num(f.orderSkus)} SKU)${f.daysOfCover != null ? ` · хватит на ${num(f.daysOfCover, 1)} дн. по скорости всей страны` : ""} · в итог страны не входит`
              : "склад завода не найден"}
          </KpiTile>
        )}
      </div>
      <p className="mt-3 text-xs text-ink-3">
        Остатки на {dateTime(data.syncedAt)} (снимок Linko {date(data.snapshotDate)}). Скорость — вторичка нетто возвратов за {num(data.velocityDays)} дн. ({date(data.velocityFrom)} –{" "}
        {date(data.velocityTo)}) с поправкой на дни в нуле за {monthLabel(c.year, c.month).toLowerCase()}: по стране {kg(c.rawKgPerDay)} → {kg(c.kgPerDay)} кг в день ({signedPct(c.change)}, пар с
        поправкой — {num(c.pairs)}). Вес единицы — по строкам заказов с {date(data.unitWeightFrom)}; цена — прайс «{priceList}» на {date(data.priceAsOf)}.{" "}
        {t.approxWeight > 0 && `У ${num(t.approxWeight)} SKU продаж за год не было — вес из названия, значения помечены «≈». `}
        {t.withoutPrice > 0 && `${num(t.withoutPrice)} SKU нет в прайсе — в стоимость не вошли. `}
        {data.excludedOutsideReport + data.excludedWithoutWeight > 0 &&
          `Вне таблицы: ${num(data.excludedOutsideReport)} SKU вне категорий отчёта, ${num(data.excludedWithoutWeight)} без веса единицы.`}
      </p>

      {data.categoryTiles.length > 1 && (
        <div className="mt-4 grid grid-cols-2 gap-3 md:grid-cols-3 min-[87.5rem]:grid-cols-5">
          {data.categoryTiles.map((tile) => (
            <Link
              key={tile.name}
              href={tileHref(tile.name)}
              aria-pressed={tile.selected}
              title="Клик — только эта категория; повторный клик снимает выбор"
              className={`block min-w-0 rounded-xl border bg-surface px-4 py-3.5 shadow-sm transition max-lg:px-3.5 max-lg:py-3 ${
                tile.selected ? "border-accent ring-1 ring-accent" : "border-line hover:border-ink-3"
              }`}
            >
              <div className="truncate text-sm font-semibold text-ink">{tile.name}</div>
              <div className="text-xs text-ink-3">{num(tile.skus)} SKU</div>
              <div className="mt-2 text-[22px] font-semibold leading-none tracking-tight text-ink tabular-nums max-lg:text-xl">
                {kg(tile.kg)} <span className="text-xs font-normal text-ink-3">кг</span>
              </div>
              <div className="mt-3 grid grid-cols-2 gap-x-3 gap-y-2 text-xs">
                <Fact label="Хватит, дн." value={num(tile.daysOfCover, 1)} />
                <Fact label="Сумма" value={money(tile.valueSum)} />
                <Fact label="Дефицит" value={`${num(tile.deficit)} SKU`} />
                <Fact label="Рек. заказ" value={`${kg(tile.orderKg)} кг`} />
              </div>
            </Link>
          ))}
        </div>
      )}

      {data.items.length === 0 ? (
        <Alert tone="info" className="mt-6">
          По этим фильтрам товаров нет.{" "}
          <Link href={href({ q: null, cat: null, pack: null, top: null, status: null })} className="text-accent hover:underline">
            Сбросить фильтры
          </Link>
        </Alert>
      ) : view === "matrix" ? (
        <StockMatrix key={tableKey} data={data} unit={unit} />
      ) : (
        <StockTable key={tableKey} data={data} unit={unit} />
      )}

      <CollapsedSections>
        <StockExcludedTable rows={data.excluded} outsideReport={data.excludedOutsideReport} withoutWeight={data.excludedWithoutWeight} />
        <OtherStocksTable rows={data.otherStocks} />
      </CollapsedSections>

      <Section title="Как считается" hint="все данные — из Linko; правила — DOC §9.4">
        <ul className="space-y-2 text-sm text-ink-2">
          <li>
            <b className="text-ink">Остаток</b> — снимок Linko на дату последней загрузки, а не итог месяца, поэтому переключателя периода нет. «Вся страна» — только склады
            дилеров (по одному на регион справочника); склады старых филиалов, «Основной», интеграционные — в «Прочих складах»; завод и экспорт — отдельные охваты и в итог страны
            не входят.
          </li>
          <li>
            <b className="text-ink">Продажи в день</b> — вторичка нетто возвратов за {num(data.velocityDays)} дн. до последнего полного дня в регионе склада, делённая на (
            {num(data.velocityDays)} − дни в нуле этого товара в регионе за последний закрытый месяц, не больше 30): дни, когда товара не было, продаж не занижают. «Хватит» —
            остаток ÷ скорость; дефицит — меньше 15 дней, норма 15–30, затоварка — больше 30, «не продаётся» — остаток лежит, а продаж за базу не было.
          </li>
          <li>
            <b className="text-ink">Рек. заказ дилера</b> = скорость с поправкой × 15 − остаток, вверх до целой коробки (0 — хватает); деньги — по входной цене. <b className="text-ink">Заказ у
            завода</b> = сумма заказов всех дилеров (при любом охвате) − остаток завода: сколько не хватает, чтобы отгрузить всем. Своего запаса «на 15 дней» у завода и экспорта нет.
          </li>
          <li>
            <b className="text-ink">Единица учёта, коробка и цена.</b> Остаток и цена в Linko — за единицу учёта (кг у весового, штука или шоубокс у штучного). Кг = штуки × вес единицы
            (Σ веса ÷ Σ количества по строкам заказов с 1-го числа закрытого месяца, без свежих продаж — за год, без продаж — из названия, «≈»). Коробка — из названия (у весового
            любой вес, у штучного — если в коробке целое число штук), иначе по отгрузкам завода (целыми коробками). Стоимость — штуки × цена прайса «{priceList}», действовавшая на
            начало текущего месяца: повышение, введённое в этом месяце, остаток не переоценивает.
          </li>
        </ul>
        <Note>Умножать штуки на вес коробки нельзя — это завышает остаток во столько раз, сколько штук в коробке. Фильтры применяет сервер: плитки, итоги и карточки категорий — по отфильтрованным строкам.</Note>
      </Section>
    </SalesFrame>
  );
}
