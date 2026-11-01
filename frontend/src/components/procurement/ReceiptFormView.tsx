"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useMemo, useRef, useState } from "react";
import { CheckCheck, FileText, Loader2, PackageCheck, PackagePlus, Paperclip, Save, Trash2, X } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { CatalogPicker } from "@/components/requests/CatalogPicker";
import { fileProblem } from "@/components/requests/RequestForm";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { fileSize } from "@/lib/format";
import { dateRu, money, parseNumber, quantity, receiptLine, type ReceiptDetail, type ReceiptForm, type ReceiptOrderLine, type RequestCategory } from "@/lib/procurement";

const field =
  "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
const num = "h-10 w-full rounded-lg border border-line bg-surface px-2.5 text-right text-sm tabular-nums text-ink focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
const secondary = "inline-flex h-10 items-center justify-center gap-2 rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted disabled:opacity-50";
const primary = "inline-flex h-10 items-center justify-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50";
const card = "rounded-2xl border border-line bg-surface p-4 sm:p-5";

type Row = { orderItemId: string; received: string; defective: string; shortage: string; itemId: string | null; itemLabel: string | null; comment: string };

const text = (n: number) => (n ? String(n).replace(".", ",") : "");
const value = (s: string) => (s.trim() ? parseNumber(s) : 0);

/**
 * Приёмка товара по заказу (кладовщик): по каждой позиции — сколько ждёт поставки, фактически пришло, из них брак, недостача по
 * накладной; «принято на склад» считается сразу. Позиции без карточки склада привязываются к карточке (выбрать или создать — с той же
 * единицей). «Сохранить черновик» — склад не меняется; «Подтвердить поступление» — приход на склад, резерв под заявки, статус заказа.
 * Накладную и акт можно приложить сразу — файлы загрузятся после сохранения.
 */
