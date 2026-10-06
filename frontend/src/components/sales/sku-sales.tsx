"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { Note, Section } from "./bits";
import { DataTable, NameCell, type Column } from "./DataTable";
import { TopChip } from "./outstock";
import { kg, money, monthLabel, monthShort, num, pct, plural, productsLabel, regionsLabel } from "@/lib/sales/format";
import { monthIndex, ym } from "@/lib/sales/months";
import type { SalesMonth, SkuSalesRegionRow, SkuSalesRow, SkuSalesView } from "@/lib/sales/types";

// «Продажи по SKU»: все числа — с сервера (SkuSalesReport), здесь только вывод. Фильтры и период — параметры адреса.

/** Самый длинный отрезок — 24 месяца (так же проверяет сервер). */
const MAX_MONTHS = 24;

const QUARTERS = ["I", "II", "III", "IV"];

const monthOption = (value: string) => {
  const [y, m] = value.split("-").map(Number);
  return `${monthShort(m)} ${y}`;
};

/**
 * Период: быстрые варианты (год, кварталы и месяцы текущего года) и «с … по …» месяцами. Невозможные отрезки в списках выключены
 * (конец раньше начала, длиннее 24 месяцев); перепутанные границы меняются местами. Меняет только параметры адреса from и to.
 */
export function SkuSalesPeriod({ months, from, to }: { months: SalesMonth[]; from: string; to: string }) {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();

  const sorted = [...months].sort((a, b) => a.year * 12 + a.month - (b.year * 12 + b.month));
  const latest = sorted[sorted.length - 1] ?? { year: Number(to.slice(0, 4)), month: Number(to.slice(5, 7)) };
  const year = latest.year;
  const ofYear = sorted.filter((m) => m.year === year);

  const go = (f: string, t: string) => {
    const [a, b] = (monthIndex(f) ?? 0) > (monthIndex(t) ?? 0) ? [t, f] : [f, t];
    const next = new URLSearchParams(params);
    next.set("from", a);
    next.set("to", b);
    router.push(`${pathname}?${next}`);
  };

  // Быстрые варианты без повторов: квартал из одного месяца — это месяц, год из одного квартала — это квартал.
  const monthPresets = [...ofYear].reverse().map((m) => ({ value: `${ym(m.year, m.month)}|${ym(m.year, m.month)}`, label: monthLabel(m.year, m.month) }));
  const quarterPresets = [0, 1, 2, 3]
    .map((q) => ofYear.filter((m) => Math.floor((m.month - 1) / 3) === q))
    .filter((list) => list.length > 1)
    .map((list) => {
      const q = Math.floor((list[0].month - 1) / 3);
      const partial = list[list.length - 1].month < q * 3 + 3;
      return {
        value: `${ym(year, q * 3 + 1)}|${ym(year, list[list.length - 1].month)}`,
        label: `${QUARTERS[q]} квартал ${year}${partial ? ` (по ${monthShort(list[list.length - 1].month).toLowerCase()})` : ""}`,
      };
    });
  const yearValue = ofYear.length ? `${ym(year, 1)}|${ym(year, ofYear[ofYear.length - 1].month)}` : null;
  const yearPreset =
    yearValue && ofYear.length > 1 && !quarterPresets.some((p) => p.value === yearValue)
      ? [{ value: yearValue, label: `Год ${year} (янв–${monthShort(ofYear[ofYear.length - 1].month).toLowerCase()})` }]
      : [];
  const current = `${from}|${to}`;
  const known = [...yearPreset, ...quarterPresets, ...monthPresets].some((p) => p.value === current);

  // «С … по …»: все месяцы с данными (и текущие границы, даже если данных за них нет).
  const options = [...new Set([...sorted.map((m) => ym(m.year, m.month)), from, to])].sort((a, b) => (monthIndex(a) ?? 0) - (monthIndex(b) ?? 0));
  const fi = monthIndex(from) ?? 0;
  const ti = monthIndex(to) ?? 0;

  const select = "h-9 rounded-lg border border-line bg-surface px-3 text-sm font-medium text-ink max-lg:h-11 max-lg:min-w-0 max-lg:flex-1";
  return (
    <div className="flex flex-wrap items-center gap-2 max-lg:w-full">
      <select aria-label="Период" value={known ? current : ""} onChange={(e) => e.target.value && go(...(e.target.value.split("|") as [string, string]))} className={select}>
        {!known && <option value="">Свой период</option>}
        {yearPreset.map((p) => (
          <option key={p.value} value={p.value}>
            {p.label}
          </option>
        ))}
        {quarterPresets.length > 0 && (
          <optgroup label="Кварталы">
            {quarterPresets.map((p) => (
              <option key={p.value} value={p.value}>
                {p.label}
              </option>
            ))}
          </optgroup>
        )}
        <optgroup label="Месяцы">
          {monthPresets.map((p) => (
            <option key={p.value} value={p.value}>
              {p.label}
            </option>
          ))}
        </optgroup>
      </select>
      <span className="text-[11px] uppercase tracking-wide text-ink-3">с</span>
      <select aria-label="С месяца" value={from} onChange={(e) => go(e.target.value, to)} className={select}>
        {options.map((o) => {
          const i = monthIndex(o) ?? 0;
          return (
            <option key={o} value={o} disabled={i > ti || ti - i + 1 > MAX_MONTHS}>
              {monthOption(o)}
            </option>
          );
        })}
      </select>
      <span className="text-[11px] uppercase tracking-wide text-ink-3">по</span>
      <select aria-label="По какой месяц" value={to} onChange={(e) => go(from, e.target.value)} className={select}>
        {options.map((o) => {
          const i = monthIndex(o) ?? 0;
          return (
            <option key={o} value={o} disabled={i < fi || i - fi + 1 > MAX_MONTHS}>
              {monthOption(o)}
            </option>
          );
        })}
      </select>
    </div>
  );
}

