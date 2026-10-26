"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ArrowDown, ArrowUp, ArrowUpDown, UsersRound } from "lucide-react";
import { formatDate } from "@/i18n/core";
import { useLocale, useT } from "@/i18n/client";
import { plural } from "@/lib/format";
import type { HrTimesheetRow } from "@/lib/hr";
import { weekday } from "@/lib/hr";
import { dayKind, type TimesheetSort } from "@/lib/hr-timesheet";
import { DayPopover, type DayAnchor } from "./DayPopover";
import { TablePagination } from "./TablePagination";
import { TimesheetLegend } from "./TimesheetLegend";
import { columnBg, gridMinWidth, gridTemplate, TimesheetRow, TOTALS, type ColumnKind } from "./TimesheetRow";
import styles from "./timesheet.module.css";

const focus = "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-(--ts-accent)";
const OPEN_DELAY = 120;
const CLOSE_DELAY = 160;

export type TableProps = {
  from: string;
  to: string;
  dates: string[];
  weekend: boolean[];
  today: string;
  /** Строки текущей страницы. */
  rows: HrTimesheetRow[];
  /** Сколько строк после фильтров (для футера). */
  filtered: number;
  /** Сотрудников за период (до фильтров на странице). */
  employees: number;
  documents: string[];
  density: "normal" | "compact";
  sort: TimesheetSort;
  dir: "asc" | "desc";
  page: number;
  size: number;
  loading: boolean;
  demo: boolean;
  onSort: (sort: TimesheetSort) => void;
  onPage: (page: number) => void;
  onSize: (size: number) => void;
  onReset: () => void;
};

/**
 * Табель: строка заголовков закреплена сверху (при прокрутке страницы), колонка сотрудника — слева, итоги — справа (при горизонтальной
 * прокрутке дней, с тенью-разделителем). Колонки выходных и сегодняшнего дня подсвечены на всю высоту. Карточка дня — по наведению
 * (с задержкой) или нажатию на чип; нажатие закрепляет её до щелчка мимо или Escape.
 */
