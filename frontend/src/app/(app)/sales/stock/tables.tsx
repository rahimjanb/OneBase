"use client";

import { useMemo, useState } from "react";
import { ArrowDown, ArrowUp } from "lucide-react";
import { DataTable, NameCell, type Column } from "@/components/sales/DataTable";
import { Note, Section } from "@/components/sales/bits";
import { TopChip } from "@/components/sales/outstock";
import { kg, money, num } from "@/lib/sales/format";
import type { StockCell, StockExcluded, StockItem, StockRegion, StockStatus, StockTotals, StockView } from "@/lib/sales/types";

// Таблицы «Рек. остатка»: все числа — с сервера (StockService), здесь только единица показа и вывод.

/** Единица показа: кг, коробки, деньги по входу дилера, штуки как в Linko. */
export type StockUnit = "kg" | "boxes" | "sum" | "pieces";

const unitLabels: Record<StockUnit, string> = { kg: "кг", boxes: "коробки", sum: "сум", pieces: "штуки" };

const stockStatus: Record<StockStatus, { label: string; cls: string }> = {
  deficit: { label: "дефицит", cls: "bg-bad-soft text-bad" },
  overstock: { label: "затоварка", cls: "bg-warn-soft text-warn" },
  dead: { label: "не продаётся", cls: "bg-muted text-ink-2" },
  ok: { label: "норма", cls: "bg-ok-soft text-ok" },
  unknown: { label: "нет веса", cls: "bg-muted text-ink-3" },
  none: { label: "—", cls: "text-ink-3" },
};

function StatusPill({ status }: { status: StockStatus }) {
  const s = stockStatus[status];
  return <span className={`inline-block whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium ${s.cls}`}>{s.label}</span>;
}

/** Величины клетки склада или строки — у строки те же поля, что у клетки. */
type Amounts = Pick<
  StockCell,
  | "pieces"
  | "kg"
  | "boxes"
  | "valueSum"
  | "kgPerDay"
  | "rawKgPerDay"
  | "boxesPerDay"
  | "sumPerDay"
  | "daysOfCover"
  | "need15Kg"
  | "need15Boxes"
  | "need15Sum"
  | "orderKg"
  | "orderBoxes"
  | "orderPieces"
  | "orderSum"
  | "status"
> & { zeroDays?: number };

/** Значение в выбранной единице — все четыре посчитаны сервером; штук в день и штук на 15 дней сервер не даёт, в «штуках» это кг. */
const pick = (unit: StockUnit, kgValue: number | null, boxes: number | null, sum: number | null, pieces: number | null) =>
  unit === "boxes" ? boxes : unit === "sum" ? sum : unit === "pieces" ? pieces : kgValue;
const stockOf = (c: Amounts, unit: StockUnit) => pick(unit, c.kg, c.boxes, c.valueSum, c.pieces);
const perDayOf = (c: Amounts, unit: StockUnit) => pick(unit, c.kgPerDay, c.boxesPerDay, c.sumPerDay, c.kgPerDay);
const need15Of = (c: Amounts, unit: StockUnit) => pick(unit, c.need15Kg, c.need15Boxes, c.need15Sum, c.need15Kg);
const orderOf = (c: Amounts, unit: StockUnit) => pick(unit, c.orderKg, c.orderBoxes, c.orderSum, c.orderPieces);
const totalStockOf = (t: StockTotals, unit: StockUnit) => pick(unit, t.kg, t.boxes, t.valueSum, t.pieces);
const totalPerDayOf = (t: StockTotals, unit: StockUnit) => pick(unit, t.kgPerDay, t.boxesPerDay, t.sumPerDay, t.kgPerDay);
const totalNeed15Of = (t: StockTotals, unit: StockUnit) => pick(unit, t.need15Kg, t.need15Boxes, t.need15Sum, t.need15Kg);
const totalOrderOf = (t: StockTotals, unit: StockUnit) => pick(unit, t.orderKg, t.orderBoxes, t.orderSum, null);

