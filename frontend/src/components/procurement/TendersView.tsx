"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";
import { AlertTriangle, Ban, ChevronLeft, ChevronRight, CircleCheckBig, Hourglass, Plus, ScrollText, Search, type LucideIcon } from "lucide-react";
import { useT } from "@/i18n/client";
import { dateRu, money, tenderStatusLabel, type TenderList, type TenderStatus } from "@/lib/procurement";

const field =
  "h-10 rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";

const statuses = [
  { value: "", label: "Все" },
  { value: "waiting", label: "Ждут поставки" },
  { value: "overdue", label: "Просрочены" },
  { value: "delivered", label: "Поставлено" },
  { value: "cancelled", label: "Отменённые" },
] as const;

const look: Record<TenderStatus, { icon: LucideIcon; tone: string }> = {
  Contracted: { icon: Hourglass, tone: "bg-accent-soft text-accent-strong" },
  Delivered: { icon: CircleCheckBig, tone: "bg-ok-soft text-ok" },
  Cancelled: { icon: Ban, tone: "bg-muted text-ink-3" },
};

export function TenderStatusPill({ status }: { status: TenderStatus }) {
  const t = useT();
  const l = look[status];
  return (
    <span className={`inline-flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded-md px-2 py-0.5 text-xs font-semibold leading-5 ${l.tone}`}>
      <l.icon className="size-3.5" aria-hidden />
      {t(tenderStatusLabel[status])}
    </span>
  );
}

/**
 * Журнал тендерных закупок: ждут поставки, просрочены, сумма по фильтру; поиск (номер, предмет, тендер, договор, поставщик, позиция),
 * статус и период по дате тендера; таблица на компьютере и карточки на телефоне.
 */
