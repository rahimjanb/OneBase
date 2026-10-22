"use client";

import { useEffect, useMemo, useState } from "react";
import { Loader2, Sparkles } from "lucide-react";
import { Modal } from "@/components/files/Modal";
import { AutoMapPanel } from "@/components/settings/AutoMapPanel";
import { PeriodSelect } from "@/components/settings/PeriodSelect";
import { useT } from "@/i18n/client";
import {
  AllSheets,
  dataSourcesApi,
  type BalanceMapping,
  type BindingView,
  type ConnectionView,
  type DataSourceField,
  type FieldBinding,
  type LedgerMapping,
  type MappingDecision,
  type MappingFieldSetting,
  type PlanMapping,
  type PnlMapping,
  type Schedule,
  type SectionView,
  type SheetMappingProposal,
  type SheetPreview,
  type TermsMapping,
} from "@/lib/data-sources";

const input = "h-9 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink focus:border-accent/60 focus:outline-none";
const label = "mb-1 block text-xs font-medium text-ink-2";
const intervals = [15, 30, 60, 180, 360, 720, 1440];

type Column = { value: string; label: string; header: string };

/** Столбцы листа: значение — заголовок (если он есть и не повторяется), иначе буква; подпись — «B · Регион». */
function columnsOf(preview: SheetPreview | null, headerRow: number): Column[] {
  if (!preview) return [];
  const header = preview.cells[headerRow - 1] ?? [];
  const names = header.map((h) => h.trim());
  return preview.letters.map((letter, i) => {
    const name = names[i] ?? "";
    const unique = name.length > 0 && names.filter((n) => n.toLowerCase() === name.toLowerCase()).length === 1;
    return { value: unique ? name : letter, label: `${letter} · ${name || "—"}`, header: name };
  });
}

/**
 * Снимок заголовков для сохранения: столбец сопоставления → заголовок в листе предпросмотра. Синхронизация сверяет с ним лист: переименовали
 * или переставили столбцы — поле не пишется, а не читает не тот столбец. Столбец не из этого листа — прежний снимок остаётся.
 */
function withHeaders(fields: FieldBinding[], columns: Column[]): FieldBinding[] {
  if (columns.length === 0) return fields;
  return fields.map((f) => {
    const refs = [
      f.plan?.regionColumn, f.plan?.categoryColumn, f.plan?.valueColumn, f.plan?.monthColumn, f.plan?.yearColumn, ...(f.plan?.valueColumns?.map((c) => c.column) ?? []),
      f.profile?.regionColumn, f.profile?.valueColumn, f.pnl?.dealerColumn, f.pnl?.lineColumn, f.pnl?.valueColumn, f.pnl?.monthColumn, f.pnl?.yearColumn,
      f.ledger?.dateColumn, f.ledger?.dealerColumn, f.ledger?.amountColumn, f.ledger?.kindColumn, f.ledger?.categoryColumn, f.ledger?.commentColumn,
      f.terms?.dealerColumn, f.terms?.normColumn, f.terms?.openingColumn, f.balance?.dealerColumn, f.balance?.amountColumn,
    ].filter((r): r is string => !!r);
    const headers: Record<string, string> = {};
    for (const ref of refs) {
      const column = columns.find((c) => c.value === ref);
      if (!column) return f;
      headers[ref] = column.header;
    }
    return { ...f, headers };
  });
}

/** Столбец по словам в заголовке — чтобы сопоставление было заполнено сразу. */
function guess(columns: Column[], pattern: RegExp): string | null {
  return columns.find((c) => pattern.test(c.label))?.value ?? null;
}

function defaultField(field: DataSourceField, columns: Column[]): FieldBinding {
  const region = guess(columns, /регион|дилер|филиал|склад|region/i) ?? columns[0]?.value ?? "A";
  const month = guess(columns, /месяц|период|month|дата/i);
  if (field.kind === "profile") {
    return { field: field.key, profile: { regionColumn: guess(columns, /регион|филиал|region/i) ?? region, valueColumn: guess(columns, field.key === "dealer" ? /дилер|dealer/i : /свр|супервайз/i) ?? "" }, replace: false };
  }
  const dealer = guess(columns, /дилер|диллер|контрагент|subkategor|dealer/i) ?? region;
  if (field.kind === "ledger") {
    const ledger: LedgerMapping = {
      dateColumn: guess(columns, /^.{0,4}(sana|дата|date)/i) ?? month ?? "", dealerColumn: dealer, amountColumn: guess(columns, /сумм|summa|amount/i) ?? "",
      kindColumn: guess(columns, field.key === "dealer_payments" ? /koshelok|кошел|касса|wallet/i : /стать|kind/i), categoryColumn: null, categories: null,
      commentColumn: guess(columns, /коммент|izoh|comment|примеч/i), dateFrom: null,
    };
    return { field: field.key, ledger, replace: true };
  }
  if (field.kind === "terms") {
    return { field: field.key, terms: { dealerColumn: dealer, normColumn: guess(columns, /норм|norma|срок|kun/i), openingColumn: null, openingDate: null }, replace: true };
  }
  if (field.kind === "balance") {
    return { field: field.key, balance: { dealerColumn: dealer, amountColumn: guess(columns, /остаток|сальдо|долг|balance/i) ?? "", dateCell: null, date: null }, replace: true };
  }
  if (field.kind === "pnl") {
    const pnl: PnlMapping = {
      sheet: null, headerRow: 1, dealerColumn: guess(columns, /дилер|контрагент|dealer/i) ?? region, valueColumns: null,
      lineColumn: guess(columns, /стать|строк|line|показател/i), valueColumn: guess(columns, /сумм|amount|значен/i),
      yearColumn: guess(columns, /^.{0,4}год|year/i), monthColumn: month, year: month ? null : new Date().getFullYear(), month: null, multiplier: 1,
    };
    return { field: field.key, pnl, replace: true, period: { mode: "year" } };
  }
  const plan: PlanMapping = {
    regionColumn: region, categoryColumn: guess(columns, /категор|category/i), valueColumn: guess(columns, /план|plan|кг|kg/i), valueColumns: null,
    monthColumn: month, yearColumn: guess(columns, /^.{0,4}год|year/i), year: new Date().getFullYear(), month: month ? null : new Date().getMonth() + 1, multiplier: 1,
  };
  return { field: field.key, plan, replace: true, period: { mode: "year" } };
}

