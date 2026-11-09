"use client";

import Link from "next/link";
import { usePathname, useSearchParams } from "next/navigation";
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, Paperclip } from "lucide-react";
import { useT } from "@/i18n/client";
import { dateRu, money, type RequestPage, type RequestRow } from "@/lib/procurement";
import { RequestKindPill, RequestPriorityPill, RequestStatusPill } from "./RequestKit";

const columns: { key: string; label: string; sort?: string; className?: string }[] = [
  { key: "request", label: "Заявка", sort: "number" },
  { key: "items", label: "Позиции" },
  { key: "department", label: "Отдел", sort: "department" },
  { key: "initiator", label: "Инициатор", sort: "initiator" },
  { key: "priority", label: "Приоритет", sort: "priority" },
  { key: "status", label: "Статус", sort: "status" },
  { key: "total", label: "Сумма, сум", sort: "total", className: "text-right" },
  { key: "desired", label: "Нужно к", sort: "desired" },
  { key: "created", label: "Создана", sort: "created" },
];

/** Позиции строкой: «Бумага А4 и ещё 2» и категории. */
function Items({ row }: { row: RequestRow }) {
  const t = useT();
  if (row.itemsCount === 0) return <span className="text-ink-3">—</span>;
  return (
    <>
      <span className="block max-w-64 truncate text-ink-2">
        {row.firstItem}
        {row.itemsCount > 1 && <span className="text-ink-3"> {t("и ещё {n}", { n: row.itemsCount - 1 })}</span>}
      </span>
      {row.categories.length > 0 && <span className="block max-w-64 truncate text-xs text-ink-3">{row.categories.join(", ")}</span>}
    </>
  );
}

/**
 * Таблица заявок: на компьютере — компактные колонки с сортировкой по клику на заголовок, на телефоне — карточки. Строка открывает
 * карточку заявки. Сумма — предполагаемая (по позициям с ценой).
 */
export function RequestTable({ data, empty }: { data: RequestPage; empty: string }) {
  const t = useT();
  const pathname = usePathname();
  const params = useSearchParams();
  const sort = params.get("sort");
  const desc = params.get("desc") === "true";
  const href = (patch: Record<string, string | null>) => {
    const next = new URLSearchParams(params.toString());
    for (const [k, v] of Object.entries(patch)) {
      if (v) next.set(k, v);
      else next.delete(k);
    }
    return `${pathname}${next.size ? `?${next}` : ""}`;
  };
  const pages = Math.max(1, Math.ceil(data.total / data.pageSize));

  if (data.items.length === 0) {
    return <p className="rounded-xl border border-dashed border-line bg-surface px-4 py-10 text-center text-sm text-ink-3">{t(empty)}</p>;
  }

  return (
    <>
      <div className="hidden overflow-x-auto rounded-xl border border-line bg-surface lg:block">
        <table className="w-full min-w-[1080px] text-sm">
          <thead>
            <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
              {columns.map((c) => (
                <th key={c.key} className={`px-3 py-2.5 font-semibold ${c.className ?? ""}`}>
                  {c.sort ? (
                    <Link
                      href={href({ sort: c.sort, desc: sort === c.sort && !desc ? "true" : null, page: null })}
                      className="inline-flex items-center gap-1 hover:text-ink"
                      aria-sort={sort === c.sort ? (desc ? "descending" : "ascending") : undefined}
                    >
                      {t(c.label)}
                      {sort === c.sort && (desc ? <ArrowDown className="size-3" /> : <ArrowUp className="size-3" />)}
                    </Link>
                  ) : (
                    t(c.label)
                  )}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {data.items.map((row) => (
              <tr key={row.id} className="border-b border-line align-top last:border-0 hover:bg-muted/50">
                <td className="max-w-72 px-3 py-2.5">
                  <Link href={`/requests/${row.id}`} className="group block">
                    <span className="block text-xs font-medium tabular-nums text-ink-3">{row.number}</span>
                    <span className="block font-medium text-ink group-hover:text-accent-strong">{row.title}</span>
                  </Link>
                  <RequestKindPill kind={row.kind} className="mt-1" />
                  {row.attachments > 0 && (
                    <span className="mt-1 inline-flex items-center gap-1 text-[11px] text-ink-3">
                      <Paperclip className="size-3" aria-hidden /> {row.attachments}
                    </span>
                  )}
                </td>
                <td className="px-3 py-2.5">
                  <Items row={row} />
                </td>
                <td className="px-3 py-2.5 text-ink-2">{row.departmentName}</td>
                <td className="px-3 py-2.5 text-ink-2">{row.mine ? <span className="text-accent-strong">{t("вы")}</span> : row.initiatorName}</td>
                <td className="px-3 py-2.5">
                  <RequestPriorityPill priority={row.priority} />
                </td>
                <td className="px-3 py-2.5">
                  <RequestStatusPill status={row.status} />
                </td>
                <td className="px-3 py-2.5 text-right tabular-nums text-ink">{row.estimatedTotal > 0 ? money(row.estimatedTotal) : <span className="text-ink-3">—</span>}</td>
                <td className="px-3 py-2.5 tabular-nums text-ink-2">{dateRu(row.desiredDate)}</td>
                <td className="px-3 py-2.5 tabular-nums text-ink-2">{dateRu(row.createdAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <ul className="space-y-2 lg:hidden">
        {data.items.map((row) => (
          <li key={row.id}>
            <Link href={`/requests/${row.id}`} className="block rounded-xl border border-line bg-surface p-3.5 hover:border-accent/40">
              <div className="flex items-start justify-between gap-2">
                <span className="min-w-0">
                  <span className="block text-xs tabular-nums text-ink-3">{row.number}</span>
                  <span className="block font-medium text-ink">{row.title}</span>
                  <RequestKindPill kind={row.kind} className="mt-1" />
                </span>
                <RequestStatusPill status={row.status} />
              </div>
              <div className="mt-1.5 text-sm">
                <Items row={row} />
              </div>
              <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-ink-3">
                <RequestPriorityPill priority={row.priority} />
                <span>{row.departmentName}</span>
                {!row.mine && <span>{row.initiatorName}</span>}
                {row.desiredDate && <span>{t("нужно к {date}", { date: dateRu(row.desiredDate) })}</span>}
                {row.estimatedTotal > 0 && <span className="font-medium tabular-nums text-ink-2">≈ {money(row.estimatedTotal)} {t("сум")}</span>}
              </div>
            </Link>
          </li>
        ))}
      </ul>

      <div className="mt-3 flex flex-wrap items-center justify-between gap-3 text-sm text-ink-2">
        <span>{t("Заявок: {n}", { n: data.total })}</span>
        {pages > 1 && (
          <div className="flex items-center gap-2">
            {data.page > 1 && (
              <Link href={href({ page: String(data.page - 1) })} className="inline-flex h-9 items-center gap-1 rounded-lg border border-line bg-surface px-3 hover:bg-muted">
                <ChevronLeft className="size-4" /> {t("Назад")}
              </Link>
            )}
            <span>{t("{page} из {pages}", { page: data.page, pages })}</span>
            {data.page < pages && (
              <Link href={href({ page: String(data.page + 1) })} className="inline-flex h-9 items-center gap-1 rounded-lg border border-line bg-surface px-3 hover:bg-muted">
                {t("Дальше")} <ChevronRight className="size-4" />
              </Link>
            )}
          </div>
        )}
      </div>
    </>
  );
}
