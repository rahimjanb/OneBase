"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useRef, useState } from "react";
import { Loader2, Plus, Save, Trash2 } from "lucide-react";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { money, parseNumber, type RequestCategory, type RequestUnit, type TenderDetail } from "@/lib/procurement";
import { todayInTashkent } from "@/lib/tasks";

type Line = { key: string; name: string; quantity: string; unitCode: string; price: string };

const field = "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
const card = "rounded-2xl border border-line bg-surface p-4 sm:p-5";
const text = (v: number | null | undefined) => (v === null || v === undefined ? "" : String(v).replace(".", ","));
const newKey = () => (typeof crypto !== "undefined" && "randomUUID" in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`);

function Label({ children, optional }: { children: React.ReactNode; optional?: boolean }) {
  const t = useT();
  return (
    <span className="mb-1.5 block text-sm font-medium text-ink">
      {children}
      {optional && <span className="font-normal text-ink-3"> {t("необязательно")}</span>}
    </span>
  );
}

/**
 * Запись журнала тендерных закупок: предмет, дата и номер тендера, победитель (из справочника — подсказкой; нового поставщика добавит
 * сервер), категория (по умолчанию «Сырьё»), договор, участники, срок поставки, позиции (наименование, количество, единица, цена) и
 * комментарий. Новая запись — с ключом повтора формы; правка — с версией (изменили в другом окне — сервер скажет).
 */
export function TenderForm({
  initial,
  categories,
  units,
  suppliers,
}: {
  initial?: TenderDetail;
  categories: RequestCategory[];
  units: RequestUnit[];
  suppliers: { id: string; name: string }[];
}) {
  const t = useT();
  const router = useRouter();
  const today = todayInTashkent();
  const raw = categories.find((c) => c.code === "raw_materials" && c.isActive)?.id ?? categories.find((c) => c.isActive)?.id ?? "";
  const [title, setTitle] = useState(initial?.title ?? "");
  const [tenderNo, setTenderNo] = useState(initial?.tenderNo ?? "");
  const [tenderDate, setTenderDate] = useState(initial?.tenderDate ?? today);
  const [supplier, setSupplier] = useState(initial?.supplierName ?? "");
  const [categoryId, setCategoryId] = useState(initial?.categoryId ?? raw);
  const [contractNo, setContractNo] = useState(initial?.contractNo ?? "");
  const [participants, setParticipants] = useState(initial?.participants ? String(initial.participants) : "");
  const [deliveryDate, setDeliveryDate] = useState(initial?.deliveryDate ?? "");
  const [comment, setComment] = useState(initial?.comment ?? "");
  const [lines, setLines] = useState<Line[]>(
    initial?.items.map((i) => ({ key: i.id, name: i.name, quantity: text(i.quantity), unitCode: i.unitCode, price: text(i.price) })) ?? [
      { key: newKey(), name: "", quantity: "", unitCode: units.some((u) => u.code === "кг") ? "кг" : (units[0]?.code ?? ""), price: "" },
    ],
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const clientKey = useRef(newKey());

  const sumOf = (l: Line) => {
    const q = parseNumber(l.quantity);
    const p = parseNumber(l.price);
    return q !== null && p !== null ? Math.round(q * p * 100) / 100 : null;
  };
  const total = lines.reduce((s, l) => s + (sumOf(l) ?? 0), 0);
  const setLine = (key: string, patch: Partial<Line>) => setLines((ls) => ls.map((l) => (l.key === key ? { ...l, ...patch } : l)));

  const save = async () => {
    if (!title.trim()) return setError(t("Укажите предмет тендера: что закупали."));
    if (!supplier.trim()) return setError(t("Укажите победителя тендера — поставщика."));
    const items = lines
      .filter((l) => l.name.trim() || l.quantity.trim() || l.price.trim())
      .map((l) => ({ name: l.name.trim(), unitCode: l.unitCode, quantity: parseNumber(l.quantity) ?? 0, price: parseNumber(l.price) ?? 0 }));
    if (items.length === 0) return setError(t("Добавьте хотя бы одну позицию: что закупили, сколько и почём."));
    const known = suppliers.find((s) => s.name.trim().toLowerCase() === supplier.trim().toLowerCase());
    const body = {
      title: title.trim(),
      tenderNo: tenderNo.trim() || null,
      tenderDate: tenderDate || null,
      supplierId: known?.id ?? null,
      supplierName: known ? null : supplier.trim(),
      categoryId: categoryId || null,
      contractNo: contractNo.trim() || null,
      participants: participants.trim() ? Number(participants) : null,
      deliveryDate: deliveryDate || null,
      comment: comment.trim() || null,
      items,
      clientKey: initial ? null : clientKey.current,
      version: initial?.version ?? null,
    };
    setBusy(true);
    setError(null);
    try {
      if (initial) {
        await bff(`procurement/tenders/${initial.id}`, { method: "PUT", body: JSON.stringify(body) });
        router.push(`/procurement/tenders/${initial.id}`);
      } else {
        const { id } = await bff<{ id: string }>("procurement/tenders", { method: "POST", body: JSON.stringify(body) });
        router.push(`/procurement/tenders/${id}`);
      }
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setBusy(false);
    }
  };

  return (
    <div className="mx-auto max-w-4xl space-y-4">
      <section className={card}>
        <h2 className="mb-3 text-base font-semibold text-ink">{t("Тендер")}</h2>
        <div className="grid gap-3 sm:grid-cols-2">
          <label className="block sm:col-span-2">
            <Label>{t("Предмет тендера")}</Label>
            <input value={title} onChange={(e) => setTitle(e.target.value)} maxLength={300} placeholder={t("Например: сахар-песок на I квартал")} className={field} />
          </label>
          <label className="block">
            <Label>{t("Дата тендера")}</Label>
            <input type="date" value={tenderDate} max={today} onChange={(e) => setTenderDate(e.target.value)} className={field} />
          </label>
          <label className="block">
            <Label optional>{t("Номер тендера")}</Label>
            <input value={tenderNo} onChange={(e) => setTenderNo(e.target.value)} maxLength={100} placeholder={t("Номер лота или внутренний")} className={field} />
          </label>
          <label className="block">
            <Label>{t("Победитель")}</Label>
            <input value={supplier} onChange={(e) => setSupplier(e.target.value)} list="tender-suppliers" maxLength={200} placeholder={t("Поставщик")} className={field} />
            <datalist id="tender-suppliers">
              {suppliers.map((s) => (
                <option key={s.id} value={s.name} />
              ))}
            </datalist>
            <span className="mt-1 block text-xs text-ink-3">{t("Нового поставщика нет в справочнике — он добавится в «Поставщики».")}</span>
          </label>
          <label className="block">
            <Label>{t("Категория")}</Label>
            <select value={categoryId} onChange={(e) => setCategoryId(e.target.value)} className={field}>
              {categories
                .filter((c) => c.isActive || c.id === categoryId)
                .map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
            </select>
          </label>
        </div>
        <div className="mt-3 grid gap-3 sm:grid-cols-3">
          <label className="block">
            <Label optional>{t("Номер договора")}</Label>
            <input value={contractNo} onChange={(e) => setContractNo(e.target.value)} maxLength={100} className={field} />
          </label>
          <label className="block">
            <Label optional>{t("Участников")}</Label>
            <input value={participants} onChange={(e) => setParticipants(e.target.value.replace(/\D/g, ""))} inputMode="numeric" maxLength={4} className={field} />
          </label>
          <label className="block">
            <Label optional>{t("Срок поставки")}</Label>
            <input type="date" value={deliveryDate} min={tenderDate || undefined} onChange={(e) => setDeliveryDate(e.target.value)} className={field} />
          </label>
        </div>
      </section>

      <section className={card}>
        <h2 className="mb-3 text-base font-semibold text-ink">
          {t("Позиции")} <span className="font-normal text-ink-3">· {t("что закупили, сколько и почём")}</span>
        </h2>
        <ul className="space-y-2">
          {lines.map((l, i) => {
            const sum = sumOf(l);
            return (
              <li key={l.key} className="grid grid-cols-[minmax(0,1fr)_auto] gap-2 rounded-xl border border-line p-2.5 sm:grid-cols-[minmax(0,1fr)_88px_88px_128px_148px_auto] sm:items-center sm:border-0 sm:p-0">
                <input
                  value={l.name}
                  onChange={(e) => setLine(l.key, { name: e.target.value })}
                  maxLength={300}
                  placeholder={t("Наименование")}
                  aria-label={t("Позиция {n}: наименование", { n: i + 1 })}
                  className={`${field} max-sm:col-span-1`}
                />
                <button
                  type="button"
                  onClick={() => setLines((ls) => (ls.length > 1 ? ls.filter((x) => x.key !== l.key) : ls))}
                  disabled={lines.length === 1}
                  aria-label={t("Удалить позицию {n}", { n: i + 1 })}
                  className="grid size-10 place-items-center rounded-lg text-ink-3 hover:text-bad disabled:opacity-30 sm:order-last"
                >
                  <Trash2 className="size-4" aria-hidden />
                </button>
                <div className="col-span-2 grid grid-cols-3 gap-2 sm:contents">
                  <input
                    value={l.quantity}
                    onChange={(e) => setLine(l.key, { quantity: e.target.value })}
                    inputMode="decimal"
                    placeholder={t("Кол-во")}
                    aria-label={t("Позиция {n}: количество", { n: i + 1 })}
                    className={`${field} text-right tabular-nums`}
                  />
                  <select value={l.unitCode} onChange={(e) => setLine(l.key, { unitCode: e.target.value })} aria-label={t("Позиция {n}: единица", { n: i + 1 })} className={field}>
                    {units.map((u) => (
                      <option key={u.code} value={u.code}>
                        {u.code}
                      </option>
                    ))}
                  </select>
                  <input
                    value={l.price}
                    onChange={(e) => setLine(l.key, { price: e.target.value })}
                    inputMode="decimal"
                    placeholder={t("Цена за ед.")}
                    aria-label={t("Позиция {n}: цена за единицу", { n: i + 1 })}
                    className={`${field} text-right tabular-nums`}
                  />
                </div>
                <span className="col-span-2 whitespace-nowrap text-right text-sm tabular-nums text-ink-2 sm:col-span-1">{sum !== null ? `${money(sum)} ${t("сум")}` : "—"}</span>
              </li>
            );
          })}
        </ul>
        <div className="mt-3 flex flex-wrap items-center justify-between gap-3 border-t border-line pt-3">
          <button
            type="button"
            onClick={() => setLines((ls) => [...ls, { key: newKey(), name: "", quantity: "", unitCode: ls[ls.length - 1]?.unitCode ?? "кг", price: "" }])}
            disabled={lines.length >= 50}
            className="inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3.5 text-sm font-medium text-ink hover:bg-muted disabled:opacity-50"
          >
            <Plus className="size-4" aria-hidden /> {t("Добавить позицию")}
          </button>
          <p className="text-sm text-ink-2">
            {t("Итого")} <span className="font-semibold tabular-nums text-ink">{money(total)}</span> {t("сум")}
          </p>
        </div>
      </section>

      <section className={card}>
        <label className="block">
          <Label optional>{t("Комментарий")}</Label>
          <textarea
            value={comment}
            onChange={(e) => setComment(e.target.value)}
            maxLength={2000}
            rows={3}
            placeholder={t("Условия, почему выбран этот поставщик, особенности поставки")}
            className="w-full rounded-lg border border-line bg-surface px-3 py-2.5 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20"
          />
        </label>
      </section>

      <ErrorNote text={error} />
      <div className="flex justify-end gap-2">
        <Link href={initial ? `/procurement/tenders/${initial.id}` : "/procurement/tenders"} className="inline-flex h-10 items-center rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted">
          {t("Отмена")}
        </Link>
        <button type="button" onClick={() => void save()} disabled={busy} className="inline-flex h-10 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50">
          {busy ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Save className="size-4" aria-hidden />} {t("Сохранить")}
        </button>
      </div>
    </div>
  );
}
