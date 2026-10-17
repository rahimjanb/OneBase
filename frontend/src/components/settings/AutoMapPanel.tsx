"use client";

import { useMemo, useState } from "react";
import { AlertTriangle, Loader2, Sparkles, X } from "lucide-react";
import { PeriodSelect } from "@/components/settings/PeriodSelect";
import type { MappingDecision, MappingFieldSetting, MappingStatus, MappingSuggestion, PeriodFilter, SheetMappingProposal } from "@/lib/data-sources";

const select = "h-8 w-full rounded-md border border-line bg-surface px-2 text-xs text-ink focus:border-accent/60 focus:outline-none disabled:opacity-60";

const statusView: Record<MappingStatus, { label: string; className: string }> = {
  accepted: { label: "выбрано", className: "bg-ok-soft text-ok" },
  review: { label: "проверьте", className: "bg-warn-soft text-warn" },
  unmatched: { label: "не сопоставлено", className: "bg-muted text-ink-2" },
  rejected: { label: "отклонено", className: "bg-bad-soft text-bad" },
  blocked: { label: "поле занято", className: "bg-bad-soft text-bad" },
};

const monthSources: { value: MappingFieldSetting["monthSource"]; label: string }[] = [
  { value: "current", label: "текущий месяц (с 1-го — новый)" },
  { value: "sheet", label: "из названия листа" },
  { value: "fixed", label: "один месяц" },
];

type Row = { suggestion: MappingSuggestion; accepted: boolean; targetField: string | null; category: string | null };

/** Уверенность: высокая — от порога «выбрано», средняя — от порога «проверьте», иначе низкая. */
function confidenceClass(c: number, p: SheetMappingProposal) {
  return c >= p.acceptConfidence ? "text-ok" : c >= p.reviewConfidence ? "text-warn" : "text-ink-3";
}

/**
 * Предложения ИИ по сопоставлению столбцов: столбец → поле OneBase, уверенность, причина, предупреждения. Каждое можно принять,
 * изменить или отклонить; месяц полей без столбца месяца задаётся здесь же. «Подтвердить» сохраняет привязку на бэкенде (с теми же
 * проверками владельца полей), «В ручной редактор» — переносит поля в обычные настройки для правки.
 */
