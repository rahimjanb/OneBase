"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";
import { ChevronLeft, ChevronRight, Search } from "lucide-react";
import { useT } from "@/i18n/client";
import { movementKindLabel, quantity, stampRu, type MovementKind, type MovementRow, type Page } from "@/lib/procurement";

const field =
  "h-10 rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
const kinds: MovementKind[] = ["Receipt", "Issue", "WriteOff", "Adjustment"];

/** Журнал движений склада: вид, даты, поиск по артикулу, названию и номеру документа; до/после по каждой строке. Журнал не правится. */
export function MovementsView({ data }: { data: Page<MovementRow> }) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [search, setSearch] = useState(params.get("q") ?? "");
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
  const pages = Math.max(1, Math.ceil(data.total / data.pageSize));

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center gap-2">
        <label className="relative block min-w-0 flex-1 sm:max-w-[320px]">
          <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" aria-hidden />
          <span className="sr-only">{t("Поиск")}</span>
          <input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("Артикул, название, номер документа")} className={`${field} w-full pl-9`} />
        </label>
        <select value={params.get("kind") ?? ""} onChange={(e) => apply({ kind: e.target.value || null })} aria-label={t("Вид движения")} className={field}>
          <option value="">{t("Все движения")}</option>
          {kinds.map((k) => (
            <option key={k} value={k}>
              {t(movementKindLabel[k])}
            </option>
          ))}
        </select>
        <input type="date" value={params.get("from") ?? ""} onChange={(e) => apply({ from: e.target.value || null })} aria-label={t("С")} className={field} />
        <input type="date" value={params.get("to") ?? ""} onChange={(e) => apply({ to: e.target.value || null })} aria-label={t("По")} className={field} />
      </div>
      {data.items.length === 0 ? (
        <p className="rounded-xl border border-dashed border-line bg-surface px-4 py-10 text-center text-sm text-ink-3">{t("Движений по этому фильтру нет.")}</p>
      ) : (
        <ul className="divide-y divide-line overflow-hidden rounded-xl border border-line bg-surface">
          {data.items.map((m) => (
            <li key={m.id} className="flex items-center gap-3 px-4 py-3 text-sm">
              <span className="min-w-0 flex-1">
                <span className="block text-ink">
                  <span className="font-medium">{m.itemName}</span> <span className="tabular-nums text-ink-3">{m.sku}</span>
                </span>
                <span className="block truncate text-xs text-ink-3">
                  {t(movementKindLabel[m.kind])} · <span className="tabular-nums">{m.documentNumber}</span> · {stampRu(m.createdAt)}
                  {m.actorName && ` · ${m.actorName}`}
                  {m.departmentName && ` · ${m.departmentName}`}
                  {m.receiver && ` · ${t("получил")}: ${m.receiver}`}
                  {m.reference && ` · ${m.reference}`}
                  {m.comment && ` · ${m.comment}`}
                </span>
                {m.requestId && (
                  <Link href={`/requests/${m.requestId}`} className="text-xs text-accent-strong hover:underline">
                    {t("заявка {n}", { n: m.requestNumber ?? "" })}
                  </Link>
                )}
                {m.orderId && (
                  <span className="flex flex-wrap gap-x-3 text-xs">
                    <Link href={`/procurement/receipts/${m.documentId}`} className="text-accent-strong hover:underline">
                      {t("приёмка {n}", { n: m.documentNumber })}
                    </Link>
                    <Link href={`/procurement/orders/${m.orderId}`} className="text-accent-strong hover:underline">
                      {t("заказ {n}", { n: m.orderNumber ?? "" })}
                    </Link>
                  </span>
                )}
              </span>
              <span className={`shrink-0 text-right tabular-nums ${m.quantity > 0 ? "text-ok" : m.quantity < 0 ? "text-bad" : "text-ink-3"}`}>
                {m.quantity > 0 ? "+" : ""}
                {quantity(m.quantity)} {m.unitCode}
                <span className="block text-[11px] text-ink-3">
                  {quantity(m.balanceBefore)} → {quantity(m.balanceAfter)}
                </span>
              </span>
            </li>
          ))}
        </ul>
      )}
      <div className="mt-3 flex flex-wrap items-center justify-between gap-3 text-sm text-ink-2">
        <span>{t("Движений: {n}", { n: data.total })}</span>
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
  );
}
