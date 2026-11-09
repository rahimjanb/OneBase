"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { AlertTriangle, ArrowRight, Info, Loader2, XCircle } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { CatalogPicker } from "@/components/requests/CatalogPicker";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import type { ProductOption } from "@/lib/production";
import {
  importApi,
  importStatusLabel,
  importStatusTone,
  parseManualNorm,
  type ImportDecision,
  type ImportIssue,
  type ImportLine,
  type ImportRecipeView,
} from "@/lib/recipe-import";
import { field, primary, secondary } from "../ProdKit";

const issueIcon = { block: XCircle, warn: AlertTriangle, info: Info } as const;
const issueTone = { block: "text-bad", warn: "text-warn", info: "text-ink-3" } as const;
const num = (v: number | null | undefined) => (v == null ? "—" : v.toLocaleString("ru-RU", { maximumFractionDigits: 15 }));

/**
 * Рецептура загрузки: шапка (лист, столбец, артикул, база норм, итог и потери листа как в Excel, товар Linko, позиция выхода полуфабриката),
 * замечания, построчная сверка «ячейка Excel → позиция склада → норма в техкарте» с решениями технолога, что изменится в карте OneBase и что
 * изменилось в файле с прошлой загрузки. Правка — только до создания черновика.
 */
export function ImportRecipeSheet({
  importId,
  recipeId,
  canEdit,
  onClose,
  onChanged,
}: {
  importId: string;
  recipeId: string;
  canEdit: boolean;
  onClose: () => void;
  onChanged: () => void;
}) {
  const t = useT();
  const [view, setView] = useState<ImportRecipeView | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(() => {
    importApi.recipe(importId, recipeId).then(setView, (e: Error) => setError(e.message));
  }, [importId, recipeId]);
  useEffect(load, [load]);

  const act = async (action: () => Promise<ImportRecipeView>) => {
    setBusy(true);
    setError(null);
    try {
      setView(await action());
      onChanged();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const editable = canEdit && view !== null && view.row.status !== "Applied";
  const recipeInput = (patch: Partial<{ code: string | null; linkoProductId: number | null; include: boolean }>) => ({
    code: view?.row.code ?? null,
    linkoProductId: view?.linkoProductId ?? null,
    include: view?.row.include ?? true,
    ...patch,
  });

  return (
    <Sheet open onClose={onClose} title={view?.row.name ?? t("Рецептура")} subtitle={view ? `${view.row.sheet} · ${t("столбец {c}", { c: view.row.column })}` : undefined} size="xl">
      <div className="space-y-4 overflow-y-auto p-4">
        {!view ? (
          error ? <ErrorNote text={error} /> : <Loader2 className="mx-auto size-6 animate-spin text-ink-3" aria-label={t("Загрузка")} />
        ) : (
          <>
            <div className="flex flex-wrap items-center gap-2">
              <span className={`rounded-md px-2 py-0.5 text-xs font-semibold ${importStatusTone[view.row.status]}`}>{t(importStatusLabel[view.row.status])}</span>
              {view.row.recipeId && (
                <Link href={`/production/recipes/${view.row.recipeId}`} className="text-sm font-medium text-accent-strong hover:underline">
                  {t("Открыть техкарту")}
                </Link>
              )}
              {editable && (
                <label className="ml-auto flex items-center gap-2 text-sm text-ink-2">
                  <input
                    type="checkbox"
                    className="size-4 accent-[var(--color-accent)]"
                    checked={view.row.include}
                    disabled={busy}
                    onChange={(e) => act(() => importApi.updateRecipe(importId, recipeId, recipeInput({ include: e.target.checked })))}
                  />
                  {t("Создавать техкарту")}
                </label>
              )}
            </div>

            <dl className="grid gap-x-6 gap-y-1 text-sm sm:grid-cols-2">
              <Fact label={t("Артикул в Excel")}>{view.row.article ?? <span className="text-warn">{t("нет")}</span>}</Fact>
              <Fact label={t("Код техкарты")}>
                {editable && !view.row.article ? (
                  <CodeField value={view.row.code ?? ""} disabled={busy} onSave={(code) => act(() => importApi.updateRecipe(importId, recipeId, recipeInput({ code })))} />
                ) : (
                  view.row.code ?? "—"
                )}
              </Fact>
              <Fact label={t("Группа")}>{view.groupName ?? "—"}</Fact>
              <Fact label={t("Нормы заданы на")}>
                {view.row.baseQuantity ? `${num(view.row.baseQuantity)} ${view.row.baseUnit}` : <span className="text-warn">{t("не определено")}</span>}
                {view.baseLabel && <span className="block text-xs text-ink-3">{view.baseLabel}</span>}
              </Fact>
              <Fact label={t("Итог листа «на тонну»")}>
                {view.totalCell ? `${view.totalCell}: ${view.totalRaw ?? "—"}` : "—"}
                {view.totalFormula && view.totalFormula !== "shared" && <span className="block break-all text-xs text-ink-3">={view.totalFormula}</span>}
              </Fact>
              <Fact label={t("Потери в Excel")}>{view.lossCell ? `${view.lossCell}: ${view.lossRaw ?? "—"}` : "—"}</Fact>
              {view.row.isSemiFinished ? (
                <Fact label={t("Позиция склада выхода")}>{view.outputItemName ?? <span className="text-warn">{t("не заведена")}</span>}</Fact>
              ) : (
                <Fact label={t("Товар Linko")}>
                  <LinkoField
                    view={view}
                    editable={editable}
                    busy={busy}
                    onSave={(linkoProductId) => act(() => importApi.updateRecipe(importId, recipeId, recipeInput({ linkoProductId })))}
                  />
                </Fact>
              )}
              {view.existingLabel && (
                <Fact label={t("В OneBase")}>
                  {view.existingRecipeId ? (
                    <Link href={`/production/recipes/${view.existingRecipeId}`} className="text-accent-strong hover:underline">
                      {view.existingLabel}
                    </Link>
                  ) : (
                    view.existingLabel
                  )}
                </Fact>
              )}
            </dl>

            {view.issues.length > 0 && <Issues issues={view.issues} />}
            <ErrorNote text={error} />

            <section>
              <h3 className="mb-2 text-sm font-semibold text-ink">{t("Сверка: Excel → техкарта")}</h3>
              <div className="overflow-x-auto rounded-xl border border-line">
                <table className="w-full min-w-[760px] text-sm">
                  <thead className="bg-muted/60 text-left text-xs text-ink-3">
                    <tr>
                      <th className="px-3 py-2 font-medium">{t("Ячейка")}</th>
                      <th className="px-3 py-2 font-medium">{t("Сырьё в Excel")}</th>
                      <th className="px-3 py-2 text-right font-medium">{t("В Excel")}</th>
                      <th className="px-3 py-2 font-medium">{t("Позиция склада")}</th>
                      <th className="px-3 py-2 text-right font-medium">{t("В техкарту")}</th>
                      <th className="px-3 py-2 font-medium">{t("Итог")}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-line">
                    {view.lines.map((l) => (
                      <LineRow key={l.id} line={l} editable={editable} busy={busy}
                        onDecide={(input) => act(() => importApi.decide(importId, l.id, input))} />
                    ))}
                  </tbody>
                </table>
              </div>
            </section>

            {view.cardChanges.length > 0 && (
              <Changes title={t("Что изменится в техкарте {label}", { label: view.existingLabel ?? "" })} changes={view.cardChanges} />
            )}
            {view.fileChanges.length > 0 && (
              <Changes title={t("Что изменилось в файле с загрузки {against}", { against: view.fileChangesAgainst ?? "" })} changes={view.fileChanges} />
            )}
          </>
        )}
      </div>
    </Sheet>
  );
}

function Fact({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex items-start justify-between gap-3 border-b border-line/60 py-1.5">
      <dt className="shrink-0 text-ink-3">{label}</dt>
      <dd className="min-w-0 text-right text-ink">{children}</dd>
    </div>
  );
}

function Issues({ issues }: { issues: ImportIssue[] }) {
  const t = useT();
  const order = { block: 0, warn: 1, info: 2 } as const;
  return (
    <section className="space-y-1.5 rounded-xl bg-muted/40 p-3">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-ink-3">{t("Замечания")}</h3>
      {[...issues]
        .sort((a, b) => order[a.level] - order[b.level])
        .map((i, n) => {
          const Icon = issueIcon[i.level];
          return (
            <p key={n} className={`flex items-start gap-2 text-sm ${issueTone[i.level]}`}>
              <Icon className="mt-0.5 size-4 shrink-0" aria-hidden />
              <span className="text-ink-2">
                {i.cell && <span className="mr-1 font-mono text-xs text-ink-3">{i.cell}</span>}
                {i.text}
              </span>
            </p>
          );
        })}
    </section>
  );
}

/** Строка сверки: ячейка, сырьё и единица в Excel, значение как в файле (и формула), позиция склада, норма в техкарту, итог и решение технолога. */
function LineRow({
  line: l,
  editable,
  busy,
  onDecide,
}: {
  line: ImportLine;
  editable: boolean;
  busy: boolean;
  onDecide: (input: { itemId: string | null; decision: ImportDecision; manualValue: number | null; comment: string | null }) => void;
}) {
  const t = useT();
  const [mode, setMode] = useState<"none" | "manual" | "exclude">("none");
  const [value, setValue] = useState(l.manualValue != null ? String(l.manualValue) : "");
  const [comment, setComment] = useState(l.decisionComment ?? "");
  const muted = l.kind === "Zero";
  const needsValue = (l.kind === "Error" || l.kind === "Text") && l.decision !== "ManualValue" && l.decision !== "Exclude";
  const parsed = parseManualNorm(value);
  return (
    <tr className={muted ? "text-ink-3" : "text-ink"}>
      <td className="whitespace-nowrap px-3 py-2 align-top font-mono text-xs text-ink-3">{l.cell}</td>
      <td className="px-3 py-2 align-top">
        <span className="block">{l.name ?? "—"}</span>
        <span className="block text-xs text-ink-3">
          {l.article ?? t("без артикула")} · {l.unit ?? <span className="text-warn">{t("единица не указана")}</span>}
          {l.weight === "1" ? ` · ${t("в массе")}` : ""}
        </span>
      </td>
      <td className="whitespace-nowrap px-3 py-2 text-right align-top tabular-nums">
        {/* Число — как показывает Excel; внутренняя запись double (4.0999999999999996) — только в подсказке. */}
        {l.kind === "Number" || l.kind === "Zero" || l.kind === "Negative" ? (
          <span title={l.raw ? t("В файле: {raw}", { raw: l.raw }) : undefined}>{num(l.value)}</span>
        ) : (
          <span className="text-warn">{l.text}</span>
        )}
        {l.formula && l.formula !== "shared" && <span className="block max-w-40 truncate text-xs text-ink-3">={l.formula}</span>}
      </td>
      <td className="px-3 py-2 align-top">
        {l.itemId ? (
          <span>
            {l.itemName}
            <span className="block text-xs text-ink-3">
              {l.itemSku} · {l.itemUnit} · {l.match === "Sku" ? t("по артикулу") : l.match === "Saved" ? t("запомнено") : t("вручную")}
            </span>
          </span>
        ) : l.kind === "Number" && editable ? (
          <CatalogPicker
            name=""
            linked={false}
            label={t("Позиция склада")}
            placeholder={t("Сопоставить")}
            onPick={(o) => onDecide({ itemId: o.id, decision: l.decision, manualValue: l.manualValue, comment: l.decisionComment })}
          />
        ) : l.kind === "Number" ? (
          <span className="text-warn">{t("нет в каталоге")}</span>
        ) : (
          "—"
        )}
      </td>
      <td className="whitespace-nowrap px-3 py-2 text-right align-top tabular-nums">
        {l.cardQuantity != null ? (
          <span className="font-medium">
            {num(l.cardQuantity)} {l.cardUnit}
          </span>
        ) : (
          "—"
        )}
      </td>
      <td className="px-3 py-2 align-top text-xs">
        <span className={l.cardQuantity != null ? "text-ok" : muted ? "text-ink-3" : "text-warn"}>{t(l.outcome)}</span>
        {l.decisionComment && <span className="block text-ink-3">{l.decisionComment}</span>}
        {editable && !muted && (
          <div className="mt-1 flex flex-wrap gap-x-3 gap-y-1">
            {(needsValue || l.decision === "ManualValue" || (l.kind === "Number" && l.value != null && String(l.value).split(".")[1]?.length > 6)) && (
              <button className="text-accent-strong hover:underline" onClick={() => setMode("manual")}>
                {t("Норма вручную")}
              </button>
            )}
            {l.decision !== "Exclude" && l.kind !== "Negative" && (
              <button className="text-accent-strong hover:underline" onClick={() => setMode("exclude")}>
                {t("Не переносить")}
              </button>
            )}
            {!l.unit && l.itemId && l.decision !== "UnitConfirmed" && (
              <button className="text-accent-strong hover:underline" disabled={busy}
                onClick={() => onDecide({ itemId: null, decision: "UnitConfirmed", manualValue: null, comment: null })}>
                {t("Подтвердить «{u}»", { u: l.itemUnit ?? "" })}
              </button>
            )}
            {l.decision !== "None" && (
              <button className="text-ink-3 hover:underline" disabled={busy} onClick={() => onDecide({ itemId: null, decision: "None", manualValue: null, comment: null })}>
                {t("Как в Excel")}
              </button>
            )}
          </div>
        )}
        {mode !== "none" && (
          <div className="mt-2 space-y-1.5 rounded-lg bg-muted/50 p-2">
            {mode === "manual" && (
              <input className={`${field} h-8`} inputMode="decimal" placeholder={t("Норма, до 6 знаков")} value={value} onChange={(e) => setValue(e.target.value)} />
            )}
            <input className={`${field} h-8`} placeholder={t("Причина (обязательно)")} value={comment} onChange={(e) => setComment(e.target.value)} />
            <div className="flex gap-2">
              <button
                className={`${primary} h-8`}
                disabled={busy || !comment.trim() || (mode === "manual" && parsed === null)}
                onClick={() => {
                  onDecide({ itemId: null, decision: mode === "manual" ? "ManualValue" : "Exclude", manualValue: mode === "manual" ? parsed : null, comment: comment.trim() });
                  setMode("none");
                }}
              >
                {t("Сохранить")}
              </button>
              <button className={`${secondary} h-8`} onClick={() => setMode("none")}>
                {t("Отмена")}
              </button>
            </div>
          </div>
        )}
      </td>
    </tr>
  );
}

function Changes({ title, changes }: { title: string; changes: { kind: string; subject: string; before: string | null; after: string | null }[] }) {
  const t = useT();
  const label = { added: "добавится", removed: "уйдёт", changed: "изменится" } as Record<string, string>;
  return (
    <section>
      <h3 className="mb-2 text-sm font-semibold text-ink">{title}</h3>
      <ul className="space-y-1 text-sm">
        {changes.map((c, i) => (
          <li key={i} className="flex flex-wrap items-center gap-x-2">
            <span className={`rounded px-1.5 text-xs ${c.kind === "added" ? "bg-ok-soft text-ok" : c.kind === "removed" ? "bg-bad-soft text-bad" : "bg-warn-soft text-warn"}`}>
              {t(label[c.kind] ?? c.kind)}
            </span>
            <span className="text-ink">{c.subject}</span>
            <span className="tabular-nums text-ink-3">{c.before ?? "—"}</span>
            <ArrowRight className="size-3 text-ink-3" aria-hidden />
            <span className="tabular-nums text-ink">{c.after ?? "—"}</span>
          </li>
        ))}
      </ul>
    </section>
  );
}

function CodeField({ value, disabled, onSave }: { value: string; disabled: boolean; onSave: (code: string | null) => void }) {
  const t = useT();
  const [text, setText] = useState(value);
  return (
    <span className="flex items-center justify-end gap-2">
      <input className={`${field} h-8 w-40`} value={text} maxLength={32} placeholder={t("Код техкарты")} onChange={(e) => setText(e.target.value)} />
      <button className={`${secondary} h-8`} disabled={disabled || text.trim() === value} onClick={() => onSave(text.trim() || null)}>
        {t("Сохранить")}
      </button>
    </span>
  );
}

/** Товар Linko: найден по точному коду — показан; найден по номеру артикула — «Подтвердить»; иначе — выбрать из списка. */
function LinkoField({ view, editable, busy, onSave }: { view: ImportRecipeView; editable: boolean; busy: boolean; onSave: (id: number | null) => void }) {
  const t = useT();
  const [search, setSearch] = useState("");
  const [options, setOptions] = useState<ProductOption[]>([]);
  useEffect(() => {
    if (!editable || search.trim().length < 2) return;
    const timer = window.setTimeout(() => {
      bff<ProductOption[]>(`production/products?q=${encodeURIComponent(search.trim())}`).then(setOptions, () => setOptions([]));
    }, 250);
    return () => window.clearTimeout(timer);
  }, [search, editable]);

  if (view.linkoProductId) {
    return (
      <span>
        {view.linkoName ?? view.linkoProductId}
        {editable && (
          <button className="ml-2 text-xs text-ink-3 hover:underline" disabled={busy} onClick={() => onSave(null)}>
            {t("убрать")}
          </button>
        )}
      </span>
    );
  }

  return (
    <span className="flex flex-col items-end gap-1">
      {view.linkoSuggestedId && (
        <span className="text-xs text-ink-2">
          {t("Похоже: {name}", { name: view.linkoSuggestedName ?? "" })}
          {editable && (
            <button className="ml-2 font-medium text-accent-strong hover:underline" disabled={busy} onClick={() => onSave(view.linkoSuggestedId)}>
              {t("Подтвердить")}
            </button>
          )}
        </span>
      )}
      {editable ? (
        <span className="relative w-56">
          <input className={`${field} h-8`} placeholder={t("Найти товар Linko")} value={search} onChange={(e) => setSearch(e.target.value)} />
          {options.length > 0 && search.trim().length >= 2 && (
            <ul className="absolute right-0 z-10 mt-1 max-h-56 w-72 overflow-y-auto rounded-lg border border-line bg-surface-solid text-left text-xs shadow-lg">
              {options.map((o) => (
                <li key={o.id}>
                  <button className="block w-full px-3 py-2 text-left hover:bg-muted" onClick={() => (onSave(o.id), setSearch(""), setOptions([]))}>
                    {o.code ? `${o.code} · ` : ""}
                    {o.name}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </span>
      ) : (
        <span className="text-ink-3">{t("не выбран")}</span>
      )}
    </span>
  );
}
