"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";
import { BookOpenCheck, Plus, Search } from "lucide-react";
import { useT } from "@/i18n/client";
import { quantity } from "@/lib/procurement";
import { dayRu, type RecipeList } from "@/lib/production";
import { RecipeStatusPill, field } from "./ProdKit";

const statuses = [
  { value: "", label: "Все" },
  { value: "active", label: "Действующие" },
  { value: "draft", label: "С черновиком" },
  { value: "archived", label: "Архив" },
] as const;

/**
 * Рецептуры: по коду — действующая версия (а нет — последняя), выход и на сколько заданы нормы, позиций, есть ли черновик новой версии;
 * поиск по коду, названию и выходу, фильтр по состоянию. Новую рецептуру создаёт технолог.
 */
export function RecipesView({ data }: { data: RecipeList }) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [search, setSearch] = useState(params.get("q") ?? "");
  const status = params.get("status") ?? "";
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

  if (data.total === 0 && !status && !params.get("q")) {
    return (
      <section className="flex flex-col items-center rounded-2xl border border-line bg-surface px-6 py-14 text-center">
        <span className="grid size-14 place-items-center rounded-2xl bg-accent-soft text-accent-strong">
          <BookOpenCheck className="size-7" aria-hidden />
        </span>
        <h2 className="mt-4 text-lg font-semibold text-ink">{t("Рецептур пока нет")}</h2>
        <p className="mt-1 max-w-md text-sm text-ink-2">
          {t("Рецептура — состав продукции: какие позиции склада и сколько уходит на выход, с технологическими потерями. По действующей версии создаются задания на выпуск.")}
        </p>
        {data.canCreate && (
          <Link href="/production/recipes/new" className="mt-5 inline-flex h-10 items-center gap-2 rounded-xl bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong">
            <Plus className="size-4" aria-hidden /> {t("Новая рецептура")}
          </Link>
        )}
      </section>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-2">
        <label className="relative min-w-0 flex-1 basis-64">
          <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" aria-hidden />
          <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("Код, название или выход")} aria-label={t("Поиск рецептуры")} className={`${field} pl-9`} />
        </label>
        <div className="flex flex-wrap gap-1.5">
          {statuses.map((s) => (
            <button
              key={s.value}
              type="button"
              onClick={() => apply({ status: s.value || null })}
              aria-pressed={status === s.value}
              className={`h-9 rounded-lg border px-3 text-sm font-medium ${status === s.value ? "border-accent bg-accent text-white" : "border-line bg-surface text-ink hover:bg-muted"}`}
            >
              {t(s.label)}
            </button>
          ))}
        </div>
      </div>

      {data.items.length === 0 ? (
        <p className="rounded-xl border border-line bg-surface px-4 py-8 text-center text-sm text-ink-3">{t("Ничего не нашлось — измените поиск или фильтр.")}</p>
      ) : (
        <ul className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
          {data.items.map((r) => (
            <li key={r.id}>
              <Link href={`/production/recipes/${r.id}`} className="block h-full rounded-2xl border border-line bg-surface p-4 hover:border-accent/40 hover:bg-muted/40">
                <div className="flex items-start justify-between gap-2">
                  <span className="min-w-0">
                    <span className="block truncate font-semibold text-ink">{r.name}</span>
                    <span className="block text-xs tabular-nums text-ink-3">
                      {r.code} · {t("версия {n}", { n: r.versionNo })}
                      {r.versions > 1 && ` · ${t("версий: {n}", { n: r.versions })}`}
                    </span>
                  </span>
                  <RecipeStatusPill status={r.status} />
                </div>
                <p className="mt-3 text-sm text-ink-2">
                  {t("Выход: {name}", { name: r.outputName })} — <span className="tabular-nums">{quantity(r.outputQuantity)} {r.outputUnit}</span>
                </p>
                <p className="mt-1 text-xs text-ink-3">
                  {t("позиций: {n}", { n: r.lines })}
                  {r.activatedAt && ` · ${t("в действии с {date}", { date: dayRu(r.activatedAt) })}`}
                </p>
                {r.draftId && r.draftId !== r.id && <p className="mt-2 text-xs font-medium text-warn">{t("Есть черновик новой версии")}</p>}
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
