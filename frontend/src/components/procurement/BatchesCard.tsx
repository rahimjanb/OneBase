"use client";

import { useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Ban, Loader2, LockOpen, ShieldAlert, ShieldCheck, Undo2 } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { DocumentsBlock } from "@/components/procurement/ProcKit";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import {
  batchQualityLabel,
  batchQualityTone,
  batchStateLabel,
  batchTone,
  dateRu,
  money,
  movementKindLabel,
  parseNumber,
  qualityCheckStatusLabel,
  quantity,
  stampRu,
  type BatchCard,
} from "@/lib/procurement";

const field =
  "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
const newKey = () => (typeof crypto !== "undefined" && "randomUUID" in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`);

/**
 * Карточка партии: статус качества (и почему заблокирована), плитки (остаток, годна до, произведена, пришла), данные партии, остаток по
 * складам, проверки качества, история решений, документы (сертификаты), движения по всем складам. Назначенный по качеству блокирует и
 * снимает блокировку, кладовщик возвращает забракованную или заблокированную партию поставщику.
 */
export function BatchCardView({ card }: { card: BatchCard }) {
  const t = useT();
  const b = card.batch;
  const [sheet, setSheet] = useState<"block" | "unblock" | "return" | null>(null);
  const tiles = [
    { label: "Остаток", value: `${quantity(b.onHand)} ${b.unitCode}` },
    { label: b.state === "ok" ? "Годна до" : batchStateLabel[b.state], value: b.expiresOn ? dateRu(b.expiresOn) : t("не указан"), tone: batchTone(b.state) },
    { label: "Произведена", value: dateRu(b.manufacturedOn) },
    { label: "Пришла", value: dateRu(b.receivedOn) },
  ];
  const facts = [
    { label: "Номер партии производителя", value: b.supplierBatchNo },
    { label: "Поставщик", value: b.supplierName },
    { label: "Цена прихода", value: b.unitCost === null ? null : `${money(b.unitCost)} ${t("сум")}` },
    { label: "Условия хранения", value: card.storageConditions },
    { label: "Появилась документом", value: card.sourceNumber },
  ].filter((f) => f.value);
  const can = card.can;
  const held = b.quality !== "Approved";

  return (
    <div className="space-y-5">
      <div className={`flex flex-wrap items-center gap-3 rounded-xl border p-4 ${held ? "border-bad/30 bg-bad-soft/40" : "border-line bg-surface"}`}>
        <span className={`inline-flex items-center gap-1.5 rounded-md px-2 py-1 text-sm font-semibold ${batchQualityTone[b.quality]}`}>
          {held ? <ShieldAlert className="size-4" aria-hidden /> : <ShieldCheck className="size-4" aria-hidden />}
          {t(batchQualityLabel[b.quality])}
        </span>
        <span className="min-w-0 flex-1 text-sm text-ink-2">
          {b.quality === "Pending"
            ? t("Ждёт решения по качеству — до него не выдаётся (переместить и списать можно).")
            : b.quality === "Rejected"
              ? t("Не выдаётся — верните поставщику или спишите.")
              : b.quality === "Blocked"
                ? `${t("Не выдаётся до снятия блокировки")}${b.blockReason ? `: ${t(b.blockReason)}` : ""}.`
                : b.usable
                  ? t("Выдаётся, пока не истёк срок годности.")
                  : t("Просрочена — не выдаётся, её можно списать или переместить.")}
          {card.decidedByName && card.decidedAt && <span className="block text-xs text-ink-3">{`${card.decidedByName} · ${stampRu(card.decidedAt)}`}</span>}
        </span>
        <div className="flex flex-wrap gap-2">
          {can?.block && (
            <button type="button" onClick={() => setSheet("block")} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-line bg-surface px-3 text-sm font-medium text-bad hover:bg-bad-soft">
              <Ban className="size-4" aria-hidden /> {t("Заблокировать")}
            </button>
          )}
          {can?.unblock && (
            <button type="button" onClick={() => setSheet("unblock")} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-line bg-surface px-3 text-sm font-medium text-ink hover:bg-muted">
              <LockOpen className="size-4" aria-hidden /> {t("Снять блокировку")}
            </button>
          )}
          {can?.return && (
            <button type="button" onClick={() => setSheet("return")} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-line bg-surface px-3 text-sm font-medium text-ink hover:bg-muted">
              <Undo2 className="size-4" aria-hidden /> {t("Вернуть поставщику")}
            </button>
          )}
        </div>
      </div>

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {tiles.map((x) => (
          <div key={x.label} className="rounded-xl border border-line bg-surface p-4">
            <div className={`text-xl font-semibold tabular-nums text-ink ${x.tone ?? ""}`}>{x.value}</div>
            <div className="mt-0.5 text-xs text-ink-3">{t(x.label)}</div>
          </div>
        ))}
      </div>

      <div className="grid gap-5 lg:grid-cols-[1fr_2fr]">
        <div className="space-y-5">
          {facts.length > 0 && (
            <section className="rounded-xl border border-line bg-surface p-4">
              <dl className="space-y-2 text-sm">
                {facts.map((f) => (
                  <div key={f.label}>
                    <dt className="text-xs text-ink-3">{t(f.label)}</dt>
                    <dd className="text-ink">{f.value}</dd>
                  </div>
                ))}
              </dl>
            </section>
          )}
          <section>
            <h2 className="mb-2 text-sm font-semibold text-ink">{t("По складам")}</h2>
            <ul className="divide-y divide-line rounded-xl border border-line bg-surface">
              {card.warehouses.map((w) => (
                <li key={w.warehouseId} className="flex items-center justify-between gap-3 px-3 py-2 text-sm">
                  <span className={w.onHand > 0 ? "text-ink" : "text-ink-3"}>{w.name}</span>
                  <span className="tabular-nums text-ink">
                    {quantity(w.onHand)} {b.unitCode}
                  </span>
                </li>
              ))}
            </ul>
          </section>
          {(card.checks?.length ?? 0) > 0 && (
            <section>
              <h2 className="mb-2 text-sm font-semibold text-ink">{t("Проверки качества")}</h2>
              <ul className="divide-y divide-line rounded-xl border border-line bg-surface">
                {card.checks!.map((c) => (
                  <li key={c.id}>
                    <Link href={`/production/quality/${c.id}`} className="flex items-center justify-between gap-3 px-3 py-2 text-sm hover:bg-muted/60">
                      <span className="min-w-0">
                        <span className="block font-medium text-ink">{c.number}</span>
                        <span className="block text-xs text-ink-3">{[stampRu(c.createdAt), c.decidedByName].filter(Boolean).join(" · ")}</span>
                      </span>
                      <span className={`shrink-0 rounded-md px-1.5 py-0.5 text-[11px] font-semibold ${c.decision ? batchQualityTone[c.decision] : "bg-warn-soft text-warn"}`}>
                        {c.decision ? t(batchQualityLabel[c.decision]) : t(qualityCheckStatusLabel[c.status])}
                      </span>
                    </Link>
                  </li>
                ))}
              </ul>
            </section>
          )}
          {(card.history?.length ?? 0) > 0 && (
            <section>
              <h2 className="mb-2 text-sm font-semibold text-ink">{t("Решения по качеству")}</h2>
              <ul className="space-y-2 rounded-xl border border-line bg-surface p-3 text-sm">
                {card.history!.map((h) => (
                  <li key={h.id}>
                    <span className="block text-ink">{t(h.text)}</span>
                    <span className="block text-xs text-ink-3">{[h.actorName, stampRu(h.createdAt), h.comment].filter(Boolean).join(" · ")}</span>
                  </li>
                ))}
              </ul>
            </section>
          )}
          <section>
            <h2 className="mb-2 text-sm font-semibold text-ink">{t("Документы партии")}</h2>
            <div className="rounded-xl border border-line bg-surface p-3">
              <DocumentsBlock owner="batch" id={b.id} documents={card.documents ?? []} can={Boolean(can?.attach)} kinds={["certificate", "declaration", "lab_report", "other"]} />
            </div>
          </section>
        </div>

        <section>
          <h2 className="mb-2 text-sm font-semibold text-ink">{t("Движения партии")}</h2>
          <ul className="divide-y divide-line overflow-hidden rounded-xl border border-line bg-surface">
            {card.movements.map((m) => {
              const change = m.batchAfter === null ? m.quantity : m.kind === "BatchEntry" ? m.batchAfter : m.quantity;
              return (
                <li key={m.id} className="flex items-center gap-3 px-3 py-2.5 text-sm">
                  <span className="min-w-0 flex-1">
                    <span className="block text-ink">
                      {t(movementKindLabel[m.kind])} · <span className="tabular-nums text-ink-3">{m.documentNumber}</span>
                    </span>
                    <span className="block truncate text-xs text-ink-3">
                      {[stampRu(m.createdAt), m.warehouseName, m.counterpartName && `${m.kind === "TransferOut" ? "→" : "←"} ${m.counterpartName}`, m.departmentName, m.actorName, m.comment]
                        .filter(Boolean)
                        .join(" · ")}
                    </span>
                  </span>
                  <span className={`shrink-0 text-right tabular-nums ${change > 0 ? "text-ok" : change < 0 ? "text-bad" : "text-ink-3"}`}>
                    {change > 0 ? "+" : ""}
                    {quantity(change)}
                    {m.batchAfter !== null && <span className="block text-[11px] text-ink-3">→ {quantity(m.batchAfter)}</span>}
                  </span>
                </li>
              );
            })}
          </ul>
        </section>
      </div>

      {(sheet === "block" || sheet === "unblock") && <BlockSheet card={card} unblock={sheet === "unblock"} onClose={() => setSheet(null)} />}
      {sheet === "return" && <SupplierReturnSheet card={card} onClose={() => setSheet(null)} />}
    </div>
  );
}

/** Заблокировать (жалоба, отзыв) или снять блокировку — назначенный по качеству, причина обязательна; у партии «срок не указан» — сначала срок. */
function BlockSheet({ card, unblock, onClose }: { card: BatchCard; unblock: boolean; onClose: () => void }) {
  const t = useT();
  const router = useRouter();
  const b = card.batch;
  const [reason, setReason] = useState("");
  const [expires, setExpires] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const needsDate = unblock && !b.expiresOn;

  const save = async () => {
    setError(null);
    if (!reason.trim()) return setError(t(unblock ? "Укажите, почему блокировка снята." : "Укажите причину блокировки."));
    if (needsDate && !expires) return setError(t("Укажите срок годности партии — без него её не выдать."));
    setBusy(true);
    try {
      await bff(`quality/batches/${b.id}/${unblock ? "unblock" : "block"}`, {
        method: "POST",
        body: JSON.stringify({ reason: reason.trim(), expiresOn: needsDate ? expires : null, version: card.version }),
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
      title={t(unblock ? "Снять блокировку" : "Заблокировать партию")}
      subtitle={`${b.number} · ${b.itemName}`}
      footer={
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-10 items-center rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted">
            {t("Закрыть")}
          </button>
          <button
            type="button"
            onClick={() => void save()}
            disabled={busy}
            className={`inline-flex h-10 items-center gap-2 rounded-lg px-4 text-sm font-semibold text-white disabled:opacity-50 ${unblock ? "bg-accent hover:bg-accent-strong" : "bg-bad hover:bg-bad/90"}`}
          >
            {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}
            {t(unblock ? "Снять блокировку" : "Заблокировать")}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <p className="text-sm text-ink-2">
          {t(
            unblock
              ? "Партия снова станет допущенной и будет выдаваться, пока не истечёт срок годности."
              : "Заблокированную партию не выдают и не резервируют под заявки, пока блокировку не снимут. Переместить, списать и вернуть поставщику её можно.",
          )}
        </p>
        {needsDate && (
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Годна до")}</span>
            <input type="date" value={expires} onChange={(e) => setExpires(e.target.value)} className={field} />
            <span className="mt-1 block text-xs text-ink-3">{t("Срок не был указан при вводе остатков — укажите его по документам или анализу.")}</span>
          </label>
        )}
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Причина")}</span>
          <textarea value={reason} onChange={(e) => setReason(e.target.value)} maxLength={1000} rows={3} className={`${field} h-auto py-2.5`} />
        </label>
        <ErrorNote text={error} />
      </div>
    </Sheet>
  );
}

/** Возврат поставщику (кладовщик): забракованная или заблокированная партия со склада, количество, причина, номер документа. */
function SupplierReturnSheet({ card, onClose }: { card: BatchCard; onClose: () => void }) {
  const t = useT();
  const router = useRouter();
  const b = card.batch;
  const stocked = card.warehouses.filter((w) => w.onHand > 0);
  const [warehouse, setWarehouse] = useState(stocked[0]?.warehouseId ?? "");
  const [qty, setQty] = useState(String(stocked[0]?.onHand ?? "").replace(".", ","));
  const [reason, setReason] = useState(b.quality === "Rejected" ? t("Брак по контролю качества") : "");
  const [reference, setReference] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const clientKey = useRef(newKey());
  const onHand = stocked.find((w) => w.warehouseId === warehouse)?.onHand ?? 0;

  const save = async () => {
    setError(null);
    const value = parseNumber(qty);
    if (value === null || value <= 0) return setError(t("Укажите количество."));
    if (value > onHand) return setError(t("На складе {n} {unit} — больше не вернуть.", { n: quantity(onHand), unit: b.unitCode }));
    if (!reason.trim()) return setError(t("Укажите причину возврата поставщику."));
    setBusy(true);
    try {
      await bff("procurement/stock/supplier-return", {
        method: "POST",
        body: JSON.stringify({
          warehouseId: warehouse,
          supplierId: card.supplierId,
          lines: [{ itemId: b.itemId, quantity: value, batchId: b.id }],
          comment: reason.trim(),
          reference: reference.trim() || null,
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
      title={t("Вернуть поставщику")}
      subtitle={`${b.number} · ${b.itemName}${b.supplierName ? ` · ${b.supplierName}` : ""}`}
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
            {t("Вернуть")}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        {stocked.length > 1 && (
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Склад")}</span>
            <select value={warehouse} onChange={(e) => setWarehouse(e.target.value)} className={field}>
              {stocked.map((w) => (
                <option key={w.warehouseId} value={w.warehouseId}>
                  {`${w.name} · ${quantity(w.onHand)} ${b.unitCode}`}
                </option>
              ))}
            </select>
          </label>
        )}
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Количество, {unit}", { unit: b.unitCode })}</span>
          <input value={qty} onChange={(e) => setQty(e.target.value)} inputMode="decimal" className={`${field} tabular-nums`} />
        </label>
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Причина")}</span>
          <textarea value={reason} onChange={(e) => setReason(e.target.value)} maxLength={1000} rows={2} className={`${field} h-auto py-2.5`} />
        </label>
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">
            {t("Номер документа")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
          </span>
          <input value={reference} onChange={(e) => setReference(e.target.value)} maxLength={100} placeholder={t("Акт возврата, накладная")} className={field} />
        </label>
        <ErrorNote text={error} />
      </div>
    </Sheet>
  );
}
