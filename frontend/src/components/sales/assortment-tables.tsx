"use client";

import { useT } from "@/i18n/client";
import type { T } from "@/i18n/core";
import { DataTable, NameCell, type Column } from "./DataTable";
import { Note, Section } from "./bits";
import { regionColumns } from "./categories";
import { TopChip } from "./outstock";
import { kg, money, monthShort, num, pct, plural, productsLabel } from "@/lib/sales/format";
import type { AgentAssortment, AssortmentView, ExportView, ProductRow, StoreView } from "@/lib/sales/types";

const withQuery = (path: string, query: string) => (query ? `${path}?${query}` : path);

/** Название с переносом: длинные наименования товаров иначе растягивают таблицу и уводят цифры за край. top — метка «ТОП». */
function WrapName({ name, sub, top = false }: { name: string; sub?: string | null; top?: boolean }) {
  return (
    <span className="flex max-w-[320px] items-start gap-1.5 whitespace-normal">
      {top && (
        <span className="mt-0.5">
          <TopChip />
        </span>
      )}
      <NameCell name={name} sub={sub} />
    </span>
  );
}

const categoryCell = (name: string, inReport: boolean, t: T) => (
  <span className={inReport ? "text-ink-2" : "text-ink-3"} title={inReport ? undefined : t("вне категорий отчёта")}>
    {t(name)}
    {!inReport && " *"}
  </span>
);

// ---------- Товары (агент, экспорт, ассортимент) ----------

/**
 * Товары категорий отчёта; outsideReport — сколько товаров других типов Linko (бонус, подарки) в таблицу не вошло; quality — на странице
 * есть блок «Качество данных» с их весом и суммой (иначе примечание просто говорит, что они исключены).
 */
export function ProductsTable({
  rows,
  title = "Товары",
  hint = "по выручке за месяц",
  outsideReport,
  quality = false,
}: {
  rows: ProductRow[];
  title?: string;
  hint?: string;
  outsideReport?: number;
  quality?: boolean;
}) {
  const t = useT();
  const outside = (n: number) =>
    `${t(
      plural(n, [
        "Ещё {items} других типов Linko (бонус, подарки) в таблицу не входит",
        "Ещё {items} других типов Linko (бонус, подарки) в таблицу не входят",
        "Ещё {items} других типов Linko (бонус, подарки) в таблицу не входят",
      ]),
      { items: productsLabel(n, t) },
    )}${quality ? t(" — их вес и сумма в «Качестве данных»") : ""}.`;
  const columns: Column<ProductRow>[] = [
    { key: "name", label: t("Продукт"), value: (r) => r.name, render: (r) => <WrapName name={r.name} sub={r.code ? t("код {code}", { code: r.code }) : null} top={r.isTop} /> },
    { key: "cat", label: t("Категория"), value: (r) => r.category, render: (r) => categoryCell(r.category, r.inReport, t) },
    { key: "kg", label: t("Факт, кг"), align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "revenue", label: t("Выручка"), align: "right", value: (r) => r.revenue, render: (r) => money(r.revenue, t) },
    { key: "share", label: t("Доля"), align: "right", value: (r) => r.share, render: (r) => pct(r.share, 1) },
    { key: "akb", label: t("ТТ"), align: "right", value: (r) => r.akb, render: (r) => num(r.akb) },
    { key: "dist", label: t("Дистрибуция"), align: "right", value: (r) => r.distribution, render: (r) => pct(r.distribution, 1) },
  ];
  return (
    <DataTable
      title={t(title)}
      hint={t(hint)}
      columns={columns}
      rows={rows}
      rowKey={(r) => String(r.productId)}
      limit={30}
      note={`${t(
        "Только категории отчёта. Доля — от выручки этих товаров. ТТ — точки с положительной строкой товара (кг или сумма больше нуля); дистрибуция — их доля от всех ТТ с покупкой. «ТОП» — товар из списка ТОП.",
      )}${outsideReport ? ` ${outside(outsideReport)}` : ""}`}
    />
  );
}

// ---------- Карточка агента: магазины и «отстаёт по товарам» ----------

