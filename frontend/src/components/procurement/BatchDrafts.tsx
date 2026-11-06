"use client";

import { Plus, X } from "lucide-react";
import { useT } from "@/i18n/client";
import { batchExpiry, type BatchDraft } from "@/lib/procurement";

const field =
  "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";

let seq = 0;
/** Пустая партия формы (или с количеством — из строки). */
export const newBatchDraft = (quantity = ""): BatchDraft => ({ key: `b-${Date.now()}-${seq++}`, no: "", made: "", expires: "", quantity });

/**
 * Партии прихода позиции: номер партии поставщика, дата производства, срок годности, количество. Срок можно не указывать, если у позиции
 * задан срок хранения и указана дата производства — он подставится. Количество — в единице строки (unitCode), сумма партий — количество строки.
 */
export function BatchDrafts({
  drafts,
  shelfLifeDays,
  unitCode,
  onChange,
}: {
  drafts: BatchDraft[];
  shelfLifeDays: number | null | undefined;
  unitCode?: string;
  onChange: (drafts: BatchDraft[]) => void;
}) {
  const t = useT();
  const patch = (key: string, change: Partial<BatchDraft>) => onChange(drafts.map((b) => (b.key === key ? { ...b, ...change } : b)));
  return (
    <div className="mt-2 space-y-2">
      <div className="hidden gap-2 px-0.5 text-[11px] font-medium uppercase tracking-wide text-ink-3 sm:grid sm:grid-cols-[1fr_9.5rem_9.5rem_7rem_2.5rem]">
        <span>{t("Номер партии")}</span>
        <span>{t("Произведена")}</span>
        <span>{t("Годна до")}</span>
        <span>{unitCode ? `${t("Количество")}, ${unitCode}` : t("Количество")}</span>
      </div>
      {drafts.map((b, j) => {
        const expiry = batchExpiry(b.expires, b.made, shelfLifeDays);
        return (
          <div key={b.key} className="grid grid-cols-2 gap-2 sm:grid-cols-[1fr_9.5rem_9.5rem_7rem_2.5rem]">
            <input
              value={b.no}
              onChange={(e) => patch(b.key, { no: e.target.value })}
              maxLength={64}
              placeholder={t("Номер партии")}
              aria-label={t("Номер партии {n}", { n: j + 1 })}
              className={`${field} col-span-2 sm:col-span-1`}
            />
            <label className="block">
              <span className="mb-0.5 block text-[11px] text-ink-3 sm:sr-only">{t("Дата производства")}</span>
              <input type="date" value={b.made} onChange={(e) => patch(b.key, { made: e.target.value })} title={t("Дата производства")} className={field} />
            </label>
            <label className="block">
              <span className="mb-0.5 block text-[11px] text-ink-3 sm:sr-only">{t("Годна до")}</span>
              <input
                type="date"
                value={b.expires}
                onChange={(e) => patch(b.key, { expires: e.target.value })}
                title={t("Годна до")}
                className={`${field} ${expiry ? "" : "border-warn/60"}`}
              />
            </label>
            <input
              value={b.quantity}
              onChange={(e) => patch(b.key, { quantity: e.target.value })}
              inputMode="decimal"
              placeholder={t("Количество")}
              aria-label={t("Количество партии {n}", { n: j + 1 })}
              className={`${field} tabular-nums`}
            />
            <button
              type="button"
              onClick={() => onChange(drafts.length === 1 ? [newBatchDraft()] : drafts.filter((d) => d.key !== b.key))}
              aria-label={t("Убрать партию {n}", { n: j + 1 })}
              className="grid size-10 place-items-center rounded-md text-ink-3 hover:text-bad"
            >
              <X className="size-4" aria-hidden />
            </button>
            {!b.expires && expiry && (
              <p className="col-span-2 text-[11px] text-ink-3 sm:col-span-5">{t("Годна до {date} — по сроку хранения позиции.", { date: expiry.split("-").reverse().join(".") })}</p>
            )}
          </div>
        );
      })}
      <button
        type="button"
        onClick={() => onChange([...drafts, newBatchDraft()])}
        className="inline-flex h-8 items-center gap-1.5 rounded-lg px-2 text-xs font-semibold text-accent-strong hover:bg-accent-soft"
      >
        <Plus className="size-3.5" aria-hidden /> {t("Ещё партия")}
      </button>
    </div>
  );
}
