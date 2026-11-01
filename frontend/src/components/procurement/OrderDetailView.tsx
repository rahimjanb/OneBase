"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { AlertTriangle, Archive, BadgeCheck, Ban, Check, Gavel, History, ListChecks, Loader2, OctagonX, PackageCheck, PackageX, Pencil, Send, Truck, Undo2 } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { approvalDecisionLabel, dateRu, money, quantity, stampRu, type OrderDetail } from "@/lib/procurement";
import { todayInTashkent } from "@/lib/tasks";
import { DocumentsBlock, OrderStatusPill } from "./ProcKit";
import { ClaimsBlock, ReceiptStatusPill } from "./ReceiptKit";

const card = "rounded-2xl border border-line bg-surface p-4 sm:p-5";
const secondary = "inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3.5 text-sm font-medium text-ink hover:bg-muted disabled:opacity-50";
const primary = "inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50";

type Kind = "approve" | "reject" | "return" | "cancel" | "ordered" | "remainder";
const dialogs: Record<Kind, { title: string; button: string; label: string; required: boolean; path: string }> = {
  approve: { title: "Утвердить заказ", button: "Утвердить", label: "Комментарий", required: false, path: "approve" },
  reject: { title: "Отклонить заказ", button: "Отклонить", label: "Почему отклонён", required: true, path: "reject" },
  return: { title: "Вернуть на доработку", button: "Вернуть", label: "Что исправить", required: true, path: "return" },
  cancel: { title: "Отменить заказ", button: "Отменить заказ", label: "Причина отмены", required: true, path: "cancel" },
  ordered: { title: "Заказ отправлен поставщику", button: "Отметить заказанным", label: "Дата заказа", required: false, path: "ordered" },
  remainder: { title: "Закрыть недопоставленный остаток", button: "Закрыть остаток", label: "Почему остаток не будет поставлен", required: true, path: "close-remainder" },
};

const steps = ["Draft", "Approved", "Ordered", "Delivered", "Completed"] as const;
const stepLabel: Record<(typeof steps)[number], string> = { Draft: "Черновик", Approved: "Утверждён", Ordered: "Заказан", Delivered: "Поставлен", Completed: "Закрыт" };

function Fact({ label, children }: { label: string; children: React.ReactNode }) {
  const t = useT();
  return (
    <div className="flex items-start justify-between gap-4 py-2 text-sm">
      <dt className="shrink-0 text-ink-3">{t(label)}</dt>
      <dd className="min-w-0 text-right text-ink">{children}</dd>
    </div>
  );
}

/**
 * Карточка заказа поставщику: этапы, позиции (со ссылками на заявки, получено и закрыто), итоги (скидка, НДС, доставка), приёмки и
 * претензии, сведения, согласование директора, документы (договор, счёт, накладная, акт, платёжное поручение), история. Кнопки — только
 * разрешённые (сервер проверяет то же самое): снабжение — изменить, оформить, отметить заказанным, отменить, закрыть остаток, закрыть
 * заказ; директор — утвердить, отклонить, вернуть; кладовщик — принять товар.
 */
