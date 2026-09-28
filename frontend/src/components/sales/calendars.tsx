"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { Fragment, useMemo, useState } from "react";
import { ChevronRight } from "lucide-react";
import { Note, Section } from "./bits";
import { CopyButton, TableHead, nextSort, sortRows, toTsv, type Column } from "./DataTable";
import { kg, money, num, pct } from "@/lib/sales/format";
import type { MonthCalendar, VisitCalendarRow } from "@/lib/sales/types";

// ---------- Календарь визитов ----------

const visitColumns: Column<VisitCalendarRow>[] = [
  { key: "name", label: "Регион / ТП", value: (r) => r.name },
  { key: "plan", label: "План", align: "right", value: (r) => r.plan },
  { key: "doneIn", group: "Факт визитов", label: "По маршруту", align: "right", value: (r) => r.doneInPlan },
  { key: "doneOff", group: "Факт визитов", label: "Вне маршрута", align: "right", value: (r) => r.doneOffPlan },
  { key: "doneAll", group: "Факт визитов", label: "Все визиты", align: "right", value: (r) => r.doneAll },
  { key: "ordIn", group: "Заказы", label: "С визита по маршруту", align: "right", value: (r) => r.ordersInPlan },
  { key: "ordOff", group: "Заказы", label: "С визита вне маршрута", align: "right", value: (r) => r.ordersOffPlan },
  { key: "ordAll", group: "Заказы", label: "Всего", align: "right", value: (r) => r.ordersTotal },
  { key: "notVisited", group: "Выполнение", label: "Не посещено", align: "right", value: (r) => r.notVisited },
  { key: "planShare", group: "Выполнение", label: "% плана", align: "right", value: (r) => r.planShare },
];

function OrderCell({ count, sum }: { count: number; sum: number }) {
  return (
    <span className="block">
      <span className="block">{count > 0 ? num(count) : "·"}</span>
      {count > 0 && <span className="block text-[11px] text-ink-3">{money(sum)}</span>}
    </span>
  );
}

function VisitCells({ row }: { row: VisitCalendarRow }) {
  const cell = "px-3 py-2 text-right tabular-nums align-top";
  const dot = (v: number) => (v > 0 ? num(v) : "·");
  return (
    <>
      <td className={cell}>{dot(row.plan)}</td>
      <td className={`${cell} bg-accent-soft/40`}>{dot(row.doneInPlan)}</td>
      <td className={`${cell} bg-accent-soft/40`}>{dot(row.doneOffPlan)}</td>
      <td className={`${cell} bg-accent-soft/40 font-semibold`}>{dot(row.doneAll)}</td>
      <td className={cell}><OrderCell count={row.ordersInPlan} sum={row.ordersInPlanSum} /></td>
      <td className={cell}><OrderCell count={row.ordersOffPlan} sum={row.ordersOffPlanSum} /></td>
      <td className={`${cell} font-semibold`}><OrderCell count={row.ordersTotal} sum={row.ordersTotalSum} /></td>
      <td className={`${cell} ${row.notVisited > 0 ? "text-bad" : ""}`}>{row.plan > 0 ? num(row.notVisited) : "·"}</td>
      <td className={cell}>{pct(row.planShare)}</td>
    </>
  );
}

function sumRows(name: string, rows: VisitCalendarRow[]): VisitCalendarRow {
  const s = (f: (r: VisitCalendarRow) => number) => rows.reduce((acc, r) => acc + f(r), 0);
  const plan = s((r) => r.plan);
  const doneIn = s((r) => r.doneInPlan);
  return {
    id: "total",
    name,
    subtitle: null,
    plan,
    doneInPlan: doneIn,
    doneOffPlan: s((r) => r.doneOffPlan),
    doneAll: s((r) => r.doneAll),
    ordersInPlan: s((r) => r.ordersInPlan),
    ordersInPlanSum: s((r) => r.ordersInPlanSum),
    ordersOffPlan: s((r) => r.ordersOffPlan),
    ordersOffPlanSum: s((r) => r.ordersOffPlanSum),
    ordersTotal: s((r) => r.ordersTotal),
    ordersTotalSum: s((r) => r.ordersTotalSum),
    notVisited: s((r) => r.notVisited),
    planShare: plan === 0 ? null : doneIn / plan,
    children: [],
  };
}

/** Выбор диапазона дней: меняет from/to в адресе. */
function DayRange({ from, to, max }: { from: number; to: number; max: number }) {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  return (
    <select
      aria-label="Диапазон дней"
      value={`${from}-${to}`}
      onChange={(e) => {
        const [f, t] = e.target.value.split("-");
        const next = new URLSearchParams(params);
        next.set("from", f);
        next.set("to", t);
        router.push(`${pathname}?${next}`, { scroll: false });
      }}
      className="h-8 rounded-lg border border-line bg-surface px-2 text-xs text-ink"
    >
      {Array.from({ length: max }, (_, i) => i + 1).map((day) => (
        <option key={day} value={`1-${day}`}>
          с 1-го по {day}-е
        </option>
      ))}
      {from !== 1 && <option value={`${from}-${to}`}>{`с ${from}-го по ${to}-е`}</option>}
    </select>
  );
}