const abcClass = { A: "font-bold text-ok", B: "font-bold text-warn", C: "font-semibold text-ink-3" } as const;

/** Название товара с меткой ТОП и номером в рейтинге. */
function ProductCell({ name, top, sub, rank }: { name: string; top: boolean; sub?: string | null; rank?: number }) {
  return (
    <span className="flex max-w-[360px] items-start gap-1.5 whitespace-normal max-lg:max-w-[52vw]">
      {rank != null && <span className="mt-px w-7 shrink-0 text-right tabular-nums text-ink-3">{rank}</span>}
      {top && (
        <span className="mt-0.5">
          <TopChip />
        </span>
      )}
      <NameCell name={name} sub={sub} />
    </span>
  );
}

/** Изменение кг в день: рост — зелёный, падение — красный; «новый» — в первом квартале продаж не было. */
function Dynamics({ value, isNew }: { value: number | null; isNew: boolean }) {
  if (isNew) return <span className="text-ok">новый</span>;
  if (value == null) return <span className="text-ink-3">—</span>;
  return <span className={value >= 0 ? "text-ok" : "text-bad"}>{`${value >= 0 ? "+" : "−"}${pct(Math.abs(value), 0)}`}</span>;
}

type FooterCell = { value: React.ReactNode; right?: boolean };

/** Итоговые строки таблицы (значения — с сервера). */
function Footer({ rows }: { rows: FooterCell[][] }) {
  return (
    <tfoot>
      {rows.map((cells, r) => (
        <tr key={r} className="bg-muted font-semibold text-ink">
          {cells.map((c, i) => (
            <td key={i} className={`px-3 py-2.5 first:pl-4 last:pr-4 sm:first:pl-5 sm:last:pr-5 ${c.right ? "text-right tabular-nums" : ""}`}>
              {c.value}
            </td>
          ))}
        </tr>
      ))}
    </tfoot>
  );
}

