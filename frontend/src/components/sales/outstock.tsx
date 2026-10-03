"use client";

import { DataTable, NameCell, type Column } from "./DataTable";
import { kg, money, num } from "@/lib/sales/format";
import { queryWith, withQuery } from "@/lib/sales/query";
import type { OutstockGroup, OutstockPair } from "@/lib/sales/types";

/** Полоса дней: зелёная клетка — товар утром был, красная — нет. */
function DaysStrip({ days, from }: { days: string; from: string }) {
  const start = new Date(from);
  return (
    <span className="inline-flex gap-px" aria-label={`${days.split("0").length - 1} дней в нуле из ${days.length}`}>
      {Array.from(days).map((d, i) => {
        const date = new Date(start);
        date.setDate(start.getDate() + i);
        return (
          <span
            key={i}
            title={`${date.getDate()}.${String(date.getMonth() + 1).padStart(2, "0")}: ${d === "1" ? "товар был" : "нет товара"}`}
            className={`h-3.5 w-1.5 rounded-sm ${d === "1" ? "bg-ok/60" : "bg-bad"}`}
          />
        );
      })}
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

/** Пары «товар × регион»: ядро потерь или все пары с потерями. */
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
      // Длинные названия переносятся в пределах колонки, иначе таблица уезжает за край.
      render: (r) => (
        <span className="block max-w-[320px] whitespace-normal max-lg:max-w-[44vw]">
          <NameCell name={r.product} sub={[showRegion ? r.region : null, r.code ? `код ${r.code}` : null].filter(Boolean).join(" · ")} />
        </span>
      ),
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
      note="Дни в нуле — утром на складе дилера было не больше 0,5 кг товара, который регион в этом периоде продавал. Упущено, кг = дней в нуле × средние продажи в день; сумы — по средней цене товара у дилера за период. «Недовоз» — на складе завода в тот день товар был."
    />
  );
}

/** Итоги по регионам или товарам. */
/** linkQuery — период из адреса: строка региона открывает аутсток этого региона. */
export function OutstockGroupTable({ rows, title, hint, nameLabel, limit, linkQuery }: { rows: OutstockGroup[]; title: string; hint?: string; nameLabel: string; limit?: number; linkQuery?: string }) {
  const columns: Column<OutstockGroup>[] = [
    { key: "name", label: nameLabel, value: (r) => r.name, render: (r) => <NameCell name={r.name} /> },
    { key: "pairs", label: "Товаров", align: "right", value: (r) => r.pairs, render: (r) => num(r.pairs) },
    { key: "core", label: "В ядре", align: "right", value: (r) => r.corePairs, render: (r) => num(r.corePairs) },
    { key: "chronic", label: "Хронических", align: "right", value: (r) => r.chronic, render: (r) => <span className={r.chronic > 0 ? "text-bad" : "text-ink-3"}>{num(r.chronic)}</span> },
    { key: "lostKg", label: "Упущено, кг", align: "right", value: (r) => r.lostKg, render: (r) => kg(r.lostKg) },
    { key: "lostSum", label: "Упущено, сум", align: "right", value: (r) => r.lostSum, render: (r) => <span className="font-semibold">{money(r.lostSum)}</span> },
    { key: "dealer", label: "Недовоз, сум", align: "right", value: (r) => r.dealerLossSum, render: (r) => money(r.dealerLossSum) },
    { key: "factory", label: "Завод, сум", align: "right", value: (r) => r.factoryLossSum, render: (r) => money(r.factoryLossSum) },
  ];
  const href = linkQuery == null ? undefined : (r: OutstockGroup) => withQuery("/sales/outstock", queryWith(linkQuery, { region: r.id }));
  return <DataTable title={title} hint={hint} columns={columns} rows={rows} rowKey={(r) => r.id} rowHref={href} limit={limit} empty="Потерь нет" />;
}
