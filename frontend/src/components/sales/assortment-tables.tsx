"use client";

import { DataTable, NameCell, type Column } from "./DataTable";
import { Note, Section } from "./bits";
import { kg, money, monthShort, num, pct } from "@/lib/sales/format";
import type { AgentAssortment, AssortmentView, ExportView, PrimaryView, ProductRow, StockItem, StockStatus, StockView, StoreView } from "@/lib/sales/types";

const withQuery = (path: string, query: string) => (query ? `${path}?${query}` : path);

/** Название с переносом: длинные наименования товаров иначе растягивают таблицу и уводят цифры за край. */
function WrapName({ name, sub }: { name: string; sub?: string | null }) {
  return (
    <span className="block max-w-[320px] whitespace-normal">
      <NameCell name={name} sub={sub} />
    </span>
  );
}

const categoryCell = (name: string, inReport = true) => (
  <span className={inReport ? "text-ink-2" : "text-ink-3"} title={inReport ? undefined : "вне категорий отчёта"}>
    {name}
    {!inReport && " *"}
  </span>
);

// ---------- Товары (агент, экспорт, ассортимент) ----------

export function ProductsTable({ rows, title = "Товары", hint = "по выручке за месяц" }: { rows: ProductRow[]; title?: string; hint?: string }) {
  const columns: Column<ProductRow>[] = [
    { key: "name", label: "Продукт", value: (r) => r.name, render: (r) => <WrapName name={r.name} sub={r.code ? `код ${r.code}` : null} /> },
    { key: "cat", label: "Категория", value: (r) => r.category, render: (r) => categoryCell(r.category, r.inReport) },
    { key: "kg", label: "Факт, кг", align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "revenue", label: "Выручка", align: "right", value: (r) => r.revenue, render: (r) => money(r.revenue) },
    { key: "share", label: "Доля", align: "right", value: (r) => r.share, render: (r) => pct(r.share, 1) },
    { key: "akb", label: "ТТ", align: "right", value: (r) => r.akb, render: (r) => num(r.akb) },
    { key: "dist", label: "Дистрибуция", align: "right", value: (r) => r.distribution, render: (r) => pct(r.distribution, 1) },
  ];
  return (
    <DataTable
      title={title}
      hint={hint}
      columns={columns}
      rows={rows}
      rowKey={(r) => String(r.productId)}
      limit={30}
      note="Доля — от выручки набора. ТТ — точек, купивших товар; дистрибуция — их доля от всех ТТ с покупкой. * — тип товара вне восьми категорий отчёта."
    />
  );
}

// ---------- Карточка агента: магазины и «отстаёт по товарам» ----------

export function AgentStoresTable({ rows, agentId, query }: { rows: AgentAssortment["stores"]; agentId: number; query: string }) {
  type Row = AgentAssortment["stores"][number];
  const columns: Column<Row>[] = [
    { key: "name", label: "Магазин", value: (r) => r.name, render: (r) => <WrapName name={r.name} sub={`№ ${r.marketId}`} /> },
    { key: "kg", label: "Факт, кг", align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "revenue", label: "Выручка", align: "right", value: (r) => r.revenue, render: (r) => money(r.revenue) },
    { key: "cats", label: "Категорий", align: "right", value: (r) => r.categories, render: (r) => num(r.categories) },
    { key: "pos", label: "Позиций", align: "right", value: (r) => r.positions, render: (r) => num(r.positions) },
    { key: "share", label: "Доля", align: "right", value: (r) => r.share, render: (r) => pct(r.share, 1) },
  ];
  const base = new URLSearchParams(query);
  base.set("agent", String(agentId));
  return (
    <DataTable
      title="Магазины"
      hint="клик по строке — что он туда продал"
      columns={columns}
      rows={rows}
      rowKey={(r) => String(r.marketId)}
      rowHref={(r) => `/sales/stores/${r.marketId}?${base}`}
      limit={30}
    />
  );
}

