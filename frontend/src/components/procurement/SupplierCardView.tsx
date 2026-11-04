"use client";

import Link from "next/link";
import { useState } from "react";
import { AlertTriangle, Pencil } from "lucide-react";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { useT } from "@/i18n/client";
import { dateRu, money, quantity, type ProcDocument, type RequestCategory, type SupplierCard } from "@/lib/procurement";
import { DocumentsBlock, OrderStatusPill, RatingBadge } from "./ProcKit";
import { ClaimsBlock } from "./ReceiptKit";
import { SupplierFormSheet } from "./SuppliersView";

const card = "rounded-2xl border border-line bg-surface p-4 sm:p-5";

function Fact({ label, children }: { label: string; children: React.ReactNode }) {
  const t = useT();
  return (
    <div className="flex items-start justify-between gap-4 py-2 text-sm">
      <dt className="shrink-0 text-ink-3">{t(label)}</dt>
      <dd className="min-w-0 break-words text-right text-ink">{children}</dd>
    </div>
  );
}

/** Составляющая рейтинга: полоса 0–100 и пояснение; нет данных — так и написано. */
function Part({ label, value, hint }: { label: string; value: number | null; hint: string }) {
  const t = useT();
  return (
    <div>
      <div className="flex items-baseline justify-between gap-3 text-sm">
        <span className="text-ink-2">{t(label)}</span>
        <span className="tabular-nums text-ink">{value === null ? <span className="text-xs text-ink-3">{t("нет данных")}</span> : `${Math.round(value)}`}</span>
      </div>
      <div className="mt-1 h-1.5 overflow-hidden rounded-full bg-muted">
        {value !== null && <div className={`h-full rounded-full ${value >= 80 ? "bg-ok" : value >= 60 ? "bg-warn" : "bg-bad"}`} style={{ width: `${Math.max(2, value)}%` }} />}
      </div>
      <p className="mt-0.5 text-[11px] text-ink-3">{hint}</p>
    </div>
  );
}

/**
 * Карточка поставщика: реквизиты и контакты, категории, рейтинг по составляющим (сроки, качество, соблюдение цен, претензии, полнота
 * поставок), история заказов, цен и просрочек, претензии по приёмкам, документы (договор).
 */