/** «SKU итого»: рейтинг по весу, доли, ABC, АКБ, регионы и динамика первого и последнего квартала отрезка. Сортировка — по клику, «—» в конце. */
export function SkuSalesTable({ data }: { data: SkuSalesView }) {
  const q = data.quarters;
  const national = data.regionId == null;
  const columns: Column<SkuSalesRow>[] = [
    {
      key: "name",
      label: "№ · Продукт",
      value: (r) => r.rank,
      render: (r) => <ProductCell name={r.name} top={r.isTop} sub={r.code ? `код ${r.code}` : null} rank={r.rank} />,
    },
    { key: "cat", label: "Категория", value: (r) => r.category, render: (r) => <span className="text-ink-2">{r.category}</span> },
    { key: "kg", label: "Кг", align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "sum", label: "Сумма", align: "right", value: (r) => r.revenue, render: (r) => money(r.revenue) },
    { key: "price", label: "Цена, сум/кг", align: "right", value: (r) => r.pricePerKg, render: (r) => num(r.pricePerKg) },
    { key: "share", label: "Доля кг", align: "right", value: (r) => r.share, render: (r) => pct(r.share, 1) },
    { key: "cum", label: "Накоп.", align: "right", value: (r) => r.cumulative, render: (r) => <span className="text-ink-3">{pct(r.cumulative)}</span> },
    { key: "abc", label: "ABC", align: "right", value: (r) => r.abc, render: (r) => <span className={abcClass[r.abc]}>{r.abc}</span> },
    { key: "akb", label: "АКБ", align: "right", value: (r) => r.akb, render: (r) => num(r.akb) },
    ...(national ? ([{ key: "regs", label: "Регионов", align: "right", value: (r) => r.regions, render: (r) => num(r.regions) }] satisfies Column<SkuSalesRow>[]) : []),
    ...(q
      ? ([
          { key: "first", label: `Кг/день · ${q.first}`, align: "right", value: (r) => r.firstKgPerDay, render: (r) => <span className="text-ink-3">{kg(r.firstKgPerDay)}</span> },
          { key: "last", label: `Кг/день · ${q.last}`, align: "right", value: (r) => r.lastKgPerDay, render: (r) => kg(r.lastKgPerDay) },
          {
            key: "dyn",
            label: "Динамика",
            align: "right",
            value: (r) => (r.isNew ? Number.MAX_SAFE_INTEGER : r.dynamics),
            render: (r) => <Dynamics value={r.dynamics} isNew={r.isNew} />,
          },
        ] satisfies Column<SkuSalesRow>[])
      : []),
  ];
  const t = data.totals;

  return (
    <DataTable
      title="SKU итого"
      hint="ABC — первые 80% веса · АКБ — точки, купившие SKU · динамика — кг в день последнего квартала отрезка к первому"
      columns={columns}
      rows={data.rows}
      rowKey={(r) => String(r.productId)}
      limit={60}
      empty="Продаж за период нет"
      footer={
        data.rows.length > 0 && (
          <Footer
            rows={[
              [
                { value: "Итого" },
                { value: "" },
                { value: kg(t.kg), right: true },
                { value: money(t.revenue), right: true },
                { value: num(t.pricePerKg), right: true },
                { value: "" },
                { value: "" },
                { value: "" },
                { value: "" },
                ...(national ? [{ value: num(t.regions), right: true }] : []),
                ...(q ? [{ value: "" }, { value: "" }, { value: "" }] : []),
              ],
            ]}
          />
        )
      }
      note={`Кг и сумма — нетто: заказы по дате приёмки минус возвраты, без «Завода» и «К К Мерч». Ранг, доля, накопленная доля и ABC считаются внутри выборки (регион, ТОП, категории): A — SKU, дающие первые 80% её веса, B — до 95%, C — хвост. АКБ — точки, у которых строка SKU положительна хотя бы в одном месяце отрезка (точка, купившая у двух ТП, — одна).${
        national ? " «Регионов» — где у SKU кг больше нуля." : ""
      }${q ? ` Динамика — кг в день в ${q.last} (${num(q.lastDays)} дн.) к ${q.first} (${num(q.firstDays)} дн.); «новый» — в первом квартале продаж не было.` : ""}`}
    />
  );
}

type RegionUnit = "kg" | "sum" | "share";

const units: { key: RegionUnit; label: string }[] = [
  { key: "kg", label: "кг" },
  { key: "sum", label: "сум" },
  { key: "share", label: "доля" },
];

/** Переключатель единиц внутри блока — это вид, а не фильтр: в адрес не пишется. */
function UnitSwitch({ value, onChange }: { value: RegionUnit; onChange: (unit: RegionUnit) => void }) {
  return (
    <div className="flex overflow-hidden rounded-lg border border-line text-sm" role="group" aria-label="Единицы">
      {units.map((u) => (
        <button
          key={u.key}
          type="button"
          aria-pressed={value === u.key}
          onClick={() => onChange(u.key)}
          className={`px-3 py-1.5 max-lg:py-2.5 ${value === u.key ? "bg-accent text-white" : "bg-surface text-ink hover:bg-muted"}`}
        >
          {u.label}
        </button>
      ))}
    </div>
  );
}

