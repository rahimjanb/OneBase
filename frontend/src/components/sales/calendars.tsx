"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { Fragment, useMemo, useState } from "react";
import { ChevronRight } from "lucide-react";
import { Note, Section, levelClass } from "./bits";
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

const WEEKDAYS = ["вс", "пн", "вт", "ср", "чт", "пт", "сб"];

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
      <td className={`${cell} ${row.planShare == null ? "" : levelClass(row.planShareLevel)}`}>{pct(row.planShare)}</td>
    </>
  );
}

/**
 * Выбор дней (DOC-filters §1): «с 1-го по N-е» накопительно (по умолчанию — по последний день с фактом) или один день;
 * дни после отчётного помечены «факта нет». Меняет from/to в адресе — считает сервер.
 */
function DayRange({ from, to, maxDay, factDays, year, month }: { from: number; to: number; maxDay: number; factDays: number; year: number; month: number }) {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const value = `${from}-${to}`;
  // «С 1-го по N-е» — от 2-го дня (1-е число — это один день) по последний день с фактом.
  const cumulative = Array.from({ length: Math.max(0, factDays - 1) }, (_, i) => i + 2);
  const days = Array.from({ length: maxDay }, (_, i) => i + 1);
  const listed = (from === 1 && to <= factDays) || from === to;
  return (
    <select
      aria-label="Дни"
      value={value}
      onChange={(e) => {
        const [f, t] = e.target.value.split("-");
        const next = new URLSearchParams(params);
        next.set("from", f);
        next.set("to", t);
        router.push(`${pathname}?${next}`, { scroll: false });
      }}
      className="h-8 rounded-lg border border-line bg-surface px-2 text-xs text-ink"
    >
      {cumulative.length > 0 && (
        <optgroup label="С начала месяца">
          {cumulative.map((day) => (
            <option key={day} value={`1-${day}`}>
              с 1-го по {day}-е
            </option>
          ))}
        </optgroup>
      )}
      <optgroup label="Один день">
        {days.map((day) => (
          <option key={day} value={`${day}-${day}`}>
            {day} · {WEEKDAYS[new Date(year, month - 1, day).getDay()]}
            {day > factDays ? " · факта нет" : ""}
          </option>
        ))}
      </optgroup>
      {!listed && <option value={value}>{`с ${from}-го по ${to}-е`}</option>}
    </select>
  );
}

/**
 * Календарь визитов: регионы (раскрываются до ТП) или сразу ТП региона.
 * rows — верхний уровень; если у строк есть children, они раскрываются по клику. Итог — с сервера: у региона это его же строка (rows[0]),
 * у республики и РМ — totalRow.
 */
export function VisitCalendarTable({
  rows,
  totalRow,
  flatten = false,
  totalName,
  from,
  to,
  maxDay,
  factDays,
  year,
  month,
}: {
  rows: VisitCalendarRow[];
  /** Итог по строкам регионов (республика, РМ) — считает сервер. */
  totalRow?: VisitCalendarRow;
  /** Показать детей первой строки как основные строки (уровень региона). */
  flatten?: boolean;
  totalName: string;
  from: number;
  to: number;
  maxDay: number;
  /** Последний день с фактом (отчётный день): дни после него — «факта нет». */
  factDays: number;
  year: number;
  month: number;
}) {
  const [sort, setSort] = useState<{ key: string; dir: "asc" | "desc" } | null>(null);
  const [open, setOpen] = useState<Set<string>>(new Set());

  const top = flatten ? (rows[0]?.children ?? []) : rows;
  const sorted = useMemo(() => sortRows(top, visitColumns, sort), [top, sort]);
  const total: VisitCalendarRow | null = flatten ? (rows[0] ? { ...rows[0], name: totalName } : null) : totalRow ? { ...totalRow, name: totalName } : null;
  const singleDay = from === to;
  const noFact = from > factDays;

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
      hint={singleDay ? `${from}-е число${noFact ? " · факта нет" : ""}` : `с ${from}-го по ${to}-е число`}
      actions={
        <>
          <DayRange from={from} to={to} maxDay={maxDay} factDays={factDays} year={year} month={month} />
          <CopyButton onCopy={() => toTsv(visitColumns, [...sorted.flatMap((r) => [r, ...r.children]), ...(total ? [total] : [])])} />
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
                          {row.visitsOutsideTeam > 0 && (
                            <span className="block text-xs text-ink-3" title="Выполненные визиты не ТП (операторы, супервайзеры): в строки ТП не входят">
                              не ТП: {num(row.visitsOutsideTeam)} визитов
                            </span>
                          )}
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
          {total && (
            <tfoot>
              <tr className="border-t-2 border-line bg-muted/60 font-semibold">
                <td className="px-3 py-2">
                  <span className="block text-ink">{total.name}</span>
                  {total.subtitle && <span className="block text-xs font-normal text-ink-3">{total.subtitle}</span>}
                </td>
                <VisitCells row={total} />
              </tr>
            </tfoot>
          )}
        </table>
      </div>
      {top.length === 0 && <p className="py-6 text-center text-sm text-ink-3">{noFact ? "За этот день факта ещё нет" : "Визитов за период нет"}</p>}
      <Note>
        Строки — ТП подразделения (как в «Команде ТП»). План — визиты по маршруту за выбранные дни; «по маршруту» — плановый визит выполнен,
        «вне маршрута» — визит без плана. Заказы — принятые в эти дни; «с визита», если в день ввода заказа в этот магазин был выполненный визит
        (по маршруту или вне его), иначе — без визита{total && total.ordersNoVisit > 0 ? `: таких ${num(total.ordersNoVisit)}` : ""}. Под числом заказов —
        их сумма. «Не посещено» и «% плана» у региона и итога — по их суммарному плану и визитам.
        {total && total.visitsOutsideTeam > 0 && ` Ещё ${num(total.visitsOutsideTeam)} выполненных визитов — не ТП (операторы, супервайзеры), в строки не входят.`}
        {!flatten && " Нажмите на регион, чтобы раскрыть торговых представителей."}
      </Note>
    </Section>
  );
}

// ---------- Календарь месяца по ТП / по регионам ----------

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
  title = "Календарь месяца по ТП",
  rowLabel = "ТП",
  rowKind = "agent",
}: {
  calendar: MonthCalendar;
  year: number;
  month: number;
  categories: { id: number; name: string }[];
  title?: string;
  rowLabel?: string;
  /** Строки — ТП (с ID) или регионы. */
  rowKind?: "agent" | "region";
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
      [rowLabel, ...days.map(String), "Итого"].join("\t"),
      ...rows.map((r) => [r.name, ...r.days.map((d) => (d == null ? "" : String(d).replace(".", ","))), String(r.total).replace(".", ",")].join("\t")),
      ["Итого", ...calendar.totalDays.map((d) => (d == null ? "" : String(d).replace(".", ","))), String(calendar.total).replace(".", ",")].join("\t"),
    ].join("\n");

  return (
    <Section
      title={title}
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
                  {rowLabel}
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
                  {rowKind === "agent" && <span className="block text-[11px] text-ink-3">ID {r.id}</span>}
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
        Воскресенья выделены серым столбцом, «·» — нет данных за день. Кг и деньги: сумма дней — итог месяца. АКБ в итогах — уникальные ТТ за период,
        а не сумма по дням.
      </Note>
    </Section>
  );
}
