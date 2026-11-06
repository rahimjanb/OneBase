"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Loader2, Search, Sparkles } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { parseNumber, quantity, type MinimumPlan, type MinimumRow } from "@/lib/procurement";

const input =
  "h-10 w-28 rounded-lg border border-line-strong bg-surface px-3 text-right text-sm tabular-nums text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";

/**
 * «Задать минимумы»: позиции без минимума — сначала с наибольшим расходом, у каждой расход за 30 дней и предложение (расход вверх до целых);
 * «Заполнить предложениями» ставит их в пустые поля, сохраняются только заполненные. item — одна позиция («Мин.» в строке): поле с текущим
 * минимумом, пустое — убрать минимум. Минимум один на позицию — на все склады.
 */
export function MinimumsSheet({ open, onClose, item }: { open: boolean; onClose: () => void; item?: { id: string; name: string } | null }) {
  const t = useT();
  const router = useRouter();
  const [plan, setPlan] = useState<MinimumPlan | null>(null);
  const [values, setValues] = useState<Record<string, string>>({});
  const [search, setSearch] = useState("");
  const [query, setQuery] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const single = Boolean(item);

  useEffect(() => {
    if (!open) return;
    setPlan(null);
    setValues({});
    setSearch("");
    setQuery("");
    setError(null);
  }, [open, item?.id]);

  useEffect(() => {
    const timer = window.setTimeout(() => setQuery(search.trim()), 300);
    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    if (!open) return;
    let alive = true;
    const params = new URLSearchParams();
    if (item) params.set("items", item.id);
    else if (query) params.set("q", query);
    bff<MinimumPlan>(`procurement/stock/minimums?${params}`)
      .then((p) => {
        if (!alive) return;
        setPlan(p);
        // Одна позиция — поле с её минимумом.
        if (item) setValues(Object.fromEntries(p.rows.map((r) => [r.itemId, r.minQty > 0 ? String(r.minQty).replace(".", ",") : ""])));
      })
      .catch((e: Error) => alive && setError(e.message));
    return () => {
      alive = false;
    };
  }, [open, item, query]);

  const rows = plan?.rows ?? [];
  const filled = rows.filter((r) => (values[r.itemId] ?? "").trim() !== "" || (single && r.minQty > 0 && (values[r.itemId] ?? "") === ""));
  const suggestions = rows.filter((r) => r.suggested !== null && (values[r.itemId] ?? "").trim() === "");

  const save = async () => {
    const items: { itemId: string; minQty: number }[] = [];
    for (const r of filled) {
      const text = (values[r.itemId] ?? "").trim();
      const value = text === "" ? 0 : parseNumber(text);
      if (value === null || value < 0) {
        setError(t("Минимум для «{name}» — число от 0.", { name: r.name }));
        return;
      }
      items.push({ itemId: r.itemId, minQty: value });
    }
    if (items.length === 0) return;
    setBusy(true);
    setError(null);
    try {
      await bff("procurement/items/minimums", { method: "PUT", body: JSON.stringify({ items }) });
      router.refresh();
      onClose();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Sheet
      open={open}
      onClose={onClose}
      size="xl"
      title={single ? t("Минимальный остаток") : t("Задать минимумы")}
      subtitle={
        single
          ? item!.name
          : t("Минимум — остаток, ниже которого позицию пора закупать. Предложение — сколько выдано за 30 дней.")
      }
      footer={
        <div className="flex flex-wrap items-center justify-between gap-3">
          <span className="text-sm text-ink-3">{single ? t("Пустое поле — без минимума") : t("К сохранению: {n}", { n: filled.length })}</span>
          <div className="flex gap-2">
            <button type="button" onClick={onClose} className="inline-flex h-10 items-center rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted">
              {t("Отмена")}
            </button>
            <button
              type="button"
              onClick={() => void save()}
              disabled={busy || filled.length === 0}
              className="inline-flex h-10 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50"
            >
              {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}
              {t("Сохранить")}
            </button>
          </div>
        </div>
      }
    >
      <div className="space-y-4">
        {!single && (
          <div className="flex flex-wrap items-center gap-2">
            <label className="relative block min-w-[220px] flex-1">
              <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" aria-hidden />
              <span className="sr-only">{t("Поиск")}</span>
              <input
                type="search"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder={t("Артикул, название, штрихкод")}
                className="h-10 w-full rounded-lg border border-line-strong bg-surface pl-9 pr-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20"
              />
            </label>
            <button
              type="button"
              disabled={suggestions.length === 0}
              onClick={() => setValues((v) => ({ ...v, ...Object.fromEntries(suggestions.map((r) => [r.itemId, quantity(r.suggested!).replace(/\s/g, "")])) }))}
              className="inline-flex h-10 items-center gap-2 rounded-lg border border-line-strong bg-surface px-3.5 text-sm font-medium text-ink hover:bg-muted disabled:opacity-50"
            >
              <Sparkles className="size-4 text-accent" aria-hidden /> {t("Заполнить предложениями")}
              {suggestions.length > 0 && <span className="tabular-nums text-ink-3">{suggestions.length}</span>}
            </button>
          </div>
        )}
        <ErrorNote text={error} />
        {plan === null && !error ? (
          <p className="flex items-center gap-2 py-8 text-sm text-ink-3">
            <Loader2 className="size-4 animate-spin" aria-hidden /> {t("Загрузка")}
          </p>
        ) : rows.length === 0 ? (
          <p className="rounded-xl border border-dashed border-line px-4 py-8 text-center text-sm text-ink-3">
            {query ? t("По этому поиску позиций без минимума нет.") : t("У всех активных позиций минимум задан.")}
          </p>
        ) : (
          <>
            {!single && plan && (
              <p className="text-sm text-ink-2">
                {plan.total > rows.length
                  ? t("Без минимума: {total}. Показаны первые {n} — сначала ходовые.", { total: plan.total, n: rows.length })
                  : t("Без минимума: {total} — сначала ходовые.", { total: plan.total })}
              </p>
            )}
            <ul className="divide-y divide-line rounded-xl border border-line">
              {rows.map((r) => (
                <MinimumLine key={r.itemId} row={r} value={values[r.itemId] ?? ""} onChange={(v) => setValues((s) => ({ ...s, [r.itemId]: v }))} />
              ))}
            </ul>
          </>
        )}
      </div>
    </Sheet>
  );
}

function MinimumLine({ row, value, onChange }: { row: MinimumRow; value: string; onChange: (value: string) => void }) {
  const t = useT();
  return (
    <li className="flex flex-wrap items-center gap-x-4 gap-y-2 px-4 py-3">
      <div className="min-w-0 flex-1">
        <span className="block truncate font-medium text-ink" title={row.name}>
          {row.name}
        </span>
        <span className="mt-0.5 block text-xs text-ink-3">
          <span className="tabular-nums">{row.sku}</span> • {row.categoryName} • {t("на складе {n} {unit}", { n: quantity(row.onHand), unit: row.unitCode })}
        </span>
        <span className="mt-0.5 block text-xs text-ink-2">
          {row.issued30 > 0
            ? t("выдано за 30 дней: {n} {unit}", { n: quantity(row.issued30), unit: row.unitCode })
            : row.issued90 > 0
              ? t("за 30 дней не выдавали, за 90 дней: {n} {unit}", { n: quantity(row.issued90), unit: row.unitCode })
              : row.moving
                ? t("за 90 дней были движения, но не выдачи")
                : t("движений за 90 дней не было")}
        </span>
      </div>
      <div className="flex items-center gap-2">
        {row.suggested !== null && value.trim() === "" && (
          <button type="button" onClick={() => onChange(quantity(row.suggested!).replace(/\s/g, ""))} className="rounded-lg px-2 py-1 text-xs font-medium text-accent-strong hover:bg-accent-soft">
            {t("взять {n}", { n: quantity(row.suggested) })}
          </button>
        )}
        <label className="flex items-center gap-2">
          <span className="sr-only">{t("Минимум для «{name}»", { name: row.name })}</span>
          <input inputMode="decimal" value={value} onChange={(e) => onChange(e.target.value)} placeholder="—" className={input} />
          <span className="w-8 text-sm text-ink-3">{row.unitCode}</span>
        </label>
      </div>
    </li>
  );
}
