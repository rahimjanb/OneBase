"use client";

import { useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { Loader2, Plus, X } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { batchExpiry, batchesTotal, parseNumber, quantity, type BatchDraft, type StockRow } from "@/lib/procurement";

const field =
  "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";

let seq = 0;
const draft = (quantity = ""): BatchDraft => ({ key: `e-${Date.now()}-${seq++}`, no: "", made: "", expires: "", quantity });
const newKey = () => (typeof crypto !== "undefined" && "randomUUID" in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`);

/**
 * «Ввести партии»: имеющийся остаток позиции раскладывается по партиям со сроком годности — на каждом складе, где он есть, сумма партий =
 * остаток склада (расхождение — сначала инвентаризация). Остаток не меняется, дальше позиция ведётся по партиям. Срок можно не указывать,
 * если задан срок хранения и известна дата производства. Срок совсем не известен — «срок не известен»: партия заблокирована, пока
 * назначенный по качеству не укажет срок.
 */
export function BatchEntrySheet({ item, stock, onClose }: { item: StockRow; stock: { warehouseId: string; name: string; onHand: number }[]; onClose: () => void }) {
  const t = useT();
  const router = useRouter();
  const [rows, setRows] = useState<Record<string, BatchDraft[]>>(() => Object.fromEntries(stock.map((w) => [w.warehouseId, [draft(String(w.onHand).replace(".", ","))]])));
  const [comment, setComment] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const clientKey = useRef(newKey());

  const patch = (warehouse: string, key: string, change: Partial<BatchDraft>) =>
    setRows((all) => ({ ...all, [warehouse]: all[warehouse].map((d) => (d.key === key ? { ...d, ...change } : d)) }));

  const save = async () => {
    setError(null);
    for (const w of stock) {
      const total = batchesTotal(rows[w.warehouseId]);
      if (total === null) return setError(t("«{name}»: укажите количество каждой партии.", { name: w.name }));
      if (total !== w.onHand) return setError(t("«{name}»: партии вместе — {a}, а на складе {b} — должно совпасть.", { name: w.name, a: quantity(total), b: quantity(w.onHand) }));
      if (rows[w.warehouseId].some((d) => !d.unknown && !batchExpiry(d.expires, d.made, item.shelfLifeDays)))
        return setError(t("«{name}»: укажите срок годности каждой партии — или отметьте «срок не известен».", { name: w.name }));
    }
    setBusy(true);
    try {
      await bff("procurement/stock/batch-entry", {
        method: "POST",
        body: JSON.stringify({
          itemId: item.itemId,
          warehouses: stock.map((w) => ({
            warehouseId: w.warehouseId,
            batches: rows[w.warehouseId].map((d) => ({
              supplierBatchNo: d.no.trim() || null,
              manufacturedOn: d.made || null,
              expiresOn: d.unknown ? null : d.expires || null,
              quantity: parseNumber(d.quantity),
              expiryUnknown: Boolean(d.unknown),
            })),
          })),
          comment: comment.trim() || null,
          clientKey: clientKey.current,
        }),
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
      title={t("Ввести партии")}
      subtitle={`${item.sku} · ${item.name}`}
      size="xl"
      footer={
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-10 items-center rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted">
            {t("Закрыть")}
          </button>
          <button
            type="button"
            onClick={() => void save()}
            disabled={busy}
            className="inline-flex h-10 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50"
          >
            {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}
            {t("Ввести партии")}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <p className="text-sm text-ink-2">
          {t("Разложите остаток по партиям со сроком годности. Остаток не изменится; дальше выдаётся только годное, сначала — с ближайшим сроком. Просроченную партию тоже введите — её можно будет списать.")}
        </p>
        <p className="text-xs text-ink-3">
          {t("Срок не известен — отметьте «срок не известен»: партия будет заблокирована, пока назначенный по качеству не укажет срок по документам или анализу.")}
        </p>
        {stock.map((w) => {
          const total = batchesTotal(rows[w.warehouseId]);
          return (
            <section key={w.warehouseId} className="rounded-xl border border-line p-3">
              <h3 className="mb-2 flex flex-wrap items-baseline justify-between gap-2 text-sm font-semibold text-ink">
                {w.name}
                <span className={`text-xs font-normal tabular-nums ${total === w.onHand ? "text-ok" : "text-warn"}`}>
                  {t("партии {a} из {b} {unit}", { a: total === null ? "—" : quantity(total), b: quantity(w.onHand), unit: item.unitCode })}
                </span>
              </h3>
              <div className="space-y-2">
                <div className="hidden gap-2 px-0.5 text-[11px] font-medium uppercase tracking-wide text-ink-3 sm:grid sm:grid-cols-[1fr_9.5rem_9.5rem_7rem_2.5rem]">
                  <span>{t("Номер партии")}</span>
                  <span>{t("Произведена")}</span>
                  <span>{t("Годна до")}</span>
                  <span>{t("Количество")}</span>
                </div>
                {rows[w.warehouseId].map((d, j) => (
                  <div key={d.key} className="grid grid-cols-2 gap-2 sm:grid-cols-[1fr_9.5rem_9.5rem_7rem_2.5rem]">
                    <input
                      value={d.no}
                      onChange={(e) => patch(w.warehouseId, d.key, { no: e.target.value })}
                      maxLength={64}
                      placeholder={t("Номер партии")}
                      aria-label={t("Номер партии {n}", { n: j + 1 })}
                      className={`${field} col-span-2 sm:col-span-1`}
                    />
                    <label className="block">
                      <span className="mb-0.5 block text-[11px] text-ink-3 sm:sr-only">{t("Дата производства")}</span>
                      <input type="date" value={d.made} onChange={(e) => patch(w.warehouseId, d.key, { made: e.target.value })} title={t("Дата производства")} className={field} />
                    </label>
                    <div>
                      <label className="block">
                        <span className="mb-0.5 block text-[11px] text-ink-3 sm:sr-only">{t("Годна до")}</span>
                        <input
                          type="date"
                          value={d.unknown ? "" : d.expires}
                          disabled={d.unknown}
                          onChange={(e) => patch(w.warehouseId, d.key, { expires: e.target.value })}
                          title={t("Годна до")}
                          className={`${field} disabled:bg-muted disabled:text-ink-3`}
                        />
                      </label>
                      <label className="mt-1 flex items-center gap-1.5 text-[11px] text-ink-3">
                        <input type="checkbox" checked={Boolean(d.unknown)} onChange={(e) => patch(w.warehouseId, d.key, { unknown: e.target.checked })} className="size-3.5 accent-accent" />
                        {t("срок не известен")}
                      </label>
                    </div>
                    <input
                      value={d.quantity}
                      onChange={(e) => patch(w.warehouseId, d.key, { quantity: e.target.value })}
                      inputMode="decimal"
                      placeholder={t("Количество")}
                      aria-label={t("Количество партии {n}", { n: j + 1 })}
                      className={`${field} tabular-nums`}
                    />
                    <button
                      type="button"
                      onClick={() => setRows((all) => ({ ...all, [w.warehouseId]: all[w.warehouseId].length === 1 ? [draft()] : all[w.warehouseId].filter((x) => x.key !== d.key) }))}
                      aria-label={t("Убрать партию {n}", { n: j + 1 })}
                      className="grid size-10 place-items-center rounded-md text-ink-3 hover:text-bad"
                    >
                      <X className="size-4" aria-hidden />
                    </button>
                  </div>
                ))}
                <button
                  type="button"
                  onClick={() => setRows((all) => ({ ...all, [w.warehouseId]: [...all[w.warehouseId], draft()] }))}
                  className="inline-flex h-8 items-center gap-1.5 rounded-lg px-2 text-xs font-semibold text-accent-strong hover:bg-accent-soft"
                >
                  <Plus className="size-3.5" aria-hidden /> {t("Ещё партия")}
                </button>
              </div>
            </section>
          );
        })}
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">
            {t("Комментарий")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
          </span>
          <textarea value={comment} onChange={(e) => setComment(e.target.value)} maxLength={1000} rows={2} className={`${field} h-auto py-2.5`} />
        </label>
        <ErrorNote text={error} />
      </div>
    </Sheet>
  );
}
