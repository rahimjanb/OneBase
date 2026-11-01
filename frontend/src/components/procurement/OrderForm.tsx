"use client";

import { useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { AlertTriangle, Loader2, Save, Send } from "lucide-react";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { money, orderTotals, parseNumber, quantity, type Need, type Offer, type OrderDetail, type SupplierRow } from "@/lib/procurement";

const field =
  "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20 disabled:opacity-60";
const label = "mb-1.5 block text-sm font-medium text-ink";
const card = "rounded-2xl border border-line bg-surface p-4 sm:p-5";
const num = (n: number | null | undefined) => (n === null || n === undefined ? "" : String(n).replace(".", ","));

type Line = Need & { selected: boolean; qty: string; price: string };

/**
 * Новый заказ поставщику и правка. Позиции — только из утверждённых заявок (не больше незаказанного остатка «к закупке»); из
 * предложения — цены, НДС, доставка и условия подставляются. Итог считается сразу; цена выше утверждённой в заявке — предупреждение:
 * такой заказ при оформлении уйдёт директору. Сервер проверяет всё ещё раз.
 */
export function OrderForm({
  needs,
  suppliers,
  responsibles,
  me,
  offer,
  initial,
}: {
  needs: Need[];
  suppliers: SupplierRow[];
  responsibles: { id: string; name: string }[];
  me: string;
  offer?: Offer | null;
  initial?: OrderDetail;
}) {
  const t = useT();
  const router = useRouter();
  const clientKey = useRef(typeof crypto !== "undefined" && "randomUUID" in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`);
  const [supplierId, setSupplierId] = useState(initial?.supplierId ?? offer?.supplierId ?? "");
  const [responsibleId, setResponsibleId] = useState(initial?.responsibleId ?? (responsibles.some((r) => r.id === me) ? me : (responsibles[0]?.id ?? "")));
  const [expected, setExpected] = useState(
    initial?.expectedDate ?? (offer?.deliveryDays !== null && offer?.deliveryDays !== undefined ? new Date(Date.now() + offer.deliveryDays * 86400000).toISOString().slice(0, 10) : ""),
  );
  const [terms, setTerms] = useState(initial?.paymentTerms ?? offer?.paymentTerms ?? "");
  const [vatRate, setVatRate] = useState(num(initial?.vatRate ?? offer?.vatRate ?? 12));
  const [includeVat, setIncludeVat] = useState(initial?.pricesIncludeVat ?? offer?.pricesIncludeVat ?? true);
  const [delivery, setDelivery] = useState(num(initial?.deliveryCost ?? offer?.deliveryCost ?? 0));
  const [discount, setDiscount] = useState(num(initial?.discount ?? 0));
  const [comment, setComment] = useState(initial?.comment ?? "");
  const [busy, setBusy] = useState<"save" | "submit" | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [lines, setLines] = useState<Line[]>(() => {
    const rows = new Map<string, Line>();
    for (const n of needs) rows.set(n.requestItemId, { ...n, selected: false, qty: num(n.remaining), price: num(n.estimatedPrice) });
    for (const i of initial?.items ?? []) {
      const need = rows.get(i.requestItemId);
      const remaining = (need?.remaining ?? 0) + i.quantity;
      rows.set(i.requestItemId, {
        requestItemId: i.requestItemId,
        requestId: i.requestId,
        requestNumber: i.requestNumber,
        requestTitle: need?.requestTitle ?? "",
        departmentName: need?.departmentName ?? "",
        name: i.name,
        unitCode: i.unitCode,
        toPurchase: need?.toPurchase ?? remaining,
        ordered: need?.ordered ?? i.quantity,
        remaining,
        estimatedPrice: i.estimatedPrice,
        categoryName: need?.categoryName ?? "",
        selected: true,
        qty: num(i.quantity),
        price: num(i.unitPrice),
      });
    }
    for (const l of offer?.lines ?? []) {
      const row = rows.get(l.requestItemId);
      if (row) rows.set(l.requestItemId, { ...row, selected: true, price: num(l.unitPrice), qty: num(Math.min(row.remaining, l.quantity)) });
    }
    return [...rows.values()];
  });

  const supplier = suppliers.find((s) => s.id === supplierId);
  const selected = lines.filter((l) => l.selected);
  const vat = parseNumber(vatRate) ?? 0;
  const totals = orderTotals(
    selected.map((l) => ({ quantity: parseNumber(l.qty) ?? 0, price: parseNumber(l.price) ?? 0 })),
    parseNumber(discount) ?? 0,
    vat,
    includeVat,
    parseNumber(delivery) ?? 0,
  );
  // Дороже утверждённого (цена с НДС выше предполагаемой) или в заявке не было цены — заказ при оформлении уйдёт директору.
  const aboveApproved = selected.filter((l) => {
    const price = parseNumber(l.price) ?? 0;
    const gross = includeVat ? price : price * (1 + vat / 100);
    return l.estimatedPrice === null || gross > l.estimatedPrice;
  });
  const approvedEdit = !!initial && initial.status !== "Draft";
  const byRequest = useMemo(() => {
    const map = new Map<string, Line[]>();
    for (const l of lines) map.set(l.requestNumber, [...(map.get(l.requestNumber) ?? []), l]);
    return [...map.entries()];
  }, [lines]);
  const patch = (id: string, change: Partial<Line>) => setLines((list) => list.map((l) => (l.requestItemId === id ? { ...l, ...change } : l)));

  const save = async (submit: boolean) => {
    setError(null);
    if (!supplierId) return setError(t("Выберите поставщика."));
    if (selected.length === 0) return setError(t("Отметьте хотя бы одну позицию из утверждённых заявок."));
    for (const l of selected) {
      const q = parseNumber(l.qty);
      if (q === null || q <= 0) return setError(t("«{name}»: укажите количество.", { name: l.name }));
      if (q > l.remaining) return setError(t("«{name}»: можно заказать не больше {n}.", { name: l.name, n: `${quantity(l.remaining)} ${l.unitCode}` }));
      if (parseNumber(l.price) === null) return setError(t("«{name}»: укажите цену.", { name: l.name }));
    }
    setBusy(submit ? "submit" : "save");
    const body = {
      supplierId,
      offerId: initial?.offerId ?? offer?.id ?? null,
      responsibleId: responsibleId || null,
      expectedDate: expected || null,
      paymentTerms: terms.trim() || null,
      discount: parseNumber(discount) ?? 0,
      vatRate: vat,
      pricesIncludeVat: includeVat,
      deliveryCost: parseNumber(delivery) ?? 0,
      comment: comment.trim() || null,
      lines: selected.map((l) => ({ requestItemId: l.requestItemId, quantity: parseNumber(l.qty), unitPrice: parseNumber(l.price) })),
      submit,
      clientKey: initial ? null : clientKey.current,
      version: initial?.version ?? null,
    };
    try {
      let id = initial?.id;
      if (initial) await bff(`procurement/orders/${initial.id}`, { method: "PUT", body: JSON.stringify(body) });
      else id = (await bff<{ id: string }>("procurement/orders", { method: "POST", body: JSON.stringify(body) })).id;
      router.push(`/procurement/orders/${id}`);
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setBusy(null);
    }
  };

  return (
    <div className="mx-auto grid max-w-6xl gap-4 lg:grid-cols-[minmax(0,1fr)_300px]">
      <div className="min-w-0 space-y-4">
        {approvedEdit && (
          <p className="flex items-start gap-2 rounded-2xl border border-violet/30 bg-violet-soft px-4 py-3 text-sm text-ink">
            <AlertTriangle className="mt-0.5 size-4 shrink-0 text-violet-light" aria-hidden />
            {t("Заказ уже утверждён. Другой поставщик, рост количества, цены или итога сверх допуска, новые и удалённые позиции отправят его директору повторно.")}
          </p>
        )}
        <section className={card}>
          <div className="grid gap-4 sm:grid-cols-2">
            <label className="block">
              <span className={label}>{t("Поставщик")}</span>
              <select value={supplierId} onChange={(e) => setSupplierId(e.target.value)} className={field}>
                <option value="">{t("Выберите поставщика")}</option>
                {suppliers.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name}
                    {s.inn ? ` · ${s.inn}` : ""}
                  </option>
                ))}
              </select>
              {supplier && (
                <span className="mt-1 block text-xs text-ink-3">
                  {supplier.leadTimeDays !== null && t("обычно {n} дн.", { n: supplier.leadTimeDays })}
                  {supplier.overdue > 0 && <span className="text-bad"> · {t("просрочек сейчас: {n}", { n: supplier.overdue })}</span>}
                </span>
              )}
            </label>
            <label className="block">
              <span className={label}>{t("Ответственный")}</span>
              <select value={responsibleId} onChange={(e) => setResponsibleId(e.target.value)} className={field}>
                {responsibles.map((r) => (
                  <option key={r.id} value={r.id}>
                    {r.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="block">
              <span className={label}>{t("Ожидаемая дата поставки")}</span>
              <input type="date" value={expected} onChange={(e) => setExpected(e.target.value)} className={field} />
            </label>
            <label className="block">
              <span className={label}>{t("Условия оплаты")}</span>
              <input value={terms} onChange={(e) => setTerms(e.target.value)} maxLength={500} placeholder={t("Например: предоплата 50%, остаток по факту")} className={field} />
            </label>
          </div>
        </section>

        <section className={card} aria-labelledby="po-lines">
          <h2 id="po-lines" className="mb-1 text-base font-semibold text-ink">
            {t("Позиции из утверждённых заявок")}
          </h2>
          <p className="mb-3 text-xs text-ink-3">{t("Отметьте, что заказать у этого поставщика. Не больше незаказанного остатка «к закупке».")}</p>
          {lines.length === 0 ? (
            <p className="rounded-xl bg-muted px-3 py-6 text-center text-sm text-ink-3">{t("Утверждённых незаказанных позиций нет.")}</p>
          ) : (
            <div className="space-y-4">
              {byRequest.map(([number, rows]) => (
                <div key={number}>
                  <div className="mb-1.5 text-xs font-semibold text-ink-3">
                    {number}
                    {rows[0].requestTitle && ` · ${rows[0].requestTitle}`}
                    {rows[0].departmentName && ` · ${rows[0].departmentName}`}
                  </div>
                  <ul className="divide-y divide-line rounded-xl border border-line">
                    {rows.map((l) => {
                      const over = l.selected && aboveApproved.includes(l);
                      return (
                        <li key={l.requestItemId} className="flex flex-wrap items-center gap-x-3 gap-y-2 px-3 py-2.5">
                          <label className="flex min-w-0 flex-1 basis-56 items-center gap-2.5">
                            <input type="checkbox" checked={l.selected} onChange={(e) => patch(l.requestItemId, { selected: e.target.checked })} className="size-4 shrink-0 accent-[var(--color-accent)]" />
                            <span className="min-w-0">
                              <span className="block truncate text-sm text-ink">{l.name}</span>
                              <span className="block text-[11px] text-ink-3">
                                {t("осталось заказать {n}", { n: `${quantity(l.remaining)} ${l.unitCode}` })}
                                {l.estimatedPrice !== null && ` · ${t("в заявке {p}", { p: money(l.estimatedPrice) })}`}
                              </span>
                            </span>
                          </label>
                          <input
                            value={l.qty}
                            onChange={(e) => patch(l.requestItemId, { qty: e.target.value, selected: true })}
                            inputMode="decimal"
                            aria-label={t("Количество: {name}", { name: l.name })}
                            className="h-9 w-24 rounded-md border border-line bg-surface px-2 text-right text-sm tabular-nums text-ink focus:border-accent focus:outline-none"
                          />
                          <input
                            value={l.price}
                            onChange={(e) => patch(l.requestItemId, { price: e.target.value, selected: true })}
                            inputMode="decimal"
                            placeholder={t("цена")}
                            aria-label={t("Цена за единицу: {name}", { name: l.name })}
                            className={`h-9 w-32 rounded-md border bg-surface px-2 text-right text-sm tabular-nums text-ink focus:border-accent focus:outline-none ${over ? "border-violet/60" : "border-line"}`}
                          />
                          <span className="w-28 text-right text-sm tabular-nums text-ink-2">
                            {l.selected ? money(Math.round((parseNumber(l.qty) ?? 0) * (parseNumber(l.price) ?? 0) * 100) / 100) : "—"}
                          </span>
                        </li>
                      );
                    })}
                  </ul>
                </div>
              ))}
            </div>
          )}
        </section>

        <section className={card}>
          <div className="grid gap-4 sm:grid-cols-3">
            <label className="block">
              <span className={label}>{t("НДС, %")}</span>
              <input value={vatRate} onChange={(e) => setVatRate(e.target.value)} inputMode="decimal" className={`${field} tabular-nums`} />
            </label>
            <label className="block">
              <span className={label}>{t("Доставка, сум")}</span>
              <input value={delivery} onChange={(e) => setDelivery(e.target.value)} inputMode="decimal" className={`${field} tabular-nums`} />
            </label>
            <label className="block">
              <span className={label}>{t("Скидка, сум")}</span>
              <input value={discount} onChange={(e) => setDiscount(e.target.value)} inputMode="decimal" className={`${field} tabular-nums`} />
            </label>
            <label className="flex items-center gap-2 text-sm text-ink sm:col-span-3">
              <input type="checkbox" checked={includeVat} onChange={(e) => setIncludeVat(e.target.checked)} className="size-4 accent-[var(--color-accent)]" />
              {t("Цены указаны с НДС")}
            </label>
            <label className="block sm:col-span-3">
              <span className={label}>
                {t("Комментарий")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
              </span>
              <textarea value={comment} onChange={(e) => setComment(e.target.value)} maxLength={2000} rows={2} className={`${field} h-auto py-2.5`} />
            </label>
          </div>
        </section>
      </div>

      <aside>
        <div className={`${card} space-y-3 lg:sticky lg:top-4`}>
          <dl className="space-y-1.5 text-sm">
            <div className="flex justify-between gap-3">
              <dt className="text-ink-3">{t("Позиции")}</dt>
              <dd className="tabular-nums text-ink">{money(totals.subtotal)}</dd>
            </div>
            {(parseNumber(discount) ?? 0) > 0 && (
              <div className="flex justify-between gap-3">
                <dt className="text-ink-3">{t("Скидка")}</dt>
                <dd className="tabular-nums text-ink">−{money(parseNumber(discount))}</dd>
              </div>
            )}
            {totals.vat > 0 && (
              <div className="flex justify-between gap-3">
                <dt className="text-ink-3">{t("НДС")}</dt>
                <dd className="tabular-nums text-ink">{money(totals.vat)}</dd>
              </div>
            )}
            {(parseNumber(delivery) ?? 0) > 0 && (
              <div className="flex justify-between gap-3">
                <dt className="text-ink-3">{t("Доставка")}</dt>
                <dd className="tabular-nums text-ink">{money(parseNumber(delivery))}</dd>
              </div>
            )}
          </dl>
          <div className="border-t border-line pt-3">
            <div className="text-xs text-ink-3">{t("Итог заказа")}</div>
            <div className="text-2xl font-semibold tabular-nums text-ink">
              {money(totals.total)} <span className="text-base font-medium text-ink-3">{t("сум")}</span>
            </div>
          </div>
          {aboveApproved.length > 0 && (
            <p className="rounded-xl bg-violet-soft px-3 py-2 text-xs text-ink">
              {t("Цена выше утверждённой в заявке (или цены в заявке не было): {names}. При оформлении заказ уйдёт директору на согласование.", {
                names: aboveApproved.map((l) => l.name).join(", "),
              })}
            </p>
          )}
          <ErrorNote text={error} />
          <div className="flex gap-2 lg:flex-col">
            {!approvedEdit && (
              <button
                type="button"
                onClick={() => void save(true)}
                disabled={busy !== null}
                className="inline-flex h-11 flex-1 items-center justify-center gap-2 rounded-xl bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50 lg:h-10 lg:flex-none"
              >
                {busy === "submit" ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Send className="size-4" aria-hidden />}
                {t("Оформить заказ")}
              </button>
            )}
            <button
              type="button"
              onClick={() => void save(false)}
              disabled={busy !== null}
              className={`inline-flex h-11 flex-1 items-center justify-center gap-2 rounded-xl px-4 text-sm font-semibold disabled:opacity-50 lg:h-10 lg:flex-none ${
                approvedEdit ? "bg-accent text-white hover:bg-accent-strong" : "border border-line bg-surface text-ink hover:bg-muted"
              }`}
            >
              {busy === "save" ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Save className="size-4" aria-hidden />}
              {approvedEdit ? t("Сохранить изменения") : t("Сохранить черновик")}
            </button>
          </div>
          {!approvedEdit && <p className="text-xs leading-relaxed text-ink-3">{t("Черновик ещё не утверждён. «Оформить»: в рамках утверждённой заявки — утверждается сразу, дороже — уходит директору.")}</p>}
        </div>
      </aside>
    </div>
  );
}
