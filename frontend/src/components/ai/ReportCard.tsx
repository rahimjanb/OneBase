"use client";

import { useEffect, useState } from "react";
import { Download, FileSpreadsheet, Loader2 } from "lucide-react";
import { useT } from "@/i18n/client";
import type { T } from "@/i18n/core";
import { bff } from "@/lib/bff";
import { reportDownloadUrl, reportSourceText, type ReportCell, type ReportFormat, type ReportRef, type ReportSheet, type ReportView } from "@/lib/consultant";

/**
 * Названия листов и колонок пишет модель — они как есть; сервер подставляет свои, если модель их не дала
 * («Лист 2», «Колонка 3», «Значение») — эти переводятся.
 */
function nameText(name: string, t: T): string {
  const n = /^(Лист|Колонка) (\d+)$/.exec(name);
  if (n) return n[1] === "Лист" ? t("отчёт::Лист {n}", { n: n[2] }) : t("отчёт::Колонка {n}", { n: n[2] });
  return name === "Значение" ? t(name) : name;
}

/** Сколько строк листа показывать в чате: остальное — в файле Excel. */
const PreviewRows = 100;

const summable = (f: ReportFormat) => f === "integer" || f === "number" || f === "kg" || f === "money";

/** Ячейка как в Excel: целые, кг и сумы — с разрядами, проценты — «27,8 %», даты — дд.мм.гггг. */
function cellText(value: ReportCell, format: ReportFormat, t: T): string {
  if (value == null) return "";
  if (typeof value === "boolean") return value ? t("да") : t("нет");
  if (typeof value === "number") {
    if (format === "percent") return `${value.toLocaleString("ru-RU", { maximumFractionDigits: 1 })} %`;
    const digits = format === "number" ? 2 : format === "text" || format === "date" ? 2 : 0;
    return value.toLocaleString("ru-RU", { maximumFractionDigits: digits });
  }
  if (format === "date") {
    const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
    if (m) return `${m[3]}.${m[2]}.${m[1]}`;
  }
  return value;
}

const plural = (n: number, forms: [string, string, string]) => {
  const m10 = n % 10;
  const m100 = n % 100;
  return forms[m10 === 1 && m100 !== 11 ? 0 : m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14) ? 1 : 2];
};

/**
 * Отчёт Excel в ответе консультанта: название, листы и строки, кнопка «Скачать Excel» и предпросмотр — вкладки листов,
 * первые 100 строк каждого с итогом, пояснение и источники. Данные загружаются при показе карточки.
 */
export function ReportCard({ report }: { report: ReportRef }) {
  const t = useT();
  const [view, setView] = useState<ReportView | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [active, setActive] = useState(0);

  useEffect(() => {
    let cancelled = false;
    bff<ReportView>(`ai/reports/${report.id}`)
      .then((v) => !cancelled && setView(v))
      .catch((e: Error) => !cancelled && setError(e.message));
    return () => {
      cancelled = true;
    };
  }, [report.id]);

  const sheet = view?.sheets[Math.min(active, (view?.sheets.length ?? 1) - 1)];

  return (
    <section className="mt-3 overflow-hidden rounded-2xl border border-line bg-surface" aria-label={t("Отчёт «{title}»", { title: report.title })}>
      <header className="flex flex-wrap items-center gap-3 border-b border-line px-4 py-3">
        <span className="grid size-9 shrink-0 place-items-center rounded-xl bg-ok-soft text-ok">
          <FileSpreadsheet className="size-5" strokeWidth={1.75} />
        </span>
        <div className="min-w-0 flex-1">
          <div className="truncate text-sm font-semibold text-ink">{report.title}</div>
          <div className="text-xs text-ink-3">
            Excel · {report.sheetCount} {plural(report.sheetCount, [t("лист"), t("листа"), t("листов")])} · {report.rowCount.toLocaleString("ru-RU")}{" "}
            {plural(report.rowCount, [t("строка"), t("строки"), t("строк")])}
          </div>
        </div>
        <a
          href={reportDownloadUrl(report)}
          download
          className="inline-flex h-9 shrink-0 items-center gap-2 rounded-xl border border-transparent bg-accent px-3.5 text-sm font-medium text-white hover:bg-accent-strong max-sm:w-full max-sm:justify-center"
        >
          <Download className="size-4" />
          {t("Скачать Excel")}
        </a>
      </header>

      {view && view.sheets.length > 1 && (
        <nav className="no-scrollbar flex gap-1 overflow-x-auto border-b border-line px-3 py-2" aria-label={t("Листы отчёта")}>
          {view.sheets.map((s, i) => (
            <button
              key={`${s.name}-${i}`}
              type="button"
              onClick={() => setActive(i)}
              aria-pressed={i === active}
              className={`shrink-0 whitespace-nowrap rounded-lg px-3 py-1.5 text-xs font-medium transition-colors ${
                i === active ? "bg-accent-soft text-accent-strong" : "text-ink-2 hover:bg-muted hover:text-ink"
              }`}
            >
              {nameText(s.name, t)}
              <span className="ml-1.5 text-ink-3">{s.rows.length}</span>
            </button>
          ))}
        </nav>
      )}

      {error ? (
        <div className="px-4 py-3 text-sm text-bad">{t("Предпросмотр не загрузился: {error}. Файл можно скачать кнопкой выше.", { error: t(error) })}</div>
      ) : !view || !sheet ? (
        <div className="flex items-center gap-2 px-4 py-6 text-sm text-ink-3">
          <Loader2 className="size-4 animate-spin" />
          {t("Загружаю предпросмотр…")}
        </div>
      ) : (
        <SheetPreview sheet={sheet} sources={view.sources} />
      )}
    </section>
  );
}

