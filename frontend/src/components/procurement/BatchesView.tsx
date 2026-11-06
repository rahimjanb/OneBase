"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";
import { ChevronLeft, ChevronRight, Search } from "lucide-react";
import { useT } from "@/i18n/client";
import { activeWarehouses, batchQualityLabel, batchQualityTone, batchStateLabel, batchTone, dateRu, quantity, type BatchRow, type Page, type Warehouse } from "@/lib/procurement";

const field =
  "h-10 rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
const secondary = "inline-flex h-9 items-center gap-1 rounded-lg border border-line bg-surface px-3 text-sm hover:bg-muted";

const states = [
  { value: "", label: "С остатком" },
  { value: "usable", label: "Годные" },
  { value: "expiring", label: "Истекает срок" },
  { value: "expired", label: "Просрочено" },
  { value: "control", label: "На контроле" },
  { value: "blocked", label: "Забраковано и заблокировано" },
  { value: "empty", label: "Все, с закончившимися" },
] as const;

/** Сколько осталось до срока: «ещё 12 дн.», «сегодня последний день», «просрочена 3 дн.»; срока нет — «срок не указан». */
function Left({ days, tone }: { days: number | null; tone: string }) {
  const t = useT();
  return (
    <span className={`tabular-nums ${tone}`}>
      {days === null
        ? t("срок не указан")
        : days > 0
          ? t("ещё {n} дн.", { n: days })
          : days === 0
            ? t("сегодня последний день")
            : t("просрочена {n} дн.", { n: -days })}
    </span>
  );
}

/** Отметка качества у партии, которую не выдать по качеству: на контроле, забракована, заблокирована. */
function QualityMark({ b }: { b: BatchRow }) {
  const t = useT();
  if (b.quality === "Approved") return null;
  return <span className={`ml-1.5 rounded-md px-1.5 py-0.5 text-[11px] font-semibold ${batchQualityTone[b.quality]}`}>{t(batchQualityLabel[b.quality])}</span>;
}

/** Реестр партий: склад (когда складов больше одного), состояние, поиск по номеру, номеру производителя, артикулу и названию; строка — карточка партии. */
export function BatchesView({ data, warehouses }: { data: Page<BatchRow>; warehouses: Warehouse[] }) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [search, setSearch] = useState(params.get("q") ?? "");
  const state = params.get("state") ?? "";
  const stores = activeWarehouses(warehouses);
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
          <input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("Номер партии, артикул, название")} className={`${field} w-full pl-9`} />
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
          {states.map((s) => (
            <button
              key={s.value}
              type="button"
              onClick={() => apply({ state: s.value || null })}
              className={`h-8 rounded-lg px-3 text-sm ${state === s.value ? "bg-surface font-medium text-ink shadow-sm" : "text-ink-2 hover:text-ink"}`}
            >
              {t(s.label)}
            </button>
          ))}
        </div>
      </div>

      {data.items.length === 0 ? (
        <p className="rounded-xl border border-dashed border-line bg-surface px-4 py-10 text-center text-sm text-ink-3">
          {t("Партий по этому фильтру нет. Партии появляются, когда позицию ведут по партиям: приход с номером партии и сроком годности или ввод партий на остаток.")}
        </p>
      ) : (
        <>
          <div className="hidden overflow-x-auto rounded-xl border border-line bg-surface lg:block">
            <table className="w-full min-w-[960px] text-sm">
              <thead>
                <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                  <th className="px-3 py-2.5 font-semibold">{t("Партия")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Позиция")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Произведена")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Годна до")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Срок")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t("Остаток")}</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((b) => (
                  <tr key={b.id} onClick={() => router.push(`/procurement/batches/${b.id}`)} className="cursor-pointer border-b border-line last:border-0 hover:bg-muted/50">
                    <td className="px-3 py-2.5">
                      <Link href={`/procurement/batches/${b.id}`} className="font-medium tabular-nums text-ink hover:text-accent-strong" onClick={(e) => e.stopPropagation()}>
                        {b.number}
                      </Link>
                      <QualityMark b={b} />
                      <span className="block text-xs text-ink-3">{[b.supplierBatchNo, b.supplierName].filter(Boolean).join(" · ") || "—"}</span>
                    </td>
                    <td className="px-3 py-2.5">
                      <span className="block text-ink">{b.itemName}</span>
                      <span className="text-xs tabular-nums text-ink-3">{b.sku}</span>
                    </td>
                    <td className="px-3 py-2.5 tabular-nums text-ink-2">{dateRu(b.manufacturedOn)}</td>
                    <td className={`px-3 py-2.5 font-medium tabular-nums ${batchTone(b.state)}`}>{b.expiresOn ? dateRu(b.expiresOn) : t("не указан")}</td>
                    <td className="px-3 py-2.5 text-xs">
                      <span className={batchTone(b.state)}>{t(batchStateLabel[b.state])}</span>{b.expiresOn && <> · <Left days={b.daysLeft} tone="text-ink-3" /></>}
                    </td>
                    <td className="px-3 py-2.5 text-right tabular-nums text-ink">
                      {quantity(b.onHand)} <span className="text-ink-3">{b.unitCode}</span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <ul className="space-y-2 lg:hidden">
            {data.items.map((b) => (
              <li key={b.id}>
                <Link href={`/procurement/batches/${b.id}`} className="block rounded-xl border border-line bg-surface p-3.5">
                  <span className="flex items-start justify-between gap-3">
                    <span className="min-w-0">
                      <span className="block text-xs tabular-nums text-ink-3">{[b.number, b.supplierBatchNo].filter(Boolean).join(" · ")}</span>
                      <span className="block font-medium text-ink">
                        {b.itemName}
                        <QualityMark b={b} />
                      </span>
                    </span>
                    <span className="shrink-0 text-right text-sm font-semibold tabular-nums text-ink">
                      {quantity(b.onHand)} {b.unitCode}
                    </span>
                  </span>
                  <span className={`mt-1 block text-xs ${batchTone(b.state)}`}>
                    {b.expiresOn ? t("годна до {date}", { date: dateRu(b.expiresOn) }) : t("срок не указан")}
                    {b.expiresOn && (
                      <>
                        {" · "}
                        <Left days={b.daysLeft} tone="" />
                      </>
                    )}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
          <div className="mt-3 flex flex-wrap items-center justify-between gap-3 text-sm text-ink-2">
            <span>{t("Партий: {n}", { n: data.total })}</span>
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
