"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";
import { AlertTriangle, ArrowDownToLine, ArrowUpFromLine, ChevronLeft, ChevronRight, ClipboardList, Loader2, PackageMinus, PackagePlus, Pencil, Search } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import {
  movementKindLabel,
  parseNumber,
  quantity,
  stampRu,
  type ItemCard,
  type Page,
  type RequestCategory,
  type RequestDepartment,
  type RequestUnit,
  type StockRow,
  type StockSummary,
} from "@/lib/procurement";
import { StockOperation, type OperationKind } from "./StockOperation";

const field =
  "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20 disabled:opacity-60";
const secondary = "inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3 text-sm font-medium text-ink hover:bg-muted";

const filters = [
  { value: "", label: "Все" },
  { value: "low", label: "Ниже минимума" },
  { value: "empty", label: "Нет на складе" },
  { value: "reserved", label: "В резерве" },
  { value: "inactive", label: "Отключённые" },
] as const;

/** Кнопки операций склада: кладовщику — все, снабжению — только новая позиция каталога. */
export function StockActions({ warehouse, catalog, departments, categories, units }: { warehouse: boolean; catalog: boolean; departments: RequestDepartment[]; categories: RequestCategory[]; units: RequestUnit[] }) {
  const t = useT();
  const [op, setOp] = useState<OperationKind | null>(null);
  const [creating, setCreating] = useState(false);
  return (
    <div className="flex items-center gap-1.5 sm:flex-wrap sm:gap-2">
      {warehouse && (
        <>
          <button type="button" onClick={() => setOp("receipt")} className={secondary}>
            <ArrowDownToLine className="size-4" aria-hidden /> <span className="max-sm:sr-only">{t("Приход")}</span>
          </button>
          <button type="button" onClick={() => setOp("issue")} className={secondary}>
            <ArrowUpFromLine className="size-4" aria-hidden /> <span className="max-sm:sr-only">{t("Выдача")}</span>
          </button>
          <button type="button" onClick={() => setOp("writeoff")} className={secondary}>
            <PackageMinus className="size-4" aria-hidden /> <span className="max-sm:sr-only">{t("Списание")}</span>
          </button>
          <button type="button" onClick={() => setOp("inventory")} className={secondary}>
            <ClipboardList className="size-4" aria-hidden /> <span className="max-sm:sr-only">{t("Инвентаризация")}</span>
          </button>
        </>
      )}
      {catalog && (
        <button type="button" onClick={() => setCreating(true)} className="inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-accent-strong">
          <PackagePlus className="size-4" aria-hidden /> <span className="max-sm:sr-only">{t("Новая позиция")}</span>
        </button>
      )}
      {op && <StockOperation kind={op} open onClose={() => setOp(null)} departments={departments} />}
      <ItemFormSheet open={creating} onClose={() => setCreating(false)} categories={categories} units={units} />
    </div>
  );
}

/**
 * Склад: плитки (позиций, ниже минимума, нет на складе, в резерве, заявок к выдаче), поиск по SKU, названию, штрихкоду, категория и
 * фильтры, таблица остатков (на складе, в резерве, доступно, минимум); строка открывает карточку товара — движения, резервы, правка.
 */