export function LaggingTable({ rows }: { rows: AgentAssortment["lagging"] }) {
  type Row = AgentAssortment["lagging"][number];
  const columns: Column<Row>[] = [
    { key: "name", label: "Продукт", value: (r) => r.name, render: (r) => <WrapName name={r.name} /> },
    { key: "cat", label: "Категория", value: (r) => r.category },
    { key: "tt", label: "ТТ у ТП", align: "right", value: (r) => r.agentAkb, render: (r) => num(r.agentAkb) },
    { key: "own", label: "У ТП", align: "right", value: (r) => r.agentDistribution, render: (r) => <span className="text-bad">{pct(r.agentDistribution)}</span> },
    { key: "region", label: "В регионе", align: "right", value: (r) => r.regionDistribution, render: (r) => pct(r.regionDistribution) },
    { key: "rev", label: "Выручка региона", align: "right", value: (r) => r.regionRevenue, render: (r) => money(r.regionRevenue) },
  ];
  return (
    <DataTable
      title="Отстаёт по товарам"
      hint="регион ставит шире, чем этот ТП"
      columns={columns}
      rows={rows}
      rowKey={(r) => String(r.productId)}
      empty="Нет товаров, которые регион ставит заметно шире"
      note="Товар попадает сюда, если в регионе его покупает не меньше 15% точек, а у этого ТП — меньше половины от доли региона. Дистрибуция — доля ТТ с покупкой, купивших товар."
    />
  );
}

// ---------- Магазин ----------

export function StoreProductsTable({ rows }: { rows: StoreView["products"] }) {
  type Row = StoreView["products"][number];
  const columns: Column<Row>[] = [
    { key: "name", label: "Продукт", value: (r) => r.name, render: (r) => <WrapName name={r.name} sub={r.code ? `код ${r.code}` : null} /> },
    { key: "cat", label: "Категория", value: (r) => r.category },
    { key: "kg", label: "Факт, кг", align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "revenue", label: "Выручка", align: "right", value: (r) => r.revenue, render: (r) => money(r.revenue) },
  ];
  return <DataTable title="Что продано в эту точку" hint="за месяц, возвраты вычтены" columns={columns} rows={rows} rowKey={(r) => String(r.productId)} />;
}

// ---------- Экспорт ----------

export function ExportMarketsTable({ rows }: { rows: ExportView["markets"] }) {
  type Row = ExportView["markets"][number];
  const columns: Column<Row>[] = [
    { key: "name", label: "Покупатель", value: (r) => r.name, render: (r) => <WrapName name={r.name} sub={`№ ${r.marketId}`} /> },
    { key: "kg", label: "Факт, кг", align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "revenue", label: "Выручка", align: "right", value: (r) => r.revenue, render: (r) => money(r.revenue) },
    { key: "orders", label: "Заказов", align: "right", value: (r) => r.orders, render: (r) => num(r.orders) },
    { key: "prev", label: "Прошл. мес., кг", align: "right", value: (r) => r.prevMonthKg, render: (r) => kg(r.prevMonthKg) },
  ];
  return <DataTable title="Покупатели" hint="торговые точки филиала «Завод»" columns={columns} rows={rows} rowKey={(r) => String(r.marketId)} />;
}

export function ExportAgentsTable({ rows }: { rows: ExportView["agents"] }) {
  type Row = ExportView["agents"][number];
  const columns: Column<Row>[] = [
    { key: "name", label: "ТП", value: (r) => r.name, render: (r) => <WrapName name={r.name} sub={r.agentId ? `ID ${r.agentId}` : null} /> },
    { key: "kg", label: "Факт, кг", align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "revenue", label: "Выручка", align: "right", value: (r) => r.revenue, render: (r) => money(r.revenue) },
    { key: "markets", label: "Точек", align: "right", value: (r) => r.markets, render: (r) => num(r.markets) },
  ];
  return <DataTable title="Торговые представители" columns={columns} rows={rows} rowKey={(r) => String(r.agentId ?? "none")} />;
}

// ---------- Ассортимент ----------

