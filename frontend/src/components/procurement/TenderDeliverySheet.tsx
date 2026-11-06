"use client";

import { useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { Loader2 } from "lucide-react";
import { BatchDrafts, newBatchDraft } from "@/components/procurement/BatchDrafts";
import { Sheet } from "@/components/field/Sheet";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { batchExpiry, batchesTotal, parseNumber, quantity, unitToBase, type BatchDraft, type TenderDetail, type TenderItem, type Warehouse } from "@/lib/procurement";

const field =
  "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
const newKey = () => (typeof crypto !== "undefined" && "randomUUID" in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`);
const text = (n: number) => String(n).replace(".", ",");

/** Строка приёма: количество в единице позиции тендера; у позиции с партиями — партии (их сумма — количество). */
type Row = { quantity: string; batches: BatchDraft[] };

/** Позиции тендера, которые можно принять на склад: связаны с карточкой, единица пересчитывается, ещё не всё поставлено. */
export const receivable = (items: TenderItem[]) => items.filter((i) => i.itemId && i.factor != null && (i.left ?? 0) > 0);

/**
 * «Принять на склад» по тендеру — кладовщик: что пришло по каждой позиции (в единице тендера; на склад — в единице карточки), партии со
 * сроком годности (у позиции с партиями; на контроль качества, если он у позиции включён), склад, номер накладной. Приход RC- сторно не
 * отменяется: «поставлено» по тендеру растёт; больше остатка позиции не принять. Повторное нажатие не проводит приход второй раз.
 */
export function TenderDeliverySheet({ tender, warehouses, onClose }: { tender: TenderDetail; warehouses: Warehouse[]; onClose: () => void }) {
  const t = useT();
  const router = useRouter();
  const items = receivable(tender.items);
  const posts = warehouses.filter((w) => w.isActive && w.posts);
  const initialWarehouse = posts.find((w) => w.id === tender.warehouseId)?.id ?? posts.find((w) => w.isDefault)?.id ?? posts[0]?.id ?? "";
  const [rows, setRows] = useState<Record<string, Row>>(() =>
    Object.fromEntries(items.map((i) => [i.id, { quantity: text(i.left ?? 0), batches: [newBatchDraft(text(i.left ?? 0))] }])),
  );
  const [warehouse, setWarehouse] = useState(initialWarehouse);
  const [reference, setReference] = useState("");
  const [comment, setComment] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const clientKey = useRef(newKey());

  const patch = (id: string, change: Partial<Row>) => setRows((all) => ({ ...all, [id]: { ...all[id], ...change } }));
  const amount = (i: TenderItem) => (i.tracksBatches ? batchesTotal(rows[i.id].batches) : parseNumber(rows[i.id].quantity));

  const save = async () => {
    setError(null);
    if (!warehouse) return setError(t("Нет склада, по которому вы проводите операции."));
    const lines = [];
    for (const i of items) {
      const row = rows[i.id];
      const typed = i.tracksBatches ? row.batches.some((b) => b.quantity.trim()) : row.quantity.trim() !== "";
      if (!typed) continue;
      const q = amount(i);
      if (q === null || q <= 0)
        return setError(i.tracksBatches ? t("«{name}»: укажите количество каждой партии.", { name: i.name }) : t("«{name}»: укажите количество.", { name: i.name }));
      if (q > (i.left ?? 0)) return setError(t("«{name}»: осталось поставить {n} — больше не принять.", { name: i.name, n: `${quantity(i.left ?? 0)} ${i.unitCode}` }));
      if (i.factor !== 1 && unitToBase(q, i.factor!) === null)
        return setError(t("«{name}»: в {unit} получается больше трёх знаков — уточните количество.", { name: i.name, unit: i.itemUnitCode ?? "" }));
      if (i.tracksBatches && row.batches.some((b) => !batchExpiry(b.expires, b.made, i.shelfLifeDays)))
        return setError(t("«{name}»: укажите срок годности каждой партии.", { name: i.name }));
      lines.push({
        tenderItemId: i.id,
        quantity: q,
        batches: i.tracksBatches
          ? row.batches.map((b) => ({ supplierBatchNo: b.no.trim() || null, manufacturedOn: b.made || null, expiresOn: b.expires || null, quantity: parseNumber(b.quantity) }))
          : null,
      });
    }
    if (lines.length === 0) return setError(t("Укажите, что пришло, хотя бы по одной позиции."));
    setBusy(true);
    try {
      await bff(`procurement/tenders/${tender.id}/delivery`, {
        method: "POST",
        body: JSON.stringify({ lines, warehouseId: warehouse, reference: reference.trim() || null, comment: comment.trim() || null, clientKey: clientKey.current }),
      });
      onClose();
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Sheet
      open
      onClose={onClose}
      title={t("Принять на склад")}
      subtitle={`${tender.number} · ${tender.title} · ${tender.supplierName}`}
      size="xl"
      footer={
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-10 items-center rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted">
            {t("Закрыть")}
          </button>
          <button
            type="button"
            onClick={() => void save()}
            disabled={busy || items.length === 0}
            className="inline-flex h-10 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50"
          >
            {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}
            {t("Провести приход")}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <p className="text-sm text-ink-2">
          {t("Укажите, что пришло. Позиции с контролем качества встанут на контроль — выдать их можно после допуска. Приход по тендеру сторно не отменяется.")}
        </p>
        <div className="grid gap-3 sm:grid-cols-2">
          {posts.length > 1 && (
            <label className="block">
              <span className="mb-1.5 block text-sm font-medium text-ink">{t("Склад")}</span>
              <select value={warehouse} onChange={(e) => setWarehouse(e.target.value)} className={field}>
                {posts.map((w) => (
                  <option key={w.id} value={w.id}>
                    {w.name}
                  </option>
                ))}
              </select>
            </label>
          )}
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Номер накладной")}</span>
            <input value={reference} onChange={(e) => setReference(e.target.value)} maxLength={100} className={field} />
          </label>
        </div>
        <ul className="space-y-3">
          {items.map((i) => {
            const q = amount(i);
            const inBase = q && i.factor && i.factor !== 1 ? unitToBase(q, i.factor) : null;
            return (
              <li key={i.id} className="rounded-xl border border-line p-3">
                <div className="flex flex-wrap items-start gap-3">
                  <div className="min-w-0 flex-1">
                    <p className="text-sm font-medium text-ink">
                      {i.name} <span className="tabular-nums text-ink-3">{i.sku}</span>
                    </p>
                    <p className="mt-0.5 text-xs text-ink-3">
                      {t("по тендеру {a} · поставлено {b} · осталось {c}", {
                        a: `${quantity(i.quantity)} ${i.unitCode}`,
                        b: quantity(i.receivedQty ?? 0),
                        c: quantity(i.left ?? 0),
                      })}
                      {i.factor !== 1 && ` · ${t("1 {a} = {f} {b}", { a: i.unitCode, f: quantity(i.factor ?? 1), b: i.itemUnitCode ?? "" })}`}
                    </p>
                    {inBase !== null && <p className="mt-0.5 text-xs text-accent-strong">{t("на склад: {q}", { q: `${quantity(inBase)} ${i.itemUnitCode}` })}</p>}
                  </div>
                  {i.tracksBatches ? (
                    <span className="flex h-10 items-center text-sm tabular-nums text-ink-2">{q === null ? "—" : `${quantity(q)} ${i.unitCode}`}</span>
                  ) : (
                    <input
                      value={rows[i.id].quantity}
                      onChange={(e) => patch(i.id, { quantity: e.target.value })}
                      inputMode="decimal"
                      placeholder={t("Пришло")}
                      aria-label={t("Пришло: {name}, {unit}", { name: i.name, unit: i.unitCode })}
                      className={`${field} w-32 text-right tabular-nums`}
                    />
                  )}
                </div>
                {i.tracksBatches && <BatchDrafts drafts={rows[i.id].batches} shelfLifeDays={i.shelfLifeDays} unitCode={i.unitCode} onChange={(batches) => patch(i.id, { batches })} />}
              </li>
            );
          })}
        </ul>
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">
            {t("Комментарий")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
          </span>
          <textarea
            value={comment}
            onChange={(e) => setComment(e.target.value)}
            maxLength={2000}
            rows={2}
            className="w-full rounded-lg border border-line bg-surface px-3 py-2.5 text-sm text-ink focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20"
          />
        </label>
        <ErrorNote text={error} />
      </div>
    </Sheet>
  );
}
