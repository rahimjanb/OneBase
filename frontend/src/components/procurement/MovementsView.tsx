"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { ChevronLeft, ChevronRight, Loader2, Search, Undo2 } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { dateRu, movementKindLabel, quantity, reversible, stampRu, type MovementKind, type MovementRow, type Page, type Warehouse } from "@/lib/procurement";

const field =
  "h-10 rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
const kinds: MovementKind[] = ["Receipt", "Issue", "WriteOff", "Adjustment", "TransferOut", "TransferIn", "BatchEntry", "Reversal", "SupplierReturn"];

/**
 * Журнал движений склада: вид, склад (когда складов больше одного), даты, поиск по артикулу, названию и номеру документа; до/после по
 * каждой строке — остаток склада движения, у перемещения — куда ушло или откуда пришло; партия строки. Журнал не правится: ошибку исправляет
 * сторно (кладовщик) — зеркальный документ.
 */
export function MovementsView({ data, warehouses, canReverse = false }: { data: Page<MovementRow>; warehouses: Warehouse[]; canReverse?: boolean }) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [search, setSearch] = useState(params.get("q") ?? "");
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
  const several = warehouses.length > 1;
  const [reversing, setReversing] = useState<MovementRow | null>(null);

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center gap-2">
        <label className="relative block min-w-0 flex-1 sm:max-w-[320px]">
          <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" aria-hidden />
          <span className="sr-only">{t("Поиск")}</span>
          <input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("Артикул, название, номер документа")} className={`${field} w-full pl-9`} />
        </label>
        {several && (
          <select value={params.get("warehouse") ?? ""} onChange={(e) => apply({ warehouse: e.target.value || null })} aria-label={t("Склад")} className={field}>
            <option value="">{t("Все склады")}</option>
            {warehouses.map((w) => (
              <option key={w.id} value={w.id}>
                {w.isActive ? w.name : t("{name} (отключён)", { name: w.name })}
              </option>
            ))}
          </select>
        )}
        <select value={params.get("kind") ?? ""} onChange={(e) => apply({ kind: e.target.value || null })} aria-label={t("Вид движения")} className={field}>
          <option value="">{t("Все движения")}</option>
          {kinds.map((k) => (
            <option key={k} value={k}>
              {t(movementKindLabel[k])}
            </option>
          ))}
        </select>
        <input type="date" value={params.get("from") ?? ""} onChange={(e) => apply({ from: e.target.value || null })} aria-label={t("С")} className={field} />
        <input type="date" value={params.get("to") ?? ""} onChange={(e) => apply({ to: e.target.value || null })} aria-label={t("По")} className={field} />
      </div>
      {data.items.length === 0 ? (
        <p className="rounded-xl border border-dashed border-line bg-surface px-4 py-10 text-center text-sm text-ink-3">{t("Движений по этому фильтру нет.")}</p>
      ) : (
        <ul className="divide-y divide-line overflow-hidden rounded-xl border border-line bg-surface">
          {data.items.map((m) => (
            <li key={m.id} className="flex items-center gap-3 px-4 py-3 text-sm">
              <span className="min-w-0 flex-1">
                <span className="block text-ink">
                  <span className="font-medium">{m.itemName}</span> <span className="tabular-nums text-ink-3">{m.sku}</span>
                </span>
                <span className="block truncate text-xs text-ink-3">
                  {t(movementKindLabel[m.kind])} · <span className="tabular-nums">{m.documentNumber}</span> · {stampRu(m.createdAt)}
                  {several && m.warehouseName && ` · ${m.warehouseName}`}
                  {m.counterpartName && ` · ${m.kind === "TransferOut" ? "→" : "←"} ${m.counterpartName}`}
                  {m.batchNumber && ` · ${t("партия {n}, до {date}", { n: m.batchNumber, date: dateRu(m.batchExpiresOn) })}`}

                  {m.actorName && ` · ${m.actorName}`}
                  {m.departmentName && ` · ${m.departmentName}`}
                  {m.receiver && ` · ${t("получил")}: ${m.receiver}`}
                  {m.reference && ` · ${m.reference}`}
                  {m.comment && ` · ${m.comment}`}
                </span>
                {m.requestId && (
                  <Link href={`/requests/${m.requestId}`} className="text-xs text-accent-strong hover:underline">
                    {t("заявка {n}", { n: m.requestNumber ?? "" })}
                  </Link>
                )}
                {m.tenderId && (
                  <Link href={`/procurement/tenders/${m.tenderId}`} className="block text-xs text-accent-strong hover:underline">
                    {t("поставка по тендеру")}
                  </Link>
                )}
                {(m.reversedBy || (canReverse && reversible(m))) && (
                  <span className="flex flex-wrap gap-x-3 text-xs">
                    {m.batchId && (
                      <Link href={`/procurement/batches/${m.batchId}`} className="text-accent-strong hover:underline">
                        {t("карточка партии")}
                      </Link>
                    )}
                    {m.reversedBy ? (
                      <span className="text-ink-3">{t("сторнирован документом {n}", { n: m.reversedBy })}</span>
                    ) : (
                      <button type="button" onClick={() => setReversing(m)} className="inline-flex items-center gap-1 text-accent-strong hover:underline">
                        <Undo2 className="size-3.5" aria-hidden /> {t("Сторно {n}", { n: m.documentNumber })}
                      </button>
                    )}
                  </span>
                )}
                {m.orderId && (
                  <span className="flex flex-wrap gap-x-3 text-xs">
                    <Link href={`/procurement/receipts/${m.documentId}`} className="text-accent-strong hover:underline">
                      {t("приёмка {n}", { n: m.documentNumber })}
                    </Link>
                    <Link href={`/procurement/orders/${m.orderId}`} className="text-accent-strong hover:underline">
                      {t("заказ {n}", { n: m.orderNumber ?? "" })}
                    </Link>
                  </span>
                )}
              </span>
              <span className={`shrink-0 text-right tabular-nums ${m.quantity > 0 ? "text-ok" : m.quantity < 0 ? "text-bad" : "text-ink-3"}`}>
                {m.quantity > 0 ? "+" : ""}
                {quantity(m.quantity)} {m.unitCode}
                <span className="block text-[11px] text-ink-3">
                  {quantity(m.balanceBefore)} → {quantity(m.balanceAfter)}
                </span>
              </span>
            </li>
          ))}
        </ul>
      )}
      {reversing && <ReverseSheet movement={reversing} onClose={() => setReversing(null)} />}
      <div className="mt-3 flex flex-wrap items-center justify-between gap-3 text-sm text-ink-2">
        <span>{t("Движений: {n}", { n: data.total })}</span>

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
  );
}

