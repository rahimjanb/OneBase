"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { ArrowRight, Loader2, Plus, X } from "lucide-react";
import { BatchDrafts, newBatchDraft } from "@/components/procurement/BatchDrafts";
import { Sheet } from "@/components/field/Sheet";
import { CatalogPicker } from "@/components/requests/CatalogPicker";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import {
  batchExpiry,
  batchOptionLabel,
  batchesTotal,
  operationWarehouse,
  parseNumber,
  quantity,
  unitToBase,
  type BatchDraft,
  type BatchOption,
  type CatalogOption,
  type RequestDepartment,
  type Warehouse,
} from "@/lib/procurement";

export type OperationKind = "receipt" | "issue" | "writeoff" | "inventory" | "transfer";

const look: Record<OperationKind, { title: string; button: string; path: string; qty: string; hint: string }> = {
  receipt: { title: "Приход на склад", button: "Провести приход", path: "receipt", qty: "Количество", hint: "Товар поступил на склад: остаток увеличится." },
  issue: { title: "Выдача отделу", button: "Выдать", path: "issue", qty: "Количество", hint: "Выдача без заявки: только из свободного остатка, резерв заявок не трогается." },
  writeoff: { title: "Списание", button: "Списать", path: "write-off", qty: "Количество", hint: "Брак, порча, утеря: только из свободного остатка, причина обязательна." },
  inventory: { title: "Инвентаризация", button: "Провести инвентаризацию", path: "inventory", qty: "Факт", hint: "Укажите фактический остаток — разница с учётом спишется или доприходуется." },
  transfer: {
    title: "Перемещение между складами",
    button: "Переместить",
    path: "transfer",
    qty: "Количество",
    hint: "Только свободный остаток — резерв заявок остаётся. Товар сразу уходит со склада-отправителя и приходит на склад-получатель.",
  },
};

/**
 * Строка формы; warehouse — склад, для которого показан остаток выбранного товара. У позиции с партиями: batches — партии прихода,
 * batchId — партия выдачи, списания, перемещения или инвентаризации (пусто — FEFO), options — партии позиции на складе формы. unit — единица
 * закупки прихода (пусто — единица карточки): количество, партии и цена — в ней, сервер пересчитает в единицу карточки.
 */
type Line = {
  key: string;
  option: CatalogOption | null;
  warehouse: string | null;
  quantity: string;
  cost: string;
  batches: BatchDraft[];
  batchId: string;
  options: BatchOption[] | null;
  unit: string;
};

