"use client";

import Link from "next/link";
import { DataTable, NameCell, type Column } from "@/components/sales/DataTable";
import { useT } from "@/i18n/client";
import { date, kg, num, pct, delta } from "@/lib/sales/format";
import {
  NO_REGION,
  TAJIK_BASE,
  qty,
  regionSourceLabel,
  sharesText,
  somoni,
  tajikHref,
  weightSourceLabel,
  type TajikAgentRow,
  type TajikDocument,
  type TajikProductRow,
  type TajikRegionRow,
  type TajikStockRow,
  type TajikStoreRow,
  type TajikStoreView,
} from "@/lib/sales/tajik";

const deltaClass = (v: number | null) => (v == null ? "text-ink-3" : v >= 0 ? "text-ok" : v > -0.1 ? "text-warn" : "text-bad");

/** «Все регионы» таблицей: клик — регион (точки и что в них продано). */
export function TajikRegionsTable({ rows, query }: { rows: TajikRegionRow[]; query: string }) {
  const t = useT();
  const columns: Column<TajikRegionRow>[] = [
    {
      key: "name",
      label: t("Регион"),
      value: (r) => r.name,
      render: (r) => <NameCell name={r.id === NO_REGION ? t(r.name) : r.name} sub={sharesText(r.categories.map((c) => ({ ...c, name: t(c.name) })))} />,
    },
    { key: "kg", label: t("Факт, кг"), align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "share", label: t("Доля"), align: "right", value: (r) => r.share, render: (r) => pct(r.share) },
    { key: "amount", label: t("Сумма, сомони"), align: "right", value: (r) => r.amount, render: (r) => somoni(r.amount) },
    { key: "stores", label: t("Точек"), align: "right", value: (r) => r.stores, render: (r) => num(r.stores) },
    { key: "docs", label: t("Накладных"), align: "right", value: (r) => r.documents, render: (r) => num(r.documents) },
    { key: "agents", label: t("ТП"), align: "right", value: (r) => r.agents, render: (r) => num(r.agents) },
    {
      key: "vs",
      label: t("К прошл. мес."),
      align: "right",
      value: (r) => r.vsPrev,
      render: (r) => <span className={deltaClass(r.vsPrev)}>{delta(r.vsPrev)}</span>,
    },
  ];
  return (
    <DataTable
      title={t("Все регионы")}
      hint={t("клик — точки региона и что в них продано")}
      columns={columns}
      rows={rows}
      rowKey={(r) => r.id}
      rowHref={(r) => tajikHref(`${TAJIK_BASE}/regions/${r.id}`, query)}
    />
  );
}

export function TajikAgentsTable({ rows, title }: { rows: TajikAgentRow[]; title?: string }) {
  const t = useT();
  const columns: Column<TajikAgentRow>[] = [
    { key: "name", label: t("ТП"), value: (r) => r.name, render: (r) => <NameCell name={r.name} sub={r.regions.map((x) => t(x)).join(", ")} /> },
    { key: "kg", label: t("Факт, кг"), align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "amount", label: t("Сумма, сомони"), align: "right", value: (r) => r.amount, render: (r) => somoni(r.amount) },
    { key: "stores", label: t("Точек"), align: "right", value: (r) => r.stores, render: (r) => num(r.stores) },
    { key: "docs", label: t("Накладных"), align: "right", value: (r) => r.documents, render: (r) => num(r.documents) },
  ];
  return <DataTable title={title ?? t("Торговые представители")} columns={columns} rows={rows} rowKey={(r) => r.name} />;
}

