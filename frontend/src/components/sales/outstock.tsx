"use client";

import { useState } from "react";
import { Section } from "./bits";
import { DataTable, NameCell, type Column } from "./DataTable";
import { kg, money, num, pct } from "@/lib/sales/format";
import { queryWith, withQuery } from "@/lib/sales/query";
import type { OutstockMatrix, OutstockMatrixRow, OutstockPair, OutstockProduct, OutstockRegion, OutstockTopProduct } from "@/lib/sales/types";

/** Зелёная метка ТОП-товара. */
export function TopChip({ title = "ТОП-товар" }: { title?: string }) {
  return (
    <span title={title} className="inline-block shrink-0 rounded border border-ok/40 bg-ok-soft px-1 py-px text-[10px] font-semibold uppercase leading-4 text-ok">
      ТОП
    </span>
  );
}

/** Цвет доли: красный — большая, оранжевый — заметная. */
const shareClass = (share: number | null, bad = 0.1, warn = 0.05) =>
  share == null ? "text-ink-3" : share >= bad ? "font-semibold text-bad" : share >= warn ? "text-warn" : "text-ink-2";

const dayLabel = (from: string, i: number) => {
  const d = new Date(from);
  d.setDate(d.getDate() + i);
  return `${d.getDate()}.${String(d.getMonth() + 1).padStart(2, "0")}`;
};

/** Полоса дней: зелёная клетка — товар утром был, красная — нет. */
function DaysStrip({ days, from }: { days: string; from: string }) {
  return (
    <span className="inline-flex gap-px" aria-label={`${days.split("0").length - 1} дней в нуле из ${days.length}`}>
      {Array.from(days).map((d, i) => (
        <span key={i} title={`${dayLabel(from, i)}: ${d === "1" ? "товар был" : "нет товара"}`} className={`h-3.5 w-1.5 rounded-sm ${d === "1" ? "bg-ok/60" : "bg-bad"}`} />
      ))}
    </span>
  );
}

function Whose({ p }: { p: OutstockPair }) {
  if (p.zeroDays === 0) return <span className="text-ink-3">—</span>;
  return (
    <span className="whitespace-nowrap text-xs">
      {p.dealerDays > 0 && <span className="text-warn">недовоз {num(p.dealerDays)}</span>}
      {p.dealerDays > 0 && p.factoryDays > 0 && <span className="text-ink-3"> · </span>}
      {p.factoryDays > 0 && <span className="text-bad">завод {num(p.factoryDays)}</span>}
    </span>
  );
}

const chip = (cls: string, text: string) => <span className={`inline-block whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium ${cls}`}>{text}</span>;

/** Название с меткой ТОП; длинные названия переносятся в пределах колонки, иначе таблица уезжает за край. */
function ProductName({ name, top, sub, max = "max-w-[320px]" }: { name: string; top: boolean; sub?: string | null; max?: string }) {
  return (
    <span className={`flex items-start gap-1.5 whitespace-normal ${max} max-lg:max-w-[44vw]`}>
      {top && (
        <span className="mt-0.5">
          <TopChip />
        </span>
      )}
      <NameCell name={name} sub={sub} />
    </span>
  );
}

