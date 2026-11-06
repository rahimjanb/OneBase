"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState, useTransition } from "react";
import { FileSpreadsheet, FileText } from "lucide-react";
import { useT } from "@/i18n/client";
import { periodOf, reportCell, type ReportInfo, type ReportPreview, type RequestCategory, type RequestDepartment } from "@/lib/procurement";
import { todayInTashkent } from "@/lib/tasks";

const field = "h-9 rounded-lg border border-line bg-surface px-2.5 text-sm text-ink focus:border-accent focus:outline-none";
const numeric = new Set(["integer", "number", "kg", "money", "percent"]);

/**
 * Отчёты снабжения: слева — какой отчёт, сверху одной строкой — период и фильтры (отдел, категория, сотрудник, поставщик); ниже —
 * предпросмотр листов (первые 100 строк) и выгрузка того же отчёта в Excel и PDF.
 */
export function ReportsView({
  kinds,
  kind,
  preview,
  categories,
  departments,
  employees,
  suppliers,
  base = "procurement",
  warehouses = [],
}: {
  /** Чьи отчёты: снабжения или производства (те же предпросмотр, Excel и PDF). */
  base?: "procurement" | "production";
  /** Склады — фильтр отчётов производства. */
  warehouses?: { id: string; name: string }[];
  kinds: ReportInfo[];
  kind: string;
  preview: ReportPreview;
  categories: RequestCategory[];
  departments: RequestDepartment[];
  employees: { id: string; name: string }[];
  suppliers: { id: string; name: string }[];
}) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [pending, start] = useTransition();
  const today = todayInTashkent();
  const month = periodOf("month", today);
  const [from, setFrom] = useState(params.get("from") ?? month.from);
  const [to, setTo] = useState(params.get("to") ?? month.to);
  useEffect(() => {
    setFrom(params.get("from") ?? month.from);
    setTo(params.get("to") ?? month.to);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [params]);
  const apply = (patch: Record<string, string | null>) => {
    const next = new URLSearchParams(params.toString());
    for (const [k, v] of Object.entries(patch)) {
      if (v) next.set(k, v);
      else next.delete(k);
    }
    start(() => router.push(`${pathname}?${next}`, { scroll: false }));
  };
  const query = new URLSearchParams();
  for (const key of ["from", "to", "department", "category", "employee", "supplier", "warehouse"]) {
    const v = params.get(key);
    if (v) query.set(key, v);
  }
  const download = (ext: "xlsx" | "pdf") => `/bff/api/${base}/reports/${encodeURIComponent(kind)}/${ext}${query.size ? `?${query}` : ""}`;
  const select = (key: string, label: string, options: { id: string; name: string }[]) => (
    <select value={params.get(key) ?? ""} onChange={(e) => apply({ [key]: e.target.value || null })} aria-label={t(label)} className={`${field} max-w-[220px]`}>
      <option value="">{t(label)}</option>
      {options.map((o) => (
        <option key={o.id} value={o.id}>
          {o.name}
        </option>
      ))}
    </select>
  );

  return (
    <div className="grid gap-4 lg:grid-cols-[260px_minmax(0,1fr)]">
      <select value={kind} onChange={(e) => apply({ kind: e.target.value })} aria-label={t("Отчёт")} className={`${field} h-10 w-full lg:hidden`}>
        {kinds.map((k) => (
          <option key={k.key} value={k.key}>
            {t(k.title)}
          </option>
        ))}
      </select>
      <nav aria-label={t("Отчёты")} className="hidden space-y-1 lg:block">
        {kinds.map((k) => (
          <button
            key={k.key}
            type="button"
            onClick={() => apply({ kind: k.key })}
            aria-current={k.key === kind ? "page" : undefined}
            className={`block w-full rounded-xl border px-3 py-2.5 text-left ${k.key === kind ? "border-accent/50 bg-accent-soft" : "border-transparent hover:bg-muted"}`}
          >
            <span className="block text-sm font-semibold text-ink">{t(k.title)}</span>
            <span className="block text-xs text-ink-3">{t(k.description)}</span>
          </button>
        ))}
      </nav>

      <div className={`min-w-0 space-y-4 transition-opacity ${pending ? "opacity-60" : ""}`}>
        <div className="flex flex-wrap items-center gap-2">
          {(["month", "prev", "quarter", "year"] as const).map((p) => {
            const r = periodOf(p, today);
            const on = r.from === from && r.to === to;
            return (
              <button
                key={p}
                type="button"
                onClick={() => apply({ from: r.from, to: r.to })}
                aria-pressed={on}
                className={`h-9 rounded-lg border px-3 text-sm font-medium ${on ? "border-accent bg-accent text-white" : "border-line bg-surface text-ink hover:bg-muted"}`}
              >
                {t({ month: "Этот месяц", prev: "Прошлый месяц", quarter: "3 месяца", year: "Год" }[p])}
              </button>
            );
          })}
          <input type="date" value={from} max={to} onChange={(e) => setFrom(e.target.value)} onBlur={() => from && apply({ from })} aria-label={t("С")} className={field} />
          <input type="date" value={to} min={from} max={today} onChange={(e) => setTo(e.target.value)} onBlur={() => to && apply({ to })} aria-label={t("По")} className={field} />
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {departments.length > 0 && select("department", "Все отделы", departments.map((d) => ({ id: d.id, name: d.name })))}
          {categories.length > 0 && select("category", "Все категории", categories.map((c) => ({ id: c.id, name: c.name })))}
          {employees.length > 0 && select("employee", "Все сотрудники", employees)}
          {suppliers.length > 0 && select("supplier", "Все поставщики", suppliers)}
          {warehouses.length > 1 && select("warehouse", "Все склады", warehouses)}
          <span className="ml-auto inline-flex gap-2">
            <a href={download("xlsx")} className="inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-accent-strong">
              <FileSpreadsheet className="size-4" aria-hidden /> Excel
            </a>
            <a href={download("pdf")} className="inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3.5 text-sm font-medium text-ink hover:bg-muted">
              <FileText className="size-4" aria-hidden /> PDF
            </a>
          </span>
        </div>

        <div>
          <h2 className="text-lg font-semibold text-ink">{preview.title}</h2>
          {preview.sources.length > 0 && <p className="text-xs text-ink-3">{preview.sources.join(" · ")}</p>}
        </div>
        {preview.sheets.map((s) => (
          <section key={s.name} className="rounded-2xl border border-line bg-surface p-4">
            <h3 className="mb-2 flex items-baseline justify-between gap-3 text-base font-semibold text-ink">
              {s.name}
              <span className="text-xs font-normal text-ink-3">
                {s.totalRows > s.rows.length ? t("показано {a} из {b} строк — полностью в файле", { a: s.rows.length, b: s.totalRows }) : t("строк: {n}", { n: s.totalRows })}
              </span>
            </h3>
            {s.rows.length === 0 ? (
              <p className="text-sm text-ink-3">{t("По этому фильтру данных нет.")}</p>
            ) : (
              <div className="max-h-[560px] overflow-auto">
                <table className="w-full min-w-[640px] text-sm">
                  <thead className="sticky top-0 bg-surface">
                    <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                      {s.columns.map((c) => (
                        <th key={c.header} className={`whitespace-nowrap py-2 pr-3 font-semibold ${numeric.has(c.format) ? "text-right" : ""}`}>
                          {c.header}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {s.rows.map((row, i) => (
                      <tr key={i} className="border-b border-line align-top last:border-0">
                        {s.columns.map((c, j) => (
                          <td key={c.header} className={`py-1.5 pr-3 ${numeric.has(c.format) ? "whitespace-nowrap text-right tabular-nums" : j === 0 || c.format === "date" ? "whitespace-nowrap text-ink" : "text-ink-2"}`}>
                            {reportCell(row[j] ?? null, c.format)}
                          </td>
                        ))}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            {s.note && <p className="mt-2 text-xs text-ink-3">{s.note}</p>}
          </section>
        ))}
      </div>
    </div>
  );
}
