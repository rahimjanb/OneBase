"use client";

import Link from "next/link";
import { useMemo, useState } from "react";
import { AlertTriangle, CheckCircle2, FilePlus2, Info, Layers, PackageSearch, RefreshCw, Search } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { CatalogPicker } from "@/components/requests/CatalogPicker";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import {
  fileSize,
  filterCounts,
  importActionLabel,
  importApi,
  importStatusLabel,
  importStatusTone,
  matchesFilter,
  type ImportApplyResult,
  type ImportFilter,
  type ImportMissing,
  type ImportRecipeRow,
  type ImportView,
} from "@/lib/recipe-import";
import { card, primary, secondary } from "../ProdKit";
import { ImportRecipeSheet } from "./ImportRecipeSheet";

const tabs: { key: ImportFilter; label: string }[] = [
  { key: "all", label: "Все" },
  { key: "ready", label: "Готовы" },
  { key: "review", label: "Нужна проверка" },
  { key: "unchanged", label: "Без изменений" },
  { key: "skipped", label: "Дубли и пустые" },
  { key: "applied", label: "Черновики созданы" },
  { key: "excluded", label: "Исключены" },
];

/**
 * Загрузка файла техкарт: сводка (рецептуры, нормы, ошибки Excel, сопоставление со складом), отборы по статусу, таблица рецептур — у
 * каждой первая причина, почему она не готова; карточка рецептуры — построчная сверка «Excel → техкарта» и решения технолога. Создание
 * черновиков — только готовых и только после подтверждения; дальше обычное утверждение техкарт.
 */