/** Пары «товар × регион» с днями в нуле. */
export function OutstockPairsTable({
  rows,
  from,
  title,
  hint,
  limit,
  showRegion = true,
}: {
  rows: OutstockPair[];
  from: string;
  title: string;
  hint?: string;
  limit?: number;
  showRegion?: boolean;
}) {
  const columns: Column<OutstockPair>[] = [
    {
      key: "product",
      label: "Товар",
      value: (r) => r.product,
      render: (r) => <ProductName name={r.product} top={r.top} sub={[showRegion ? r.region : null, r.code ? `код ${r.code}` : null].filter(Boolean).join(" · ")} />,
    },
    { key: "cat", label: "Категория", value: (r) => r.category },
    { key: "zero", label: "Дней в нуле", align: "right", value: (r) => r.zeroDays, render: (r) => <span className={r.zeroDays > 0 ? "font-semibold text-bad" : "text-ink-3"}>{num(r.zeroDays)}</span> },
    { key: "days", label: "По дням", value: (r) => r.days, render: (r) => <DaysStrip days={r.days} from={from} /> },
    { key: "period", label: "Продано, кг", align: "right", value: (r) => r.periodKg, render: (r) => kg(r.periodKg) },
    { key: "perDay", label: "В день, кг", align: "right", value: (r) => r.perDayKg, render: (r) => kg(r.perDayKg) },
    { key: "lostKg", label: "Упущено, кг", align: "right", value: (r) => r.lostKg, render: (r) => kg(r.lostKg) },
    { key: "price", label: "Цена, сум/кг", align: "right", value: (r) => r.avgPrice, render: (r) => num(r.avgPrice) },
    { key: "lostSum", label: "Упущено, сум", align: "right", value: (r) => r.lostSum, render: (r) => <span className={r.lostSum > 0 ? "font-semibold text-ink" : "text-ink-3"}>{money(r.lostSum)}</span> },
    { key: "whose", label: "Чья потеря, дн.", value: (r) => r.dealerDays, render: (r) => <Whose p={r} /> },
    {
      key: "flags",
      label: "Признаки",
      value: (r) => (r.chronic ? 1 : 0) + (r.core ? 2 : 0),
      render: (r) => (
        <span className="inline-flex gap-1">
          {r.core && chip("bg-accent-soft text-accent-strong", "ядро")}
          {r.chronic && chip("bg-bad-soft text-bad", "хронический")}
        </span>
      ),
    },
    { key: "snapshot", label: "Остаток сейчас, кг", align: "right", value: (r) => r.snapshotKg, render: (r) => kg(r.snapshotKg) },
  ];
  return (
    <DataTable
      title={title}
      hint={hint}
      columns={columns}
      rows={rows}
      rowKey={(r) => `${r.regionId}:${r.productId}`}
      limit={limit}
      empty="Дней без товара не найдено"
      note="Дни в нуле — утром на складе дилера было не больше 0,5 кг товара, который регион в этом периоде продавал. Упущено, кг = дней в нуле × средние продажи в день; сумы — по средней цене товара у дилера за период. «Ядро» — пары, которые вместе дают 80% потерь; «хронический» — в нуле половину периода и дольше; «недовоз» — на складе завода в тот день товар был."
    />
  );
}

function TopList({ items }: { items: OutstockTopProduct[] }) {
  if (!items.length) return <span className="text-ink-3">—</span>;
  return (
    <span className="flex flex-col gap-0.5 text-xs">
      {items.map((p) => (
        <span key={p.productId} className="flex items-center gap-1.5 whitespace-nowrap">
          {p.top && <TopChip />}
          <span className="max-w-[260px] truncate font-medium text-ink max-lg:max-w-[40vw]" title={p.name}>
            {p.name}
          </span>
          <span className="text-ink-2">— {money(p.lostSum)}</span>
          <span className="text-ink-3">({pct(p.share)})</span>
        </span>
      ))}
    </span>
  );
}

/** По регионам: клик по строке — календарь по дням этого региона. linkQuery — период и фильтры из адреса. */
export function OutstockRegionsTable({ rows, linkQuery, limit }: { rows: OutstockRegion[]; linkQuery: string; limit?: number }) {
  const columns: Column<OutstockRegion>[] = [
    { key: "name", label: "Регион", value: (r) => r.name, render: (r) => <NameCell name={r.name} sub={r.dealer} /> },
    { key: "sold", label: "Продажи, кг", align: "right", value: (r) => r.soldKg, render: (r) => kg(r.soldKg) },
    { key: "lostKg", label: "Упущено, кг", align: "right", value: (r) => r.lostKg, render: (r) => kg(r.lostKg) },
    { key: "lostSum", label: "Упущено, сум", align: "right", value: (r) => r.lostSum, render: (r) => <span className="font-semibold">{money(r.lostSum)}</span> },
    { key: "share", label: "К факту", align: "right", value: (r) => r.lossShare, render: (r) => <span className={shareClass(r.lossShare)}>{pct(r.lossShare, 1)}</span> },
    { key: "zero", label: "Дней в нуле", align: "right", value: (r) => r.zeroDays, render: (r) => num(r.zeroDays) },
    { key: "pairs", label: "Позиций", align: "right", value: (r) => r.pairs, render: (r) => num(r.pairs) },
    {
      key: "top",
      label: "Что теряем больше всего",
      value: (r) => r.top.map((p) => `${p.name} — ${money(p.lostSum)} (${pct(p.share)})`).join("; "),
      render: (r) => <TopList items={r.top} />,
    },
  ];
  return (
    <DataTable
      title="По регионам"
      hint="клик — календарь по дням: что, когда стояло в нуле и когда довезли"
      columns={columns}
      rows={rows}
      rowKey={(r) => r.id}
      rowHref={(r) => withQuery("/sales/outstock", queryWith(linkQuery, { region: r.id }))}
      limit={limit}
      empty="Потерь нет"
      note="«К факту» — упущенные кг к проданным за период. «Позиций» — товаров региона, которые хотя бы день стояли в нуле. «Дней в нуле» — сумма по этим товарам."
    />
  );
}

