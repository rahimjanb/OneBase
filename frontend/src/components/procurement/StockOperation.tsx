"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { Loader2, Plus, X } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { CatalogPicker } from "@/components/requests/CatalogPicker";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { parseNumber, quantity, type CatalogOption, type RequestDepartment } from "@/lib/procurement";

export type OperationKind = "receipt" | "issue" | "writeoff" | "inventory";

const look: Record<OperationKind, { title: string; button: string; path: string; qty: string; hint: string }> = {
  receipt: { title: "Приход на склад", button: "Провести приход", path: "receipt", qty: "Количество", hint: "Товар поступил на склад: остаток увеличится." },
  issue: { title: "Выдача отделу", button: "Выдать", path: "issue", qty: "Количество", hint: "Выдача без заявки: только из свободного остатка, резерв заявок не трогается." },
  writeoff: { title: "Списание", button: "Списать", path: "write-off", qty: "Количество", hint: "Брак, порча, утеря: только из свободного остатка, причина обязательна." },
  inventory: { title: "Инвентаризация", button: "Провести инвентаризацию", path: "inventory", qty: "Факт", hint: "Укажите фактический остаток — разница с учётом спишется или доприходуется." },
};

type Line = { key: string; option: CatalogOption | null; quantity: string; cost: string };

let seq = 0;
const blank = (): Line => ({ key: `l-${Date.now()}-${seq++}`, option: null, quantity: "", cost: "" });
const newKey = () => (typeof crypto !== "undefined" && "randomUUID" in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`);

const base = "h-10 rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
const field = `${base} w-full`;

/**
 * Складской документ: приход (с ценой за единицу и номером накладной), выдача отделу без заявки (отдел, кто получил), списание (причина),
 * инвентаризация (факт по позициям). Товары — из каталога; сервер проверяет остатки ещё раз и не уходит в минус. Повторное нажатие не
 * проводит документ второй раз (ключ формы).
 */
export function StockOperation({
  kind,
  open,
  onClose,
  departments,
  preset,
}: {
  kind: OperationKind;
  open: boolean;
  onClose: () => void;
  departments: RequestDepartment[];
  /** Открыть сразу с этой позицией (из карточки товара). */
  preset?: CatalogOption | null;
}) {
  const t = useT();
  const router = useRouter();
  const [lines, setLines] = useState<Line[]>([blank()]);
  const [comment, setComment] = useState("");
  const [reference, setReference] = useState("");
  const [department, setDepartment] = useState("");
  const [receiver, setReceiver] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const clientKey = useRef(newKey());
  const l = look[kind];

  useEffect(() => {
    if (!open) return;
    setLines([{ ...blank(), option: preset ?? null }]);
    setComment("");
    setReference("");
    setDepartment("");
    setReceiver("");
    setError(null);
    clientKey.current = newKey();
  }, [open, preset]);

  const patch = (key: string, change: Partial<Line>) => setLines((list) => list.map((x) => (x.key === key ? { ...x, ...change } : x)));

  const submit = async () => {
    setError(null);
    const filled = lines.filter((x) => x.option);
    if (filled.length === 0) {
      setError(t("Добавьте хотя бы одну позицию."));
      return;
    }
    for (const x of filled) {
      const q = parseNumber(x.quantity);
      if (q === null || q < 0 || (kind !== "inventory" && q === 0)) {
        setError(t("«{name}»: укажите количество.", { name: x.option!.name }));
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
    setBusy(true);
    try {
      await bff(`procurement/stock/${l.path}`, {
        method: "POST",
        body: JSON.stringify({
          lines: filled.map((x) => ({ itemId: x.option!.id, quantity: parseNumber(x.quantity), unitCost: kind === "receipt" ? parseNumber(x.cost) : null })),
          comment: comment.trim() || null,
          reference: reference.trim() || null,
          departmentId: department || null,
          receiver: receiver.trim() || null,
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
        <ul className="space-y-2">
          {lines.map((x, i) => (
            <li key={x.key} className="flex flex-wrap items-start gap-2 sm:flex-nowrap">
              <div className="min-w-0 basis-full sm:flex-1 sm:basis-auto">
                <CatalogPicker
                  name={x.option?.name ?? ""}
                  sku={x.option?.sku}
                  linked={!!x.option}
                  label={t("Товар, строка {n}", { n: i + 1 })}
                  onPick={(o) => patch(x.key, { option: o, quantity: kind === "inventory" ? String(o.onHand).replace(".", ",") : x.quantity })}
                  onUnlink={() => patch(x.key, { option: null })}
                />
                {x.option && (
                  <p className="mt-1 text-[11px] text-ink-3">
                    {t("на складе {a} · доступно {b}", { a: `${quantity(x.option.onHand)} ${x.option.unitCode}`, b: quantity(x.option.available) })}
                  </p>
                )}
              </div>
              <input
                value={x.quantity}
                onChange={(e) => patch(x.key, { quantity: e.target.value })}
                inputMode="decimal"
                placeholder={t(l.qty)}
                aria-label={t("{what}, строка {n}", { what: t(l.qty), n: i + 1 })}
                className={`${base} w-28 shrink-0 tabular-nums`}
              />
              {kind === "receipt" && (
                <input
                  value={x.cost}
                  onChange={(e) => patch(x.key, { cost: e.target.value })}
                  inputMode="decimal"
                  placeholder={t("Цена, сум")}
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
            </li>
          ))}
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
        {kind === "receipt" && (
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">
              {t("Номер накладной")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
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