function ColumnSelect({ value, columns, onChange, empty }: { value: string | null; columns: Column[]; onChange: (v: string | null) => void; empty?: string }) {
  const t = useT();
  const known = value == null || columns.some((c) => c.value === value);
  return (
    <select value={value ?? ""} onChange={(e) => onChange(e.target.value || null)} className={input}>
      <option value="">{empty ?? t("— выберите столбец —")}</option>
      {!known && <option value={value!}>{value}</option>}
      {columns.map((c) => (
        <option key={c.value + c.label} value={c.value}>
          {c.label}
        </option>
      ))}
    </select>
  );
}

/**
 * Привязка раздела к листу Google Sheets: таблица и лист (с предпросмотром), поля раздела, которые обновлять, — у каждого
 * своё сопоставление столбцов, — и расписание. Обновляются только отмеченные поля.
 */
export function BindingEditor({
  section,
  binding,
  connections,
  taken,
  onClose,
  onSaved,
}: {
  section: SectionView;
  binding: BindingView | null;
  connections: ConnectionView[];
  taken: Record<string, string>;
  onClose: () => void;
  onSaved: () => void;
}) {
  const t = useT();
  const [connectionId, setConnectionId] = useState(binding?.connectionId ?? connections[0]?.id ?? "");
  const connection = connections.find((c) => c.id === connectionId);
  // Листы привязки с одинаковой разметкой; «*» — все листы таблицы (и новые). Предпросмотр — одного из них.
  const [sheets, setSheets] = useState<string[]>(binding?.sheets ?? (connection?.sheets[0] ? [connection.sheets[0].name] : []));
  const all = sheets.includes(AllSheets);
  const previewable = all ? (connection?.sheets.map((s) => s.name) ?? []) : sheets;
  const [previewPick, setPreviewPick] = useState<string>("");
  const sheet = previewable.includes(previewPick) ? previewPick : (previewable[0] ?? "");
  const [headerRow, setHeaderRow] = useState(binding?.headerRow ?? 1);
  const [fields, setFields] = useState<FieldBinding[]>(binding?.fields ?? []);
  // Новая привязка — каждые 30 минут: свежие планы без ручного запуска.
  const [schedule, setSchedule] = useState<Schedule>(binding?.schedule ?? "interval");
  const [intervalMinutes, setIntervalMinutes] = useState(binding?.intervalMinutes ?? 30);
  const [dailyAt, setDailyAt] = useState(binding?.dailyAt ?? "07:00");
  const [enabled, setEnabled] = useState(binding?.enabled ?? true);
  const [preview, setPreview] = useState<SheetPreview | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [loadingPreview, setLoadingPreview] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // ИИ-сопоставление: предложение бэкенда (реальный вызов модели), ошибка, идёт ли анализ или подтверждение.
  const [proposal, setProposal] = useState<SheetMappingProposal | null>(null);
  const [aiLoading, setAiLoading] = useState(false);
  const [aiError, setAiError] = useState<string | null>(null);
  const [aiBusy, setAiBusy] = useState<"confirm" | "manual" | null>(null);

  useEffect(() => {
    if (!connectionId || !sheet) return;
    let cancelled = false;
    setLoadingPreview(true);
    setPreviewError(null);
    dataSourcesApi
      .preview(connectionId, sheet)
      .then((p) => !cancelled && setPreview(p))
      .catch((e: Error) => !cancelled && (setPreview(null), setPreviewError(e.message)))
      .finally(() => !cancelled && setLoadingPreview(false));
    return () => {
      cancelled = true;
    };
  }, [connectionId, sheet]);

  const columns = useMemo(() => columnsOf(preview, headerRow), [preview, headerRow]);
  const toggle = (field: DataSourceField, on: boolean) =>
    setFields((prev) => (on ? [...prev, defaultField(field, columns)] : prev.filter((f) => f.field !== field.key)));
  const update = (key: string, change: (f: FieldBinding) => FieldBinding) => setFields((prev) => prev.map((f) => (f.field === key ? change(f) : f)));

  const target = { section: section.key, connectionId, sheets, sheet: sheet || null, headerRow, bindingId: binding?.id ?? null };

  const autoMap = async () => {
    setAiLoading(true);
    setAiError(null);
    setProposal(null);
    try {
      setProposal(await dataSourcesApi.autoMap(target));
    } catch (e) {
      setAiError((e as Error).message);
    } finally {
      setAiLoading(false);
    }
  };

  const confirmAutoMap = async (decisions: MappingDecision[], settings: MappingFieldSetting[], dryRun: boolean) => {
    setAiBusy(dryRun ? "manual" : "confirm");
    setAiError(null);
    try {
      const result = await dataSourcesApi.confirmAutoMap({
        ...target,
        decisions,
        settings,
        schedule,
        intervalMinutes: schedule === "interval" ? intervalMinutes : null,
        dailyAt: schedule === "daily" ? dailyAt : null,
        enabled,
        dryRun,
      });
      if (dryRun) {
        setFields(result.fields);
        setProposal(null);
      } else {
        onSaved();
      }
    } catch (e) {
      setAiError((e as Error).message);
    } finally {
      setAiBusy(null);
    }
  };

  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      await dataSourcesApi.saveBinding(binding?.id ?? null, {
        section: section.key,
        connectionId,
        sheets,
        headerRow,
        fields: withHeaders(fields, columns),
        schedule,
        intervalMinutes: schedule === "interval" ? intervalMinutes : null,
        dailyAt: schedule === "daily" ? dailyAt : null,
        enabled,
      });
      onSaved();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal title={`${t(section.name)}: ${binding ? t("привязка Google Sheets") : t("новая привязка Google Sheets")}`} onClose={onClose} wide>
      <div className="space-y-5">
        <div className="grid gap-3 sm:grid-cols-[1fr_1fr_140px]">
          <div>
            <label className={label}>{t("Таблица")}</label>
            <select
              value={connectionId}
              onChange={(e) => {
                setConnectionId(e.target.value);
                const first = connections.find((c) => c.id === e.target.value)?.sheets[0]?.name;
                setSheets(first ? [first] : []);
              }}
              className={input}
            >
              {connections.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className={label}>{t("Листы")}</label>
            <div className="flex h-9 items-center rounded-lg border border-line bg-surface px-3 text-sm text-ink">
              <span className="truncate">{all ? t("Все листы таблицы") : sheets.length === 0 ? t("— выберите листы —") : sheets.join(", ")}</span>
            </div>
          </div>
          <div>
            <label className={label}>{t("Строка заголовков")}</label>
            <input type="number" min={1} max={1000} value={headerRow} onChange={(e) => setHeaderRow(Math.max(1, Number(e.target.value) || 1))} className={input} />
          </div>
        </div>

        <div>
          <div className="mb-1.5 text-xs font-medium text-ink-2">{t("Какие листы читать (разметка у них должна быть одинаковой: те же столбцы и строка заголовков)")}</div>
          <div className="flex flex-wrap gap-2">
            <label className={`inline-flex items-center gap-2 rounded-lg border px-3 py-1.5 text-sm ${all ? "border-accent/60 bg-accent-soft text-accent-strong" : "border-line text-ink-2"}`}>
              <input type="checkbox" checked={all} onChange={(e) => setSheets(e.target.checked ? [AllSheets] : connection?.sheets[0] ? [connection.sheets[0].name] : [])} className="accent-[var(--color-accent)]" />
              {t("Все листы — и новые, когда появятся")}
            </label>
            {!all &&
              [...(connection?.sheets.map((s) => s.name) ?? []), ...sheets.filter((n) => !connection?.sheets.some((s) => s.name === n))].map((name) => {
                const on = sheets.includes(name);
                const info = connection?.sheets.find((s) => s.name === name);
                return (
                  <label key={name} className={`inline-flex items-center gap-2 rounded-lg border px-3 py-1.5 text-sm ${on ? "border-accent/60 bg-accent-soft text-ink" : "border-line text-ink-2"}`}>
                    <input
                      type="checkbox"
                      checked={on}
                      onChange={(e) => setSheets((prev) => (e.target.checked ? [...prev, name] : prev.filter((n) => n !== name)))}
                      className="accent-[var(--color-accent)]"
                    />
                    {name}
                    <span className="text-xs text-ink-3">{info ? t("{rows} строк", { rows: info.rows }) : t("нет в таблице")}</span>
                  </label>
                );
              })}
          </div>
        </div>

        <div className="rounded-xl border border-line">
          <div className="flex flex-wrap items-center gap-2 border-b border-line px-3 py-2 text-xs text-ink-3">
            {t("Предпросмотр листа")}
            {previewable.length > 1 && (
              <select value={sheet} onChange={(e) => setPreviewPick(e.target.value)} className="h-7 rounded-md border border-line bg-surface px-2 text-xs text-ink">
                {previewable.map((n) => (
                  <option key={n} value={n}>
                    {n}
                  </option>
                ))}
              </select>
            )}
            {loadingPreview && <Loader2 className="size-3.5 animate-spin" />}
            {preview && <span>· {t("строк {rows}, столбцов {columns}; строка заголовков выделена", { rows: preview.rows, columns: preview.columns })}</span>}
          </div>
          {previewError ? (
            <div className="px-3 py-3 text-sm text-bad">{previewError}</div>
          ) : preview ? (
            <div className="max-h-56 overflow-auto">
              <table className="min-w-max border-collapse text-xs">
                <thead className="sticky top-0 bg-muted-solid">
                  <tr>
                    <th className="px-2 py-1 text-ink-3">#</th>
                    {preview.letters.map((l) => (
                      <th key={l} className="px-2 py-1 text-left font-semibold text-ink-3">
                        {l}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {preview.cells.slice(0, 10).map((row, i) => (
                    <tr key={i} className={`border-t border-line ${i + 1 === headerRow ? "bg-accent-soft font-semibold text-ink" : "text-ink-2"}`}>
                      <td className="px-2 py-1 text-ink-3">{i + 1}</td>
                      {preview.letters.map((l, c) => (
                        <td key={l} className="max-w-48 truncate whitespace-nowrap px-2 py-1">
                          {row[c] ?? ""}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <div className="px-3 py-3 text-sm text-ink-3">{loadingPreview ? t("Читаю таблицу…") : t("Выберите таблицу и лист.")}</div>
          )}
        </div>

        <div>
          <div className="mb-2 flex flex-wrap items-center gap-2">
            <span className="text-sm font-semibold text-ink">{t("Какие поля обновлять из этого листа")}</span>
            <button
              type="button"
              onClick={autoMap}
              disabled={aiLoading || !preview || !connectionId || sheets.length === 0}
              title={!preview ? t("Сначала дождитесь предпросмотра листа") : t("ИИ посмотрит заголовки и значения листа и предложит, какие столбцы в какие поля OneBase")}
              className="ml-auto inline-flex h-8 items-center gap-1.5 rounded-lg border border-accent/60 bg-accent-soft px-3 text-xs font-medium text-accent-strong hover:bg-accent-soft/70 disabled:opacity-50"
            >
              {aiLoading ? <Loader2 className="size-3.5 animate-spin" /> : <Sparkles className="size-3.5" />}
              {aiLoading ? t("ИИ анализирует лист…") : t("Сопоставить с помощью ИИ")}
            </button>
          </div>
          <p className="mb-3 text-xs text-ink-3">{t("Обновляются только отмеченные поля. Строки с ошибкой (регион не найден, не число) не записываются — поле ждёт исправления таблицы.")}</p>
          {aiError && !proposal && <div className="mb-3 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{aiError}</div>}
          {proposal && (
            <div className="mb-3">
              <AutoMapPanel
                proposal={proposal}
                busy={aiBusy}
                error={aiError}
                onConfirm={(d, s) => confirmAutoMap(d, s, false)}
                onToManual={(d, s) => confirmAutoMap(d, s, true)}
                onClose={() => (setProposal(null), setAiError(null))}
              />
            </div>
          )}
          <div className="space-y-3">
            {section.fields.map((f) => {
              const selected = fields.find((x) => x.field === f.key);
              const elsewhere = taken[f.key];
              return (
                <div key={f.key} className={`rounded-xl border ${selected ? "border-accent/50" : "border-line"} p-3`}>
                  <label className="flex items-start gap-2.5">
                    <input
                      type="checkbox"
                      className="mt-0.5 size-4 accent-[var(--color-accent)]"
                      checked={!!selected}
                      disabled={!!elsewhere && !selected}
                      onChange={(e) => toggle(f, e.target.checked)}
                    />
                    <span className="min-w-0">
                      <span className="block text-sm font-medium text-ink">{t(f.label)}</span>
                      <span className="block text-xs text-ink-3">{elsewhere && !selected ? t("Уже обновляется: {where}.", { where: elsewhere }) : t(f.description)}</span>
                    </span>
                  </label>
                  {selected && (
                    <div className="mt-3 border-t border-line pt-3">
                      {f.kind === "plan" && selected.plan && (
                        <PlanEditor
                          plan={selected.plan}
                          replace={selected.replace}
                          monthFromSheet={!!selected.monthFromSheet}
                          currentMonth={!!selected.currentMonth}
                          columns={columns}
                          onChange={(plan, replace, monthFromSheet, currentMonth) => update(f.key, (x) => ({ ...x, plan, replace, monthFromSheet, currentMonth }))}
                        />
                      )}
                      {f.kind === "profile" && selected.profile && (
                        <div className="grid gap-3 sm:grid-cols-2">
                          <div>
                            <label className={label}>{t("Регион")}</label>
                            <ColumnSelect value={selected.profile.regionColumn} columns={columns} onChange={(v) => update(f.key, (x) => ({ ...x, profile: { ...x.profile!, regionColumn: v ?? "" } }))} />
                          </div>
                          <div>
                            <label className={label}>{t(f.label)}</label>
                            <ColumnSelect value={selected.profile.valueColumn} columns={columns} onChange={(v) => update(f.key, (x) => ({ ...x, profile: { ...x.profile!, valueColumn: v ?? "" } }))} />
                          </div>
                          <p className="text-xs text-ink-3 sm:col-span-2">{t("Пустая ячейка не стирает записанное в OneBase.")}</p>
                        </div>
                      )}
                      {f.kind === "pnl" && selected.pnl && (
                        <PnlEditor
                          pnl={selected.pnl}
                          replace={selected.replace}
                          monthFromSheet={!!selected.monthFromSheet}
                          currentMonth={!!selected.currentMonth}
                          columns={columns}
                          onChange={(pnl, replace, monthFromSheet, currentMonth) => update(f.key, (x) => ({ ...x, pnl, replace, monthFromSheet, currentMonth }))}
                        />
                      )}
                      {f.kind === "ledger" && selected.ledger && (
                        <LedgerEditor
                          ledger={selected.ledger}
                          payments={f.key === "dealer_payments"}
                          columns={columns}
                          onChange={(ledger) => update(f.key, (x) => ({ ...x, ledger }))}
                        />
                      )}
                      {f.kind === "terms" && selected.terms && (
                        <TermsEditor terms={selected.terms} columns={columns} onChange={(terms) => update(f.key, (x) => ({ ...x, terms }))} />
                      )}
                      {f.kind === "balance" && selected.balance && (
                        <BalanceEditor balance={selected.balance} columns={columns} onChange={(balance) => update(f.key, (x) => ({ ...x, balance }))} />
                      )}
                      {/* Период — когда в листе много месяцев (месяц столбцом или в названиях листов). */}
                      {(f.kind === "plan" || f.kind === "pnl") &&
                        !selected.currentMonth &&
                        (selected.monthFromSheet || (f.kind === "plan" ? selected.plan?.monthColumn : selected.pnl?.monthColumn)) && (
                          <div className="mt-3 border-t border-line pt-3">
                            <PeriodSelect value={selected.period} onChange={(period) => update(f.key, (x) => ({ ...x, period }))} />
                          </div>
                        )}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>

        <div>
          <div className="mb-2 text-sm font-semibold text-ink">{t("Время синхронизации")}</div>
          <div className="flex flex-wrap items-center gap-x-5 gap-y-2 text-sm text-ink">
            {(["manual", "interval", "daily"] as Schedule[]).map((s) => (
              <label key={s} className="inline-flex items-center gap-2 whitespace-nowrap">
                <input type="radio" name="schedule" checked={schedule === s} onChange={() => setSchedule(s)} className="accent-[var(--color-accent)]" />
                {s === "manual" ? t("Вручную") : s === "interval" ? t("Каждые") : t("Ежедневно в")}
                {s === "interval" && (
                  <select value={intervalMinutes} onChange={(e) => setIntervalMinutes(Number(e.target.value))} disabled={schedule !== "interval"} className={`${input} w-auto`}>
                    {intervals.map((m) => (
                      <option key={m} value={m}>
                        {m < 60 ? t("{n} мин", { n: m }) : t("{n} ч", { n: m / 60 })}
                      </option>
                    ))}
                  </select>
                )}
                {s === "daily" && (
                  <input type="time" value={dailyAt} onChange={(e) => setDailyAt(e.target.value)} disabled={schedule !== "daily"} className={`${input} w-auto`} />
                )}
              </label>
            ))}
          </div>
          <p className="mt-1.5 text-xs text-ink-3">{t("Время — ташкентское. «Синхронизировать сейчас» работает при любом расписании.")}</p>
          <label className="mt-3 inline-flex items-center gap-2 text-sm text-ink">
            <input type="checkbox" checked={enabled} onChange={(e) => setEnabled(e.target.checked)} className="size-4 accent-[var(--color-accent)]" />
            {t("Привязка включена (выключенная по расписанию не запускается)")}
          </label>
        </div>

        {error && <div className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</div>}
        <div className="flex justify-end gap-2 border-t border-line pt-4">
          <button type="button" onClick={onClose} className="h-9 rounded-lg border border-line px-4 text-sm text-ink hover:bg-muted">
            {t("Отмена")}
          </button>
          <button
            type="button"
            onClick={save}
            disabled={saving || !connectionId || sheets.length === 0 || fields.length === 0}
            className="inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-medium text-white hover:bg-accent-strong disabled:opacity-50"
          >
            {saving && <Loader2 className="size-4 animate-spin" />}
            {t("Сохранить")}
          </button>
        </div>
      </div>
    </Modal>
  );
}

type Layout = "column" | "columns" | "total";

/** План: регион, категории (столбцом, столбцами или без категорий), месяц (столбцом или один месяц), единицы и режим записи. */
function PlanEditor({
  plan,
  replace,
  monthFromSheet,
  currentMonth,
  columns,
  onChange,
}: {
  plan: PlanMapping;
  replace: boolean;
  monthFromSheet: boolean;
  currentMonth: boolean;
  columns: Column[];
  onChange: (p: PlanMapping, replace: boolean, monthFromSheet: boolean, currentMonth: boolean) => void;
}) {
  const t = useT();
  const layout: Layout = plan.valueColumns?.length ? "columns" : plan.categoryColumn ? "column" : "total";
  const monthMode = currentMonth ? "current" : monthFromSheet ? "sheet" : plan.monthColumn != null ? "column" : "fixed";
  const set = (p: Partial<PlanMapping>) => onChange({ ...plan, ...p }, replace, monthFromSheet, currentMonth);
  const setLayout = (l: Layout) =>
    l === "columns"
      ? set({ categoryColumn: null, valueColumn: null, valueColumns: plan.valueColumns?.length ? plan.valueColumns : [] })
      : l === "column"
        ? set({ valueColumns: null, categoryColumn: plan.categoryColumn ?? columns[1]?.value ?? null })
        : set({ valueColumns: null, categoryColumn: null });

  return (
    <div className="space-y-3">
      <div className="grid gap-3 sm:grid-cols-3">
        <div>
          <label className={label}>{t("Регион (дилер)")}</label>
          <ColumnSelect value={plan.regionColumn} columns={columns} onChange={(v) => set({ regionColumn: v ?? "" })} />
        </div>
        <div className="sm:col-span-2">
          <label className={label}>{t("Категории")}</label>
          <select value={layout} onChange={(e) => setLayout(e.target.value as Layout)} className={input}>
            <option value="column">{t("категория в отдельном столбце")}</option>
            <option value="columns">{t("каждая категория — свой столбец")}</option>
            <option value="total">{t("без категорий — итог региона")}</option>
          </select>
        </div>
      </div>
      {layout === "column" && (
        <div className="grid gap-3 sm:grid-cols-2">
          <div>
            <label className={label}>{t("Категория")}</label>
            <ColumnSelect value={plan.categoryColumn} columns={columns} onChange={(v) => set({ categoryColumn: v })} />
          </div>
          <div>
            <label className={label}>{t("План")}</label>
            <ColumnSelect value={plan.valueColumn} columns={columns} onChange={(v) => set({ valueColumn: v })} />
          </div>
        </div>
      )}
      {layout === "total" && (
        <div className="sm:w-1/2">
          <label className={label}>{t("План")}</label>
          <ColumnSelect value={plan.valueColumn} columns={columns} onChange={(v) => set({ valueColumn: v })} />
        </div>
      )}
      {layout === "columns" && (
        <div>
          <label className={label}>{t("Столбцы категорий (название категории — из заголовка)")}</label>
          <div className="flex flex-wrap gap-x-4 gap-y-1.5">
            {columns.map((c) => {
              const on = plan.valueColumns?.some((v) => v.column === c.value) ?? false;
              return (
                <label key={c.value + c.label} className="inline-flex items-center gap-1.5 text-sm text-ink-2">
                  <input
                    type="checkbox"
                    checked={on}
                    onChange={(e) =>
                      set({ valueColumns: e.target.checked ? [...(plan.valueColumns ?? []), { column: c.value, category: null }] : (plan.valueColumns ?? []).filter((v) => v.column !== c.value) })
                    }
                    className="accent-[var(--color-accent)]"
                  />
                  {c.label}
                </label>
              );
            })}
          </div>
        </div>
      )}
      <div className="grid gap-3 sm:grid-cols-4">
        <div className="sm:col-span-2">
          <label className={label}>{t("Месяц")}</label>
          <select
            value={monthMode}
            onChange={(e) =>
              e.target.value === "column"
                ? onChange({ ...plan, monthColumn: columns[0]?.value ?? "A", month: null }, replace, false, false)
                : e.target.value === "sheet"
                  ? onChange({ ...plan, monthColumn: null, month: null }, replace, true, false)
                  : e.target.value === "current"
                    ? onChange({ ...plan, monthColumn: null, month: null, yearColumn: null }, replace, false, true)
                    : onChange({ ...plan, monthColumn: null, month: new Date().getMonth() + 1 }, replace, false, false)
            }
            className={input}
          >
            <option value="column">{t("из столбца (сентябрь, 09.2026, 2026-09)")}</option>
            <option value="sheet">{t("из названия листа (лист на месяц: «Сентябрь 2026»)")}</option>
            <option value="current">{t("текущий месяц — с 1-го числа сам переходит на новый")}</option>
            <option value="fixed">{t("один месяц для всех строк")}</option>
          </select>
        </div>
        {monthMode === "current" ? (
          <div className="self-end pb-1 text-xs text-ink-3 sm:col-span-2">
            {t("Лист всегда про текущий месяц (по Ташкенту). В новом месяце те же цифры, что за прошлый, по расписанию не копируются — ждём обновления таблицы.")}
          </div>
        ) : monthMode === "sheet" ? (
          <div className="self-end pb-2 text-xs text-ink-3">{t("«Сентябрь 2026», «09.2026», «2026-09»; без года — год справа.")}</div>
        ) : monthMode === "column" ? (
          <div className="sm:col-span-2">
            <label className={label}>{t("Столбец месяца")}</label>
            <ColumnSelect value={plan.monthColumn} columns={columns} onChange={(v) => set({ monthColumn: v })} />
          </div>
        ) : (
          <div>
            <label className={label}>{t("Месяц")}</label>
            <input type="number" min={1} max={12} value={plan.month ?? ""} onChange={(e) => set({ month: Number(e.target.value) || null })} className={input} />
          </div>
        )}
        {monthMode !== "current" && (
          <div>
            <label className={label}>{t("Год (если в месяце нет года)")}</label>
            <input type="number" min={2020} max={2100} value={plan.year ?? ""} onChange={(e) => set({ year: Number(e.target.value) || null })} className={input} />
          </div>
        )}
      </div>
      <div className="flex flex-wrap items-center gap-x-6 gap-y-2 text-sm text-ink">
        <label className="inline-flex items-center gap-2">
          {t("Единицы")}
          <select value={plan.multiplier} onChange={(e) => set({ multiplier: Number(e.target.value) })} className={`${input} w-auto`}>
            <option value={1}>{t("кг")}</option>
            <option value={1000}>{t("тонны")}</option>
          </select>
        </label>
        <label className="inline-flex items-center gap-2">
          <input type="checkbox" checked={replace} onChange={(e) => onChange(plan, e.target.checked, monthFromSheet, currentMonth)} className="size-4 accent-[var(--color-accent)]" />
          {t("Листы полные за свои месяцы — удалять строки плана этих месяцев, которых в листах нет")}
        </label>
      </div>
    </div>
  );
}

/** P&L: реестр — контрагент, статья (как в OneBase: продажи, себестоимость, статьи расходов), сумма, месяц. */
function PnlEditor({
  pnl,
  replace,
  monthFromSheet,
  currentMonth,
  columns,
  onChange,
}: {
  pnl: PnlMapping;
  replace: boolean;
  monthFromSheet: boolean;
  currentMonth: boolean;
  columns: Column[];
  onChange: (p: PnlMapping, replace: boolean, monthFromSheet: boolean, currentMonth: boolean) => void;
}) {
  const t = useT();
  const set = (p: Partial<PnlMapping>) => onChange({ ...pnl, ...p }, replace, monthFromSheet, currentMonth);
  const auto = monthFromSheet || currentMonth;
  return (
    <div className="space-y-3">
      <div className="grid gap-3 sm:grid-cols-3">
        <div>
          <label className={label}>{t("Контрагент (дилер)")}</label>
          <ColumnSelect value={pnl.dealerColumn} columns={columns} onChange={(v) => set({ dealerColumn: v })} />
        </div>
        <div>
          <label className={label}>{t("Статья P&L")}</label>
          <ColumnSelect value={pnl.lineColumn} columns={columns} onChange={(v) => set({ lineColumn: v })} />
        </div>
        <div>
          <label className={label}>{t("Сумма")}</label>
          <ColumnSelect value={pnl.valueColumn} columns={columns} onChange={(v) => set({ valueColumn: v })} />
        </div>
      </div>
      <div className="grid gap-3 sm:grid-cols-3">
        <div>
          <label className={label}>{t("Месяц")}</label>
          <label className="mb-1 flex items-center gap-2 text-xs text-ink-2">
            <input
              type="checkbox"
              checked={monthFromSheet}
              onChange={(e) => onChange({ ...pnl, monthColumn: e.target.checked ? null : pnl.monthColumn, month: null }, replace, e.target.checked, false)}
              className="accent-[var(--color-accent)]"
            />
            {t("из названия листа")}
          </label>
          <label className="mb-1.5 flex items-center gap-2 text-xs text-ink-2">
            <input
              type="checkbox"
              checked={currentMonth}
              onChange={(e) => onChange({ ...pnl, monthColumn: e.target.checked ? null : pnl.monthColumn, month: null }, replace, false, e.target.checked)}
              className="accent-[var(--color-accent)]"
            />
            {t("текущий месяц (с 1-го — новый)")}
          </label>
          {!auto && <ColumnSelect value={pnl.monthColumn} columns={columns} onChange={(v) => set({ monthColumn: v, month: v ? null : pnl.month })} empty={t("— один месяц —")} />}
        </div>
        {!pnl.monthColumn && !auto && (
          <div>
            <label className={label}>{t("Номер месяца")}</label>
            <input type="number" min={1} max={12} value={pnl.month ?? ""} onChange={(e) => set({ month: Number(e.target.value) || null })} className={input} />
          </div>
        )}
        <div>
          <label className={label}>{t("Год (если в месяце нет года)")}</label>
          <input type="number" min={2020} max={2100} value={pnl.year ?? ""} onChange={(e) => set({ year: Number(e.target.value) || null })} className={input} />
        </div>
      </div>
      <label className="inline-flex items-center gap-2 text-sm text-ink">
        <input type="checkbox" checked={replace} onChange={(e) => onChange(pnl, e.target.checked, monthFromSheet, currentMonth)} className="size-4 accent-[var(--color-accent)]" />
        {t("Листы полные за свои месяцы — удалять строки P&L этих месяцев, которых в листах нет")}
      </label>
    </div>
  );
}

/** Реестр (оплаты, расходы): дата, контрагент, сумма, кошелёк или статья, отбор по категории, комментарий; лист копируется целиком. */
function LedgerEditor({ ledger, payments, columns, onChange }: { ledger: LedgerMapping; payments: boolean; columns: Column[]; onChange: (l: LedgerMapping) => void }) {
  const t = useT();
  const set = (p: Partial<LedgerMapping>) => onChange({ ...ledger, ...p });
  return (
    <div className="space-y-3">
      <div className="grid gap-3 sm:grid-cols-3">
        <div>
          <label className={label}>{t("Дата")}</label>
          <ColumnSelect value={ledger.dateColumn} columns={columns} onChange={(v) => set({ dateColumn: v ?? "" })} />
        </div>
        <div>
          <label className={label}>{t("Контрагент (дилер)")}</label>
          <ColumnSelect value={ledger.dealerColumn} columns={columns} onChange={(v) => set({ dealerColumn: v ?? "" })} />
        </div>
        <div>
          <label className={label}>{t("Сумма, сум")}</label>
          <ColumnSelect value={ledger.amountColumn} columns={columns} onChange={(v) => set({ amountColumn: v ?? "" })} />
        </div>
      </div>
      <div className="grid gap-3 sm:grid-cols-3">
        <div>
          <label className={label}>{payments ? t("Кошелёк (касса или банк)") : t("Статья расхода (601…609)")}</label>
          <ColumnSelect value={ledger.kindColumn} columns={columns} onChange={(v) => set({ kindColumn: v })} empty={t("— нет —")} />
        </div>
        <div>
          <label className={label}>{t("Категория для отбора строк")}</label>
          <ColumnSelect value={ledger.categoryColumn} columns={columns} onChange={(v) => set({ categoryColumn: v, categories: v ? ledger.categories : null })} empty={t("— все строки —")} />
        </div>
        <div>
          <label className={label}>{t("Брать категории (начало, через запятую)")}</label>
          <input
            value={(ledger.categories ?? []).join(", ")}
            onChange={(e) => set({ categories: e.target.value.split(",").map((c) => c.trim()).filter(Boolean) })}
            disabled={!ledger.categoryColumn}
            placeholder="101, 103"
            className={`${input} disabled:opacity-50`}
          />
        </div>
      </div>
      <div className="grid gap-3 sm:grid-cols-3">
        <div className="sm:col-span-2">
          <label className={label}>{t("Комментарий")}</label>
          <ColumnSelect value={ledger.commentColumn} columns={columns} onChange={(v) => set({ commentColumn: v })} empty={t("— нет —")} />
        </div>
        <div>
          <label className={label}>{t("Строки с даты")}</label>
          <input type="date" value={ledger.dateFrom ?? ""} onChange={(e) => set({ dateFrom: e.target.value || null })} className={input} />
        </div>
      </div>
      <p className="text-xs text-ink-3">
        {t("Лист копируется целиком при каждой синхронизации. Строки раньше «Строки с даты» не берутся — например, остатки на начало, записанные в реестр расходом.")}
      </p>
    </div>
  );
}

/** Условия контрагента: срок оплаты (дней) и остаток долга на дату. */
function TermsEditor({ terms, columns, onChange }: { terms: TermsMapping; columns: Column[]; onChange: (v: TermsMapping) => void }) {
  const t = useT();
  const set = (p: Partial<TermsMapping>) => onChange({ ...terms, ...p });
  return (
    <div className="grid gap-3 sm:grid-cols-4">
      <div>
        <label className={label}>{t("Контрагент (дилер)")}</label>
        <ColumnSelect value={terms.dealerColumn} columns={columns} onChange={(v) => set({ dealerColumn: v ?? "" })} />
      </div>
      <div>
        <label className={label}>{t("Срок оплаты, дней")}</label>
        <ColumnSelect value={terms.normColumn} columns={columns} onChange={(v) => set({ normColumn: v })} empty={t("— нет —")} />
      </div>
      <div>
        <label className={label}>{t("Остаток долга")}</label>
        <ColumnSelect value={terms.openingColumn} columns={columns} onChange={(v) => set({ openingColumn: v, openingDate: v ? terms.openingDate : null })} empty={t("— нет —")} />
      </div>
      <div>
        <label className={label}>{t("Остаток на дату")}</label>
        <input type="date" value={terms.openingDate ?? ""} onChange={(e) => set({ openingDate: e.target.value || null })} disabled={!terms.openingColumn} className={`${input} disabled:opacity-50`} />
      </div>
      <p className="text-xs text-ink-3 sm:col-span-4">{t("Пустой срок — договор без нормы: просрочки нет. Остаток долга — опора расчёта, если контрагента нет в акте бухгалтерии.")}</p>
    </div>
  );
}

/** Остатки контрагентов на дату (акт бухгалтерии): контрагент, сумма; дата — из ячейки листа или одна дата. */
function BalanceEditor({ balance, columns, onChange }: { balance: BalanceMapping; columns: Column[]; onChange: (v: BalanceMapping) => void }) {
  const t = useT();
  const set = (p: Partial<BalanceMapping>) => onChange({ ...balance, ...p });
  const fromCell = balance.date == null;
  return (
    <div className="grid gap-3 sm:grid-cols-4">
      <div>
        <label className={label}>{t("Контрагент (дилер)")}</label>
        <ColumnSelect value={balance.dealerColumn} columns={columns} onChange={(v) => set({ dealerColumn: v ?? "" })} />
      </div>
      <div>
        <label className={label}>{t("Остаток, сум")}</label>
        <ColumnSelect value={balance.amountColumn} columns={columns} onChange={(v) => set({ amountColumn: v ?? "" })} />
      </div>
      <div>
        <label className={label}>{t("Дата остатка")}</label>
        <select
          value={fromCell ? "cell" : "fixed"}
          onChange={(e) => (e.target.value === "cell" ? set({ date: null, dateCell: balance.dateCell ?? "F1" }) : set({ dateCell: null, date: new Date().toISOString().slice(0, 8) + "01" }))}
          className={input}
        >
          <option value="cell">{t("из ячейки листа")}</option>
          <option value="fixed">{t("одна дата")}</option>
        </select>
      </div>
      <div>
        <label className={label}>{fromCell ? t("Ячейка с датой") : t("Дата")}</label>
        {fromCell ? (
          <input value={balance.dateCell ?? ""} onChange={(e) => set({ dateCell: e.target.value.trim().toUpperCase() || null })} placeholder="F1" className={input} />
        ) : (
          <input type="date" value={balance.date ?? ""} onChange={(e) => set({ date: e.target.value || null })} className={input} />
        )}
      </div>
      <p className="text-xs text-ink-3 sm:col-span-4">
        {t("Строки-заголовки групп («Диллер», «Ритейл», «Долги») и «Итого» не контрагенты — они задают группу строк ниже. Ячейка с датой — когда лист каждый месяц переходит на новый месяц (акт на «Сентябрь 2026» — дата 01.09.2026).")}
      </p>
    </div>
  );
}
