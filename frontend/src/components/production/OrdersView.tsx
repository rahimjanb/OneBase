"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";
import { AlertTriangle, ChevronLeft, ChevronRight, Factory, Plus, Search } from "lucide-react";
import { useT } from "@/i18n/client";
import { dateRu, quantity } from "@/lib/procurement";
import type { ProductionOrderList } from "@/lib/production";
import { todayInTashkent } from "@/lib/tasks";
import { OrderStatusPill, field } from "./ProdKit";

const statuses = [
  { value: "open", label: "Открытые" },
  { value: "Draft", label: "Черновики" },
  { value: "Released", label: "Выпущены" },
  { value: "InProgress", label: "В работе" },
  { value: "Completed", label: "Закрытые" },
  { value: "Cancelled", label: "Отменённые" },
  { value: "all", label: "Все" },
] as const;

/**
 * Журнал заданий на выпуск: открытые (черновики, выпущены, в работе) по умолчанию, поиск по номеру и рецептуре, перерасход по закрытым;
 * таблица на компьютере и карточки на телефоне. Кладовщик выдаёт сырьё по выпущенным заданиям своего склада.
 */
export function OrdersView({ data }: { data: ProductionOrderList }) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const today = todayInTashkent();
  const [search, setSearch] = useState(params.get("q") ?? "");
  const status = params.get("status") ?? "open";
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
  const tiles = [
    { label: "Черновики", value: data.drafts, status: "Draft" },
    { label: "Выпущены — ждут выдачи", value: data.released, status: "Released" },
    { label: "В работе", value: data.inProgress, status: "InProgress" },
  ];

  return (
    <div className="space-y-4">
      <div className="grid gap-2 sm:grid-cols-3">
        {tiles.map((x) => (
          <button
            key={x.status}
            type="button"
            onClick={() => apply({ status: x.status })}
            className={`rounded-2xl border px-4 py-3 text-left ${status === x.status ? "border-accent bg-accent-soft" : "border-line bg-surface hover:bg-muted"}`}
          >
            <span className="block text-2xl font-semibold tabular-nums text-ink">{x.value}</span>
            <span className="text-xs text-ink-3">{t(x.label)}</span>
          </button>
        ))}
      </div>
      <div className="flex flex-wrap items-center gap-2">
        <label className="relative min-w-0 flex-1 basis-64">
          <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" aria-hidden />
          <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("Номер задания или рецептура")} aria-label={t("Поиск задания")} className={`${field} pl-9`} />
        </label>
        <select value={status} onChange={(e) => apply({ status: e.target.value })} aria-label={t("Статус")} className={`${field} w-48`}>
          {statuses.map((s) => (
            <option key={s.value} value={s.value}>
              {t(s.label)}
            </option>
          ))}
        </select>
      </div>

      {data.items.length === 0 ? (
        <section className="flex flex-col items-center rounded-2xl border border-line bg-surface px-6 py-12 text-center">
          <span className="grid size-12 place-items-center rounded-2xl bg-accent-soft text-accent-strong">
            <Factory className="size-6" aria-hidden />
          </span>
          <p className="mt-3 text-sm text-ink-2">{t("Заданий по фильтру нет.")}</p>
          {data.canCreate && (
            <Link href="/production/orders/new" className="mt-4 inline-flex h-10 items-center gap-2 rounded-xl bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong">
              <Plus className="size-4" aria-hidden /> {t("Новое задание")}
            </Link>
          )}
        </section>
      ) : (
        <>
          <div className="hidden overflow-hidden rounded-2xl border border-line bg-surface lg:block">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                  <th className="px-4 py-2.5 font-semibold">{t("Задание")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Рецептура")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t("План")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t("Выход")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Дата")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Цех · склад")}</th>
                  <th className="px-4 py-2.5 font-semibold">{t("Статус")}</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((o) => {
                  const late = (o.status === "Released" || o.status === "InProgress") && o.plannedDate < today;
                  return (
                    <tr key={o.id} className="border-b border-line last:border-0 hover:bg-muted/40">
                      <td className="px-4 py-2.5">
                        <Link href={`/production/orders/${o.id}`} className="font-medium tabular-nums text-ink hover:text-accent-strong">
                          {o.number}
                        </Link>
                        {o.responsibleName && <span className="block text-xs text-ink-3">{o.responsibleName}</span>}
                      </td>
                      <td className="px-3 py-2.5 text-ink">
                        {o.recipeName}
                        <span className="block text-xs tabular-nums text-ink-3">
                          {o.recipeCode} v{o.recipeVersion}
                        </span>
                      </td>
                      <td className="whitespace-nowrap px-3 py-2.5 text-right tabular-nums">
                        {quantity(o.plannedQty)} {o.outputUnit}
                      </td>
                      <td className="whitespace-nowrap px-3 py-2.5 text-right tabular-nums">{o.outputQty === null ? "—" : `${quantity(o.outputQty)} ${o.outputUnit}`}</td>
                      <td className={`whitespace-nowrap px-3 py-2.5 tabular-nums ${late ? "font-semibold text-bad" : ""}`}>{dateRu(o.plannedDate)}</td>
                      <td className="px-3 py-2.5 text-ink-2">
                        {o.departmentName}
                        <span className="block text-xs text-ink-3">{o.warehouseName}</span>
                      </td>
                      <td className="px-4 py-2.5">
                        <span className="inline-flex flex-wrap items-center gap-1.5">
                          <OrderStatusPill status={o.status} />
                          {o.overconsumed && (
                            <span className="inline-flex items-center gap-1 rounded-md bg-bad-soft px-1.5 py-0.5 text-[11px] font-semibold text-bad">
                              <AlertTriangle className="size-3" aria-hidden /> {t("перерасход")}
                            </span>
                          )}
                        </span>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <ul className="space-y-2 lg:hidden">
            {data.items.map((o) => (
              <li key={o.id}>
                <Link href={`/production/orders/${o.id}`} className="block rounded-2xl border border-line bg-surface p-3.5">
                  <span className="flex items-start justify-between gap-2">
                    <span className="min-w-0">
                      <span className="block font-medium tabular-nums text-ink">{o.number}</span>
                      <span className="block truncate text-sm text-ink-2">{o.recipeName}</span>
                    </span>
                    <OrderStatusPill status={o.status} />
                  </span>
                  <span className="mt-2 block text-xs text-ink-3">
                    {quantity(o.plannedQty)} {o.outputUnit} · {dateRu(o.plannedDate)} · {o.departmentName}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
          {pages > 1 && (
            <div className="flex items-center justify-end gap-2 text-sm text-ink-2">
              <button type="button" disabled={data.page <= 1} onClick={() => apply({ page: String(data.page - 1) })} aria-label={t("Назад")} className="grid size-9 place-items-center rounded-lg border border-line disabled:opacity-40">
                <ChevronLeft className="size-4" aria-hidden />
              </button>
              <span className="tabular-nums">
                {data.page} / {pages}
              </span>
              <button type="button" disabled={data.page >= pages} onClick={() => apply({ page: String(data.page + 1) })} aria-label={t("Вперёд")} className="grid size-9 place-items-center rounded-lg border border-line disabled:opacity-40">
                <ChevronRight className="size-4" aria-hidden />
              </button>
            </div>
          )}
        </>
      )}
    </div>
  );
}
