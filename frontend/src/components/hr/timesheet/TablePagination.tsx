"use client";

import { ChevronDown, ChevronLeft, ChevronRight } from "lucide-react";
import { useT } from "@/i18n/client";

const focus = "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-(--ts-accent)";
export const PAGE_SIZES = [25, 50, 100] as const;

/** Номера страниц: все до 7; иначе первая, соседние с текущей, последняя — через «…» (1 2 3 … 12). */
export function pageList(page: number, pages: number): (number | "gap")[] {
  if (pages <= 7) return Array.from({ length: pages }, (_, i) => i + 1);
  const keep = new Set([1, pages, page - 1, page, page + 1]);
  if (page <= 3) [2, 3, 4].forEach((n) => keep.add(n));
  if (page >= pages - 2) [pages - 3, pages - 2, pages - 1].forEach((n) => keep.add(n));
  const list = [...keep].filter((n) => n >= 1 && n <= pages).sort((a, b) => a - b);
  return list.flatMap((n, i) => (i > 0 && n - list[i - 1] > 1 ? (["gap", n] as const) : [n]));
}

/** Футер таблицы: «Показано 1–25 из 276», размер страницы и номера страниц. */
export function TablePagination({ page, size, total, onPage, onSize }: { page: number; size: number; total: number; onPage: (p: number) => void; onSize: (s: number) => void }) {
  const t = useT();
  const pages = Math.max(1, Math.ceil(total / size));
  const first = total === 0 ? 0 : (page - 1) * size + 1;
  const last = Math.min(total, page * size);
  const button = `grid size-8 cursor-pointer place-items-center rounded-[8px] text-[13px] transition-colors duration-150 disabled:cursor-default disabled:opacity-40 ${focus}`;
  return (
    <div className="flex min-h-14 flex-wrap items-center justify-between gap-3 border-t border-(--ts-border) px-5 py-3">
      <span className="text-[13px] text-(--ts-muted) tabular-nums">{t("Показано {a}–{b} из {n}", { a: first, b: last, n: total })}</span>
      <div className="flex flex-wrap items-center gap-3">
        <label className="relative">
          <span className="sr-only">{t("Строк на странице")}</span>
          <select
            value={size}
            onChange={(e) => onSize(Number(e.target.value))}
            className={`h-8 cursor-pointer appearance-none rounded-[8px] border border-(--ts-border-strong) bg-(--ts-surface) pl-3 pr-8 text-[13px] text-(--ts-text-2) ${focus}`}
          >
            {PAGE_SIZES.map((n) => (
              <option key={n} value={n}>
                {t("{n} на странице", { n })}
              </option>
            ))}
          </select>
          <ChevronDown aria-hidden className="pointer-events-none absolute right-2.5 top-1/2 size-3.5 -translate-y-1/2 text-(--ts-subtle)" />
        </label>
        <nav aria-label={t("Страницы")} className="flex items-center gap-1">
          <button type="button" aria-label={t("Предыдущая страница")} disabled={page <= 1} onClick={() => onPage(page - 1)} className={`${button} text-(--ts-muted) hover:bg-(--ts-surface-2)`}>
            <ChevronLeft className="size-4" />
          </button>
          {pageList(page, pages).map((p, i) =>
            p === "gap" ? (
              <span key={`g${i}`} className="grid size-8 place-items-center text-[13px] text-(--ts-subtle)">
                …
              </span>
            ) : (
              <button
                key={p}
                type="button"
                aria-current={p === page ? "page" : undefined}
                onClick={() => onPage(p)}
                className={`${button} tabular-nums ${p === page ? "bg-(--ts-text) font-semibold text-(--ts-surface)" : "text-(--ts-text-2) hover:bg-(--ts-surface-2)"}`}
              >
                {p}
              </button>
            ),
          )}
          <button type="button" aria-label={t("Следующая страница")} disabled={page >= pages} onClick={() => onPage(page + 1)} className={`${button} text-(--ts-muted) hover:bg-(--ts-surface-2)`}>
            <ChevronRight className="size-4" />
          </button>
        </nav>
      </div>
    </div>
  );
}
