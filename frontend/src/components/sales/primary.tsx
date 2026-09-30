"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useMemo, useState } from "react";
import { ExecutionBar, KpiTile, Note, Section, execClass } from "./bits";
import { CopyButton, DataTable, NameCell, type Column } from "./DataTable";
import { date, kg, money, monthGenitive, monthName, monthShort, num, pct, tsvValue } from "@/lib/sales/format";
import type { PrimaryAmounts, PrimaryRow, PrimaryView } from "@/lib/sales/types";

// «Первичка → Республика» как в «Полевом контроле»: плитки месяца, столбики по месяцам, план и факт по категориям и
// дилерам, календарь отгрузок, «С начала года» и товары. Переключатель единиц меняет графики и таблицы сразу.

type Unit = keyof PrimaryAmounts;

const units: { key: Unit; label: string }[] = [
  { key: "kg", label: "кг" },
  { key: "boxes", label: "коробки" },
  { key: "sumFactory", label: "сум завода" },
  { key: "sumDealer", label: "сум дилера" },
];

const fmt = (value: number | null | undefined, unit: Unit) => (unit === "kg" ? kg(value) : unit === "boxes" ? num(value) : money(value));
const tons = (value: number | null | undefined) => (value == null ? "—" : num(value / 1000, 1));
const barTone = (v: number | null) => (v == null ? "accent" : v < 0.5 ? "bad" : v < 0.9 ? "warn" : "accent");

/** Идёт ли месяц сейчас (последний столбик — штриховкой, в подписи «идёт»). */
function isRunning(year: number, month: number) {
  const now = new Date();
  return now.getFullYear() === year && now.getMonth() + 1 === month;
}

function UnitSwitch({ unit, onChange, only }: { unit: Unit; onChange: (u: Unit) => void; only?: Unit[] }) {
  return (
    <div className="inline-flex overflow-hidden rounded-full border border-line bg-surface text-xs shadow-sm" role="group" aria-label="Единицы">
      {units.filter((u) => !only || only.includes(u.key)).map((u) => (
        <button
          key={u.key}
          type="button"
          onClick={() => onChange(u.key)}
          aria-pressed={unit === u.key}
          className={`px-3 py-1.5 font-medium ${unit === u.key ? "bg-accent text-white" : "text-ink-2 hover:bg-muted"}`}
        >
          {u.label}
        </button>
      ))}
    </div>
  );
}

function MonthTiles({ data }: { data: PrimaryView }) {
  const plan = data.planMonthKg;
  const fact = data.monthTotal.kg;
  const exec = plan ? fact / plan : null;
  return (
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3 min-[87.5rem]:grid-cols-5">
      <KpiTile label="План месяца" value={plan != null ? kg(plan) : "—"} unit={plan != null ? "кг" : undefined}>
        {plan != null ? `отгрузка дилерам за ${monthName(data.month)}` : "план первички на месяц не загружен"}
      </KpiTile>
      <KpiTile label="Отгружено" value={kg(fact)} unit="кг">
        {num(data.monthTotal.boxes)} коробок · {money(data.monthTotal.sumFactory)}
      </KpiTile>
      <KpiTile label="Выполнение" value={pct(exec, 1)}>
        {plan != null ? (
          <>
            {kg(fact)} из {kg(plan)} кг
            <div className="mt-1.5">
              <ExecutionBar value={exec} tone={barTone(exec)} />
            </div>
          </>
        ) : (
          "нужен план месяца"
        )}
      </KpiTile>
      <KpiTile label="Осталось" value={plan != null ? kg(Math.max(0, plan - fact)) : "—"} unit={plan != null ? "кг" : undefined}>
        {plan == null ? "нужен план месяца" : fact >= plan ? `план выполнен, сверх него ${kg(fact - plan)} кг` : "до плана месяца"}
      </KpiTile>
      <KpiTile label="Прогноз" value={kg(data.forecastKg ?? fact)} unit="кг">
        {data.forecastKg != null
          ? `${plan ? `${pct(data.forecastKg / plan)} плана · ` : ""}темп по ${data.workedDays}-е число`
          : data.dataThrough
            ? "месяц закрыт — это факт"
            : "отгрузок в месяце нет"}
      </KpiTile>
    </div>
  );
}