function WhereList({ row, query }: { row: TajikProductRow; query: string }) {
  const t = useT();
  if (!row.where || row.where.length === 0) return null;
  return (
    <div className="py-1">
      <div className="mb-2 text-xs font-medium text-ink-3">{t("Где продан")}</div>
      <ul className="grid gap-1 text-sm sm:grid-cols-2">
        {row.where.map((s) => (
          <li key={s.id} className="flex items-baseline justify-between gap-3 rounded-md px-2 py-1 hover:bg-muted">
            <Link href={tajikHref(`${TAJIK_BASE}/stores/${s.id}`, query)} className="min-w-0 truncate text-ink hover:text-accent-strong">
              {s.name}
            </Link>
            <span className="shrink-0 tabular-nums text-ink-2">
              {qty(s.qty)} {t("ед.")} · {kg(s.kg)} {t("кг")}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

/** Товары: категория, вес единицы и откуда он, количество, вес, сумма; withWhere — строка раскрывается списком точек. */
export function TajikProductsTable({ rows, query = "", withWhere = false, title }: { rows: TajikProductRow[]; query?: string; withWhere?: boolean; title?: string }) {
  const t = useT();
  const columns: Column<TajikProductRow>[] = [
    { key: "name", label: t("Товар"), value: (r) => r.name, render: (r) => <NameCell name={r.name} sub={r.linkoName ? `${r.code} · Linko: ${r.linkoName}` : r.code} /> },
    { key: "cat", label: t("Категория"), value: (r) => r.category, render: (r) => (r.category ? t(r.category) : <span className="text-ink-3">{t("Без категории")}</span>) },
    {
      key: "unit",
      label: t("Вес ед., кг"),
      align: "right",
      value: (r) => r.unitKg,
      render: (r) => (
        <span title={t(weightSourceLabel[r.weightSource])} className={r.weightSource === "ambiguous" || r.weightSource === "none" ? "text-warn" : undefined}>
          {r.unitKg == null ? "—" : qty(r.unitKg)}
        </span>
      ),
    },
    { key: "qty", label: t("Кол-во, ед."), align: "right", value: (r) => r.qty, render: (r) => qty(r.qty) },
    { key: "kg", label: t("Факт, кг"), align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "amount", label: t("Сумма, сомони"), align: "right", value: (r) => r.amount, render: (r) => somoni(r.amount) },
    { key: "price", label: t("Цена, сомони"), align: "right", value: (r) => r.avgPrice, render: (r) => somoni(r.avgPrice) },
    { key: "stores", label: t("Точек"), align: "right", value: (r) => r.stores, render: (r) => num(r.stores) },
  ];
  return (
    <DataTable
      title={title ?? t("Товары")}
      hint={withWhere ? t("клик по строке — в каких точках продан") : undefined}
      columns={columns}
      rows={rows}
      rowKey={(r) => r.code}
      expand={withWhere ? (r) => <WhereList row={r} query={query} /> : undefined}
      note={t(
        "Вес единицы — из названия товара в 1С («1,5 кг», «420гр*8шт»). Если в названии штука и коробка, единицу выбирает цена: цена за кг в сомони сверяется с ценой того же товара в Linko. «Штука — проверить» — цены Linko нет, принята штука.",
      )}
    />
  );
}

/** Точки региона: ТП, вес, сумма, накладные, последняя покупка, что брали (категории); клик — страница точки. */
export function TajikStoresTable({ rows, query, title }: { rows: TajikStoreRow[]; query: string; title?: string }) {
  const t = useT();
  const columns: Column<TajikStoreRow>[] = [
    { key: "name", label: t("Точка"), value: (r) => r.name, render: (r) => <NameCell name={r.name} sub={sharesText(r.categories.map((c) => ({ ...c, name: t(c.name) })))} /> },
    { key: "agent", label: t("ТП"), value: (r) => r.agent, render: (r) => r.agent ?? "—" },
    { key: "region", label: t("Регион"), value: (r) => r.regionName, render: (r) => <span title={t(regionSourceLabel[r.regionSource])}>{r.regionSource === "none" ? t(r.regionName) : r.regionName}</span> },
    { key: "kg", label: t("Факт, кг"), align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "amount", label: t("Сумма, сомони"), align: "right", value: (r) => r.amount, render: (r) => somoni(r.amount) },
    { key: "docs", label: t("Накладных"), align: "right", value: (r) => r.documents, render: (r) => num(r.documents) },
    { key: "last", label: t("Последняя"), align: "right", value: (r) => r.lastDate, render: (r) => date(r.lastDate) },
  ];
  return (
    <DataTable
      title={title ?? t("Точки")}
      hint={t("клик — накладные точки")}
      columns={columns}
      rows={rows}
      rowKey={(r) => r.id}
      rowHref={(r) => tajikHref(`${TAJIK_BASE}/stores/${r.id}`, query)}
      limit={50}
    />
  );
}

/** Остаток склада дилера: количество, вес, продано за 30 дней, на сколько дней хватит. */
export function TajikStockTable({ rows, stockDate }: { rows: TajikStockRow[]; stockDate: string | null }) {
  const t = useT();
  const columns: Column<TajikStockRow>[] = [
    { key: "name", label: t("Товар"), value: (r) => r.name, render: (r) => <NameCell name={r.name} sub={`${r.code} · ${r.warehouse}`} /> },
    { key: "cat", label: t("Категория"), value: (r) => r.category, render: (r) => (r.category ? t(r.category) : "—") },
    { key: "qty", label: t("Остаток, ед."), align: "right", value: (r) => r.qty, render: (r) => qty(r.qty) },
    { key: "kg", label: t("Остаток, кг"), align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "sold", label: t("Продано за 30 дн., ед."), align: "right", value: (r) => r.soldQty30, render: (r) => qty(r.soldQty30) },
    {
      key: "days",
      label: t("Хватит, дн."),
      align: "right",
      value: (r) => r.daysOfCover,
      render: (r) => <span className={r.daysOfCover != null && r.daysOfCover < 7 ? "font-semibold text-bad" : undefined}>{r.daysOfCover == null ? "—" : num(r.daysOfCover)}</span>,
    },
  ];
  return (
    <DataTable
      title={t("Остаток склада дилера")}
      hint={stockDate ? t("на {date}", { date: date(stockDate) }) : undefined}
      columns={columns}
      rows={rows}
      rowKey={(r) => `${r.code}|${r.warehouse}`}
      empty={t("Остаток из 1С ещё не загружался")}
      note={t("«Хватит» — остаток, делённый на средние продажи дня за последние 30 дней (по накладным 1С). Меньше недели — красным.")}
    />
  );
}

function DocumentLines({ doc }: { doc: TajikDocument }) {
  const t = useT();
  return (
    <div className="-mx-1 overflow-x-auto">
      <table className="w-full min-w-max text-sm">
        <thead>
          <tr className="text-left text-[11px] uppercase tracking-wide text-ink-3">
            <th className="px-2 py-1 font-semibold">{t("Товар")}</th>
            <th className="px-2 py-1 font-semibold">{t("Категория")}</th>
            <th className="px-2 py-1 text-right font-semibold">{t("Кол-во")}</th>
            <th className="px-2 py-1 text-right font-semibold">{t("Цена")}</th>
            <th className="px-2 py-1 text-right font-semibold">{t("Скидка")}</th>
            <th className="px-2 py-1 text-right font-semibold">{t("Сумма")}</th>
            <th className="px-2 py-1 text-right font-semibold">{t("кг")}</th>
          </tr>
        </thead>
        <tbody>
          {doc.lines.map((l, i) => (
            <tr key={i} className="border-t border-line/60">
              <td className="px-2 py-1 text-ink">{l.name}</td>
              <td className="px-2 py-1 text-ink-2">{l.category ? t(l.category) : "—"}</td>
              <td className="px-2 py-1 text-right tabular-nums">{qty(l.qty)}</td>
              <td className="px-2 py-1 text-right tabular-nums">{somoni(l.price)}</td>
              <td className="px-2 py-1 text-right tabular-nums text-ink-2">
                {l.discount ? `${somoni(l.discount)}${l.discountPercent ? ` (${num(l.discountPercent, 1)}%)` : ""}` : "—"}
              </td>
              <td className="px-2 py-1 text-right tabular-nums">{somoni(l.amount)}</td>
              <td className="px-2 py-1 text-right tabular-nums">{kg(l.kg)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** Накладные точки за месяц: строка раскрывается товарами. */
export function TajikDocumentsTable({ rows }: { rows: TajikDocument[] }) {
  const t = useT();
  const columns: Column<TajikDocument>[] = [
    { key: "number", label: t("Накладная"), value: (r) => r.number, render: (r) => <NameCell name={r.number} sub={r.agent} /> },
    { key: "date", label: t("Дата"), value: (r) => r.date, render: (r) => date(r.date) },
    { key: "lines", label: t("Строк"), align: "right", value: (r) => r.lines.length, render: (r) => num(r.lines.length) },
    { key: "kg", label: t("кг"), align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "discount", label: t("Скидка, сомони"), align: "right", value: (r) => r.discount, render: (r) => (r.discount ? somoni(r.discount) : "—") },
    { key: "amount", label: t("Сумма, сомони"), align: "right", value: (r) => r.amount, render: (r) => somoni(r.amount) },
  ];
  return (
    <DataTable
      title={t("Накладные")}
      hint={t("клик по строке — товары накладной")}
      columns={columns}
      rows={rows}
      rowKey={(r) => `${r.number}|${r.date}`}
      expand={(r) => <DocumentLines doc={r} />}
      empty={t("За месяц накладных нет")}
    />
  );
}

export function TajikStoreMonthsTable({ rows }: { rows: TajikStoreView["months"] }) {
  const t = useT();
  type Row = TajikStoreView["months"][number];
  const columns: Column<Row>[] = [
    { key: "month", label: t("Месяц"), value: (r) => r.year * 100 + r.month, render: (r) => `${String(r.month).padStart(2, "0")}.${r.year}` },
    { key: "kg", label: t("Факт, кг"), align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "amount", label: t("Сумма, сомони"), align: "right", value: (r) => r.amount, render: (r) => somoni(r.amount) },
    { key: "docs", label: t("Накладных"), align: "right", value: (r) => r.documents, render: (r) => num(r.documents) },
  ];
  return <DataTable title={t("По месяцам")} columns={columns} rows={rows} rowKey={(r) => `${r.year}-${r.month}`} />;
}
