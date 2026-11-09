"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useRef, useState } from "react";
import {
  AlertTriangle,
  BadgeCheck,
  Ban,
  Check,
  Download,
  FileText,
  Gavel,
  History,
  Loader2,
  OctagonX,
  PackageCheck,
  Paperclip,
  Pencil,
  Scale,
  Send,
  ShoppingBag,
  ShoppingCart,
  Trash2,
  Undo2,
  UserCheck,
} from "lucide-react";
import { OrderStatusPill } from "@/components/procurement/ProcKit";
import { PurchaseSheet } from "@/components/procurement/PurchaseSheet";
import { Sheet } from "@/components/field/Sheet";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { fileSize } from "@/lib/format";
import {
  approvalDecisionLabel,
  approvalStatuses,
  dateRu,
  itemStatusLabel,
  money,
  parseNumber,
  quantity,
  requestStages,
  requestStatusLabel,
  stageIndex,
  stampRu,
  tenderStatusLabel,
  type RequestDetail,
} from "@/lib/procurement";
import { RequestRoute } from "./RequestRoute";
import { RequestKindPill, RequestPriorityPill, RequestStatusPill, requestStatusLook } from "./RequestKit";
import { allowedFiles, fileProblem, uploadRequestFile } from "./RequestForm";

const card = "rounded-2xl border border-line bg-surface p-4 sm:p-5";
const secondary = "inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3.5 text-sm font-medium text-ink hover:bg-muted disabled:opacity-50";
const primary = "inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50";

type Dialog = { kind: "return" | "cancel" | "approve" | "reject" | "issue" } | null;

const dialogText: Record<"return" | "cancel" | "approve" | "reject", { title: string; button: string; label: string; required: boolean; hint?: string }> = {
  return: { title: "Вернуть на доработку", button: "Вернуть", label: "Что нужно исправить", required: true, hint: "Комментарий увидит инициатор — заявка вернётся к нему на правку." },
  cancel: { title: "Отменить заявку", button: "Отменить заявку", label: "Причина отмены", required: false },
  approve: { title: "Согласовать заявку", button: "Согласовать", label: "Комментарий", required: false },
  reject: { title: "Отклонить заявку", button: "Отклонить", label: "Почему отклонена", required: true, hint: "Комментарий увидит инициатор. Резерв на складе снимется." },
};

/** Что будет после согласования — по шагу маршрута и (у финансиста) сумме к закупке; заявка на сырьё — по своим шагам. */
function approveHint(detail: RequestDetail, purchase: number, t: (text: string, vars?: Record<string, string | number>) => string): string {
  if (detail.kind === "Raw" && detail.route && !detail.route.some((s) => s.key === "supply_head")) {
    const at = detail.route.findIndex((s) => s.state === "current");
    const next = detail.route.slice(at + 1).find((s) => s.state === "pending");
    if (next?.key === "analyst") return t("Дальше заявку согласует аналитик.");
    if (next?.key === "finance") return t("Дальше заявку согласует финансист.");
    if (next?.key === "director") return t("Дальше заявку согласует директор.");
    return t("После согласования заявка уйдёт в снабжение.");
  }
  if (detail.status === "HeadApproval") return t("Дальше заявку согласует финансист.");
  if (detail.status === "SupplyApproval") return t("Закупка сырья — дальше заявку согласует директор при любой сумме.");
  if (detail.status === "FinanceApproval") {
    return purchase > detail.directorLimit
      ? t("К закупке больше {limit} сум — дальше заявку согласует директор.", { limit: money(detail.directorLimit) })
      : t("К закупке не больше {limit} сум — после согласования заявка уйдёт в снабжение.", { limit: money(detail.directorLimit) });
  }
  return t("После утверждения заявка уйдёт в снабжение — снабженцы купят позиции «к закупке».");
}