export function TimesheetTable(p: TableProps) {
  const t = useT();
  const locale = useLocale();
  const head = useRef<HTMLDivElement>(null);
  const body = useRef<HTMLDivElement>(null);
  const [edges, setEdges] = useState({ left: false, right: false });
  const [anchor, setAnchor] = useState<DayAnchor | null>(null);
  const openTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const closeTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const anchorRef = useRef<DayAnchor | null>(null);
  anchorRef.current = anchor;

  const n = p.dates.length;
  const template = gridTemplate(n);
  const minWidth = gridMinWidth(n);
  const todayIndex = p.dates.indexOf(p.today);
  const columns: ColumnKind[] = p.dates.map((d, i) => (i === todayIndex ? "today" : p.weekend[i] ? "weekend" : "plain"));
  const dayLabels = useMemo(() => p.dates.map((d) => formatDate(new Date(`${d}T12:00:00Z`), locale, { day: "numeric", month: "long" })), [p.dates, locale]);

  // Горизонтальная прокрутка тела двигает и строку заголовков; тени — когда есть что прокрутить влево или вправо.
  const measure = useCallback(() => {
    const el = body.current;
    if (!el) return;
    if (head.current) head.current.scrollLeft = el.scrollLeft;
    const left = el.scrollLeft > 1;
    const right = el.scrollLeft + el.clientWidth < el.scrollWidth - 1;
    setEdges((e) => (e.left === left && e.right === right ? e : { left, right }));
  }, []);

  useEffect(() => {
    measure();
    const observer = new ResizeObserver(measure);
    if (body.current) observer.observe(body.current);
    return () => observer.disconnect();
  }, [measure, n, p.rows.length]);

  const clearTimers = () => {
    if (openTimer.current) clearTimeout(openTimer.current);
    if (closeTimer.current) clearTimeout(closeTimer.current);
    openTimer.current = closeTimer.current = null;
  };

  const close = useCallback(() => {
    clearTimers();
    setAnchor(null);
  }, []);

  const anchorOf = (row: HrTimesheetRow, index: number, el: HTMLElement, pinned: boolean, keyboard = false): DayAnchor => ({
    rowId: row.employeeId,
    employee: row.employee,
    date: p.dates[index],
    code: row.codes[index],
    rect: el.getBoundingClientRect(),
    pinned,
    chip: el,
    keyboard,
  });

  const handlers = useMemo(
    () => ({
      onChipEnter: (row: HrTimesheetRow, index: number, el: HTMLButtonElement) => {
        // Только мышь: на телефоне карточку открывает нажатие.
        if (!window.matchMedia("(hover: hover)").matches || anchorRef.current?.pinned) return;
        clearTimers();
        openTimer.current = setTimeout(() => setAnchor(anchorOf(row, index, el, false)), anchorRef.current ? 0 : OPEN_DELAY);
      },
      onChipLeave: () => {
        if (openTimer.current) clearTimeout(openTimer.current);
        if (anchorRef.current?.pinned) return;
        closeTimer.current = setTimeout(() => setAnchor(null), CLOSE_DELAY);
      },
      onChipClick: (row: HrTimesheetRow, index: number, el: HTMLButtonElement, keyboard: boolean) => {
        clearTimers();
        const current = anchorRef.current;
        if (current?.pinned && current.rowId === row.employeeId && current.date === p.dates[index]) {
          setAnchor(null);
          return;
        }
        setAnchor(anchorOf(row, index, el, true, keyboard));
      },
    }),
    [p.dates],
  );

  // Закреплённая карточка закрывается щелчком мимо; любая — прокруткой страницы или таблицы.
  useEffect(() => {
    if (!anchor) return;
    const onDown = (e: PointerEvent) => {
      const target = e.target as HTMLElement;
      if (anchor.pinned && !target.closest("[role=dialog]") && !target.closest("[aria-expanded=true]")) close();
    };
    const onScroll = () => close();
    const main = document.querySelector("[data-app-scroll]");
    document.addEventListener("pointerdown", onDown);
    main?.addEventListener("scroll", onScroll, { passive: true });
    window.addEventListener("resize", onScroll);
    return () => {
      document.removeEventListener("pointerdown", onDown);
      main?.removeEventListener("scroll", onScroll);
      window.removeEventListener("resize", onScroll);
    };
  }, [anchor, close]);

  useEffect(() => close, [close]);

  const selectedRow = anchor?.rowId ?? null;
  const selectedIndex = anchor ? p.dates.indexOf(anchor.date) : -1;
  const sameMonth = p.from.slice(0, 7) === p.to.slice(0, 7);
  const month = formatDate(new Date(`${p.from}T12:00:00Z`), locale, { month: "long" });
  const raw = sameMonth ? t("Табель за {month}", { month }) : t("Табель за {from} — {to}", { from: dayLabels[0] ?? p.from, to: dayLabels[n - 1] ?? p.to });
  // В узбекском месяц стоит первым («oktabr uchun tabel») — с заглавной.
  const title = raw.charAt(0).toLocaleUpperCase(locale) + raw.slice(1);
  const count = `${p.employees.toLocaleString("ru-RU")} ${t(plural(p.employees, ["сотрудник", "сотрудника", "сотрудников"]))}`;

  // По умолчанию (по ФИО от А до Я) — нейтральная «↑↓»; явная сортировка — стрелка направления.
  const sortIcon = (key: TimesheetSort) =>
    p.sort !== key || (key === "name" && p.dir === "asc") ? (
      <ArrowUpDown aria-hidden className="size-3" />
    ) : p.dir === "asc" ? (
      <ArrowUp aria-hidden className="size-3" />
    ) : (
      <ArrowDown aria-hidden className="size-3" />
    );
  const ariaSort = (key: TimesheetSort) => (p.sort === key ? (p.dir === "asc" ? "ascending" : "descending") : "none");
  const stickyHead = "sticky z-10 bg-(--ts-surface)";

  return (
    <section aria-label={title} className="overflow-clip rounded-[12px] border border-(--ts-border) bg-(--ts-surface) [--ts-name-col:180px] md:[--ts-name-col:240px]">
      <div className="flex min-h-14 flex-wrap items-center justify-between gap-x-6 gap-y-2 border-b border-(--ts-border) px-5 py-3">
        <h2 className="flex items-baseline gap-2">
          <span className="text-[15px] font-semibold text-(--ts-text)">{title}</span>
          <span className="text-[13px] text-(--ts-subtle) tabular-nums">{count}</span>
        </h2>
        <TimesheetLegend documents={p.documents} />
      </div>

      <div role="table" aria-label={title} aria-rowcount={p.filtered + 1}>
        {/* Заголовки: закреплены сверху при прокрутке страницы, по горизонтали следуют за телом таблицы. */}
        <div className="sticky top-0 z-20 border-b border-(--ts-border) bg-(--ts-surface)">
          <div ref={head} className="overflow-hidden">
            <div role="row" className="grid h-12 items-center" style={{ gridTemplateColumns: template, minWidth }}>
              <div role="columnheader" aria-sort={ariaSort("name")} className={`${stickyHead} left-0 flex h-full items-center px-5 ${edges.left ? "shadow-[6px_0_10px_-6px_var(--ts-sticky-shadow)]" : ""}`}>
                <button type="button" onClick={() => p.onSort("name")} className={`flex cursor-pointer items-center gap-1.5 rounded text-[11px] font-semibold uppercase tracking-[0.6px] text-(--ts-subtle) hover:text-(--ts-text-2) ${focus}`}>
                  {t("Сотрудник")} {sortIcon("name")}
                </button>
              </div>
              {p.dates.map((d, i) => {
                const today = i === todayIndex;
                const column = selectedIndex === i ? "selected" : columns[i];
                return (
                  <div key={d} role="columnheader" className={`flex h-full flex-col items-center justify-center gap-0.5 ${columnBg[column]}`}>
                    {today ? (
                      <span className="grid h-5 w-6 place-items-center rounded-md bg-(--ts-accent) text-[11.5px] font-bold text-(--ts-on-accent) tabular-nums">{d.slice(8, 10)}</span>
                    ) : (
                      <span className={`text-[11.5px] font-semibold tabular-nums ${p.weekend[i] ? "text-(--ts-subtle)" : "text-(--ts-text-2)"}`}>{d.slice(8, 10)}</span>
                    )}
                    <span className={`text-[10px] leading-3 ${today ? "font-semibold text-(--ts-accent)" : "text-(--ts-subtle)"}`}>{t(weekday(d))}</span>
                  </div>
                );
              })}
              {TOTALS.map((c, i) => (
                <div
                  key={c.key}
                  role="columnheader"
                  aria-sort={ariaSort(c.key)}
                  style={{ right: c.right }}
                  className={`${stickyHead} flex h-full items-center justify-end ${c.key === "worked" ? "pr-5" : "pr-3"} ${i === 0 ? `border-l border-(--ts-border) ${edges.right ? "shadow-[-6px_0_10px_-6px_var(--ts-sticky-shadow)]" : ""}` : ""}`}
                >
                  <button
                    type="button"
                    onClick={() => p.onSort(c.key)}
                    className={`flex cursor-pointer items-center gap-1 whitespace-nowrap rounded text-[11px] font-semibold uppercase tracking-[0.4px] ${p.sort === c.key ? "text-(--ts-text-2)" : "text-(--ts-subtle)"} hover:text-(--ts-text-2) ${focus}`}
                  >
                    {t(c.label)}
                    {p.sort === c.key && sortIcon(c.key)}
                  </button>
                </div>
              ))}
            </div>
          </div>
        </div>

        <div ref={body} onScroll={measure} className={`overflow-x-auto ${styles.scroll}`}>
          <div role="rowgroup" style={{ minWidth }}>
            {p.loading ? (
              Array.from({ length: 8 }, (_, r) => (
                <div key={r} className={`grid items-center border-b border-(--ts-border) ${p.density === "compact" ? "h-10" : "h-[52px]"}`} style={{ gridTemplateColumns: template }} aria-hidden>
                  <div className="flex items-center gap-3 px-5">
                    <span className={`size-[30px] rounded-full ${styles.skeleton}`} />
                    <span className="flex flex-col gap-1.5">
                      <span className={`h-3 w-32 rounded ${styles.skeleton}`} />
                      <span className={`h-2.5 w-20 rounded ${styles.skeleton}`} />
                    </span>
                  </div>
                  {p.dates.map((d, i) => (
                    <div key={d} className={`grid h-full place-items-center ${columnBg[columns[i]]}`}>
                      <span className={`h-[22px] w-7 rounded-[5px] ${styles.skeleton}`} />
                    </div>
                  ))}
                  {TOTALS.map((c) => (
                    <div key={c.key} className="flex justify-end pr-3">
                      <span className={`h-3.5 rounded ${styles.skeleton}`} style={{ width: c.key === "worked" ? 96 : 20 }} />
                    </div>
                  ))}
                </div>
              ))
            ) : p.rows.length === 0 ? null : (
              p.rows.map((row, i) => (
                <TimesheetRow
                  key={row.employeeId}
                  row={row}
                  template={template}
                  dates={p.dates}
                  columns={columns}
                  selectedIndex={selectedRow === row.employeeId ? selectedIndex : null}
                  dayLabels={dayLabels}
                  density={p.density}
                  shadowLeft={edges.left}
                  shadowRight={edges.right}
                  cardHref={p.demo ? null : `/hr/employees/${row.employeeId}?from=${p.from}&to=${p.to}`}
                  last={i === p.rows.length - 1}
                  handlers={handlers}
                />
              ))
            )}
          </div>
        </div>

        {!p.loading && p.rows.length === 0 && (
          <div className="flex flex-col items-center gap-3 px-6 py-14 text-center">
            <span className="grid size-12 place-items-center rounded-full bg-(--ts-surface-2) text-(--ts-subtle)">
              <UsersRound className="size-6" />
            </span>
            <p className="text-sm text-(--ts-muted)">{t("Нет сотрудников по выбранным фильтрам")}</p>
            <button
              type="button"
              onClick={p.onReset}
              className={`h-[34px] cursor-pointer rounded-[9px] border border-(--ts-border-strong) bg-(--ts-surface) px-3.5 text-[13px] font-medium text-(--ts-text-2) transition-colors duration-150 hover:bg-(--ts-surface-2) ${focus}`}
            >
              {t("Сбросить фильтры")}
            </button>
          </div>
        )}
      </div>

      {!p.loading && p.filtered > 0 && <TablePagination page={p.page} size={p.size} total={p.filtered} onPage={p.onPage} onSize={p.onSize} />}

      {anchor && (
        <DayPopover
          anchor={anchor}
          from={p.from}
          to={p.to}
          onClose={close}
          onPointerEnter={() => closeTimer.current && clearTimeout(closeTimer.current)}
          onPointerLeave={() => {
            if (!anchorRef.current?.pinned) closeTimer.current = setTimeout(() => setAnchor(null), CLOSE_DELAY);
          }}
        />
      )}
    </section>
  );
}

/** Коды документов в периоде (ОТ, Б, К, О, НО) — для легенды. */
export function documentCodes(rows: HrTimesheetRow[]): string[] {
  const set = new Set<string>();
  for (const r of rows) for (const c of r.codes) if (dayKind(c) === "document") set.add(c);
  return [...set].sort();
}
