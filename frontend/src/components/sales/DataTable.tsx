"use client";

import { useRouter } from "next/navigation";
import { Fragment, useMemo, useState } from "react";
import { ArrowDown, ArrowUp, Copy } from "lucide-react";
import { tsvValue } from "@/lib/sales/format";
import { Note, Section } from "./bits";

export type Column<T> = {
  key: string;
  label: string;
  /** Верхняя строка двухуровневой шапки. */
  group?: string;
  align?: "left" | "right";
  /** Значение для сортировки и копирования (TSV). */
  value: (row: T) => string | number | null;
  /** Отображение; по умолчанию — value. */
  render?: (row: T) => React.ReactNode;
  className?: string;
};

type Sort = { key: string; dir: "asc" | "desc" } | null;

export function CopyButton({ onCopy }: { onCopy: () => string }) {
  const [copied, setCopied] = useState(false);
  return (
    <button
      type="button"
      onClick={async () => {
        try {
          await navigator.clipboard.writeText(onCopy());
          setCopied(true);
          setTimeout(() => setCopied(false), 1500);
        } catch {
          setCopied(false);
        }
      }}
      className="inline-flex h-8 items-center gap-1.5 rounded-lg border border-line bg-surface px-3 text-xs font-medium text-ink hover:bg-muted max-lg:hidden"
    >
      <Copy className="size-3.5" />
      {copied ? "Скопировано" : "Копировать"}
    </button>
  );
}

export function toTsv<T>(columns: Column<T>[], rows: T[]): string {
  const header = columns.map((c) => (c.group ? `${c.group}: ${c.label}` : c.label)).join("\t");
  const lines = rows.map((row) => columns.map((c) => tsvValue(c.value(row))).join("\t"));
  return [header, ...lines].join("\n");
}

export function sortRows<T>(rows: T[], columns: Column<T>[], sort: Sort): T[] {
  if (!sort) return rows;
  const column = columns.find((c) => c.key === sort.key);
  if (!column) return rows;
  const factor = sort.dir === "asc" ? 1 : -1;
  return [...rows].sort((a, b) => {
    const x = column.value(a);
    const y = column.value(b);
    if (x == null && y == null) return 0;
    if (x == null) return 1; // пустые — всегда в конце
    if (y == null) return -1;
    if (typeof x === "number" && typeof y === "number") return (x - y) * factor;
    return String(x).localeCompare(String(y), "ru") * factor;
  });
}

/** Шапка с сортировкой по клику и поддержкой групп колонок. */
export function TableHead<T>({ columns, sort, onSort }: { columns: Column<T>[]; sort: Sort; onSort: (key: string) => void }) {
  const hasGroups = columns.some((c) => c.group);
  const groups: { label: string; span: number }[] = [];
  for (const c of columns) {
    const label = c.group ?? "";
    const last = groups[groups.length - 1];
    if (last && last.label === label) last.span++;
    else groups.push({ label, span: 1 });
  }

  return (
    <thead>
      {hasGroups && (
        <tr>
          {groups.map((g, i) => (
            <th
              key={i}
              colSpan={g.span}
              className={`px-3 pb-1 pt-2 text-center text-[11px] font-semibold uppercase tracking-wide text-ink-3 first:pl-4 last:pr-4 sm:first:pl-5 sm:last:pr-5 ${g.label ? "border-b border-line" : ""}`}
            >
              {g.label}
            </th>
          ))}
        </tr>
      )}
      <tr className="border-b border-line">
        {columns.map((c) => {
          const active = sort?.key === c.key;
          return (
            <th key={c.key} className={`whitespace-nowrap px-3 py-2 font-semibold first:sticky first:left-0 first:z-[1] first:bg-surface first:pl-4 last:pr-4 sm:first:pl-5 sm:last:pr-5 ${c.align === "right" ? "text-right" : "text-left"}`}>
              <button
                type="button"
                onClick={() => onSort(c.key)}
                className={`inline-flex items-center gap-1 text-[11px] uppercase tracking-wide hover:text-ink ${active ? "text-ink" : "text-ink-3"}`}
              >
                {c.label}
                {active && (sort!.dir === "asc" ? <ArrowUp className="size-3" /> : <ArrowDown className="size-3" />)}
              </button>
            </th>
          );
        })}
      </tr>
    </thead>
  );
}

export function nextSort(current: Sort, key: string): Sort {
  if (current?.key !== key) return { key, dir: "desc" };
  return current.dir === "desc" ? { key, dir: "asc" } : null;
}

