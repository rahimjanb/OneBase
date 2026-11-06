"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";
import { ChevronLeft, ChevronRight, Search } from "lucide-react";
import { useT } from "@/i18n/client";
import { activeWarehouses, batchQualityLabel, batchQualityTone, dateRu, qualityCheckStatusLabel, qualityControlLabel, quantity, stampRu, type Page, type Warehouse } from "@/lib/procurement";
import { waitingTone, type QualityCheckRow, type QualityNav } from "@/lib/quality";

const field =
  "h-10 rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
const secondary = "inline-flex h-9 items-center gap-1 rounded-lg border border-line bg-surface px-3 text-sm hover:bg-muted";

/** Шаг проверки или решение — плашкой. */
export function CheckStatus({ row }: { row: Pick<QualityCheckRow, "status" | "decision"> }) {
  const t = useT();
  if (row.decision) return <span className={`rounded-md px-1.5 py-0.5 text-[11px] font-semibold ${batchQualityTone[row.decision]}`}>{t(batchQualityLabel[row.decision])}</span>;
  const tone = row.status === "Decision" ? "bg-accent-soft text-accent-strong" : row.status === "Cancelled" ? "bg-muted text-ink-3" : "bg-warn-soft text-warn";
  return <span className={`rounded-md px-1.5 py-0.5 text-[11px] font-semibold ${tone}`}>{t(qualityCheckStatusLabel[row.status])}</span>;
}

/**
 * Очередь контроля качества: открытые проверки по шагам (документы, лаборатория, ждут решения — со счётчиками), решения и забракованные;
 * склад (когда складов больше одного) и поиск по номеру, партии, позиции и поставщику. Строка — карточка проверки.
 */