/** Формат в единице: деньги сокращённо, коробки до десяти — с десятыми (иначе «0» читается как «не нужно»), штуки целые, кг — как везде. */
const fmt = (unit: StockUnit, v: number | null) =>
  v == null ? "—" : unit === "sum" ? money(v) : unit === "boxes" ? num(v, v > 0 && v < 10 ? 1 : 0) : unit === "pieces" ? num(v) : kg(v);
/** Скорость: по одному SKU это единицы килограммов в день — с десятыми. */
const fmtDay = (unit: StockUnit, v: number | null) => (v == null ? "—" : unit === "sum" ? money(v) : unit === "boxes" ? num(v, 1) : kg(v));
const perDayLabel = (unit: StockUnit) => (unit === "pieces" ? "кг" : unitLabels[unit]);
const packLabel = (p: number) => `${num(p, p < 0.1 ? 3 : 2)} кг`;

/** Скорость с поправкой на аутсток: «*» и подсказка, если поправка изменила скорость (сервер отдаёт обе). */
function PerDay({ c, unit }: { c: Amounts; unit: StockUnit }) {
  const corrected = c.kgPerDay != null && c.rawKgPerDay != null && c.kgPerDay - c.rawKgPerDay > 0.05;
  const title = corrected
    ? `С поправкой на аутсток${c.zeroDays ? ` (${num(c.zeroDays)} дн. в нуле в закрытом месяце)` : ""}: без поправки ${kg(c.rawKgPerDay)} кг в день`
    : undefined;
  return (
    <span title={title}>
      {fmtDay(unit, perDayOf(c, unit))}
      {corrected ? "*" : ""}
    </span>
  );
}

/** Заказ — жирным и только когда он есть: пустая клетка = докупать не нужно. */
function OrderCell({ c, unit }: { c: Amounts; unit: StockUnit }) {
  if (!(c.orderKg > 0)) return <span className="text-ink-3">·</span>;
  return <span className="font-semibold text-accent-strong">{fmt(unit, orderOf(c, unit))}</span>;
}

const coverClass = (status: StockStatus) => (status === "deficit" ? "font-semibold text-bad" : status === "overstock" ? "text-warn" : "");

/** Название с переносом и меткой «ТОП»: длинные наименования иначе растягивают таблицу. */
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

const weightTitle = (r: StockItem) =>
  `${
    r.unitKgSource === "orders"
      ? "По строкам заказов с 1-го числа закрытого месяца."
      : r.unitKgSource === "ordersYear"
        ? "Свежих продаж нет — по строкам заказов за год."
        : r.unitKgSource === "name"
          ? "Продаж за год не было — вес из названия (оценка)."
          : "Веса единицы нет."
  } Коробка: ${r.boxNote}.`;