export function SupplierCardView({ supplier, documents, categories }: { supplier: SupplierCard; documents: ProcDocument[]; categories: RequestCategory[] }) {
  const t = useT();
  const [editing, setEditing] = useState(false);
  const r = supplier.rating;
  const overdue = supplier.orders.filter((o) => o.overdue);
  return (
    <>
      <PageHeader
        title={supplier.name}
        subtitle={
          <span className="inline-flex flex-wrap items-center gap-3">
            {supplier.inn ? t("ИНН {n}", { n: supplier.inn }) : t("ИНН не указан")}
            <RatingBadge rating={r.overall} />
            {!supplier.isActive && <span className="rounded-md bg-muted px-1.5 py-0.5 text-xs text-ink-3">{t("отключён")}</span>}
          </span>
        }
        back="/procurement/suppliers"
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Снабжение"), href: "/procurement" }, { label: t("Поставщики"), href: "/procurement/suppliers" }, { label: supplier.name }]}
        actions={
          supplier.canEdit ? (
            <button type="button" onClick={() => setEditing(true)} className="inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3.5 text-sm font-medium text-ink hover:bg-muted">
              <Pencil className="size-4" aria-hidden /> {t("Изменить")}
            </button>
          ) : undefined
        }
      />
      <PageBody>
        <div className="mx-auto grid max-w-6xl gap-4 lg:grid-cols-[minmax(0,1fr)_340px]">
          <div className="min-w-0 space-y-4">
            {supplier.products && (
              <section className={card}>
                <h2 className="mb-2.5 text-base font-semibold text-ink">{t("Что поставляет")}</h2>
                <ul className="flex flex-wrap gap-1.5">
                  {supplier.products.split("\n").map((p, i) => (
                    <li key={`${p}-${i}`} className="rounded-lg bg-muted px-2.5 py-1 text-sm text-ink">
                      {p}
                    </li>
                  ))}
                </ul>
              </section>
            )}
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
              {[
                { label: "Заказов", value: String(supplier.orders.filter((o) => o.status !== "Cancelled").length) },
                { label: "Сумма заказов, сум", value: money(supplier.ordersTotal) },
                { label: "Просрочено сейчас", value: String(r.overdueOrders), tone: r.overdueOrders > 0 ? "text-bad" : "" },
                { label: "Предложений", value: String(supplier.offers) },
              ].map((x) => (
                <div key={x.label} className="rounded-xl border border-line bg-surface p-4">
                  <div className={`text-xl font-semibold tabular-nums text-ink ${x.tone ?? ""}`}>{x.value}</div>
                  <div className="mt-0.5 text-xs text-ink-3">{t(x.label)}</div>
                </div>
              ))}
            </div>

            <section className={card}>
              <h2 className="mb-3 text-base font-semibold text-ink">{t("История заказов")}</h2>
              {supplier.orders.length === 0 ? (
                <p className="text-sm text-ink-3">{t("Заказов у этого поставщика ещё не было.")}</p>
              ) : (
                <ul className="divide-y divide-line">
                  {supplier.orders.map((o) => (
                    <li key={o.id}>
                      <Link href={`/procurement/orders/${o.id}`} className="flex flex-wrap items-center gap-x-3 gap-y-1 py-2.5 text-sm hover:text-accent-strong">
                        <span className="w-32 font-medium tabular-nums text-ink">{o.number}</span>
                        <OrderStatusPill status={o.status} />
                        <span className="flex-1 text-xs text-ink-3">
                          {o.orderDate ? t("заказ {date}", { date: dateRu(o.orderDate) }) : ""}
                          {o.expectedDate && <span className={o.overdue ? " font-semibold text-bad" : ""}> · {t("ожидается {date}", { date: dateRu(o.expectedDate) })}</span>}
                        </span>
                        <span className="tabular-nums text-ink">{money(o.total)}</span>
                      </Link>
                    </li>
                  ))}
                </ul>
              )}
            </section>

            <section className={card}>
              <h2 className="mb-3 text-base font-semibold text-ink">{t("История цен")}</h2>
              {supplier.prices.length === 0 ? (
                <p className="text-sm text-ink-3">{t("Цены появятся с первыми заказами.")}</p>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full min-w-[520px] text-sm">
                    <thead>
                      <tr className="whitespace-nowrap border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                        <th className="py-2 pr-3 font-semibold">{t("Дата")}</th>
                        <th className="py-2 pr-3 font-semibold">{t("Позиция")}</th>
                        <th className="py-2 pr-3 text-right font-semibold">{t("Кол-во")}</th>
                        <th className="py-2 pr-3 text-right font-semibold">{t("Цена, сум")}</th>
                        <th className="py-2 font-semibold">{t("Заказ")}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {supplier.prices.map((p, i) => (
                        <tr key={`${p.orderId}-${p.requestItemId}-${i}`} className="border-b border-line last:border-0">
                          <td className="py-2 pr-3 tabular-nums text-ink-2">{dateRu(p.date)}</td>
                          <td className="py-2 pr-3 text-ink">{p.itemName}</td>
                          <td className="whitespace-nowrap py-2 pr-3 text-right tabular-nums">
                            {quantity(p.quantity)} {p.unitCode}
                          </td>
                          <td className="whitespace-nowrap py-2 pr-3 text-right tabular-nums">{money(p.unitPrice)}</td>
                          <td className="py-2">
                            <Link href={`/procurement/orders/${p.orderId}`} className="tabular-nums text-accent-strong hover:underline">
                              {p.orderNumber}
                            </Link>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </section>

            {supplier.claims.length > 0 && (
              <section className={`${card} ${supplier.claims.some((c) => c.status === "Open") ? "border-bad/30" : ""}`}>
                <h2 className="mb-1 text-base font-semibold text-ink">{t("Претензии")}</h2>
                <ClaimsBlock claims={supplier.claims} canResolve={supplier.canEdit} showOrder />
              </section>
            )}

            {overdue.length > 0 && (
              <section className={`${card} border-bad/30`}>
                <h2 className="mb-2 flex items-center gap-2 text-base font-semibold text-bad">
                  <AlertTriangle className="size-4" aria-hidden /> {t("Просроченные поставки")}
                </h2>
                <ul className="space-y-1 text-sm">
                  {overdue.map((o) => (
                    <li key={o.id}>
                      <Link href={`/procurement/orders/${o.id}`} className="text-ink hover:text-accent-strong">
                        {o.number}
                      </Link>{" "}
                      <span className="text-ink-3">· {t("ожидался {date}", { date: dateRu(o.expectedDate) })}</span>
                    </li>
                  ))}
                </ul>
              </section>
            )}

            <section className={card}>
              <h2 className="mb-3 text-base font-semibold text-ink">{t("Документы")}</h2>
              <DocumentsBlock owner="supplier" id={supplier.id} documents={documents} can={supplier.canEdit} kinds={["contract", "act", "other"]} />
            </section>
          </div>

          <div className="space-y-4">
            <section className={card}>
              <h2 className="mb-3 flex items-center justify-between gap-2 text-base font-semibold text-ink">
                {t("Рейтинг")} <RatingBadge rating={r.overall} />
              </h2>
              <div className="space-y-3">
                <Part
                  label="Сроки"
                  value={r.onTime}
                  hint={r.dueOrders > 0 ? t("заказов со сроком: {a}, с опозданием: {b}", { a: r.dueOrders, b: r.lateOrders }) : t("срок ещё не наступал")}
                />
                <Part label="Качество" value={r.quality} hint={r.ratedOffers > 0 ? t("оценок в предложениях: {n}", { n: r.ratedOffers }) : t("качество в предложениях не оценивали")} />
                <Part label="Соблюдение цен" value={r.prices} hint={r.pricedLines > 0 ? t("строк заказов из предложений: {n}", { n: r.pricedLines }) : t("заказов из предложений ещё нет")} />
                <Part
                  label="Без претензий"
                  value={r.claims}
                  hint={r.receiptLines > 0 ? t("строк приёмки: {a}, с браком или недостачей: {b}", { a: r.receiptLines, b: r.claimLines }) : t("приёмок ещё не было")}
                />
                <Part
                  label="Полнота поставок"
                  value={r.completeness}
                  hint={r.finishedOrders > 0 ? t("поставленных заказов: {n}", { n: r.finishedOrders }) : t("поставленных заказов ещё нет")}
                />
              </div>
            </section>

            <section className={card}>
              <h2 className="mb-1 text-base font-semibold text-ink">{t("Реквизиты и контакты")}</h2>
              <dl className="divide-y divide-line">
                {supplier.legalName && <Fact label="Фирма">{supplier.legalName}</Fact>}
                <Fact label="Контактное лицо">{supplier.contactPerson ?? "—"}</Fact>
                <Fact label="Телефон">{supplier.phone ? <a href={`tel:${supplier.phone}`} className="text-accent-strong">{supplier.phone}</a> : "—"}</Fact>
                <Fact label="Email">{supplier.email ? <a href={`mailto:${supplier.email}`} className="text-accent-strong">{supplier.email}</a> : "—"}</Fact>
                <Fact label="Адрес">{supplier.address ?? "—"}</Fact>
                <Fact label="Банк">{supplier.bankName ?? "—"}</Fact>
                <Fact label="МФО">{supplier.bankMfo ?? "—"}</Fact>
                <Fact label="Расчётный счёт">
                  <span className="tabular-nums">{supplier.bankAccount ?? "—"}</span>
                </Fact>
                <Fact label="Условия оплаты">{supplier.paymentTerms ?? "—"}</Fact>
                <Fact label="Срок поставки">{supplier.leadTimeDays !== null ? t("{n} дн.", { n: supplier.leadTimeDays }) : "—"}</Fact>
              </dl>
              {supplier.categories.length > 0 && (
                <div className="mt-3 flex flex-wrap gap-1.5">
                  {supplier.categories.map((c) => (
                    <span key={c.id} className="rounded-full bg-muted px-2.5 py-1 text-xs text-ink-2">
                      {c.name}
                    </span>
                  ))}
                </div>
              )}
              {supplier.notes && <p className="mt-3 whitespace-pre-line rounded-lg bg-muted px-3 py-2 text-sm text-ink-2">{supplier.notes}</p>}
            </section>

          </div>
        </div>
      </PageBody>
      <SupplierFormSheet open={editing} onClose={() => setEditing(false)} categories={categories} supplier={supplier} />
    </>
  );
}
