"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { CalendarClock, ShoppingBag } from "lucide-react";
import { RequestPriorityPill, RequestStatusPill } from "@/components/requests/RequestKit";
import { useT } from "@/i18n/client";
import { dateRu, money, quantity, type ToBuy } from "@/lib/procurement";
import { PurchaseSheet } from "./PurchaseSheet";

/**
 * «К закупке»: согласованные заявки, по которым снабжению осталось купить, — сначала срочные, потом по желаемой дате. По каждой — что и
 * сколько купить, по какой согласованной цене; «Куплено» — купленное сразу на складе и ждёт выдачи сотруднику.
 */
export function ToBuyView({ items, canBuy }: { items: ToBuy[]; canBuy: boolean }) {
  const t = useT();
  const router = useRouter();
  const [open, setOpen] = useState<ToBuy | null>(null);
  const total = items.reduce((sum, r) => sum + r.total, 0);
  const overdue = items.filter((r) => r.overdue).length;

  if (items.length === 0) {
    return (
      <section className="flex flex-col items-center rounded-2xl border border-line bg-surface px-6 py-14 text-center">
        <span className="grid size-14 place-items-center rounded-2xl bg-ok-soft text-ok">
          <ShoppingBag className="size-7" aria-hidden />
        </span>
        <h2 className="mt-4 text-lg font-semibold text-ink">{t("Покупать нечего")}</h2>
        <p className="mt-1.5 max-w-md text-sm text-ink-2">{t("Заявки появятся здесь, когда их согласуют — руководитель отдела, финансист, а по крупной сумме и директор.")}</p>
      </section>
    );
  }

  return (
    <>
      <p className="mb-3 text-sm text-ink-2">
        {t("Заявок: {n}", { n: items.length })}
        {total > 0 && (
          <span className="text-ink-3">
            {" "}
            · {t("к закупке")} ≈ {money(total)} {t("сум")}
          </span>
        )}
        {overdue > 0 && <span className="text-bad"> · {t("просрочено: {n}", { n: overdue })}</span>}
      </p>
      <ul className="space-y-3">
        {items.map((r) => (
          <li key={r.requestId} className={`rounded-2xl border bg-surface p-4 sm:p-5 ${r.overdue ? "border-bad/40" : "border-line"}`}>
            <div className="flex flex-wrap items-start justify-between gap-3">
              <span className="min-w-0 flex-1">
                <span className="flex flex-wrap items-center gap-2">
                  <span className="text-xs tabular-nums text-ink-3">{r.number}</span>
                  <RequestStatusPill status={r.status} />
                  <RequestPriorityPill priority={r.priority} />
                </span>
                <Link href={`/requests/${r.requestId}`} className="mt-1 block font-semibold text-ink hover:text-accent-strong">
                  {r.title}
                </Link>
                <span className="block text-sm text-ink-2">
                  {r.departmentName} · {r.initiatorName}
                </span>
                {r.desiredDate && (
                  <span className={`mt-0.5 inline-flex items-center gap-1 text-xs ${r.overdue ? "font-medium text-bad" : "text-ink-3"}`}>
                    <CalendarClock className="size-3.5" aria-hidden />
                    {r.overdue ? t("нужно было к {date}", { date: dateRu(r.desiredDate) }) : t("нужно к {date}", { date: dateRu(r.desiredDate) })}
                  </span>
                )}
              </span>
              <span className="flex items-center gap-3">
                <span className="text-right">
                  <span className="block text-lg font-semibold tabular-nums text-ink">{r.total > 0 ? money(r.total) : "—"}</span>
                  <span className="block text-xs text-ink-3">{t("к закупке, сум")}</span>
                </span>
                {canBuy && (
                  <button
                    type="button"
                    onClick={() => setOpen(r)}
                    className="inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-accent-strong"
                  >
                    <ShoppingBag className="size-4" aria-hidden /> {t("Куплено")}
                  </button>
                )}
              </span>
            </div>
            <ul className="mt-3 divide-y divide-line rounded-xl bg-muted/60">
              {r.lines.map((l) => (
                <li key={l.requestItemId} className="flex items-center justify-between gap-3 px-3 py-2 text-sm">
                  <span className="min-w-0">
                    <span className="block truncate text-ink">{l.name}</span>
                    <span className="block truncate text-xs text-ink-3">{[l.categoryName, l.comment].filter(Boolean).join(" · ")}</span>
                  </span>
                  <span className="shrink-0 text-right tabular-nums">
                    <span className="font-semibold text-ink">
                      {quantity(l.left)} {l.unitCode}
                    </span>
                    {l.price !== null && (
                      <span className="block text-[11px] text-ink-3">
                        × {money(l.price)} {t("сум")}
                      </span>
                    )}
                  </span>
                </li>
              ))}
            </ul>
          </li>
        ))}
      </ul>

      <PurchaseSheet
        open={!!open}
        onClose={() => setOpen(null)}
        request={{ id: open?.requestId ?? "", number: open?.number ?? "", title: open?.title ?? "" }}
        lines={(open?.lines ?? []).map((l) => ({ requestItemId: l.requestItemId, name: l.name, unitCode: l.unitCode, left: l.left, price: l.price }))}
        onDone={() => {
          setOpen(null);
          router.refresh();
        }}
      />
    </>
  );
}