export function TendersView({ data }: { data: TenderList }) {
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

  const filtered = !!(status || params.get("q") || params.get("from") || params.get("to"));
  const tiles = [
    { label: "Ждут поставки", value: String(data.waiting), status: "waiting", tone: "" },
    { label: "Просрочены", value: String(data.overdue), status: "overdue", tone: data.overdue > 0 ? "text-bad" : "" },
    { label: filtered ? "Сумма по фильтру, сум" : "Сумма в журнале, сум", value: money(data.amount), status: null, tone: "" },
  ];
  const pages = Math.max(1, Math.ceil(data.total / data.pageSize));

  if (data.total === 0 && !filtered) {
    return (
      <section className="flex flex-col items-center rounded-2xl border border-line bg-surface px-6 py-14 text-center">
        <span className="grid size-14 place-items-center rounded-2xl bg-accent-soft text-accent-strong">
          <ScrollText className="size-7" aria-hidden />
        </span>
        <h2 className="mt-4 text-lg font-semibold text-ink">{t("Тендерных закупок пока нет")}</h2>
        <p className="mt-1.5 max-w-md text-sm text-ink-2">
          {t("Записывайте сюда закупки сырья и материалов для производства, проведённые через тендер: кто победил, по какой цене, договор и срок поставки.")}
        </p>
        {data.canCreate && (
          <Link href="/procurement/tenders/new" className="mt-5 inline-flex h-10 items-center gap-2 rounded-xl bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong">
            <Plus className="size-4" aria-hidden /> {t("Добавить тендер")}
          </Link>
        )}
      </section>
    );
  }

  return (
    <>
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
        {tiles.map((tile) =>
          tile.status ? (
            <button
              key={tile.label}
              type="button"
              onClick={() => apply({ status: status === tile.status ? null : tile.status })}
              className={`rounded-xl border bg-surface p-4 text-left transition-colors hover:border-accent/40 ${status === tile.status ? "border-accent/50 ring-1 ring-accent/20" : "border-line"}`}
            >
              <div className={`text-2xl font-semibold tabular-nums text-ink ${tile.tone}`}>{tile.value}</div>
              <div className="mt-0.5 text-xs text-ink-3">{t(tile.label)}</div>
            </button>
          ) : (
            <div key={tile.label} className="rounded-xl border border-line bg-surface p-4 max-sm:col-span-2">
              <div className="text-2xl font-semibold tabular-nums text-ink">{tile.value}</div>
              <div className="mt-0.5 text-xs text-ink-3">{t(tile.label)}</div>
            </div>
          ),
        )}
      </div>

      <div className="my-4 flex flex-wrap items-center gap-2">
        <label className="relative block min-w-0 basis-full sm:max-w-[400px] sm:flex-1 sm:basis-auto">
          <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" aria-hidden />
          <span className="sr-only">{t("Поиск")}</span>
          <input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("Предмет, поставщик, договор, позиция")} className={`${field} w-full pl-9`} />
        </label>
        <select value={status} onChange={(e) => apply({ status: e.target.value || null })} aria-label={t("Статус")} className={field}>
          {statuses.map((s) => (
            <option key={s.value} value={s.value}>
              {t(s.label)}
            </option>
          ))}
        </select>
        <label className="flex items-center gap-1.5 text-sm text-ink-3">
          {t("с")}
          <input type="date" value={params.get("from") ?? ""} onChange={(e) => apply({ from: e.target.value || null })} aria-label={t("Дата тендера с")} className={field} />
        </label>
        <label className="flex items-center gap-1.5 text-sm text-ink-3">
          {t("по")}
          <input type="date" value={params.get("to") ?? ""} onChange={(e) => apply({ to: e.target.value || null })} aria-label={t("Дата тендера по")} className={field} />
        </label>
      </div>

      {data.items.length === 0 ? (
        <p className="rounded-xl border border-dashed border-line bg-surface px-4 py-10 text-center text-sm text-ink-3">{t("Тендерных закупок по этому фильтру нет.")}</p>
      ) : (
        <>
          <div className="hidden overflow-x-auto rounded-xl border border-line bg-surface lg:block">
            <table className="w-full min-w-[960px] text-sm">
              <thead>
                <tr className="whitespace-nowrap border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                  <th className="px-3 py-2.5 font-semibold">{t("Тендер")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Победитель")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Позиции")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Статус")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t("Сумма, сум")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Поставка")}</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((r) => (
                  <tr key={r.id} className="border-b border-line align-top last:border-0 hover:bg-muted/50">
                    <td className="max-w-80 px-3 py-2.5">
                      <Link href={`/procurement/tenders/${r.id}`} className="block truncate font-medium text-ink hover:text-accent-strong">
                        {r.title}
                      </Link>
                      <span className="block text-xs tabular-nums text-ink-3">
                        {r.number} · {dateRu(r.tenderDate)}
                        {r.tenderNo && ` · ${r.tenderNo}`}
                      </span>
                    </td>
                    <td className="px-3 py-2.5 text-ink-2">{r.supplierName}</td>
                    <td className="max-w-64 px-3 py-2.5 text-ink-2">
                      <span className="block truncate">
                        {r.firstItem}
                        {r.lines > 1 && <span className="text-ink-3"> {t("и ещё {n}", { n: r.lines - 1 })}</span>}
                      </span>
                      <span className="block text-xs text-ink-3">{r.categoryName}</span>
                    </td>
                    <td className="px-3 py-2.5">
                      <TenderStatusPill status={r.status} />
                    </td>
                    <td className="whitespace-nowrap px-3 py-2.5 text-right tabular-nums text-ink">{money(r.total)}</td>
                    <td className={`whitespace-nowrap px-3 py-2.5 tabular-nums ${r.overdue ? "font-semibold text-bad" : "text-ink-2"}`}>
                      {r.overdue && <AlertTriangle className="mr-1 inline size-3.5" aria-label={t("Просрочена")} />}
                      {r.status === "Delivered" ? dateRu(r.deliveredOn) : dateRu(r.deliveryDate)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <ul className="space-y-2 lg:hidden">
            {data.items.map((r) => (
              <li key={r.id}>
                <Link href={`/procurement/tenders/${r.id}`} className="block rounded-xl border border-line bg-surface p-3.5">
                  <span className="flex items-start justify-between gap-2">
                    <span className="min-w-0">
                      <span className="block text-xs tabular-nums text-ink-3">
                        {r.number} · {dateRu(r.tenderDate)}
                      </span>
                      <span className="block font-medium text-ink">{r.title}</span>
                    </span>
                    <TenderStatusPill status={r.status} />
                  </span>
                  <span className="mt-1 block truncate text-sm text-ink-2">{r.supplierName}</span>
                  <span className="mt-1.5 flex flex-wrap gap-x-3 text-xs text-ink-3">
                    <span className="font-medium tabular-nums text-ink-2">
                      {money(r.total)} {t("сум")}
                    </span>
                    {r.status === "Contracted" && r.deliveryDate && (
                      <span className={r.overdue ? "font-semibold text-bad" : ""}>{t("поставка до {date}", { date: dateRu(r.deliveryDate) })}</span>
                    )}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
          <div className="mt-3 flex flex-wrap items-center justify-between gap-3 text-sm text-ink-2">
            <span>{t("Записей: {n}", { n: data.total })}</span>
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