let seq = 0;
const blank = (): Line => ({ key: `l-${Date.now()}-${seq++}`, option: null, warehouse: null, quantity: "", cost: "", batches: [newBatchDraft()], batchId: "", options: null, unit: "" });
const newKey = () => (typeof crypto !== "undefined" && "randomUUID" in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`);

const base = "h-10 rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
const field = `${base} w-full`;

/**
 * Складской документ: приход (с ценой за единицу и номером накладной), выдача отделу без заявки (отдел, кто получил), списание (причина),
 * инвентаризация (факт по позициям), перемещение между складами. Склад — из выбранного на странице (выбор виден, когда складов больше
 * одного; закреплённому кладовщику — только его склады). Позиция с партиями: приход — партии со сроком годности (количество строки — их
 * сумма), выдача, списание и перемещение — партия на выбор (по умолчанию FEFO: сначала ближайший срок), инвентаризация — факт партии. Товары —
 * из каталога; сервер проверяет остатки ещё раз и не уходит в минус. Повторное нажатие не проводит документ второй раз (ключ формы).
 */
export function StockOperation({
  kind,
  open,
  onClose,
  departments,
  warehouses,
  warehouseId,
  preset,
  presets,
}: {
  kind: OperationKind;
  open: boolean;
  onClose: () => void;
  departments: RequestDepartment[];
  warehouses: Warehouse[];
  /** Склад, выбранный на странице. */
  warehouseId?: string | null;
  /** Открыть сразу с этой позицией (из карточки товара). */
  preset?: CatalogOption | null;
  /** Открыть сразу с этими позициями — по строке каждая (выбранные галочками в таблице склада). */
  presets?: CatalogOption[] | null;
}) {
  const t = useT();
  const router = useRouter();
  const [lines, setLines] = useState<Line[]>([blank()]);
  const [comment, setComment] = useState("");
  const [reference, setReference] = useState("");
  const [department, setDepartment] = useState("");
  const [receiver, setReceiver] = useState("");
  const [warehouse, setWarehouse] = useState<string | null>(null);
  const [target, setTarget] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const clientKey = useRef(newKey());
  const l = look[kind];
  const transfer = kind === "transfer";
  const receipt = kind === "receipt";
  const inventory = kind === "inventory";
  const sources = warehouses.filter((w) => w.isActive && w.posts);
  const targets = warehouses.filter((w) => w.isActive && w.id !== warehouse);

  useEffect(() => {
    if (!open) return;
    const from = operationWarehouse(warehouses, warehouseId);
    // Карточка товара и таблица открывают операцию с остатком всего склада (по всем складам) — остаток склада формы подтянется при выборе товара.
    const chosen = presets?.length ? presets : preset ? [preset] : [];
    setLines(chosen.length ? chosen.map((option) => ({ ...blank(), option, warehouse: warehouseId === from ? from : null })) : [blank()]);
    setComment("");
    setReference("");
    setDepartment("");
    setReceiver("");
    setWarehouse(from);
    setTarget("");
    setError(null);
    clientKey.current = newKey();
    // warehouses — с сервера, меняются только при обновлении страницы.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, preset, presets, warehouseId]);

  // Партии позиций на складе формы — для выбора в выдаче, списании, перемещении и инвентаризации.
  const tracked = lines.filter((x) => x.option?.tracksBatches).map((x) => x.option!.id);
  const trackedKey = tracked.join(",");
  useEffect(() => {
    if (!open || receipt || !warehouse || tracked.length === 0) return;
    let alive = true;
    for (const id of new Set(tracked)) {
      bff<BatchOption[]>(`procurement/batches/options?item=${id}&warehouse=${warehouse}`)
        .then((options) => alive && setLines((list) => list.map((x) => (x.option?.id === id ? { ...x, options, batchId: options.some((o) => o.id === x.batchId) ? x.batchId : "" } : x))))
        .catch(() => undefined);
    }
    return () => {
      alive = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, receipt, warehouse, trackedKey]);

  const patch = (key: string, change: Partial<Line>) => setLines((list) => list.map((x) => (x.key === key ? { ...x, ...change } : x)));
  const factorOf = (x: Line) => (x.unit ? (x.option?.units?.find((u) => u.unitCode === x.unit)?.factor ?? null) : 1);

  const submit = async () => {
    setError(null);
    const filled = lines.filter((x) => x.option);
    if (!warehouse) {
      setError(t("Нет склада, по которому вы проводите операции."));
      return;
    }
    if (transfer && !target) {
      setError(t("Выберите склад-получатель."));
      return;
    }
    if (filled.length === 0) {
      setError(t("Добавьте хотя бы одну позицию."));
      return;
    }
    for (const x of filled) {
      const factor = factorOf(x);
      if (receipt && factor === null) {
        setError(t("«{name}»: единица «{unit}» больше не задана у позиции — выберите другую.", { name: x.option!.name, unit: x.unit }));
        return;
      }
      if (receipt && x.option!.tracksBatches) {
        if (batchesTotal(x.batches) === null) {
          setError(t("«{name}»: укажите количество каждой партии.", { name: x.option!.name }));
          return;
        }
        if (factor !== 1 && x.batches.some((b) => unitToBase(parseNumber(b.quantity) ?? 0, factor!) === null)) {
          setError(t("«{name}»: в {unit} получается больше трёх знаков — уточните количество.", { name: x.option!.name, unit: x.option!.unitCode }));
          return;
        }
        if (x.batches.some((b) => !batchExpiry(b.expires, b.made, x.option!.shelfLifeDays))) {
          setError(t("«{name}»: укажите срок годности каждой партии.", { name: x.option!.name }));
          return;
        }
        continue;
      }
      const q = parseNumber(x.quantity);
      if (q === null || q < 0 || (!inventory && q === 0)) {
        setError(t("«{name}»: укажите количество.", { name: x.option!.name }));
        return;
      }
      if (receipt && factor !== 1 && unitToBase(q, factor!) === null) {
        setError(t("«{name}»: в {unit} получается больше трёх знаков — уточните количество.", { name: x.option!.name, unit: x.option!.unitCode }));
        return;
      }
      if (inventory && x.option!.tracksBatches && !x.batchId) {
        setError(t("«{name}»: выберите партию — инвентаризация по партиям.", { name: x.option!.name }));
        return;
      }
    }
    if (kind === "writeoff" && !comment.trim()) {
      setError(t("Укажите причину списания."));
      return;
    }
    if (kind === "issue" && !department) {
      setError(t("Выберите отдел-получатель."));
      return;
    }
    const line = (x: Line) => {
      if (receipt && x.option!.tracksBatches) {
        return {
          itemId: x.option!.id,
          quantity: batchesTotal(x.batches),
          unitCost: parseNumber(x.cost),
          unitCode: x.unit || null,
          batches: x.batches.map((b) => ({ supplierBatchNo: b.no.trim() || null, manufacturedOn: b.made || null, expiresOn: b.expires || null, quantity: parseNumber(b.quantity) })),
        };
      }
      return {
        itemId: x.option!.id,
        quantity: parseNumber(x.quantity),
        unitCost: receipt ? parseNumber(x.cost) : null,
        batchId: x.batchId || null,
        unitCode: receipt ? x.unit || null : null,
      };
    };
    setBusy(true);
    try {
      const body = transfer
        ? {
            fromWarehouseId: warehouse,
            toWarehouseId: target,
            lines: filled.map((x) => ({ itemId: x.option!.id, quantity: parseNumber(x.quantity), batchId: x.batchId || null })),
            comment: comment.trim() || null,
            reference: reference.trim() || null,
            clientKey: clientKey.current,
          }
        : {
            lines: filled.map(line),
            comment: comment.trim() || null,
            reference: reference.trim() || null,
            departmentId: department || null,
            receiver: receiver.trim() || null,
            clientKey: clientKey.current,
            warehouseId: warehouse,
          };
      await bff(`procurement/stock/${l.path}`, { method: "POST", body: JSON.stringify(body) });
      onClose();
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  const select = (value: string, onChange: (v: string) => void, list: Warehouse[], label: string, empty?: string) => (
    <label className="block min-w-0 flex-1">
      <span className="mb-1.5 block text-sm font-medium text-ink">{label}</span>
      <select value={value} onChange={(e) => onChange(e.target.value)} className={field}>
        {empty && <option value="">{empty}</option>}
        {list.map((w) => (
          <option key={w.id} value={w.id}>
            {w.name}
          </option>
        ))}
      </select>
    </label>
  );

  return (
    <Sheet
      open={open}
      onClose={onClose}
      title={t(l.title)}
      subtitle={t(l.hint)}
      size="xl"
      footer={
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-10 items-center rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted">
            {t("Закрыть")}
          </button>
          <button
            type="button"
            onClick={() => void submit()}
            disabled={busy}
            className="inline-flex h-10 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50"
          >
            {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}
            {t(l.button)}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        {transfer ? (
          <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
            {select(
              warehouse ?? "",
              (v) => {
                setWarehouse(v);
                if (v === target) setTarget("");
              },
              sources,
              t("Откуда"),
            )}
            <ArrowRight className="mb-3 hidden size-4 shrink-0 text-ink-3 sm:block" aria-hidden />
            {select(target, setTarget, targets, t("Куда"), t("Выберите склад"))}
          </div>
        ) : (
          sources.length > 1 && <div className="sm:max-w-sm">{select(warehouse ?? "", setWarehouse, sources, t("Склад"))}</div>
        )}
        <ul className="space-y-3">
          {lines.map((x, i) => {
            const batched = !!x.option?.tracksBatches;
            const receiptBatches = receipt && batched;
            const total = receiptBatches ? batchesTotal(x.batches) : null;
            const factor = receipt && x.unit ? factorOf(x) : null;
            const typed = receiptBatches ? total : parseNumber(x.quantity);
            const inBase = factor && typed ? unitToBase(typed, factor) : null;
            const unitHint = factor
              ? inBase === null
                ? t("1 {a} = {f} {b}", { a: x.unit, f: quantity(factor), b: x.option!.unitCode })
                : t("= {q} {b}", { q: quantity(inBase), b: x.option!.unitCode })
              : null;
            return (
              <li key={x.key} className={batched ? "rounded-xl border border-line p-2.5" : ""}>
                <div className="flex flex-wrap items-start gap-2 sm:flex-nowrap">
                  <div className="min-w-0 basis-full sm:flex-1 sm:basis-auto">
                    <CatalogPicker
                      name={x.option?.name ?? ""}
                      sku={x.option?.sku}
                      linked={!!x.option}
                      label={t("Товар, строка {n}", { n: i + 1 })}
                      warehouseId={warehouse}
                      onPick={(o) => patch(x.key, { option: o, warehouse, quantity: inventory && !o.tracksBatches ? String(o.onHand).replace(".", ",") : x.quantity, batchId: "", options: null, unit: "" })}
                      onUnlink={() => patch(x.key, { option: null, warehouse: null, batchId: "", options: null, unit: "" })}
                    />
                    {x.option && x.warehouse === warehouse && (
                      <p className="mt-1 text-[11px] text-ink-3">
                        {t("на складе {a} · доступно {b}", { a: `${quantity(x.option.onHand)} ${x.option.unitCode}`, b: quantity(x.option.available) })}
                        {batched && ` · ${t("по партиям")}`}
                      </p>
                    )}
                    {unitHint && <p className="mt-0.5 text-[11px] text-accent-strong">{unitHint}</p>}
                  </div>
                  {receipt && (x.option?.units?.length ?? 0) > 0 && (
                    <select
                      value={x.unit}
                      onChange={(e) => patch(x.key, { unit: e.target.value })}
                      aria-label={t("Единица, строка {n}", { n: i + 1 })}
                      title={t("Единица прихода")}
                      className={`${base} w-24 shrink-0`}
                    >
                      <option value="">{x.option!.unitCode}</option>
                      {x.option!.units!.map((u) => (
                        <option key={u.unitCode} value={u.unitCode}>
                          {u.unitCode}
                        </option>
                      ))}
                    </select>
                  )}
                  {receiptBatches ? (
                    <span className="flex h-10 w-28 shrink-0 items-center justify-end px-1 text-sm tabular-nums text-ink-2" aria-label={t("Количество, строка {n}", { n: i + 1 })}>
                      {total === null ? "—" : `${quantity(total)} ${x.unit || x.option!.unitCode}`}
                    </span>
                  ) : (
                    <input
                      value={x.quantity}
                      onChange={(e) => patch(x.key, { quantity: e.target.value })}
                      inputMode="decimal"
                      placeholder={t(l.qty)}
                      aria-label={t("{what}, строка {n}", { what: t(l.qty), n: i + 1 })}
                      className={`${base} w-28 shrink-0 tabular-nums`}
                    />
                  )}
                  {receipt && (
                    <input
                      value={x.cost}
                      onChange={(e) => patch(x.key, { cost: e.target.value })}
                      inputMode="decimal"
                      placeholder={x.unit ? t("Цена за {unit}", { unit: x.unit }) : t("Цена, сум")}
                      aria-label={t("Цена за единицу, строка {n}", { n: i + 1 })}
                      className={`${base} w-32 shrink-0 tabular-nums`}
                    />
                  )}
                  <button
                    type="button"
                    onClick={() => setLines((list) => (list.length === 1 ? [blank()] : list.filter((y) => y.key !== x.key)))}
                    aria-label={t("Убрать строку {n}", { n: i + 1 })}
                    className="grid size-10 shrink-0 place-items-center rounded-md text-ink-3 hover:text-bad"
                  >
                    <X className="size-4" aria-hidden />
                  </button>
                </div>

                {receiptBatches && <BatchDrafts drafts={x.batches} shelfLifeDays={x.option!.shelfLifeDays} unitCode={x.unit || undefined} onChange={(batches) => patch(x.key, { batches })} />}

                {batched && !receipt && (
                  <label className="mt-2 block">
                    <span className="sr-only">{t("Партия")}</span>
                    <select
                      value={x.batchId}
                      onChange={(e) => {
                        const chosen = x.options?.find((o) => o.id === e.target.value);
                        patch(x.key, { batchId: e.target.value, quantity: inventory && chosen ? String(chosen.onHand).replace(".", ",") : x.quantity });
                      }}
                      className={field}
                    >
                      <option value="">{inventory ? t("Выберите партию") : t("Партия — автоматически (сначала ближайший срок)")}</option>
                      {(x.options ?? []).map((o) => (
                        <option key={o.id} value={o.id} disabled={kind === "issue" && !o.usable}>
                          {batchOptionLabel(o, x.option!.unitCode)}
                        </option>
                      ))}
                    </select>
                  </label>
                )}
              </li>
            );
          })}
        </ul>
        <button type="button" onClick={() => setLines((list) => [...list, blank()])} className="inline-flex h-9 items-center gap-2 rounded-lg px-2 text-sm font-semibold text-accent-strong hover:bg-accent-soft">
          <Plus className="size-4" aria-hidden /> {t("Добавить позицию")}
        </button>

        {kind === "issue" && (
          <div className="grid gap-3 sm:grid-cols-2">
            <label className="block">
              <span className="mb-1.5 block text-sm font-medium text-ink">{t("Отдел-получатель")}</span>
              <select value={department} onChange={(e) => setDepartment(e.target.value)} className={field}>
                <option value="">{t("Выберите отдел")}</option>
                {departments.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="block">
              <span className="mb-1.5 block text-sm font-medium text-ink">{t("Кто получил")}</span>
              <input value={receiver} onChange={(e) => setReceiver(e.target.value)} maxLength={200} className={field} />
            </label>
          </div>
        )}
        {(receipt || transfer) && (
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">
              {transfer ? t("Номер документа") : t("Номер накладной")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
            </span>
            <input value={reference} onChange={(e) => setReference(e.target.value)} maxLength={100} className={field} />
          </label>
        )}
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">
            {kind === "writeoff" ? t("Причина списания") : t("Комментарий")}
            {kind !== "writeoff" && <span className="font-normal text-ink-3"> {t("необязательно")}</span>}
          </span>
          <textarea value={comment} onChange={(e) => setComment(e.target.value)} maxLength={1000} rows={2} className={`${field} h-auto py-2.5`} />
        </label>
        <ErrorNote text={error} />
      </div>
    </Sheet>
  );
}