/** Дилеры × категории: упущено по каждой категории у каждого региона; переключатель сум/кг. */
export function OutstockMatrixTable({ matrix }: { matrix: OutstockMatrix }) {
  const [unit, setUnit] = useState<"sum" | "kg">("sum");
  const fmt = unit === "sum" ? money : kg;
  const pick = (r: OutstockMatrixRow, i: number) => (unit === "sum" ? r.sum[i] : r.kg[i]);
  const cell = (v: number) => (v > 0 ? fmt(v) : <span className="text-ink-3">·</span>);
  const columns: Column<OutstockMatrixRow>[] = [
    { key: "name", label: "Регион · дилер", value: (r) => r.name, render: (r) => <NameCell name={r.name} sub={r.dealer} /> },
    ...matrix.categories.map((c, i) => ({
      key: `c${i}`,
      label: c,
      align: "right" as const,
      value: (r: OutstockMatrixRow) => pick(r, i),
      render: (r: OutstockMatrixRow) => cell(pick(r, i)),
    })),
    {
      key: "total",
      label: "Итого",
      align: "right",
      value: (r) => (unit === "sum" ? r.totalSum : r.totalKg),
      render: (r) => <span className="font-semibold">{fmt(unit === "sum" ? r.totalSum : r.totalKg)}</span>,
    },
  ];
  const toggle = (
    <div className="flex rounded-lg border border-line text-xs">
      {(["sum", "kg"] as const).map((u) => (
        <button key={u} type="button" onClick={() => setUnit(u)} className={`px-2.5 py-1 max-lg:py-2 ${unit === u ? "bg-accent text-white" : "text-ink hover:bg-muted"}`}>
          {u === "sum" ? "сум" : "кг"}
        </button>
      ))}
    </div>
  );
  const totals = unit === "sum" ? matrix.totalSum : matrix.totalKg;
  const hint = matrix.categories.length ? `упущено по каждой категории у каждого дилера · итого: ${matrix.categories.map((c, i) => `${c} ${fmt(totals[i])}`).join(", ")}` : "упущено по каждой категории у каждого дилера";
  return <DataTable title="Дилеры × категории" hint={hint} actions={toggle} columns={columns} rows={matrix.rows} rowKey={(r) => r.id} empty="Потерь нет" />;
}

/** По товарам: где дыры на полке стоят дороже всего. */
export function OutstockProductsTable({ rows, limit }: { rows: OutstockProduct[]; limit?: number }) {
  const columns: Column<OutstockProduct>[] = [
    {
      key: "name",
      label: "Продукт",
      value: (r) => r.name,
      render: (r) => <ProductName name={r.name} top={r.top} sub={[r.category, r.code ? `код ${r.code}` : null].filter(Boolean).join(" · ")} max="max-w-[460px]" />,
    },
    { key: "sold", label: "Продажи, кг", align: "right", value: (r) => r.soldKg, render: (r) => kg(r.soldKg) },
    { key: "zeroShare", label: "Доля дней в нуле", align: "right", value: (r) => r.zeroShare, render: (r) => <span className={shareClass(r.zeroShare, 0.3, 0.15)}>{pct(r.zeroShare, 1)}</span> },
    { key: "lostKg", label: "Упущено, кг", align: "right", value: (r) => r.lostKg, render: (r) => kg(r.lostKg) },
    { key: "lostSum", label: "Упущено, сум", align: "right", value: (r) => r.lostSum, render: (r) => <span className="font-semibold">{money(r.lostSum)}</span> },
    { key: "regions", label: "Регионов", align: "right", value: (r) => r.regions, render: (r) => <span className={r.regions >= 4 ? "font-semibold text-bad" : ""}>{num(r.regions)}</span> },
  ];
  return (
    <DataTable
      title="По товарам"
      hint="где дыры на полке стоят дороже всего"
      columns={columns}
      rows={rows}
      rowKey={(r) => String(r.id)}
      limit={limit}
      empty="Потерь нет"
      note="Доля дней в нуле — среди дней тех регионов, где товар хотя бы день стоял в нуле (регионов × дней периода). «Регионов» — сколько таких регионов."
    />
  );
}