/** Разрез строки по складам: завод (заказ у завода) и склады дилеров охвата, где есть остаток или продажи. */
function RegionPanel({ item, regions, factory, unit }: { item: StockItem; regions: StockRegion[]; factory: StockRegion | null; unit: StockUnit }) {
  const rows = [
    ...(factory && item.factory ? [{ id: "factory", name: factory.name, c: item.factory, plant: true }] : []),
    ...regions
      .flatMap((r) => {
        const c = item.regions[r.id];
        return c && ((c.kg ?? 0) > 0 || (c.kgPerDay ?? 0) > 0) ? [{ id: r.id, name: r.name, c, plant: false }] : [];
      })
      .sort((a, b) => (b.c.kg ?? 0) - (a.c.kg ?? 0)),
  ];
  const th = "py-1 pr-4 text-right text-[11px] font-medium uppercase tracking-wide text-ink-3";
  const td = "border-t border-line py-1 pr-4 text-right tabular-nums";
  return (
    <div className="overflow-x-auto">
      <div className="mb-2 text-xs text-ink-3">По складам · {item.name}</div>
      <table className="min-w-max text-xs">
        <thead>
          <tr>
            <th className={`${th} text-left`}>Склад</th>
            <th className={th}>Остаток, {unitLabels[unit]}</th>
            <th className={th}>В день, {perDayLabel(unit)}</th>
            <th className={th}>Хватит, дн.</th>
            <th className={th}>15 дн., {perDayLabel(unit)}</th>
            <th className={th}>Заказ, {unitLabels[unit]}</th>
            <th className={`${th} text-left`}>Статус</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr key={r.id} className={r.plant ? "text-ink-2" : ""}>
              <td className={`${td} text-left font-medium text-ink`} title={r.plant ? "Заказ у завода — заказы всех дилеров минус его остаток; скорость — по всей стране" : undefined}>
                {r.name}
                {r.plant && <span className="ml-1 text-ink-3">(завод)</span>}
              </td>
              <td className={td}>{fmt(unit, stockOf(r.c, unit))}</td>
              <td className={td}>
                <PerDay c={r.c} unit={unit} />
              </td>
              <td className={`${td} ${coverClass(r.c.status)}`}>{num(r.c.daysOfCover, 1)}</td>
              <td className={td}>{fmt(unit, need15Of(r.c, unit))}</td>
              <td className={td}>
                <OrderCell c={r.c} unit={unit} />
              </td>
              <td className={`${td} text-left`}>
                <StatusPill status={r.c.status} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** Список: товар — строка, клик раскрывает разрез по складам. Сортировка по колонкам; «—» всегда внизу. */
export function StockTable({ data, unit }: { data: StockView; unit: StockUnit }) {
  const approx = (r: StockItem) => r.unitKgSource === "name";
  const mark = (r: StockItem, text: string) => (approx(r) && unit !== "pieces" ? `≈ ${text}` : text);
  const priceList = data.priceList ?? "вход дилеру";
  const plant = data.scope === "plant";
  const dealers = !plant && data.scope !== "export";
  const factoryColumns: Column<StockItem>[] =
    dealers && data.factory
      ? [
          { key: "factory", label: "Завод, кг", align: "right", value: (r) => r.factory?.kg ?? null, render: (r) => kg(r.factory?.kg ?? null) },
          {
            key: "factoryOrder",
            label: "Не хватает на заводе, кг",
            align: "right",
            value: (r) => r.factory?.orderKg ?? null,
            render: (r) => (r.factory && r.factory.orderKg > 0 ? <span className="font-semibold text-bad">{kg(r.factory.orderKg)}</span> : <span className="text-ink-3">·</span>),
          },
        ]
      : [];
  const columns: Column<StockItem>[] = [
    { key: "name", label: "Наименование", value: (r) => r.name, render: (r) => <WrapName name={r.name} sub={r.code ? `код ${r.code}` : null} top={r.top} /> },
    { key: "cat", label: "Категория", value: (r) => r.category },
    {
      key: "pack",
      label: "Фасовка",
      align: "right",
      value: (r) => r.packKg,
      render: (r) =>
        r.packKg == null ? (
          <span className="text-ink-3" title="веса в названии нет">
            —
          </span>
        ) : (
          packLabel(r.packKg)
        ),
    },
    {
      key: "unit",
      label: "Вес штуки",
      align: "right",
      value: (r) => r.unitKg,
      render: (r) => <span title={weightTitle(r)}>{r.unitKg == null ? "—" : `${approx(r) ? "≈ " : ""}${num(r.unitKg, 3)} кг`}</span>,
    },
    { key: "stock", label: `Остаток, ${unitLabels[unit]}`, align: "right", value: (r) => stockOf(r, unit), render: (r) => mark(r, fmt(unit, stockOf(r, unit))) },
    { key: "perDay", label: `В день, ${perDayLabel(unit)}`, align: "right", value: (r) => perDayOf(r, unit), render: (r) => <PerDay c={r} unit={unit} /> },
    {
      key: "days",
      label: "Хватит, дн.",
      align: "right",
      value: (r) => r.daysOfCover,
      render: (r) => <span className={coverClass(r.status)}>{r.daysOfCover == null ? "—" : `${approx(r) ? "≈ " : ""}${num(r.daysOfCover, 1)}`}</span>,
    },
    { key: "need15", label: "15 дн., кг", align: "right", value: (r) => r.need15Kg, render: (r) => kg(r.need15Kg) },
    { key: "need30", label: "30 дн., кг", align: "right", value: (r) => r.need30Kg, render: (r) => kg(r.need30Kg) },
    {
      key: "price",
      label: "Цена входа",
      align: "right",
      value: (r) => r.price,
      render: (r) => <span title={r.price == null ? `Товара нет в прайсе «${priceList}»` : `За единицу учёта, прайс «${priceList}» на начало месяца`}>{r.price == null ? "—" : num(r.price)}</span>,
    },
    { key: "value", label: "Сумма запаса", align: "right", value: (r) => r.valueSum, render: (r) => mark(r, money(r.valueSum)) },
    {
      key: "order",
      label: plant ? `Не хватает на заводе, ${unitLabels[unit]}` : `Рек. заказ, ${unitLabels[unit]}`,
      align: "right",
      value: (r) => r.orderKg,
      render: (r) => <OrderCell c={r} unit={unit} />,
    },
    { key: "status", label: "Статус", value: (r) => r.status, render: (r) => <StatusPill status={r.status} /> },
    ...factoryColumns,
  ];
  return (
    <DataTable
      title="Рекомендуемый остаток"
      hint={dealers ? "клик по строке — разрез по складам" : plant ? "склад завода: скорость — продажи всей страны, заказ — чего не хватает, чтобы отгрузить всем дилерам" : "склад экспорта: своей скорости продаж нет"}
      columns={columns}
      rows={data.items}
      rowKey={(r) => String(r.productId)}
      expand={dealers ? (r) => <RegionPanel item={r} regions={data.scopeRegions} factory={data.factory} unit={unit} /> : undefined}
      note={`Кг = штуки × вес штуки (Σ веса ÷ Σ количества по строкам заказов с 1-го числа закрытого месяца, иначе за год, иначе из названия — ≈). Коробки = кг ÷ вес коробки: из названия (у весового товара любой, у штучного — если в коробке целое число штук), иначе по отгрузкам завода. Сумма запаса = штуки × входная цена дилера из прайса «${priceList}» на начало месяца. «В день» — вторичка нетто возвратов за ${num(data.velocityDays)} дн. ÷ (${num(data.velocityDays)} − дни в нуле за закрытый месяц, не больше 30) — звёздочка там, где поправка сработала. Рек. заказ = скорость × 15 − остаток, вверх до целой коробки; у завода — заказы всех дилеров минус его остаток. Дефицит — меньше 15 дней, норма 15–30, затоварка — больше 30, «не продаётся» — остаток есть, продаж за базу нет.`}
    />
  );
}

type Metric = "v" | "day" | "left" | "rec" | "ord";

const metrics: { key: Metric; label: (unit: StockUnit) => string; title?: string }[] = [
  { key: "v", label: (u) => unitLabels[u] },
  { key: "day", label: (u) => `в день, ${perDayLabel(u)}` },
  { key: "left", label: () => "дн." },
  { key: "rec", label: (u) => `15 дн., ${perDayLabel(u)}` },
  { key: "ord", label: (u) => `заказ, ${unitLabels[u]}`, title: "Рекомендуемый заказ: до запаса на 15 дней, вверх до целой коробки; у завода — заказы дилеров минус его остаток" },
];

const metricOf = (c: Amounts, unit: StockUnit, m: Metric) =>
  m === "v" ? stockOf(c, unit) : m === "day" ? perDayOf(c, unit) : m === "left" ? c.daysOfCover : m === "rec" ? need15Of(c, unit) : orderOf(c, unit);
const totalOf = (t: StockTotals, unit: StockUnit, m: Metric) =>
  m === "v" ? totalStockOf(t, unit) : m === "day" ? totalPerDayOf(t, unit) : m === "left" ? t.daysOfCover : m === "rec" ? totalNeed15Of(t, unit) : totalOrderOf(t, unit);

type Group = { key: string; name: string; plant: boolean; cellOf: (r: StockItem) => Amounts | null; totals: StockTotals | null };

const empty = (c: Amounts | null): c is null => !c || ((c.kg ?? 0) === 0 && (c.kgPerDay ?? 0) === 0);

/** Пять клеток одного склада: остаток, в день, дн., 15 дн., заказ — в выбранной единице. */
function Trio({ c, unit, plant }: { c: Amounts | null; unit: StockUnit; plant: boolean }) {
  const cls = "border-b border-line px-2 py-1.5 text-right tabular-nums";
  if (empty(c)) {
    return (
      <>
        {metrics.map((m, i) => (
          <td key={m.key} className={`${cls} text-ink-3 ${i === 0 ? "border-l border-line" : ""}`}>
            ·
          </td>
        ))}
      </>
    );
  }
  return (
    <>
      <td className={`${cls} border-l border-line`}>{fmt(unit, stockOf(c, unit))}</td>
      <td className={`${cls} text-ink-2`}>
        <PerDay c={c} unit={unit} />
      </td>
      <td className={`${cls} ${coverClass(c.status)}`}>{num(c.daysOfCover, 1)}</td>
      <td className={cls}>{fmt(unit, need15Of(c, unit))}</td>
      <td className={cls} title={plant && c.orderKg > 0 ? "Заказ у завода: заказы всех дилеров минус остаток на заводе" : undefined}>
        <OrderCell c={c} unit={unit} />
      </td>
    </>
  );
}

/**
 * Матрица «товар × склад»: на каждый склад охвата пять колонок (остаток, в день, дн., 15 дн., заказ); завод — первой группой при любом охвате
 * дилеров (рядом с регионом видно, есть ли товар на заводе), «Всего» — последней, если складов больше одного. Подвал — итоги с сервера.
 */
export function StockMatrix({ data, unit }: { data: StockView; unit: StockUnit }) {
  const [sort, setSort] = useState<{ key: string; desc: boolean } | null>(null);
  const groups = useMemo<Group[]>(() => {
    const regions: Group[] = data.scopeRegions.map((r) => ({ key: r.id, name: r.name, plant: false, cellOf: (it) => it.regions[r.id] ?? null, totals: data.regionTotals[r.id] ?? null }));
    const plant: Group[] = data.factory && data.scope !== "export" ? [{ key: "factory", name: data.factory.name, plant: true, cellOf: (it) => it.factory, totals: data.factoryTotals }] : [];
    const total: Group[] = regions.length > 1 ? [{ key: "total", name: "Всего", plant: false, cellOf: (it) => it, totals: data.totals }] : [];
    const only: Group[] = regions.length === 0 && plant.length === 0 ? [{ key: "scope", name: data.scopeName, plant: false, cellOf: (it) => it, totals: data.totals }] : [];
    return [...plant, ...regions, ...total, ...only];
  }, [data]);

  const rows = useMemo(() => {
    if (!sort) return data.items;
    const [gk, m] = sort.key.split(":");
    const group = groups.find((g) => g.key === gk);
    const value = (r: StockItem): number | string | null => {
      if (sort.key === "name") return r.name;
      if (sort.key === "cat") return r.category;
      if (sort.key === "pack") return r.packKg;
      const c = group?.cellOf(r) ?? null;
      return empty(c) ? null : metricOf(c, unit, m as Metric);
    };
    const factor = sort.desc ? -1 : 1;
    return [...data.items].sort((a, b) => {
      const x = value(a);
      const y = value(b);
      if (x == null && y == null) return 0;
      if (x == null) return 1; // «—» — всегда внизу
      if (y == null) return -1;
      return typeof x === "number" && typeof y === "number" ? (x - y) * factor : String(x).localeCompare(String(y), "ru") * factor;
    });
  }, [data.items, groups, sort, unit]);

  const click = (key: string, textual = false) =>
    setSort((s) => (s?.key === key ? (s.desc === !textual ? { key, desc: textual } : null) : { key, desc: !textual }));
  const arrow = (key: string) => (sort?.key === key ? sort.desc ? <ArrowDown className="inline size-3" /> : <ArrowUp className="inline size-3" /> : null);
  const th = "whitespace-nowrap px-2 py-1.5 text-[11px] font-semibold uppercase tracking-wide text-ink-3";
  const sortBtn = (key: string, label: string, textual = false, title?: string) => (
    <button type="button" onClick={() => click(key, textual)} title={title} className={`inline-flex items-center gap-1 hover:text-ink ${sort?.key === key ? "text-ink" : ""}`}>
      {label}
      {arrow(key)}
    </button>
  );
  const sticky = "sticky left-0 z-[1] bg-surface";

  return (
    <Section title="Рекомендуемый остаток" hint="на каждый склад: остаток, продажи в день, на сколько хватит, запас на 15 дней и рекомендуемый заказ">
      <div className="-mx-4 overflow-x-auto sm:-mx-5">
        <table className="w-full min-w-max border-collapse text-xs">
          <thead>
            <tr>
              <th rowSpan={2} className={`${th} ${sticky} border-b border-line pl-4 text-left sm:pl-5`}>
                {sortBtn("name", "Наименование", true)}
              </th>
              <th rowSpan={2} className={`${th} border-b border-line text-left`}>
                {sortBtn("cat", "Категория", true)}
              </th>
              <th rowSpan={2} className={`${th} border-b border-line text-right`}>
                {sortBtn("pack", "Фасовка")}
              </th>
              {groups.map((g) => (
                <th key={g.key} colSpan={metrics.length} className={`${th} border-b border-l border-line text-center ${g.plant ? "text-ink" : ""}`}>
                  {g.name}
                </th>
              ))}
            </tr>
            <tr>
              {groups.map((g) =>
                metrics.map((m, i) => (
                  <th key={`${g.key}:${m.key}`} className={`${th} border-b border-line text-right ${i === 0 ? "border-l" : ""}`}>
                    {sortBtn(`${g.key}:${m.key}`, m.label(unit), false, m.title)}
                  </th>
                )),
              )}
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.productId} className="hover:bg-muted">
                <td className={`${sticky} max-w-[280px] border-b border-line py-1.5 pl-4 pr-2 text-ink sm:pl-5`} title={r.name}>
                  <span className="flex items-center gap-1.5">
                    {r.top && <TopChip />}
                    <span className="truncate">{r.name}</span>
                  </span>
                </td>
                <td className="whitespace-nowrap border-b border-line px-2 py-1.5 text-ink-2">{r.category}</td>
                <td className="whitespace-nowrap border-b border-line px-2 py-1.5 text-right tabular-nums text-ink-2">{r.packKg == null ? "—" : packLabel(r.packKg)}</td>
                {groups.map((g) => (
                  <Trio key={g.key} c={g.cellOf(r)} unit={unit} plant={g.plant} />
                ))}
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="bg-muted/50 font-semibold">
              <td colSpan={3} className={`${sticky} bg-muted/50 py-1.5 pl-4 pr-2 sm:pl-5`}>
                Итого · {num(data.items.length)} SKU
              </td>
              {groups.map((g) =>
                metrics.map((m, i) => (
                  <td key={`${g.key}:${m.key}`} className={`px-2 py-1.5 text-right tabular-nums ${i === 0 ? "border-l border-line" : ""}`}>
                    {g.totals == null ? "—" : m === metrics[1] ? fmtDay(unit, totalOf(g.totals, unit, m.key)) : m.key === "left" ? num(g.totals.daysOfCover, 1) : fmt(unit, totalOf(g.totals, unit, m.key))}
                  </td>
                )),
              )}
            </tr>
          </tfoot>
        </table>
      </div>
      <Note>
        Клетка — склад: остаток в выбранной единице, продажи в день (звёздочка — с поправкой на аутсток), на сколько дней хватит, запас на 15 дней
        и рекомендуемый заказ (пусто — докупать не нужно). Завод в «Всего» не входит: это тот же товар до отгрузки; его заказ — заказы всех дилеров
        минус остаток на заводе. Итоги считает сервер по отфильтрованным строкам. «—» в сортировке всегда внизу.
      </Note>
    </Section>
  );
}

const excludedReason: Record<StockExcluded["reason"], string> = { outsideReport: "вне категорий отчёта", withoutWeight: "нет веса единицы" };

/** Товары, которых в таблице нет: бонусы, подарочные наборы, импорт — и SKU без веса единицы. */
export function StockExcludedTable({ rows, outsideReport, withoutWeight }: { rows: StockExcluded[]; outsideReport: number; withoutWeight: number }) {
  const columns: Column<StockExcluded>[] = [
    { key: "name", label: "Товар", value: (r) => r.name, render: (r) => <WrapName name={r.name} sub={r.code ? `код ${r.code}` : null} /> },
    { key: "cat", label: "Тип Linko", value: (r) => r.category },
    { key: "reason", label: "Почему не в таблице", value: (r) => excludedReason[r.reason] },
    { key: "pieces", label: "У дилеров, шт.", align: "right", value: (r) => r.pieces, render: (r) => num(r.pieces) },
    { key: "kg", label: "Кг", align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "factory", label: "На заводе, шт.", align: "right", value: (r) => r.factoryPieces, render: (r) => num(r.factoryPieces) },
  ];
  return (
    <DataTable
      title="Вне таблицы"
      hint={`вне категорий отчёта — ${num(outsideReport)}, без веса единицы — ${num(withoutWeight)}`}
      columns={columns}
      rows={rows}
      rowKey={(r) => String(r.productId)}
      limit={20}
      empty="Все товары с остатком — в таблице"
      note="Бонусные товары, подарочные наборы и импорт (типы Linko вне восьми категорий отчёта) в остаток не входят — как в «Полевом контроле». Товар без веса единицы (нет продаж за год и фасовки в названии) в кг не пересчитать, поэтому он тоже вне таблицы и итогов."
    />
  );
}

export function OtherStocksTable({ rows }: { rows: StockView["otherStocks"] }) {
  type Row = StockView["otherStocks"][number];
  const columns: Column<Row>[] = [
    { key: "name", label: "Склад", value: (r) => r.name, render: (r) => <WrapName name={r.name} sub={`ID ${r.stockId}`} /> },
    { key: "region", label: "Чей склад", value: (r) => r.region, render: (r) => r.region ?? <span className="text-ink-3">—</span> },
    { key: "items", label: "Товаров", align: "right", value: (r) => r.items, render: (r) => num(r.items) },
    { key: "pieces", label: "Штук", align: "right", value: (r) => r.pieces, render: (r) => num(r.pieces) },
    { key: "kg", label: "Кг", align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
  ];
  return (
    <DataTable
      title="Прочие склады"
      hint="в «Всю страну» не входят"
      columns={columns}
      rows={rows}
      rowKey={(r) => String(r.stockId)}
      empty="Все склады с остатком — склады дилеров, завод или экспорт"
      note="Склады старых филиалов («Жиззах (эски)»), интеграционные, «Основной», «Коканд бозор» и прочие без своего региона в справочнике. «Вся страна» — только 19 складов дилеров, как в «Полевом контроле»; «Чей склад» — регион по Sales:StockRegionAliases, если склад к нему относится."
    />
  );
}
