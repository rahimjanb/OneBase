"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { ClipboardList, Loader2, Plus } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { quantity } from "@/lib/procurement";
import { dayRu, shiftStatusLabel, shiftStatusTone, type ShiftForm, type ShiftList } from "@/lib/production";
import { todayInTashkent } from "@/lib/tasks";
import { Label, field, newKey, primary, secondary } from "./ProdKit";

const filters = [
  { value: "", label: "Все" },
  { value: "open", label: "Открытые" },
  { value: "Draft", label: "Ввод" },
  { value: "Closed", label: "Ждут утверждения" },
  { value: "Approved", label: "Утверждённые" },
] as const;

/**
 * Смены: журнал сменных отчётов (дата и смена, задание, продукт, оператор, годный выход, брак, статус) с фильтром по статусу; «Новая смена»
 * — по выпущенному заданию: дата, номер смены, линия, оператор, начальник смены.
 */
export function ShiftsView({ data, form, order }: { data: ShiftList; form: ShiftForm | null; order?: string }) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const status = params.get("status") ?? "";
  const [open, setOpen] = useState(!!order && !!form);
  const [orderId, setOrderId] = useState(order ?? form?.orders[0]?.id ?? "");
  const [date, setDate] = useState(todayInTashkent());
  const [shiftNo, setShiftNo] = useState("1");
  const [line, setLine] = useState("");
  const [operator, setOperator] = useState("");
  const [supervisor, setSupervisor] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [key] = useState(newKey);

  const apply = (value: string) => {
    const next = new URLSearchParams(params.toString());
    if (value) next.set("status", value);
    else next.delete("status");
    next.delete("page");
    router.push(`${pathname}${next.size ? `?${next}` : ""}`, { scroll: false });
  };

  const create = async () => {
    if (!orderId) return setError(t("Выберите задание на выпуск."));
    setBusy(true);
    setError(null);
    try {
      const { id } = await bff<{ id: string }>("production/shifts", {
        method: "POST",
        body: JSON.stringify({ orderId, shiftDate: date, shiftNo: Number(shiftNo), line: line.trim() || null, operatorId: operator || null, supervisorId: supervisor || null, clientKey: key }),
      });
      router.push(`/production/shifts/${id}`);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setBusy(false);
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-2">
        {filters.map((f) => (
          <button
            key={f.value}
            type="button"
            onClick={() => apply(f.value)}
            aria-pressed={status === f.value}
            className={`h-9 rounded-lg border px-3 text-sm font-medium ${status === f.value ? "border-accent bg-accent text-white" : "border-line bg-surface text-ink hover:bg-muted"}`}
          >
            {t(f.label)}
            {f.value === "Draft" && data.drafts > 0 && <span className="ml-1.5 tabular-nums opacity-70">{data.drafts}</span>}
            {f.value === "Closed" && data.closed > 0 && <span className="ml-1.5 tabular-nums opacity-70">{data.closed}</span>}
          </button>
        ))}
        {data.canCreate && form && (
          <button type="button" onClick={() => setOpen(true)} className={`${primary} ml-auto h-9`}>
            <Plus className="size-4" aria-hidden /> {t("Новая смена")}
          </button>
        )}
      </div>

      {data.items.length === 0 ? (
        <section className="flex flex-col items-center rounded-2xl border border-line bg-surface px-6 py-12 text-center">
          <span className="grid size-12 place-items-center rounded-2xl bg-accent-soft text-accent-strong">
            <ClipboardList className="size-6" aria-hidden />
          </span>
          <h2 className="mt-3 text-base font-semibold text-ink">{status ? t("Смен с таким статусом нет") : t("Смен пока нет")}</h2>
          <p className="mt-1 max-w-md text-sm text-ink-2">
            {t("Сменный отчёт — что сделала смена по заданию: замесы, выпечка, брак по этапам, упаковка. По нему система считает расход по результатам.")}
          </p>
        </section>
      ) : (
        <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
          <table className="w-full min-w-[760px] text-sm">
            <thead>
              <tr className="whitespace-nowrap border-b border-line text-left text-[11px] text-ink-3">
                <th className="px-4 py-2.5 font-semibold">{t("Смена")}</th>
                <th className="py-2.5 pr-3 font-semibold">{t("Задание и продукт")}</th>
                <th className="py-2.5 pr-3 font-semibold">{t("Оператор")}</th>
                <th className="py-2.5 pr-3 text-right font-semibold">{t("Годный выход")}</th>
                <th className="py-2.5 pr-3 text-right font-semibold">{t("Брак, кг")}</th>
                <th className="py-2.5 pr-4 text-right font-semibold">{t("Статус")}</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((s) => (
                <tr key={s.id} className="border-b border-line last:border-0 hover:bg-muted/40">
                  <td className="px-4 py-2.5">
                    <Link href={`/production/shifts/${s.id}`} className="font-medium tabular-nums text-accent-strong hover:underline">
                      {s.number}
                    </Link>
                    <span className="block text-xs text-ink-3">
                      {dayRu(s.shiftDate)} · {t("смена {n}", { n: s.shiftNo })}
                      {s.line && ` · ${s.line}`}
                    </span>
                    {s.kind === "Correction" && <span className="block text-xs text-warn">{t("корректировка {n}", { n: s.correctsNumber ?? "" })}</span>}
                  </td>
                  <td className="py-2.5 pr-3 text-ink">
                    {s.recipeName}
                    <span className="block text-xs tabular-nums text-ink-3">{s.orderNumber}</span>
                  </td>
                  <td className="py-2.5 pr-3 text-ink-2">{s.operatorName}</td>
                  <td className="whitespace-nowrap py-2.5 pr-3 text-right tabular-nums">{s.goodOutput !== null ? `${quantity(s.goodOutput)} ${s.outputUnit}` : "—"}</td>
                  <td className={`whitespace-nowrap py-2.5 pr-3 text-right tabular-nums ${s.defectKg > 0 ? "text-bad" : "text-ink-3"}`}>{quantity(s.defectKg)}</td>
                  <td className="py-2.5 pr-4 text-right">
                    <span className={`inline-flex rounded-md px-2 py-0.5 text-xs font-semibold ${shiftStatusTone[s.status]}`}>{t(shiftStatusLabel[s.status])}</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {form && (
        <Sheet
          open={open}
          onClose={() => setOpen(false)}
          title={t("Новая смена")}
          footer={
            <div className="flex justify-end gap-2">
              <button type="button" onClick={() => setOpen(false)} className={secondary}>
                {t("Закрыть")}
              </button>
              <button type="button" onClick={() => void create()} disabled={busy || form.orders.length === 0} className={primary}>
                {busy && <Loader2 className="size-4 animate-spin" aria-hidden />} {t("Открыть смену")}
              </button>
            </div>
          }
        >
          {form.orders.length === 0 ? (
            <p className="text-sm text-ink-2">{t("Нет выпущенных заданий — смену открывают по заданию, которое выпущено или в работе.")}</p>
          ) : (
            <div className="space-y-3">
              <label className="block">
                <Label>{t("Задание на выпуск")}</Label>
                <select value={orderId} onChange={(e) => setOrderId(e.target.value)} className={field}>
                  {form.orders.map((o) => (
                    <option key={o.id} value={o.id}>
                      {o.number} · {o.recipeName} · {quantity(o.plannedQty)} {o.outputUnit}
                    </option>
                  ))}
                </select>
              </label>
              <div className="grid grid-cols-2 gap-3">
                <label className="block">
                  <Label>{t("Дата")}</Label>
                  <input type="date" value={date} onChange={(e) => setDate(e.target.value)} className={field} />
                </label>
                <label className="block">
                  <Label>{t("Смена")}</Label>
                  <select value={shiftNo} onChange={(e) => setShiftNo(e.target.value)} className={field}>
                    {[1, 2, 3].map((n) => (
                      <option key={n} value={n}>
                        {t("смена {n}", { n })}
                      </option>
                    ))}
                  </select>
                </label>
              </div>
              <label className="block">
                <Label optional>{t("Линия")}</Label>
                <input value={line} onChange={(e) => setLine(e.target.value)} maxLength={100} placeholder={t("Например: линия 1")} className={field} />
              </label>
              <div className="grid gap-3 sm:grid-cols-2">
                <label className="block">
                  <Label optional>{t("Оператор")}</Label>
                  <select value={operator} onChange={(e) => setOperator(e.target.value)} className={field}>
                    <option value="">{t("Я")}</option>
                    {form.people.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.name}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="block">
                  <Label optional>{t("Начальник смены")}</Label>
                  <select value={supervisor} onChange={(e) => setSupervisor(e.target.value)} className={field}>
                    <option value="">{t("Не выбран")}</option>
                    {form.people.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.name}
                      </option>
                    ))}
                  </select>
                </label>
              </div>
            </div>
          )}
          <div className="mt-3">
            <ErrorNote text={error} />
          </div>
        </Sheet>
      )}
    </div>
  );
}
