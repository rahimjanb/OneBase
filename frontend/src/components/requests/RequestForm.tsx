"use client";

import { useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { AlertTriangle, FileText, Loader2, Paperclip, Plus, Save, Send, Trash2, Warehouse, Wheat, X } from "lucide-react";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { fileSize } from "@/lib/format";
import {
  checkDraft,
  draftTotal,
  kindAccepts,
  lineSum,
  money,
  parseNumber,
  purchaseTotal,
  quantity as qtyText,
  split,
  stockCover,
  rawStepLabel,
  requestKindLabel,
  requestPriorities,
  requestPriorityLabel,
  type ItemDraft,
  type RequestDetail,
  type RequestFormData,
  type RequestKind,
  type RequestPriority,
} from "@/lib/procurement";
import { todayInTashkent } from "@/lib/tasks";
import { CatalogPicker } from "./CatalogPicker";

const field =
  "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20 disabled:opacity-60";
const label = "mb-1.5 block text-sm font-medium text-ink";
const card = "rounded-2xl border border-line bg-surface p-4 sm:p-5";

/** Вложения, которые принимает сервер (тип определяется по расширению), и предел размера. */
export const allowedFiles = [".pdf", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".heic", ".txt", ".csv", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".zip"];
export const maxFileBytes = 20 * 1024 * 1024;

/** Проверка файла до загрузки: тип и размер; ошибка — текст на русском (переводится), иначе null. */
export function fileProblem(file: File): string | null {
  const ext = file.name.includes(".") ? file.name.slice(file.name.lastIndexOf(".")).toLowerCase() : "";
  if (!allowedFiles.includes(ext)) return "Можно прикрепить PDF, Word, Excel, PowerPoint, картинку, текст или ZIP.";
  if (file.size > maxFileBytes) return "Файл больше 20 МБ.";
  if (file.size === 0) return "Файл пустой.";
  return null;
}

/** Загрузка вложения к заявке (multipart — без Content-Type: браузер сам ставит границу). */
export async function uploadRequestFile(requestId: string, file: File) {
  const form = new FormData();
  form.append("file", file);
  const response = await fetch(`/bff/api/procurement/requests/${requestId}/attachments`, { method: "POST", body: form });
  if (!response.ok) {
    const json = await response.json().catch(() => ({}));
    throw new Error(json.error ?? `HTTP ${response.status}`);
  }
}

let seq = 0;
const key = () => `row-${Date.now()}-${seq++}`;

function blankItem(categoryId: string, unitCode = "шт"): ItemDraft {
  return { key: key(), id: null, name: "", categoryId, unitCode, quantity: "", price: "", comment: "" };
}

const numberText = (n: number | null) => (n === null ? "" : String(n).replace(".", ","));

/**
 * Новая заявка и правка (черновик или заявка на доработке): тип (обычная закупка или закупка сырья — тем, кому сырьё разрешено), название,
 * отдел, приоритет, желаемая дата, позиции строками (наименование, категория, количество, единица, цена — сумма считается сразу), причина и
 * комментарий, вложения. Категории и товары каталога — по типу: сырьё только в заявке на сырьё. «Сохранить черновик» — без отправки,
 * «Отправить» — сразу на согласование. Повторное нажатие не создаёт вторую заявку (ключ формы). Сервер проверяет всё ещё раз.
 */
/**
 * Заполненная новая заявка (из «Аналитика»: «Создать заявку в снабжение»): название, причина, приоритет, желаемая дата и позиция каталога
 * с количеством и ценой. Всё можно поправить перед отправкой.
 */
export type RequestPrefill = {
  title: string;
  reason: string;
  priority: RequestPriority;
  desiredDate: string;
  item: { itemId: string | null; sku: string | null; name: string; categoryId: string; unitCode: string; available: number | null; quantity: number; price: number | null };
};

export function RequestForm({
  form,
  initial,
  defaultKind = "Regular",
  prefill,
}: {
  form: RequestFormData;
  initial?: RequestDetail;
  defaultKind?: RequestKind;
  prefill?: RequestPrefill | null;
}) {
  const t = useT();
  const router = useRouter();
  const today = todayInTashkent();
  const clientKey = useRef<string>(typeof crypto !== "undefined" && "randomUUID" in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`);
  const fileInput = useRef<HTMLInputElement>(null);

  const [kind, setKind] = useState<RequestKind>(initial?.kind ?? (form.canCreateRaw ? defaultKind : "Regular"));
  const [title, setTitle] = useState(initial?.title ?? prefill?.title ?? "");
  const [departmentId, setDepartmentId] = useState(initial?.departmentId ?? form.defaultDepartmentId ?? "");
  const [priority, setPriority] = useState<RequestPriority>(initial?.priority ?? prefill?.priority ?? "Normal");
  const [desiredDate, setDesiredDate] = useState(initial?.desiredDate ?? prefill?.desiredDate ?? "");
  const [reason, setReason] = useState(initial?.reason ?? prefill?.reason ?? "");
  const [comment, setComment] = useState(initial?.comment ?? "");
  const [items, setItems] = useState<ItemDraft[]>(() =>
    initial?.items.length
      ? initial.items.map((i) => ({
          key: key(),
          id: i.id,
          name: i.name,
          categoryId: i.categoryId,
          unitCode: i.unitCode,
          quantity: numberText(i.quantity),
          price: numberText(i.estimatedPrice),
          comment: i.comment ?? "",
          itemId: i.itemId,
          sku: i.sku,
          // Доступно для этой позиции: свободное на складе плюс её же резерв (он уже её).
          available: i.itemId ? (i.available ?? 0) + i.reservedLeft : null,
          fromStock: numberText(i.fromStock),
          fromStockTouched: true,
          purchaseReason: i.purchaseReason ?? "",
          issued: i.issuedQty,
        }))
      : prefill
        ? [
            {
              key: key(),
              id: null,
              name: prefill.item.name,
              categoryId: prefill.item.categoryId,
              unitCode: prefill.item.unitCode,
              quantity: numberText(prefill.item.quantity),
              price: numberText(prefill.item.price),
              comment: "",
              itemId: prefill.item.itemId,
              sku: prefill.item.sku,
              available: prefill.item.available,
              // Количество — нехватка сверх остатка: со склада не берётся (иначе закупка станет меньше нужной).
              fromStock: "0",
              fromStockTouched: !!prefill.item.itemId,
              purchaseReason: prefill.item.itemId && (prefill.item.available ?? 0) > 0 ? prefill.reason : "",
            },
          ]
        : [blankItem("")],
  );
  const [files, setFiles] = useState<File[]>([]);
  const [busy, setBusy] = useState<"draft" | "submit" | null>(null);
  const [error, setError] = useState<string | null>(null);

  // Категория позиции, которой уже нет среди активных (отключили после создания), остаётся в списке этой позиции.
  const categories = useMemo(() => {
    const list = [...form.categories];
    for (const item of initial?.items ?? []) {
      if (!list.some((c) => c.id === item.categoryId)) list.push({ id: item.categoryId, name: item.categoryName, code: null, sortOrder: 9999, isActive: false, used: 0 });
    }
    return list;
  }, [form.categories, initial]);

  const total = draftTotal(items);
  const toBuy = purchaseTotal(items);
  const department = form.departments.find((d) => d.id === departmentId);
  const revision = initial?.status === "RevisionRequired";
  // Правка утверждённой заявки снабжением: название, отдел и причина — как утверждены; склад у позиций не меняется; существенное
  // изменение отправит заявку директору повторно.
  const approvedEdit = initial?.can.approvedEdit ?? false;
  const stockLocked = (item: ItemDraft) => (item.issued ?? 0) > 0 || (approvedEdit && !!item.id && (parseNumber(item.fromStock ?? "") ?? 0) > 0);
  // Тип выбирают те, кому разрешена закупка сырья (и автор черновика на сырьё — чтобы вернуть его в обычную); у утверждённой — не меняется.
  const choosesKind = !approvedEdit && (form.canCreateRaw || initial?.kind === "Raw");
  const categoryOf = (id: string) => categories.find((c) => c.id === id);
  // Сменили тип: позиции чужих категорий — без категории (и без товара каталога), чтобы выбрать подходящие.
  const changeKind = (next: RequestKind) => {
    setKind(next);
    setItems((list) =>
      list.map((i) =>
        !i.categoryId || kindAccepts(next, categoryOf(i.categoryId)) || stockLocked(i)
          ? i
          : { ...i, categoryId: "", ...(i.itemId ? { itemId: null, sku: null, available: null, fromStock: "0", fromStockTouched: false, purchaseReason: "" } : {}) },
      ),
    );
  };

  const patch = (rowKey: string, change: Partial<ItemDraft>) => setItems((list) => list.map((i) => (i.key === rowKey ? { ...i, ...change } : i)));
  // Количество или выбранный товар поменялись — «со склада» подставляется само (сколько есть), пока пользователь не менял его руками.
  const withStock = (item: ItemDraft): ItemDraft =>
    item.itemId && !item.fromStockTouched && !approvedEdit ? { ...item, fromStock: numberText(stockCover(item)) } : item;
  const patchItem = (rowKey: string, change: Partial<ItemDraft>) => setItems((list) => list.map((i) => (i.key === rowKey ? withStock({ ...i, ...change }) : i)));
  // Новая строка — с категорией предыдущей (заявки обычно в одной категории), единица — «шт»: категорию заранее не выбираем, чтобы её не
  // пропустили незаметно.
  const addRow = () => setItems((list) => [...list, blankItem(list[list.length - 1]?.categoryId ?? "")]);
  const removeRow = (rowKey: string) => setItems((list) => (list.length === 1 ? [blankItem("")] : list.filter((i) => i.key !== rowKey)));

  const addFiles = (list: FileList) => {
    const next: File[] = [];
    for (const file of Array.from(list)) {
      const problem = fileProblem(file);
      if (problem) {
        setError(`«${file.name}»: ${t(problem)}`);
        continue;
      }
      next.push(file);
    }
    setFiles((current) => [...current, ...next].slice(0, 20));
  };

  const save = async (submit: boolean) => {
    setError(null);
    const problem = checkDraft({ title, reason, items }, submit);
    if (problem) {
      setError(t(problem));
      return;
    }
    if (form.departments.length > 1 && !departmentId) {
      setError(t("Выберите отдел заявки."));
      return;
    }
    setBusy(submit ? "submit" : "draft");
    const body = {
      title: title.trim(),
      departmentId: departmentId || null,
      priority,
      desiredDate: desiredDate || null,
      reason: reason.trim() || null,
      comment: comment.trim() || null,
      items: items
        .filter((i) => i.name.trim() || i.quantity.trim() || i.price.trim())
        .map((i) => ({
          id: i.id,
          name: i.name.trim(),
          categoryId: i.categoryId,
          unitCode: i.unitCode,
          quantity: parseNumber(i.quantity),
          estimatedPrice: parseNumber(i.price),
          comment: i.comment.trim() || null,
          itemId: i.itemId ?? null,
          fromStock: i.itemId ? split(i).fromStock : 0,
          purchaseReason: (i.purchaseReason ?? "").trim() || null,
        })),
      submit,
      clientKey: initial ? null : clientKey.current,
      version: initial?.version ?? null,
      kind,
    };
    try {
      let id = initial?.id;
      if (initial) {
        await bff(`procurement/requests/${initial.id}`, { method: "PUT", body: JSON.stringify(body) });
      } else {
        id = (await bff<{ id: string }>("procurement/requests", { method: "POST", body: JSON.stringify(body) })).id;
      }
      let failed = 0;
      for (const file of files) {
        try {
          await uploadRequestFile(id!, file);
        } catch {
          failed++;
        }
      }
      router.push(`/requests/${id}${failed > 0 ? "?files=failed" : ""}`);
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setBusy(null);
    }
  };

  const actions = approvedEdit ? (
    <button
      type="button"
      onClick={() => void save(false)}
      disabled={busy !== null}
      className="inline-flex h-11 flex-1 items-center justify-center gap-2 rounded-xl bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50 lg:h-10 lg:w-full lg:flex-none"
    >
      {busy ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Save className="size-4" aria-hidden />}
      {t("Сохранить изменения")}
    </button>
  ) : (
    <>
      <button
        type="button"
        onClick={() => void save(false)}
        disabled={busy !== null}
        className="inline-flex h-11 flex-1 items-center justify-center gap-2 rounded-xl border border-line bg-surface px-4 text-sm font-semibold text-ink hover:bg-muted disabled:opacity-50 lg:h-10 lg:w-full lg:flex-none"
      >
        {busy === "draft" && <Loader2 className="size-4 animate-spin" aria-hidden />}
        {initial && initial.status !== "Draft" ? t("Сохранить") : t("Сохранить черновик")}
      </button>
      <button
        type="button"
        onClick={() => void save(true)}
        disabled={busy !== null}
        className="inline-flex h-11 flex-1 items-center justify-center gap-2 rounded-xl bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50 lg:h-10 lg:w-full lg:flex-none"
      >
        {busy === "submit" ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Send className="size-4" aria-hidden />}
        {revision ? t("Отправить повторно") : t("Отправить")}
      </button>
    </>
  );

  return (
    <div className="mx-auto grid max-w-6xl gap-4 lg:grid-cols-[minmax(0,1fr)_300px]">
      <div className="min-w-0 space-y-4">
        {approvedEdit && (
          <p className="flex items-start gap-2 rounded-2xl border border-violet/30 bg-violet-soft px-4 py-3 text-sm text-ink">
            <AlertTriangle className="mt-0.5 size-4 shrink-0 text-violet-light" aria-hidden />
            <span>
              {initial?.kind === "Raw"
                ? t("Заявка на сырьё утверждена. Рост количества к закупке или цены, новые и удалённые позиции отправят её на повторное согласование с первого шага маршрута; уменьшение — только в историю.")
                : t("Заявка утверждена директором. Рост количества к закупке или цены, новые и удалённые позиции отправят её директору на повторное согласование; уменьшение — только в историю.")}
            </span>
          </p>
        )}
        {initial?.revisionComment && (
          <p className="rounded-2xl border border-warn/30 bg-warn-soft px-4 py-3 text-sm text-ink">
            <span className="font-semibold text-warn">{t("Что исправить")}: </span>
            {initial.revisionComment}
          </p>
        )}

        <section className={card}>
          <div className="grid gap-4 sm:grid-cols-2">
            {choosesKind && (
              <div className="sm:col-span-2">
                <span className={label}>{t("Тип заявки")}</span>
                <div role="radiogroup" aria-label={t("Тип заявки")} className="grid grid-cols-2 gap-1 rounded-xl bg-muted p-1">
                  {(["Regular", "Raw"] as const).map((k) => (
                    <button
                      key={k}
                      type="button"
                      role="radio"
                      aria-checked={kind === k}
                      disabled={k === "Raw" && !form.canCreateRaw}
                      onClick={() => kind !== k && changeKind(k)}
                      className={`inline-flex h-9 items-center justify-center gap-1.5 rounded-lg text-sm font-medium transition-colors disabled:opacity-50 ${
                        kind === k ? "bg-surface text-ink shadow-sm" : "text-ink-2 hover:text-ink"
                      }`}
                    >
                      {k === "Raw" && <Wheat className="size-4" aria-hidden />}
                      {t(requestKindLabel[k])}
                    </button>
                  ))}
                </div>
                <p className="mt-1.5 text-xs text-ink-3">
                  {kind === "Raw"
                    ? form.rawSteps?.length
                      ? t("Только позиции категорий сырья. Согласуют: {steps} → снабжение.", { steps: form.rawSteps.map((s) => t(rawStepLabel[s]).toLowerCase()).join(" → ") })
                      : t("Только позиции категорий сырья.")
                    : t("Сырьё — отдельной заявкой «Закупка сырья».")}
                </p>
              </div>
            )}
            <div className="sm:col-span-2">
              <label htmlFor="rq-title" className={label}>
                {t("Название")}
              </label>
              <input
                id="rq-title"
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                maxLength={200}
                placeholder={kind === "Raw" ? t("Например: мука и какао на ноябрь") : t("Например: канцелярия на ноябрь")}
                className={field}
                autoFocus={!initial}
                disabled={approvedEdit}
              />
            </div>
            <div>
              <span className={label}>{t("Отдел")}</span>
              {form.departments.length > 1 && !approvedEdit ? (
                <select value={departmentId} onChange={(e) => setDepartmentId(e.target.value)} className={field} aria-label={t("Отдел")}>
                  {!departmentId && <option value="">{t("Выберите отдел")}</option>}
                  {form.departments.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.name}
                    </option>
                  ))}
                </select>
              ) : (
                <p className="flex h-10 items-center rounded-lg bg-muted px-3 text-sm text-ink-2">
                  {approvedEdit ? initial?.departmentName : (department?.name ?? form.departments[0]?.name ?? t("Отдел не указан"))}
                </p>
              )}
            </div>
            <div>
              <label htmlFor="rq-date" className={label}>
                {t("Нужно к дате")}
              </label>
              <input id="rq-date" type="date" value={desiredDate} min={desiredDate && desiredDate < today ? desiredDate : today} onChange={(e) => setDesiredDate(e.target.value)} className={field} />
            </div>
            <div className="sm:col-span-2">
              <span className={label}>{t("Приоритет")}</span>
              <div role="radiogroup" aria-label={t("Приоритет")} className="grid grid-cols-4 gap-1 rounded-xl bg-muted p-1">
                {requestPriorities.map((p) => (
                  <button
                    key={p}
                    type="button"
                    role="radio"
                    aria-checked={priority === p}
                    onClick={() => setPriority(p)}
                    className={`h-9 rounded-lg text-sm font-medium transition-colors ${
                      priority === p ? (p === "Urgent" ? "bg-surface text-bad shadow-sm" : "bg-surface text-ink shadow-sm") : "text-ink-2 hover:text-ink"
                    }`}
                  >
                    {t(requestPriorityLabel[p])}
                  </button>
                ))}
              </div>
            </div>
          </div>
        </section>

        <section className={`${card} @container`} aria-labelledby="rq-items">
          <div className="mb-3 flex items-center justify-between gap-3">
            <h2 id="rq-items" className="text-base font-semibold text-ink">
              {t("Позиции")}
            </h2>
            <span className="text-xs text-ink-3">
              {kind === "Raw" ? t("Цена за единицу обязательна для закупки") : t("Цена за единицу обязательна для закупки — по сумме решается, нужен ли директор")}
            </span>
          </div>

          <div className="hidden grid-cols-[24px_minmax(0,2fr)_minmax(0,1.6fr)_84px_92px_116px_104px_32px] gap-2 border-b border-line pb-2 text-[11px] font-semibold uppercase tracking-wide text-ink-3 @3xl:grid">
            <span>№</span>
            <span>{t("Наименование")}</span>
            <span>{t("Категория")}</span>
            <span>{t("Кол-во")}</span>
            <span>{t("Ед.")}</span>
            <span>{t("Цена, сум")}</span>
            <span className="text-right">{t("Сумма")}</span>
            <span />
          </div>

          <ul className="divide-y divide-line">
            {items.map((item, index) => {
              const sum = lineSum(item);
              return (
                <li key={item.key} className="py-3 @3xl:py-2">
                  <div className="grid grid-cols-2 gap-2 @lg:grid-cols-4 @3xl:grid-cols-[24px_minmax(0,2fr)_minmax(0,1.6fr)_84px_92px_116px_104px_32px] @3xl:items-center">
                    <span className="col-span-2 flex items-center justify-between text-xs font-semibold text-ink-3 @lg:col-span-4 @3xl:col-span-1 @3xl:block @3xl:text-sm @3xl:font-medium">
                      <span>
                        <span className="@3xl:hidden">{t("Позиция")} </span>
                        {index + 1}
                      </span>
                      <button type="button" onClick={() => removeRow(item.key)} aria-label={t("Убрать позицию {n}", { n: index + 1 })} className="grid size-8 place-items-center rounded-md text-ink-3 hover:text-bad @3xl:hidden">
                        <Trash2 className="size-4" aria-hidden />
                      </button>
                    </span>
                    <div className="col-span-2 min-w-0 @3xl:col-span-1">
                      <CatalogPicker
                        free
                        name={item.name}
                        sku={item.sku}
                        linked={!!item.itemId}
                        locked={stockLocked(item)}
                        label={t("Наименование, позиция {n}", { n: index + 1 })}
                        accept={(o) => kindAccepts(kind, categoryOf(o.categoryId))}
                        onText={(name) => patch(item.key, { name })}
                        onPick={(o) =>
                          patchItem(item.key, {
                            itemId: o.id,
                            sku: o.sku,
                            name: o.name,
                            categoryId: o.categoryId,
                            unitCode: o.unitCode,
                            available: o.available,
                            fromStockTouched: false,
                          })
                        }
                        onUnlink={() => patch(item.key, { itemId: null, sku: null, available: null, fromStock: "0", fromStockTouched: false, purchaseReason: "" })}
                      />
                    </div>
                    <select
                      value={item.categoryId}
                      onChange={(e) => patch(item.key, { categoryId: e.target.value })}
                      disabled={!!item.itemId}
                      aria-label={t("Категория, позиция {n}", { n: index + 1 })}
                      className={`${field} col-span-2 truncate @3xl:col-span-1 ${item.categoryId ? "" : "text-ink-3"}`}
                    >
                      {!item.categoryId && <option value="">{t("Выберите категорию")}</option>}
                      {categories
                        .filter((c) => kindAccepts(kind, c) || c.id === item.categoryId)
                        .map((c) => (
                          <option key={c.id} value={c.id}>
                            {c.name}
                          </option>
                        ))}
                    </select>
                    <input
                      value={item.quantity}
                      onChange={(e) => patchItem(item.key, { quantity: e.target.value })}
                      inputMode="decimal"
                      placeholder="0"
                      aria-label={t("Количество, позиция {n}", { n: index + 1 })}
                      className={`${field} tabular-nums`}
                    />
                    <select
                      value={item.unitCode}
                      onChange={(e) => patch(item.key, { unitCode: e.target.value })}
                      disabled={!!item.itemId}
                      aria-label={t("Единица, позиция {n}", { n: index + 1 })}
                      className={`${field} truncate`}
                    >
                      {form.units.map((u) => (
                        <option key={u.code} value={u.code}>
                          {u.code}
                        </option>
                      ))}
                    </select>
                    <input
                      value={item.price}
                      onChange={(e) => patch(item.key, { price: e.target.value })}
                      inputMode="decimal"
                      placeholder={t("примерно")}
                      aria-label={t("Цена за единицу, позиция {n}", { n: index + 1 })}
                      className={`${field} tabular-nums`}
                    />
                    <span className="flex h-10 items-center justify-end text-sm tabular-nums text-ink-2 @max-3xl:justify-start @max-3xl:px-1">
                      <span className="mr-1 text-ink-3 @3xl:hidden">{t("Сумма")}:</span>
                      {sum !== null ? money(sum) : "—"}
                    </span>
                    <button type="button" onClick={() => removeRow(item.key)} aria-label={t("Убрать позицию {n}", { n: index + 1 })} className="hidden size-8 place-items-center rounded-md text-ink-3 hover:bg-muted hover:text-bad @3xl:grid">
                      <X className="size-4" aria-hidden />
                    </button>
                  </div>
                  {item.itemId && <StockLine item={item} locked={stockLocked(item) || approvedEdit} onChange={(change) => patch(item.key, change)} />}
                </li>
              );
            })}
          </ul>

          <button type="button" onClick={addRow} disabled={items.length >= 100} className="mt-2 inline-flex h-10 items-center gap-2 rounded-lg px-2 text-sm font-semibold text-accent-strong hover:bg-accent-soft disabled:opacity-50">
            <Plus className="size-4" aria-hidden /> {t("Добавить позицию")}
          </button>
        </section>

        <section className={card}>
          <div className="grid gap-4">
            <div>
              <label htmlFor="rq-reason" className={label}>
                {t("Причина закупки")}
              </label>
              <textarea
                id="rq-reason"
                disabled={approvedEdit}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                maxLength={2000}
                rows={3}
                placeholder={t("Зачем это нужно отделу — без причины заявку не отправить")}
                className={`${field} h-auto py-2.5`}
              />
            </div>
            <div>
              <label htmlFor="rq-comment" className={label}>
                {t("Комментарий")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
              </label>
              <textarea id="rq-comment" value={comment} onChange={(e) => setComment(e.target.value)} maxLength={2000} rows={2} className={`${field} h-auto py-2.5`} />
            </div>
          </div>
        </section>

        <section className={card} aria-labelledby="rq-files">
          <div className="flex items-center justify-between gap-3">
            <h2 id="rq-files" className="text-base font-semibold text-ink">
              {t("Вложения")}
            </h2>
            <span className="text-xs text-ink-3">{t("счёт, КП, фото, спецификация · до 20 МБ")}</span>
          </div>
          {initial && initial.documents.length > 0 && (
            <p className="mt-2 text-xs text-ink-3">{t("Уже прикреплено: {n}. Удалить их можно в карточке заявки.", { n: initial.documents.length })}</p>
          )}
          {files.length > 0 && (
            <ul className="mt-3 space-y-1">
              {files.map((file, i) => (
                <li key={`${file.name}-${i}`} className="flex items-center gap-3 rounded-xl px-1 py-1.5">
                  <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-muted text-ink-3">
                    <FileText className="size-4" aria-hidden />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-sm text-ink">{file.name}</span>
                    <span className="text-[11px] text-ink-3">{fileSize(file.size)}</span>
                  </span>
                  <button type="button" onClick={() => setFiles((list) => list.filter((_, j) => j !== i))} aria-label={t("Убрать {name}", { name: file.name })} className="grid size-8 place-items-center rounded-md text-ink-3 hover:text-bad">
                    <X className="size-4" aria-hidden />
                  </button>
                </li>
              ))}
            </ul>
          )}
          <input
            ref={fileInput}
            type="file"
            multiple
            accept={allowedFiles.join(",")}
            className="sr-only"
            tabIndex={-1}
            onChange={(e) => {
              if (e.target.files?.length) addFiles(e.target.files);
              e.target.value = "";
            }}
          />
          <button type="button" onClick={() => fileInput.current?.click()} className="mt-3 inline-flex h-10 items-center gap-2 rounded-lg border border-dashed border-line-strong px-3.5 text-sm font-medium text-ink-2 hover:border-accent hover:text-accent-strong">
            <Paperclip className="size-4" aria-hidden /> {t("Прикрепить файл")}
          </button>
        </section>
      </div>

      <aside>
        <div className={`${card} space-y-4 lg:sticky lg:top-4`}>
          <div>
            <div className="text-xs text-ink-3">{t("Предполагаемая сумма")}</div>
            <div className="mt-1 text-2xl font-semibold tabular-nums text-ink">
              {total > 0 ? money(total) : "—"} {total > 0 && <span className="text-base font-medium text-ink-3">{t("сум")}</span>}
            </div>
            <div className="mt-1 text-xs text-ink-3">{t("по позициям с ценой")}</div>
            {toBuy !== total && (
              <div className="mt-3 flex items-baseline justify-between gap-3 border-t border-line pt-3 text-sm">
                <span className="text-ink-3">{t("к закупке")}</span>
                <span className="font-semibold tabular-nums text-ink">{toBuy > 0 ? `${money(toBuy)} ${t("сум")}` : t("всё со склада")}</span>
              </div>
            )}
          </div>
          <ErrorNote text={error} />
          <div className="flex gap-2 lg:flex-col">{actions}</div>
          {!approvedEdit && (
            <p className="text-xs leading-relaxed text-ink-3">
              {t("После отправки часть «со склада» резервируется, остальное уйдёт на согласование по маршруту. Изменить заявку можно будет, только если её вернут на доработку.")}
            </p>
          )}
        </div>
      </aside>

    </div>
  );
}

/**
 * Склад под позицией из каталога: сколько доступно, сколько взять со склада (подставляется само — сколько есть, можно поменять) и сколько
 * к закупке; взяли меньше, чем есть, — поле «почему купить».
 */
function StockLine({ item, locked, onChange }: { item: ItemDraft; locked: boolean; onChange: (change: Partial<ItemDraft>) => void }) {
  const t = useT();
  const { fromStock, toPurchase } = split(item);
  const cover = stockCover(item);
  const available = item.available ?? 0;
  const needReason = !locked && fromStock < cover;
  return (
    <div className="mt-2 rounded-xl bg-muted/70 px-3 py-2.5 text-sm @3xl:ml-8">
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
        <span className="inline-flex items-center gap-1.5 text-ink-2">
          <Warehouse className="size-4 text-ink-3" aria-hidden />
          {item.sku && <span className="rounded bg-surface px-1.5 py-0.5 text-[11px] font-medium tabular-nums text-ink-3">{item.sku}</span>}
          {available > 0 ? t("На складе доступно {n}", { n: `${qtyText(available)} ${item.unitCode}` }) : t("На складе нет")}
        </span>
        <label className="inline-flex items-center gap-2 text-ink-2">
          {t("Со склада")}
          <input
            value={item.fromStock ?? ""}
            onChange={(e) => onChange({ fromStock: e.target.value, fromStockTouched: true })}
            disabled={locked || available <= 0}
            inputMode="decimal"
            aria-label={t("Со склада, {name}", { name: item.name })}
            className="h-8 w-20 rounded-md border border-line bg-surface px-2 text-sm tabular-nums text-ink focus:border-accent focus:outline-none disabled:opacity-60"
          />
        </label>
        <span className="text-ink-2">
          {t("К закупке")}: <span className="font-semibold tabular-nums text-ink">{qtyText(toPurchase)}</span> {item.unitCode}
        </span>
        {!locked && cover > 0 && fromStock !== cover && (
          <button type="button" onClick={() => onChange({ fromStock: String(cover).replace(".", ","), fromStockTouched: false })} className="text-xs font-semibold text-accent-strong hover:underline">
            {t("Взять всё доступное")}
          </button>
        )}
      </div>
      {needReason && (
        <input
          value={item.purchaseReason ?? ""}
          onChange={(e) => onChange({ purchaseReason: e.target.value })}
          maxLength={500}
          placeholder={t("На складе есть {n} — почему купить, а не взять со склада?", { n: `${qtyText(cover - fromStock)} ${item.unitCode}` })}
          aria-label={t("Обоснование закупки, {name}", { name: item.name })}
          className="mt-2 h-9 w-full rounded-md border border-warn/40 bg-surface px-2.5 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none"
        />
      )}
    </div>
  );
}