export function AgentStoresTable({ rows, agentId, query }: { rows: AgentAssortment["stores"]; agentId: number; query: string }) {
  const t = useT();
  type Row = AgentAssortment["stores"][number];
  const columns: Column<Row>[] = [
    { key: "name", label: t("Магазин"), value: (r) => r.name, render: (r) => <WrapName name={r.name} sub={`№ ${r.marketId}`} /> },
    { key: "kg", label: t("Факт, кг"), align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "revenue", label: t("Выручка"), align: "right", value: (r) => r.revenue, render: (r) => money(r.revenue, t) },
    { key: "cats", label: t("Категорий"), align: "right", value: (r) => r.categories, render: (r) => num(r.categories) },
    { key: "pos", label: t("Позиций"), align: "right", value: (r) => r.positions, render: (r) => num(r.positions) },
    { key: "share", label: t("Доля"), align: "right", value: (r) => r.share, render: (r) => pct(r.share, 1) },
  ];
  const base = new URLSearchParams(query);
  base.set("agent", String(agentId));
  return (
    <DataTable
      title={t("Магазины")}
      hint={t("клик по строке — что он туда продал")}
      columns={columns}
      rows={rows}
      rowKey={(r) => String(r.marketId)}
      rowHref={(r) => `/sales/stores/${r.marketId}?${base}`}
      limit={30}
    />
  );
}

export function LaggingTable({ rows }: { rows: AgentAssortment["lagging"] }) {
  const t = useT();
  type Row = AgentAssortment["lagging"][number];
  const columns: Column<Row>[] = [
    { key: "name", label: t("Продукт"), value: (r) => r.name, render: (r) => <WrapName name={r.name} /> },
    { key: "cat", label: t("Категория"), value: (r) => r.category, render: (r) => t(r.category) },
    { key: "tt", label: t("ТТ у ТП"), align: "right", value: (r) => r.agentAkb, render: (r) => num(r.agentAkb) },
    { key: "own", label: t("У ТП"), align: "right", value: (r) => r.agentDistribution, render: (r) => <span className="text-bad">{pct(r.agentDistribution)}</span> },
    { key: "region", label: t("В регионе"), align: "right", value: (r) => r.regionDistribution, render: (r) => pct(r.regionDistribution) },
    { key: "rev", label: t("Выручка региона"), align: "right", value: (r) => r.regionRevenue, render: (r) => money(r.regionRevenue, t) },
  ];
  return (
    <DataTable
      title={t("Отстаёт по товарам")}
      hint={t("регион ставит шире, чем этот ТП")}
      columns={columns}
      rows={rows}
      rowKey={(r) => String(r.productId)}
      empty={t("Нет товаров, которые регион ставит заметно шире")}
      note={t("Товар попадает сюда, если в регионе его покупает не меньше 15% точек, а у этого ТП — меньше половины от доли региона. Дистрибуция — доля ТТ с покупкой, купивших товар.")}
    />
  );
}

// ---------- Магазин ----------

export function StoreProductsTable({ rows }: { rows: StoreView["products"] }) {
  const t = useT();
  type Row = StoreView["products"][number];
  const columns: Column<Row>[] = [
    { key: "name", label: t("Продукт"), value: (r) => r.name, render: (r) => <WrapName name={r.name} sub={r.code ? t("код {code}", { code: r.code }) : null} top={r.isTop} /> },
    { key: "cat", label: t("Категория"), value: (r) => r.category, render: (r) => t(r.category) },
    { key: "kg", label: t("Факт, кг"), align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "revenue", label: t("Выручка"), align: "right", value: (r) => r.revenue, render: (r) => money(r.revenue, t) },
  ];
  return (
    <DataTable
      title={t("Что продано в эту точку")}
      hint={t("за месяц, возвраты вычтены")}
      columns={columns}
      rows={rows}
      rowKey={(r) => String(r.productId)}
      note={t("«ТОП» — товар из списка ТОП.")}
    />
  );
}

// ---------- Экспорт ----------

export function ExportMarketsTable({ rows }: { rows: ExportView["markets"] }) {
  const t = useT();
  type Row = ExportView["markets"][number];
  const columns: Column<Row>[] = [
    { key: "name", label: t("Покупатель"), value: (r) => r.name, render: (r) => <WrapName name={r.name} sub={`№ ${r.marketId}`} /> },
    { key: "kg", label: t("Факт, кг"), align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "revenue", label: t("Выручка"), align: "right", value: (r) => r.revenue, render: (r) => money(r.revenue, t) },
    { key: "orders", label: t("Заказов"), align: "right", value: (r) => r.orders, render: (r) => num(r.orders) },
    { key: "prev", label: t("Прошл. мес., кг"), align: "right", value: (r) => r.prevMonthKg, render: (r) => kg(r.prevMonthKg) },
  ];
  return <DataTable title={t("Покупатели")} hint={t("торговые точки филиала «Завод»")} columns={columns} rows={rows} rowKey={(r) => String(r.marketId)} />;
}