/**
 * Секция-таблица: заголовок, «Копировать» (TSV для Excel), сортировка по колонкам, горизонтальный скролл.
 * Колонки содержат функции, поэтому таблицы описываются в клиентских компонентах.
 */
export function DataTable<T>({
  title,
  hint,
  note,
  actions,
  columns,
  rows,
  rowKey,
  rowHref,
  expand,
  footer,
  empty = "Нет данных за период",
  limit,
}: {
  title: string;
  hint?: React.ReactNode;
  note?: React.ReactNode;
  actions?: React.ReactNode;
  columns: Column<T>[];
  rows: T[];
  rowKey: (row: T) => string;
  rowHref?: (row: T) => string | null;
  /** Раскрываемое содержимое строки (клик по строке). */
  expand?: (row: T) => React.ReactNode | null;
  footer?: React.ReactNode;
  empty?: string;
  /** Сколько строк показать до «Показать все». Сортировка и «Копировать» — по всем строкам. */
  limit?: number;
}) {
  const router = useRouter();
  const [sort, setSort] = useState<Sort>(null);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [showAll, setShowAll] = useState(false);
  const sorted = useMemo(() => sortRows(rows, columns, sort), [rows, columns, sort]);
  const cut = limit != null && !showAll && sorted.length > limit;
  const visible = cut ? sorted.slice(0, limit) : sorted;

  return (
    <Section
      title={title}
      hint={hint}
      actions={
        <>
          {actions}
          {rows.length > 0 && <CopyButton onCopy={() => toTsv(columns, sorted)} />}
        </>
      }
    >
      <div className="-mx-4 overflow-x-auto sm:-mx-5">
        <table className="w-full min-w-max border-collapse text-sm">
          <TableHead columns={columns} sort={sort} onSort={(key) => setSort((s) => nextSort(s, key))} />
          <tbody>
            {visible.map((row) => {
              const key = rowKey(row);
              const href = rowHref?.(row) ?? null;
              const details = expand && expanded === key ? expand(row) : null;
              const clickable = href != null || expand != null;
              return (
                <Fragment key={key}>
                  <tr
                    onClick={
                      href ? () => router.push(href) : expand ? () => setExpanded((e) => (e === key ? null : key)) : undefined
                    }
                    className={`group/row border-b border-line ${clickable ? "cursor-pointer hover:bg-muted active:bg-muted" : ""}`}
                  >
                    {columns.map((c) => (
                      <td
                        key={c.key}
                        // Первая колонка не уезжает при горизонтальной прокрутке: название видно всегда, особенно на телефоне.
                        className={`px-3 py-2.5 align-middle max-lg:py-3 first:sticky first:left-0 first:z-[1] first:bg-surface first:pl-4 max-lg:first:shadow-[inset_-1px_0_0_var(--color-line)] first:group-hover/row:bg-muted last:pr-4 sm:first:pl-5 sm:last:pr-5 ${c.align === "right" ? "text-right tabular-nums" : ""} ${c.className ?? ""}`}
                      >
                        {c.render ? c.render(row) : (c.value(row) ?? "—")}
                      </td>
                    ))}
                  </tr>
                  {details && (
                    <tr className="border-b border-line bg-muted/50">
                      <td colSpan={columns.length} className="px-4 py-3 sm:px-5">
                        {details}
                      </td>
                    </tr>
                  )}
                </Fragment>
              );
            })}
          </tbody>
          {footer}
        </table>
      </div>
      {rows.length === 0 && <p className="py-6 text-center text-sm text-ink-3">{empty}</p>}
      {limit != null && sorted.length > limit && (
        <button
          type="button"
          onClick={() => setShowAll((s) => !s)}
          className="mt-3 text-sm font-medium text-accent hover:underline max-lg:flex max-lg:h-11 max-lg:w-full max-lg:items-center max-lg:justify-center max-lg:rounded-lg max-lg:border max-lg:border-line max-lg:bg-surface"
        >
          {showAll ? "Свернуть" : `Показать все (${sorted.length})`}
        </button>
      )}
      {note && <Note>{note}</Note>}
    </Section>
  );
}

/** Ячейка «название + серый подзаголовок». */
export function NameCell({ name, sub }: { name: string; sub?: string | null }) {
  return (
    <span className="block min-w-[160px] max-lg:min-w-0 max-lg:max-w-[44vw]">
      <span className="block font-medium text-ink max-lg:truncate">{name}</span>
      {sub && <span className="block text-xs text-ink-3 max-lg:truncate">{sub}</span>}
    </span>
  );
}