export function QualityQueue({ data, nav, warehouses }: { data: Page<QualityCheckRow>; nav: QualityNav; warehouses: Warehouse[] }) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [search, setSearch] = useState(params.get("q") ?? "");
  const status = params.get("status") ?? "open";
  const stores = activeWarehouses(warehouses);
  const tabs = [
    { value: "open", label: "Открытые", count: nav.open },
    { value: "documents", label: "Документы", count: nav.documents },
    { value: "lab", label: "Лаборатория", count: nav.lab },
    { value: "decision", label: "Ждут решения", count: nav.decision },
    { value: "decided", label: "Решения", count: null },
    { value: "rejected", label: "Забракованные", count: null },
    { value: "all", label: "Все", count: null },
  ];
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
  const open = ["open", "documents", "lab", "decision"].includes(status);

  return (
    <>
      {!nav.decides && nav.decision > 0 && (
        <p className="mb-4 rounded-xl bg-muted px-3 py-2 text-sm text-ink-2">
          {t("Решение по партиям принимают сотрудники, назначенные в «Настройки → Отделы → Снабжение → Контроль качества».")}
        </p>
      )}
      <div className="mb-4 flex flex-wrap items-center gap-2">
        <label className="relative block min-w-[220px] flex-1 sm:max-w-[320px]">
          <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" aria-hidden />
          <span className="sr-only">{t("Поиск")}</span>
          <input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("Проверка, партия, позиция, поставщик")} className={`${field} w-full pl-9`} />
        </label>
        {stores.length > 1 && (
          <select value={params.get("warehouse") ?? ""} onChange={(e) => apply({ warehouse: e.target.value || null })} aria-label={t("Склад")} className={field}>
            <option value="">{t("Все склады")}</option>
            {stores.map((w) => (
              <option key={w.id} value={w.id}>
                {w.name}
              </option>
            ))}
          </select>
        )}
        <div className="flex flex-wrap gap-1 rounded-xl bg-muted p-1">
          {tabs.map((s) => (
            <button
              key={s.value}
              type="button"
              onClick={() => apply({ status: s.value === "open" ? null : s.value })}
              className={`inline-flex h-8 items-center gap-1.5 rounded-lg px-3 text-sm ${status === s.value ? "bg-surface font-medium text-ink shadow-sm" : "text-ink-2 hover:text-ink"}`}
            >
              {t(s.label)}
              {s.count !== null && s.count > 0 && <span className="rounded-full bg-accent-soft px-1.5 text-[11px] font-semibold tabular-nums text-accent-strong">{s.count}</span>}
            </button>
          ))}
        </div>
      </div>

      {data.items.length === 0 ? (
        <p className="rounded-xl border border-dashed border-line bg-surface px-4 py-10 text-center text-sm text-ink-3">
          {open
            ? t("Партий на контроле нет. Проверка появляется при приходе позиции, у которой в карточке включён контроль качества.")
            : t("По этому фильтру проверок нет.")}
        </p>
      ) : (
        <>
          <div className="hidden overflow-x-auto rounded-xl border border-line bg-surface lg:block">
            <table className="w-full min-w-[960px] text-sm">
              <thead>
                <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                  <th className="px-3 py-2.5 font-semibold">{t("Проверка")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Позиция и партия")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Поставщик")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Склад")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t("Пришло")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Шаг")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t(open ? "Ждёт" : "Решение")}</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((c) => (
                  <tr key={c.id} onClick={() => router.push(`/production/quality/${c.id}`)} className="cursor-pointer border-b border-line last:border-0 hover:bg-muted/50">
                    <td className="px-3 py-2.5">
                      <Link href={`/production/quality/${c.id}`} onClick={(e) => e.stopPropagation()} className="font-medium tabular-nums text-ink hover:text-accent-strong">
                        {c.number}
                      </Link>
                      <span className="block text-xs text-ink-3">{t(qualityControlLabel[c.control])}</span>
                    </td>
                    <td className="px-3 py-2.5">
                      <span className="block text-ink">{c.itemName}</span>
                      <span className="text-xs tabular-nums text-ink-3">
                        {[c.batchNumber, c.supplierBatchNo, c.expiresOn ? t("годна до {date}", { date: dateRu(c.expiresOn) }) : null].filter(Boolean).join(" · ")}
                      </span>
                    </td>
                    <td className="px-3 py-2.5 text-ink-2">{c.supplierName ?? "—"}</td>
                    <td className="px-3 py-2.5 text-ink-2">{c.warehouseName}</td>
                    <td className="px-3 py-2.5 text-right tabular-nums text-ink">
                      {quantity(c.received)} <span className="text-ink-3">{c.unitCode}</span>
                    </td>
                    <td className="px-3 py-2.5">
                      <CheckStatus row={c} />
                    </td>
                    <td className="px-3 py-2.5 text-right text-xs">
                      {open ? (
                        <span className={`tabular-nums ${waitingTone(c.daysWaiting)}`}>{c.daysWaiting === 0 ? t("сегодня") : t("{n} дн.", { n: c.daysWaiting })}</span>
                      ) : (
                        <span className="text-ink-3">{[c.decidedByName, c.decidedAt ? stampRu(c.decidedAt) : null].filter(Boolean).join(" · ") || "—"}</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <ul className="space-y-2 lg:hidden">
            {data.items.map((c) => (
              <li key={c.id}>
                <Link href={`/production/quality/${c.id}`} className="block rounded-xl border border-line bg-surface p-3.5">
                  <span className="flex items-start justify-between gap-3">
                    <span className="min-w-0">
                      <span className="block text-xs tabular-nums text-ink-3">{[c.number, c.batchNumber].join(" · ")}</span>
                      <span className="block font-medium text-ink">{c.itemName}</span>
                    </span>
                    <CheckStatus row={c} />
                  </span>
                  <span className="mt-1 block text-xs text-ink-3">
                    {[`${quantity(c.received)} ${c.unitCode}`, c.supplierName, c.warehouseName].filter(Boolean).join(" · ")}
                  </span>
                  {open && (
                    <span className={`mt-0.5 block text-xs ${waitingTone(c.daysWaiting)}`}>
                      {c.daysWaiting === 0 ? t("на контроле с сегодня") : t("на контроле {n} дн.", { n: c.daysWaiting })}
                    </span>
                  )}
                </Link>
              </li>
            ))}
          </ul>
          <div className="mt-3 flex flex-wrap items-center justify-between gap-3 text-sm text-ink-2">
            <span>{t("Проверок: {n}", { n: data.total })}</span>
            {pages > 1 && (
              <div className="flex items-center gap-2">
                {data.page > 1 && (
                  <button type="button" onClick={() => apply({ page: String(data.page - 1) })} className={secondary}>
                    <ChevronLeft className="size-4" /> {t("Назад")}
                  </button>
                )}
                <span>{t("{page} из {pages}", { page: data.page, pages })}</span>
                {data.page < pages && (
                  <button type="button" onClick={() => apply({ page: String(data.page + 1) })} className={secondary}>
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