export function ExportAgentsTable({ rows }: { rows: ExportView["agents"] }) {
  const t = useT();
  type Row = ExportView["agents"][number];
  const columns: Column<Row>[] = [
    { key: "name", label: t("ТП"), value: (r) => r.name, render: (r) => <WrapName name={r.agentId ? r.name : t(r.name)} sub={r.agentId ? `ID ${r.agentId}` : null} /> },
    { key: "kg", label: t("Факт, кг"), align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "revenue", label: t("Выручка"), align: "right", value: (r) => r.revenue, render: (r) => money(r.revenue, t) },
    { key: "markets", label: t("Точек"), align: "right", value: (r) => r.markets, render: (r) => num(r.markets) },
  ];
  return <DataTable title={t("Торговые представители")} columns={columns} rows={rows} rowKey={(r) => String(r.agentId ?? "none")} />;
}

// ---------- Ассортимент ----------

/** «По регионам» вкладки «Ассортимент» — у охвата из двух и больше регионов (сервер отдаёт пустой список, если регион один). */
export function AssortmentRegionsTable({ rows, query }: { rows: AssortmentView["regions"]; query: string }) {
  const t = useT();
  return (
    <DataTable
      title={t("По регионам")}
      hint={t("клик — открыть регион")}
      columns={regionColumns(t)}
      rows={rows}
      rowKey={(r) => r.id}
      rowHref={(r) => withQuery(`/sales/regions/${r.id}`, query)}
      note={t("«SKU идёт» — сколько артикулов категорий отчёта из тех, что продаются по республике в этом месяце, есть в регионе (точка с положительной строкой). «Не возят» — остальные. «Пропало» — продавались в регионе в прошлом месяце, в этом нет. ТТ — точки региона с покупкой. «Нет данных» — в регионе за месяц ни одной покупки: это почти всегда дыра в выгрузке, а не регион, который ничего не возит.")}
    />
  );
}

const levelClass = { none: "bg-bad-soft text-bad", low: "bg-warn-soft text-warn", ok: "text-ink" } as const;

/** Матрица «товар × регион»: цвет клетки (нет / меньше 40% средней / норма) считает сервер. */
export function AssortmentMatrix({ data }: { data: AssortmentView }) {
  const t = useT();
  if (data.matrix.length === 0) return null;
  return (
    <Section title={t("Товар по регионам")} hint={t("доля точек, где товар стоит")}>
      <div className="-mx-4 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
        <table className="w-full min-w-max border-collapse text-xs">
          <thead>
            <tr className="border-b border-line">
              <th className="sticky left-0 z-10 bg-surface-solid px-2 py-2 text-left text-[11px] font-semibold uppercase tracking-wide text-ink-3">{t("Продукт")}</th>
              <th className="px-2 py-2 text-right text-[11px] font-semibold uppercase tracking-wide text-ink-3">{t("Всего")}</th>
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
                <td className="sticky left-0 z-10 max-w-[280px] bg-surface-solid px-2 py-1.5 text-ink" title={`${row.name} · ${t(row.category)}`}>
                  <span className="flex items-center gap-1.5">
                    {row.isTop && <TopChip />}
                    <span className="truncate">{row.name}</span>
                  </span>
                </td>
                <td className="px-2 py-1.5 text-right tabular-nums text-ink-2">{pct(row.averageDistribution)}</td>
                {row.cells.map((c) => (
                  <td key={c.regionId} className="px-1 py-1" title={t("{n} ТТ с товаром", { n: num(c.tt) })}>
                    <span className={`block rounded px-1.5 py-0.5 text-right tabular-nums ${levelClass[c.level]}`}>
                      {c.level === "none" ? t("нет") : pct(c.distribution)}
                    </span>
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Note>
        {t(
          "Клетка — дистрибуция: доля ТТ региона с покупкой, у которых строка товара положительна. «Всего» — дистрибуция по всему охвату: все ТТ с товаром ÷ все ТТ с покупкой в регионах матрицы. Красным — товара в регионе нет вовсе, оранжевым — стоит меньше чем в 40% от «Всего». Показаны 20 товаров категорий отчёта с наибольшей выручкой; матрица строится, если продажи есть хотя бы в двух регионах.",
        )}
      </Note>
    </Section>
  );
}

// Таблицы «Рек. остатка» — в app/(app)/sales/stock/tables.tsx.