const newKey = () => (typeof crypto !== "undefined" && "randomUUID" in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`);

/** Сторно документа: зеркальные движения на те же склады и партии; исправленный документ остаётся в журнале. Причина обязательна. */
function ReverseSheet({ movement, onClose }: { movement: MovementRow; onClose: () => void }) {
  const t = useT();
  const router = useRouter();
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const clientKey = useRef(newKey());
  const save = async () => {
    if (!reason.trim()) {
      setError(t("Укажите причину сторно."));
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await bff(`procurement/stock/documents/${movement.documentId}/reverse`, { method: "POST", body: JSON.stringify({ reason: reason.trim(), clientKey: clientKey.current }) });
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
      title={t("Сторно {n}", { n: movement.documentNumber })}
      subtitle={t("Все строки документа проведутся обратно — на те же склады и партии. Документ останется в журнале с пометкой «сторнирован».")}
      footer={
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-10 items-center rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted">
            {t("Закрыть")}
          </button>
          <button type="button" onClick={() => void save()} disabled={busy} className="inline-flex h-10 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50">
            {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}
            {t("Сторнировать")}
          </button>
        </div>
      }
    >
      <label className="block">
        <span className="mb-1.5 block text-sm font-medium text-ink">{t("Причина")}</span>
        <textarea
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          maxLength={1000}
          rows={3}
          autoFocus
          className="w-full rounded-lg border border-line bg-surface px-3 py-2.5 text-sm text-ink focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20"
        />
      </label>
      <div className="mt-3">
        <ErrorNote text={error} />
      </div>
    </Sheet>
  );
}