/** Полоса этапов: черновик → согласование → утверждена → выполнена; отклонённая и отменённая — плашкой вместо полосы. */
function Stages({ detail }: { detail: RequestDetail }) {
  const t = useT();
  const current = stageIndex(detail.status);
  if (current < 0) {
    const look = requestStatusLook[detail.status];
    return (
      <div className={`flex items-center gap-2 rounded-2xl px-4 py-3 text-sm font-medium ${look.tone}`}>
        <look.icon className="size-4" aria-hidden />
        {t(requestStatusLabel[detail.status])}
        {detail.cancelledAt && <span className="font-normal opacity-80">· {stampRu(detail.cancelledAt)}</span>}
      </div>
    );
  }
  return (
    <ol className="grid grid-cols-4 gap-1.5" aria-label={t("Этапы заявки")}>
      {requestStages.map((stage, i) => {
        const done = i < current;
        const on = i === current;
        const label =
          on && detail.status === "RevisionRequired"
            ? "На доработке"
            : on && detail.status === "PartiallyFulfilled"
              ? "Частично выполнена"
              : on && approvalStatuses.includes(detail.status)
                ? requestStatusLabel[detail.status]
                : stage;
        return (
          <li key={stage} className="min-w-0" aria-current={on ? "step" : undefined}>
            <div className={`h-1.5 rounded-full ${done ? "bg-ok" : on ? (detail.status === "RevisionRequired" ? "bg-warn" : "bg-accent") : "bg-muted"}`} />
            <div className={`mt-1.5 flex items-center gap-1 truncate text-[11px] sm:text-xs ${on ? "font-semibold text-ink" : done ? "text-ink-2" : "text-ink-3"}`}>
              {done && <Check className="size-3 shrink-0 text-ok" aria-hidden />}
              {label === stage ? (
                <span className="truncate">{t(label)}</span>
              ) : (
                <>
                  <span className="truncate sm:hidden">{t(on && approvalStatuses.includes(detail.status) ? stage : label)}</span>
                  <span className="hidden truncate sm:inline">{t(label)}</span>
                </>
              )}
            </div>
          </li>
        );
      })}
    </ol>
  );
}

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
 * Карточка заявки: этапы, позиции со статусами, сумма, причина и комментарий, вложения, история (кто, когда, что изменилось, комментарий).
 * Кнопки — только те, что разрешены пользователю (их же проверяет сервер): изменить и отправить — инициатор; согласовать, вернуть и
 * отклонить — согласующий текущего шага; «Куплено» — снабжение по утверждённой заявке; выдать — кладовщик; отменить — инициатор до утверждения.
 */