/** SKU × регионы (по всей стране, регион — колонка): кг, сумма или доля SKU в весе региона. */
export function SkuRegionMatrix({ data }: { data: SkuSalesView }) {
  const [unit, setUnit] = useState<RegionUnit>("kg");
  const m = data.regionMatrix;
  const cell = (r: SkuSalesRegionRow, i: number) => {
    const value = unit === "sum" ? r.regionRevenue[i] : unit === "share" ? r.regionShare[i] : r.regionKg[i];
    if (!value) return <span className="text-ink-3">·</span>;
    return unit === "sum" ? money(value) : unit === "share" ? pct(value, 1) : kg(value);
  };
  const th = "px-2 py-2 text-right text-[11px] font-semibold uppercase tracking-wide text-ink-3";

  return (
    <Section title="По регионам" hint="регион — колонка; фильтры ТОП, категорий и ABC действуют" actions={<UnitSwitch value={unit} onChange={setUnit} />}>
      {m.rows.length === 0 ? (
        <p className="py-6 text-center text-sm text-ink-3">Продаж за период нет</p>
      ) : (
        <div className="-mx-4 max-h-[640px] overflow-auto px-4 sm:-mx-5 sm:px-5">
          <table className="w-full min-w-max border-collapse text-xs">
            <thead className="sticky top-0 z-20 bg-surface">
              <tr className="border-b border-line">
                <th className="sticky left-0 z-10 bg-surface px-2 py-2 text-left text-[11px] font-semibold uppercase tracking-wide text-ink-3">Продукт</th>
                {data.regions.map((r) => (
                  <th key={r.id} className={`${th} ${r.id === data.regionId ? "text-accent-strong" : ""}`}>
                    {r.name}
                  </th>
                ))}
                <th className={th}>{unit === "share" ? "Страна" : "Итого"}</th>
              </tr>
            </thead>
            <tbody>
              {m.rows.map((r) => (
                <tr key={r.productId} className="border-b border-line">
                  <td className="sticky left-0 z-10 max-w-[300px] bg-surface px-2 py-1.5 text-ink" title={`${r.name} · ${r.category} · ${r.abc}`}>
                    <span className="flex items-center gap-1.5">
                      {r.isTop && <TopChip />}
                      <span className="truncate">{r.name}</span>
                    </span>
                  </td>
                  {data.regions.map((reg, i) => (
                    <td key={reg.id} className="px-2 py-1.5 text-right tabular-nums text-ink-2" title={`${reg.name}: АКБ ${num(r.regionAkb[i])}`}>
                      {cell(r, i)}
                    </td>
                  ))}
                  <td className="px-2 py-1.5 text-right font-semibold tabular-nums text-ink">
                    {unit === "sum" ? money(r.revenue) : unit === "share" ? pct(r.countryShare, 1) : kg(r.kg)}
                  </td>
                </tr>
              ))}
            </tbody>
            {unit !== "share" && (
              <tfoot>
                <tr className="bg-muted font-semibold text-ink">
                  <td className="sticky left-0 z-10 bg-muted px-2 py-2">Итого</td>
                  {data.regions.map((reg, i) => (
                    <td key={reg.id} className="px-2 py-2 text-right tabular-nums">
                      {unit === "sum" ? money(m.totalRevenue[i]) : kg(m.totalKg[i])}
                    </td>
                  ))}
                  <td className="px-2 py-2 text-right tabular-nums">{unit === "sum" ? money(m.revenue) : kg(m.kg)}</td>
                </tr>
              </tfoot>
            )}
          </table>
        </div>
      )}
      <Note>
        Доля — вес SKU в весе региона (по строкам таблицы). Наведите на клетку — АКБ SKU в регионе. Регионы — по убыванию веса за период; ABC здесь — по всей
        стране.
      </Note>
    </Section>
  );
}