function SheetPreview({ sheet, sources }: { sheet: ReportSheet; sources: string[] }) {
  const t = useT();
  const rows = sheet.rows.slice(0, PreviewRows);
  const numeric = sheet.columns.map((c) => c.format !== "text" && c.format !== "date");
  // Итог — по всем строкам листа, как формула СУММ в файле.
  const totals = sheet.total
    ? sheet.columns.map((c, i) => (summable(c.format) ? sheet.rows.reduce((sum, r) => sum + (typeof r[i] === "number" ? (r[i] as number) : 0), 0) : null))
    : null;
  const label = totals ? totals.findIndex((x) => x == null) : -1;

  return (
    <div>
      <div className="max-h-96 overflow-auto">
        <table className="w-full min-w-max border-collapse text-xs">
          <thead className="sticky top-0 z-10 bg-muted-solid">
            <tr>
              {sheet.columns.map((c, i) => (
                <th key={i} scope="col" className={`border-b border-line px-3 py-2 font-semibold text-ink-2 ${numeric[i] ? "text-right" : "text-left"}`}>
                  {nameText(c.header, t)}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((r, ri) => (
              <tr key={ri} className="border-b border-line last:border-0">
                {sheet.columns.map((c, i) => (
                  <td key={i} className={`whitespace-nowrap px-3 py-1.5 text-ink ${numeric[i] ? "text-right tabular-nums" : ""}`}>
                    {cellText(r[i] ?? null, c.format, t)}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
          {totals && (
            <tfoot className="sticky bottom-0 bg-muted-solid">
              <tr>
                {sheet.columns.map((c, i) => (
                  <td key={i} className={`border-t border-line px-3 py-2 font-semibold text-ink ${numeric[i] ? "text-right tabular-nums" : ""}`}>
                    {totals[i] != null ? cellText(totals[i], c.format, t) : i === label ? t("Итого") : ""}
                  </td>
                ))}
              </tr>
            </tfoot>
          )}
        </table>
        {sheet.rows.length === 0 && <div className="px-4 py-4 text-sm text-ink-3">{t("На листе нет строк.")}</div>}
      </div>
      {(sheet.rows.length > PreviewRows || sheet.note || sources.length > 0) && (
        <div className="space-y-1 border-t border-line px-4 py-2.5 text-xs text-ink-3">
          {sheet.rows.length > PreviewRows && (
            <div>
              {t("Показаны первые {n} строк из {total} — все строки в файле Excel.", { n: PreviewRows, total: sheet.rows.length.toLocaleString("ru-RU") })}
            </div>
          )}
          {sheet.note && <div>{sheet.note}</div>}
          {sources.length > 0 && <div>{t("Источники: {list}", { list: sources.map((s) => reportSourceText(s, t)).join("; ") })}</div>}
        </div>
      )}
    </div>
  );
}