export function AutoMapPanel({
  proposal,
  busy,
  error,
  onConfirm,
  onToManual,
  onClose,
}: {
  proposal: SheetMappingProposal;
  busy: "confirm" | "manual" | null;
  error: string | null;
  onConfirm: (decisions: MappingDecision[], settings: MappingFieldSetting[]) => void;
  onToManual: (decisions: MappingDecision[], settings: MappingFieldSetting[]) => void;
  onClose: () => void;
}) {
  const [rows, setRows] = useState<Row[]>(() =>
    proposal.suggestions.map((s) => ({
      suggestion: s,
      accepted: s.status === "accepted",
      targetField: s.status === "rejected" || s.status === "blocked" ? null : s.targetField,
      category: s.category,
    })),
  );
  const [settings, setSettings] = useState<Record<string, MappingFieldSetting>>(() =>
    Object.fromEntries(proposal.fieldSettings.map((s) => [s.field, s])),
  );

  const targets = proposal.targets.filter((t) => !t.blocked);
  const byKey = useMemo(() => new Map(proposal.targets.map((t) => [t.key.toLowerCase(), t])), [proposal.targets]);
  const decisions: MappingDecision[] = rows
    .filter((r) => r.accepted && r.targetField)
    .map((r) => ({ sourceColumn: r.suggestion.sourceColumn, targetField: r.targetField!, category: r.category, transformType: r.suggestion.transformType }));

  // Поля плана и P&L без столбца месяца — месяц нужно выбрать.
  const chosenFields = [...new Set(decisions.map((d) => byKey.get(d.targetField.toLowerCase())?.field).filter(Boolean) as string[])];
  const needMonth = chosenFields.filter(
    (f) => proposal.targets.some((t) => t.field === f && t.role === "month") && !decisions.some((d) => byKey.get(d.targetField.toLowerCase())?.field === f && byKey.get(d.targetField.toLowerCase())?.role === "month"),
  );
  const fieldLabel = (f: string) => proposal.targets.find((t) => t.field === f)?.label.split(" · ")[0] ?? f;
  // Период — у полей, где месяцев в листе может быть много: месяц столбцом или в названиях листов. По умолчанию — текущий год.
  const hasMonthColumn = (f: string) => decisions.some((d) => byKey.get(d.targetField.toLowerCase())?.field === f && byKey.get(d.targetField.toLowerCase())?.role === "month");
  const periodFields = chosenFields.filter((f) => hasMonthColumn(f) || settings[f]?.monthSource === "sheet");
  const setPeriod = (f: string, period: PeriodFilter) =>
    setSettings((prev) => ({ ...prev, [f]: { ...(prev[f] ?? { field: f, monthSource: "column", year: null, month: null }), period } }));
  const finalSettings = Object.values(settings).filter((s) => chosenFields.includes(s.field));
  const missingMonth = needMonth.filter((f) => !settings[f] || settings[f].monthSource === "column" || (settings[f].monthSource === "fixed" && (!settings[f].year || !settings[f].month)));

  // Чего не хватает выбранным полям (как проверка на сервере): регион, значение; у P&L — контрагент, статья, сумма. Месяц — выше.
  const roleSet = (f: string) => new Set(decisions.map((d) => byKey.get(d.targetField.toLowerCase())).filter((t) => t?.field === f).map((t) => t!.role));
  const incomplete = chosenFields
    .map((f) => {
      const roles = roleSet(f);
      const kind = proposal.targets.some((t) => t.field === f && t.role === "line") ? "pnl" : proposal.targets.some((t) => t.field === f && t.role === "category") ? "plan" : "profile";
      const missing =
        kind === "pnl"
          ? ["dealer", "line", "amount"].filter((r) => !roles.has(r)).length > 0 && "контрагента, статьи и суммы"
          : !roles.has("region")
            ? "региона"
            : kind === "plan"
              ? !roles.has("value") && !roles.has("category_value") && "плана"
              : !roles.has("value") && "значения";
      return missing ? `«${fieldLabel(f)}»: нет столбца ${missing}` : null;
    })
    .filter((x): x is string => !!x);
  const problems = [...incomplete, ...missingMonth.map((f) => `«${fieldLabel(f)}»: выберите месяц`)];

  const update = (i: number, change: Partial<Row>) => setRows((prev) => prev.map((r, j) => (j === i ? { ...r, ...change } : r)));
  const acceptAll = () =>
    setRows((prev) => prev.map((r) => (r.targetField && (r.suggestion.status === "accepted" || r.suggestion.status === "review") ? { ...r, accepted: true } : r)));
  const counts = proposal.suggestions.reduce<Record<string, number>>((acc, s) => ((acc[s.status] = (acc[s.status] ?? 0) + 1), acc), {});

  return (
    <div className="rounded-xl border border-accent/50 bg-accent-soft/40 p-3">
      <div className="flex flex-wrap items-center gap-2">
        <Sparkles className="size-4 text-accent-strong" />
        <span className="text-sm font-semibold text-ink">Предложение ИИ — лист «{proposal.sheet}»</span>
        <span className="text-xs text-ink-3">
          модель {proposal.model} · выбрано {counts.accepted ?? 0}, проверить {counts.review ?? 0}, без поля {(counts.unmatched ?? 0) + (counts.rejected ?? 0) + (counts.blocked ?? 0)}
        </span>
        <button type="button" onClick={onClose} className="ml-auto rounded-md p-1 text-ink-3 hover:bg-muted hover:text-ink" aria-label="Закрыть предложение">
          <X className="size-4" />
        </button>
      </div>
      <p className="mt-1 text-xs text-ink-3">
        Сразу выбраны соответствия с уверенностью от {Math.round(proposal.acceptConfidence * 100)}%; от {Math.round(proposal.reviewConfidence * 100)}% — отметьте сами после
        проверки. Поле можно поменять в списке. Ничего не записано, пока вы не подтвердите.
      </p>
      {proposal.stale.length > 0 && (
        <div className="mt-2 rounded-lg bg-warn-soft px-3 py-2 text-xs text-warn">
          Сохранённое сопоставление устарело: {proposal.stale.join("; ")}.
        </div>
      )}

      <div className="mt-3 space-y-2">
        {rows.map((r, i) => {
          const s = r.suggestion;
          const view = statusView[s.status];
          const target = r.targetField ? byKey.get(r.targetField.toLowerCase()) : null;
          return (
            <div key={`${s.sourceColumn}-${i}`} className={`rounded-lg border bg-surface p-2.5 ${r.accepted ? "border-accent/50" : "border-line"}`}>
              <div className="grid items-center gap-2 sm:grid-cols-[auto_minmax(0,11rem)_minmax(0,1fr)_auto]">
                <input
                  type="checkbox"
                  aria-label={`Принять: ${s.sourceColumn}`}
                  checked={r.accepted}
                  disabled={!r.targetField}
                  onChange={(e) => update(i, { accepted: e.target.checked })}
                  className="size-4 accent-[var(--color-accent)]"
                />
                <div className="min-w-0 text-sm">
                  <span className="font-semibold text-ink">{s.sourceColumn}</span>
                  <span className="text-ink-2"> · {s.header || "без заголовка"}</span>
                </div>
                <div className="flex min-w-0 gap-2">
                  <select
                    aria-label={`Поле OneBase для ${s.sourceColumn}`}
                    value={r.targetField ?? ""}
                    onChange={(e) => update(i, { targetField: e.target.value || null, accepted: !!e.target.value })}
                    className={select}
                  >
                    <option value="">— не сопоставлять —</option>
                    {targets.map((t) => (
                      <option key={t.key} value={t.key}>
                        {t.label}
                      </option>
                    ))}
                  </select>
                  {target?.role === "category_value" && (
                    <select aria-label="Категория" value={r.category ?? ""} onChange={(e) => update(i, { category: e.target.value || null })} className={`${select} max-w-36`}>
                      <option value="">категория…</option>
                      {proposal.categories.map((c) => (
                        <option key={c} value={c}>
                          {c}
                        </option>
                      ))}
                    </select>
                  )}
                </div>
                <div className="flex items-center gap-2 text-xs sm:justify-end">
                  {s.targetField && s.status !== "rejected" && <span className={`font-semibold ${confidenceClass(s.confidence, proposal)}`}>{Math.round(s.confidence * 100)}%</span>}
                  <span className={`rounded-full px-2 py-0.5 font-medium ${view.className}`}>{view.label}</span>
                </div>
              </div>
              <div className="mt-1 text-xs text-ink-3 sm:pl-6">
                {s.reason}
                {s.transformType !== "none" && ` · преобразование: ${s.transformType === "tons_to_kg" ? "тонны → кг" : s.transformType}`}
              </div>
              {s.warnings.length > 0 && (
                <div className="mt-1 flex items-start gap-1.5 text-xs text-warn sm:pl-6">
                  <AlertTriangle className="mt-0.5 size-3.5 shrink-0" />
                  <span>{s.warnings.join(" · ")}</span>
                </div>
              )}
            </div>
          );
        })}
      </div>

      {needMonth.length > 0 && (
        <div className="mt-3 space-y-2 rounded-lg border border-line bg-surface p-2.5">
          <div className="text-xs font-medium text-ink-2">Месяц полей без столбца месяца</div>
          {needMonth.map((f) => {
            const s = settings[f];
            const set = (change: Partial<MappingFieldSetting>) =>
              setSettings((prev) => ({ ...prev, [f]: { ...(prev[f] ?? { field: f, monthSource: "current", year: null, month: null }), ...change } }));
            return (
              <div key={f} className="grid items-center gap-2 text-xs sm:grid-cols-[minmax(0,10rem)_minmax(0,1fr)_5rem_4rem]">
                <span className="font-medium text-ink">{fieldLabel(f)}</span>
                <select aria-label={`Месяц: ${fieldLabel(f)}`} value={s && s.monthSource !== "column" ? s.monthSource : ""} onChange={(e) => set({ monthSource: e.target.value as MappingFieldSetting["monthSource"] })} className={select}>
                  <option value="">— выберите —</option>
                  {monthSources.map((m) => (
                    <option key={m.value} value={m.value}>
                      {m.label}
                    </option>
                  ))}
                </select>
                {s?.monthSource === "fixed" && (
                  <>
                    <input type="number" aria-label="Год" placeholder="год" value={s.year ?? ""} onChange={(e) => set({ year: Number(e.target.value) || null })} className={select} />
                    <input type="number" aria-label="Месяц" placeholder="мес." min={1} max={12} value={s.month ?? ""} onChange={(e) => set({ month: Number(e.target.value) || null })} className={select} />
                  </>
                )}
              </div>
            );
          })}
        </div>
      )}

      {periodFields.length > 0 && (
        <div className="mt-3 space-y-3 rounded-lg border border-line bg-surface p-2.5">
          {periodFields.map((f) => (
            <div key={f}>
              <div className="mb-1.5 text-xs font-medium text-ink">{fieldLabel(f)}</div>
              <PeriodSelect value={settings[f]?.period ?? { mode: "year" }} onChange={(p) => setPeriod(f, p)} />
            </div>
          ))}
        </div>
      )}

      {proposal.notes.length > 0 && <div className="mt-2 text-xs text-ink-3">ИИ: {proposal.notes.join(" ")}</div>}
      {problems.length > 0 && (
        <div className="mt-2 flex items-start gap-1.5 text-xs text-warn">
          <AlertTriangle className="mt-0.5 size-3.5 shrink-0" />
          <span>Чтобы сохранить: {problems.join("; ")} — или снимите отметку с этого поля.</span>
        </div>
      )}
      {proposal.fieldErrors.length > 0 && <div className="mt-2 text-xs text-warn">{proposal.fieldErrors.join(" ")}</div>}
      {error && <div className="mt-2 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</div>}

      <div className="mt-3 flex flex-wrap justify-end gap-2">
        <button
          type="button"
          onClick={acceptAll}
          disabled={!!busy || rows.every((r) => r.accepted || !r.targetField || !(r.suggestion.status === "accepted" || r.suggestion.status === "review"))}
          className="mr-auto inline-flex h-9 items-center gap-2 rounded-lg border border-line px-3 text-sm text-ink hover:bg-muted disabled:opacity-50"
        >
          Принять все предложения ИИ
        </button>
        <button
          type="button"
          onClick={() => onToManual(decisions, finalSettings)}
          disabled={!!busy || decisions.length === 0 || problems.length > 0}
          className="inline-flex h-9 items-center gap-2 rounded-lg border border-line px-3 text-sm text-ink hover:bg-muted disabled:opacity-50"
        >
          {busy === "manual" && <Loader2 className="size-4 animate-spin" />}
          В ручной редактор
        </button>
        <button
          type="button"
          onClick={() => onConfirm(decisions, finalSettings)}
          disabled={!!busy || decisions.length === 0 || problems.length > 0}
          title={problems.length > 0 ? problems.join("; ") : undefined}
          className="inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-medium text-white hover:bg-accent-strong disabled:opacity-50"
        >
          {busy === "confirm" && <Loader2 className="size-4 animate-spin" />}
          Подтвердить и сохранить
        </button>
      </div>
    </div>
  );
}
