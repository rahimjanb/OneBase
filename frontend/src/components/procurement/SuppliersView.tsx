"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { FileSpreadsheet, Loader2, Plus, Search } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { money, type Page, type RequestCategory, type SupplierCard, type SupplierRow } from "@/lib/procurement";
import { RatingBadge } from "./ProcKit";

const field =
  "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
const secondary = "inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3 text-sm font-medium text-ink hover:bg-muted disabled:opacity-50";

/** «Новый поставщик» и «Импорт из Excel» — кнопки шапки (снабжение). */
export function SupplierActions({ categories }: { categories: RequestCategory[] }) {
  const t = useT();
  const router = useRouter();
  const [creating, setCreating] = useState(false);
  const input = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<{ created: number; updated: number; skipped: number; errors: string[] } | null>(null);
  const [error, setError] = useState<string | null>(null);

  const importFile = async (file: File) => {
    setBusy(true);
    setError(null);
    try {
      const form = new FormData();
      form.append("file", file);
      const response = await fetch("/bff/api/procurement/suppliers/import", { method: "POST", body: form });
      const json = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(json.error ? t(json.error) : t("Ошибка {status}", { status: response.status }));
      setResult(json);
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setResult({ created: 0, updated: 0, skipped: 0, errors: [] });
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="flex items-center gap-2">
      <input
        ref={input}
        type="file"
        accept=".xlsx,.xlsm,.csv"
        className="sr-only"
        tabIndex={-1}
        onChange={(e) => {
          const file = e.target.files?.[0];
          if (file) void importFile(file);
          e.target.value = "";
        }}
      />
      <button type="button" onClick={() => input.current?.click()} disabled={busy} className={secondary}>
        {busy ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <FileSpreadsheet className="size-4" aria-hidden />}
        <span className="max-sm:sr-only">{t("Импорт из Excel")}</span>
      </button>
      <button type="button" onClick={() => setCreating(true)} className="inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-accent-strong">
        <Plus className="size-4" aria-hidden /> <span className="max-sm:sr-only">{t("Новый поставщик")}</span>
      </button>
      <SupplierFormSheet open={creating} onClose={() => setCreating(false)} categories={categories} />
      <Sheet open={result !== null} onClose={() => setResult(null)} title={t("Импорт поставщиков")}>
        {error ? (
          <ErrorNote text={error} />
        ) : (
          result && (
            <div className="space-y-3 text-sm">
              <p className="text-ink">{t("Добавлено: {a}, обновлено: {b}, пропущено: {c}.", { a: result.created, b: result.updated, c: result.skipped })}</p>
              {result.errors.length > 0 && (
                <ul className="max-h-64 space-y-1 overflow-y-auto rounded-xl bg-muted px-3 py-2 text-xs text-ink-2">
                  {result.errors.map((e) => (
                    <li key={e}>{e}</li>
                  ))}
                </ul>
              )}
            </div>
          )
        )}
        <p className="mt-3 text-xs text-ink-3">
          {t("Первая строка — заголовки: Название, ИНН, Контактное лицо, Телефон, Email, Адрес, Банк, МФО, Расчётный счёт, Условия оплаты, Срок поставки, Категории (через запятую), Примечание. Совпадение — по ИНН, без ИНН — по названию.")}
        </p>
      </Sheet>
    </div>
  );
}

/** Реестр поставщиков: поиск, категория, активные/отключённые, рейтинг, заказы, сумма, просрочки. */
export function SuppliersView({ data, categories }: { data: Page<SupplierRow>; categories: RequestCategory[] }) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [search, setSearch] = useState(params.get("q") ?? "");
  const apply = (patch: Record<string, string | null>) => {
    const next = new URLSearchParams(params.toString());
    for (const [k, v] of Object.entries(patch)) {
      if (v) next.set(k, v);
      else next.delete(k);
    }
    router.push(`${pathname}${next.size ? `?${next}` : ""}`, { scroll: false });
  };
  useEffect(() => {
    const timer = window.setTimeout(() => {
      if ((params.get("q") ?? "") !== search.trim()) apply({ q: search.trim() || null });
    }, 400);
    return () => window.clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search]);

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center gap-2">
        <label className="relative block min-w-0 basis-full sm:basis-auto sm:flex-1 sm:max-w-[300px]">
          <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" aria-hidden />
          <span className="sr-only">{t("Поиск")}</span>
          <input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("Название, ИНН, контакт, телефон")} className={`${field} w-full pl-9`} />
        </label>
        <select value={params.get("category") ?? ""} onChange={(e) => apply({ category: e.target.value || null })} aria-label={t("Категория")} className={`${field} w-auto max-w-[220px]`}>
          <option value="">{t("Все категории")}</option>
          {categories.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
        <select value={params.get("sort") ?? ""} onChange={(e) => apply({ sort: e.target.value || null, desc: e.target.value ? "true" : null })} aria-label={t("Сортировка")} className={`${field} w-auto max-w-[220px]`}>
          <option value="">{t("По названию")}</option>
          <option value="rating">{t("По рейтингу")}</option>
          <option value="total">{t("По сумме заказов")}</option>
          <option value="overdue">{t("По просрочкам")}</option>
        </select>
        <label className="inline-flex items-center gap-2 text-sm text-ink-2">
          <input type="checkbox" checked={params.get("active") === "all"} onChange={(e) => apply({ active: e.target.checked ? "all" : null })} className="size-4 accent-[var(--color-accent)]" />
          {t("С отключёнными")}
        </label>
      </div>
      {data.items.length === 0 ? (
        <p className="rounded-xl border border-dashed border-line bg-surface px-4 py-10 text-center text-sm text-ink-3">
          {params.size === 0 ? t("Поставщиков пока нет — добавьте вручную или загрузите из Excel.") : t("По этому фильтру поставщиков нет.")}
        </p>
      ) : (
        <>
          <div className="hidden overflow-x-auto rounded-xl border border-line bg-surface lg:block">
            <table className="w-full min-w-[960px] text-sm">
              <thead>
                <tr className="whitespace-nowrap border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                  <th className="px-3 py-2.5 font-semibold">{t("Поставщик")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Категории")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Контакт")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Рейтинг")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t("Заказов")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t("Сумма, сум")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t("Просрочено")}</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((s) => (
                  <tr key={s.id} className={`border-b border-line align-top last:border-0 hover:bg-muted/50 ${s.isActive ? "" : "opacity-60"}`}>
                    <td className="px-3 py-2.5">
                      <Link href={`/procurement/suppliers/${s.id}`} className="font-medium text-ink hover:text-accent-strong">
                        {s.name}
                      </Link>
                      <span className="block text-xs tabular-nums text-ink-3">
                        {s.inn ? t("ИНН {n}", { n: s.inn }) : t("ИНН не указан")}
                        {!s.isActive && ` · ${t("отключён")}`}
                      </span>
                    </td>
                    <td className="max-w-56 px-3 py-2.5 text-xs text-ink-2">{s.categories.join(", ") || "—"}</td>
                    <td className="px-3 py-2.5 text-ink-2">
                      {s.contactPerson ?? "—"}
                      {s.phone && <span className="block text-xs text-ink-3">{s.phone}</span>}
                    </td>
                    <td className="px-3 py-2.5">
                      <RatingBadge rating={s.rating} />
                    </td>
                    <td className="px-3 py-2.5 text-right tabular-nums text-ink">{s.orders}</td>
                    <td className="whitespace-nowrap px-3 py-2.5 text-right tabular-nums text-ink">{s.ordersTotal > 0 ? money(s.ordersTotal) : "—"}</td>
                    <td className={`px-3 py-2.5 text-right tabular-nums ${s.overdue > 0 ? "font-semibold text-bad" : "text-ink-3"}`}>{s.overdue}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <ul className="space-y-2 lg:hidden">
            {data.items.map((s) => (
              <li key={s.id}>
                <Link href={`/procurement/suppliers/${s.id}`} className="block rounded-xl border border-line bg-surface p-3.5">
                  <span className="flex items-start justify-between gap-2">
                    <span className="min-w-0">
                      <span className="block font-medium text-ink">{s.name}</span>
                      <span className="block text-xs text-ink-3">{s.categories.join(", ") || (s.inn ? t("ИНН {n}", { n: s.inn }) : "")}</span>
                    </span>
                    <RatingBadge rating={s.rating} />
                  </span>
                  <span className="mt-1.5 flex flex-wrap gap-x-3 text-xs text-ink-3">
                    <span>{t("заказов {n}", { n: s.orders })}</span>
                    {s.ordersTotal > 0 && <span className="tabular-nums">{money(s.ordersTotal)} {t("сум")}</span>}
                    {s.overdue > 0 && <span className="font-semibold text-bad">{t("просрочено {n}", { n: s.overdue })}</span>}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
          <p className="mt-3 text-sm text-ink-2">{t("Поставщиков: {n}", { n: data.total })}</p>
        </>
      )}
    </>
  );
}

/** Новый поставщик или правка: реквизиты, контакты, банк, условия, срок, категории, активен. */
export function SupplierFormSheet({ open, onClose, categories, supplier }: { open: boolean; onClose: () => void; categories: RequestCategory[]; supplier?: SupplierCard }) {
  const t = useT();
  const router = useRouter();
  const blank = { name: "", inn: "", contactPerson: "", phone: "", email: "", address: "", bankName: "", bankMfo: "", bankAccount: "", paymentTerms: "", leadTimeDays: "", notes: "" };
  const [form, setForm] = useState(blank);
  const [cats, setCats] = useState<string[]>([]);
  const [active, setActive] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setError(null);
    setForm(
      supplier
        ? {
            name: supplier.name,
            inn: supplier.inn ?? "",
            contactPerson: supplier.contactPerson ?? "",
            phone: supplier.phone ?? "",
            email: supplier.email ?? "",
            address: supplier.address ?? "",
            bankName: supplier.bankName ?? "",
            bankMfo: supplier.bankMfo ?? "",
            bankAccount: supplier.bankAccount ?? "",
            paymentTerms: supplier.paymentTerms ?? "",
            leadTimeDays: supplier.leadTimeDays === null ? "" : String(supplier.leadTimeDays),
            notes: supplier.notes ?? "",
          }
        : blank,
    );
    setCats(supplier?.categories.map((c) => c.id) ?? []);
    setActive(supplier?.isActive ?? true);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, supplier]);

  const save = async () => {
    if (!form.name.trim()) return setError(t("Укажите название поставщика."));
    setBusy(true);
    setError(null);
    try {
      const body = JSON.stringify({ ...Object.fromEntries(Object.entries(form).map(([k, v]) => [k, v.trim() || null])), leadTimeDays: form.leadTimeDays ? Number(form.leadTimeDays) : null, categoryIds: cats, isActive: active });
      if (supplier) await bff(`procurement/suppliers/${supplier.id}`, { method: "PUT", body });
      else {
        const { id } = await bff<{ id: string }>("procurement/suppliers", { method: "POST", body });
        router.push(`/procurement/suppliers/${id}`);
      }
      onClose();
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  const input = (key: keyof typeof blank, labelText: string, props: React.InputHTMLAttributes<HTMLInputElement> = {}) => (
    <label className="block">
      <span className="mb-1.5 block text-sm font-medium text-ink">{t(labelText)}</span>
      <input value={form[key]} onChange={(e) => setForm((f) => ({ ...f, [key]: e.target.value }))} className={field} {...props} />
    </label>
  );

  return (
    <Sheet
      open={open}
      onClose={onClose}
      title={supplier ? t("Изменить поставщика") : t("Новый поставщик")}
      size="xl"
      footer={
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-10 items-center rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted">
            {t("Закрыть")}
          </button>
          <button type="button" onClick={() => void save()} disabled={busy} className="inline-flex h-10 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50">
            {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}
            {t("Сохранить")}
          </button>
        </div>
      }
    >
      <div className="grid gap-3 sm:grid-cols-2">
        <div className="sm:col-span-2">{input("name", "Название", { maxLength: 200, autoFocus: !supplier })}</div>
        {input("inn", "ИНН", { inputMode: "numeric", maxLength: 20 })}
        {input("contactPerson", "Контактное лицо", { maxLength: 200 })}
        {input("phone", "Телефон", { maxLength: 50, inputMode: "tel" })}
        {input("email", "Email", { maxLength: 200, type: "email" })}
        <div className="sm:col-span-2">{input("address", "Адрес", { maxLength: 500 })}</div>
        {input("bankName", "Банк", { maxLength: 200 })}
        <div className="grid grid-cols-2 gap-3">
          {input("bankMfo", "МФО", { maxLength: 20, inputMode: "numeric" })}
          {input("leadTimeDays", "Срок поставки, дней", { inputMode: "numeric", maxLength: 3 })}
        </div>
        <div className="sm:col-span-2">{input("bankAccount", "Расчётный счёт", { maxLength: 40, inputMode: "numeric" })}</div>
        <div className="sm:col-span-2">{input("paymentTerms", "Условия оплаты", { maxLength: 500 })}</div>
        <fieldset className="sm:col-span-2">
          <legend className="mb-1.5 text-sm font-medium text-ink">{t("Категории товаров")}</legend>
          <div className="flex flex-wrap gap-1.5">
            {categories.map((c) => {
              const on = cats.includes(c.id);
              return (
                <button
                  key={c.id}
                  type="button"
                  aria-pressed={on}
                  onClick={() => setCats((list) => (on ? list.filter((x) => x !== c.id) : [...list, c.id]))}
                  className={`h-8 rounded-full border px-3 text-sm ${on ? "border-accent bg-accent-soft text-accent-strong" : "border-line bg-surface text-ink-2 hover:bg-muted"}`}
                >
                  {c.name}
                </button>
              );
            })}
          </div>
        </fieldset>
        <label className="block sm:col-span-2">
          <span className="mb-1.5 block text-sm font-medium text-ink">
            {t("Примечание")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
          </span>
          <textarea value={form.notes} onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))} maxLength={2000} rows={2} className={`${field} h-auto py-2.5`} />
        </label>
        {supplier && (
          <label className="flex items-center gap-2 text-sm text-ink sm:col-span-2">
            <input type="checkbox" checked={active} onChange={(e) => setActive(e.target.checked)} className="size-4 accent-[var(--color-accent)]" />
            {t("Поставщик активен — его можно выбрать в предложении и заказе")}
          </label>
        )}
      </div>
      <div className="mt-3">
        <ErrorNote text={error} />
      </div>
    </Sheet>
  );
}