/** SKU × месяцы: кг по месяцам отрезка (с регионом — по региону), внизу — итог и кг в день. */
export function SkuMonthsTable({ data }: { data: SkuSalesView }) {
  const columns: Column<SkuSalesRow>[] = [
    { key: "name", label: "Продукт", value: (r) => r.name, render: (r) => <ProductCell name={r.name} top={r.isTop} sub={r.category} /> },
    ...data.months.map(
      (m, i): Column<SkuSalesRow> => ({
        key: `m${i}`,
        label: `${monthShort(m.month)}${m.partial ? "*" : ""}${data.months.some((x) => x.year !== m.year) ? ` ${String(m.year).slice(2)}` : ""}`,
        align: "right",
        value: (r) => r.months[i],
        render: (r) => (r.months[i] ? kg(r.months[i]) : <span className="text-ink-3">·</span>),
      }),
    ),
    { key: "kg", label: "Итого", align: "right", value: (r) => r.kg, render: (r) => <span className="font-semibold">{kg(r.kg)}</span> },
  ];
  const totals = data.monthTotals;

  return (
    <DataTable
      title="По месяцам"
      hint={`кг; ${data.regionName ? `регион ${data.regionName}` : "вся страна"}; возвраты вычтены`}
      columns={columns}
      rows={data.rows}
      rowKey={(r) => String(r.productId)}
      limit={60}
      empty="Продаж за период нет"
      footer={
        data.rows.length > 0 && (
          <Footer
            rows={[
              [{ value: "Итого" }, ...totals.map((m) => ({ value: kg(m.kg), right: true })), { value: kg(data.totals.kg), right: true }],
              [{ value: "Кг в день" }, ...totals.map((m) => ({ value: kg(m.kgPerDay), right: true })), { value: "" }],
            ]}
          />
        )
      }
      note="* — месяц не закрыт: данные по отчётный день; кг в день — по дням с данными."
    />
  );
}

