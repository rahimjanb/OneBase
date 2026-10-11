"use client";

import { useMemo, useState } from "react";
import { AlertTriangle, Check, FileSpreadsheet, Info, Loader2, RotateCcw, ShieldCheck, X } from "lucide-react";
import {
  actionView,
  itemStatusLabel,
  kg,
  pct,
  statusView,
  valueText,
  when,
  type ChangeAction,
  type ChangeSet,
  type Impact,
} from "@/lib/data-manager";

type Props = {
  set: ChangeSet;
  canChange: boolean;
  /** Идёт действие с этим планом (применение, откат, отмена). */
  acting: boolean;
  /** Идёт любое действие в чате — кнопки недоступны. */
  locked: boolean;
  onApply: (set: ChangeSet) => void;
  onCancel: (set: ChangeSet) => void;
  onRollback: (set: ChangeSet) => void;
};

type Tab = "changes" | "conflicts" | "unchanged";

const changes: ChangeAction[] = ["Create", "Update", "Delete"];
const pageSize = 30;

/** План изменений: строки «было → стало», конфликты, предупреждения, влияние на KPI, проверка, подтверждение и откат. */
export function ChangeSetCard({ set, canChange, acting, locked, onApply, onCancel, onRollback }: Props) {
  const counts = set.summary.counts;
  const byTab = useMemo(
    () => ({
      changes: set.items.filter((i) => changes.includes(i.action)),
      conflicts: set.items.filter((i) => i.action === "Conflict"),
      unchanged: set.items.filter((i) => i.action === "Unchanged"),
    }),
    [set.items],
  );
  const [tab, setTab] = useState<Tab>(byTab.changes.length > 0 ? "changes" : byTab.conflicts.length > 0 ? "conflicts" : "unchanged");
  const [all, setAll] = useState(false);
  const rows = byTab[tab];
  const shown = all ? rows : rows.slice(0, pageSize);
  const status = statusView[set.status];
  const applicable = counts.create + counts.update + counts.delete;

  return (
    <section className="rounded-xl border border-line bg-surface" aria-label={`План изменений: ${set.title}`}>
      <header className="flex flex-wrap items-start gap-3 border-b border-line px-4 py-3">
        <span className="grid size-9 shrink-0 place-items-center rounded-full bg-accent-soft text-accent-strong">
          <FileSpreadsheet className="size-4" />
        </span>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="font-semibold text-ink">{set.title}</h3>
            <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${status.tone}`}>{status.label}</span>
          </div>
          <p className="mt-0.5 text-xs text-ink-3">
            Подготовлен {when(set.createdAt)}
            {set.userName ? ` · ${set.userName}` : ""}
            {set.mode === "replace" ? " · полная замена месяца" : ""}
            {set.appliedAt ? ` · применён ${when(set.appliedAt)}${set.appliedBy ? ` (${set.appliedBy})` : ""}` : ""}
            {set.rolledBackAt ? ` · откачен ${when(set.rolledBackAt)}${set.rolledBackBy ? ` (${set.rolledBackBy})` : ""}` : ""}
          </p>
        </div>
      </header>

      <div className="space-y-3 px-4 py-3">
        <div className="flex flex-wrap gap-2 text-xs">
          <Count label="Создать" value={counts.create} tone="bg-ok-soft text-ok" />
          <Count label="Изменить" value={counts.update} tone="bg-accent-soft text-accent-strong" />
          <Count label="Удалить" value={counts.delete} tone="bg-bad-soft text-bad" />
          <Count label="Без изменений" value={counts.unchanged} tone="bg-muted text-ink-2" />
          <Count label="Конфликты" value={counts.conflict} tone="bg-warn-soft text-warn" />
          {set.summary.skippedEmpty > 0 && <span className="rounded-full bg-muted px-2.5 py-1 text-ink-3">пустых ячеек: {set.summary.skippedEmpty}</span>}
        </div>

        {set.summary.previous && (
          <Banner tone="info">
            Новая версия отчёта по сравнению с операцией от {when(set.summary.previous.appliedAt)}
            {set.summary.previous.fileName ? ` («${set.summary.previous.fileName}»)` : ""}: исправлено строк — {set.summary.previous.correctedRows}.
          </Banner>
        )}
        {set.summary.notes.map((n) => (
          <Banner key={n} tone="info">
            {n}
          </Banner>
        ))}
        {set.summary.warnings.map((w) => (
          <Banner key={w} tone="warn">
            {w}
          </Banner>
        ))}
        {set.error && <Banner tone="bad">{set.error}</Banner>}

        {set.summary.impact.length > 0 && !set.verification && (
          <div>
            <h4 className="text-xs font-semibold uppercase tracking-wide text-ink-3">Как изменятся показатели</h4>
            {set.summary.impact.map((i) => (
              <ImpactTable key={i.period} impact={i} />
            ))}
          </div>
        )}

        {set.verification && (
          <div className={`rounded-lg px-3 py-2.5 text-sm ${set.verification.mismatches === 0 ? "bg-ok-soft" : "bg-bad-soft"}`}>
            <div className="flex items-center gap-2 font-medium text-ink">
              <ShieldCheck className={`size-4 ${set.verification.mismatches === 0 ? "text-ok" : "text-bad"}`} />
              Проверка {when(set.verification.checkedAt)}: строк {set.verification.checked}, расхождений {set.verification.mismatches}
            </div>
            {set.verification.problems.length > 0 && (
              <ul className="mt-1.5 list-disc space-y-0.5 pl-5 text-ink-2">
                {set.verification.problems.map((p) => (
                  <li key={p.label}>
                    {p.label}: ожидалось {valueText(p.expected)}, сейчас {valueText(p.actual)}
                  </li>
                ))}
              </ul>
            )}
            {set.verification.kpi.map((i) => (
              <ImpactTable key={i.period} impact={i} after />
            ))}
            {set.verification.notes.map((n) => (
              <p key={n} className="mt-1 text-xs text-ink-2">
                {n}
              </p>
            ))}
          </div>
        )}

        <div>
          <div role="tablist" className="flex flex-wrap gap-1 border-b border-line">
            {(
              [
                ["changes", `Изменения (${byTab.changes.length})`],
                ["conflicts", `Конфликты (${byTab.conflicts.length})`],
                ["unchanged", `Без изменений (${byTab.unchanged.length})`],
              ] as [Tab, string][]
            ).map(([key, label]) => (
              <button
                key={key}
                type="button"
                role="tab"
                aria-selected={tab === key}
                onClick={() => {
                  setTab(key);
                  setAll(false);
                }}
                className={`-mb-px border-b-2 px-3 py-2 text-sm font-medium ${tab === key ? "border-accent text-ink" : "border-transparent text-ink-3 hover:text-ink"}`}
              >
                {label}
              </button>
            ))}
          </div>
          {rows.length === 0 ? (
            <p className="py-4 text-center text-sm text-ink-3">Строк нет</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="mt-1 w-full text-sm">
                <thead>
                  <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                    <th className="py-2 pr-3 font-semibold">Действие</th>
                    <th className="py-2 pr-3 font-semibold">Запись</th>
                    <th className="py-2 pr-3 text-right font-semibold">Было</th>
                    <th className="py-2 pr-3 text-right font-semibold">Стало</th>
                    <th className="py-2 pr-3 font-semibold">Источник и комментарий</th>
                  </tr>
                </thead>
                <tbody>
                  {shown.map((i) => (
                    <tr key={i.id} className="border-b border-line/60 align-top last:border-0">
                      <td className="py-2 pr-3">
                        <span className={`whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium ${actionView[i.action].tone}`}>{actionView[i.action].label}</span>
                        {itemStatusLabel[i.status] && <div className="mt-1 text-[11px] text-ink-3">{itemStatusLabel[i.status]}</div>}
                      </td>
                      <td className="py-2 pr-3 text-ink">{i.label}</td>
                      <td className="whitespace-nowrap py-2 pr-3 text-right tabular-nums text-ink-2">{valueText(i.before)}</td>
                      <td className="whitespace-nowrap py-2 pr-3 text-right font-medium tabular-nums text-ink">{valueText(i.after)}</td>
                      <td className="py-2 pr-3 text-xs text-ink-3">
                        {i.sourceRef && <div>{i.sourceRef}</div>}
                        {i.message && <div className={i.action === "Conflict" ? "text-warn" : ""}>{i.message}</div>}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {rows.length > pageSize && (
                <button type="button" onClick={() => setAll(!all)} className="mt-2 text-sm font-medium text-accent-strong hover:underline">
                  {all ? "Свернуть" : `Показать все ${rows.length}`}
                </button>
              )}
            </div>
          )}
        </div>
      </div>

      {(set.canApply || set.canCancel || set.canRollback) && (
        <footer className="flex flex-wrap items-center gap-2 border-t border-line px-4 py-3">
          {!canChange && <p className="flex-1 text-xs text-ink-3">Подтвердить или откатить может пользователь с правом «AI Data Manager».</p>}
          {canChange && set.canApply && (
            <button
              type="button"
              disabled={locked}
              onClick={() => onApply(set)}
              className="inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50 max-lg:h-11"
            >
              {acting ? <Loader2 className="size-4 animate-spin" /> : <Check className="size-4" />}
              Подтвердить и применить ({applicable})
            </button>
          )}
          {canChange && set.canCancel && (
            <button
              type="button"
              disabled={locked}
              onClick={() => onCancel(set)}
              className="inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted disabled:opacity-50 max-lg:h-11"
            >
              <X className="size-4" />
              Отменить
            </button>
          )}
          {canChange && set.canRollback && (
            <button
              type="button"
              disabled={locked}
              onClick={() => onRollback(set)}
              className="inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-4 text-sm font-medium text-bad hover:bg-bad-soft disabled:opacity-50 max-lg:h-11"
            >
              {acting ? <Loader2 className="size-4 animate-spin" /> : <RotateCcw className="size-4" />}
              Откатить
            </button>
          )}
        </footer>
      )}
    </section>
  );
}

function Count({ label, value, tone }: { label: string; value: number; tone: string }) {
  return (
    <span className={`rounded-full px-2.5 py-1 font-medium ${value === 0 ? "bg-muted text-ink-3" : tone}`}>
      {label}: {value}
    </span>
  );
}

function Banner({ tone, children }: { tone: "info" | "warn" | "bad"; children: React.ReactNode }) {
  const style = tone === "warn" ? "bg-warn-soft" : tone === "bad" ? "bg-bad-soft" : "bg-muted/70";
  const Icon = tone === "info" ? Info : AlertTriangle;
  const iconTone = tone === "warn" ? "text-warn" : tone === "bad" ? "text-bad" : "text-ink-3";
  return (
    <div className={`flex items-start gap-2 rounded-lg px-3 py-2 text-sm text-ink-2 ${style}`}>
      <Icon className={`mt-0.5 size-4 shrink-0 ${iconTone}`} />
      <span>{children}</span>
    </div>
  );
}

/** Показатели до/после: план, факт, выполнение — по республике и регионам плана. */
function ImpactTable({ impact, after = false }: { impact: Impact; after?: boolean }) {
  const rows = [...(impact.republic ? [impact.republic] : []), ...impact.regions];
  return (
    <div className="mt-2">
      {rows.length > 0 && (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                <th className="py-1.5 pr-3 font-semibold">{impact.period}</th>
                <th className="py-1.5 pr-3 text-right font-semibold">{after ? "План в расчёте" : "План: было → станет"}</th>
                <th className="py-1.5 pr-3 text-right font-semibold">Факт</th>
                <th className="py-1.5 pr-3 text-right font-semibold">{after ? "Выполнение" : "Выполнение: было → станет"}</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r, i) => (
                <tr key={`${r.name}-${i}`} className={`border-b border-line/60 last:border-0 ${i === 0 && impact.republic ? "font-medium" : ""}`}>
                  <td className="py-1.5 pr-3 text-ink">{r.name}</td>
                  <td className="whitespace-nowrap py-1.5 pr-3 text-right tabular-nums">
                    {after ? kg(r.planAfter) : `${kg(r.planBefore)} → ${kg(r.planAfter)}`}
                  </td>
                  <td className="whitespace-nowrap py-1.5 pr-3 text-right tabular-nums text-ink-2">{kg(r.factKg)}</td>
                  <td className="whitespace-nowrap py-1.5 pr-3 text-right tabular-nums">
                    {after ? pct(r.executionAfter) : `${pct(r.executionBefore)} → ${pct(r.executionAfter)}`}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <p className="mt-1 text-xs text-ink-3">{impact.note}</p>
    </div>
  );
}
