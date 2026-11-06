"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { Loader2, Save } from "lucide-react";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { parseNumber, quantity } from "@/lib/procurement";
import type { ProductionOrderDetail, ProductionOrderForm, Requirement } from "@/lib/production";
import { todayInTashkent } from "@/lib/tasks";
import { Label, card, field, newKey, primary, secondary } from "./ProdKit";

/**
 * Задание на выпуск (черновик): действующая рецептура, план и дата, цех, склад выдачи сырья, мастер, комментарий. Ниже — потребность по
 * нормам на план и сколько годного свободно на выбранном складе (не хватает — видно сразу; выпуск при этом не запрещён). Новое задание —
 * с ключом повтора формы; правка — с версией.
 */
export function OrderForm({ form, initial, recipeId }: { form: ProductionOrderForm; initial?: ProductionOrderDetail; recipeId?: string }) {
  const t = useT();
  const router = useRouter();
  const production = form.departments[0]?.id ?? "";
  const [recipe, setRecipe] = useState(initial?.recipeId ?? recipeId ?? form.recipes[0]?.id ?? "");
  const [planned, setPlanned] = useState(initial ? String(initial.plannedQty).replace(".", ",") : "");
  const [date, setDate] = useState(initial?.plannedDate ?? todayInTashkent());
  const [department, setDepartment] = useState(initial?.departmentId ?? production);
  const [warehouse, setWarehouse] = useState(initial?.warehouseId ?? form.warehouses[0]?.id ?? "");
  const [responsible, setResponsible] = useState(initial?.responsibleId ?? "");
  const [comment, setComment] = useState(initial?.comment ?? "");
  const [need, setNeed] = useState<Requirement | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const clientKey = useRef(newKey());
  const chosen = form.recipes.find((r) => r.id === recipe);
  const qty = parseNumber(planned);

  useEffect(() => {
    if (!recipe || qty === null || qty <= 0) {
      setNeed(null);
      return;
    }
    let alive = true;
    const timer = window.setTimeout(() => {
      bff<Requirement>(`production/recipes/${recipe}/requirement?quantity=${qty}${warehouse ? `&warehouse=${warehouse}` : ""}`)
        .then((r) => alive && setNeed(r))
        .catch(() => alive && setNeed(null));
    }, 300);
    return () => {
      alive = false;
      window.clearTimeout(timer);
    };
  }, [recipe, qty, warehouse]);

  const save = async () => {
    setError(null);
    if (!recipe) return setError(t("Выберите рецептуру."));
    if (qty === null || qty <= 0) return setError(t("Укажите, сколько выпустить."));
    const body = {
      recipeId: recipe,
      departmentId: department || null,
      warehouseId: warehouse || null,
      plannedQty: qty,
      plannedDate: date || null,
      responsibleId: responsible || null,
      comment: comment.trim() || null,
      clientKey: initial ? null : clientKey.current,
      version: initial?.version ?? null,
    };
    setBusy(true);
    try {
      if (initial) {
        await bff(`production/orders/${initial.id}`, { method: "PUT", body: JSON.stringify(body) });
        router.push(`/production/orders/${initial.id}`);
      } else {
        const { id } = await bff<{ id: string }>("production/orders", { method: "POST", body: JSON.stringify(body) });
        router.push(`/production/orders/${id}`);
      }
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setBusy(false);
    }
  };

  if (form.recipes.length === 0) {
    return (
      <p className="mx-auto max-w-2xl rounded-2xl border border-line bg-surface px-5 py-8 text-center text-sm text-ink-2">
        {t("Действующих рецептур нет — задание создают по действующей версии рецептуры. Попросите технолога ввести рецептуру в действие.")}
      </p>
    );
  }

  return (
    <div className="mx-auto max-w-4xl space-y-4">
      <section className={card}>
        <h2 className="mb-3 text-base font-semibold text-ink">{t("Задание")}</h2>
        <div className="grid gap-3 sm:grid-cols-2">
          <label className="block sm:col-span-2">
            <Label>{t("Рецептура")}</Label>
            <select value={recipe} onChange={(e) => setRecipe(e.target.value)} disabled={!!initial} className={field}>
              {form.recipes.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.name} — {r.code} v{r.versionNo}
                </option>
              ))}
            </select>
            {chosen && (
              <span className="mt-1 block text-xs text-ink-3">
                {t("Выход: {name}; нормы на {q}", { name: chosen.outputName, q: `${quantity(chosen.outputQuantity)} ${chosen.outputUnit}` })}
              </span>
            )}
          </label>
          <label className="block">
            <Label>{t("Выпустить")}</Label>
            <span className="relative block">
              <input value={planned} onChange={(e) => setPlanned(e.target.value)} inputMode="decimal" placeholder="0" className={`${field} pr-12 text-right tabular-nums`} />
              <span className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-sm text-ink-3">{chosen?.outputUnit}</span>
            </span>
          </label>
          <label className="block">
            <Label>{t("Дата выпуска")}</Label>
            <input type="date" value={date} onChange={(e) => setDate(e.target.value)} className={field} />
          </label>
          <label className="block">
            <Label>{t("Цех")}</Label>
            <select value={department} onChange={(e) => setDepartment(e.target.value)} className={field}>
              {form.departments.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                </option>
              ))}
            </select>
          </label>
          <label className="block">
            <Label>{t("Склад выдачи сырья")}</Label>
            <select value={warehouse} onChange={(e) => setWarehouse(e.target.value)} className={field}>
              {form.warehouses.map((w) => (
                <option key={w.id} value={w.id}>
                  {w.name}
                </option>
              ))}
            </select>
          </label>
          <label className="block">
            <Label optional>{t("Мастер")}</Label>
            <select value={responsible} onChange={(e) => setResponsible(e.target.value)} className={field}>
              <option value="">{t("Не выбран")}</option>
              {form.people.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
          </label>
          <label className="block sm:col-span-2">
            <Label optional>{t("Комментарий")}</Label>
            <textarea
              value={comment}
              onChange={(e) => setComment(e.target.value)}
              maxLength={2000}
              rows={2}
              className="w-full rounded-lg border border-line bg-surface px-3 py-2.5 text-sm text-ink focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20"
            />
          </label>
        </div>
      </section>

      {need && (
        <section className={card} aria-labelledby="po-need">
          <h2 id="po-need" className="mb-3 text-base font-semibold text-ink">
            {t("Потребность в сырье")} <span className="font-normal text-ink-3">· {need.warehouseName}</span>
          </h2>
          <div className="overflow-x-auto">
            <table className="w-full min-w-[480px] text-sm">
              <thead>
                <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                  <th className="py-2 pr-3 font-semibold">{t("Позиция")}</th>
                  <th className="py-2 pr-3 text-right font-semibold">{t("Нужно")}</th>
                  <th className="py-2 pr-3 text-right font-semibold">{t("Годного свободно")}</th>
                  <th className="py-2 text-right font-semibold">{t("Не хватает")}</th>
                </tr>
              </thead>
              <tbody>
                {need.lines.map((l) => (
                  <tr key={l.itemId} className="border-b border-line last:border-0">
                    <td className="py-2 pr-3 text-ink">{l.name}</td>
                    <td className="whitespace-nowrap py-2 pr-3 text-right tabular-nums">
                      {quantity(l.required)} {l.unitCode}
                    </td>
                    <td className="whitespace-nowrap py-2 pr-3 text-right tabular-nums text-ink-2">{quantity(l.available)}</td>
                    <td className={`whitespace-nowrap py-2 text-right tabular-nums ${l.shortage > 0 ? "font-semibold text-bad" : "text-ok"}`}>
                      {l.shortage > 0 ? quantity(l.shortage) : t("хватает")}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {need.lines.some((l) => l.shortage > 0) && (
            <p className="mt-2 text-xs text-warn">{t("Не хватает сырья — при выпуске задания мастер и снабжение получат уведомление. Выдают только годное: допущенные партии в сроке.")}</p>
          )}
        </section>
      )}

      <ErrorNote text={error} />
      <div className="flex justify-end gap-2">
        <Link href={initial ? `/production/orders/${initial.id}` : "/production/orders"} className={`${secondary} h-10`}>
          {t("Отмена")}
        </Link>
        <button type="button" onClick={() => void save()} disabled={busy} className={`${primary} h-10`}>
          {busy ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Save className="size-4" aria-hidden />} {t("Сохранить черновик")}
        </button>
      </div>
    </div>
  );
}
