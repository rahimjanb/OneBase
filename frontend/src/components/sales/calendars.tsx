"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { Fragment, useMemo, useState } from "react";
import { ArrowRight, ChevronRight } from "lucide-react";
import { useT } from "@/i18n/client";
import { Note, Section, levelClass } from "./bits";
import { CopyButton, TableHead, nextSort, sortRows, toTsv, type Column } from "./DataTable";
import { kg, money, num, pct } from "@/lib/sales/format";
import { withQuery } from "@/lib/sales/query";
import type { MonthCalendar, MonthCalendarRow, VisitCalendarRow } from "@/lib/sales/types";

/** Строка продаж без региона: её имя «Без региона» — подпись сервера (переводится), у остальных строк имя — данные. */
const NO_REGION = "00000000-0000-0000-0000-000000000000";

// ---------- Календарь визитов ----------

// Подписи колонок — русские ключи словаря: их переводят TableHead и toTsv.
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
  const t = useT();
  return (
    <span className="block">
      <span className="block">{count > 0 ? num(count) : "·"}</span>
      {count > 0 && <span className="block text-[11px] text-ink-3">{money(sum, t)}</span>}
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
  const t = useT();
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
      aria-label={t("Дни")}
      value={value}
      onChange={(e) => {
        const [f, to] = e.target.value.split("-");
        const next = new URLSearchParams(params);
        next.set("from", f);
        next.set("to", to);
        router.push(`${pathname}?${next}`, { scroll: false });
      }}
      className="h-8 rounded-lg border border-line bg-surface px-2 text-xs text-ink"
    >
      {cumulative.length > 0 && (
        <optgroup label={t("С начала месяца")}>
          {cumulative.map((day) => (
            <option key={day} value={`1-${day}`}>
              {t("с 1-го по {day}-е", { day })}
            </option>
          ))}
        </optgroup>
      )}
      <optgroup label={t("Один день")}>
        {days.map((day) => (
          <option key={day} value={`${day}-${day}`}>
            {day} · {t(WEEKDAYS[new Date(year, month - 1, day).getDay()])}
            {day > factDays ? ` · ${t("факта нет")}` : ""}
          </option>
        ))}
      </optgroup>
      {!listed && <option value={value}>{t("с {from}-го по {to}-е", { from, to })}</option>}
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
  const t = useT();
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
      title={t("Календарь визитов")}
      hint={singleDay ? `${t("{day}-е число", { day: from })}${noFact ? ` · ${t("факта нет")}` : ""}` : t("с {from}-го по {to}-е число", { from, to })}
      actions={
        <>
          <DayRange from={from} to={to} maxDay={maxDay} factDays={factDays} year={year} month={month} />
          <CopyButton onCopy={() => toTsv(visitColumns, [...sorted.flatMap((r) => [r, ...r.children]), ...(total ? [total] : [])], t)} />
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
                          <span className="block font-medium text-ink">{row.id === NO_REGION ? t(row.name) : row.name}</span>
                          {row.subtitle && <span className="block text-xs text-ink-3">{t(row.subtitle)}</span>}
                          {row.visitsOutsideTeam > 0 && (
                            <span className="block text-xs text-ink-3" title={t("Выполненные визиты не ТП (операторы, супервайзеры): в строки ТП не входят")}>
                              {t("не ТП: {n} визитов", { n: num(row.visitsOutsideTeam) })}
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
                  {total.subtitle && <span className="block text-xs font-normal text-ink-3">{t(total.subtitle)}</span>}
                </td>
                <VisitCells row={total} />
              </tr>
            </tfoot>
          )}
        </table>
      </div>
      {top.length === 0 && <p className="py-6 text-center text-sm text-ink-3">{noFact ? t("За этот день факта ещё нет") : t("Визитов за период нет")}</p>}
      <Note>
        {t(
          "Строки — ТП подразделения (как в «Команде ТП»). План — визиты по маршруту за выбранные дни; «по маршруту» — плановый визит выполнен, «вне маршрута» — визит без плана. Заказы — принятые в эти дни; «с визита», если в день ввода заказа в этот магазин был выполненный визит (по маршруту или вне его), иначе — без визита{noVisit}. Под числом заказов — их сумма. «Не посещено» и «% плана» у региона и итога — по их суммарному плану и визитам.",
          { noVisit: total && total.ordersNoVisit > 0 ? t(": таких {n}", { n: num(total.ordersNoVisit) }) : "" },
        )}
        {total && total.visitsOutsideTeam > 0 && t(" Ещё {n} выполненных визитов — не ТП (операторы, супервайзеры), в строки не входят.", { n: num(total.visitsOutsideTeam) })}
        {!flatten && t(" Нажмите на регион, чтобы раскрыть торговых представителей.")}
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

/** Строка «прочие» под регионом: продажи без ТП или не ТП (офис, каналы) — подпись переводится, у ТП имя — данные. */
const OTHER = "other";

export function MonthCalendarTable({
  calendar,
  year,
  month,
  categories,
  title = "Календарь месяца по ТП",
  rowLabel: rowText = "ТП",
  rowKind = "agent",
  query = "",
}: {
  calendar: MonthCalendar;
  year: number;
  month: number;
  categories: { id: number; name: string }[];
  title?: string;
  rowLabel?: string;
  /** Строки — ТП (с ID) или регионы (раскрываются до ТП, → — страница региона). */
  rowKind?: "agent" | "region";
  /** Период и охват для ссылки «→ открыть регион». */
  query?: string;
}) {
  const t = useT();
  const rowLabel = t(rowText);
  const pathname = usePathname();
  const params = useSearchParams();
  const router = useRouter();
  const [sortByTotal, setSortByTotal] = useState(true);
  const [open, setOpen] = useState<Set<string>>(new Set());

  const format = (v: number | null) => (v == null ? "·" : calendar.metric === "sum" ? money(v, t) : calendar.metric === "akb" ? num(v) : kg(v));
  const href = (metric: string, category?: number | null) => {
    const next = new URLSearchParams(params);
    next.set("metric", metric);
    if (category) next.set("category", String(category));
    else next.delete("category");
    return `${pathname}?${next}`;
  };
  const toggle = (id: string) =>
    setOpen((s) => {
      const next = new Set(s);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  // У ТП Linko имя часто начинается с его же номера («476 Хиндол»): номер уже стоит строкой «ID 476» под именем.
  const nameOf = (r: MonthCalendarRow) =>
    r.id === NO_REGION || r.id === OTHER ? t(r.name) : r.name.startsWith(`${r.id} `) ? r.name.slice(r.id.length + 1).trim() || r.name : r.name;
  // «Прочие» — всегда последней строкой под регионом.
  const sortRowsOf = (list: MonthCalendarRow[]) =>
    [...list].sort(
      (a, b) =>
        Number(a.id === OTHER) - Number(b.id === OTHER) || (sortByTotal ? b.total - a.total : nameOf(a).localeCompare(nameOf(b), "ru")),
    );
  const rows = sortRowsOf(calendar.rows);
  const childrenOf = (r: MonthCalendarRow) => sortRowsOf(r.children ?? []);
  const expandable = rowKind === "region" && rows.some((r) => (r.children?.length ?? 0) > 0);
  const days = calendar.sundays.map((_, i) => i + 1);

  const cell = (v: number | null) => (v == null ? "" : String(v).replace(".", ","));
  const tsv = () =>
    [
      [rowLabel, ...days.map(String), t("Итого")].join("\t"),
      ...rows.flatMap((r) => [
        [nameOf(r), ...r.days.map(cell), cell(r.total)].join("\t"),
        ...(open.has(r.id) ? childrenOf(r).map((c) => [`  ${nameOf(c)}`, ...c.days.map(cell), cell(c.total)].join("\t")) : []),
      ]),
      [t("Итого"), ...calendar.totalDays.map(cell), cell(calendar.total)].join("\t"),
    ].join("\n");

  const dayCells = (r: MonthCalendarRow) =>
    r.days.map((v, i) => (
      <td key={i} className={`px-1.5 py-1.5 text-right tabular-nums ${calendar.sundays[i] ? "bg-muted" : ""} ${v == null ? "text-ink-3" : "text-ink"}`}>
        {format(v)}
      </td>
    ));

  return (
    <Section
      title={t(title)}
      hint={`${t("{period} · столбец = день месяца", { period: `${String(month).padStart(2, "0")}.${year}` })}${
        expandable ? ` · ${t("клик по региону — развернуть его ТП, → — открыть регион")}` : ""
      }`}
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
                {t(m.label)}
              </Link>
            ))}
          </div>
          <select
            aria-label={t("АКБ по категории")}
            value={calendar.metric === "akb" && calendar.categoryId ? String(calendar.categoryId) : ""}
            onChange={(e) => router.push(e.target.value ? href("akb", Number(e.target.value)) : href("akb"), { scroll: false })}
            className="h-8 max-w-44 rounded-lg border border-line bg-surface px-2 text-xs text-ink"
          >
            <option value="">{t("АКБ по категории…")}</option>
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
              <th className="sticky left-0 z-10 bg-surface-solid px-2 py-2 text-left font-semibold">
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
                  {t("Итого")}
                </button>
              </th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => {
              const children = childrenOf(r);
              const canOpen = expandable && children.length > 0;
              const isOpen = canOpen && open.has(r.id);
              const agents = children.filter((c) => c.id !== OTHER).length;
              const sub = rowKind === "region" ? [r.subtitle, agents > 0 ? t("{n} ТП", { n: agents }) : null].filter(Boolean).join(" · ") : "";
              return (
                <Fragment key={r.id}>
                  <tr
                    onClick={canOpen ? () => toggle(r.id) : undefined}
                    className={`group border-b border-line ${canOpen ? "cursor-pointer hover:bg-muted" : ""} ${isOpen ? "bg-muted/60" : ""}`}
                  >
                    <td className={`sticky left-0 z-10 px-2 py-1.5 ${isOpen ? "bg-muted-solid" : "bg-surface-solid"} ${canOpen ? "group-hover:bg-muted-solid" : ""}`}>
                      <span className="flex items-start gap-1.5">
                        {expandable && (
                          <ChevronRight
                            className={`mt-0.5 size-3.5 shrink-0 text-ink-3 transition-transform ${isOpen ? "rotate-90" : ""} ${canOpen ? "" : "invisible"}`}
                          />
                        )}
                        <span className="min-w-0">
                          <span className="flex items-center gap-1.5">
                            <span className="whitespace-nowrap text-[13px] font-medium text-ink">{nameOf(r)}</span>
                            {rowKind === "region" && r.id !== NO_REGION && (
                              <Link
                                href={withQuery(`/sales/regions/${r.id}`, query)}
                                onClick={(e) => e.stopPropagation()}
                                aria-label={t("Открыть страницу региона")}
                                title={t("Открыть страницу региона")}
                                className="grid size-5 shrink-0 place-items-center rounded-md border border-line text-ink-3 hover:border-accent hover:text-accent"
                              >
                                <ArrowRight className="size-3" />
                              </Link>
                            )}
                          </span>
                          {rowKind === "agent" && <span className="block text-[11px] text-ink-3">ID {r.id}</span>}
                          {sub && <span className="block whitespace-nowrap text-[11px] text-ink-3">{sub}</span>}
                        </span>
                      </span>
                    </td>
                    {dayCells(r)}
                    <td className="px-2 py-1.5 text-right font-semibold tabular-nums">{format(r.total)}</td>
                  </tr>
                  {isOpen &&
                    children.map((c) => (
                      <tr key={`${r.id}-${c.id}`} className="border-b border-line">
                        <td className="sticky left-0 z-10 bg-surface-solid py-1.5 pl-7 pr-2">
                          <span className="block whitespace-nowrap border-l-2 border-line pl-2.5">
                            <span className="block text-[13px] text-ink">{nameOf(c)}</span>
                            <span className="block text-[11px] text-ink-3">{c.id === OTHER ? t("без ТП и не ТП: офис, каналы") : `ID ${c.id}`}</span>
                          </span>
                        </td>
                        {dayCells(c)}
                        <td className="px-2 py-1.5 text-right tabular-nums text-ink">{format(c.total)}</td>
                      </tr>
                    ))}
                </Fragment>
              );
            })}
          </tbody>
          <tfoot>
            <tr className="border-t-2 border-line bg-muted/60 font-semibold">
              <td className="sticky left-0 z-10 bg-muted-solid px-2 py-1.5">{t("Итого")}</td>
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
        {t(
          "Воскресенья выделены серым столбцом, «·» — нет данных за день. Кг и деньги: сумма дней — итог месяца. АКБ в итогах — уникальные ТТ за период, а не сумма по дням.",
        )}
        {expandable &&
          t(
            " Под регионом — его ТП (как в «Команде ТП» региона) и «Прочие продажи» — без ТП и не ТП: кг и деньги ТП в сумме равны строке региона, АКБ — нет: точку могут обслуживать двое.",
          )}
      </Note>
    </Section>
  );
}