/** Столбики по месяцам: план — светлая мишень во всю высоту плана, факт — заливка внутри. Клик — открыть месяц. */
function MonthsChart({ data, unit }: { data: PrimaryView; unit: Unit }) {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const last = Math.max(data.month, ...data.monthsWithData, 1);
  const months = Array.from({ length: last }, (_, i) => i + 1);
  const planOn = unit === "kg" && data.planMonths.some((p) => p != null);
  const values = months.map((m) => data.months[m - 1][unit]);
  const plans = months.map((m) => (planOn ? data.planMonths[m - 1] : null));
  const max = Math.max(1, ...values, ...plans.map((p) => p ?? 0));

  const open = (m: number) => {
    const next = new URLSearchParams(params);
    next.set("year", String(data.year));
    next.set("month", String(m));
    router.push(`${pathname}?${next}`);
  };

  return (
    <Section title="По месяцам" hint={`${data.year} · клик — открыть месяц${isRunning(data.year, last) ? " · последний месяц ещё идёт" : ""}`}>
      <div className="flex h-[260px] items-end gap-2 sm:gap-4">
        {months.map((m, i) => {
          const v = values[i];
          const plan = plans[i];
          const exec = plan ? data.months[m - 1].kg / plan : null;
          const running = isRunning(data.year, m);
          const selected = m === data.month;
          return (
            <button key={m} type="button" onClick={() => open(m)} className="group flex h-full min-w-0 flex-1 flex-col items-center justify-end" title={`${monthName(m)}: ${fmt(v, unit)}${plan ? ` из ${kg(plan)} кг плана` : ""}`}>
              <span className="mb-1 whitespace-nowrap text-[11px] font-semibold tabular-nums text-ink-2">{v ? fmt(v, unit) : ""}</span>
              <div className="relative w-full max-w-[56px]" style={{ height: `${(Math.max(v, plan ?? 0) / max) * 190}px` }}>
                {plan != null && <div className="absolute inset-x-0 bottom-0 rounded-t bg-accent/15" style={{ height: `${(plan / Math.max(v, plan)) * 100}%` }} />}
                <div
                  className={`absolute inset-x-1 bottom-0 rounded-t ${running ? "border border-accent" : "bg-accent group-hover:bg-accent-strong"}`}
                  style={{
                    height: `${(v / Math.max(v, plan ?? 0, 1)) * 100}%`,
                    backgroundImage: running ? "repeating-linear-gradient(-45deg, var(--color-accent) 0 4px, transparent 4px 8px)" : undefined,
                  }}
                />
              </div>
              <span className={`mt-2 whitespace-nowrap text-xs ${selected ? "font-semibold text-accent-strong underline underline-offset-4" : "text-ink-2"}`}>
                {monthShort(m)}
                {running ? " (идёт)" : ""}
              </span>
              <span className={`text-[11px] tabular-nums ${execClass(exec)}`}>{exec != null ? pct(exec) : " "}</span>
            </button>
          );
        })}
      </div>
      <div className="mt-3 flex flex-wrap gap-4 text-xs text-ink-2">
        <span className="inline-flex items-center gap-1.5">
          <span className="size-2.5 rounded-sm bg-accent" />
          Факт
        </span>
        {planOn && (
          <span className="inline-flex items-center gap-1.5">
            <span className="size-2.5 rounded-sm bg-accent/15" />
            План
          </span>
        )}
        <span className="inline-flex items-center gap-1.5">
          <span className="size-2.5 rounded-sm border border-accent" style={{ backgroundImage: "repeating-linear-gradient(-45deg, var(--color-accent) 0 2px, transparent 2px 4px)" }} />
          месяц идёт
        </span>
      </div>
    </Section>
  );
}

function TotalsFooter({ cells }: { cells: { value: React.ReactNode; right?: boolean }[] }) {
  return (
    <tfoot>
      <tr className="bg-muted font-semibold text-ink">
        {cells.map((c, i) => (
          <td key={i} className={`px-3 py-2.5 ${c.right ? "text-right tabular-nums" : ""}`}>
            {c.value}
          </td>
        ))}
      </tr>
    </tfoot>
  );
}

