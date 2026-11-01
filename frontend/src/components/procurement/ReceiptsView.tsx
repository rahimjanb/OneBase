"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";
import { AlertTriangle, ChevronLeft, ChevronRight, PackageCheck, Search, ShieldAlert } from "lucide-react";
import { useT } from "@/i18n/client";
import { dateRu, stampRu, type ExpectedRow, type Page, type ReceiptRow } from "@/lib/procurement";
import { OrderStatusPill } from "./ProcKit";
import { ReceiptStatusPill } from "./ReceiptKit";

const field =
  "h-10 rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
const primary = "inline-flex h-9 shrink-0 items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-accent-strong";

const groups = [
  { value: "", label: "Все приёмки" },
  { value: "draft", label: "Черновики" },
  { value: "posted", label: "Подтверждённые" },
  { value: "claims", label: "С открытыми претензиями" },
] as const;

/**
 * «Снабжение → Приёмка»: заказы, которые ждут поставки (просроченные — сверху; кладовщик принимает товар), и журнал приёмок — черновики,
 * подтверждённые, с открытыми претензиями; поиск по номеру, накладной, заказу, поставщику и товару.
 */
export function ReceiptsView({ expected, data, warehouse }: { expected: ExpectedRow[]; data: Page<ReceiptRow>; warehouse: boolean }) {
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
  const pages = Math.max(1, Math.ceil(data.total / data.pageSize));

  return (
    <div className="space-y-6">
      <section aria-labelledby="rc-expected">
        <h2 id="rc-expected" className="mb-3 flex items-center gap-2 text-base font-semibold text-ink">
          {t("Ждут поставки")} <span className="font-normal text-ink-3">· {expected.length}</span>
        </h2>
        {expected.length === 0 ? (
          <p className="rounded-xl border border-dashed border-line bg-surface px-4 py-8 text-center text-sm text-ink-3">
            {t("Заказов, которые ждут поставки, нет. Они появятся, когда снабжение отметит заказ отправленным поставщику.")}
          </p>
        ) : (
          <ul className="grid gap-3 md:grid-cols-2 2xl:grid-cols-3">
            {expected.map((o) => (
              <li key={o.orderId} className={`flex flex-col rounded-xl border bg-surface p-4 ${o.overdue ? "border-bad/40" : "border-line"}`}>
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <Link href={`/procurement/orders/${o.orderId}`} className="text-xs font-medium tabular-nums text-ink-3 hover:text-accent-strong">
                      {o.number}
                    </Link>
                    <p className="truncate font-semibold text-ink">{o.supplierName}</p>
                  </div>
                  <OrderStatusPill status={o.status} />
                </div>
                <dl className="mt-2 space-y-0.5 text-sm text-ink-2">
                  <div className={o.overdue ? "font-semibold text-bad" : ""}>
                    {o.overdue && <AlertTriangle className="mr-1 inline size-3.5" aria-hidden />}
                    {o.expectedDate ? t("ожидается {date}", { date: dateRu(o.expectedDate) }) : t("дата поставки не указана")}
                  </div>
                  <div>{t("ждут поставки: {a} из {b} позиций", { a: o.waiting, b: o.lines })}</div>
                  <div className="text-xs text-ink-3">
                    {o.requests.join(", ")} · {o.responsibleName}
                  </div>
                </dl>
                <div className="mt-3 flex flex-wrap items-center gap-2 pt-1">
                  {warehouse && (
                    <Link href={`/procurement/receipts/new?order=${o.orderId}`} className={primary}>
                      <PackageCheck className="size-4" aria-hidden /> {t("Принять товар")}
                    </Link>
                  )}
                  {o.drafts > 0 && (
                    <button type="button" onClick={() => apply({ status: "draft", q: o.number })} className="text-xs font-medium text-accent-strong hover:underline">
                      {t("черновиков: {n}", { n: o.drafts })}
                    </button>
                  )}
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section aria-labelledby="rc-list">
        <h2 id="rc-list" className="mb-3 text-base font-semibold text-ink">
          {t("Приёмки")}
        </h2>
        <div className="mb-3 flex flex-wrap items-center gap-2">
          <label className="relative block min-w-0 basis-full sm:max-w-[300px] sm:flex-1 sm:basis-auto">
            <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" aria-hidden />
            <span className="sr-only">{t("Поиск")}</span>
            <input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("Номер, накладная, заказ, поставщик, товар")} className={`${field} w-full pl-9`} />
          </label>
          <select value={status} onChange={(e) => apply({ status: e.target.value || null })} aria-label={t("Статус")} className={field}>
            {groups.map((g) => (
              <option key={g.value} value={g.value}>
                {t(g.label)}
              </option>
            ))}
          </select>
        </div>
        {data.items.length === 0 ? (
          <p className="rounded-xl border border-dashed border-line bg-surface px-4 py-8 text-center text-sm text-ink-3">
            {status || params.get("q") ? t("Приёмок по этому фильтру нет.") : t("Приёмок ещё не было.")}
          </p>
        ) : (
          <>
            <div className="hidden overflow-x-auto rounded-xl border border-line bg-surface lg:block">
              <table className="w-full min-w-[900px] text-sm">
                <thead>
                  <tr className="whitespace-nowrap border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                    <th className="px-3 py-2.5 font-semibold">{t("Приёмка")}</th>
                    <th className="px-3 py-2.5 font-semibold">{t("Заказ")}</th>
                    <th className="px-3 py-2.5 font-semibold">{t("Поставщик")}</th>
                    <th className="px-3 py-2.5 font-semibold">{t("Накладная")}</th>
                    <th className="px-3 py-2.5 font-semibold">{t("Статус")}</th>
                    <th className="px-3 py-2.5 font-semibold">{t("Когда")}</th>
                    <th className="px-3 py-2.5 font-semibold">{t("Кладовщик")}</th>
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((r) => (
                    <tr key={r.id} className="border-b border-line align-top last:border-0 hover:bg-muted/50">
                      <td className="px-3 py-2.5">
                        <Link href={`/procurement/receipts/${r.id}`} className="font-medium tabular-nums text-ink hover:text-accent-strong">
                          {r.number}
                        </Link>
                        <span className="block text-xs text-ink-3">{t("позиций: {n}", { n: r.lines })}</span>
                      </td>
                      <td className="px-3 py-2.5">
                        <Link href={`/procurement/orders/${r.orderId}`} className="tabular-nums text-accent-strong hover:underline">
                          {r.orderNumber}
                        </Link>
                      </td>
                      <td className="px-3 py-2.5 text-ink-2">{r.supplierName}</td>
                      <td className="px-3 py-2.5 text-ink-2">{r.reference ?? "—"}</td>
                      <td className="px-3 py-2.5">
                        <span className="flex flex-wrap items-center gap-1.5">
                          <ReceiptStatusPill status={r.status} />
                          {r.openClaims > 0 ? (
                            <span className="inline-flex items-center gap-1 text-xs font-semibold text-bad">
                              <ShieldAlert className="size-3.5" aria-hidden /> {t("претензий: {n}", { n: r.openClaims })}
                            </span>
                          ) : (
                            r.problems && <span className="text-xs text-ink-3">{t("брак или недостача")}</span>
                          )}
                        </span>
                      </td>
                      <td className="whitespace-nowrap px-3 py-2.5 tabular-nums text-ink-2">{stampRu(r.postedAt ?? r.createdAt)}</td>
                      <td className="px-3 py-2.5 text-ink-2">{r.actorName ?? "—"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <ul className="space-y-2 lg:hidden">
              {data.items.map((r) => (
                <li key={r.id}>
                  <Link href={`/procurement/receipts/${r.id}`} className="block rounded-xl border border-line bg-surface p-3.5">
                    <span className="flex items-start justify-between gap-2">
                      <span className="min-w-0">
                        <span className="block text-xs tabular-nums text-ink-3">
                          {r.number} · {r.orderNumber}
                        </span>
                        <span className="block font-medium text-ink">{r.supplierName}</span>
                      </span>
                      <ReceiptStatusPill status={r.status} />
                    </span>
                    <span className="mt-1.5 flex flex-wrap gap-x-3 text-xs text-ink-3">
                      <span className="tabular-nums">{stampRu(r.postedAt ?? r.createdAt)}</span>
                      {r.reference && <span>{t("накладная {n}", { n: r.reference })}</span>}
                      {r.openClaims > 0 && <span className="font-semibold text-bad">{t("претензий: {n}", { n: r.openClaims })}</span>}
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
            <div className="mt-3 flex flex-wrap items-center justify-between gap-3 text-sm text-ink-2">
              <span>{t("Приёмок: {n}", { n: data.total })}</span>
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
      </section>
    </div>
  );
}
