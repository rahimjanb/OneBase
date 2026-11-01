"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";
import { AlertTriangle, ChevronLeft, ChevronRight, Search } from "lucide-react";
import { useT } from "@/i18n/client";
import { dateRu, money, type OrderRow, type OrderSummary, type Page } from "@/lib/procurement";
import { OrderStatusPill } from "./ProcKit";

const field =
  "h-10 rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";

const groups = [
  { value: "", label: "Все" },
  { value: "open", label: "В работе" },
  { value: "awaiting", label: "Ждут директора" },
  { value: "overdue", label: "Просрочены" },
  { value: "expected", label: "Ожидаются" },
  { value: "partial", label: "Частично поставлены" },
  { value: "done", label: "Поставлены" },
  { value: "cancelled", label: "Отменены" },
] as const;

/**
 * Заказы поставщикам: плитки контроля (в работе, ждут директора, просрочены, ожидаются за 7 дней, частичные поставки, отменены за месяц),
 * фильтры по статусу, поставщику и ответственному (сотруднику), поиск по номеру, поставщику, позиции и заявке; таблица и карточки на телефоне.
 */
export function OrdersView({
  data,
  summary,
  suppliers,
  responsibles,
}: {
  data: Page<OrderRow>;
  summary: OrderSummary;
  suppliers: { id: string; name: string }[];
  responsibles: { id: string; name: string }[];
}) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [search, setSearch] = useState(params.get("q") ?? "");
  const status = params.get("status") ?? "";
  const apply = (patch: Record<string, string | null>) => {
    const next = new URLSearchParams(params.toString());
    for (const [k, v] of Object.entries(patch)) {
      if (v) next.set(k, v);
      else next.delete(k);
    }
    if (!("page" in patch)) next.delete("page");
    router.push(`${pathname}${next.size ? `?${next}` : ""}`, { scroll: false });
  };
  useEffect(() => {
    const timer = window.setTimeout(() => {
      if ((params.get("q") ?? "") !== search.trim()) apply({ q: search.trim() || null });
    }, 400);
    return () => window.clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search]);

  const tiles = [
    { label: "В работе", value: summary.open, status: "open", hint: `${money(summary.openTotal)} ${t("сум")}` },
    { label: "Ждут директора", value: summary.awaitingApproval, status: "awaiting", tone: summary.awaitingApproval > 0 ? "text-violet-light" : "" },
    { label: "Просрочены", value: summary.overdue, status: "overdue", tone: summary.overdue > 0 ? "text-bad" : "" },
    { label: "Ожидаются за 7 дней", value: summary.expectedWeek, status: "expected" },
    { label: "Частично поставлены", value: summary.partial, status: "partial", tone: summary.partial > 0 ? "text-warn" : "" },
    { label: "Отменены за месяц", value: summary.cancelledMonth, status: "cancelled" },
  ];
  const pages = Math.max(1, Math.ceil(data.total / data.pageSize));

  return (
    <>
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-6">
        {tiles.map((tile) => (
          <button
            key={tile.label}
            type="button"
            onClick={() => apply({ status: status === tile.status ? null : tile.status })}
            className={`rounded-xl border bg-surface p-4 text-left transition-colors hover:border-accent/40 ${status === tile.status ? "border-accent/50 ring-1 ring-accent/20" : "border-line"}`}
          >
            <div className={`text-2xl font-semibold tabular-nums text-ink ${tile.tone ?? ""}`}>{tile.value}</div>
            <div className="mt-0.5 text-xs text-ink-3">{t(tile.label)}</div>
            {tile.hint && <div className="mt-0.5 text-[11px] tabular-nums text-ink-3">{tile.hint}</div>}
          </button>
        ))}
      </div>

      <div className="my-4 flex flex-wrap items-center gap-2">
        <label className="relative block min-w-0 basis-full sm:basis-auto sm:flex-1 sm:max-w-[300px]">
          <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" aria-hidden />
          <span className="sr-only">{t("Поиск")}</span>
          <input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("Номер, поставщик, товар, заявка")} className={`${field} w-full pl-9`} />
        </label>
        <select value={status} onChange={(e) => apply({ status: e.target.value || null })} aria-label={t("Статус")} className={field}>
          {groups.map((g) => (
            <option key={g.value} value={g.value}>
              {t(g.label)}
            </option>
          ))}
        </select>
        <select value={params.get("supplier") ?? ""} onChange={(e) => apply({ supplier: e.target.value || null })} aria-label={t("Поставщик")} className={`${field} max-w-[220px]`}>
          <option value="">{t("Все поставщики")}</option>
          {suppliers.map((s) => (
            <option key={s.id} value={s.id}>
              {s.name}
            </option>
          ))}
        </select>
        <select value={params.get("responsible") ?? ""} onChange={(e) => apply({ responsible: e.target.value || null })} aria-label={t("Ответственный")} className={`${field} max-w-[220px]`}>
          <option value="">{t("Все ответственные")}</option>
          {responsibles.map((r) => (
            <option key={r.id} value={r.id}>
              {r.name}
            </option>
          ))}
        </select>
      </div>

      {data.items.length === 0 ? (
        <p className="rounded-xl border border-dashed border-line bg-surface px-4 py-10 text-center text-sm text-ink-3">
          {summary.open + summary.cancelledMonth === 0 && !status ? t("Заказов пока нет — создайте первый из утверждённой заявки.") : t("Заказов по этому фильтру нет.")}
        </p>
      ) : (
        <>
          <div className="hidden overflow-x-auto rounded-xl border border-line bg-surface lg:block">
            <table className="w-full min-w-[960px] text-sm">
              <thead>
                <tr className="whitespace-nowrap border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                  <th className="px-3 py-2.5 font-semibold">{t("Заказ")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Поставщик")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Позиции")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Ответственный")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Статус")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t("Итог, сум")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Ожидается")}</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((o) => (
                  <tr key={o.id} className="border-b border-line align-top last:border-0 hover:bg-muted/50">
                    <td className="px-3 py-2.5">
                      <Link href={`/procurement/orders/${o.id}`} className="font-medium tabular-nums text-ink hover:text-accent-strong">
                        {o.number}
                      </Link>
                      <span className="block text-xs text-ink-3">{o.requests.join(", ")}</span>
                    </td>
                    <td className="px-3 py-2.5 text-ink-2">{o.supplierName}</td>
                    <td className="max-w-64 px-3 py-2.5 text-ink-2">
                      <span className="block truncate">
                        {o.firstItem}
                        {o.lines > 1 && <span className="text-ink-3"> {t("и ещё {n}", { n: o.lines - 1 })}</span>}
                      </span>
                    </td>
                    <td className="px-3 py-2.5 text-ink-2">{o.responsibleName}</td>
                    <td className="px-3 py-2.5">
                      <OrderStatusPill status={o.status} />
                      {o.approvalRound > 1 && <span className="ml-1 text-[11px] text-violet-light">{t("повторно")}</span>}
                    </td>
                    <td className="whitespace-nowrap px-3 py-2.5 text-right tabular-nums text-ink">{money(o.total)}</td>
                    <td className={`whitespace-nowrap px-3 py-2.5 tabular-nums ${o.overdue ? "font-semibold text-bad" : "text-ink-2"}`}>
                      {o.overdue && <AlertTriangle className="mr-1 inline size-3.5" aria-label={t("Просрочен")} />}
                      {dateRu(o.expectedDate)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <ul className="space-y-2 lg:hidden">
            {data.items.map((o) => (
              <li key={o.id}>
                <Link href={`/procurement/orders/${o.id}`} className="block rounded-xl border border-line bg-surface p-3.5">
                  <span className="flex items-start justify-between gap-2">
                    <span className="min-w-0">
                      <span className="block text-xs tabular-nums text-ink-3">{o.number}</span>
                      <span className="block font-medium text-ink">{o.supplierName}</span>
                    </span>
                    <OrderStatusPill status={o.status} />
                  </span>
                  <span className="mt-1 block truncate text-sm text-ink-2">
                    {o.firstItem}
                    {o.lines > 1 && ` ${t("и ещё {n}", { n: o.lines - 1 })}`}
                  </span>
                  <span className="mt-1.5 flex flex-wrap gap-x-3 text-xs text-ink-3">
                    <span className="font-medium tabular-nums text-ink-2">{money(o.total)} {t("сум")}</span>
                    {o.expectedDate && <span className={o.overdue ? "font-semibold text-bad" : ""}>{t("ожидается {date}", { date: dateRu(o.expectedDate) })}</span>}
                    <span>{o.responsibleName}</span>
                  </span>
                </Link>
              </li>
            ))}
          </ul>
          <div className="mt-3 flex flex-wrap items-center justify-between gap-3 text-sm text-ink-2">
            <span>{t("Заказов: {n}", { n: data.total })}</span>
            {pages > 1 && (
              <div className="flex items-center gap-2">
                {data.page > 1 && (
                  <button type="button" onClick={() => apply({ page: String(data.page - 1) })} className="inline-flex h-9 items-center gap-1 rounded-lg border border-line bg-surface px-3 hover:bg-muted">
                    <ChevronLeft className="size-4" /> {t("Назад")}
                  </button>
                )}
                <span>{t("{page} из {pages}", { page: data.page, pages })}</span>
                {data.page < pages && (
                  <button type="button" onClick={() => apply({ page: String(data.page + 1) })} className="inline-flex h-9 items-center gap-1 rounded-lg border border-line bg-surface px-3 hover:bg-muted">
                    {t("Дальше")} <ChevronRight className="size-4" />
                  </button>
                )}
              </div>
            )}
          </div>
        </>
      )}
    </>
  );
}