/** План и факт за выбранный месяц: категории или дилеры. План — только в кг; в других единицах — факт и доля. */
function PlanFactTable({ title, nameLabel, rows, data, unit }: { title: string; nameLabel: string; rows: PrimaryRow[]; data: PrimaryView; unit: Unit }) {
  const planOn = unit === "kg" && rows.some((r) => r.planMonthKg != null);
  const shown = rows.filter((r) => r.month[unit] !== 0 || (planOn && r.planMonthKg != null));
  const total = data.monthTotal[unit];
  const planTotal = shown.reduce((s, r) => s + (r.planMonthKg ?? 0), 0);

  const columns: Column<PrimaryRow>[] = [
    { key: "name", label: nameLabel, value: (r) => r.name, render: (r) => <NameCell name={r.name} sub={r.sub} /> },
    ...(planOn ? [{ key: "plan", label: "План", align: "right" as const, value: (r: PrimaryRow) => r.planMonthKg, render: (r: PrimaryRow) => kg(r.planMonthKg) }] : []),
    { key: "fact", label: "Факт", align: "right", value: (r) => r.month[unit], render: (r) => <span className="font-semibold">{fmt(r.month[unit], unit)}</span> },
    ...(planOn
      ? [
          {
            key: "exec",
            label: "Выполнение",
            align: "right" as const,
            value: (r: PrimaryRow) => (r.planMonthKg ? r.month.kg / r.planMonthKg : null),
            render: (r: PrimaryRow) => {
              const exec = r.planMonthKg ? r.month.kg / r.planMonthKg : null;
              return (
                <span className="inline-flex items-center justify-end gap-3">
                  <span className={`w-12 tabular-nums ${execClass(exec)}`}>{pct(exec)}</span>
                  <span className="w-28">
                    <ExecutionBar value={exec} tone={barTone(exec)} />
                  </span>
                </span>
              );
            },
          },
          {
            key: "left",
            label: "Осталось",
            align: "right" as const,
            value: (r: PrimaryRow) => (r.planMonthKg != null ? r.planMonthKg - r.month.kg : null),
            render: (r: PrimaryRow) =>
              r.planMonthKg == null ? "—" : r.month.kg >= r.planMonthKg ? <span className="text-ok">выполнен</span> : <span className="text-bad">{kg(r.planMonthKg - r.month.kg)}</span>,
          },
        ]
      : [{ key: "share", label: "Доля", align: "right" as const, value: (r: PrimaryRow) => (total ? r.month[unit] / total : null), render: (r: PrimaryRow) => pct(total ? r.month[unit] / total : null, 1) }]),
  ];

  return (
    <DataTable
      title={title}
      hint={`за ${monthName(data.month)}${planOn ? ", в килограммах" : ""}`}
      columns={columns}
      rows={shown}
      rowKey={(r) => r.id}
      empty="В этом месяце отгрузок нет"
      footer={
        shown.length > 0 && (
          <TotalsFooter
            cells={[
              { value: "Итого" },
              ...(planOn ? [{ value: kg(planTotal), right: true }] : []),
              { value: fmt(total, unit), right: true },
              ...(planOn
                ? [
                    { value: <span className={execClass(planTotal ? data.monthTotal.kg / planTotal : null)}>{pct(planTotal ? data.monthTotal.kg / planTotal : null)}</span>, right: true },
                    { value: kg(Math.max(0, planTotal - data.monthTotal.kg)), right: true },
                  ]
                : [{ value: "100%", right: true }]),
            ]}
          />
        )
      }
    />
  );
}

/** Календарь отгрузок: дилер × день. Клик по клетке — что отгрузили; по дилеру — весь его месяц; по дню — все дилеры. */
/** Диапазоны дней для фильтра календаря: все дни с отгрузкой или декада. */
const dayRanges = [
  { key: "all", label: "все дни", from: 1, to: 31 },
  { key: "d1", label: "1–10", from: 1, to: 10 },
  { key: "d2", label: "11–20", from: 11, to: 20 },
  { key: "d3", label: "21–31", from: 21, to: 31 },
] as const;

