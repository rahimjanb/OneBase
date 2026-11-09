"use client";

import { useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { FlaskConical, Loader2, Truck } from "lucide-react";
import { BatchDrafts, newBatchDraft } from "@/components/procurement/BatchDrafts";
import { Sheet } from "@/components/field/Sheet";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import type { ExpectedDelivery, ExpectedItem } from "@/lib/arrivals";
import { bff } from "@/lib/bff";
import { batchExpiry, batchesTotal, parseNumber, qualityControlLabel, quantity, unitToBase, type BatchDraft } from "@/lib/procurement";

const field =
  "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
const newKey = () => (typeof crypto !== "undefined" && "randomUUID" in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`);
const text = (n: number) => String(n).replace(".", ",");

/** Строка приезда: сколько по накладной (в единице тендера); у позиции с партиями — партии со сроком (их сумма — по накладной). */
type Row = { quantity: string; batches: BatchDraft[] };

/** Позиции, по которым можно отметить приезд: связаны с карточкой склада, единица пересчитывается, ещё осталось поставить. */
export const arrivable = (items: ExpectedItem[]) => items.filter((i) => i.itemId && i.factor != null && i.left > 0);

/**
 * «Машина приехала» — кладовщик: госномер, водитель, накладная и что привезли по каждой позиции тендера (по накладной; у позиции с партиями —
 * партии поставщика со сроком годности, по строке на партию). Склад не меняется: позиции с контролем качества сначала проверит лаборатория,
 * потом кладовщик взвесит и примет. Повторное нажатие не заводит вторую поставку.
 */
export function ArrivalSheet({ delivery, onClose }: { delivery: ExpectedDelivery; onClose: () => void }) {
  const t = useT();
  const router = useRouter();
  const items = arrivable(delivery.items);
  const [rows, setRows] = useState<Record<string, Row>>(() =>
    Object.fromEntries(items.map((i) => [i.lineId, { quantity: text(i.left), batches: [newBatchDraft(text(i.left))] }])),
  );
  const [vehicle, setVehicle] = useState("");
  const [driver, setDriver] = useState("");
  const [reference, setReference] = useState("");
  const [comment, setComment] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const clientKey = useRef(newKey());
  const checked = items.filter((i) => i.control !== "None");

  const patch = (id: string, change: Partial<Row>) => setRows((all) => ({ ...all, [id]: { ...all[id], ...change } }));
  const amount = (i: ExpectedItem) => (i.tracksBatches ? batchesTotal(rows[i.lineId].batches) : parseNumber(rows[i.lineId].quantity));

  const save = async () => {
    setError(null);
    const lines = [];
    for (const i of items) {
      const row = rows[i.lineId];
      const typed = i.tracksBatches ? row.batches.some((b) => b.quantity.trim()) : row.quantity.trim() !== "";
      if (!typed) continue;
      const q = amount(i);
      if (q === null || q <= 0)
        return setError(i.tracksBatches ? t("«{name}»: укажите количество каждой партии.", { name: i.name }) : t("«{name}»: укажите количество.", { name: i.name }));
      if (q > i.left) return setError(t("«{name}»: осталось поставить {n} — больше не принять.", { name: i.name, n: `${quantity(i.left)} ${t(i.unitCode)}` }));
      if (i.factor !== 1 && unitToBase(q, i.factor!) === null)
        return setError(t("«{name}»: в {unit} получается больше трёх знаков — уточните количество.", { name: i.name, unit: t(i.itemUnitCode ?? "") }));
      if (i.tracksBatches) {
        if (row.batches.some((b) => !batchExpiry(b.expires, b.made, i.shelfLifeDays))) return setError(t("«{name}»: укажите срок годности каждой партии.", { name: i.name }));
        for (const b of row.batches)
          lines.push({ lineId: i.lineId, declared: parseNumber(b.quantity), supplierBatchNo: b.no.trim() || null, manufacturedOn: b.made || null, expiresOn: b.expires || null });
      } else {
        lines.push({ lineId: i.lineId, declared: q });
      }
    }
    if (lines.length === 0) return setError(t("Укажите, что привезли, хотя бы по одной позиции."));
    setBusy(true);
    try {
      const { id } = await bff<{ id: string }>("procurement/arrivals", {
        method: "POST",
        body: JSON.stringify({
          tenderId: delivery.kind === "tender" ? delivery.sourceId : null,
          orderId: delivery.kind === "order" ? delivery.sourceId : null,
          lines,
          warehouseId: delivery.warehouseId,
          vehicleNo: vehicle.trim() || null,
          driverName: driver.trim() || null,
          reference: reference.trim() || null,
          comment: comment.trim() || null,
          clientKey: clientKey.current,
        }),
      });
      router.push(`/procurement/arrivals/${id}`);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setBusy(false);
    }
  };

  return (
    <Sheet
      open
      onClose={onClose}
      title={t("Машина приехала")}
      subtitle={`${delivery.number} · ${delivery.supplierName} · ${delivery.warehouseName}`}
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
            {busy ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Truck className="size-4" aria-hidden />}
            {checked.length > 0 ? t("Отправить на проверку качества") : t("Отметить приезд")}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <p className="text-sm text-ink-2">
          {checked.length > 0
            ? t("Запишите, что пришло по накладной. На склад пока ничего не приходуется: сначала лаборатория проверит качество, потом вы взвесите и примете.")
            : t("Запишите, что пришло по накладной. Контроль качества этим позициям не нужен — после отметки взвесьте и примите на склад.")}
        </p>
        <div className="grid gap-3 sm:grid-cols-3">
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Госномер машины")}</span>
            <input value={vehicle} onChange={(e) => setVehicle(e.target.value)} maxLength={32} placeholder="01 A 123 BC" autoFocus className={field} />
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Водитель")}</span>
            <input value={driver} onChange={(e) => setDriver(e.target.value)} maxLength={200} className={field} />
          </label>
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
              <li key={i.lineId} className="rounded-xl border border-line p-3">
                <div className="flex flex-wrap items-start gap-3">
                  <div className="min-w-0 flex-1">
                    <p className="text-sm font-medium text-ink">
                      {i.name} <span className="tabular-nums text-ink-3">{i.sku}</span>
                    </p>
                    <p className="mt-0.5 text-xs text-ink-3">
                      {t("по тендеру {a} · поставлено {b} · осталось {c}", { a: `${quantity(i.quantity)} ${t(i.unitCode)}`, b: quantity(i.received), c: quantity(i.left) })}
                      {i.onArrival > 0 && ` · ${t("на приёмке {n}", { n: quantity(i.onArrival) })}`}
                      {i.factor !== 1 && ` · ${t("1 {a} = {f} {b}", { a: t(i.unitCode), f: quantity(i.factor ?? 1), b: t(i.itemUnitCode ?? "") })}`}
                    </p>
                    <p className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs">
                      {i.control !== "None" ? (
                        <span className="inline-flex items-center gap-1 text-warn">
                          <FlaskConical className="size-3.5" aria-hidden /> {t("проверит лаборатория: {what}", { what: t(qualityControlLabel[i.control]).toLowerCase() })}
                        </span>
                      ) : (
                        <span className="text-ink-3">{t("без контроля качества")}</span>
                      )}
                      {inBase !== null && <span className="text-ink-2">{t("по накладной ≈ {q}", { q: `${quantity(inBase)} ${t(i.itemUnitCode ?? "")}` })}</span>}
                    </p>
                  </div>
                  {i.tracksBatches ? (
                    <span className="flex h-10 items-center text-sm tabular-nums text-ink-2">{q === null ? "—" : `${quantity(q)} ${t(i.unitCode)}`}</span>
                  ) : (
                    <input
                      value={rows[i.lineId].quantity}
                      onChange={(e) => patch(i.lineId, { quantity: e.target.value })}
                      inputMode="decimal"
                      placeholder={t("По накладной")}
                      aria-label={t("По накладной: {name}, {unit}", { name: i.name, unit: t(i.unitCode) })}
                      className={`${field} w-32 text-right tabular-nums`}
                    />
                  )}
                </div>
                {i.tracksBatches && (
                  <BatchDrafts drafts={rows[i.lineId].batches} shelfLifeDays={i.shelfLifeDays} unitCode={i.unitCode} onChange={(batches) => patch(i.lineId, { batches })} />
                )}
              </li>
            );
          })}
        </ul>
        {items.length === 0 && <p className="rounded-lg bg-muted px-3 py-2 text-sm text-ink-2">{t("По тендеру всё уже привезли или приняли — отмечать нечего.")}</p>}
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
        <ErrorNote text={error} />
      </div>
    </Sheet>
  );
}