export function RequestDetailView({ detail, filesFailed, backHref }: { detail: RequestDetail; filesFailed: boolean; backHref: string }) {
  const t = useT();
  const router = useRouter();
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [dialog, setDialog] = useState<Dialog>(null);
  const [buying, setBuying] = useState(false);
  const [note, setNote] = useState("");
  const fileInput = useRef<HTMLInputElement>(null);
  const can = detail.can;
  // Финансист уточняет цены позиций к закупке при согласовании: по новой сумме решается, нужен ли директор.
  const purchaseItems = detail.items.filter((i) => i.status !== "Cancelled" && i.toPurchase > 0);
  const [prices, setPrices] = useState<Record<string, string>>({});
  const priceOf = (id: string, fallback: number | null) => (prices[id] !== undefined ? parseNumber(prices[id]) : fallback);
  const purchaseNow = Math.round(purchaseItems.reduce((sum, i) => sum + i.toPurchase * (priceOf(i.id, i.estimatedPrice) ?? 0), 0) * 100) / 100;

  const run = async (name: string, action: () => Promise<unknown>) => {
    setBusy(name);
    setError(null);
    try {
      await action();
      router.refresh();
      return true;
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      return false;
    } finally {
      setBusy(null);
    }
  };
  const post = (action: string, body?: object) => bff(`procurement/requests/${detail.id}/${action}`, { method: "POST", body: body ? JSON.stringify(body) : undefined });

  const upload = async (files: FileList) => {
    for (const file of Array.from(files)) {
      const problem = fileProblem(file);
      if (problem) {
        setError(`«${file.name}»: ${t(problem)}`);
        continue;
      }
      const ok = await run("upload", () => uploadRequestFile(detail.id, file).catch((e: Error) => Promise.reject(new Error(t(e.message)))));
      if (!ok) break;
    }
  };

  const [issueQty, setIssueQty] = useState<Record<string, string>>({});
  const [receiver, setReceiver] = useState("");
  const issueKey = useRef<string>("");
  const openIssue = () => {
    setIssueQty(Object.fromEntries(detail.items.filter((i) => i.reservedLeft > 0).map((i) => [i.id, String(i.reservedLeft).replace(".", ",")])));
    issueKey.current = typeof crypto !== "undefined" && "randomUUID" in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`;
    setDialog({ kind: "issue" });
  };

  const confirmDialog = async () => {
    if (!dialog) return;
    if (dialog.kind === "issue") {
      const lines = Object.entries(issueQty)
        .map(([requestItemId, value]) => ({ requestItemId, quantity: parseNumber(value) ?? 0 }))
        .filter((l) => l.quantity > 0);
      if (lines.length === 0) {
        setError(t("Укажите, что и сколько выдать."));
        return;
      }
      const ok = await run("issue", () => post("issue", { lines, receiver: receiver.trim() || null, comment: note.trim() || null, clientKey: issueKey.current }));
      if (ok) {
        setDialog(null);
        setNote("");
        setReceiver("");
      }
      return;
    }
    if (dialogText[dialog.kind].required && !note.trim()) {
      setError(t(dialog.kind === "reject" ? "Напишите комментарий: почему заявка отклонена." : "Напишите комментарий: что нужно исправить в заявке."));
      return;
    }
    let changed: { itemId: string; price: number }[] = [];
    if (dialog.kind === "approve" && can.price) {
      for (const i of purchaseItems) {
        if (prices[i.id] === undefined) continue;
        const p = parseNumber(prices[i.id]);
        if (p === null || p <= 0 || Math.round(p * 100) / 100 !== p) {
          setError(t("«{name}»: цена — больше нуля, не больше двух знаков после запятой.", { name: i.name }));
          return;
        }
        if (p !== i.estimatedPrice) changed = [...changed, { itemId: i.id, price: p }];
      }
    }
    const ok = await run(dialog.kind, () => post(dialog.kind, dialog.kind === "approve" ? { comment: note.trim() || null, prices: changed } : { comment: note.trim() || null }));
    if (ok) {
      setDialog(null);
      setNote("");
      setPrices({});
    }
  };

  const actions = (
    <div className="flex flex-wrap items-center gap-2">
      <a href={`/bff/api/procurement/requests/${detail.id}/docx`} download className={secondary} title={t("Заявка документом Word — для печати и подписи")}>
        <FileText className="size-4" aria-hidden /> {t("Скачать (Word)")}
      </a>
      {can.edit && (
        <Link href={`/requests/${detail.id}/edit`} className={secondary}>
          <Pencil className="size-4" aria-hidden /> {t("Изменить")}
        </Link>
      )}
      {can.cancel && (
        <button type="button" onClick={() => setDialog({ kind: "cancel" })} disabled={busy !== null} className={`${secondary} text-bad`}>
          <Ban className="size-4" aria-hidden /> {t("Отменить")}
        </button>
      )}
      {can.return && (
        <button type="button" onClick={() => setDialog({ kind: "return" })} disabled={busy !== null} className={secondary}>
          <Undo2 className="size-4" aria-hidden /> {t("Вернуть на доработку")}
        </button>
      )}
      {detail.offersCount > 0 && (
        <Link href={`/requests/${detail.id}/offers`} className={secondary}>
          <Scale className="size-4" aria-hidden /> {t("Предложения")}
          <span className="rounded-full bg-muted px-1.5 text-[11px] tabular-nums">{detail.offersCount}</span>
        </Link>
      )}
      {can.purchase && (
        <button type="button" onClick={() => setBuying(true)} disabled={busy !== null} className={primary}>
          <ShoppingBag className="size-4" aria-hidden /> {t("Куплено")}
        </button>
      )}
      {can.approve && (
        <button type="button" onClick={() => setDialog({ kind: "reject" })} disabled={busy !== null} className={`${secondary} text-bad`}>
          <OctagonX className="size-4" aria-hidden /> {t("Отклонить")}
        </button>
      )}
      {can.approve && (
        <button type="button" onClick={() => setDialog({ kind: "approve" })} disabled={busy !== null} className={primary}>
          <BadgeCheck className="size-4" aria-hidden /> {detail.status === "DirectorApproval" ? t("Утвердить") : t("Согласовать")}
        </button>
      )}
      {can.issue && (
        <button type="button" onClick={openIssue} disabled={busy !== null} className={primary}>
          <PackageCheck className="size-4" aria-hidden /> {t("Выдать со склада")}
        </button>
      )}
      {can.forward && (
        <button type="button" onClick={() => void run("forward", () => post("forward"))} disabled={busy !== null} className={primary}>
          {busy === "forward" ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <UserCheck className="size-4" aria-hidden />} {t("Передать директору")}
        </button>
      )}
      {can.submit && (
        <button type="button" onClick={() => void run("submit", () => post("submit"))} disabled={busy !== null} className={primary}>
          {busy === "submit" ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Send className="size-4" aria-hidden />}
          {detail.status === "RevisionRequired" ? t("Отправить повторно") : t("Отправить")}
        </button>
      )}
    </div>
  );

  const activeItems = detail.items.filter((i) => i.status !== "Cancelled").length;
  // «Куплено» — покупки снабжения; заказы поставщикам — прежний путь (у старых заявок).
  const bought = detail.orders.filter((o) => o.direct);
  const ordered = detail.orders.filter((o) => !o.direct);
  const toBuy = detail.items
    .filter((i) => i.status !== "Cancelled" && i.toPurchase > i.orderedQty)
    .map((i) => ({
      requestItemId: i.id,
      name: i.name,
      unitCode: i.unitCode,
      left: Math.round((i.toPurchase - i.orderedQty) * 1000) / 1000,
      price: i.estimatedPrice,
      tracksBatches: i.tracksBatches,
      shelfLifeDays: i.shelfLifeDays,
    }));

  return (
    <>
      <PageHeader
        title={detail.title}
        subtitle={
          <span className="inline-flex flex-wrap items-center gap-2">
            <span className="tabular-nums">{detail.number}</span>
            <RequestStatusPill status={detail.status} />
            <RequestPriorityPill priority={detail.priority} />
            <RequestKindPill kind={detail.kind} />
          </span>
        }
        back={backHref}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Заявки"), href: "/requests" }, { label: detail.number }]}
        actions={<span className="max-lg:hidden">{actions}</span>}
      />
      <PageBody>
        <div className="mx-auto max-w-6xl space-y-4">
          <div className="lg:hidden">{actions}</div>
          {/* Ошибка действия в диалоге показывается в самом диалоге. */}
          <ErrorNote text={dialog ? null : error} />
          {filesFailed && (
            <p role="status" className="flex items-start gap-2 rounded-xl bg-warn-soft px-3 py-2 text-sm text-ink">
              <AlertTriangle className="mt-0.5 size-4 shrink-0 text-warn" aria-hidden />
              {t("Заявка сохранена, но не все файлы загрузились — добавьте их ещё раз в блоке «Вложения».")}
            </p>
          )}
          {detail.revisionComment && (
            <div className="rounded-2xl border border-warn/30 bg-warn-soft px-4 py-3 text-sm text-ink">
              <div className="font-semibold text-warn">{t("Возвращена на доработку")}</div>
              <p className="mt-1 whitespace-pre-line">{detail.revisionComment}</p>
              {can.edit && <p className="mt-1.5 text-xs text-ink-2">{t("Исправьте заявку и отправьте её повторно.")}</p>}
            </div>
          )}

          <section className={card}>
            <Stages detail={detail} />
          </section>
          <RequestRoute detail={detail} />

          <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_340px]">
            <div className="min-w-0 space-y-4">
              <section className={card} aria-labelledby="rq-items">
                <div className="mb-3 flex items-center justify-between gap-3">
                  <h2 id="rq-items" className="text-base font-semibold text-ink">
                    {t("Позиции")} <span className="font-normal text-ink-3">· {activeItems}</span>
                  </h2>
                  {detail.estimatedTotal > 0 && (
                    <span className="text-right text-sm text-ink-2">
                      ≈ <span className="font-semibold tabular-nums text-ink">{money(detail.estimatedTotal)}</span> {t("сум")}
                      {detail.purchaseTotal !== detail.estimatedTotal && (
                        <span className="block text-xs text-ink-3">
                          {t("к закупке")} {detail.purchaseTotal > 0 ? `≈ ${money(detail.purchaseTotal)} ${t("сум")}` : t("— ничего, всё со склада")}
                        </span>
                      )}
                    </span>
                  )}
                </div>
                <div className="overflow-x-auto max-md:hidden">
                  <table className="w-full min-w-[720px] text-sm">
                    <thead>
                      <tr className="whitespace-nowrap border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                        <th className="py-2 pr-3 font-semibold">№</th>
                        <th className="py-2 pr-3 font-semibold">{t("Наименование")}</th>
                        <th className="py-2 pr-3 text-right font-semibold">{t("Кол-во")}</th>
                        <th className="py-2 pr-3 text-right font-semibold">{t("Со склада")}</th>
                        <th className="py-2 pr-3 text-right font-semibold">{t("К закупке")}</th>
                        <th className="py-2 pr-3 text-right font-semibold">{t("Выдано")}</th>
                        <th className="py-2 pr-3 text-right font-semibold">{t("Сумма")}</th>
                        <th className="py-2 font-semibold">{t("Статус")}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {detail.items.map((item) => (
                        <tr key={item.id} className={`border-b border-line align-top last:border-0 ${item.status === "Cancelled" ? "text-ink-3 line-through decoration-ink-4" : ""}`}>
                          <td className="py-2.5 pr-3 tabular-nums text-ink-3">{item.lineNo}</td>
                          <td className="py-2.5 pr-3">
                            <span className="text-ink">{item.name}</span>
                            <span className="block text-xs text-ink-3">
                              {item.sku ? `${item.sku} · ` : ""}
                              {item.categoryName}
                              {item.estimatedPrice !== null && ` · ${money(item.estimatedPrice)} ${t("сум")}/${item.unitCode}`}
                            </span>
                            {item.purchaseReason && <span className="mt-0.5 block text-xs text-warn">{t("Закупка, хотя есть на складе")}: {item.purchaseReason}</span>}
                            {item.comment && <span className="block text-xs text-ink-3">{item.comment}</span>}
                          </td>
                          <td className="whitespace-nowrap py-2.5 pr-3 text-right tabular-nums">
                            {quantity(item.quantity)} {item.unitCode}
                          </td>
                          <td className="whitespace-nowrap py-2.5 pr-3 text-right tabular-nums text-ink-2">
                            {item.fromStock > 0 ? quantity(item.fromStock) : "—"}
                            {item.reservedLeft > 0 && <span className="block text-[11px] text-ink-3">{t("в резерве {n}", { n: quantity(item.reservedLeft) })}</span>}
                          </td>
                          <td className="whitespace-nowrap py-2.5 pr-3 text-right tabular-nums">
                            {item.toPurchase > 0 ? quantity(item.toPurchase) : "—"}
                            {item.orderedQty > 0 && <span className="block text-[11px] text-ink-3">{t("заказано {n}", { n: quantity(item.orderedQty) })}</span>}
                            {item.receivedQty > 0 && <span className="block text-[11px] text-ok">{t("поступило {n}", { n: quantity(item.receivedQty) })}</span>}
                          </td>
                          <td className="whitespace-nowrap py-2.5 pr-3 text-right tabular-nums text-ink-2">{item.issuedQty > 0 ? quantity(item.issuedQty) : "—"}</td>
                          <td className="whitespace-nowrap py-2.5 pr-3 text-right tabular-nums">{money(item.sum)}</td>
                          <td className="whitespace-nowrap py-2.5 text-xs text-ink-2">{t(itemStatusLabel[item.status])}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <ul className="divide-y divide-line md:hidden">
                  {detail.items.map((item) => (
                    <li key={item.id} className={`py-2.5 ${item.status === "Cancelled" ? "opacity-60" : ""}`}>
                      <div className="flex items-start justify-between gap-3">
                        <span className="min-w-0">
                          <span className="block text-sm font-medium text-ink">
                            {item.lineNo}. {item.name}
                          </span>
                          <span className="block text-xs text-ink-3">
                            {item.categoryName} · {t(itemStatusLabel[item.status])}
                          </span>
                          {(item.fromStock > 0 || item.issuedQty > 0) && (
                            <span className="block text-xs text-ink-2">
                              {t("со склада {a} · к закупке {b} · выдано {c}", { a: quantity(item.fromStock), b: quantity(item.toPurchase), c: quantity(item.issuedQty) })}
                            </span>
                          )}
                          {(item.orderedQty > 0 || item.receivedQty > 0) && (
                            <span className="block text-xs text-ink-3">
                              {t("заказано {a} · поступило {b}", { a: quantity(item.orderedQty), b: quantity(item.receivedQty) })}
                            </span>
                          )}
                        </span>
                        <span className="shrink-0 text-right text-sm tabular-nums">
                          <span className="block text-ink">
                            {quantity(item.quantity)} {item.unitCode}
                          </span>
                          {item.sum !== null && <span className="block text-xs text-ink-3">{money(item.sum)}</span>}
                        </span>
                      </div>
                    </li>
                  ))}
                </ul>
              </section>

              {(detail.reason || detail.comment) && (
                <section className={`${card} space-y-3`}>
                  {detail.reason && (
                    <div>
                      <h2 className="text-xs font-semibold uppercase tracking-wide text-ink-3">{t("Причина закупки")}</h2>
                      <p className="mt-1 whitespace-pre-line text-sm text-ink">{detail.reason}</p>
                    </div>
                  )}
                  {detail.comment && (
                    <div>
                      <h2 className="text-xs font-semibold uppercase tracking-wide text-ink-3">{t("Комментарий")}</h2>
                      <p className="mt-1 whitespace-pre-line text-sm text-ink">{detail.comment}</p>
                    </div>
                  )}
                </section>
              )}

              <section className={card} aria-labelledby="rq-files">
                <div className="flex items-center justify-between gap-3">
                  <h2 id="rq-files" className="flex items-center gap-2 text-base font-semibold text-ink">
                    <Paperclip className="size-4 text-ink-3" aria-hidden /> {t("Вложения")}
                  </h2>
                  <span className="text-xs tabular-nums text-ink-3">{detail.documents.length}</span>
                </div>
                {detail.documents.length > 0 ? (
                  <ul className="mt-3 space-y-1">
                    {detail.documents.map((d) => {
                      const href = `/bff/api/procurement/attachments/${d.id}`;
                      return (
                        <li key={d.id} className="group flex items-center gap-3 rounded-xl px-1 py-1.5 hover:bg-muted/60">
                          <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-muted text-ink-3">
                            <FileText className="size-4" aria-hidden />
                          </span>
                          <a href={href} className="min-w-0 flex-1">
                            <span className="block truncate text-sm font-medium text-ink group-hover:text-accent-strong">{d.fileName}</span>
                            <span className="block truncate text-[11px] text-ink-3">
                              {fileSize(d.sizeBytes)}
                              {d.uploadedByName ? ` · ${d.uploadedByName}` : ""} · {stampRu(d.createdAt)}
                            </span>
                          </a>
                          <a href={href} aria-label={t("Скачать {name}", { name: d.fileName })} className="grid size-8 shrink-0 place-items-center rounded-md text-ink-3 hover:text-ink">
                            <Download className="size-4" aria-hidden />
                          </a>
                          {d.canDelete && (
                            <button
                              type="button"
                              onClick={() => {
                                if (window.confirm(t("Удалить вложение «{name}»?", { name: d.fileName }))) void run(`file-${d.id}`, () => bff(`procurement/attachments/${d.id}`, { method: "DELETE" }));
                              }}
                              aria-label={t("Удалить {name}", { name: d.fileName })}
                              className="grid size-8 shrink-0 place-items-center rounded-md text-ink-3 hover:text-bad"
                            >
                              <Trash2 className="size-4" aria-hidden />
                            </button>
                          )}
                        </li>
                      );
                    })}
                  </ul>
                ) : (
                  <p className="mt-2 text-sm text-ink-3">{t("Вложений нет")}</p>
                )}
                {can.attach && (
                  <>
                    <input
                      ref={fileInput}
                      type="file"
                      multiple
                      accept={allowedFiles.join(",")}
                      className="sr-only"
                      tabIndex={-1}
                      onChange={(e) => {
                        if (e.target.files?.length) void upload(e.target.files);
                        e.target.value = "";
                      }}
                    />
                    <button
                      type="button"
                      onClick={() => fileInput.current?.click()}
                      disabled={busy === "upload"}
                      className="mt-3 inline-flex h-10 items-center gap-2 rounded-lg border border-dashed border-line-strong px-3.5 text-sm font-medium text-ink-2 hover:border-accent hover:text-accent-strong disabled:opacity-50"
                    >
                      {busy === "upload" ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Paperclip className="size-4" aria-hidden />} {t("Прикрепить файл")}
                    </button>
                  </>
                )}
              </section>
            </div>

            <div className="space-y-4">
              <section className={card}>
                <h2 className="mb-1 text-base font-semibold text-ink">{t("Сведения")}</h2>
                <dl className="divide-y divide-line">
                  <Fact label="Отдел">{detail.departmentName}</Fact>
                  <Fact label="Инициатор">
                    {detail.initiatorName}
                    {detail.initiatorPosition && <span className="block text-xs text-ink-3">{detail.initiatorPosition}</span>}
                  </Fact>
                  <Fact label="Нужно к">{dateRu(detail.desiredDate)}</Fact>
                  <Fact label="Сумма">{detail.estimatedTotal > 0 ? `≈ ${money(detail.estimatedTotal)} ${t("сум")}` : "—"}</Fact>
                  <Fact label="Создана">{stampRu(detail.createdAt)}</Fact>
                  {detail.submittedAt && <Fact label="Отправлена">{stampRu(detail.submittedAt)}</Fact>}
                  {detail.forwardedAt && approvalStatuses.includes(detail.status) && (
                    <Fact label={detail.route ? "На текущем шаге с" : "Передана директору"}>{stampRu(detail.forwardedAt)}</Fact>
                  )}
                  {detail.approvedAt && (
                    <Fact label="Утверждена">
                      {stampRu(detail.approvedAt)}
                      <span className="block text-xs text-ink-3">{detail.approvedByName ?? t("без согласования: всё со склада")}</span>
                    </Fact>
                  )}
                </dl>
              </section>

              {bought.length > 0 && (
                <section className={card} aria-labelledby="rq-bought">
                  <h2 id="rq-bought" className="mb-2 flex items-center gap-2 text-base font-semibold text-ink">
                    <ShoppingBag className="size-4 text-ink-3" aria-hidden /> {t("Покупки")}
                    <span className="ml-auto text-sm font-normal tabular-nums text-ink-3">
                      {money(bought.reduce((sum, o) => sum + o.total, 0))} {t("сум")}
                    </span>
                  </h2>
                  <ul className="divide-y divide-line">
                    {bought.map((o) => (
                      <li key={o.id} className="py-2.5 text-sm">
                        <span className="flex items-center justify-between gap-2">
                          <span className="min-w-0 truncate font-medium text-ink">{o.supplierName}</span>
                          <span className="shrink-0 font-medium tabular-nums text-ink">
                            {money(o.total)} {t("сум")}
                          </span>
                        </span>
                        <span className="mt-0.5 block text-xs text-ink-3">
                          {[o.responsibleName, o.deliveredAt && stampRu(o.deliveredAt), o.number].filter(Boolean).join(" · ")}
                        </span>
                      </li>
                    ))}
                  </ul>
                </section>
              )}

              {ordered.length > 0 && (
                <section className={card} aria-labelledby="rq-orders">
                  <h2 id="rq-orders" className="mb-2 flex items-center gap-2 text-base font-semibold text-ink">
                    <ShoppingCart className="size-4 text-ink-3" aria-hidden /> {t("Заказы поставщикам")}
                  </h2>
                  <ul className="divide-y divide-line">
                    {ordered.map((o) => (
                      <li key={o.id}>
                        <Link href={`/procurement/orders/${o.id}`} className="block py-2.5 text-sm hover:text-accent-strong">
                          <span className="flex items-center justify-between gap-2">
                            <span className="font-medium tabular-nums text-ink">{o.number}</span>
                            <OrderStatusPill status={o.status} />
                          </span>
                          <span className="mt-0.5 block text-xs text-ink-3">
                            {o.supplierName} · {money(o.total)} {t("сум")}
                            {o.expectedDate && ` · ${t("ожидается {date}", { date: dateRu(o.expectedDate) })}`}
                          </span>
                        </Link>
                      </li>
                    ))}
                  </ul>
                </section>
              )}

              {(detail.tenders?.length ?? 0) > 0 && (
                <section className={card} aria-labelledby="rq-tenders">
                  <h2 id="rq-tenders" className="mb-2 flex items-center gap-2 text-base font-semibold text-ink">
                    <ShoppingCart className="size-4 text-ink-3" aria-hidden /> {t("Тендеры по заявке")}
                  </h2>
                  <ul className="divide-y divide-line">
                    {detail.tenders!.map((x) => (
                      <li key={x.id}>
                        <Link href={`/procurement/tenders/${x.id}`} className="block py-2.5 text-sm hover:text-accent-strong">
                          <span className="flex items-center justify-between gap-2">
                            <span className="font-medium tabular-nums text-ink">{x.number}</span>
                            <span className="text-xs text-ink-3">{t(tenderStatusLabel[x.status])}</span>
                          </span>
                          <span className="mt-0.5 block text-xs text-ink-3">
                            {x.title} · {x.supplierName} · {money(x.total)} {t("сум")}
                          </span>
                        </Link>
                      </li>
                    ))}
                  </ul>
                </section>
              )}

              {!detail.route && (detail.approvals.length > 0 || detail.status === "DirectorApproval") && (
                <section className={card} aria-labelledby="rq-approval">
                  <h2 id="rq-approval" className="mb-3 flex items-center gap-2 text-base font-semibold text-ink">
                    <Gavel className="size-4 text-ink-3" aria-hidden /> {t("Согласование директора")}
                  </h2>
                  {detail.status === "DirectorApproval" && (
                    <p className="mb-3 rounded-xl bg-violet-soft px-3 py-2 text-sm text-ink">
                      {detail.approvalRound > 1 ? t("Повторное согласование (раунд {n})", { n: detail.approvalRound }) : t("Ждёт решения директора")}
                      {detail.forwardedAt && <span className="block text-xs text-ink-3">{t("с {date}", { date: stampRu(detail.forwardedAt) })}</span>}
                      {detail.purchaseTotal > 0 && (
                        <span className="block text-xs text-ink-2">
                          {t("к закупке")} ≈ {money(detail.purchaseTotal)} {t("сум")}
                        </span>
                      )}
                    </p>
                  )}
                  <ol className="space-y-3">
                    {detail.approvals.map((a) => (
                      <li key={a.id} className="text-sm">
                        <span className={`font-medium ${a.decision === "Approved" ? "text-ok" : a.decision === "Rejected" ? "text-bad" : "text-warn"}`}>
                          {t(approvalDecisionLabel[a.decision])}
                        </span>
                        <span className="text-ink-3"> · {t("раунд {n}", { n: a.round })}</span>
                        {a.comment && <p className="mt-1 whitespace-pre-line rounded-lg bg-muted px-2.5 py-1.5 text-ink-2">{a.comment}</p>}
                        <p className="mt-0.5 text-xs text-ink-3">
                          {a.deciderName} · {stampRu(a.createdAt)} · {t("к закупке")} {money(a.purchaseTotal)} {t("сум")}
                        </p>
                      </li>
                    ))}
                  </ol>
                </section>
              )}

              <section className={card} aria-labelledby="rq-history">
                <h2 id="rq-history" className="mb-3 flex items-center gap-2 text-base font-semibold text-ink">
                  <History className="size-4 text-ink-3" aria-hidden /> {t("История")}
                </h2>
                <ol className="relative space-y-4 border-l border-line pl-4">
                  {[...detail.history].reverse().map((e) => {
                    const look = e.toStatus ? requestStatusLook[e.toStatus] : null;
                    return (
                      <li key={e.id} className="relative">
                        <span className={`absolute -left-[21px] top-1.5 size-2.5 rounded-full ring-4 ring-surface ${look?.dot ?? "bg-ink-4"}`} aria-hidden />
                        <p className="text-sm text-ink">{t(e.text)}</p>
                        {e.comment && <p className="mt-1 whitespace-pre-line rounded-lg bg-muted px-2.5 py-1.5 text-sm text-ink-2">{e.comment}</p>}
                        <p className="mt-0.5 text-xs text-ink-3">
                          {e.actorName} · {stampRu(e.createdAt)}
                        </p>
                      </li>
                    );
                  })}
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
          setNote("");
          setError(null);
        }}
        title={dialog?.kind === "issue" ? t("Выдать со склада") : dialog ? t(dialogText[dialog.kind].title) : ""}
        subtitle={`${detail.number} · ${detail.title}`}
        footer={
          <div className="flex justify-end gap-2">
            <button
              type="button"
              onClick={() => {
                setDialog(null);
                setError(null);
              }}
              className={secondary}
            >
              {t("Закрыть")}
            </button>
            <button type="button" onClick={() => void confirmDialog()} disabled={busy !== null} className={dialog?.kind === "cancel" || dialog?.kind === "reject" ? `${primary} bg-bad hover:bg-bad` : primary}>
              {busy === dialog?.kind && <Loader2 className="size-4 animate-spin" aria-hidden />}
              {dialog?.kind === "issue" ? t("Выдать") : dialog ? t(dialogText[dialog.kind].button) : ""}
            </button>
          </div>
        }
      >
        {dialog?.kind === "issue" && (
          <div className="mb-4 space-y-2">
            {detail.items
              .filter((i) => i.reservedLeft > 0)
              .map((i) => (
                <label key={i.id} className="flex items-center gap-3 rounded-xl border border-line px-3 py-2">
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-sm text-ink">{i.name}</span>
                    <span className="text-xs text-ink-3">{t("в резерве {n}", { n: `${quantity(i.reservedLeft)} ${i.unitCode}` })}</span>
                  </span>
                  <input
                    value={issueQty[i.id] ?? ""}
                    onChange={(e) => setIssueQty((q) => ({ ...q, [i.id]: e.target.value }))}
                    inputMode="decimal"
                    aria-label={t("Выдать: {name}", { name: i.name })}
                    className="h-9 w-24 rounded-md border border-line bg-surface px-2 text-right text-sm tabular-nums text-ink focus:border-accent focus:outline-none"
                  />
                  <span className="w-10 text-xs text-ink-3">{i.unitCode}</span>
                </label>
              ))}
            <label className="block">
              <span className="mb-1.5 block text-sm font-medium text-ink">
                {t("Кто получил")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
              </span>
              <input value={receiver} onChange={(e) => setReceiver(e.target.value)} maxLength={200} className="h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink focus:border-accent focus:outline-none" />
            </label>
          </div>
        )}
        {dialog?.kind === "approve" && can.price && purchaseItems.length > 0 && (
          <div className="mb-4">
            <p className="mb-2 text-sm font-medium text-ink">
              {t("Цены к закупке")} <span className="font-normal text-ink-3">{t("можно уточнить")}</span>
            </p>
            <div className="space-y-2">
              {purchaseItems.map((i) => {
                const price = priceOf(i.id, i.estimatedPrice);
                return (
                  <label key={i.id} className="flex items-center gap-3 rounded-xl border border-line px-3 py-2">
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm text-ink">{i.name}</span>
                      <span className="text-xs text-ink-3">
                        {quantity(i.toPurchase)} {i.unitCode} · {price !== null ? `${money(i.toPurchase * price)} ${t("сум")}` : "—"}
                      </span>
                    </span>
                    <input
                      value={prices[i.id] ?? (i.estimatedPrice !== null ? String(i.estimatedPrice).replace(".", ",") : "")}
                      onChange={(e) => setPrices((p) => ({ ...p, [i.id]: e.target.value }))}
                      inputMode="decimal"
                      aria-label={t("Цена за единицу: {name}", { name: i.name })}
                      className="h-9 w-32 rounded-md border border-line bg-surface px-2 text-right text-sm tabular-nums text-ink focus:border-accent focus:outline-none"
                    />
                    <span className="w-8 text-xs text-ink-3">{t("сум")}</span>
                  </label>
                );
              })}
            </div>
            <p className="mt-2 text-right text-sm text-ink-2">
              {t("к закупке")} ≈ <span className="font-semibold tabular-nums text-ink">{money(purchaseNow)}</span> {t("сум")}
            </p>
          </div>
        )}
        <label htmlFor="rq-note" className="mb-1.5 block text-sm font-medium text-ink">
          {dialog?.kind === "issue" ? t("Комментарий") : dialog ? t(dialogText[dialog.kind].label) : ""}
          {dialog && (dialog.kind === "issue" || !dialogText[dialog.kind].required) && (
            <span className="font-normal text-ink-3"> {t("необязательно")}</span>
          )}
        </label>
        <textarea
          id="rq-note"
          value={note}
          onChange={(e) => setNote(e.target.value)}
          maxLength={2000}
          rows={dialog?.kind === "issue" ? 2 : 4}
          autoFocus={dialog?.kind !== "issue"}
          className="w-full rounded-lg border border-line bg-surface px-3 py-2.5 text-sm text-ink focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20"
        />
        {dialog && dialog.kind !== "issue" && dialogText[dialog.kind].hint && <p className="mt-2 text-xs text-ink-3">{t(dialogText[dialog.kind].hint!)}</p>}
        {dialog?.kind === "approve" && <p className="mt-2 text-xs text-ink-3">{approveHint(detail, purchaseNow, t)}</p>}
        <div className="mt-3">
          <ErrorNote text={error} />
        </div>
      </Sheet>
      <PurchaseSheet
        open={buying}
        onClose={() => setBuying(false)}
        request={{ id: detail.id, number: detail.number, title: detail.title }}
        lines={toBuy}
        onDone={() => {
          setBuying(false);
          router.refresh();
        }}
      />
    </>
  );
}
