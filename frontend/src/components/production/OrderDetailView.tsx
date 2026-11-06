"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { AlertTriangle, Ban, CircleCheckBig, History, Loader2, PackageMinus, PackagePlus, Pencil, Rocket, Undo2 } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { batchOptionLabel, dateRu, parseNumber, quantity, stampRu, type BatchOption } from "@/lib/procurement";
import { deviation, percentLabel, requirement, type ProductionOrderDetail } from "@/lib/production";
import { Fact, OrderStatusPill, card, field, newKey, primary, secondary } from "./ProdKit";

type Dialog = "release" | "issue" | "return" | "complete" | "cancel" | null;
const text = (n: number) => String(n).replace(".", ",");

/**
 * Карточка задания на выпуск: потребность по нормам (снимок при выпуске), выдано, возвращено, расход и сколько осталось выдать; что на руках
 * у цеха по партиям; документы выдачи и возврата; после закрытия — норма на фактический выход и отклонение по каждой позиции. Производство
 * выпускает, закрывает и отменяет задание; кладовщик склада задания выдаёт сырьё (только годные партии, по умолчанию FEFO) и принимает возврат.
 */
export function OrderDetailView({ order }: { order: ProductionOrderDetail }) {
  const t = useT();
  const router = useRouter();
  const can = order.can;
  const [dialog, setDialog] = useState<Dialog>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const done = order.status === "Completed";

  const post = async (path: string, body: object) => {
    setBusy(true);
    setError(null);
    try {
      await bff(`production/orders/${order.id}/${path}`, { method: "POST", body: JSON.stringify(body) });
      setDialog(null);
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
        <Link href={`/production/orders/${order.id}/edit`} className={secondary}>
          <Pencil className="size-4" aria-hidden /> {t("Изменить")}
        </Link>
      )}
      {can.return && (
        <button type="button" onClick={() => setDialog("return")} className={secondary}>
          <Undo2 className="size-4" aria-hidden /> {t("Возврат на склад")}
        </button>
      )}
      {can.issue && (
        <button type="button" onClick={() => setDialog("issue")} className={can.complete ? secondary : primary}>
          <PackageMinus className="size-4" aria-hidden /> {t("Выдать сырьё")}
        </button>
      )}
      {can.release && (
        <button type="button" onClick={() => setDialog("release")} className={primary}>
          <Rocket className="size-4" aria-hidden /> {t("Выпустить")}
        </button>
      )}
      {can.complete && (
        <button type="button" onClick={() => setDialog("complete")} className={primary}>
          <CircleCheckBig className="size-4" aria-hidden /> {t("Закрыть задание")}
        </button>
      )}
    </div>
  );

  return (
    <>
      <PageHeader
        title={`${order.number} · ${order.recipeName}`}
        subtitle={
          <span className="inline-flex flex-wrap items-center gap-2">
            <OrderStatusPill status={order.status} />
            <span className="tabular-nums">
              {quantity(order.plannedQty)} {order.outputUnit} · {dateRu(order.plannedDate)}
            </span>
            {order.lines.some((l) => l.overconsumed) && (
              <span className="inline-flex items-center gap-1 rounded-md bg-bad-soft px-1.5 py-0.5 text-xs font-semibold text-bad">
                <AlertTriangle className="size-3" aria-hidden /> {t("перерасход больше {p} %", { p: quantity(order.thresholdPercent) })}
              </span>
            )}
          </span>
        }
        back="/production/orders"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Отделы"), href: "/departments" },
          { label: t("Производство"), href: "/production" },
          { label: t("Задания"), href: "/production/orders" },
          { label: order.number },
        ]}
        actions={<span className="max-lg:hidden">{actions}</span>}
      />
      <PageBody>
        <div className="mx-auto max-w-6xl space-y-4">
          <div className="lg:hidden">{actions}</div>
          {order.status === "Draft" && (
            <p className="rounded-xl border border-line bg-muted px-4 py-2.5 text-sm text-ink-2">
              {t("Черновик: проверьте план и склад и нажмите «Выпустить» — потребность зафиксируется, и кладовщик сможет выдать сырьё.")}
            </p>
          )}
          <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_340px]">
            <div className="min-w-0 space-y-4">
              <section className={card} aria-labelledby="po-lines">
                <h2 id="po-lines" className="mb-3 text-base font-semibold text-ink">
                  {done ? t("Расход и норма") : t("Сырьё по заданию")} <span className="font-normal text-ink-3">· {order.lines.length}</span>
                </h2>
                <div className="overflow-x-auto">
                  <table className="w-full min-w-[640px] text-sm">
                    <thead>
                      <tr className="whitespace-nowrap border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                        <th className="py-2 pr-3 font-semibold">{t("Позиция")}</th>
                        <th className="py-2 pr-3 text-right font-semibold">{t("Потребность")}</th>
                        <th className="py-2 pr-3 text-right font-semibold">{t("Выдано")}</th>
                        <th className="py-2 pr-3 text-right font-semibold">{t("Возвращено")}</th>
                        {done ? (
                          <>
                            <th className="py-2 pr-3 text-right font-semibold">{t("Норма на факт")}</th>
                            <th className="py-2 pr-3 text-right font-semibold">{t("Расход")}</th>
                            <th className="py-2 text-right font-semibold">{t("Отклонение")}</th>
                          </>
                        ) : (
                          <>
                            <th className="py-2 pr-3 text-right font-semibold">{t("Осталось выдать")}</th>
                            <th className="py-2 text-right font-semibold">{t("Годного на складе")}</th>
                          </>
                        )}
                      </tr>
                    </thead>
                    <tbody>
                      {order.lines.map((l) => (
                        <tr key={l.id} className={`border-b border-line align-top last:border-0 ${l.overconsumed ? "bg-bad-soft/40" : ""}`}>
                          <td className="py-2.5 pr-3 text-ink">
                            {l.name}
                            <span className="block text-[11px] text-ink-3">
                              <span className="tabular-nums">{l.sku}</span> · {t("норма {q} на {o}", { q: `${quantity(l.norm)} ${l.unitCode}`, o: `${quantity(order.recipeOutput)} ${order.outputUnit}` })}
                              {l.lossPercent > 0 && ` · ${t("потери {p} %", { p: quantity(l.lossPercent) })}`}
                            </span>
                          </td>
                          <td className="whitespace-nowrap py-2.5 pr-3 text-right tabular-nums">
                            {quantity(l.required)} {l.unitCode}
                          </td>
                          <td className="whitespace-nowrap py-2.5 pr-3 text-right tabular-nums">{quantity(l.issued)}</td>
                          <td className="whitespace-nowrap py-2.5 pr-3 text-right tabular-nums text-ink-2">{l.returned > 0 ? quantity(l.returned) : "—"}</td>
                          {done ? (
                            <>
                              <td className="whitespace-nowrap py-2.5 pr-3 text-right tabular-nums">{l.actualNorm === null ? "—" : quantity(l.actualNorm)}</td>
                              <td className="whitespace-nowrap py-2.5 pr-3 text-right tabular-nums">{quantity(l.consumed)}</td>
                              <td className={`whitespace-nowrap py-2.5 text-right tabular-nums ${l.overconsumed ? "font-semibold text-bad" : (l.deviation ?? 0) < 0 ? "text-ok" : "text-ink-2"}`}>
                                {l.deviation === null ? "—" : `${l.deviation > 0 ? "+" : ""}${quantity(l.deviation)}`}
                                <span className="block text-[11px]">{percentLabel(l.deviationPercent)}</span>
                              </td>
                            </>
                          ) : (
                            <>
                              <td className="whitespace-nowrap py-2.5 pr-3 text-right tabular-nums">{l.left > 0 ? quantity(l.left) : <span className="text-ok">{t("выдано")}</span>}</td>
                              <td className={`whitespace-nowrap py-2.5 text-right tabular-nums ${l.available < l.left ? "font-semibold text-bad" : "text-ink-2"}`}>{quantity(l.available)}</td>
                            </>
                          )}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                {done && (
                  <p className="mt-3 border-t border-line pt-3 text-xs text-ink-3">
                    {t("Норма на факт — норма рецептуры на фактический выход {q} с потерями; расход — выдано минус возвращено. Перерасход больше {p} % отмечен красным.", {
                      q: `${quantity(order.outputQty ?? 0)} ${order.outputUnit}`,
                      p: quantity(order.thresholdPercent),
                    })}
                  </p>
                )}
              </section>

              {order.holdings.length > 0 && (
                <section className={card} aria-labelledby="po-hold">
                  <h2 id="po-hold" className="mb-3 text-base font-semibold text-ink">{done ? t("Израсходовано по партиям") : t("Партии в производстве")}</h2>
                  <ul className="divide-y divide-line">
                    {order.holdings.map((h) => (
                      <li key={`${h.itemId}-${h.batchId}`} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm">
                        <span className="min-w-0">
                          <span className="block text-ink">{h.name}</span>
                          <span className="block text-xs text-ink-3">
                            {h.batchNumber ? [h.batchNumber, h.supplierBatchNo, h.expiresOn ? t("до {date}", { date: dateRu(h.expiresOn) }) : null].filter(Boolean).join(" · ") : t("без партий")}
                          </span>
                        </span>
                        <span className="text-right text-xs tabular-nums text-ink-2">
                          {t("выдано {a} · возвращено {b}", { a: quantity(h.issued), b: quantity(h.returned) })}
                          <span className="block font-semibold text-ink">
                            {done ? t("израсходовано {n}", { n: `${quantity(h.left)} ${h.unitCode}` }) : t("у цеха {n}", { n: `${quantity(h.left)} ${h.unitCode}` })}
                          </span>
                        </span>
                      </li>
                    ))}
                  </ul>
                </section>
              )}

              {order.documents.length > 0 && (
                <section className={card} aria-labelledby="po-docs">
                  <h2 id="po-docs" className="mb-3 text-base font-semibold text-ink">{t("Выдачи и возвраты")}</h2>
                  <ul className="divide-y divide-line">
                    {order.documents.map((d) => (
                      <li key={d.id} className="py-2 text-sm">
                        <span className="flex flex-wrap items-center gap-2">
                          {d.kind === "ProductionIssue" ? <PackageMinus className="size-4 text-ink-3" aria-hidden /> : <PackagePlus className="size-4 text-ok" aria-hidden />}
                          <span className="font-medium tabular-nums text-ink">{d.number}</span>
                          <span className="text-ink-3">
                            {d.kind === "ProductionIssue" ? t("выдача") : t("возврат")} · {stampRu(d.at)}
                            {d.actorName && ` · ${d.actorName}`}
                            {d.receiver && ` · ${t("получил")}: ${d.receiver}`}
                          </span>
                        </span>
                        <span className="mt-0.5 block text-xs text-ink-2">
                          {d.moves.map((m) => `${m.itemName} ${quantity(m.quantity)} ${m.unitCode}${m.batchNumber ? ` (${m.batchNumber})` : ""}`).join("; ")}
                        </span>
                        {d.comment && <span className="block text-xs text-ink-3">{d.comment}</span>}
                      </li>
                    ))}
                  </ul>
                </section>
              )}
            </div>

            <div className="space-y-4">
              <section className={card}>
                <h2 className="mb-1 text-base font-semibold text-ink">{t("Сведения")}</h2>
                <dl className="divide-y divide-line">
                  <Fact label="Рецептура">
                    <Link href={`/production/recipes/${order.recipeId}`} className="text-accent-strong hover:underline">
                      {order.recipeCode} v{order.recipeVersion}
                    </Link>
                    <span className="block text-xs text-ink-3">{order.outputName}</span>
                  </Fact>
                  <Fact label="План">
                    {quantity(order.plannedQty)} {order.outputUnit}
                  </Fact>
                  {order.outputQty !== null && (
                    <Fact label="Фактический выход">
                      {quantity(order.outputQty)} {order.outputUnit}
                    </Fact>
                  )}
                  <Fact label="Дата выпуска">{dateRu(order.plannedDate)}</Fact>
                  <Fact label="Цех">{order.departmentName}</Fact>
                  <Fact label="Склад выдачи">{order.warehouseName}</Fact>
                  {order.responsibleName && <Fact label="Мастер">{order.responsibleName}</Fact>}
                  {order.releasedAt && <Fact label="Выпущено">{stampRu(order.releasedAt)}</Fact>}
                  {order.completedAt && <Fact label="Закрыто">{stampRu(order.completedAt)}</Fact>}
                  {order.cancelledAt && <Fact label="Отменено">{stampRu(order.cancelledAt)}</Fact>}
                </dl>
                {order.comment && <p className="mt-2 whitespace-pre-line rounded-lg bg-muted px-3 py-2 text-sm text-ink-2">{order.comment}</p>}
              </section>

              <section className={card}>
                <h2 className="mb-3 flex items-center gap-2 text-base font-semibold text-ink">
                  <History className="size-4 text-ink-3" aria-hidden /> {t("История")}
                </h2>
                <ol className="relative space-y-4 border-l border-line pl-4">
                  {[...order.history].reverse().map((e) => (
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

      {dialog === "issue" && <IssueSheet order={order} onClose={() => setDialog(null)} />}
      {dialog === "return" && <ReturnSheet order={order} onClose={() => setDialog(null)} />}
      {dialog === "complete" && <CompleteSheet order={order} onClose={() => setDialog(null)} />}
      <Sheet
        open={dialog === "release" || dialog === "cancel"}
        onClose={() => {
          setDialog(null);
          setError(null);
        }}
        title={dialog === "release" ? t("Выпустить задание") : t("Отменить задание")}
        subtitle={`${order.number} · ${order.recipeName}`}
        footer={<ConfirmFooter dialog={dialog} busy={busy} onClose={() => setDialog(null)} onRelease={() => void post("release", {})} />}
      >
        {dialog === "release" ? (
          <div className="space-y-2 text-sm text-ink-2">
            <p>{t("Потребность по нормам зафиксируется, и кладовщик склада «{w}» сможет выдать сырьё.", { w: order.warehouseName })}</p>
            {order.lines.some((l) => l.available < l.required) && (
              <p className="rounded-lg bg-warn-soft px-3 py-2 text-ink">{t("Годного сырья на складе меньше потребности — мастер и снабжение получат уведомление.")}</p>
            )}
          </div>
        ) : (
          <CancelForm onSubmit={(comment) => void post("cancel", { comment })} busy={busy} />
        )}
        <div className="mt-3">
          <ErrorNote text={error} />
        </div>
      </Sheet>
    </>
  );
}

function ConfirmFooter({ dialog, busy, onClose, onRelease }: { dialog: Dialog; busy: boolean; onClose: () => void; onRelease: () => void }) {
  const t = useT();
  return (
    <div className="flex justify-end gap-2">
      <button type="button" onClick={onClose} className={secondary}>
        {t("Закрыть")}
      </button>
      {dialog === "release" ? (
        <button type="button" onClick={onRelease} disabled={busy} className={primary}>
          {busy && <Loader2 className="size-4 animate-spin" aria-hidden />} {t("Выпустить")}
        </button>
      ) : (
        <button type="submit" form="po-cancel" disabled={busy} className={`${primary} bg-bad hover:bg-bad`}>
          {busy && <Loader2 className="size-4 animate-spin" aria-hidden />} {t("Отменить задание")}
        </button>
      )}
    </div>
  );
}

function CancelForm({ onSubmit, busy }: { onSubmit: (comment: string) => void; busy: boolean }) {
  const t = useT();
  const [note, setNote] = useState("");
  return (
    <form
      id="po-cancel"
      onSubmit={(e) => {
        e.preventDefault();
        if (note.trim() && !busy) onSubmit(note.trim());
      }}
    >
      <label className="block">
        <span className="mb-1.5 block text-sm font-medium text-ink">{t("Почему отменяется")}</span>
        <textarea value={note} onChange={(e) => setNote(e.target.value)} required maxLength={1000} rows={3} autoFocus className="w-full rounded-lg border border-line bg-surface px-3 py-2.5 text-sm text-ink" />
        <span className="mt-1 block text-xs text-ink-3">{t("Отменить можно, только если у цеха на руках нет сырья по заданию — сначала оформите возврат.")}</span>
      </label>
    </form>
  );
}

/** Выдача по заданию (кладовщик): по каждой позиции — сколько и из какой партии (по умолчанию — ближайший срок); больше потребности — с причиной. */
function IssueSheet({ order, onClose }: { order: ProductionOrderDetail; onClose: () => void }) {
  const t = useT();
  const router = useRouter();
  const [rows, setRows] = useState(() => Object.fromEntries(order.lines.map((l) => [l.itemId, { qty: l.left > 0 ? text(l.left) : "", batch: "" }])));
  const [options, setOptions] = useState<Record<string, BatchOption[]>>({});
  const [reason, setReason] = useState("");
  const [receiver, setReceiver] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const key = useRef(newKey());

  useEffect(() => {
    let alive = true;
    for (const l of order.lines.filter((x) => x.tracksBatches)) {
      bff<BatchOption[]>(`procurement/batches/options?item=${l.itemId}&warehouse=${order.warehouseId}`)
        .then((list) => alive && setOptions((o) => ({ ...o, [l.itemId]: list })))
        .catch(() => undefined);
    }
    return () => {
      alive = false;
    };
  }, [order.lines, order.warehouseId]);

  const over = order.lines.filter((l) => (parseNumber(rows[l.itemId]?.qty ?? "") ?? 0) > l.left);
  const submit = async () => {
    setError(null);
    const lines = order.lines
      .map((l) => ({ itemId: l.itemId, quantity: parseNumber(rows[l.itemId]?.qty ?? ""), batchId: rows[l.itemId]?.batch || null }))
      .filter((l) => l.quantity !== null && l.quantity > 0);
    if (lines.length === 0) return setError(t("Укажите количество хотя бы по одной позиции."));
    if (over.length > 0 && !reason.trim()) return setError(t("Выдаёте больше потребности — укажите причину."));
    setBusy(true);
    try {
      await bff(`production/orders/${order.id}/issue`, {
        method: "POST",
        body: JSON.stringify({ lines, reason: reason.trim() || null, receiver: receiver.trim() || null, clientKey: key.current }),
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
      open
      onClose={onClose}
      size="xl"
      title={t("Выдать сырьё в производство")}
      subtitle={`${order.number} · ${order.warehouseName}`}
      footer={
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onClose} className={secondary}>
            {t("Закрыть")}
          </button>
          <button type="button" onClick={() => void submit()} disabled={busy} className={primary}>
            {busy && <Loader2 className="size-4 animate-spin" aria-hidden />} {t("Выдать")}
          </button>
        </div>
      }
    >
      <p className="mb-3 text-sm text-ink-2">{t("Выдаются только годные партии — допущенные по качеству и в сроке. Партия не выбрана — сначала ближайший срок.")}</p>
      <ul className="space-y-3">
        {order.lines.map((l) => (
          <li key={l.itemId} className="rounded-xl border border-line p-3">
            <div className="flex flex-wrap items-start gap-3">
              <div className="min-w-0 flex-1">
                <p className="text-sm font-medium text-ink">
                  {l.name} <span className="tabular-nums text-ink-3">{l.sku}</span>
                </p>
                <p className="mt-0.5 text-xs text-ink-3">
                  {t("потребность {a} · выдано {b} · осталось {c} · годного на складе {d}", {
                    a: `${quantity(l.required)} ${l.unitCode}`,
                    b: quantity(l.issued - l.returned),
                    c: quantity(l.left),
                    d: quantity(l.available),
                  })}
                </p>
              </div>
              <label className="relative block">
                <input
                  value={rows[l.itemId]?.qty ?? ""}
                  onChange={(e) => setRows((r) => ({ ...r, [l.itemId]: { ...r[l.itemId], qty: e.target.value } }))}
                  inputMode="decimal"
                  placeholder="0"
                  aria-label={t("Выдать: {name}, {unit}", { name: l.name, unit: l.unitCode })}
                  className={`${field} w-36 pr-9 text-right tabular-nums`}
                />
                <span className="pointer-events-none absolute right-2.5 top-1/2 -translate-y-1/2 text-xs text-ink-3">{l.unitCode}</span>
              </label>
            </div>
            {l.tracksBatches && (
              <select
                value={rows[l.itemId]?.batch ?? ""}
                onChange={(e) => setRows((r) => ({ ...r, [l.itemId]: { ...r[l.itemId], batch: e.target.value } }))}
                aria-label={t("Партия: {name}", { name: l.name })}
                className={`${field} mt-2`}
              >
                <option value="">{t("Партия — автоматически (сначала ближайший срок)")}</option>
                {(options[l.itemId] ?? []).map((o) => (
                  <option key={o.id} value={o.id} disabled={!o.usable}>
                    {batchOptionLabel(o, l.unitCode)}
                  </option>
                ))}
              </select>
            )}
          </li>
        ))}
      </ul>
      <div className="mt-3 grid gap-3 sm:grid-cols-2">
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">
            {t("Кто получил")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
          </span>
          <input value={receiver} onChange={(e) => setReceiver(e.target.value)} maxLength={200} className={field} />
        </label>
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">
            {t("Причина")} {over.length === 0 && <span className="font-normal text-ink-3">{t("если больше потребности")}</span>}
          </span>
          <input value={reason} onChange={(e) => setReason(e.target.value)} maxLength={1000} className={`${field} ${over.length > 0 && !reason.trim() ? "border-warn/60" : ""}`} />
        </label>
      </div>
      {over.length > 0 && <p className="mt-2 text-xs text-warn">{t("Больше потребности: {list} — причина обязательна, отклонение будет видно при закрытии.", { list: over.map((l) => l.name).join(", ") })}</p>}
      <div className="mt-3">
        <ErrorNote text={error} />
      </div>
    </Sheet>
  );
}

/** Возврат из производства (кладовщик): в ту же партию, не больше того, что у цеха на руках по заданию. */
function ReturnSheet({ order, onClose }: { order: ProductionOrderDetail; onClose: () => void }) {
  const t = useT();
  const router = useRouter();
  const held = order.holdings.filter((h) => h.left > 0);
  const [rows, setRows] = useState<Record<string, string>>({});
  const [comment, setComment] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const key = useRef(newKey());
  const id = (h: (typeof held)[number]) => `${h.itemId}:${h.batchId ?? ""}`;

  const submit = async () => {
    setError(null);
    const lines = held.map((h) => ({ itemId: h.itemId, batchId: h.batchId, quantity: parseNumber(rows[id(h)] ?? "") })).filter((l) => l.quantity !== null && l.quantity > 0);
    if (lines.length === 0) return setError(t("Укажите, сколько возвращают, хотя бы по одной партии."));
    const bad = held.find((h) => (parseNumber(rows[id(h)] ?? "") ?? 0) > h.left);
    if (bad) return setError(t("«{name}»: у цеха {n} — больше не вернуть.", { name: bad.name, n: `${quantity(bad.left)} ${bad.unitCode}` }));
    setBusy(true);
    try {
      await bff(`production/orders/${order.id}/return`, { method: "POST", body: JSON.stringify({ lines, comment: comment.trim() || null, clientKey: key.current }) });
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
      title={t("Возврат из производства")}
      subtitle={`${order.number} · ${order.warehouseName}`}
      footer={
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onClose} className={secondary}>
            {t("Закрыть")}
          </button>
          <button type="button" onClick={() => void submit()} disabled={busy} className={primary}>
            {busy && <Loader2 className="size-4 animate-spin" aria-hidden />} {t("Принять возврат")}
          </button>
        </div>
      }
    >
      <p className="mb-3 text-sm text-ink-2">{t("Остаток возвращается в ту же партию, из которой выдавали, — не больше того, что у цеха на руках.")}</p>
      <ul className="space-y-2">
        {held.map((h) => (
          <li key={id(h)} className="flex flex-wrap items-center gap-3 rounded-xl border border-line p-3">
            <span className="min-w-0 flex-1 text-sm">
              <span className="block text-ink">{h.name}</span>
              <span className="block text-xs text-ink-3">
                {[h.batchNumber, h.supplierBatchNo].filter(Boolean).join(" · ") || t("без партий")} · {t("у цеха {n}", { n: `${quantity(h.left)} ${h.unitCode}` })}
              </span>
            </span>
            <input
              value={rows[id(h)] ?? ""}
              onChange={(e) => setRows((r) => ({ ...r, [id(h)]: e.target.value }))}
              inputMode="decimal"
              placeholder="0"
              aria-label={t("Вернуть: {name}", { name: h.name })}
              className={`${field} w-32 text-right tabular-nums`}
            />
          </li>
        ))}
      </ul>
      <label className="mt-3 block">
        <span className="mb-1.5 block text-sm font-medium text-ink">
          {t("Комментарий")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
        </span>
        <input value={comment} onChange={(e) => setComment(e.target.value)} maxLength={1000} className={field} />
      </label>
      <div className="mt-3">
        <ErrorNote text={error} />
      </div>
    </Sheet>
  );
}

/** Закрытие задания: фактический выход; сразу видно норму на факт и отклонение по позициям. */
function CompleteSheet({ order, onClose }: { order: ProductionOrderDetail; onClose: () => void }) {
  const t = useT();
  const router = useRouter();
  const [output, setOutput] = useState(text(order.plannedQty));
  const [comment, setComment] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const q = parseNumber(output);

  const submit = async () => {
    setError(null);
    if (q === null || q < 0) return setError(t("Укажите фактический выход."));
    setBusy(true);
    try {
      await bff(`production/orders/${order.id}/complete`, { method: "POST", body: JSON.stringify({ outputQty: q, comment: comment.trim() || null, version: order.version }) });
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
      size="xl"
      title={t("Закрыть задание")}
      subtitle={`${order.number} · ${order.recipeName}`}
      footer={
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onClose} className={secondary}>
            {t("Закрыть")}
          </button>
          <button type="button" onClick={() => void submit()} disabled={busy} className={primary}>
            {busy && <Loader2 className="size-4 animate-spin" aria-hidden />} {t("Закрыть задание")}
          </button>
        </div>
      }
    >
      <div className="flex flex-wrap items-end gap-3">
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Фактический выход")}</span>
          <span className="relative block">
            <input value={output} onChange={(e) => setOutput(e.target.value)} inputMode="decimal" className={`${field} w-40 pr-10 text-right tabular-nums`} />
            <span className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-sm text-ink-3">{order.outputUnit}</span>
          </span>
        </label>
        <p className="pb-2 text-xs text-ink-3">{t("план {q}", { q: `${quantity(order.plannedQty)} ${order.outputUnit}` })}</p>
      </div>
      {q !== null && q >= 0 && (
        <div className="mt-3 overflow-x-auto">
          <table className="w-full min-w-[480px] text-sm">
            <thead>
              <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                <th className="py-2 pr-3 font-semibold">{t("Позиция")}</th>
                <th className="py-2 pr-3 text-right font-semibold">{t("Норма на факт")}</th>
                <th className="py-2 pr-3 text-right font-semibold">{t("Расход")}</th>
                <th className="py-2 text-right font-semibold">{t("Отклонение")}</th>
              </tr>
            </thead>
            <tbody>
              {order.lines.map((l) => {
                const norm = requirement(l.norm, q, order.recipeOutput, l.lossPercent);
                const d = deviation(l.consumed, norm);
                const bad = norm > 0 ? l.consumed > (norm * (100 + order.thresholdPercent)) / 100 : l.consumed > 0;
                return (
                  <tr key={l.id} className="border-b border-line last:border-0">
                    <td className="py-2 pr-3 text-ink">{l.name}</td>
                    <td className="whitespace-nowrap py-2 pr-3 text-right tabular-nums">
                      {quantity(norm)} {l.unitCode}
                    </td>
                    <td className="whitespace-nowrap py-2 pr-3 text-right tabular-nums">{quantity(l.consumed)}</td>
                    <td className={`whitespace-nowrap py-2 text-right tabular-nums ${bad ? "font-semibold text-bad" : "text-ink-2"}`}>
                      {d.quantity > 0 ? "+" : ""}
                      {quantity(d.quantity)} · {percentLabel(d.percent)}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          <p className="mt-2 text-xs text-ink-3">{t("Перерасход больше {p} % — пометка в задании и уведомление руководителю производства. Остаток у цеха сначала верните на склад.", { p: quantity(order.thresholdPercent) })}</p>
        </div>
      )}
      <label className="mt-3 block">
        <span className="mb-1.5 block text-sm font-medium text-ink">
          {t("Комментарий")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
        </span>
        <input value={comment} onChange={(e) => setComment(e.target.value)} maxLength={1000} className={field} />
      </label>
      <div className="mt-3">
        <ErrorNote text={error} />
      </div>
    </Sheet>
  );
}
