"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { AlertTriangle, Ban, CircleCheckBig, History, Loader2, PackagePlus, Pencil } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { TenderDeliverySheet } from "@/components/procurement/TenderDeliverySheet";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { dateRu, money, quantity, stampRu, type TenderDetail, type Warehouse } from "@/lib/procurement";
import { todayInTashkent } from "@/lib/tasks";
import { DocumentsBlock } from "./ProcKit";
import { TenderStatusPill } from "./TendersView";

const card = "rounded-2xl border border-line bg-surface p-4 sm:p-5";
const secondary = "inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3.5 text-sm font-medium text-ink hover:bg-muted disabled:opacity-50";
const primary = "inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50";

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
 * Тендерная закупка: предмет, победитель, позиции и сумма, договор, срок и факт поставки, документы (протокол, договор, счёт) и история
 * правок. Снабжение изменяет запись, отмечает поставку и отменяет ошибочную запись с причиной. Тендер исполняет заявку и поставляется на
 * склад: кладовщик принимает поставку партиями («Принять на склад»), у позиций растёт «поставлено»; приходы — в «Поставках на склад».
 */
export function TenderDetailView({
  tender,
  warehouses = [],
  seesStock = false,
  seesRequests = false,
}: {
  tender: TenderDetail;
  warehouses?: Warehouse[];
  seesStock?: boolean;
  /** Карточка заявки открывается снабжению и директору; кладовщику — номер без ссылки. */
  seesRequests?: boolean;
}) {
  const t = useT();
  const router = useRouter();
  const [dialog, setDialog] = useState<"delivered" | "cancel" | null>(null);
  const [receiving, setReceiving] = useState(false);
  const stocked = tender.items.some((i) => i.itemId);
  const [date, setDate] = useState(todayInTashkent());
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const can = tender.can;

  const confirm = async () => {
    if (!dialog) return;
    if (dialog === "cancel" && !note.trim()) return setError(t("Напишите, почему запись отменяется."));
    setBusy(true);
    setError(null);
    try {
      await bff(`procurement/tenders/${tender.id}/${dialog}`, {
        method: "POST",
        body: JSON.stringify(dialog === "delivered" ? { date } : { comment: note.trim() }),
      });
      setDialog(null);
      setNote("");
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  const actions = (
    <div className="flex flex-wrap items-center gap-2">
      {can.cancel && (
        <button type="button" onClick={() => setDialog("cancel")} className={`${secondary} text-bad`}>
          <Ban className="size-4" aria-hidden /> {t("Отменить")}
        </button>
      )}
      {can.edit && (
        <Link href={`/procurement/tenders/${tender.id}/edit`} className={secondary}>
          <Pencil className="size-4" aria-hidden /> {t("Изменить")}
        </Link>
      )}
      {can.deliver && (
        <button type="button" onClick={() => setDialog("delivered")} className={can.receive ? secondary : primary}>
          <CircleCheckBig className="size-4" aria-hidden /> {t("Поставлено")}
        </button>
      )}
      {can.receive && (
        <button type="button" onClick={() => setReceiving(true)} className={primary}>
          <PackagePlus className="size-4" aria-hidden /> {t("Принять на склад")}
        </button>
      )}
    </div>
  );

  return (
    <>
      <PageHeader
        title={tender.title}
        subtitle={
          <span className="inline-flex flex-wrap items-center gap-2">
            <span className="tabular-nums">{tender.number}</span>
            <TenderStatusPill status={tender.status} />
            <span className="tabular-nums">
              {money(tender.total)} {t("сум")}
            </span>
            {tender.overdue && (
              <span className="inline-flex items-center gap-1 rounded-md bg-bad-soft px-1.5 py-0.5 text-xs font-semibold text-bad">
                <AlertTriangle className="size-3" aria-hidden /> {t("поставка просрочена")}
              </span>
            )}
          </span>
        }
        back="/procurement/tenders"
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Отделы"), href: "/departments" }, { label: t("Снабжение"), href: "/procurement" }, { label: t("Тендеры"), href: "/procurement/tenders" }, { label: tender.number }]}
        actions={<span className="max-lg:hidden">{actions}</span>}
      />
      <PageBody>
        <div className="mx-auto max-w-6xl space-y-4">
          <div className="lg:hidden">{actions}</div>
          <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_340px]">
            <div className="min-w-0 space-y-4">
              <section className={card} aria-labelledby="tn-items">
                <h2 id="tn-items" className="mb-3 text-base font-semibold text-ink">
                  {t("Позиции")} <span className="font-normal text-ink-3">· {tender.items.length}</span>
                </h2>
                <div className="overflow-x-auto">
                  <table className="w-full min-w-[520px] text-sm">
                    <thead>
                      <tr className="whitespace-nowrap border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                        <th className="py-2 pr-3 font-semibold">№</th>
                        <th className="py-2 pr-3 font-semibold">{t("Наименование")}</th>
                        <th className="py-2 pr-3 text-right font-semibold">{t("Кол-во")}</th>
                        {stocked && <th className="py-2 pr-3 text-right font-semibold">{t("Поставлено")}</th>}
                        <th className="py-2 pr-3 text-right font-semibold">{t("Цена, сум")}</th>
                        <th className="py-2 text-right font-semibold">{t("Сумма")}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {tender.items.map((i) => (
                        <tr key={i.id} className="border-b border-line align-top last:border-0">
                          <td className="py-2.5 pr-3 tabular-nums text-ink-3">{i.lineNo}</td>
                          <td className="py-2.5 pr-3 text-ink">
                            {i.name}
                            {i.itemId ? (
                              <span className="block text-[11px] text-ink-3">
                                <span className="tabular-nums">{i.sku}</span>
                                {i.factor == null
                                  ? ` · ${t("единица «{u}» не задана у карточки — на склад не принять", { u: i.unitCode })}`
                                  : i.factor !== 1 && ` · ${t("1 {a} = {f} {b}", { a: i.unitCode, f: quantity(i.factor), b: i.itemUnitCode ?? "" })}`}
                              </span>
                            ) : (
                              stocked && <span className="block text-[11px] text-ink-3">{t("без карточки склада")}</span>
                            )}
                          </td>
                          <td className="whitespace-nowrap py-2.5 pr-3 text-right tabular-nums">
                            {quantity(i.quantity)} {i.unitCode}
                          </td>
                          {stocked && (
                            <td className={`whitespace-nowrap py-2.5 pr-3 text-right tabular-nums ${i.itemId && (i.left ?? 0) === 0 ? "text-ok" : "text-ink-2"}`}>
                              {i.itemId ? quantity(i.receivedQty ?? 0) : "—"}
                            </td>
                          )}
                          <td className="whitespace-nowrap py-2.5 pr-3 text-right tabular-nums">{money(i.price)}</td>
                          <td className="whitespace-nowrap py-2.5 text-right tabular-nums">{money(i.sum)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <p className="mt-3 border-t border-line pt-3 text-right text-sm text-ink-2">
                  {t("Итого")} <span className="font-semibold tabular-nums text-ink">{money(tender.total)}</span> {t("сум")}
                </p>
              </section>

              {(tender.deliveries?.length ?? 0) > 0 && (
                <section className={card} aria-labelledby="tn-deliveries">
                  <h2 id="tn-deliveries" className="mb-3 text-base font-semibold text-ink">
                    {t("Поставки на склад")} <span className="font-normal text-ink-3">· {tender.deliveries!.length}</span>
                  </h2>
                  <ul className="divide-y divide-line">
                    {tender.deliveries!.map((d) => (
                      <li key={d.documentId} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm">
                        <span className="min-w-0">
                          {seesStock ? (
                            <Link href={`/procurement/movements?q=${encodeURIComponent(d.number)}`} className="font-medium tabular-nums text-accent-strong hover:underline">
                              {d.number}
                            </Link>
                          ) : (
                            <span className="font-medium tabular-nums text-ink">{d.number}</span>
                          )}
                          <span className="text-ink-3">
                            {" "}
                            · {stampRu(d.at)}
                            {d.reference && ` · ${t("накладная {n}", { n: d.reference })}`}
                            {d.actorName && ` · ${d.actorName}`}
                          </span>
                        </span>
                        <span className="text-xs text-ink-3">{t("позиций: {n}", { n: d.lines })}</span>
                      </li>
                    ))}
                  </ul>
                </section>
              )}

              <section className={card}>
                <h2 className="mb-3 text-base font-semibold text-ink">{t("Документы")}</h2>
                <DocumentsBlock owner="tender" id={tender.id} documents={tender.documents} can={can.attach} kinds={["protocol", "contract", "invoice", "waybill", "other"]} />
              </section>
            </div>

            <div className="space-y-4">
              <section className={card}>
                <h2 className="mb-1 text-base font-semibold text-ink">{t("Сведения")}</h2>
                <dl className="divide-y divide-line">
                  <Fact label="Победитель">
                    <Link href={`/procurement/suppliers/${tender.supplierId}`} className="text-accent-strong hover:underline">
                      {tender.supplierName}
                    </Link>
                  </Fact>
                  <Fact label="Дата тендера">{dateRu(tender.tenderDate)}</Fact>
                  {tender.tenderNo && <Fact label="Номер тендера">{tender.tenderNo}</Fact>}
                  {tender.participants !== null && <Fact label="Участников">{tender.participants}</Fact>}
                  <Fact label="Категория">{tender.categoryName}</Fact>
                  {tender.requestId && (
                    <Fact label="Заявка">
                      {seesRequests ? (
                        <Link href={`/requests/${tender.requestId}`} className="text-accent-strong hover:underline">
                          {tender.requestNumber}
                        </Link>
                      ) : (
                        <span className="tabular-nums">{tender.requestNumber}</span>
                      )}
                      {tender.requestTitle && <span className="block text-xs text-ink-3">{tender.requestTitle}</span>}
                    </Fact>
                  )}
                  {stocked && <Fact label="Склад поставки">{tender.warehouseName ?? t("по умолчанию")}</Fact>}
                  {tender.contractNo && <Fact label="Договор">{tender.contractNo}</Fact>}
                  <Fact label="Срок поставки">
                    <span className={tender.overdue ? "font-semibold text-bad" : ""}>{dateRu(tender.deliveryDate)}</span>
                  </Fact>
                  {tender.deliveredOn && <Fact label="Поставлено">{dateRu(tender.deliveredOn)}</Fact>}
                  {tender.cancelledAt && <Fact label="Отменена">{stampRu(tender.cancelledAt)}</Fact>}
                  <Fact label="Вёл тендер">{tender.responsibleName}</Fact>
                  <Fact label="Запись добавлена">{stampRu(tender.createdAt)}</Fact>
                </dl>
                {tender.comment && <p className="mt-2 whitespace-pre-line rounded-lg bg-muted px-3 py-2 text-sm text-ink-2">{tender.comment}</p>}
              </section>

              <section className={card}>
                <h2 className="mb-3 flex items-center gap-2 text-base font-semibold text-ink">
                  <History className="size-4 text-ink-3" aria-hidden /> {t("История")}
                </h2>
                <ol className="relative space-y-4 border-l border-line pl-4">
                  {[...tender.history].reverse().map((e) => (
                    <li key={e.id} className="relative">
                      <span className="absolute -left-[21px] top-1.5 size-2.5 rounded-full bg-ink-4 ring-4 ring-surface" aria-hidden />
                      <p className="break-words text-sm text-ink">{t(e.text)}</p>
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

      {receiving && <TenderDeliverySheet tender={tender} warehouses={warehouses} onClose={() => setReceiving(false)} />}

      <Sheet
        open={dialog !== null}
        onClose={() => {
          setDialog(null);
          setError(null);
        }}
        title={dialog === "delivered" ? t("Отметить поставку") : t("Отменить запись")}
        subtitle={`${tender.number} · ${tender.title}`}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" onClick={() => setDialog(null)} className={secondary}>
              {t("Закрыть")}
            </button>
            <button type="button" onClick={() => void confirm()} disabled={busy} className={dialog === "cancel" ? `${primary} bg-bad hover:bg-bad` : primary}>
              {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}
              {dialog === "delivered" ? t("Поставлено") : t("Отменить запись")}
            </button>
          </div>
        }
      >
        {dialog === "delivered" ? (
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Дата поставки")}</span>
            <input type="date" value={date} min={tender.tenderDate} max={todayInTashkent()} onChange={(e) => setDate(e.target.value)} className="h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink" />
            {stocked && (
              <span className="mt-2 block text-xs text-ink-3">
                {t("Тендер закроется без прихода на склад. Если товар пришёл на склад — его принимает кладовщик кнопкой «Принять на склад».")}
              </span>
            )}
          </label>
        ) : (
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Почему отменяется")}</span>
            <textarea
              value={note}
              onChange={(e) => setNote(e.target.value)}
              maxLength={2000}
              rows={4}
              autoFocus
              className="w-full rounded-lg border border-line bg-surface px-3 py-2.5 text-sm text-ink focus:border-accent focus:outline-none"
            />
            <span className="mt-2 block text-xs text-ink-3">{t("Запись останется в журнале с пометкой «Отменена» и в сумму не войдёт.")}</span>
          </label>
        )}
        <div className="mt-3">
          <ErrorNote text={error} />
        </div>
      </Sheet>
    </>
  );
}