/** Категории × регионы: вес категорий по регионам и доля категории в весе региона. */
export function SkuCategoryMatrix({ data }: { data: SkuSalesView }) {
  const c = data.categoryRegions;
  const th = "px-2 py-2 text-right text-[11px] font-semibold uppercase tracking-wide text-ink-3";
  const head = (last: string) => (
    <thead>
      <tr className="border-b border-line">
        <th className="sticky left-0 z-10 bg-surface px-2 py-2 text-left text-[11px] font-semibold uppercase tracking-wide text-ink-3">Категория</th>
        {data.regions.map((r) => (
          <th key={r.id} className={th}>
            {r.name}
          </th>
        ))}
        <th className={th}>{last}</th>
        {last === "Итого" && <th className={th}>Доля кг</th>}
      </tr>
    </thead>
  );
  const name = (n: string) => <td className="sticky left-0 z-10 bg-surface px-2 py-1.5 text-ink">{n}</td>;

  return (
    <Section title="Категории × регионы" hint="вес категорий по регионам и доля категории в весе региона">
      {c.rows.length === 0 ? (
        <p className="py-6 text-center text-sm text-ink-3">Продаж за период нет</p>
      ) : (
        <div className="space-y-6">
          <div className="-mx-4 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
            <table className="w-full min-w-max border-collapse text-xs">
              {head("Итого")}
              <tbody>
                {c.rows.map((r) => (
                  <tr key={r.name} className="border-b border-line">
                    {name(r.name)}
                    {r.kg.map((v, i) => (
                      <td key={i} className="px-2 py-1.5 text-right tabular-nums text-ink-2">
                        {v ? kg(v) : <span className="text-ink-3">·</span>}
                      </td>
                    ))}
                    <td className="px-2 py-1.5 text-right font-semibold tabular-nums text-ink">{kg(r.totalKg)}</td>
                    <td className="px-2 py-1.5 text-right tabular-nums text-ink-2">{pct(r.totalShare, 1)}</td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr className="bg-muted font-semibold text-ink">
                  <td className="sticky left-0 z-10 bg-muted px-2 py-2">Итого</td>
                  {c.totalKg.map((v, i) => (
                    <td key={i} className="px-2 py-2 text-right tabular-nums">
                      {kg(v)}
                    </td>
                  ))}
                  <td className="px-2 py-2 text-right tabular-nums">{kg(c.kg)}</td>
                  <td />
                </tr>
              </tfoot>
            </table>
          </div>
          <div>
            <h3 className="mb-2 text-sm font-semibold text-ink">Доля категории в весе региона</h3>
            <div className="-mx-4 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
              <table className="w-full min-w-max border-collapse text-xs">
                {head("Страна")}
                <tbody>
                  {c.rows.map((r) => (
                    <tr key={r.name} className="border-b border-line">
                      {name(r.name)}
                      {r.share.map((v, i) => (
                        <td key={i} className="px-2 py-1.5 text-right tabular-nums text-ink-2">
                          {v ? pct(v, 1) : <span className="text-ink-3">·</span>}
                        </td>
                      ))}
                      <td className="px-2 py-1.5 text-right font-semibold tabular-nums text-ink">{pct(r.totalShare, 1)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}
    </Section>
  );
}

/** Топ-10 SKU каждого региона по весу; доля — от веса региона. */
export function SkuTopRegions({ data }: { data: SkuSalesView }) {
  return (
    <Section title="Топ-10 регионов" hint="топ-10 SKU каждого региона по весу; доля — от веса региона">
      {data.topRegions.length === 0 ? (
        <p className="py-6 text-center text-sm text-ink-3">Продаж за период нет</p>
      ) : (
        <div className="grid gap-4 md:grid-cols-2 min-[87.5rem]:grid-cols-3">
          {data.topRegions.map((r) => (
            <div key={r.id} className="rounded-xl border border-line bg-surface p-4 shadow-sm">
              <div className="font-semibold text-ink">{r.name}</div>
              <div className="text-xs text-ink-3">{kg(r.kg)} кг</div>
              <table className="mt-2 w-full text-xs">
                <tbody>
                  {r.items.map((it, i) => (
                    <tr key={it.productId} className="border-b border-line last:border-0">
                      <td className="w-6 py-1 pr-1 text-right tabular-nums text-ink-3">{i + 1}</td>
                      <td className="max-w-[220px] py-1 pr-2" title={it.name}>
                        <span className="flex items-center gap-1.5">
                          {it.isTop && <TopChip />}
                          <span className="truncate text-ink">{it.name}</span>
                        </span>
                      </td>
                      <td className="py-1 pr-2 text-right tabular-nums text-ink">{kg(it.kg)}</td>
                      <td className="py-1 text-right tabular-nums text-ink-2">{pct(it.share, 1)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ))}
        </div>
      )}
    </Section>
  );
}

/** «Выводы»: концентрация веса — числа с сервера, здесь только текст. */
export function SkuFacts({ data, period }: { data: SkuSalesView; period: string }) {
  const f = data.facts;
  const scope = [data.regionName ?? "вся страна", data.top === "only" ? "только ТОП" : data.top === "not" ? "кроме ТОПа" : null, data.selectedCategories.join(", ") || null]
    .filter(Boolean)
    .join(" · ");
  return (
    <Section title="Выводы" hint={`${period} · ${scope}; без фильтра ABC`}>
      {f.skus === 0 ? (
        <p className="py-6 text-center text-sm text-ink-3">Продаж за период нет</p>
      ) : (
        <div className="space-y-3 text-sm leading-relaxed text-ink-2">
          <p>
            <b className="text-ink">Итого:</b> {kg(f.kg)} кг, {money(f.revenue)} сум, {num(f.skus)} SKU с продажами
            {data.regionId == null ? `, ${regionsLabel(f.regions)}` : ""}.
          </p>
          <div>
            <div className="font-semibold text-ink">Концентрация</div>
            <p>
              Один SKU — {pct(f.topKgShare, 1)} всего веса: {f.topName} ({kg(f.topKg)} кг, {money(f.topRevenue)} сум, {pct(f.topRevenueShare, 1)} денег). Топ-5 SKU —{" "}
              {pct(f.top5Share)} веса, топ-10 — {pct(f.top10Share)}, топ-25 — {pct(f.top25Share)}.
            </p>
            <p>
              Группа A — {num(f.skusA)} SKU (первые 80% веса), B — {num(f.skusB)} (до 95%). Хвост группы C — {num(f.skusC)} SKU, вместе {pct(f.groupCShare, 1)} веса:
              кандидаты на чистку матрицы.
            </p>
          </div>
          {data.outsideReport > 0 && (
            <p className="text-xs text-ink-3">
              Ещё {productsLabel(data.outsideReport)} других типов Linko (бонус, подарки) {plural(data.outsideReport, ["продавался", "продавались", "продавались"])} за
              период, но в восемь категорий отчёта не {plural(data.outsideReport, ["входит", "входят", "входят"])} — здесь {plural(data.outsideReport, ["его", "их", "их"])} нет.
            </p>
          )}
        </div>
      )}
    </Section>
  );
}
