"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { ArrowLeftRight, FileText, History, Loader2, PackageCheck } from "lucide-react";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { money, quantity, stampRu, type Claim, type ReceiptDetail } from "@/lib/procurement";
import { DocumentsBlock, OrderStatusPill } from "./ProcKit";
import { ClaimsBlock, ReceiptStatusPill } from "./ReceiptKit";

const card = "rounded-2xl border border-line bg-surface p-4 sm:p-5";
const primary = "inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50";

/**
 * Карточка приёмки: заказ и поставщик, накладная, строки (пришло, брак, недостача, принято на склад, карточка склада), претензии,
 * документы (накладная, акт), движения склада и история. Черновик без права правки (не кладовщик) — только просмотр.
 */
export function ReceiptDetailView({ receipt }: { receipt: ReceiptDetail }) {
  const t = useT();
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const claims: Claim[] = receipt.lines
    .filter((l) => l.claimStatus !== "None")
    .map((l) => ({
      lineId: l.id,
      receiptId: receipt.id,
      receiptNumber: receipt.number,
      orderId: receipt.orderId,
      orderNumber: receipt.orderNumber,
      name: l.name,
      unitCode: l.unitCode,
      defective: l.defective,
      shortage: l.shortage,
      amount: Math.round((l.defective + l.shortage) * l.unitPrice * 100) / 100,
      comment: l.comment,
      status: l.claimStatus,
      resolution: l.claimResolution,
      postedAt: receipt.postedAt,
      resolvedAt: l.claimResolvedAt,
    }));
  const posted = receipt.status === "Posted";
  const accepted = receipt.lines.reduce((s, l) => s + l.accepted * l.unitPrice, 0);

  const post = async () => {
    setBusy(true);
    setError(null);
    try {
      await bff(`procurement/receipts/${receipt.id}/post`, { method: "POST", body: JSON.stringify({ version: receipt.version }) });
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <PageHeader
        title={t("Приёмка {n}", { n: receipt.number })}
        subtitle={
          <span className="inline-flex flex-wrap items-center gap-2">
            <ReceiptStatusPill status={receipt.status} />
            <span>{receipt.supplierName}</span>
            {receipt.reference && <span className="text-ink-3">{t("накладная {n}", { n: receipt.reference })}</span>}
          </span>
        }
        back="/procurement/receipts"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Снабжение"), href: "/procurement" },
          { label: t("Приёмка"), href: "/procurement/receipts" },
          { label: receipt.number },
        ]}
        actions={
          receipt.can.post ? (
            <button type="button" onClick={() => void post()} disabled={busy} className={primary}>
              {busy ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <PackageCheck className="size-4" aria-hidden />} {t("Подтвердить поступление")}
            </button>
          ) : posted ? (
            <a href={`/bff/api/procurement/receipts/${receipt.id}/act`} download className="inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3.5 text-sm font-medium text-ink hover:bg-muted">
              <FileText className="size-4" aria-hidden /> {t("Акт приёмки (PDF)")}
            </a>
          ) : undefined
        }
      />
      <PageBody>
        <div className="mx-auto grid max-w-6xl gap-4 lg:grid-cols-[minmax(0,1fr)_340px]">
          <div className="min-w-0 space-y-4">
            <ErrorNote text={error} />
            {!posted && <p className="rounded-xl border border-warn/30 bg-warn-soft px-4 py-3 text-sm text-ink">{t("Черновик: склад ещё не изменился. Подтверждает поступление кладовщик.")}</p>}
            <section className={card} aria-labelledby="rc-items">
              <h2 id="rc-items" className="mb-3 text-base font-semibold text-ink">
                {t("Позиции")} <span className="font-normal text-ink-3">· {receipt.lines.length}</span>
              </h2>
              <div className="overflow-x-auto">
                <table className="w-full min-w-[640px] text-sm">
                  <thead>
                    <tr className="whitespace-nowrap border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                      <th className="py-2 pr-3 font-semibold">{t("Наименование")}</th>
                      <th className="py-2 pr-3 text-right font-semibold">{t("Пришло")}</th>
                      <th className="py-2 pr-3 text-right font-semibold">{t("Брак")}</th>
                      <th className="py-2 pr-3 text-right font-semibold">{t("Недостача")}</th>
                      <th className="py-2 text-right font-semibold">{posted ? t("Принято") : t("Будет принято")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {receipt.lines.map((l) => {
                      const willAccept = posted ? l.accepted : Math.max(0, l.received - l.defective);
                      return (
                        <tr key={l.id} className="border-b border-line align-top last:border-0">
                          <td className="py-2.5 pr-3">
                            <span className="text-ink">{l.name}</span>
                            <span className="block text-xs text-ink-3">
                              <Link href={`/requests/${l.requestId}`} className="text-accent-strong hover:underline">
                                {l.requestNumber}
                              </Link>
                              {l.itemSku && ` · ${l.itemSku}`}
                              {!l.itemId && !posted && <span className="text-warn"> · {t("нет карточки склада")}</span>}
                            </span>
                            {l.comment && <span className="mt-0.5 block text-xs text-ink-3">{l.comment}</span>}
                          </td>
                          <td className="whitespace-nowrap py-2.5 pr-3 text-right tabular-nums">
                            {quantity(l.received)} {l.unitCode}
                          </td>
                          <td className={`whitespace-nowrap py-2.5 pr-3 text-right tabular-nums ${l.defective > 0 ? "text-bad" : "text-ink-3"}`}>{l.defective > 0 ? quantity(l.defective) : "—"}</td>
                          <td className={`whitespace-nowrap py-2.5 pr-3 text-right tabular-nums ${l.shortage > 0 ? "text-bad" : "text-ink-3"}`}>{l.shortage > 0 ? quantity(l.shortage) : "—"}</td>
                          <td className="whitespace-nowrap py-2.5 text-right font-semibold tabular-nums text-ink">
                            {quantity(willAccept)} {l.unitCode}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
              {posted && (
                <p className="mt-3 flex flex-wrap items-center justify-between gap-2 border-t border-line pt-3 text-sm">
                  <span className="text-ink-3">{t("Принято на склад по ценам заказа")}</span>
                  <span className="font-semibold tabular-nums">
                    {money(accepted)} {t("сум")}
                  </span>
                </p>
              )}
            </section>

            {claims.length > 0 && (
              <section className={`${card} ${claims.some((c) => c.status === "Open") ? "border-bad/30" : ""}`}>
                <h2 className="mb-1 text-base font-semibold text-ink">{t("Претензии поставщику")}</h2>
                <ClaimsBlock claims={claims} canResolve={receipt.can.resolveClaims} />
              </section>
            )}

            <section className={card}>
              <h2 className="mb-3 text-base font-semibold text-ink">{t("Документы")}</h2>
              <DocumentsBlock owner="receipt" id={receipt.id} documents={receipt.documents} can={receipt.can.attach} kinds={["waybill", "act", "invoice", "other"]} />
            </section>
          </div>

          <div className="space-y-4">
            <section className={card}>
              <h2 className="mb-1 text-base font-semibold text-ink">{t("Сведения")}</h2>
              <dl className="divide-y divide-line text-sm">
                <div className="flex items-start justify-between gap-4 py-2">
                  <dt className="text-ink-3">{t("Заказ")}</dt>
                  <dd className="text-right">
                    <Link href={`/procurement/orders/${receipt.orderId}`} className="tabular-nums text-accent-strong hover:underline">
                      {receipt.orderNumber}
                    </Link>
                    <span className="mt-1 block">
                      <OrderStatusPill status={receipt.orderStatus} />
                    </span>
                  </dd>
                </div>
                <div className="flex items-start justify-between gap-4 py-2">
                  <dt className="text-ink-3">{t("Поставщик")}</dt>
                  <dd className="text-right">
                    <Link href={`/procurement/suppliers/${receipt.supplierId}`} className="text-accent-strong hover:underline">
                      {receipt.supplierName}
                    </Link>
                  </dd>
                </div>
                <div className="flex items-start justify-between gap-4 py-2">
                  <dt className="text-ink-3">{posted ? t("Подтверждена") : t("Создана")}</dt>
                  <dd className="text-right tabular-nums">
                    {stampRu(receipt.postedAt ?? receipt.createdAt)}
                    {receipt.actorName && <span className="block text-xs text-ink-3">{receipt.actorName}</span>}
                  </dd>
                </div>
              </dl>
              {receipt.comment && <p className="mt-2 whitespace-pre-line rounded-lg bg-muted px-3 py-2 text-sm text-ink-2">{receipt.comment}</p>}
              {posted && (
                <Link href={`/procurement/movements?q=${encodeURIComponent(receipt.number)}`} className="mt-3 inline-flex items-center gap-1.5 text-sm text-accent-strong hover:underline">
                  <ArrowLeftRight className="size-4" aria-hidden /> {t("Движения склада по приёмке")}
                </Link>
              )}
            </section>

            <section className={card}>
              <h2 className="mb-3 flex items-center gap-2 text-base font-semibold text-ink">
                <History className="size-4 text-ink-3" aria-hidden /> {t("История")}
              </h2>
              <ol className="relative space-y-4 border-l border-line pl-4">
                {[...receipt.history].reverse().map((e) => (
                  <li key={e.id} className="relative">
                    <span className="absolute -left-[21px] top-1.5 size-2.5 rounded-full bg-ink-4 ring-4 ring-surface" aria-hidden />
                    <p className="text-sm text-ink">{t(e.text)}</p>
                    {e.comment && <p className="mt-1 whitespace-pre-line rounded-lg bg-muted px-2.5 py-1.5 text-sm text-ink-2">{e.comment}</p>}
                    <p className="mt-0.5 text-xs text-ink-3">
                      {e.actorName} · {stampRu(e.createdAt)}
                    </p>
                  </li>
                ))}
              </ol>
            </section>
          </div>
        </div>
      </PageBody>
    </>
  );
}
