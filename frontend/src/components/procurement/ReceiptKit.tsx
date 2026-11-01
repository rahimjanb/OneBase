"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { CircleCheckBig, FilePen, Loader2, ShieldAlert } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { dateRu, money, quantity, receiptStatusLabel, stampRu, type Claim, type ReceiptStatus } from "@/lib/procurement";

const secondary = "inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3.5 text-sm font-medium text-ink hover:bg-muted disabled:opacity-50";
const primary = "inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50";

export function ReceiptStatusPill({ status }: { status: ReceiptStatus }) {
  const t = useT();
  const Icon = status === "Draft" ? FilePen : CircleCheckBig;
  return (
    <span
      className={`inline-flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded-md px-2 py-0.5 text-xs font-semibold leading-5 ${status === "Draft" ? "bg-muted text-ink-2" : "bg-ok-soft text-ok"}`}
    >
      <Icon className="size-3.5" aria-hidden />
      {t(receiptStatusLabel[status])}
    </span>
  );
}

/** «брак 2 пачка, недостача 3 пачка» */
export function claimText(t: ReturnType<typeof useT>, c: Pick<Claim, "defective" | "shortage" | "unitCode">) {
  return [
    c.defective > 0 ? t("брак {n} {unit}", { n: quantity(c.defective), unit: c.unitCode }) : null,
    c.shortage > 0 ? t("недостача {n} {unit}", { n: quantity(c.shortage), unit: c.unitCode }) : null,
  ]
    .filter(Boolean)
    .join(", ");
}

/**
 * Претензии поставщику (брак и недостача из приёмок): открытые — сверху; снабжение закрывает претензию решением (замена, возврат денег,
 * уценка). showOrder — показывать заказ (в карточке поставщика).
 */
export function ClaimsBlock({ claims, canResolve, showOrder = false }: { claims: Claim[]; canResolve: boolean; showOrder?: boolean }) {
  const t = useT();
  const router = useRouter();
  const [open, setOpen] = useState<Claim | null>(null);
  const [text, setText] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const resolve = async () => {
    if (!open) return;
    if (!text.trim()) {
      setError(t("Опишите решение по претензии: замена, возврат денег, уценка…"));
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await bff(`procurement/claims/${open.lineId}/resolve`, { method: "POST", body: JSON.stringify({ comment: text.trim() }) });
      setOpen(null);
      setText("");
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <ul className="divide-y divide-line">
        {claims.map((c) => (
          <li key={c.lineId} className="flex flex-wrap items-start justify-between gap-x-3 gap-y-1.5 py-2.5 text-sm">
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-2">
                <span className={`inline-flex items-center gap-1 rounded-md px-1.5 py-0.5 text-[11px] font-semibold ${c.status === "Open" ? "bg-bad-soft text-bad" : "bg-ok-soft text-ok"}`}>
                  {c.status === "Open" ? <ShieldAlert className="size-3" aria-hidden /> : <CircleCheckBig className="size-3" aria-hidden />}
                  {c.status === "Open" ? t("открыта") : t("закрыта")}
                </span>
                <span className="font-medium text-ink">{c.name}</span>
              </div>
              <p className="mt-0.5 text-ink-2">
                {claimText(t, c)} · <span className="tabular-nums">≈ {money(c.amount)} {t("сум")}</span>
              </p>
              <p className="mt-0.5 text-xs text-ink-3">
                <Link href={`/procurement/receipts/${c.receiptId}`} className="text-accent-strong hover:underline">
                  {c.receiptNumber}
                </Link>
                {showOrder && (
                  <>
                    {" · "}
                    <Link href={`/procurement/orders/${c.orderId}`} className="text-accent-strong hover:underline">
                      {c.orderNumber}
                    </Link>
                  </>
                )}
                {c.postedAt && ` · ${dateRu(c.postedAt)}`}
              </p>
              {c.comment && <p className="mt-1 text-xs text-ink-3">{c.comment}</p>}
              {c.resolution && (
                <p className="mt-1 rounded-lg bg-muted px-2.5 py-1.5 text-ink-2">
                  {c.resolution}
                  {c.resolvedAt && <span className="block text-xs text-ink-3">{stampRu(c.resolvedAt)}</span>}
                </p>
              )}
            </div>
            {canResolve && c.status === "Open" && (
              <button
                type="button"
                onClick={() => {
                  setOpen(c);
                  setText("");
                  setError(null);
                }}
                className={secondary}
              >
                <CircleCheckBig className="size-4" aria-hidden /> {t("Закрыть претензию")}
              </button>
            )}
          </li>
        ))}
      </ul>
      <Sheet
        open={open !== null}
        onClose={() => setOpen(null)}
        title={t("Закрыть претензию")}
        subtitle={open ? `${open.name} · ${claimText(t, open)}` : ""}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" onClick={() => setOpen(null)} className={secondary}>
              {t("Закрыть")}
            </button>
            <button type="button" onClick={() => void resolve()} disabled={busy} className={primary}>
              {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}
              {t("Закрыть претензию")}
            </button>
          </div>
        }
      >
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Решение")}</span>
          <textarea
            value={text}
            onChange={(e) => setText(e.target.value)}
            maxLength={1000}
            rows={4}
            autoFocus
            placeholder={t("Например: поставщик заменит брак со следующей поставкой")}
            className="w-full rounded-lg border border-line bg-surface px-3 py-2.5 text-sm text-ink focus:border-accent focus:outline-none"
          />
        </label>
        <div className="mt-3">
          <ErrorNote text={error} />
        </div>
      </Sheet>
    </>
  );
}