export function AssortmentRegionsTable({ rows, query }: { rows: AssortmentView["regions"]; query: string }) {
  type Row = AssortmentView["regions"][number];
  const columns: Column<Row>[] = [
    { key: "name", label: "Регион", value: (r) => r.name, render: (r) => <WrapName name={r.name} /> },
    { key: "kg", label: "Факт, кг", align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "revenue", label: "Выручка", align: "right", value: (r) => r.revenue, render: (r) => money(r.revenue) },
    { key: "sku", label: "SKU идёт", align: "right", value: (r) => r.skuSelling, render: (r) => num(r.skuSelling) },
    { key: "no", label: "Не возят", align: "right", value: (r) => r.skuNotCarried, render: (r) => <span className={r.skuNotCarried ? "text-warn" : ""}>{num(r.skuNotCarried)}</span> },
    { key: "lost", label: "Пропало", align: "right", value: (r) => r.skuLost, render: (r) => <span className={r.skuLost ? "text-bad" : ""}>{num(r.skuLost)}</span> },
    { key: "akb", label: "ТТ", align: "right", value: (r) => r.akb, render: (r) => num(r.akb) },
  ];
  return (
    <DataTable
      title="По регионам"
      hint="клик — открыть регион"
      columns={columns}
      rows={rows}
      rowKey={(r) => r.id}
      rowHref={(r) => withQuery(`/sales/regions/${r.id}`, query)}
      note="«SKU идёт» — сколько артикулов категорий отчёта из тех, что продаются по республике в этом месяце, есть в регионе. «Не возят» — остальные. «Пропало» — продавались в регионе в прошлом месяце, в этом нет. ТТ — точки региона с покупкой."
    />
  );
}

const levelClass = { none: "bg-bad-soft text-bad", low: "bg-warn-soft text-warn", ok: "text-ink" } as const;