export function OrderDetailView({ order }: { order: OrderDetail }) {
  const t = useT();
  const router = useRouter();
  const [dialog, setDialog] = useState<Kind | null>(null);
  const [note, setNote] = useState("");
  const [date, setDate] = useState(todayInTashkent());
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const can = order.can;
  const required = (kind: Kind) => dialogs[kind].required && !(kind === "cancel" && order.status === "Draft");

  const post = async (key: string, path: string, body?: object) => {
    setBusy(key);
    setError(null);
    try {
      await bff(`procurement/orders/${order.id}/${path}`, { method: "POST", body: body ? JSON.stringify(body) : undefined });
      router.refresh();
      return true;
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      return false;
    } finally {
      setBusy(null);
    }
  };

  const confirm = async () => {
    if (!dialog) return;
    if (required(dialog) && !note.trim()) {
      setError(t("Напишите комментарий."));
      return;
    }
    const ok = await post(dialog, dialogs[dialog].path, dialog === "ordered" ? { orderDate: date } : { comment: note.trim() || null });
    if (ok) {
      setDialog(null);
      setNote("");
    }
  };

  const current = order.status === "AwaitingApproval" ? 1 : order.status === "PartiallyDelivered" ? 3 : steps.indexOf(order.status as (typeof steps)[number]);
  const actions = (
    <div className="flex flex-wrap items-center gap-2">
      {can.edit && (
        <Link href={`/procurement/orders/${order.id}/edit`} className={secondary}>
          <Pencil className="size-4" aria-hidden /> {t("Изменить")}
        </Link>
      )}
      {can.cancel && (
        <button type="button" onClick={() => setDialog("cancel")} className={`${secondary} text-bad`}>
          <Ban className="size-4" aria-hidden /> {t("Отменить")}
        </button>
      )}
      {can.return && (
        <button type="button" onClick={() => setDialog("return")} className={secondary}>
          <Undo2 className="size-4" aria-hidden /> {t("Вернуть на доработку")}
        </button>
      )}
      {can.approve && (
        <button type="button" onClick={() => setDialog("reject")} className={`${secondary} text-bad`}>
          <OctagonX className="size-4" aria-hidden /> {t("Отклонить")}
        </button>
      )}
      {can.approve && (
        <button type="button" onClick={() => setDialog("approve")} className={primary}>
          <BadgeCheck className="size-4" aria-hidden /> {t("Утвердить")}
        </button>
      )}
      {can.submit && (
        <button type="button" onClick={() => void post("submit", "submit")} disabled={busy !== null} className={primary}>
          {busy === "submit" ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Send className="size-4" aria-hidden />} {t("Оформить заказ")}
        </button>
      )}
      {can.markOrdered && (
        <button type="button" onClick={() => setDialog("ordered")} className={primary}>
          <Truck className="size-4" aria-hidden /> {t("Отправлен поставщику")}
        </button>
      )}
      {can.closeRemainder && (
        <button type="button" onClick={() => setDialog("remainder")} className={secondary}>
          <PackageX className="size-4" aria-hidden /> {t("Закрыть остаток")}
        </button>
      )}
      {can.complete && (
        <button type="button" onClick={() => void post("complete", "complete")} disabled={busy !== null} className={secondary}>
          {busy === "complete" ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Archive className="size-4" aria-hidden />} {t("Закрыть заказ")}
        </button>
      )}
      {can.receive && (
        <Link href={`/procurement/receipts/new?order=${order.id}`} className={primary}>
          <PackageCheck className="size-4" aria-hidden /> {t("Принять товар")}
        </Link>
      )}
    </div>
  );

  return (
    <>
      <PageHeader
        title={`${order.number} · ${order.supplierName}`}
        subtitle={
          <span className="inline-flex flex-wrap items-center gap-2">
            <OrderStatusPill status={order.status} />
            <span className="tabular-nums">
              {money(order.total)} {t("сум")}
            </span>
            {order.overdue && (
              <span className="inline-flex items-center gap-1 rounded-md bg-bad-soft px-1.5 py-0.5 text-xs font-semibold text-bad">
                <AlertTriangle className="size-3" aria-hidden /> {t("просрочен")}
              </span>
            )}
          </span>
        }
        back="/procurement/orders"
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Снабжение"), href: "/procurement" }, { label: t("Заказы"), href: "/procurement/orders" }, { label: order.number }]}
        actions={<span className="max-lg:hidden">{actions}</span>}
      />
      <PageBody>
        <div className="mx-auto max-w-6xl space-y-4">
          <div className="lg:hidden">{actions}</div>
          <ErrorNote text={dialog ? null : error} />
          {order.status === "AwaitingApproval" && (
            <div className="rounded-2xl border border-violet/30 bg-violet-soft px-4 py-3 text-sm text-ink">
              <div className="font-semibold text-violet-light">
                {order.approvalRound > 1 ? t("Повторное согласование (раунд {n})", { n: order.approvalRound }) : t("Ждёт решения директора")}
              </div>
              {order.approvalReasons && <p className="mt-1">{order.approvalReasons}</p>}
            </div>
          )}
          {order.status === "Cancelled" ? (
            <section className={`${card} flex items-center gap-2 text-sm text-ink-3`}>
              <Ban className="size-4" aria-hidden /> {t("Заказ отменён")}
            </section>
          ) : (
            <section className={card}>
              <ol className="grid grid-cols-5 gap-1.5" aria-label={t("Этапы заказа")}>
                {steps.map((s, i) => {
                  const done = i < current;
                  const on = i === current;
                  const text = on && order.status === "AwaitingApproval" ? "Ждёт директора" : on && order.status === "PartiallyDelivered" ? "Поставлен частично" : stepLabel[s];
                  return (
                    <li key={s} className="min-w-0" aria-current={on ? "step" : undefined}>
                      <div className={`h-1.5 rounded-full ${done ? "bg-ok" : on ? (order.status === "AwaitingApproval" ? "bg-violet" : "bg-accent") : "bg-muted"}`} />
                      <div className={`mt-1.5 flex items-center gap-1 truncate text-[11px] sm:text-xs ${on ? "font-semibold text-ink" : done ? "text-ink-2" : "text-ink-3"}`}>
                        {done && <Check className="size-3 shrink-0 text-ok" aria-hidden />}
                        <span className="truncate">{t(text)}</span>
                      </div>
                    </li>
                  );
                })}
              </ol>
            </section>
          )}

          <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_340px]">
            <div className="min-w-0 space-y-4">
              <section className={card} aria-labelledby="po-items">
                <h2 id="po-items" className="mb-3 text-base font-semibold text-ink">
                  {t("Позиции")} <span className="font-normal text-ink-3">· {order.items.length}</span>
                </h2>
                <div className="overflow-x-auto">
                  <table className="w-full min-w-[600px] text-sm">
                    <thead>
                      <tr className="whitespace-nowrap border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                        <th className="py-2 pr-3 font-semibold">№</th>
                        <th className="py-2 pr-3 font-semibold">{t("Наименование")}</th>
                        <th className="py-2 pr-3 text-right font-semibold">{t("Кол-во")}</th>
                        <th className="py-2 pr-3 text-right font-semibold">{t("Цена, сум")}</th>
                        <th className="py-2 pr-3 text-right font-semibold">{t("Сумма")}</th>
                        <th className="py-2 text-right font-semibold">{t("Получено")}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {order.items.map((i) => (
                        <tr key={i.id} className="border-b border-line align-top last:border-0">
                          <td className="py-2.5 pr-3 tabular-nums text-ink-3">{i.lineNo}</td>
                          <td className="py-2.5 pr-3">
                            <span className="text-ink">{i.name}</span>
                            <Link href={`/requests/${i.requestId}`} className="block text-xs text-accent-strong hover:underline">
                              {i.requestNumber}
                            </Link>
                          </td>
                          <td className="whitespace-nowrap py-2.5 pr-3 text-right tabular-nums">
                            {quantity(i.quantity)} {i.unitCode}
                          </td>
                          <td className="whitespace-nowrap py-2.5 pr-3 text-right tabular-nums">
                            {money(i.unitPrice)}
                            {i.estimatedPrice !== null && i.estimatedPrice !== i.unitPrice && (
                              <span className={`block text-[11px] ${i.unitPrice > i.estimatedPrice ? "text-violet-light" : "text-ink-3"}`}>
                                {t("в заявке {p}", { p: money(i.estimatedPrice) })}
                              </span>
                            )}
                          </td>
                          <td className="whitespace-nowrap py-2.5 pr-3 text-right tabular-nums">{money(i.sum)}</td>
                          <td className="whitespace-nowrap py-2.5 text-right tabular-nums text-ink-2">
                            {i.receivedQty > 0 ? quantity(i.receivedQty) : "—"}
                            {i.closedQty > 0 && <span className="block text-[11px] text-warn">{t("закрыто {n}", { n: quantity(i.closedQty) })}</span>}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <dl className="ml-auto mt-3 max-w-xs space-y-1 border-t border-line pt-3 text-sm">
                  <div className="flex justify-between gap-3">
                    <dt className="text-ink-3">{t("Позиции")}</dt>
                    <dd className="tabular-nums">{money(order.subtotal)}</dd>
                  </div>
                  {order.discount > 0 && (
                    <div className="flex justify-between gap-3">
                      <dt className="text-ink-3">{t("Скидка")}</dt>
                      <dd className="tabular-nums">−{money(order.discount)}</dd>
                    </div>
                  )}
                  <div className="flex justify-between gap-3">
                    <dt className="text-ink-3">{order.pricesIncludeVat ? t("НДС {n}% в цене", { n: order.vatRate }) : t("НДС {n}%", { n: order.vatRate })}</dt>
                    <dd className="tabular-nums">{order.pricesIncludeVat ? "—" : money(order.vatAmount)}</dd>
                  </div>
                  {order.deliveryCost > 0 && (
                    <div className="flex justify-between gap-3">
                      <dt className="text-ink-3">{t("Доставка")}</dt>
                      <dd className="tabular-nums">{money(order.deliveryCost)}</dd>
                    </div>
                  )}
                  <div className="flex justify-between gap-3 pt-1 text-base font-semibold">
                    <dt>{t("Итог")}</dt>
                    <dd className="tabular-nums">
                      {money(order.total)} {t("сум")}
                    </dd>
                  </div>
                </dl>
              </section>

              {(order.receipts.length > 0 || can.receive) && (
                <section className={card} aria-labelledby="po-receipts">
                  <h2 id="po-receipts" className="mb-2 text-base font-semibold text-ink">
                    {t("Приёмки")} <span className="font-normal text-ink-3">· {order.receipts.length}</span>
                  </h2>
                  {order.receipts.length === 0 ? (
                    <p className="text-sm text-ink-3">{t("Товар по заказу ещё не принимали.")}</p>
                  ) : (
                    <ul className="divide-y divide-line">
                      {order.receipts.map((r) => (
                        <li key={r.id}>
                          <Link href={`/procurement/receipts/${r.id}`} className="flex flex-wrap items-center gap-x-3 gap-y-1 py-2.5 text-sm hover:text-accent-strong">
                            <span className="w-28 font-medium tabular-nums text-ink">{r.number}</span>
                            <ReceiptStatusPill status={r.status} />
                            <span className="flex-1 text-xs text-ink-3">
                              {stampRu(r.postedAt ?? r.createdAt)}
                              {r.actorName && ` · ${r.actorName}`}
                              {r.reference && ` · ${t("накладная {n}", { n: r.reference })}`}
                            </span>
                            {r.problems && <span className="text-xs font-semibold text-bad">{t("брак или недостача")}</span>}
                          </Link>
                        </li>
                      ))}
                    </ul>
                  )}
                </section>
              )}

              {order.claims.length > 0 && (
                <section className={`${card} ${order.claims.some((c) => c.status === "Open") ? "border-bad/30" : ""}`}>
                  <h2 className="mb-1 text-base font-semibold text-ink">{t("Претензии поставщику")}</h2>
                  <ClaimsBlock claims={order.claims} canResolve={can.resolveClaims} />
                </section>
              )}

              <section className={card}>
                <h2 className="mb-3 text-base font-semibold text-ink">{t("Документы")}</h2>
                <DocumentsBlock owner="order" id={order.id} documents={order.documents} can={can.attach} kinds={["invoice", "contract", "waybill", "act", "payment", "other"]} />
              </section>
            </div>

            <div className="space-y-4">
              <section className={card}>
                <h2 className="mb-1 text-base font-semibold text-ink">{t("Сведения")}</h2>
                <dl className="divide-y divide-line">
                  <Fact label="Поставщик">
                    <Link href={`/procurement/suppliers/${order.supplierId}`} className="text-accent-strong hover:underline">
                      {order.supplierName}
                    </Link>
                    {order.supplierPhone && <span className="block text-xs text-ink-3">{order.supplierPhone}</span>}
                  </Fact>
                  <Fact label="Ответственный">{order.responsibleName}</Fact>
                  <Fact label="Дата заказа">{dateRu(order.orderDate)}</Fact>
                  <Fact label="Ожидается">
                    <span className={order.overdue ? "font-semibold text-bad" : ""}>{dateRu(order.expectedDate)}</span>
                  </Fact>
                  {order.paymentTerms && <Fact label="Условия оплаты">{order.paymentTerms}</Fact>}
                  <Fact label="Создан">
                    {stampRu(order.createdAt)}
                    {order.createdByName && <span className="block text-xs text-ink-3">{order.createdByName}</span>}
                  </Fact>
                  {order.approvedAt && (
                    <Fact label="Утверждён">
                      {stampRu(order.approvedAt)}
                      <span className="block text-xs text-ink-3">{order.approvedByName ?? t("в рамках утверждённой заявки")}</span>
                    </Fact>
                  )}
                  {order.deliveredAt && <Fact label="Поставлен">{stampRu(order.deliveredAt)}</Fact>}
                  {order.completedAt && <Fact label="Закрыт">{stampRu(order.completedAt)}</Fact>}
                  {order.taskId && (
                    <Fact label="Задача">
                      <Link href={`/tasks/task/${order.taskId}`} className="inline-flex items-center gap-1 text-accent-strong hover:underline">
                        <ListChecks className="size-3.5" aria-hidden /> {t("Открыть в «Задачах»")}
                      </Link>
                    </Fact>
                  )}
                </dl>
                {order.comment && <p className="mt-2 whitespace-pre-line rounded-lg bg-muted px-3 py-2 text-sm text-ink-2">{order.comment}</p>}
              </section>

              {order.approvals.length > 0 && (
                <section className={card}>
                  <h2 className="mb-3 flex items-center gap-2 text-base font-semibold text-ink">
                    <Gavel className="size-4 text-ink-3" aria-hidden /> {t("Согласование директора")}
                  </h2>
                  <ol className="space-y-3">
                    {order.approvals.map((a) => (
                      <li key={a.id} className="text-sm">
                        <span className={`font-medium ${a.decision === "Approved" ? "text-ok" : a.decision === "Rejected" ? "text-bad" : "text-warn"}`}>
                          {t(approvalDecisionLabel[a.decision])}
                        </span>
                        <span className="text-ink-3"> · {t("раунд {n}", { n: a.round })}</span>
                        {a.comment && <p className="mt-1 rounded-lg bg-muted px-2.5 py-1.5 text-ink-2">{a.comment}</p>}
                        <p className="mt-0.5 text-xs text-ink-3">
                          {a.deciderName} · {stampRu(a.createdAt)} · {money(a.total)} {t("сум")}
                        </p>
                      </li>
                    ))}
                  </ol>
                </section>
              )}

              <section className={card}>
                <h2 className="mb-3 flex items-center gap-2 text-base font-semibold text-ink">
                  <History className="size-4 text-ink-3" aria-hidden /> {t("История")}
                </h2>
                <ol className="relative space-y-4 border-l border-line pl-4">
                  {[...order.history].reverse().map((e) => (
                    <li key={e.id} className="relative">
                      <span className="absolute -left-[21px] top-1.5 size-2.5 rounded-full bg-ink-4 ring-4 ring-surface" aria-hidden />
                      <p className="text-sm text-ink">{t(e.text)}</p>
                      {e.comment && <p className="mt-1 whitespace-pre-line rounded-lg bg-muted px-2.5 py-1.5 text-sm text-ink-2">{e.comment}</p>}
                      <p className="mt-0.5 text-xs text-ink-3">
                        {e.actorName} · {stampRu(e.createdAt)}
                      </p>
                    </li>
                  ))}
                </ol>
              </section>
            </div>
          </div>
        </div>
      </PageBody>

      <Sheet
        open={dialog !== null}
        onClose={() => {
          setDialog(null);
          setError(null);
        }}
        title={dialog ? t(dialogs[dialog].title) : ""}
        subtitle={`${order.number} · ${order.supplierName}`}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" onClick={() => setDialog(null)} className={secondary}>
              {t("Закрыть")}
            </button>
            <button type="button" onClick={() => void confirm()} disabled={busy !== null} className={dialog === "reject" || dialog === "cancel" ? `${primary} bg-bad hover:bg-bad` : primary}>
              {busy === dialog && <Loader2 className="size-4 animate-spin" aria-hidden />}
              {dialog ? t(dialogs[dialog].button) : ""}
            </button>
          </div>
        }
      >
        {dialog === "remainder" && (
          <p className="mb-3 text-sm text-ink-2">
            {t("Недопоставленное перестанет числиться в заказе — его можно будет заказать у другого поставщика. Инициаторы заявок, директор и склад получат уведомление.")}
          </p>
        )}
        {dialog === "ordered" ? (
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Дата заказа")}</span>
            <input type="date" value={date} max={todayInTashkent()} onChange={(e) => setDate(e.target.value)} className="h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink" />
            <span className="mt-2 block text-xs text-ink-3">{t("Инициаторы заявок получат уведомление, что товар заказан.")}</span>
          </label>
        ) : (
          dialog && (
            <label className="block">
              <span className="mb-1.5 block text-sm font-medium text-ink">
                {t(dialogs[dialog].label)}
                {!required(dialog) && <span className="font-normal text-ink-3"> {t("необязательно")}</span>}
              </span>
              <textarea
                value={note}
                onChange={(e) => setNote(e.target.value)}
                maxLength={2000}
                rows={4}
                autoFocus
                className="w-full rounded-lg border border-line bg-surface px-3 py-2.5 text-sm text-ink focus:border-accent focus:outline-none"
              />
            </label>
          )
        )}
        <div className="mt-3">
          <ErrorNote text={error} />
        </div>
      </Sheet>
    </>
  );
}