/**
 * Календарь визитов: регионы (раскрываются до ТП) или сразу ТП региона.
 * rows — верхний уровень; если у строк есть children, они раскрываются по клику.
 */
export function VisitCalendarTable({
  rows,
  flatten = false,
  totalName,
  from,
  to,
  maxDay,
}: {
  rows: VisitCalendarRow[];
  /** Показать детей первой строки как основные строки (уровень региона). */
  flatten?: boolean;
  totalName: string;
  from: number;
  to: number;
  maxDay: number;
}) {
  const [sort, setSort] = useState<{ key: string; dir: "asc" | "desc" } | null>(null);
  const [open, setOpen] = useState<Set<string>>(new Set());

  const top = flatten ? (rows[0]?.children ?? []) : rows;
  const sorted = useMemo(() => sortRows(top, visitColumns, sort), [top, sort]);
  const total = flatten && rows[0] ? { ...rows[0], name: totalName } : sumRows(totalName, rows);

  const toggle = (id: string) =>
    setOpen((s) => {
      const next = new Set(s);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  return (
    <Section
      title="Календарь визитов"
      hint={`с ${from}-го по ${to}-е число`}
      actions={
        <>
          <DayRange from={from} to={to} max={maxDay} />
          <CopyButton onCopy={() => toTsv(visitColumns, [...sorted.flatMap((r) => [r, ...r.children]), total])} />
        </>
      }
    >
      <div className="-mx-4 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
        <table className="w-full min-w-max border-collapse text-sm">
          <TableHead columns={visitColumns} sort={sort} onSort={(key) => setSort((s) => nextSort(s, key))} />
          <tbody>
            {sorted.map((row) => {
              const expandable = row.children.length > 0;
              const isOpen = open.has(row.id);
              return (
                <Fragment key={row.id}>
                  <tr
                    onClick={expandable ? () => toggle(row.id) : undefined}
                    className={`border-b border-line ${expandable ? "cursor-pointer hover:bg-muted" : ""}`}
                  >
                    <td className="px-3 py-2 align-top">
                      <span className="flex min-w-[190px] items-start gap-1.5">
                        {expandable && (
                          <ChevronRight className={`mt-0.5 size-4 shrink-0 text-ink-3 transition-transform ${isOpen ? "rotate-90" : ""}`} />
                        )}
                        <span>
                          <span className="block font-medium text-ink">{row.name}</span>
                          {row.subtitle && <span className="block text-xs text-ink-3">{row.subtitle}</span>}
                        </span>
                      </span>
                    </td>
                    <VisitCells row={row} />
                  </tr>
                  {isOpen &&
                    row.children.map((child) => (
                      <tr key={`${row.id}-${child.id}`} className="border-b border-line bg-muted/40 text-[13px]">
                        <td className="py-2 pl-10 pr-3">
                          <span className="block text-ink">{child.name}</span>
                          {child.subtitle && <span className="block text-xs text-ink-3">{child.subtitle}</span>}
                        </td>
                        <VisitCells row={child} />
                      </tr>
                    ))}
                </Fragment>
              );
            })}
          </tbody>
          <tfoot>
            <tr className="border-t-2 border-line bg-muted/60 font-semibold">
              <td className="px-3 py-2">
                <span className="block text-ink">{total.name}</span>
                {total.subtitle && <span className="block text-xs font-normal text-ink-3">{total.subtitle}</span>}
              </td>
              <VisitCells row={total} />
            </tr>
          </tfoot>
        </table>
      </div>
      {top.length === 0 && <p className="py-6 text-center text-sm text-ink-3">Визитов за период нет</p>}
      <Note>
        План — визиты по маршруту за выбранные дни. «По маршруту» — плановый визит выполнен, «вне маршрута» — визит без плана.
        Заказ относится к визиту, если в тот же день у того же агента есть визит в этот магазин; «всего» включает и заказы без визита.
        Под числом заказов — их сумма.
        {!flatten && " Нажмите на регион, чтобы раскрыть торговых представителей."}
      </Note>
    </Section>
  );
}

// ---------- Календарь месяца по ТП ----------

const metrics = [
  { key: "kg", label: "кг" },
  { key: "sum", label: "деньги" },
  { key: "akb", label: "АКБ" },
] as const;

export function MonthCalendarTable({
  calendar,
  year,
  month,
  categories,
}: {
  calendar: MonthCalendar;
  year: number;
  month: number;
  categories: { id: number; name: string }[];
}) {
  const pathname = usePathname();
  const params = useSearchParams();
  const router = useRouter();
  const [sortByTotal, setSortByTotal] = useState(true);

  const format = (v: number | null) => (v == null ? "·" : calendar.metric === "sum" ? money(v) : calendar.metric === "akb" ? num(v) : kg(v));
  const href = (metric: string, category?: number | null) => {
    const next = new URLSearchParams(params);
    next.set("metric", metric);
    if (category) next.set("category", String(category));
    else next.delete("category");
    return `${pathname}?${next}`;
  };

  const rows = sortByTotal ? [...calendar.rows].sort((a, b) => b.total - a.total) : [...calendar.rows].sort((a, b) => a.name.localeCompare(b.name, "ru"));
  const days = calendar.sundays.map((_, i) => i + 1);

  const tsv = () =>
    [
      ["ТП", ...days.map(String), "Итого"].join("\t"),
      ...rows.map((r) => [r.name, ...r.days.map((d) => (d == null ? "" : String(d).replace(".", ","))), String(r.total).replace(".", ",")].join("\t")),
      ["Итого", ...calendar.totalDays.map((d) => (d == null ? "" : String(d).replace(".", ","))), String(calendar.total).replace(".", ",")].join("\t"),
    ].join("\n");

  return (
    <Section
      title="Календарь месяца по ТП"
      hint={`${String(month).padStart(2, "0")}.${year} · столбец = день месяца`}
      actions={
        <>
          <div className="flex overflow-hidden rounded-lg border border-line text-xs">
            {metrics.map((m) => (
              <Link
                key={m.key}
                scroll={false}
                href={href(m.key)}
                className={`px-2.5 py-1.5 ${calendar.metric === m.key && (m.key !== "akb" || !calendar.categoryId) ? "bg-accent text-white" : "bg-surface text-ink hover:bg-muted"}`}
              >
                {m.label}
              </Link>
            ))}
          </div>
          <select
            aria-label="АКБ по категории"
            value={calendar.metric === "akb" && calendar.categoryId ? String(calendar.categoryId) : ""}
            onChange={(e) => router.push(e.target.value ? href("akb", Number(e.target.value)) : href("akb"), { scroll: false })}
            className="h-8 max-w-44 rounded-lg border border-line bg-surface px-2 text-xs text-ink"
          >
            <option value="">АКБ по категории…</option>
            {categories.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
          <CopyButton onCopy={tsv} />
        </>
      }
    >
      <div className="-mx-4 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
        <table className="w-full min-w-max border-collapse text-xs">
          <thead>
            <tr className="border-b border-line text-[11px] uppercase tracking-wide text-ink-3">
              <th className="sticky left-0 z-10 bg-surface px-2 py-2 text-left font-semibold">
                <button type="button" onClick={() => setSortByTotal(false)} className="uppercase hover:text-ink">
                  ТП
                </button>
              </th>
              {days.map((d) => (
                <th key={d} className={`min-w-11 px-1.5 py-2 text-right font-semibold ${calendar.sundays[d - 1] ? "bg-muted" : ""}`}>
                  {d}
                </th>
              ))}
              <th className="px-2 py-2 text-right font-semibold">
                <button type="button" onClick={() => setSortByTotal(true)} className="uppercase hover:text-ink">
                  Итого
                </button>
              </th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.id} className="border-b border-line">
                <td className="sticky left-0 z-10 bg-surface px-2 py-1.5">
                  <span className="block whitespace-nowrap text-[13px] font-medium text-ink">{r.name}</span>
                  <span className="block text-[11px] text-ink-3">ID {r.id}</span>
                </td>
                {r.days.map((v, i) => (
                  <td key={i} className={`px-1.5 py-1.5 text-right tabular-nums ${calendar.sundays[i] ? "bg-muted" : ""} ${v == null ? "text-ink-3" : "text-ink"}`}>
                    {format(v)}
                  </td>
                ))}
                <td className="px-2 py-1.5 text-right font-semibold tabular-nums">{format(r.total)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="border-t-2 border-line bg-muted/60 font-semibold">
              <td className="sticky left-0 z-10 bg-muted px-2 py-1.5">Итого</td>
              {calendar.totalDays.map((v, i) => (
                <td key={i} className="px-1.5 py-1.5 text-right tabular-nums">
                  {format(v)}
                </td>
              ))}
              <td className="px-2 py-1.5 text-right tabular-nums">{format(calendar.total)}</td>
            </tr>
          </tfoot>
        </table>
      </div>
      <Note>
        Воскресенья выделены серым столбцом, «·» — нет данных за день. АКБ в итогах — уникальные ТТ за период, а не сумма по дням.
      </Note>
    </Section>
  );
}
