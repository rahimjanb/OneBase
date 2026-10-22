"use client";

import { useMemo, useState } from "react";
import { AlertTriangle, Check, FileSpreadsheet, Info, Loader2, RotateCcw, ShieldCheck, X } from "lucide-react";
import { useT } from "@/i18n/client";
import type { T } from "@/i18n/core";
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
  type PnlCheck,
  type PnlMonthTotal,
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
  const t = useT();
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
    <section className="rounded-xl border border-line bg-surface" aria-label={t("План изменений: {title}", { title: t(set.title) })}>
      <header className="flex flex-wrap items-start gap-3 border-b border-line px-4 py-3">
        <span className="grid size-9 shrink-0 place-items-center rounded-full bg-accent-soft text-accent-strong">
          <FileSpreadsheet className="size-4" />
        </span>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="font-semibold text-ink">{t(set.title)}</h3>
            <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${status.tone}`}>{t(status.label)}</span>
          </div>
          <p className="mt-0.5 text-xs text-ink-3">
            {t("Подготовлен {date}", { date: when(set.createdAt) })}
            {set.userName ? ` · ${set.userName}` : ""}
            {set.mode === "replace" ? ` · ${t("полная замена месяца")}` : ""}
            {set.appliedAt ? ` · ${t("применён {date}", { date: when(set.appliedAt) })}${set.appliedBy ? ` (${set.appliedBy})` : ""}` : ""}
            {set.rolledBackAt ? ` · ${t("откачен {date}", { date: when(set.rolledBackAt) })}${set.rolledBackBy ? ` (${set.rolledBackBy})` : ""}` : ""}
          </p>
        </div>
      </header>

      <div className="space-y-3 px-4 py-3">
        <div className="flex flex-wrap gap-2 text-xs">
          <Count label={t("Создать")} value={counts.create} tone="bg-ok-soft text-ok" />
          <Count label={t("Изменить")} value={counts.update} tone="bg-accent-soft text-accent-strong" />
          <Count label={t("Удалить")} value={counts.delete} tone="bg-bad-soft text-bad" />
          <Count label={t("Без изменений")} value={counts.unchanged} tone="bg-muted text-ink-2" />
          <Count label={t("Конфликты")} value={counts.conflict} tone="bg-warn-soft text-warn" />
          {set.summary.skippedEmpty > 0 && (
            <span className="rounded-full bg-muted px-2.5 py-1 text-ink-3">{t("пустых ячеек: {n}", { n: set.summary.skippedEmpty })}</span>
          )}
        </div>

        {set.summary.previous && (
          <Banner tone="info">
            {t("Новая версия отчёта по сравнению с операцией от {date}", { date: when(set.summary.previous.appliedAt) })}
            {set.summary.previous.fileName ? ` («${set.summary.previous.fileName}»)` : ""}
            {t(": исправлено строк — {n}.", { n: set.summary.previous.correctedRows })}
          </Banner>
        )}
        {/* Пояснения, предупреждения и ошибки плана пишет сервер — на языке интерфейса (заметку администратора — как есть, если её нет в словаре). */}
        {set.summary.notes.map((n) => (
          <Banner key={n} tone="info">
            {t(n)}
          </Banner>
        ))}
        {set.summary.warnings.map((w) => (
          <Banner key={w} tone="warn">
            {t(w)}
          </Banner>
        ))}
        {set.error && <Banner tone="bad">{t(set.error)}</Banner>}

        {set.summary.pnl && set.summary.pnl.months.length > 0 && !set.verification && (
          <div>
            <h4 className="text-xs font-semibold uppercase tracking-wide text-ink-3">{t("Как изменится P&L (расчёт OneBase, все дилеры)")}</h4>
            {set.summary.pnl.months.map((m) => (
              <PnlTotalsTable key={`${m.year}-${m.month}`} total={m} />
            ))}
          </div>
        )}
        {set.summary.pnl && set.summary.pnl.checks.length > 0 && <PnlChecksTable checks={set.summary.pnl.checks} total={set.summary.pnl.checkedValues} />}

        {set.summary.impact.length > 0 && !set.verification && (
          <div>
            <h4 className="text-xs font-semibold uppercase tracking-wide text-ink-3">{t("Как изменятся показатели")}</h4>
            {set.summary.impact.map((i) => (
              <ImpactTable key={i.period} impact={i} />
            ))}
          </div>
        )}

        {set.verification && (
          <div className={`rounded-lg px-3 py-2.5 text-sm ${set.verification.mismatches === 0 ? "bg-ok-soft" : "bg-bad-soft"}`}>
            <div className="flex items-center gap-2 font-medium text-ink">
              <ShieldCheck className={`size-4 ${set.verification.mismatches === 0 ? "text-ok" : "text-bad"}`} />
              {t("Проверка {date}: строк {checked}, расхождений {mismatches}", {
                date: when(set.verification.checkedAt),
                checked: set.verification.checked,
                mismatches: set.verification.mismatches,
              })}
            </div>
            {set.verification.problems.length > 0 && (
              <ul className="mt-1.5 list-disc space-y-0.5 pl-5 text-ink-2">
                {set.verification.problems.map((p) => (
                  <li key={p.label}>{t("{label}: ожидалось {expected}, сейчас {actual}", { label: t(p.label), expected: valueText(p.expected), actual: valueText(p.actual) })}</li>
                ))}
              </ul>
            )}
            {set.verification.kpi.map((i) => (
              <ImpactTable key={i.period} impact={i} after />
            ))}
            {set.verification.pnl?.map((m) => (
              <PnlTotalsTable key={`${m.year}-${m.month}`} total={m} verified />
            ))}
            {set.verification.notes.map((n) => (
              <p key={n} className="mt-1 text-xs text-ink-2">
                {t(n)}
              </p>
            ))}
          </div>
        )}

        <div>
          <div role="tablist" className="flex flex-wrap gap-1 border-b border-line">
            {(
              [
                ["changes", t("Изменения ({n})", { n: byTab.changes.length })],
                ["conflicts", t("Конфликты ({n})", { n: byTab.conflicts.length })],
                ["unchanged", t("Без изменений ({n})", { n: byTab.unchanged.length })],
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
            <p className="py-4 text-center text-sm text-ink-3">{t("Строк нет")}</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="mt-1 w-full text-sm">
                <thead>
                  <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                    <th className="py-2 pr-3 font-semibold">{t("Действие")}</th>
                    <th className="py-2 pr-3 font-semibold">{t("Запись")}</th>
                    <th className="py-2 pr-3 text-right font-semibold">{t("Было")}</th>
                    <th className="py-2 pr-3 text-right font-semibold">{t("Стало")}</th>
                    <th className="py-2 pr-3 font-semibold">{t("Источник и комментарий")}</th>
                  </tr>
                </thead>
                <tbody>
                  {shown.map((i) => (
                    <tr key={i.id} className="border-b border-line/60 align-top last:border-0">
                      <td className="py-2 pr-3">
                        <span className={`whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium ${actionView[i.action].tone}`}>{t(actionView[i.action].label)}</span>
                        {itemStatusLabel[i.status] && <div className="mt-1 text-[11px] text-ink-3">{t(itemStatusLabel[i.status])}</div>}
                      </td>
                      <td className="py-2 pr-3 text-ink">{t(i.label)}</td>
                      <td className="whitespace-nowrap py-2 pr-3 text-right tabular-nums text-ink-2">{valueText(i.before)}</td>
                      <td className="whitespace-nowrap py-2 pr-3 text-right font-medium tabular-nums text-ink">{valueText(i.after)}</td>
                      <td className="py-2 pr-3 text-xs text-ink-3">
                        {i.sourceRef && <div>{i.sourceRef}</div>}
                        {i.message && <div className={i.action === "Conflict" ? "text-warn" : ""}>{t(i.message)}</div>}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {rows.length > pageSize && (
                <button type="button" onClick={() => setAll(!all)} className="mt-2 text-sm font-medium text-accent-strong hover:underline">
                  {all ? t("Свернуть") : t("Показать все {n}", { n: rows.length })}
                </button>
              )}
            </div>
          )}
        </div>
      </div>

      {(set.canApply || set.canCancel || set.canRollback) && (
        <footer className="flex flex-wrap items-center gap-2 border-t border-line px-4 py-3">
          {!canChange && <p className="flex-1 text-xs text-ink-3">{t("Подтвердить или откатить может пользователь с правом «AI Data Manager».")}</p>}
          {canChange && set.canApply && (
            <button
              type="button"
              disabled={locked}
              onClick={() => onApply(set)}
              className="inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50 max-lg:h-11"
            >
              {acting ? <Loader2 className="size-4 animate-spin" /> : <Check className="size-4" />}
              {t("Подтвердить и применить ({n})", { n: applicable })}
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
              {t("Отменить")}
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
              {t("Откатить")}
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

const pnlRows: [string, string, boolean?][] = [
  ["sales", "Продажи"],
  ["cost", "Себестоимость"],
  ["gross", "Валовая прибыль"],
  ["margin", "Маржа", true],
  ["sm", "Отдел продаж и маркетинга"],
  ["ship", "Доставка и склад"],
  ["admin", "Административные"],
  ["other", "Прочие расходы"],
  ["ebit", "EBIT"],
];

const sum = (v: number | null | undefined, t: T) => (v == null ? "—" : t("{n} сум", { n: Math.round(v).toLocaleString("ru-RU") }));

/** P&L месяца по всем дилерам: было → станет (план изменений) или ожидалось / в расчёте (проверка после применения). */
function PnlTotalsTable({ total, verified = false }: { total: PnlMonthTotal; verified?: boolean }) {
  const t = useT();
  const rows = pnlRows.filter(([key]) => total.before[key] != null || total.after[key] != null);
  return (
    <div className="mt-2 overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
            <th className="py-1.5 pr-3 font-semibold">
              {String(total.month).padStart(2, "0")}.{total.year}
            </th>
            <th className="py-1.5 pr-3 text-right font-semibold">{verified ? t("Ожидалось") : t("Было")}</th>
            <th className="py-1.5 pr-3 text-right font-semibold">{verified ? t("В расчёте OneBase") : t("Станет")}</th>
          </tr>
        </thead>
        <tbody>
          {rows.map(([key, label, isPct]) => {
            const before = total.before[key];
            const after = total.after[key];
            const changed = (before ?? 0) !== (after ?? 0);
            return (
              <tr key={key} className={`border-b border-line/60 last:border-0 ${key === "ebit" ? "font-medium" : ""}`}>
                <td className="py-1.5 pr-3 text-ink">{t(label)}</td>
                <td className="whitespace-nowrap py-1.5 pr-3 text-right tabular-nums text-ink-2">{isPct ? pct(before) : sum(before, t)}</td>
                <td className={`whitespace-nowrap py-1.5 pr-3 text-right tabular-nums ${changed ? (verified ? "text-bad" : "text-ink") : "text-ink-2"} ${changed && !verified ? "font-medium" : ""}`}>
                  {isPct ? pct(after) : sum(after, t)}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

/** Расчётные строки из отчёта (валовая, EBIT…), которые разошлись с расчётом OneBase по исходным строкам. */
function PnlChecksTable({ checks, total }: { checks: PnlCheck[]; total: number }) {
  const t = useT();
  const [all, setAll] = useState(false);
  const shown = all ? checks : checks.slice(0, 10);
  const value = (c: PnlCheck, v: number | null) => (v == null ? "—" : c.line === "margin" || c.line === "ros" ? pct(v) : sum(v, t));
  return (
    <div>
      <h4 className="text-xs font-semibold uppercase tracking-wide text-ink-3">
        {t("Сверка расчётных строк отчёта: расхождений {n} из {total}", { n: checks.length, total })}
      </h4>
      <div className="mt-1 overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
              <th className="py-1.5 pr-3 font-semibold">{t("Дилер · строка")}</th>
              <th className="py-1.5 pr-3 text-right font-semibold">{t("В отчёте")}</th>
              <th className="py-1.5 pr-3 text-right font-semibold">{t("Расчёт OneBase")}</th>
              <th className="py-1.5 pr-3 font-semibold">{t("Ячейка")}</th>
            </tr>
          </thead>
          <tbody>
            {shown.map((c, i) => (
              <tr key={`${c.dealer}-${c.line}-${c.month}-${i}`} className="border-b border-line/60 last:border-0">
                <td className="py-1.5 pr-3 text-ink">
                  {c.dealer} · {t(c.label)} · {String(c.month).padStart(2, "0")}.{c.year}
                </td>
                <td className="whitespace-nowrap py-1.5 pr-3 text-right tabular-nums">{value(c, c.report)}</td>
                <td className="whitespace-nowrap py-1.5 pr-3 text-right tabular-nums text-warn">{value(c, c.oneBase)}</td>
                <td className="py-1.5 pr-3 text-xs text-ink-3">{c.sourceRef}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {checks.length > 10 && (
        <button type="button" onClick={() => setAll(!all)} className="mt-1 text-sm font-medium text-accent-strong hover:underline">
          {all ? t("Свернуть") : t("Показать все {n}", { n: checks.length })}
        </button>
      )}
    </div>
  );
}

/** Показатели до/после: план, факт, выполнение — по республике и регионам плана. */
function ImpactTable({ impact, after = false }: { impact: Impact; after?: boolean }) {
  const t = useT();
  const rows = [...(impact.republic ? [impact.republic] : []), ...impact.regions];
  return (
    <div className="mt-2">
      {rows.length > 0 && (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                <th className="py-1.5 pr-3 font-semibold">{impact.period}</th>
                <th className="py-1.5 pr-3 text-right font-semibold">{after ? t("План в расчёте") : t("План: было → станет")}</th>
                <th className="py-1.5 pr-3 text-right font-semibold">{t("Факт")}</th>
                <th className="py-1.5 pr-3 text-right font-semibold">{after ? t("Выполнение") : t("Выполнение: было → станет")}</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r, i) => (
                <tr key={`${r.name}-${i}`} className={`border-b border-line/60 last:border-0 ${i === 0 && impact.republic ? "font-medium" : ""}`}>
                  {/* Строка республики — подпись сервера («Республика»); регионы — данные, как есть. */}
                  <td className="py-1.5 pr-3 text-ink">{i === 0 && impact.republic ? t(r.name) : r.name}</td>
                  <td className="whitespace-nowrap py-1.5 pr-3 text-right tabular-nums">
                    {after ? kg(r.planAfter, t) : `${kg(r.planBefore, t)} → ${kg(r.planAfter, t)}`}
                  </td>
                  <td className="whitespace-nowrap py-1.5 pr-3 text-right tabular-nums text-ink-2">{kg(r.factKg, t)}</td>
                  <td className="whitespace-nowrap py-1.5 pr-3 text-right tabular-nums">
                    {after ? pct(r.executionAfter) : `${pct(r.executionBefore)} → ${pct(r.executionAfter)}`}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <p className="mt-1 text-xs text-ink-3">{t(impact.note)}</p>
    </div>
  );
}