export function ReceiptFormView({ form, categories, draft }: { form: ReceiptForm; categories: RequestCategory[]; draft?: ReceiptDetail }) {
  const t = useT();
  const router = useRouter();
  const [clientKey] = useState(() => crypto.randomUUID());
  const [reference, setReference] = useState(draft?.reference ?? "");
  const [comment, setComment] = useState(draft?.comment ?? "");
  const shown = useMemo(
    () => form.lines.filter((l) => l.remaining > 0 || draft?.lines.some((d) => d.orderItemId === l.orderItemId)),
    [form.lines, draft],
  );
  const [rows, setRows] = useState<Row[]>(() =>
    shown.map((l) => {
      const d = draft?.lines.find((x) => x.orderItemId === l.orderItemId);
      return {
        orderItemId: l.orderItemId,
        received: d ? text(d.received) : "",
        defective: d ? text(d.defective) : "",
        shortage: d ? text(d.shortage) : "",
        itemId: l.itemId ?? d?.itemId ?? null,
        itemLabel: l.itemName ? `${l.itemSku} · ${l.itemName}` : d?.itemName ? `${d.itemSku} · ${d.itemName}` : null,
        comment: d?.comment ?? "",
      };
    }),
  );
  const [files, setFiles] = useState<{ file: File; kind: string }[]>([]);
  const [busy, setBusy] = useState<"save" | "post" | "delete" | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [confirming, setConfirming] = useState(false);
  const [newCard, setNewCard] = useState<ReceiptOrderLine | null>(null);
  const fileInput = useRef<HTMLInputElement>(null);

  const patch = (id: string, p: Partial<Row>) => setRows((list) => list.map((r) => (r.orderItemId === id ? { ...r, ...p } : r)));
  const lineOf = (id: string) => form.lines.find((l) => l.orderItemId === id)!;
  const computed = rows.map((r) => {
    const l = lineOf(r.orderItemId);
    const received = value(r.received);
    const defective = value(r.defective);
    const shortage = value(r.shortage);
    if (received === null || defective === null || shortage === null)
      return { row: r, line: l, accepted: 0, error: t("«{name}»: число указано неверно.", { name: l.name }), used: true, received: 0, defective: 0, shortage: 0 };
    const check = receiptLine(l.name, l.unitCode, received, defective, shortage, l.remaining);
    return { row: r, line: l, accepted: check.accepted, error: check.error, used: received > 0 || defective > 0 || shortage > 0, received, defective, shortage };
  });
  const used = computed.filter((c) => c.used);

  const check = (post: boolean): string | null => {
    if (used.length === 0) return t("Укажите, что пришло: фактическое количество, брак или недостачу хотя бы по одной позиции.");
    const bad = used.find((c) => c.error);
    if (bad?.error) return t(bad.error);
    if (post) {
      const noCard = used.find((c) => c.accepted > 0 && !c.row.itemId);
      if (noCard) return t("«{name}»: выберите карточку склада (или создайте новую), на которую принять товар.", { name: noCard.line.name });
    }
    return null;
  };

  const save = async (post: boolean) => {
    const problem = check(post);
    if (problem) {
      setError(problem);
      setConfirming(false);
      return;
    }
    setBusy(post ? "post" : "save");
    setError(null);
    const body = {
      orderId: form.orderId,
      reference: reference.trim() || null,
      comment: comment.trim() || null,
      lines: used.map((c) => ({
        orderItemId: c.row.orderItemId,
        received: c.received,
        defective: c.defective,
        shortage: c.shortage,
        itemId: c.line.itemId ? null : c.row.itemId,
        comment: c.row.comment.trim() || null,
      })),
      post,
      clientKey: draft ? null : clientKey,
      version: draft?.version ?? null,
    };
    try {
      let id = draft?.id;
      if (draft) await bff(`procurement/receipts/${draft.id}`, { method: "PUT", body: JSON.stringify(body) });
      else id = (await bff<{ id: string }>("procurement/receipts", { method: "POST", body: JSON.stringify(body) })).id;
      for (const f of files) {
        const data = new FormData();
        data.append("file", f.file);
        const response = await fetch(`/bff/api/procurement/documents?owner=receipt&id=${id}&kind=${f.kind}`, { method: "POST", body: data });
        if (!response.ok) {
          const json = await response.json().catch(() => ({}));
          throw new Error(`«${f.file.name}»: ${json.error ? t(json.error) : t("Ошибка {status}", { status: response.status })}`);
        }
      }
      router.push(`/procurement/receipts/${id}`);
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setConfirming(false);
    } finally {
      setBusy(null);
    }
  };

  const remove = async () => {
    if (!draft || !window.confirm(t("Удалить черновик приёмки {n}?", { n: draft.number }))) return;
    setBusy("delete");
    try {
      await bff(`procurement/receipts/${draft.id}`, { method: "DELETE" });
      router.push("/procurement/receipts");
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(null);
    }
  };

  const addFiles = (list: FileList) => {
    const next = [...files];
    for (const file of Array.from(list)) {
      const problem = fileProblem(file);
      if (problem) {
        setError(`«${file.name}»: ${t(problem)}`);
        continue;
      }
      next.push({ file, kind: "waybill" });
    }
    setFiles(next);
  };

  const totalAccepted = used.filter((c) => !c.error).reduce((s, c) => s + c.accepted * c.line.unitPrice, 0);

  return (
    <div className="mx-auto max-w-6xl space-y-4">
      <section className={`${card} flex flex-wrap items-center justify-between gap-3`}>
        <div className="min-w-0 text-sm">
          <Link href={`/procurement/orders/${form.orderId}`} className="font-semibold tabular-nums text-accent-strong hover:underline">
            {form.orderNumber}
          </Link>
          <span className="text-ink-2"> · {form.supplierName}</span>
          <span className={`block text-xs ${form.overdue ? "font-semibold text-bad" : "text-ink-3"}`}>
            {form.expectedDate ? t("ожидается {date}", { date: dateRu(form.expectedDate) }) : t("дата поставки не указана")}
          </span>
        </div>
        <button
          type="button"
          onClick={() => setRows((list) => list.map((r) => ({ ...r, received: text(lineOf(r.orderItemId).remaining), defective: "", shortage: "" })))}
          className={secondary}
        >
          <CheckCheck className="size-4" aria-hidden /> {t("Пришло всё, что ждём")}
        </button>
      </section>

      <section className={card} aria-labelledby="rc-lines">
        <h2 id="rc-lines" className="text-base font-semibold text-ink">
          {t("Позиции")}
        </h2>
        <p className="mb-3 mt-0.5 text-xs text-ink-3">
          {t("Пришло — всё, что фактически привезли (с браком). Брак на склад не идёт. Недостача — сколько не довезли против накладной. Принять больше, чем ждёт поставки, нельзя.")}
        </p>
        {shown.length === 0 ? (
          <p className="rounded-xl bg-muted px-4 py-6 text-center text-sm text-ink-3">{t("По заказу больше ничего не ждут.")}</p>
        ) : (
          <ul className="space-y-3">
            {computed.map(({ row, line, accepted, error: lineError }) => (
              <li key={row.orderItemId} className="@container rounded-xl border border-line p-3">
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <div className="min-w-0">
                    <p className="font-medium text-ink">{line.name}</p>
                    <p className="text-xs text-ink-3">
                      <Link href={`/requests/${line.requestId}`} className="text-accent-strong hover:underline">
                        {line.requestNumber}
                      </Link>
                      {" · "}
                      {t("заказано {a}, получено {b}", { a: `${quantity(line.ordered)} ${line.unitCode}`, b: quantity(line.received) })}
                      {line.closed > 0 && ` · ${t("закрыто {n}", { n: quantity(line.closed) })}`}
                      {" · "}
                      <span className="font-semibold text-ink-2">{t("ждёт {n} {unit}", { n: quantity(line.remaining), unit: line.unitCode })}</span>
                    </p>
                  </div>
                  <span className={`shrink-0 rounded-md px-2 py-0.5 text-xs font-semibold tabular-nums ${accepted > 0 ? "bg-ok-soft text-ok" : "bg-muted text-ink-3"}`}>
                    {t("на склад: {n} {unit}", { n: quantity(accepted), unit: line.unitCode })}
                  </span>
                </div>
                <div className="mt-2.5 grid grid-cols-3 gap-2 @lg:grid-cols-[repeat(3,minmax(0,8rem))_minmax(0,1fr)]">
                  <label className="block min-w-0">
                    <span className="mb-1 block text-xs font-medium text-ink-2">{t("Пришло")}</span>
                    <input inputMode="decimal" value={row.received} onChange={(e) => patch(row.orderItemId, { received: e.target.value })} aria-label={t("Пришло: {name}", { name: line.name })} className={num} />
                  </label>
                  <label className="block min-w-0">
                    <span className="mb-1 block text-xs font-medium text-ink-2">{t("Брак")}</span>
                    <input inputMode="decimal" value={row.defective} onChange={(e) => patch(row.orderItemId, { defective: e.target.value })} aria-label={t("Брак: {name}", { name: line.name })} className={num} />
                  </label>
                  <label className="block min-w-0">
                    <span className="mb-1 block text-xs font-medium text-ink-2">{t("Недостача")}</span>
                    <input inputMode="decimal" value={row.shortage} onChange={(e) => patch(row.orderItemId, { shortage: e.target.value })} aria-label={t("Недостача: {name}", { name: line.name })} className={num} />
                  </label>
                  <label className="col-span-3 block min-w-0 @lg:col-span-1">
                    <span className="mb-1 block text-xs font-medium text-ink-2">
                      {t("Комментарий")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
                    </span>
                    <input value={row.comment} maxLength={500} onChange={(e) => patch(row.orderItemId, { comment: e.target.value })} className={field} />
                  </label>
                </div>
                <div className="mt-2.5">
                  {line.itemId ? (
                    <p className="text-xs text-ink-3">
                      {t("Карточка склада")}: <span className="text-ink-2">{row.itemLabel}</span>
                    </p>
                  ) : (
                    <div className="flex flex-wrap items-end gap-2">
                      <div className="min-w-0 flex-1 basis-64">
                        <CatalogPicker
                          label={t("Карточка склада")}
                          name={row.itemLabel ?? ""}
                          linked={row.itemId !== null}
                          placeholder={t("Найдите по артикулу или названию")}
                          onPick={(o) => {
                            if (o.unitCode !== line.unitCode) {
                              setError(t("«{name}»: в заказе единица «{a}», у карточки «{b}» — «{c}». Выберите карточку с той же единицей или создайте новую.", { name: line.name, a: line.unitCode, b: o.name, c: o.unitCode }));
                              return;
                            }
                            setError(null);
                            patch(row.orderItemId, { itemId: o.id, itemLabel: `${o.sku} · ${o.name}` });
                          }}
                          onUnlink={() => patch(row.orderItemId, { itemId: null, itemLabel: null })}
                        />
                      </div>
                      {row.itemId === null && (
                        <button type="button" onClick={() => setNewCard(line)} className={secondary}>
                          <PackagePlus className="size-4" aria-hidden /> {t("Новая карточка")}
                        </button>
                      )}
                    </div>
                  )}
                </div>
                {lineError && <p className="mt-2 text-xs font-medium text-bad">{t(lineError)}</p>}
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className={`${card} grid gap-3 md:grid-cols-2`}>
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">
            {t("Номер накладной")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
          </span>
          <input value={reference} maxLength={100} onChange={(e) => setReference(e.target.value)} className={field} />
        </label>
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">
            {t("Комментарий")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
          </span>
          <input value={comment} maxLength={1000} onChange={(e) => setComment(e.target.value)} className={field} />
        </label>
        <div className="md:col-span-2">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Накладная, акт приёмки")}</span>
          {files.length > 0 && (
            <ul className="mb-2 space-y-1">
              {files.map((f, i) => (
                <li key={`${f.file.name}-${i}`} className="flex flex-wrap items-center gap-2 rounded-lg bg-muted px-2.5 py-1.5 text-sm">
                  <FileText className="size-4 shrink-0 text-ink-3" aria-hidden />
                  <span className="min-w-0 flex-1 truncate text-ink">{f.file.name}</span>
                  <span className="text-xs text-ink-3">{fileSize(f.file.size)}</span>
                  <select
                    value={f.kind}
                    onChange={(e) => setFiles((list) => list.map((x, j) => (j === i ? { ...x, kind: e.target.value } : x)))}
                    aria-label={t("Вид документа")}
                    className="h-8 rounded-md border border-line bg-surface px-2 text-xs"
                  >
                    <option value="waybill">{t("Накладная")}</option>
                    <option value="act">{t("Акт")}</option>
                    <option value="invoice">{t("Счёт")}</option>
                    <option value="other">{t("Документ")}</option>
                  </select>
                  <button type="button" onClick={() => setFiles((list) => list.filter((_, j) => j !== i))} aria-label={t("Убрать")} className="grid size-8 place-items-center rounded-md text-ink-3 hover:bg-surface hover:text-bad">
                    <X className="size-4" />
                  </button>
                </li>
              ))}
            </ul>
          )}
          <input ref={fileInput} type="file" multiple hidden onChange={(e) => e.target.files && (addFiles(e.target.files), (e.target.value = ""))} />
          <button type="button" onClick={() => fileInput.current?.click()} className="inline-flex h-9 items-center gap-2 rounded-lg border border-dashed border-line px-3 text-sm text-ink-2 hover:bg-muted">
            <Paperclip className="size-4" aria-hidden /> {t("Прикрепить файл")}
          </button>
          {draft && draft.documents.length > 0 && <p className="mt-2 text-xs text-ink-3">{t("Уже прикреплено: {n}", { n: draft.documents.length })}</p>}
        </div>
      </section>

      <ErrorNote text={confirming ? null : error} />
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          {draft?.can.delete && (
            <button type="button" onClick={() => void remove()} disabled={busy !== null} className={`${secondary} text-bad`}>
              {busy === "delete" ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Trash2 className="size-4" aria-hidden />} {t("Удалить черновик")}
            </button>
          )}
        </div>
        <div className="flex flex-wrap gap-2">
          <button type="button" onClick={() => void save(false)} disabled={busy !== null || shown.length === 0} className={secondary}>
            {busy === "save" ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Save className="size-4" aria-hidden />} {t("Сохранить черновик")}
          </button>
          <button
            type="button"
            onClick={() => {
              const problem = check(true);
              if (problem) setError(problem);
              else {
                setError(null);
                setConfirming(true);
              }
            }}
            disabled={busy !== null || shown.length === 0}
            className={primary}
          >
            <PackageCheck className="size-4" aria-hidden /> {t("Подтвердить поступление")}
          </button>
        </div>
      </div>

      <Sheet
        open={confirming}
        onClose={() => setConfirming(false)}
        title={t("Подтвердить поступление")}
        subtitle={`${form.orderNumber} · ${form.supplierName}`}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" onClick={() => setConfirming(false)} className={secondary}>
              {t("Закрыть")}
            </button>
            <button type="button" onClick={() => void save(true)} disabled={busy !== null} className={primary}>
              {busy === "post" && <Loader2 className="size-4 animate-spin" aria-hidden />}
              {t("Подтвердить")}
            </button>
          </div>
        }
      >
        <p className="text-sm text-ink-2">{t("Склад пополнится, товар зарезервируется под заявки. Подтверждённую приёмку не правят — исправление делается новым документом.")}</p>
        <ul className="mt-3 divide-y divide-line rounded-xl border border-line text-sm">
          {used.map((c) => (
            <li key={c.row.orderItemId} className="flex items-start justify-between gap-3 px-3 py-2">
              <span className="min-w-0">
                <span className="block text-ink">{c.line.name}</span>
                {(c.defective > 0 || c.shortage > 0) && (
                  <span className="block text-xs text-bad">
                    {[
                      c.defective > 0 ? t("брак {n} {unit}", { n: quantity(c.defective), unit: c.line.unitCode }) : null,
                      c.shortage > 0 ? t("недостача {n} {unit}", { n: quantity(c.shortage), unit: c.line.unitCode }) : null,
                    ]
                      .filter(Boolean)
                      .join(", ")}{" "}
                    — {t("претензия поставщику")}
                  </span>
                )}
              </span>
              <span className="shrink-0 font-semibold tabular-nums text-ink">
                +{quantity(c.accepted)} {c.line.unitCode}
              </span>
            </li>
          ))}
        </ul>
        <p className="mt-2 text-right text-xs text-ink-3">{t("по ценам заказа ≈ {sum} сум", { sum: money(totalAccepted) })}</p>
        <div className="mt-3">
          <ErrorNote text={error} />
        </div>
      </Sheet>

      <NewCardSheet
        line={newCard}
        categories={categories}
        onClose={() => setNewCard(null)}
        onCreated={(id, label) => {
          if (newCard) patch(newCard.orderItemId, { itemId: id, itemLabel: label });
          setNewCard(null);
        }}
      />
    </div>
  );
}

/** Новая карточка склада для позиции без карточки: название и категория — из заявки, единица — как в заказе (её не сменить). */
function NewCardSheet({
  line,
  categories,
  onClose,
  onCreated,
}: {
  line: ReceiptOrderLine | null;
  categories: RequestCategory[];
  onClose: () => void;
  onCreated: (id: string, label: string) => void;
}) {
  const t = useT();
  const [sku, setSku] = useState("");
  const [name, setName] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [minQty, setMinQty] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [shownFor, setShownFor] = useState<string | null>(null);
  if (line && shownFor !== line.orderItemId) {
    setShownFor(line.orderItemId);
    setSku("");
    setName(line.name);
    setCategoryId(line.categoryId);
    setMinQty("");
    setError(null);
  }

  const create = async () => {
    if (!line) return;
    if (!sku.trim() || !name.trim() || !categoryId) {
      setError(t("Укажите артикул, название и категорию."));
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const { id } = await bff<{ id: string }>("procurement/items", {
        method: "POST",
        body: JSON.stringify({ sku: sku.trim(), name: name.trim(), categoryId, unitCode: line.unitCode, barcode: null, description: null, minQty: parseNumber(minQty) ?? 0 }),
      });
      onCreated(id, `${sku.trim()} · ${name.trim()}`);
      setShownFor(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Sheet
      open={line !== null}
      onClose={() => {
        setShownFor(null);
        onClose();
      }}
      title={t("Новая карточка склада")}
      subtitle={line?.name}
      footer={
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onClose} className={secondary}>
            {t("Закрыть")}
          </button>
          <button type="button" onClick={() => void create()} disabled={busy} className={primary}>
            {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}
            {t("Создать")}
          </button>
        </div>
      }
    >
      <div className="space-y-3">
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Артикул (SKU)")}</span>
          <input value={sku} maxLength={64} onChange={(e) => setSku(e.target.value)} autoFocus className={field} />
        </label>
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Название")}</span>
          <input value={name} maxLength={300} onChange={(e) => setName(e.target.value)} className={field} />
        </label>
        <div className="grid grid-cols-2 gap-3">
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Категория")}</span>
            <select value={categoryId} onChange={(e) => setCategoryId(e.target.value)} className={field}>
              <option value="">{t("Выберите")}</option>
              {categories
                .filter((c) => c.isActive || c.id === categoryId)
                .map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
            </select>
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Единица")}</span>
            <input value={line?.unitCode ?? ""} disabled className={`${field} opacity-70`} />
          </label>
        </div>
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">
            {t("Минимальный остаток")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
          </span>
          <input inputMode="decimal" value={minQty} onChange={(e) => setMinQty(e.target.value)} className={field} />
        </label>
        <ErrorNote text={error} />
      </div>
    </Sheet>
  );
}
