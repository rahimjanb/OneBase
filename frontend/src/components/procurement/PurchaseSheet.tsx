"use client";

import { useEffect, useRef, useState } from "react";
import { Loader2, ShoppingBag } from "lucide-react";
import { BatchDrafts, newBatchDraft } from "@/components/procurement/BatchDrafts";
import { Sheet } from "@/components/field/Sheet";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { batchExpiry, batchesTotal, money, parseNumber, quantity, type BatchDraft, type PurchaseResult, type SupplierRow } from "@/lib/procurement";

/**
 * Позиция, которую можно отметить купленной: сколько осталось купить, согласованная цена за единицу; tracksBatches — карточка склада
 * ведётся по партиям: купленное вносится партиями со сроком годности (количество — их сумма).
 */
export type PurchaseLine = { requestItemId: string; name: string; unitCode: string; left: number; price: number | null; tracksBatches?: boolean; shelfLifeDays?: number | null };

const field = "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink focus:border-accent focus:outline-none";
const secondary = "inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3.5 text-sm font-medium text-ink hover:bg-muted disabled:opacity-50";
const primary = "inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50";

const text = (v: number | null) => (v === null ? "" : String(v).replace(".", ","));
const newKey = () => (typeof crypto !== "undefined" && "randomUUID" in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`);

/**
 * «Куплено» по заявке (снабжение): сколько и почём купили по каждой позиции (по умолчанию — весь остаток по согласованной цене), где купили
 * (поставщик из справочника — подсказкой, новый магазин добавится сам), номер чека и комментарий. Купленное сразу приходуется на склад и
 * ждёт выдачи сотруднику. Позицию, которую ещё не купили, оставьте с пустым количеством. Позиция с партиями — партии со сроком годности;
 * с контролем качества партия встаёт на контроль, резерв заявки — после допуска (брак — позиция снова к закупке).
 */
export function PurchaseSheet({
  open,
  onClose,
  request,
  lines,
  onDone,
}: {
  open: boolean;
  onClose: () => void;
  request: { id: string; number: string; title: string };
  lines: PurchaseLine[];
  onDone: (result: PurchaseResult) => void;
}) {
  const t = useT();
  const [qty, setQty] = useState<Record<string, string>>({});
  const [price, setPrice] = useState<Record<string, string>>({});
  const [batches, setBatches] = useState<Record<string, BatchDraft[]>>({});
  const [where, setWhere] = useState("");
  const [reference, setReference] = useState("");
  const [comment, setComment] = useState("");
  const [suppliers, setSuppliers] = useState<SupplierRow[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const key = useRef("");

  useEffect(() => {
    if (!open) return;
    setQty(Object.fromEntries(lines.map((l) => [l.requestItemId, text(l.left)])));
    setPrice(Object.fromEntries(lines.map((l) => [l.requestItemId, text(l.price)])));
    setBatches(Object.fromEntries(lines.filter((l) => l.tracksBatches).map((l) => [l.requestItemId, [newBatchDraft(text(l.left))]])));
    setWhere("");
    setReference("");
    setComment("");
    setError(null);
    key.current = newKey();
    let alive = true;
    bff<SupplierRow[]>("procurement/suppliers/options")
      .then((rows) => alive && setSuppliers(rows))
      .catch(() => alive && setSuppliers([]));
    return () => {
      alive = false;
    };
    // Форма заполняется заново при каждом открытии.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const rows = lines.map((l) => {
    const drafts = batches[l.requestItemId];
    // Позиция с партиями: количество — сумма партий; пустые партии — позицию не купили.
    const q = l.tracksBatches && drafts ? (drafts.every((b) => !b.quantity.trim()) ? null : batchesTotal(drafts) ?? NaN) : parseNumber(qty[l.requestItemId] ?? "");
    const p = parseNumber(price[l.requestItemId] ?? "");
    return { line: l, q, p, sum: q !== null && !Number.isNaN(q) && p !== null ? Math.round(q * p * 100) / 100 : null };
  });
  const total = rows.reduce((s, r) => s + (r.q && r.q > 0 && r.sum ? r.sum : 0), 0);

  const submit = async () => {
    const chosen = rows.filter((r) => r.q !== null && r.q !== 0);
    if (chosen.length === 0) return setError(t("Укажите, что куплено: количество и цену хотя бы по одной позиции."));
    for (const r of chosen) {
      if (r.line.tracksBatches && Number.isNaN(r.q)) return setError(t("«{name}»: укажите количество каждой партии.", { name: r.line.name }));
      if (r.line.tracksBatches && batches[r.line.requestItemId]?.some((b) => !batchExpiry(b.expires, b.made, r.line.shelfLifeDays)))
        return setError(t("«{name}»: укажите срок годности каждой партии.", { name: r.line.name }));
      if (r.q === null || r.q < 0 || Math.round(r.q * 1000) / 1000 !== r.q) return setError(t("«{name}»: количество — больше нуля, не больше трёх знаков после запятой.", { name: r.line.name }));
      if (r.q > r.line.left) return setError(t("«{name}»: осталось купить {n} — больше не оприходовать.", { name: r.line.name, n: `${quantity(r.line.left)} ${r.line.unitCode}` }));
      if (r.p === null || r.p <= 0 || Math.round(r.p * 100) / 100 !== r.p) return setError(t("«{name}»: укажите цену за {unit} — больше нуля, не больше двух знаков после запятой.", { name: r.line.name, unit: r.line.unitCode }));
    }
    const name = where.trim();
    if (!name) return setError(t("Укажите, где купили: магазин или поставщика."));
    const known = suppliers.find((s) => s.name.trim().toLowerCase() === name.toLowerCase());
    setBusy(true);
    setError(null);
    try {
      const result = await bff<PurchaseResult>(`procurement/requests/${request.id}/purchase`, {
        method: "POST",
        body: JSON.stringify({
          lines: chosen.map((r) => ({
            requestItemId: r.line.requestItemId,
            quantity: r.q,
            price: r.p,
            batches: r.line.tracksBatches
              ? (batches[r.line.requestItemId] ?? []).map((b) => ({
                  supplierBatchNo: b.no.trim() || null,
                  manufacturedOn: b.made || null,
                  expiresOn: b.expires || null,
                  quantity: parseNumber(b.quantity),
                }))
              : null,
          })),
          supplierId: known?.id ?? null,
          supplierName: known ? null : name,
          reference: reference.trim() || null,
          comment: comment.trim() || null,
          clientKey: key.current,
        }),
      });
      onDone(result);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Sheet
      open={open}
      onClose={onClose}
      title={t("Куплено")}
      subtitle={`${request.number} · ${request.title}`}
      size={lines.some((l) => l.tracksBatches) ? "xl" : undefined}
      footer={
        <div className="flex items-center justify-between gap-3">
          <p className="text-sm text-ink-2">
            {t("Итого")} <span className="font-semibold tabular-nums text-ink">{money(total)}</span> {t("сум")}
          </p>
          <div className="flex gap-2">
            <button type="button" onClick={onClose} className={secondary}>
              {t("Закрыть")}
            </button>
            <button type="button" onClick={() => void submit()} disabled={busy} className={primary}>
              {busy ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <ShoppingBag className="size-4" aria-hidden />} {t("Оприходовать")}
            </button>
          </div>
        </div>
      }
    >
      <p className="mb-2 text-sm font-medium text-ink">
        {t("Что купили")} <span className="font-normal text-ink-3">{t("количество и цена за единицу")}</span>
      </p>
      <ul className="mb-4 space-y-2">
        {rows.map(({ line, q, p, sum }) => {
          const dearer = line.price !== null && p !== null && p > line.price;
          return (
            <li key={line.requestItemId} className="rounded-xl border border-line px-3 py-2.5">
              <div className="flex items-baseline justify-between gap-3">
                <span className="min-w-0 truncate text-sm font-medium text-ink">{line.name}</span>
                <span className="shrink-0 text-xs text-ink-3">{t("осталось {n}", { n: `${quantity(line.left)} ${line.unitCode}` })}</span>
              </div>
              <div className="mt-2 flex items-center gap-2">
                {line.tracksBatches ? (
                  <span className="w-20 text-right text-sm tabular-nums text-ink-2" aria-label={t("Куплено, количество: {name}", { name: line.name })}>
                    {q === null || Number.isNaN(q) ? "—" : quantity(q)}
                  </span>
                ) : (
                  <input
                    value={qty[line.requestItemId] ?? ""}
                    onChange={(e) => setQty((v) => ({ ...v, [line.requestItemId]: e.target.value }))}
                    inputMode="decimal"
                    aria-label={t("Куплено, количество: {name}", { name: line.name })}
                    className="h-9 w-20 rounded-md border border-line bg-surface px-2 text-right text-sm tabular-nums text-ink focus:border-accent focus:outline-none"
                  />
                )}
                <span className="w-8 text-xs text-ink-3">{line.unitCode}</span>
                <span className="text-xs text-ink-3" aria-hidden>
                  ×
                </span>
                <input
                  value={price[line.requestItemId] ?? ""}
                  onChange={(e) => setPrice((v) => ({ ...v, [line.requestItemId]: e.target.value }))}
                  inputMode="decimal"
                  aria-label={t("Цена за единицу: {name}", { name: line.name })}
                  className="h-9 w-28 rounded-md border border-line bg-surface px-2 text-right text-sm tabular-nums text-ink focus:border-accent focus:outline-none"
                />
                <span className="flex-1 text-right text-sm tabular-nums text-ink">{sum !== null ? `${money(sum)} ${t("сум")}` : "—"}</span>
              </div>
              {line.price !== null && (
                <p className={`mt-1 text-xs ${dearer ? "text-warn" : "text-ink-3"}`}>
                  {dearer
                    ? t("Дороже согласованной цены {price} сум", { price: money(line.price) })
                    : t("Согласовано: {price} сум за {unit}", { price: money(line.price), unit: line.unitCode })}
                </p>
              )}
              {line.tracksBatches && batches[line.requestItemId] && (
                <>
                  <p className="mt-2 text-xs text-ink-3">{t("По партиям: номер, срок годности и количество каждой. Пустые партии — позицию ещё не купили.")}</p>
                  <BatchDrafts
                    drafts={batches[line.requestItemId]}
                    shelfLifeDays={line.shelfLifeDays}
                    unitCode={line.unitCode}
                    onChange={(drafts) => setBatches((v) => ({ ...v, [line.requestItemId]: drafts }))}
                  />
                </>
              )}
            </li>
          );
        })}
      </ul>
      <label className="mb-3 block">
        <span className="mb-1.5 block text-sm font-medium text-ink">{t("Где купили")}</span>
        <input value={where} onChange={(e) => setWhere(e.target.value)} list="purchase-suppliers" maxLength={200} placeholder={t("Магазин или поставщик")} className={field} />
        <datalist id="purchase-suppliers">
          {suppliers.map((s) => (
            <option key={s.id} value={s.name} />
          ))}
        </datalist>
        <span className="mt-1 block text-xs text-ink-3">{t("Нового магазина нет в справочнике — он добавится в «Поставщики».")}</span>
      </label>
      <label className="mb-3 block">
        <span className="mb-1.5 block text-sm font-medium text-ink">
          {t("Номер чека или накладной")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
        </span>
        <input value={reference} onChange={(e) => setReference(e.target.value)} maxLength={100} className={field} />
      </label>
      <label className="block">
        <span className="mb-1.5 block text-sm font-medium text-ink">
          {t("Комментарий")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
        </span>
        <textarea
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          maxLength={1000}
          rows={2}
          className="w-full rounded-lg border border-line bg-surface px-3 py-2.5 text-sm text-ink focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20"
        />
      </label>
      <p className="mt-3 text-xs text-ink-3">{t("Купленное сразу поступит на склад и будет ждать выдачи сотруднику.")}</p>
      <div className="mt-3">
        <ErrorNote text={error} />
      </div>
    </Sheet>
  );
}