export function ImportScreen({ initial, existing }: { initial: ImportView; existing: boolean }) {
  const t = useT();
  const [view, setView] = useState(initial);
  const [filter, setFilter] = useState<ImportFilter>(initial.totals.needsReview > 0 ? "review" : "all");
  const [search, setSearch] = useState("");
  const [open, setOpen] = useState<string | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [confirm, setConfirm] = useState(false);
  const [result, setResult] = useState<ImportApplyResult | null>(null);
  const [missing, setMissing] = useState<ImportMissing[] | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const counts = useMemo(() => filterCounts(view.recipes), [view.recipes]);
  const rows = useMemo(() => {
    const q = search.trim().toLowerCase();
    return view.recipes.filter(
      (r) => matchesFilter(r, filter) && (!q || `${r.article ?? ""} ${r.name} ${r.sheet}`.toLowerCase().includes(q)),
    );
  }, [view.recipes, filter, search]);
  const ready = view.recipes.filter((r) => r.include && r.status === "Ready");
  const chosen = selected.size > 0 ? ready.filter((r) => selected.has(r.id)) : ready;

  const run = async (what: string, action: () => Promise<void>) => {
    setBusy(what);
    setError(null);
    try {
      await action();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(null);
    }
  };

  const refresh = () => run("refresh", async () => setView(await importApi.get(view.id)));
  const showMissing = () => run("missing", async () => setMissing(await importApi.missing(view.id)));

  const total = view.totals;
  const stats: { label: string; value: number; tone?: string; hint?: string }[] = [
    { label: "Рецептур с составом", value: total.withComposition, hint: t("из {n} столбцов продуктов", { n: total.recipes }) },
    { label: "Готовы", value: total.ready, tone: "text-ok" },
    { label: "Нужна проверка", value: total.needsReview, tone: total.needsReview > 0 ? "text-warn" : undefined },
    { label: "Черновики созданы", value: total.applied, tone: "text-accent-strong" },
    { label: "Норм в Excel", value: total.norms, hint: t("нулей {z}, отрицательных {n}", { z: total.zeros, n: total.negatives }) },
    { label: "Найдено на складе", value: total.linesMatched, hint: t("не найдено строк {l}, артикулов {a}", { l: total.linesUnmatched, a: total.articlesUnmatched }) },
    { label: "Ошибки и текст в нормах", value: total.errorCells + total.textCells, tone: total.errorCells + total.textCells > 0 ? "text-warn" : undefined },
    { label: "Полуфабрикатов", value: total.semiFinished },
  ];

  return (
    <div className="space-y-4">
      {existing && (
        <p className="flex items-start gap-2 rounded-xl bg-accent-soft/50 px-4 py-3 text-sm text-ink-2">
          <Info className="mt-0.5 size-4 shrink-0 text-accent-strong" aria-hidden />
          {t("Этот файл уже загружали — открыта прежняя загрузка, новая не создана. Если обновили каталог склада — нажмите «Проверить заново».")}
        </p>
      )}

      <section className={card}>
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="min-w-0 text-sm text-ink-2">
            <p>
              {t("Загружен {when} · {who} · {size}", {
                when: new Date(view.createdAt).toLocaleString("ru-RU", { dateStyle: "short", timeStyle: "short" }),
                who: view.createdBy ?? "—",
                size: fileSize(view.fileSize),
              })}
            </p>
            <p className="mt-1 text-xs text-ink-3">
              {t("Листы:")}{" "}
              {view.sheets
                .map((s) => (s.hasRecipes ? `${s.name} (${s.withComposition})` : `${s.name} — ${t("не рецептуры")}${s.hidden ? `, ${t("скрыт")}` : ""}`))
                .join(" · ")}
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            <button className={secondary} disabled={!!busy} onClick={() => run("rematch", async () => setView(await importApi.rematch(view.id)))}>
              <RefreshCw className={`size-4 ${busy === "rematch" ? "animate-spin" : ""}`} aria-hidden /> {t("Проверить заново")}
            </button>
            <button className={primary} disabled={!!busy || ready.length === 0} onClick={() => setConfirm(true)}>
              <FilePlus2 className="size-4" aria-hidden />
              {selected.size > 0 ? t("Создать черновики: {n}", { n: chosen.length }) : t("Создать черновики готовых: {n}", { n: ready.length })}
            </button>
          </div>
        </div>
        <dl className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-4">
          {stats.map((s) => (
            <div key={s.label} className="rounded-xl bg-muted/50 px-3 py-2.5">
              <dt className="text-xs text-ink-3">{t(s.label)}</dt>
              <dd className={`text-xl font-semibold tabular-nums ${s.tone ?? "text-ink"}`}>{s.value.toLocaleString("ru-RU")}</dd>
              {s.hint && <dd className="text-xs text-ink-3">{s.hint}</dd>}
            </div>
          ))}
        </dl>
        {total.articlesUnmatched > 0 && (
          <button className="mt-3 inline-flex items-center gap-1.5 text-sm font-medium text-accent-strong hover:underline" onClick={showMissing} disabled={!!busy}>
            <PackageSearch className="size-4" aria-hidden /> {t("Артикулы, которых нет в каталоге склада: {n}", { n: total.articlesUnmatched })}
          </button>
        )}
        <div className="mt-3">
          <ErrorNote text={error} />
        </div>
      </section>

      {result && (
        <section className={`${card} space-y-2`}>
          <h2 className="flex items-center gap-2 text-sm font-semibold text-ink">
            <CheckCircle2 className="size-4 text-ok" aria-hidden />
            {t("Создано черновиков: новых техкарт {c}, новых версий {v}", { c: result.created, v: result.newVersions })}
            {result.failed > 0 && <span className="text-bad">{t("· не создано {n}", { n: result.failed })}</span>}
          </h2>
          <p className="text-xs text-ink-3">
            {t("Черновики проходят обычный маршрут: технолог распределяет компоненты по этапам и отправляет на утверждение, утверждает другой технолог или начальник производства.")}
          </p>
          <ul className="max-h-48 space-y-1 overflow-y-auto text-sm">
            {result.rows.map((r) => (
              <li key={r.importRecipeId} className="flex flex-wrap gap-x-2">
                {r.recipeId ? (
                  <Link href={`/production/recipes/${r.recipeId}`} className="text-accent-strong hover:underline">
                    {r.name}
                  </Link>
                ) : (
                  <span className="text-ink">{r.name}</span>
                )}
                <span className={r.outcome === "failed" ? "text-bad" : "text-ink-3"}>
                  {r.outcome === "created" ? t("новая техкарта") : r.outcome === "new_version" ? t("новая версия") : r.error ?? r.outcome}
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}

      <section className={card}>
        <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
          <nav className="-mx-1 flex gap-1 overflow-x-auto pb-1" aria-label={t("Отбор рецептур")}>
            {tabs.map((tab) => (
              <button
                key={tab.key}
                onClick={() => setFilter(tab.key)}
                className={`shrink-0 rounded-lg px-3 py-1.5 text-sm ${filter === tab.key ? "bg-accent-soft font-semibold text-accent-strong" : "text-ink-2 hover:bg-muted"}`}
              >
                {t(tab.label)} <span className="tabular-nums text-ink-3">{counts[tab.key]}</span>
              </button>
            ))}
          </nav>
          <label className="relative block lg:w-72">
            <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" aria-hidden />
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={t("Артикул, продукт, лист")}
              className="h-9 w-full rounded-lg border border-line bg-surface pl-9 pr-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none"
            />
          </label>
        </div>

        {rows.length === 0 ? (
          <p className="py-10 text-center text-sm text-ink-3">{t("В этом отборе рецептур нет.")}</p>
        ) : (
          <ul className="mt-3 divide-y divide-line">
            {rows.map((r) => (
              <RecipeRow
                key={r.id}
                row={r}
                selectable={r.include && r.status === "Ready"}
                selected={selected.has(r.id)}
                onSelect={(on) =>
                  setSelected((s) => {
                    const next = new Set(s);
                    if (on) next.add(r.id);
                    else next.delete(r.id);
                    return next;
                  })
                }
                onOpen={() => setOpen(r.id)}
              />
            ))}
          </ul>
        )}
      </section>

      {open && (
        <ImportRecipeSheet
          importId={view.id}
          recipeId={open}
          canEdit={view.canEdit}
          onClose={() => setOpen(null)}
          onChanged={() => void refresh()}
        />
      )}

      <Sheet open={confirm} onClose={() => setConfirm(false)} title={t("Создать черновики техкарт")}>
        <div className="space-y-3 p-4 text-sm text-ink-2">
          <p>
            {t("Будет создано черновиков: {n} — новых техкарт {c}, новых версий {v}.", {
              n: chosen.length,
              c: chosen.filter((r) => r.action === "Create").length,
              v: chosen.filter((r) => r.action === "NewVersion").length,
            })}
          </p>
          <ul className="list-disc space-y-1 pl-5 text-xs text-ink-3">
            <li>{t("Нормы — как в ячейках Excel, на 1 000 кг выхода; компоненты без этапов, с пометкой «Требуется распределение по этапам технологом».")}</li>
            <li>{t("Утверждённые техкарты не меняются: если состав другой — создаётся черновик новой версии.")}</li>
            <li>{t("Позиции склада не создаются и не меняются.")}</li>
            <li>{t("Рецептуры «Нужна проверка», дубли, пустые и исключённые не создаются.")}</li>
          </ul>
          <div className="flex justify-end gap-2 pt-2">
            <button className={secondary} onClick={() => setConfirm(false)}>
              {t("Отмена")}
            </button>
            <button
              className={primary}
              disabled={!!busy}
              onClick={() =>
                run("apply", async () => {
                  const res = await importApi.apply(view.id, selected.size > 0 ? chosen.map((r) => r.id) : null);
                  setResult(res);
                  setSelected(new Set());
                  setConfirm(false);
                  setView(await importApi.get(view.id));
                })
              }
            >
              <FilePlus2 className="size-4" aria-hidden /> {busy === "apply" ? t("Создаю…") : t("Создать")}
            </button>
          </div>
        </div>
      </Sheet>

      <Sheet open={missing !== null} onClose={() => setMissing(null)} title={t("Нет в каталоге склада")} size="xl">
        <MissingList importId={view.id} rows={missing ?? []} canEdit={view.canEdit} onMapped={(v, rest) => (setView(v), setMissing(rest))} />
      </Sheet>
    </div>
  );
}

function RecipeRow({
  row,
  selectable,
  selected,
  onSelect,
  onOpen,
}: {
  row: ImportRecipeRow;
  selectable: boolean;
  selected: boolean;
  onSelect: (on: boolean) => void;
  onOpen: () => void;
}) {
  const t = useT();
  return (
    <li className="flex items-start gap-3 py-3">
      <input
        type="checkbox"
        className="mt-1 size-4 shrink-0 accent-[var(--color-accent)] disabled:opacity-30"
        disabled={!selectable}
        checked={selected}
        aria-label={t("Выбрать {name}", { name: row.name })}
        onChange={(e) => onSelect(e.target.checked)}
      />
      <button onClick={onOpen} className="grid min-w-0 flex-1 gap-1 text-left sm:grid-cols-[minmax(0,2fr)_minmax(0,1fr)_auto] sm:items-start sm:gap-4">
        <span className="min-w-0">
          <span className="flex flex-wrap items-center gap-x-2 gap-y-1">
            <span className="truncate text-sm font-medium text-ink">{row.name}</span>
            {row.isSemiFinished && (
              <span className="inline-flex items-center gap-1 rounded-md bg-muted px-1.5 text-xs text-ink-2">
                <Layers className="size-3" aria-hidden /> {t("полуфабрикат")}
              </span>
            )}
          </span>
          <span className="block text-xs text-ink-3">
            {row.code ?? t("без артикула")} · {row.sheet} · {t("столбец {c}", { c: row.column })}
          </span>
          {row.firstIssue && row.status !== "Applied" && (
            <span className={`mt-1 flex items-start gap-1.5 text-xs ${row.blockers > 0 ? "text-warn" : "text-ink-3"}`}>
              <AlertTriangle className="mt-0.5 size-3 shrink-0" aria-hidden />
              <span className="line-clamp-2">{row.firstIssue}</span>
            </span>
          )}
        </span>
        <span className="text-xs tabular-nums text-ink-2">
          {t("компонентов {n}", { n: row.components })}
          <span className="block text-ink-3">
            {t("на складе {m}", { m: row.matched })}
            {row.unmatched > 0 && <span className="text-warn"> · {t("нет {u}", { u: row.unmatched })}</span>}
            {row.errors > 0 && <span className="text-warn"> · {t("не числа {e}", { e: row.errors })}</span>}
          </span>
        </span>
        <span className="flex flex-wrap items-center gap-1.5 sm:justify-end">
          <span className={`rounded-md px-2 py-0.5 text-xs font-semibold ${row.include ? importStatusTone[row.status] : "bg-muted text-ink-3"}`}>
            {row.include ? t(importStatusLabel[row.status]) : t("Исключена")}
          </span>
          {row.status === "Ready" && row.action !== "None" && <span className="text-xs text-ink-3">{t(importActionLabel[row.action])}</span>}
        </span>
      </button>
    </li>
  );
}

/** Артикулы Excel без позиции склада: сопоставить с позицией каталога (все строки загрузки сразу; запоминается) — или завести позицию в каталоге. */
function MissingList({
  importId,
  rows,
  canEdit,
  onMapped,
}: {
  importId: string;
  rows: ImportMissing[];
  canEdit: boolean;
  onMapped: (view: ImportView, rest: ImportMissing[]) => void;
}) {
  const t = useT();
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  return (
    <div className="space-y-3 p-4">
      <p className="text-sm text-ink-2">
        {t("Позиции с такими артикулами нет в каталоге склада. Выберите позицию, если она заведена под другим артикулом, — сопоставление запомнится. Новые позиции заводят в каталоге склада: сами они не создаются.")}
      </p>
      <Link href="/procurement/stock" className="text-sm font-medium text-accent-strong hover:underline">
        {t("Открыть каталог склада")}
      </Link>
      <ErrorNote text={error} />
      <ul className="divide-y divide-line">
        {rows.map((m) => (
          <li key={m.article} className="grid gap-2 py-3 sm:grid-cols-[minmax(0,1fr)_minmax(0,1.2fr)] sm:items-center sm:gap-4">
            <div className="min-w-0 text-sm">
              <span className="font-medium text-ink">{m.article || t("без артикула")}</span> <span className="text-ink-2">{m.names.join(" / ")}</span>
              <span className="block text-xs text-ink-3">
                {m.units.filter(Boolean).join(", ") || t("единица не указана")} · {t("рецептур {r}, строк {l}", { r: m.recipes, l: m.lines })}
                {m.semiFinished && ` · ${t("полуфабрикат этого файла")}`}
              </span>
            </div>
            {canEdit && m.article && (
              <CatalogPicker
                name=""
                linked={false}
                label={t("Позиция склада для {a}", { a: m.article })}
                placeholder={busy === m.article ? t("Сопоставляю…") : t("Найти позицию склада")}
                onPick={async (o) => {
                  setBusy(m.article);
                  setError(null);
                  try {
                    const view = await importApi.map(importId, m.article, o.id);
                    onMapped(view, rows.filter((x) => x.article !== m.article));
                  } catch (e) {
                    setError((e as Error).message);
                  } finally {
                    setBusy(null);
                  }
                }}
              />
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}