/** Календарь региона по дням: красная клетка — утром товара не было, зелёная — был, точка — в этот день привезли с завода. */
export function OutstockCalendar({ pairs, from, days, region }: { pairs: OutstockPair[]; from: string; days: number; region: string }) {
  const [showAll, setShowAll] = useState(false);
  const limit = 40;
  const rows = showAll ? pairs : pairs.slice(0, limit);
  const labels = Array.from({ length: days }, (_, i) => dayLabel(from, i));
  return (
    <Section title={`Календарь по дням · ${region}`} hint="что и когда стояло в нуле, когда довезли">
      <div className="mb-3 flex flex-wrap gap-x-4 gap-y-1 text-xs text-ink-2">
        <span className="inline-flex items-center gap-1.5">
          <span className="inline-block h-3.5 w-3 rounded-sm bg-bad" /> нет товара
        </span>
        <span className="inline-flex items-center gap-1.5">
          <span className="inline-block h-3.5 w-3 rounded-sm bg-ok/50" /> товар был
        </span>
        <span className="inline-flex items-center gap-1.5">
          <span className="inline-block size-2 rounded-full bg-accent" /> привезли с завода
        </span>
      </div>
      {pairs.length === 0 ? (
        <p className="text-sm text-ink-3">Дней без товара не найдено</p>
      ) : (
        <div className="-mx-4 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
          <table className="border-separate border-spacing-0 text-xs">
            <thead>
              <tr>
                <th className="sticky left-0 z-10 bg-surface py-1 pr-3 text-left font-medium text-ink-3">Товар</th>
                {labels.map((l, i) => (
                  <th key={i} className="px-px py-1 text-center font-medium tabular-nums text-ink-3" title={l}>
                    {i + 1}
                  </th>
                ))}
                <th className="py-1 pl-3 text-right font-medium text-ink-3">В нуле</th>
                <th className="py-1 pl-3 text-right font-medium text-ink-3">Упущено, сум</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((p) => (
                <tr key={p.productId}>
                  <td className="sticky left-0 z-10 border-t border-line bg-surface py-1 pr-3">
                    <span className="flex items-center gap-1.5">
                      {p.top && <TopChip />}
                      <span className="block max-w-[260px] truncate font-medium text-ink max-lg:max-w-[36vw]" title={p.product}>
                        {p.product}
                      </span>
                    </span>
                  </td>
                  {Array.from(p.days).map((d, i) => {
                    const arrived = p.received[i] === "1";
                    return (
                      <td key={i} className="border-t border-line px-px py-1">
                        <span
                          title={`${labels[i]}: ${d === "1" ? "товар был" : "нет товара"}${arrived ? " · привезли с завода" : ""}`}
                          className={`relative mx-auto block h-4 w-3.5 rounded-sm ${d === "1" ? "bg-ok/50" : "bg-bad"}`}
                        >
                          {arrived && <span className="absolute left-1/2 top-0 size-2 -translate-x-1/2 -translate-y-1/2 rounded-full bg-accent ring-1 ring-surface" />}
                        </span>
                      </td>
                    );
                  })}
                  <td className="border-t border-line py-1 pl-3 text-right font-semibold tabular-nums text-bad">{num(p.zeroDays)}</td>
                  <td className="border-t border-line py-1 pl-3 text-right tabular-nums">{money(p.lostSum)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {pairs.length > limit && (
        <button type="button" onClick={() => setShowAll((v) => !v)} className="mt-3 text-xs font-medium text-accent hover:underline max-lg:min-h-11">
          {showAll ? "Свернуть" : `Показать все ${num(pairs.length)}`}
        </button>
      )}
    </Section>
  );
}
