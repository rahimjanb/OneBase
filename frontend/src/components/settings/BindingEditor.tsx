"use client";

import { useEffect, useMemo, useState } from "react";
import { Loader2 } from "lucide-react";
import { Modal } from "@/components/files/Modal";
import {
  AllSheets,
  dataSourcesApi,
  type BindingView,
  type ConnectionView,
  type DataSourceField,
  type FieldBinding,
  type PlanMapping,
  type PnlMapping,
  type Schedule,
  type SectionView,
  type SheetPreview,
} from "@/lib/data-sources";

const input = "h-9 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink focus:border-accent/60 focus:outline-none";
const label = "mb-1 block text-xs font-medium text-ink-2";
const intervals = [15, 30, 60, 180, 360, 720, 1440];

type Column = { value: string; label: string };

/** Столбцы листа: значение — заголовок (если он есть и не повторяется), иначе буква; подпись — «B · Регион». */
function columnsOf(preview: SheetPreview | null, headerRow: number): Column[] {
  if (!preview) return [];
  const header = preview.cells[headerRow - 1] ?? [];
  const names = header.map((h) => h.trim());
  return preview.letters.map((letter, i) => {
    const name = names[i] ?? "";
    const unique = name.length > 0 && names.filter((n) => n.toLowerCase() === name.toLowerCase()).length === 1;
    return { value: unique ? name : letter, label: `${letter} · ${name || "—"}` };
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
  if (field.kind === "pnl") {
    const pnl: PnlMapping = {
      sheet: null, headerRow: 1, dealerColumn: guess(columns, /дилер|контрагент|dealer/i) ?? region, valueColumns: null,
      lineColumn: guess(columns, /стать|строк|line|показател/i), valueColumn: guess(columns, /сумм|amount|значен/i),
      yearColumn: guess(columns, /^.{0,4}год|year/i), monthColumn: month, year: month ? null : new Date().getFullYear(), month: null, multiplier: 1,
    };
    return { field: field.key, pnl, replace: true };
  }
  const plan: PlanMapping = {
    regionColumn: region, categoryColumn: guess(columns, /категор|category/i), valueColumn: guess(columns, /план|plan|кг|kg/i), valueColumns: null,
    monthColumn: month, yearColumn: guess(columns, /^.{0,4}год|year/i), year: new Date().getFullYear(), month: month ? null : new Date().getMonth() + 1, multiplier: 1,
  };
  return { field: field.key, plan, replace: true };
}

function ColumnSelect({ value, columns, onChange, empty }: { value: string | null; columns: Column[]; onChange: (v: string | null) => void; empty?: string }) {
  const known = value == null || columns.some((c) => c.value === value);
  return (
    <select value={value ?? ""} onChange={(e) => onChange(e.target.value || null)} className={input}>
      <option value="">{empty ?? "— выберите столбец —"}</option>
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
  const [schedule, setSchedule] = useState<Schedule>(binding?.schedule ?? "daily");
  const [intervalMinutes, setIntervalMinutes] = useState(binding?.intervalMinutes ?? 60);
  const [dailyAt, setDailyAt] = useState(binding?.dailyAt ?? "07:00");
  const [enabled, setEnabled] = useState(binding?.enabled ?? true);
  const [preview, setPreview] = useState<SheetPreview | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [loadingPreview, setLoadingPreview] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

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

  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      await dataSourcesApi.saveBinding(binding?.id ?? null, {
        section: section.key,
        connectionId,
        sheets,
        headerRow,
        fields,
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
    <Modal title={`${section.name}: ${binding ? "привязка Google Sheets" : "новая привязка Google Sheets"}`} onClose={onClose} wide>
      <div className="space-y-5">
        <div className="grid gap-3 sm:grid-cols-[1fr_1fr_140px]">
          <div>
            <label className={label}>Таблица</label>
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
            <label className={label}>Листы</label>
            <div className="flex h-9 items-center rounded-lg border border-line bg-surface px-3 text-sm text-ink">
              <span className="truncate">{all ? "Все листы таблицы" : sheets.length === 0 ? "— выберите листы —" : sheets.join(", ")}</span>
            </div>
          </div>
          <div>
            <label className={label}>Строка заголовков</label>
            <input type="number" min={1} max={1000} value={headerRow} onChange={(e) => setHeaderRow(Math.max(1, Number(e.target.value) || 1))} className={input} />
          </div>
        </div>

        <div>
          <div className="mb-1.5 text-xs font-medium text-ink-2">Какие листы читать (разметка у них должна быть одинаковой: те же столбцы и строка заголовков)</div>
          <div className="flex flex-wrap gap-2">
            <label className={`inline-flex items-center gap-2 rounded-lg border px-3 py-1.5 text-sm ${all ? "border-accent/60 bg-accent-soft text-accent-strong" : "border-line text-ink-2"}`}>
              <input type="checkbox" checked={all} onChange={(e) => setSheets(e.target.checked ? [AllSheets] : connection?.sheets[0] ? [connection.sheets[0].name] : [])} className="accent-[var(--color-accent)]" />
              Все листы — и новые, когда появятся
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
                    <span className="text-xs text-ink-3">{info ? `${info.rows} строк` : "нет в таблице"}</span>
                  </label>
                );
              })}
          </div>
        </div>

        <div className="rounded-xl border border-line">
          <div className="flex flex-wrap items-center gap-2 border-b border-line px-3 py-2 text-xs text-ink-3">
            Предпросмотр листа
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
            {preview && <span>· строк {preview.rows}, столбцов {preview.columns}; строка заголовков выделена</span>}
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
            <div className="px-3 py-3 text-sm text-ink-3">{loadingPreview ? "Читаю таблицу…" : "Выберите таблицу и лист."}</div>
          )}
        </div>

        <div>
          <div className="mb-2 text-sm font-semibold text-ink">Какие поля обновлять из этого листа</div>
          <p className="mb-3 text-xs text-ink-3">Обновляются только отмеченные поля. Строки с ошибкой (регион не найден, не число) не записываются — поле ждёт исправления таблицы.</p>
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
                      <span className="block text-sm font-medium text-ink">{f.label}</span>
                      <span className="block text-xs text-ink-3">{elsewhere && !selected ? `Уже обновляется: ${elsewhere}.` : f.description}</span>
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
                            <label className={label}>Регион</label>
                            <ColumnSelect value={selected.profile.regionColumn} columns={columns} onChange={(v) => update(f.key, (x) => ({ ...x, profile: { ...x.profile!, regionColumn: v ?? "" } }))} />
                          </div>
                          <div>
                            <label className={label}>{f.label}</label>
                            <ColumnSelect value={selected.profile.valueColumn} columns={columns} onChange={(v) => update(f.key, (x) => ({ ...x, profile: { ...x.profile!, valueColumn: v ?? "" } }))} />
                          </div>
                          <p className="text-xs text-ink-3 sm:col-span-2">Пустая ячейка не стирает записанное в OneBase.</p>
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
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>

        <div>
          <div className="mb-2 text-sm font-semibold text-ink">Время синхронизации</div>
          <div className="flex flex-wrap items-center gap-x-5 gap-y-2 text-sm text-ink">
            {(["manual", "interval", "daily"] as Schedule[]).map((s) => (
              <label key={s} className="inline-flex items-center gap-2 whitespace-nowrap">
                <input type="radio" name="schedule" checked={schedule === s} onChange={() => setSchedule(s)} className="accent-[var(--color-accent)]" />
                {s === "manual" ? "Вручную" : s === "interval" ? "Каждые" : "Ежедневно в"}
                {s === "interval" && (
                  <select value={intervalMinutes} onChange={(e) => setIntervalMinutes(Number(e.target.value))} disabled={schedule !== "interval"} className={`${input} w-auto`}>
                    {intervals.map((m) => (
                      <option key={m} value={m}>
                        {m < 60 ? `${m} мин` : `${m / 60} ч`}
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
          <p className="mt-1.5 text-xs text-ink-3">Время — ташкентское. «Синхронизировать сейчас» работает при любом расписании.</p>
          <label className="mt-3 inline-flex items-center gap-2 text-sm text-ink">
            <input type="checkbox" checked={enabled} onChange={(e) => setEnabled(e.target.checked)} className="size-4 accent-[var(--color-accent)]" />
            Привязка включена (выключенная по расписанию не запускается)
          </label>
        </div>

        {error && <div className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</div>}
        <div className="flex justify-end gap-2 border-t border-line pt-4">
          <button type="button" onClick={onClose} className="h-9 rounded-lg border border-line px-4 text-sm text-ink hover:bg-muted">
            Отмена
          </button>
          <button
            type="button"
            onClick={save}
            disabled={saving || !connectionId || sheets.length === 0 || fields.length === 0}
            className="inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-medium text-white hover:bg-accent-strong disabled:opacity-50"
          >
            {saving && <Loader2 className="size-4 animate-spin" />}
            Сохранить
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
          <label className={label}>Регион (дилер)</label>
          <ColumnSelect value={plan.regionColumn} columns={columns} onChange={(v) => set({ regionColumn: v ?? "" })} />
        </div>
        <div className="sm:col-span-2">
          <label className={label}>Категории</label>
          <select value={layout} onChange={(e) => setLayout(e.target.value as Layout)} className={input}>
            <option value="column">категория в отдельном столбце</option>
            <option value="columns">каждая категория — свой столбец</option>
            <option value="total">без категорий — итог региона</option>
          </select>
        </div>
      </div>
      {layout === "column" && (
        <div className="grid gap-3 sm:grid-cols-2">
          <div>
            <label className={label}>Категория</label>
            <ColumnSelect value={plan.categoryColumn} columns={columns} onChange={(v) => set({ categoryColumn: v })} />
          </div>
          <div>
            <label className={label}>План</label>
            <ColumnSelect value={plan.valueColumn} columns={columns} onChange={(v) => set({ valueColumn: v })} />
          </div>
        </div>
      )}
      {layout === "total" && (
        <div className="sm:w-1/2">
          <label className={label}>План</label>
          <ColumnSelect value={plan.valueColumn} columns={columns} onChange={(v) => set({ valueColumn: v })} />
        </div>
      )}
      {layout === "columns" && (
        <div>
          <label className={label}>Столбцы категорий (название категории — из заголовка)</label>
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
          <label className={label}>Месяц</label>
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
            <option value="column">из столбца (сентябрь, 09.2026, 2026-09)</option>
            <option value="sheet">из названия листа (лист на месяц: «Сентябрь 2026»)</option>
            <option value="current">текущий месяц — с 1-го числа сам переходит на новый</option>
            <option value="fixed">один месяц для всех строк</option>
          </select>
        </div>
        {monthMode === "current" ? (
          <div className="self-end pb-1 text-xs text-ink-3 sm:col-span-2">
            Лист всегда про текущий месяц (по Ташкенту). В новом месяце те же цифры, что за прошлый, по расписанию не копируются — ждём обновления таблицы.
          </div>
        ) : monthMode === "sheet" ? (
          <div className="self-end pb-2 text-xs text-ink-3">«Сентябрь 2026», «09.2026», «2026-09»; без года — год справа.</div>
        ) : monthMode === "column" ? (
          <div className="sm:col-span-2">
            <label className={label}>Столбец месяца</label>
            <ColumnSelect value={plan.monthColumn} columns={columns} onChange={(v) => set({ monthColumn: v })} />
          </div>
        ) : (
          <div>
            <label className={label}>Месяц</label>
            <input type="number" min={1} max={12} value={plan.month ?? ""} onChange={(e) => set({ month: Number(e.target.value) || null })} className={input} />
          </div>
        )}
        {monthMode !== "current" && (
          <div>
            <label className={label}>Год (если в месяце нет года)</label>
            <input type="number" min={2020} max={2100} value={plan.year ?? ""} onChange={(e) => set({ year: Number(e.target.value) || null })} className={input} />
          </div>
        )}
      </div>
      <div className="flex flex-wrap items-center gap-x-6 gap-y-2 text-sm text-ink">
        <label className="inline-flex items-center gap-2">
          Единицы
          <select value={plan.multiplier} onChange={(e) => set({ multiplier: Number(e.target.value) })} className={`${input} w-auto`}>
            <option value={1}>кг</option>
            <option value={1000}>тонны</option>
          </select>
        </label>
        <label className="inline-flex items-center gap-2">
          <input type="checkbox" checked={replace} onChange={(e) => onChange(plan, e.target.checked, monthFromSheet, currentMonth)} className="size-4 accent-[var(--color-accent)]" />
          Листы полные за свои месяцы — удалять строки плана этих месяцев, которых в листах нет
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
  const set = (p: Partial<PnlMapping>) => onChange({ ...pnl, ...p }, replace, monthFromSheet, currentMonth);
  const auto = monthFromSheet || currentMonth;
  return (
    <div className="space-y-3">
      <div className="grid gap-3 sm:grid-cols-3">
        <div>
          <label className={label}>Контрагент (дилер)</label>
          <ColumnSelect value={pnl.dealerColumn} columns={columns} onChange={(v) => set({ dealerColumn: v })} />
        </div>
        <div>
          <label className={label}>Статья P&L</label>
          <ColumnSelect value={pnl.lineColumn} columns={columns} onChange={(v) => set({ lineColumn: v })} />
        </div>
        <div>
          <label className={label}>Сумма</label>
          <ColumnSelect value={pnl.valueColumn} columns={columns} onChange={(v) => set({ valueColumn: v })} />
        </div>
      </div>
      <div className="grid gap-3 sm:grid-cols-3">
        <div>
          <label className={label}>Месяц</label>
          <label className="mb-1 flex items-center gap-2 text-xs text-ink-2">
            <input
              type="checkbox"
              checked={monthFromSheet}
              onChange={(e) => onChange({ ...pnl, monthColumn: e.target.checked ? null : pnl.monthColumn, month: null }, replace, e.target.checked, false)}
              className="accent-[var(--color-accent)]"
            />
            из названия листа
          </label>
          <label className="mb-1.5 flex items-center gap-2 text-xs text-ink-2">
            <input
              type="checkbox"
              checked={currentMonth}
              onChange={(e) => onChange({ ...pnl, monthColumn: e.target.checked ? null : pnl.monthColumn, month: null }, replace, false, e.target.checked)}
              className="accent-[var(--color-accent)]"
            />
            текущий месяц (с 1-го — новый)
          </label>
          {!auto && <ColumnSelect value={pnl.monthColumn} columns={columns} onChange={(v) => set({ monthColumn: v, month: v ? null : pnl.month })} empty="— один месяц —" />}
        </div>
        {!pnl.monthColumn && !auto && (
          <div>
            <label className={label}>Номер месяца</label>
            <input type="number" min={1} max={12} value={pnl.month ?? ""} onChange={(e) => set({ month: Number(e.target.value) || null })} className={input} />
          </div>
        )}
        <div>
          <label className={label}>Год (если в месяце нет года)</label>
          <input type="number" min={2020} max={2100} value={pnl.year ?? ""} onChange={(e) => set({ year: Number(e.target.value) || null })} className={input} />
        </div>
      </div>
      <label className="inline-flex items-center gap-2 text-sm text-ink">
        <input type="checkbox" checked={replace} onChange={(e) => onChange(pnl, e.target.checked, monthFromSheet, currentMonth)} className="size-4 accent-[var(--color-accent)]" />
        Листы полные за свои месяцы — удалять строки P&L этих месяцев, которых в листах нет
      </label>
    </div>
  );
}