export function StockView({
  data,
  summary,
  categories,
  units,
  departments,
  warehouse,
  catalog,
}: {
  data: Page<StockRow>;
  summary: StockSummary;
  categories: RequestCategory[];
  units: RequestUnit[];
  departments: RequestDepartment[];
  warehouse: boolean;
  catalog: boolean;
}) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [search, setSearch] = useState(params.get("q") ?? "");
  const [open, setOpen] = useState<string | null>(null);
  const filter = params.get("filter") ?? "";
  const category = params.get("category") ?? "";

  const apply = (patch: Record<string, string | null>) => {
    const next = new URLSearchParams(params.toString());
    for (const [k, v] of Object.entries(patch)) {
      if (v) next.set(k, v);
      else next.delete(k);
    }
    if (!("page" in patch)) next.delete("page");
    router.push(`${pathname}${next.size ? `?${next}` : ""}`, { scroll: false });
  };

  useEffect(() => {
    const timer = window.setTimeout(() => {
      if ((params.get("q") ?? "") !== search.trim()) apply({ q: search.trim() || null });
    }, 400);
    return () => window.clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search]);

  const tiles = [
    { label: "Позиций в каталоге", value: summary.items, filter: "" },
    { label: "Ниже минимума", value: summary.low, filter: "low", tone: summary.low > 0 ? "text-warn" : "" },
    { label: "Нет на складе", value: summary.empty, filter: "empty", tone: summary.empty > 0 ? "text-bad" : "" },
    { label: "С резервом", value: summary.withReserve, filter: "reserved" },
  ];
  const pages = Math.max(1, Math.ceil(data.total / data.pageSize));

  return (
    <>
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-5">
        {tiles.map((tile) => (
          <button
            key={tile.label}
            type="button"
            onClick={() => apply({ filter: tile.filter || null })}
            className={`rounded-xl border bg-surface p-4 text-left transition-colors hover:border-accent/40 ${filter === tile.filter ? "border-accent/50 ring-1 ring-accent/20" : "border-line"}`}
          >
            <div className={`text-2xl font-semibold tabular-nums text-ink ${tile.tone ?? ""}`}>{tile.value}</div>
            <div className="mt-0.5 text-xs text-ink-3">{t(tile.label)}</div>
          </button>
        ))}
        <Link href="/procurement/issue" className="col-span-2 rounded-xl border border-line bg-surface p-4 transition-colors hover:border-accent/40 sm:col-span-1">
          <div className="text-2xl font-semibold tabular-nums text-accent-strong">{summary.toIssueRequests}</div>
          <div className="mt-0.5 text-xs text-ink-3">{t("Заявок к выдаче")}</div>
        </Link>
      </div>

      <div className="my-4 flex flex-wrap items-center gap-2">
        <label className="relative block min-w-0 flex-1 sm:max-w-[320px]">
          <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" aria-hidden />
          <span className="sr-only">{t("Поиск")}</span>
          <input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("Артикул, название, штрихкод")} className={`${field} pl-9`} />
        </label>
        <select value={category} onChange={(e) => apply({ category: e.target.value || null })} aria-label={t("Категория")} className={`${field} w-auto max-w-[240px]`}>
          <option value="">{t("Все категории")}</option>
          {categories.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
        <div className="flex flex-wrap gap-1 rounded-xl bg-muted p-1">
          {filters.map((f) => (
            <button
              key={f.value}
              type="button"
              onClick={() => apply({ filter: f.value || null })}
              className={`h-8 rounded-lg px-3 text-sm ${filter === f.value ? "bg-surface font-medium text-ink shadow-sm" : "text-ink-2 hover:text-ink"}`}
            >
              {t(f.label)}
            </button>
          ))}
        </div>
      </div>

      {data.items.length === 0 ? (
        <p className="rounded-xl border border-dashed border-line bg-surface px-4 py-10 text-center text-sm text-ink-3">
          {summary.items === 0 ? t("Каталог склада пуст — добавьте позиции кнопкой «Новая позиция», затем проведите приход.") : t("По этому фильтру позиций нет.")}
        </p>
      ) : (
        <>
          <div className="hidden overflow-x-auto rounded-xl border border-line bg-surface lg:block">
            <table className="w-full min-w-[900px] text-sm">
              <thead>
                <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                  <th className="px-3 py-2.5 font-semibold">{t("Артикул")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Наименование")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t("На складе")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t("В резерве")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t("Доступно")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t("Минимум")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Последнее движение")}</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((r) => (
                  <tr key={r.itemId} onClick={() => setOpen(r.itemId)} className={`cursor-pointer border-b border-line last:border-0 hover:bg-muted/50 ${r.isActive ? "" : "opacity-60"}`}>
                    <td className="px-3 py-2.5 tabular-nums text-ink-3">{r.sku}</td>
                    <td className="px-3 py-2.5">
                      <button type="button" className="text-left font-medium text-ink hover:text-accent-strong">
                        {r.name}
                      </button>
                      <span className="block text-xs text-ink-3">{r.categoryName}</span>
                    </td>
                    <td className="px-3 py-2.5 text-right tabular-nums text-ink">
                      {quantity(r.onHand)} <span className="text-ink-3">{r.unitCode}</span>
                    </td>
                    <td className="px-3 py-2.5 text-right tabular-nums text-ink-2">{r.reserved > 0 ? quantity(r.reserved) : "—"}</td>
                    <td className={`px-3 py-2.5 text-right font-semibold tabular-nums ${r.low ? "text-warn" : r.available > 0 ? "text-ok" : "text-ink-3"}`}>
                      {r.low && <AlertTriangle className="mr-1 inline size-3.5" aria-label={t("Ниже минимума")} />}
                      {quantity(r.available)}
                    </td>
                    <td className="px-3 py-2.5 text-right tabular-nums text-ink-3">{r.minQty > 0 ? quantity(r.minQty) : "—"}</td>
                    <td className="px-3 py-2.5 tabular-nums text-ink-3">{r.lastMovementAt ? stampRu(r.lastMovementAt) : "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <ul className="space-y-2 lg:hidden">
            {data.items.map((r) => (
              <li key={r.itemId}>
                <button type="button" onClick={() => setOpen(r.itemId)} className="block w-full rounded-xl border border-line bg-surface p-3.5 text-left">
                  <span className="flex items-start justify-between gap-3">
                    <span className="min-w-0">
                      <span className="block text-xs tabular-nums text-ink-3">{r.sku}</span>
                      <span className="block font-medium text-ink">{r.name}</span>
                    </span>
                    <span className={`shrink-0 text-right text-sm font-semibold tabular-nums ${r.low ? "text-warn" : r.available > 0 ? "text-ok" : "text-ink-3"}`}>
                      {quantity(r.available)} {r.unitCode}
                      <span className="block text-[11px] font-normal text-ink-3">{t("доступно")}</span>
                    </span>
                  </span>
                  <span className="mt-1 block text-xs text-ink-3">
                    {t("на складе {a} · в резерве {b}", { a: quantity(r.onHand), b: quantity(r.reserved) })}
                    {r.minQty > 0 && ` · ${t("мин. {n}", { n: quantity(r.minQty) })}`}
                  </span>
                </button>
              </li>
            ))}
          </ul>
          <div className="mt-3 flex flex-wrap items-center justify-between gap-3 text-sm text-ink-2">
            <span>{t("Позиций: {n}", { n: data.total })}</span>
            {pages > 1 && (
              <div className="flex items-center gap-2">
                {data.page > 1 && (
                  <button type="button" onClick={() => apply({ page: String(data.page - 1) })} className={secondary}>
                    <ChevronLeft className="size-4" /> {t("Назад")}
                  </button>
                )}
                <span>{t("{page} из {pages}", { page: data.page, pages })}</span>
                {data.page < pages && (
                  <button type="button" onClick={() => apply({ page: String(data.page + 1) })} className={secondary}>
                    {t("Дальше")} <ChevronRight className="size-4" />
                  </button>
                )}
              </div>
            )}
          </div>
        </>
      )}

      <ItemSheet id={open} onClose={() => setOpen(null)} categories={categories} units={units} departments={departments} warehouse={warehouse} catalog={catalog} />
    </>
  );
}

/** Карточка товара: остатки, резервы по заявкам, последние движения; правка каталога и быстрые операции. */
function ItemSheet({
  id,
  onClose,
  categories,
  units,
  departments,
  warehouse,
  catalog,
}: {
  id: string | null;
  onClose: () => void;
  categories: RequestCategory[];
  units: RequestUnit[];
  departments: RequestDepartment[];
  warehouse: boolean;
  catalog: boolean;
}) {
  const t = useT();
  const [card, setCard] = useState<ItemCard | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState(false);
  const [op, setOp] = useState<OperationKind | null>(null);

  useEffect(() => {
    if (!id) return;
    let alive = true;
    setCard(null);
    setError(null);
    bff<ItemCard>(`procurement/items/${id}`)
      .then((c) => alive && setCard(c))
      .catch((e: Error) => alive && setError(e.message));
    return () => {
      alive = false;
    };
  }, [id, op, editing]);

  const item = card?.item;
  const option = item ? { id: item.itemId, sku: item.sku, name: item.name, categoryId: item.categoryId, categoryName: item.categoryName, unitCode: item.unitCode, onHand: item.onHand, available: item.available } : null;

  return (
    <>
      <Sheet open={!!id && !editing && !op} onClose={onClose} title={item?.name ?? t("Позиция склада")} subtitle={item ? `${item.sku} · ${item.categoryName}` : undefined} size="xl">
        {error && <ErrorNote text={error} />}
        {!card && !error && (
          <p className="flex items-center gap-2 py-8 text-sm text-ink-3">
            <Loader2 className="size-4 animate-spin" aria-hidden /> {t("Загрузка")}
          </p>
        )}
        {item && (
          <div className="space-y-5">
            <div className="grid grid-cols-3 gap-2">
              {[
                { label: "На складе", value: item.onHand },
                { label: "В резерве", value: item.reserved },
                { label: "Доступно", value: item.available, tone: item.low ? "text-warn" : "text-ok" },
              ].map((x) => (
                <div key={x.label} className="rounded-xl bg-muted px-3 py-2.5">
                  <div className={`text-xl font-semibold tabular-nums text-ink ${x.tone ?? ""}`}>
                    {quantity(x.value)} <span className="text-sm font-normal text-ink-3">{item.unitCode}</span>
                  </div>
                  <div className="text-xs text-ink-3">{t(x.label)}</div>
                </div>
              ))}
            </div>
            {item.low && (
              <p className="flex items-center gap-2 rounded-xl bg-warn-soft px-3 py-2 text-sm text-ink">
                <AlertTriangle className="size-4 text-warn" aria-hidden /> {t("Ниже минимального остатка ({n}) — пора пополнить.", { n: `${quantity(item.minQty)} ${item.unitCode}` })}
              </p>
            )}
            <div className="flex flex-wrap gap-2">
              {warehouse && (
                <>
                  <button type="button" onClick={() => setOp("receipt")} className={secondary}>
                    <ArrowDownToLine className="size-4" aria-hidden /> {t("Приход")}
                  </button>
                  <button type="button" onClick={() => setOp("issue")} className={secondary}>
                    <ArrowUpFromLine className="size-4" aria-hidden /> {t("Выдача")}
                  </button>
                  <button type="button" onClick={() => setOp("writeoff")} className={secondary}>
                    <PackageMinus className="size-4" aria-hidden /> {t("Списание")}
                  </button>
                  <button type="button" onClick={() => setOp("inventory")} className={secondary}>
                    <ClipboardList className="size-4" aria-hidden /> {t("Инвентаризация")}
                  </button>
                </>
              )}
              {catalog && (
                <button type="button" onClick={() => setEditing(true)} className={secondary}>
                  <Pencil className="size-4" aria-hidden /> {t("Изменить")}
                </button>
              )}
            </div>
            {item.description && <p className="whitespace-pre-line text-sm text-ink-2">{item.description}</p>}

            {card.reservations.length > 0 && (
              <section>
                <h3 className="mb-2 text-sm font-semibold text-ink">{t("В резерве по заявкам")}</h3>
                <ul className="divide-y divide-line rounded-xl border border-line">
                  {card.reservations.map((r) => (
                    <li key={r.requestId}>
                      <Link href={`/requests/${r.requestId}`} className="flex items-center justify-between gap-3 px-3 py-2 text-sm hover:bg-muted/50">
                        <span className="min-w-0">
                          <span className="block truncate text-ink">
                            {r.requestNumber} · {r.requestTitle}
                          </span>
                          <span className="text-xs text-ink-3">{r.departmentName}</span>
                        </span>
                        <span className="shrink-0 tabular-nums text-ink">
                          {quantity(r.quantity)} {item.unitCode}
                        </span>
                      </Link>
                    </li>
                  ))}
                </ul>
              </section>
            )}

            <section>
              <h3 className="mb-2 text-sm font-semibold text-ink">{t("Движения")}</h3>
              {card.movements.length === 0 ? (
                <p className="text-sm text-ink-3">{t("Движений ещё не было.")}</p>
              ) : (
                <ul className="divide-y divide-line rounded-xl border border-line">
                  {card.movements.map((m) => (
                    <li key={m.id} className="flex items-center gap-3 px-3 py-2 text-sm">
                      <span className="min-w-0 flex-1">
                        <span className="block text-ink">
                          {t(movementKindLabel[m.kind])} · <span className="tabular-nums text-ink-3">{m.documentNumber}</span>
                        </span>
                        <span className="block truncate text-xs text-ink-3">
                          {stampRu(m.createdAt)}
                          {m.actorName && ` · ${m.actorName}`}
                          {m.departmentName && ` · ${m.departmentName}`}
                          {m.requestNumber && ` · ${m.requestNumber}`}
                          {m.comment && ` · ${m.comment}`}
                        </span>
                      </span>
                      <span className={`shrink-0 text-right tabular-nums ${m.quantity > 0 ? "text-ok" : m.quantity < 0 ? "text-bad" : "text-ink-3"}`}>
                        {m.quantity > 0 ? "+" : ""}
                        {quantity(m.quantity)}
                        <span className="block text-[11px] text-ink-3">→ {quantity(m.balanceAfter)}</span>
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </section>
          </div>
        )}
      </Sheet>
      {item && <ItemFormSheet open={editing} onClose={() => setEditing(false)} categories={categories} units={units} item={item} />}
      {op && option && <StockOperation kind={op} open onClose={() => setOp(null)} departments={departments} preset={option} />}
    </>
  );
}

/** Новая позиция каталога или правка: артикул, название, категория, единица, штрихкод, минимальный остаток, описание, активна. */
function ItemFormSheet({ open, onClose, categories, units, item }: { open: boolean; onClose: () => void; categories: RequestCategory[]; units: RequestUnit[]; item?: StockRow }) {
  const t = useT();
  const router = useRouter();
  const [form, setForm] = useState({ sku: "", name: "", categoryId: "", unitCode: "шт", barcode: "", minQty: "", description: "", isActive: true });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setError(null);
    setForm(
      item
        ? {
            sku: item.sku,
            name: item.name,
            categoryId: item.categoryId,
            unitCode: item.unitCode,
            barcode: item.barcode ?? "",
            minQty: item.minQty ? String(item.minQty).replace(".", ",") : "",
            description: item.description ?? "",
            isActive: item.isActive,
          }
        : { sku: "", name: "", categoryId: "", unitCode: "шт", barcode: "", minQty: "", description: "", isActive: true },
    );
  }, [open, item]);

  const save = async () => {
    setError(null);
    if (!form.sku.trim() || !form.name.trim() || !form.categoryId) {
      setError(t("Укажите артикул, название и категорию."));
      return;
    }
    setBusy(true);
    try {
      const body = JSON.stringify({ ...form, minQty: parseNumber(form.minQty) ?? 0 });
      if (item) await bff(`procurement/items/${item.itemId}`, { method: "PUT", body });
      else await bff("procurement/items", { method: "POST", body });
      onClose();
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  const set = (patch: Partial<typeof form>) => setForm((f) => ({ ...f, ...patch }));
  return (
    <Sheet
      open={open}
      onClose={onClose}
      title={item ? t("Изменить позицию") : t("Новая позиция каталога")}
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
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Артикул (SKU)")}</span>
          <input value={form.sku} onChange={(e) => set({ sku: e.target.value })} maxLength={64} className={field} autoFocus={!item} />
        </label>
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">
            {t("Штрихкод")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
          </span>
          <input value={form.barcode} onChange={(e) => set({ barcode: e.target.value })} maxLength={64} className={field} />
        </label>
        <label className="block sm:col-span-2">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Название")}</span>
          <input value={form.name} onChange={(e) => set({ name: e.target.value })} maxLength={300} className={field} />
        </label>
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Категория")}</span>
          <select value={form.categoryId} onChange={(e) => set({ categoryId: e.target.value })} className={field}>
            <option value="">{t("Выберите категорию")}</option>
            {categories.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
        </label>
        <div className="grid grid-cols-2 gap-3">
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Ед.")}</span>
            <select value={form.unitCode} onChange={(e) => set({ unitCode: e.target.value })} className={field}>
              {units.map((u) => (
                <option key={u.code} value={u.code}>
                  {u.code}
                </option>
              ))}
            </select>
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Минимум")}</span>
            <input value={form.minQty} onChange={(e) => set({ minQty: e.target.value })} inputMode="decimal" placeholder="0" className={`${field} tabular-nums`} />
          </label>
        </div>
        <label className="block sm:col-span-2">
          <span className="mb-1.5 block text-sm font-medium text-ink">
            {t("Описание")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
          </span>
          <textarea value={form.description} onChange={(e) => set({ description: e.target.value })} maxLength={1000} rows={2} className={`${field} h-auto py-2.5`} />
        </label>
        {item && (
          <label className="flex items-center gap-2 text-sm text-ink sm:col-span-2">
            <input type="checkbox" checked={form.isActive} onChange={(e) => set({ isActive: e.target.checked })} className="size-4 accent-[var(--color-accent)]" />
            {t("Позиция активна — её можно выбрать в заявке и принять на склад")}
          </label>
        )}
      </div>
      <div className="mt-3">
        <ErrorNote text={error} />
      </div>
    </Sheet>
  );
}