export function AssortmentMatrix({ data }: { data: AssortmentView }) {
  if (data.matrix.length === 0) return null;
  return (
    <Section title="Товар по регионам" hint="доля точек, где товар стоит">
      <div className="-mx-4 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
        <table className="w-full min-w-max border-collapse text-xs">
          <thead>
            <tr className="border-b border-line">
              <th className="sticky left-0 z-10 bg-surface px-2 py-2 text-left text-[11px] font-semibold uppercase tracking-wide text-ink-3">Продукт</th>
              <th className="px-2 py-2 text-right text-[11px] font-semibold uppercase tracking-wide text-ink-3">Всего</th>
              {data.matrixRegions.map((r) => (
                <th key={r.id} className="px-2 py-2 text-right text-[11px] font-semibold uppercase tracking-wide text-ink-3">
                  {r.name}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {data.matrix.map((row) => (
              <tr key={row.productId} className="border-b border-line">
                <td className="sticky left-0 z-10 max-w-[280px] truncate bg-surface px-2 py-1.5 text-ink" title={`${row.name} · ${row.category}`}>
                  {row.name}
                </td>
                <td className="px-2 py-1.5 text-right tabular-nums text-ink-2">{pct(row.averageDistribution)}</td>
                {row.cells.map((c) => (
                  <td key={c.regionId} className="px-1 py-1">
                    <span className={`block rounded px-1.5 py-0.5 text-right tabular-nums ${levelClass[c.level]}`}>
                      {c.level === "none" ? "нет" : pct(c.distribution)}
                    </span>
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Note>
        Красным — товара в регионе нет вовсе, оранжевым — стоит меньше чем в 40% от своей средней дистрибуции по регионам. «Всего» — средняя по регионам,
        где товар есть. Показаны 25 товаров категорий отчёта с наибольшей выручкой.
      </Note>
    </Section>
  );
}

// ---------- Остатки ----------

const stockStatus: Record<StockStatus, { label: string; cls: string }> = {
  deficit: { label: "дефицит", cls: "bg-bad-soft text-bad" },
  overstock: { label: "затоварка", cls: "bg-warn-soft text-warn" },
  dead: { label: "не продаётся", cls: "bg-muted text-ink-2" },
  ok: { label: "норма", cls: "bg-ok-soft text-ok" },
  unknown: { label: "нет веса", cls: "bg-muted text-ink-3" },
};

export function StockTable({ data, unit }: { data: StockView; unit: "kg" | "boxes" | "pieces" }) {
  const value = (r: StockItem) => (unit === "boxes" ? r.boxes : unit === "pieces" ? r.pieces : r.kg);
  const fmt = (v: number | null) => (unit === "kg" ? kg(v) : num(v, unit === "boxes" ? 1 : 0));
  const columns: Column<StockItem>[] = [
    { key: "name", label: "Наименование", value: (r) => r.name, render: (r) => <WrapName name={r.name} sub={r.code ? `код ${r.code}` : null} /> },
    { key: "cat", label: "Категория", value: (r) => r.category, render: (r) => categoryCell(r.category, r.inReport) },
    {
      key: "unit",
      label: "Вес штуки",
      align: "right",
      value: (r) => r.unitKg,
      render: (r) => <span title={r.boxNote}>{r.unitKg == null ? "—" : `${num(r.unitKg, 3)} кг`}</span>,
    },
    { key: "stock", label: unit === "boxes" ? "Коробок" : unit === "pieces" ? "Штук" : "Остаток, кг", align: "right", value, render: (r) => fmt(value(r)) },
    { key: "perDay", label: "Продажи в день, кг", align: "right", value: (r) => r.kgPerDay, render: (r) => kg(r.kgPerDay) },
    { key: "days", label: "Хватит, дн.", align: "right", value: (r) => r.daysOfCover, render: (r) => num(r.daysOfCover) },
    { key: "need", label: "Запас на 15 дн., кг", align: "right", value: (r) => r.need15Kg, render: (r) => kg(r.need15Kg) },
    {
      key: "status",
      label: "Статус",
      value: (r) => r.status,
      render: (r) => <span className={`inline-block rounded-full px-2 py-0.5 text-xs font-medium ${stockStatus[r.status].cls}`}>{stockStatus[r.status].label}</span>,
    },
    { key: "factory", label: "Завод", align: "right", value: (r) => r.factory?.kg ?? null, render: (r) => kg(r.factory?.kg ?? null) },
  ];
  return (
    <DataTable
      title="Остатки по товарам"
      hint={unit === "boxes" ? "коробки — только там, где вес коробки подтверждён" : undefined}
      columns={columns}
      rows={data.items}
      rowKey={(r) => String(r.productId)}
      note="Linko хранит остаток в штуках (единицах учёта). Кг = штуки × вес штуки; вес штуки — Σ веса ÷ Σ количества по строкам заказов за год. Коробки = кг ÷ вес коробки из названия, если в коробке целое число штук (наведите на вес штуки — видно, откуда вес коробки). Дефицит — меньше 15 дней продаж, затоварка — больше 30, «не продаётся» — остаток есть, продаж за базовый период нет. Остаток завода в итог страны не входит."
    />
  );
}

export function OtherStocksTable({ rows }: { rows: StockView["otherStocks"] }) {
  type Row = StockView["otherStocks"][number];
  const columns: Column<Row>[] = [
    { key: "name", label: "Склад", value: (r) => r.name, render: (r) => <WrapName name={r.name} sub={`ID ${r.stockId}`} /> },
    { key: "items", label: "Товаров", align: "right", value: (r) => r.items, render: (r) => num(r.items) },
    { key: "pieces", label: "Штук", align: "right", value: (r) => r.pieces, render: (r) => num(r.pieces) },
    { key: "kg", label: "Кг", align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
  ];
  return (
    <DataTable
      title="Склады вне регионов"
      hint="в остаток регионов не входят"
      columns={columns}
      rows={rows}
      rowKey={(r) => String(r.stockId)}
      empty="Все склады с остатком сопоставлены с регионами"
      note="Склады, название которых не совпадает ни с одним регионом (старые, интеграционные, «Основной», «Оптом»…). Их остаток не складывается в регионы автоматически — иначе он раздувал бы остаток дилеров."
    />
  );
}

// ---------- Первичка ----------

export function PrimaryDealersTable({ data }: { data: PrimaryView }) {
  type Row = PrimaryView["dealerRows"][number];
  const columns: Column<Row>[] = [
    { key: "name", label: "Дилер (склад)", value: (r) => r.name, render: (r) => <WrapName name={r.name} sub={r.regionId ? null : "склад без региона"} /> },
    { key: "kg", label: "Отгружено, кг", align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "sum", label: "Сумма", align: "right", value: (r) => r.sum, render: (r) => money(r.sum) },
    { key: "share", label: "Доля", align: "right", value: (r) => r.share, render: (r) => pct(r.share, 1) },
    { key: "count", label: "Отгрузок", align: "right", value: (r) => r.shipments, render: (r) => num(r.shipments) },
  ];
  return <DataTable title="По дилерам" hint="склады, куда отгружал завод" columns={columns} rows={data.dealerRows} rowKey={(r) => String(r.stockId)} />;
}

export function PrimaryCategoriesTable({ data }: { data: PrimaryView }) {
  type Row = PrimaryView["categories"][number];
  const columns: Column<Row>[] = [
    { key: "name", label: "Категория", value: (r) => r.name, render: (r) => categoryCell(r.name, r.inReport) },
    ...Array.from({ length: data.month }, (_, i) => ({
      key: `m${i + 1}`,
      label: monthShort(i + 1),
      align: "right" as const,
      value: (r: Row) => r.months[i],
      render: (r: Row) => kg(r.months[i] || null),
    })),
    { key: "share", label: "Доля в месяце", align: "right", value: (r) => r.share, render: (r) => pct(r.share, 1) },
  ];
  return <DataTable title="По категориям" hint="кг по месяцам года" columns={columns} rows={data.categories} rowKey={(r) => r.name} />;
}

export function PrimaryItemsTable({ data }: { data: PrimaryView }) {
  type Row = PrimaryView["items"][number];
  const columns: Column<Row>[] = [
    { key: "name", label: "Наименование", value: (r) => r.name, render: (r) => <WrapName name={r.name} sub={r.code ? `код ${r.code}` : null} /> },
    { key: "cat", label: "Категория", value: (r) => r.category },
    { key: "pieces", label: "Штук", align: "right", value: (r) => r.pieces, render: (r) => num(r.pieces) },
    { key: "kg", label: "Вес, кг", align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "sum", label: "Сумма", align: "right", value: (r) => r.sum, render: (r) => money(r.sum) },
    { key: "perKg", label: "Цена за кг", align: "right", value: (r) => r.sumPerKg, render: (r) => money(r.sumPerKg) },
  ];
  return <DataTable title="Товары" hint="за месяц, по убыванию веса" columns={columns} rows={data.items} rowKey={(r) => String(r.productId)} />;
}

export function PrimaryCalendar({ data }: { data: PrimaryView }) {
  const days = Array.from({ length: data.daysInMonth }, (_, i) => i + 1).filter((d) => data.dealerRows.some((r) => r.days[d - 1] != null));
  if (days.length === 0) return null;
  const total = (d: number) => data.dealerRows.reduce((s, r) => s + (r.days[d - 1] ?? 0), 0);
  return (
    <Section title="Календарь отгрузок" hint="кг по дням; показаны только дни с отгрузками">
      <div className="-mx-4 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
        <table className="w-full min-w-max border-collapse text-xs">
          <thead>
            <tr className="border-b border-line">
              <th className="sticky left-0 z-10 bg-surface px-2 py-2 text-left text-[11px] font-semibold uppercase tracking-wide text-ink-3">Дилер</th>
              {days.map((d) => (
                <th key={d} className="px-2 py-2 text-right text-[11px] font-semibold text-ink-3">
                  {d}
                </th>
              ))}
              <th className="px-2 py-2 text-right text-[11px] font-semibold uppercase tracking-wide text-ink-3">Итого</th>
            </tr>
          </thead>
          <tbody>
            {data.dealerRows.map((r) => (
              <tr key={r.stockId} className="border-b border-line">
                <td className="sticky left-0 z-10 bg-surface px-2 py-1.5 text-ink">{r.name}</td>
                {days.map((d) => (
                  <td key={d} className="px-2 py-1.5 text-right tabular-nums text-ink-2">
                    {r.days[d - 1] == null ? <span className="text-ink-3">·</span> : kg(r.days[d - 1])}
                  </td>
                ))}
                <td className="px-2 py-1.5 text-right font-semibold tabular-nums text-ink">{kg(r.kg)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="bg-muted">
              <td className="sticky left-0 z-10 bg-muted px-2 py-1.5 font-semibold text-ink">Итого</td>
              {days.map((d) => (
                <td key={d} className="px-2 py-1.5 text-right font-semibold tabular-nums text-ink">
                  {kg(total(d))}
                </td>
              ))}
              <td className="px-2 py-1.5 text-right font-semibold tabular-nums text-ink">{kg(data.kg)}</td>
            </tr>
          </tfoot>
        </table>
      </div>
    </Section>
  );
}