function ShipmentCalendar({
  data,
  unit,
  nameLabel = "Дилер",
  allLabel = "все дилеры",
  dealerSum = true,
}: {
  data: PrimaryView;
  unit: Unit;
  nameLabel?: string;
  allLabel?: string;
  /** Колонка «Сумма дилера» в разборе дня (у экспорта её нет). */
  dealerSum?: boolean;
}) {
  const [pick, setPick] = useState<{ dealer: string | null; day: number | null } | null>(null);
  const [range, setRange] = useState<(typeof dayRanges)[number]["key"]>("all");
  const span = dayRanges.find((r) => r.key === range)!;
  const days = useMemo(
    () => [...new Set(data.monthLines.map((l) => l.day))].filter((d) => d >= span.from && d <= span.to).sort((a, b) => a - b),
    [data.monthLines, span],
  );
  const dealers = data.dealers.filter((d) => d.month.kg !== 0 || d.month.sumFactory !== 0);
  const cell = useMemo(() => {
    const map = new Map<string, number>();
    for (const l of data.monthLines) {
      const key = `${l.dealerId}|${l.day}`;
      map.set(key, (map.get(key) ?? 0) + l[unit]);
    }
    return map;
  }, [data.monthLines, unit]);
  // Итог и число дней отгрузки строки — по выбранным дням.
  const rowTotal = (id: string) => days.reduce((sum, d) => sum + (cell.get(`${id}|${d}`) ?? 0), 0);
  const rowDays = (id: string) => days.filter((d) => cell.get(`${id}|${d}`)).length;
  const dayTotal = (d: number) => dealers.reduce((sum, r) => sum + (cell.get(`${r.id}|${d}`) ?? 0), 0);

  const detail = useMemo(() => {
    if (!pick) return [];
    const byProduct = new Map<string, PrimaryAmounts>();
    for (const l of data.monthLines) {
      if ((pick.dealer && l.dealerId !== pick.dealer) || (pick.day && l.day !== pick.day)) continue;
      const key = String(l.productId ?? "—");
      const a = byProduct.get(key) ?? { kg: 0, boxes: 0, sumFactory: 0, sumDealer: 0 };
      byProduct.set(key, { kg: a.kg + l.kg, boxes: a.boxes + l.boxes, sumFactory: a.sumFactory + l.sumFactory, sumDealer: a.sumDealer + l.sumDealer });
    }
    return [...byProduct.entries()].map(([id, a]) => ({ id, name: data.productNames[id] ?? "Без товара", ...a })).sort((x, y) => y.kg - x.kg);
  }, [pick, data.monthLines, data.productNames]);

  if (data.monthLines.length === 0) return null;
  const dealerName = (id: string | null) => (id ? (data.dealers.find((d) => d.id === id)?.name ?? id) : allLabel);
  const copy = () =>
    [
      [nameLabel, ...days.map(String), "Итого", "Дней"].join("\t"),
      ...dealers.map((r) => [r.name, ...days.map((d) => tsvValue(cell.get(`${r.id}|${d}`) ?? "")), tsvValue(rowTotal(r.id)), String(rowDays(r.id))].join("\t")),
      ["Итого", ...days.map((d) => tsvValue(dayTotal(d))), tsvValue(days.reduce((sum, d) => sum + dayTotal(d), 0)), String(days.length)].join("\t"),
    ].join("\n");
  const th = "px-2 py-2 text-right text-[11px] font-semibold uppercase tracking-wide text-ink-3";
  const pickCls = (on: boolean) => (on ? "bg-accent-soft font-semibold text-ink" : "");

  return (
    <Section
      title="Календарь отгрузок"
      hint="клик по клетке — что отгрузили в этот день"
      actions={
        <>
          <select
            value={range}
            onChange={(e) => setRange(e.target.value as typeof range)}
            aria-label="Дни"
            className="h-8 rounded-lg border border-line bg-surface px-2 text-xs text-ink"
          >
            {dayRanges.map((r) => (
              <option key={r.key} value={r.key}>
                {r.label}
              </option>
            ))}
          </select>
          <CopyButton onCopy={copy} />
        </>
      }
    >
      <div className="-mx-4 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
        <table className="w-full min-w-max border-collapse text-xs">
          <thead>
            <tr className="border-b border-line">
              <th className="sticky left-0 z-10 bg-surface px-2 py-2 text-left text-[11px] font-semibold uppercase tracking-wide text-ink-3">{nameLabel}</th>
              {days.map((d) => (
                <th key={d} className={th}>
                  <button type="button" onClick={() => setPick({ dealer: null, day: d })} className="hover:text-accent-strong">
                    {d}
                  </button>
                </th>
              ))}
              <th className={th}>Итого</th>
              <th className={th}>Дней</th>
            </tr>
          </thead>
          <tbody>
            {dealers.map((r) => (
              <tr key={r.id} className="border-b border-line">
                <td className={`sticky left-0 z-10 bg-surface px-2 py-1.5 ${pickCls(pick?.dealer === r.id && pick.day == null)}`}>
                  <button type="button" onClick={() => setPick({ dealer: r.id, day: null })} className="text-left font-medium text-ink hover:text-accent-strong" title={r.sub ?? undefined}>
                    {r.name}
                  </button>
                </td>
                {days.map((d) => {
                  const v = cell.get(`${r.id}|${d}`);
                  return (
                    <td key={d} className={`px-1 py-1 text-right tabular-nums ${pickCls(pick?.dealer === r.id && pick.day === d)}`}>
                      {v ? (
                        <button type="button" onClick={() => setPick({ dealer: r.id, day: d })} className="w-full rounded px-1 py-0.5 text-right text-ink hover:bg-muted">
                          {fmt(v, unit)}
                        </button>
                      ) : (
                        <span className="px-1 text-ink-3">·</span>
                      )}
                    </td>
                  );
                })}
                <td className="px-2 py-1.5 text-right font-semibold tabular-nums text-ink">{fmt(range === "all" ? r.month[unit] : rowTotal(r.id), unit)}</td>
                <td className="px-2 py-1.5 text-right tabular-nums text-ink-2">{rowDays(r.id)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="bg-muted/60 font-semibold">
              <td className="sticky left-0 z-10 bg-muted px-2 py-1.5 text-ink">Итого</td>
              {days.map((d) => (
                <td key={d} className="px-2 py-1.5 text-right tabular-nums text-ink">
                  {fmt(dayTotal(d), unit)}
                </td>
              ))}
              <td className="px-2 py-1.5 text-right tabular-nums text-ink">{fmt(days.reduce((sum, d) => sum + dayTotal(d), 0), unit)}</td>
              <td className="px-2 py-1.5 text-right tabular-nums text-ink">{days.length}</td>
            </tr>
          </tfoot>
        </table>
        {days.length === 0 && <p className="py-3 text-center text-sm text-ink-3">В эти дни отгрузок не было</p>}
      </div>

      {pick && (
        <div className="mt-4 rounded-lg border border-line bg-muted/40 p-3">
          <div className="mb-2 flex flex-wrap items-center justify-between gap-2 text-sm">
            <span className="font-semibold text-ink">
              {dealerName(pick.dealer)} · {pick.day ? `${pick.day} ${monthGenitive(data.month)}` : `весь ${monthName(data.month)}`}
              <span className="ml-2 font-normal text-ink-3">{detail.length} товаров</span>
            </span>
            <button type="button" onClick={() => setPick(null)} className="rounded-lg border border-line bg-surface px-2.5 py-1 text-xs font-medium text-ink hover:bg-muted">
              Сбросить
            </button>
          </div>
          {detail.length === 0 ? (
            <p className="text-sm text-ink-3">В этот день отгрузок нет.</p>
          ) : (
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-line text-[11px] uppercase tracking-wide text-ink-3">
                  <th className="py-1.5 text-left font-semibold">Товар</th>
                  <th className="py-1.5 text-right font-semibold">Коробок</th>
                  <th className="py-1.5 text-right font-semibold">Кг</th>
                  <th className="py-1.5 text-right font-semibold">{dealerSum ? "Сумма завода" : "Сумма"}</th>
                  {dealerSum && <th className="py-1.5 text-right font-semibold">Сумма дилера</th>}
                </tr>
              </thead>
              <tbody>
                {detail.map((d) => (
                  <tr key={d.id} className="border-b border-line last:border-0">
                    <td className="max-w-[420px] py-1.5 pr-3 text-ink">{d.name}</td>
                    <td className="py-1.5 text-right tabular-nums text-ink-2">{d.boxes ? num(d.boxes) : "—"}</td>
                    <td className="py-1.5 text-right tabular-nums text-ink">{kg(d.kg)}</td>
                    <td className="py-1.5 text-right tabular-nums text-ink-2">{money(d.sumFactory)}</td>
                    {dealerSum && <td className="py-1.5 text-right tabular-nums text-ink-2">{money(d.sumDealer)}</td>}
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}
    </Section>
  );
}

function YtdTiles({ data }: { data: PrimaryView }) {
  const y = data.ytd;
  const margin = y.sumDealer - y.sumFactory;
  const exec = data.planYtdKg ? y.kg / data.planYtdKg : null;
  return (
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3 min-[87.5rem]:grid-cols-5">
      <KpiTile label="Отгружено" value={tons(y.kg)} unit="т">
        {num(data.ytdArticles)} артикулов
      </KpiTile>
      <KpiTile label="Сумма завода" value={money(y.sumFactory)}>
        {y.kg ? `${money(y.sumFactory / y.kg)} за кг по цене завода` : "—"}
      </KpiTile>
      <KpiTile label="Сумма дилера" value={money(y.sumDealer)}>
        {y.sumFactory ? `наценка дилеру ${money(margin)} · ${pct(margin / y.sumFactory, 1)}` : "—"}
      </KpiTile>
      <KpiTile label="Возвраты" value={tons(data.ytdReturnsKg)} unit="т">
        {num(data.ytdReturnLines)} строк · {pct(y.kg ? data.ytdReturnsKg / y.kg : null, 1)} от отгруженного
      </KpiTile>
      <KpiTile label="Выполнение" value={pct(exec, 1)}>
        {data.planYtdKg != null ? `план ${tons(data.planYtdKg)} т` : "план первички не загружен"}
      </KpiTile>
    </div>
  );
}

/** С начала года по месяцам: категории или дилеры; доля — от итога за год; план и выполнение — только в кг. */
function YearMatrix({ title, hint, nameLabel, rows, data, unit }: { title: string; hint: string; nameLabel: string; rows: PrimaryRow[]; data: PrimaryView; unit: Unit }) {
  const months = Array.from({ length: data.month }, (_, i) => i + 1);
  const ytd = (r: PrimaryRow) => months.reduce((s, m) => s + r.months[m - 1][unit], 0);
  const planYtd = (r: PrimaryRow) => (r.planMonths.slice(0, data.month).some((p) => p != null) ? r.planMonths.slice(0, data.month).reduce<number>((s, p) => s + (p ?? 0), 0) : null);
  const planOn = unit === "kg" && rows.some((r) => planYtd(r) != null);
  const shown = rows.filter((r) => ytd(r) !== 0 || (planOn && planYtd(r) != null));
  const total = shown.reduce((s, r) => s + ytd(r), 0);
  const planTotal = shown.reduce((s, r) => s + (planYtd(r) ?? 0), 0);

  const columns: Column<PrimaryRow>[] = [
    { key: "name", label: nameLabel, value: (r) => r.name, render: (r) => <NameCell name={r.name} sub={r.sub} /> },
    ...months.map((m) => ({
      key: `m${m}`,
      label: `${monthShort(m)}${isRunning(data.year, m) ? " (идёт)" : ""}`,
      align: "right" as const,
      value: (r: PrimaryRow) => r.months[m - 1][unit],
      render: (r: PrimaryRow) => <span className={r.months[m - 1][unit] ? "text-ink" : "text-ink-3"}>{fmt(r.months[m - 1][unit], unit)}</span>,
    })),
    { key: "total", label: "Итого", align: "right", value: (r) => ytd(r), render: (r) => <span className="font-semibold">{fmt(ytd(r), unit)}</span> },
    { key: "share", label: "Доля", align: "right", value: (r) => (total ? ytd(r) / total : null), render: (r) => pct(total ? ytd(r) / total : null, 1) },
    ...(planOn
      ? [
          { key: "plan", label: "План", align: "right" as const, value: (r: PrimaryRow) => planYtd(r), render: (r: PrimaryRow) => kg(planYtd(r)) },
          {
            key: "exec",
            label: "Выполнение",
            align: "right" as const,
            value: (r: PrimaryRow) => (planYtd(r) ? r.months.slice(0, data.month).reduce((s, a) => s + a.kg, 0) / planYtd(r)! : null),
            render: (r: PrimaryRow) => {
              const p = planYtd(r);
              const v = p ? r.months.slice(0, data.month).reduce((s, a) => s + a.kg, 0) / p : null;
              return <span className={execClass(v)}>{pct(v)}</span>;
            },
          },
        ]
      : []),
  ];

  return (
    <DataTable
      title={title}
      hint={hint}
      columns={columns}
      rows={shown}
      rowKey={(r) => r.id}
      footer={
        shown.length > 0 && (
          <TotalsFooter
            cells={[
              { value: "Итого" },
              ...months.map((m) => ({ value: fmt(shown.reduce((s, r) => s + r.months[m - 1][unit], 0), unit), right: true })),
              { value: fmt(total, unit), right: true },
              { value: "100%", right: true },
              ...(planOn
                ? [
                    { value: kg(planTotal), right: true },
                    { value: <span className={execClass(planTotal ? data.ytd.kg / planTotal : null)}>{pct(planTotal ? data.ytd.kg / planTotal : null)}</span>, right: true },
                  ]
                : []),
            ]}
          />
        )
      }
    />
  );
}

function ItemsTable({ data, dealer = true }: { data: PrimaryView; dealer?: boolean }) {
  type Row = PrimaryView["items"][number];
  const columns: Column<Row>[] = [
    {
      key: "name",
      label: "Наименование",
      value: (r) => r.name,
      render: (r) => (
        <span className="block max-w-[360px] whitespace-normal">
          <NameCell name={r.name} sub={r.code ? `Артикул ${r.code}` : null} />
        </span>
      ),
    },
    { key: "cat", label: "Категория", value: (r) => r.category },
    { key: "boxes", label: "Коробок", align: "right", value: (r) => r.ytd.boxes, render: (r) => (r.ytd.boxes ? <span title={r.boxesKnown ? undefined : "часть строк без известной фасовки"}>{num(r.ytd.boxes)}{r.boxesKnown ? "" : " ≈"}</span> : "—") },
    { key: "kg", label: "Вес, кг", align: "right", value: (r) => r.ytd.kg, render: (r) => <span className="font-semibold">{kg(r.ytd.kg)}</span> },
    { key: "sf", label: dealer ? "Сумма завода" : "Сумма", align: "right", value: (r) => r.ytd.sumFactory, render: (r) => money(r.ytd.sumFactory) },
    ...(dealer ? [{ key: "sd", label: "Сумма дилера", align: "right" as const, value: (r: Row) => r.ytd.sumDealer, render: (r: Row) => money(r.ytd.sumDealer) }] : []),
    { key: "perKg", label: dealer ? "Цена завода за кг" : "Цена за кг", align: "right", value: (r) => (r.ytd.kg ? r.ytd.sumFactory / r.ytd.kg : null), render: (r) => money(r.ytd.kg ? r.ytd.sumFactory / r.ytd.kg : null) },
  ];
  if (dealer)
    columns.push({
      key: "margin",
      label: "Наценка",
      align: "right",
      value: (r) => (r.ytd.sumFactory && r.ytd.sumDealer ? r.ytd.sumDealer / r.ytd.sumFactory - 1 : null),
      render: (r) => pct(r.ytd.sumFactory && r.ytd.sumDealer ? r.ytd.sumDealer / r.ytd.sumFactory - 1 : null, 1),
    });
  return <DataTable title="Товары" hint={`${num(data.items.length)} артикулов с начала года, по убыванию веса`} columns={columns} rows={data.items} rowKey={(r) => String(r.productId)} limit={50} />;
}

/** Страница «Первичка → Республика». */
export function PrimaryRepublic({ data }: { data: PrimaryView }) {
  const [unit, setUnit] = useState<Unit>("kg");
  const monthsDone = data.monthsWithData.filter((m) => m <= data.month).length;

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center justify-end gap-3">
        <UnitSwitch unit={unit} onChange={setUnit} />
      </div>
      <MonthTiles data={data} />
      <MonthsChart data={data} unit={unit} />
      <PlanFactTable title="План и факт по категориям" nameLabel="Категория" rows={data.categories} data={data} unit={unit} />
      <PlanFactTable title="План и факт по дилерам" nameLabel="Дилер" rows={data.dealers} data={data} unit={unit} />
      <ShipmentCalendar data={data} unit={unit} />

      <h2 className="mt-8 flex flex-wrap items-baseline gap-x-3 text-lg font-semibold text-ink">
        С начала года
        <span className="text-xs font-normal uppercase tracking-[0.08em] text-ink-3">
          {monthsDone} мес. · по {date(data.dataThrough)}
        </span>
      </h2>
      <div className="mt-3">
        <YtdTiles data={data} />
      </div>
      <YearMatrix title="По категориям" hint="доля — от итога за год" nameLabel="Категория" rows={data.categories} data={data} unit={unit} />
      <YearMatrix title="По дилерам" hint="сортировка по объёму" nameLabel="Дилер" rows={data.dealers} data={data} unit={unit} />
      <ItemsTable data={data} />

      <Note>
        Первичка — отгрузка завода дилеру, это не продажи в торговые точки. Источник — перемещения Linko со склада «{data.factoryStock ?? "Завод"}» на склады дилеров
        («отдано» или «принято»); склад «{data.exportStock ?? "Экспорт"}» — экспорт, сюда не входит. Сумма завода — цена перемещения (прайс «Дилерга кириш нарх»), сумма
        дилера — по прайсу «{data.dealerPriceList ?? "Дилердан чикиш нарх"}» (текущие цены). Коробки — только у товаров, где вес коробки из названия делится на вес
        единицы ({kg(data.boxesUnknownKg)} кг с начала года без коробок). Возвраты — перемещения со складов дилеров на завод; из отгрузки не вычитаются. План первички
        в Linko нет — он загружается в «Настройки → Продажи → Планы» с видом «Первичка»; план есть только в килограммах, на коробках и суммах его колонки скрыты.
      </Note>
    </>
  );
}

/** Страница «Первичка → Экспорт»: заказы филиала «Завод» экспортным точкам — по месяцам, странам и дням. */
export function PrimaryExport({ data }: { data: PrimaryView }) {
  const [unit, setUnit] = useState<Unit>("kg");
  const fact = data.monthTotal;
  const monthsDone = data.monthsWithData.filter((m) => m <= data.month).length;

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center justify-end gap-3">
        <UnitSwitch unit={unit} onChange={setUnit} only={["kg", "sumFactory"]} />
      </div>
      <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
        <KpiTile label="Отгружено" value={kg(fact.kg)} unit="кг">
          {money(fact.sumFactory)} · {num(data.monthTransfers)} заказов · {num(data.dealers.filter((d) => d.month.kg).length)} стран
        </KpiTile>
        <KpiTile label="Прогноз" value={kg(data.forecastKg ?? fact.kg)} unit="кг">
          {data.forecastKg != null ? `темп по ${data.workedDays}-е число` : data.dataThrough ? "месяц закрыт — это факт" : "отгрузок в месяце нет"}
        </KpiTile>
      </div>
      <MonthsChart data={data} unit={unit} />
      <ShipmentCalendar data={data} unit={unit} nameLabel="Страна" allLabel="все страны" dealerSum={false} />

      <h2 className="mt-8 flex flex-wrap items-baseline gap-x-3 text-lg font-semibold text-ink">
        С начала года
        <span className="text-xs font-normal uppercase tracking-[0.08em] text-ink-3">
          {monthsDone} мес. · по {date(data.dataThrough)}
        </span>
      </h2>
      <div className="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <KpiTile label="Отгружено" value={tons(data.ytd.kg)} unit="т">
          {num(data.ytdArticles)} артикулов
        </KpiTile>
        <KpiTile label="Сумма" value={money(data.ytd.sumFactory)}>
          {data.ytd.kg ? `${money(data.ytd.sumFactory / data.ytd.kg)} за кг` : "—"}
        </KpiTile>
        <KpiTile label="Стран" value={num(data.export.counterparties)}>
          {num(data.export.transfers)} заказов за {data.year} год
        </KpiTile>
        <KpiTile label="Возвраты" value={tons(data.ytdReturnsKg)} unit="т">
          {data.ytdReturnLines ? `${num(data.ytdReturnLines)} строк` : "возвратов не было"}
        </KpiTile>
      </div>
      <YearMatrix title="По странам" hint="доля — от итога за год" nameLabel="Страна" rows={data.dealers} data={data} unit={unit} />
      <YearMatrix title="По категориям" hint="доля — от итога за год" nameLabel="Категория" rows={data.categories} data={data} unit={unit} />
      <ItemsTable data={data} dealer={false} />

      {data.notes?.map((n) => (
        <Note key={n}>{n}</Note>
      ))}
      <Note>
        Экспорт — заказы Linko филиала «Завод» торговым точкам с типом EXPORT (статус «доставлен», дата — приёмка), минус возвраты по строкам. Страна
        определяется по названию или адресу точки в Linko (например «Daler Tojikiston», «Adamium Armenia»); точка без страны в названии показывается своим
        названием. Сумма — сумма заказа в сумах. Перемещения на склад «Экспорт» сюда не входят: это внутреннее движение склада, а не продажа.
      </Note>
    </>
  );
}
