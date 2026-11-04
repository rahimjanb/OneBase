"use client";

import { AlertTriangle } from "lucide-react";
import { useT } from "@/i18n/client";
import { dateRu, quantity, type StockImportResult } from "@/lib/procurement";
import { FileImport, ImportColumns, ImportErrors } from "./FileImport";

const kinds = { Opening: "остаток", In: "приход", Out: "выдача", WriteOff: "списание" } as const;

/** «Импорт из Excel» в «Движениях» (кладовщик): первая загрузка журнала склада — начальные остатки, следующие — только новые строки. */
export function StockImport() {
  return (
    <FileImport<StockImportResult>
      endpoint="/bff/api/procurement/stock/import"
      checkTitle="Проверка файла"
      doneTitle="Загрузка движений"
      hint="Журнал склада из Excel: дата, приход или расход, артикул или название, единица, количество, цех или склад — заголовки по-русски или по-узбекски, столбцы узнаются сами. Первая загрузка заводит начальные остатки, следующие загрузки того же файла проводят только новые строки. Расход в цех — выдача отделу (нового цеха — новый отдел), «Утилизация» и «Испарения» — списание."
      ready={(r) => r.posted > 0}
      report={(r) => <StockImportReport result={r} />}
    />
  );
}

function StockImportReport({ result: r }: { result: StockImportResult }) {
  const t = useT();
  const opening = r.mode === "opening";
  const docs = [
    r.receipts > 0 && t("приходов: {n}", { n: r.receipts }),
    r.issues > 0 && t("выдач: {n}", { n: r.issues }),
    r.writeOffs > 0 && t("списаний: {n}", { n: r.writeOffs }),
  ].filter(Boolean);
  return (
    <div className="space-y-4 text-sm">
      {r.unknownFile && (
        <p className="flex gap-2 rounded-xl border border-warn/30 bg-warn/10 px-3 py-2 text-ink">
          <AlertTriangle className="mt-0.5 size-4 shrink-0 text-warn" aria-hidden />
          {t("Из этого файла раньше не загружалось ни одной строки — все {n} строк будут проведены как новые движения. Если это другой журнал склада, проверьте, что так и нужно.", { n: r.new })}
        </p>
      )}
      <div className="space-y-1">
        {opening ? (
          <>
            <p className="font-medium text-ink">
              {r.dryRun ? t("Первая загрузка — начальные остатки по файлу.") : t("Начальные остатки занесены.")}
            </p>
            <p className="text-ink-2">
              {t("Строк в файле: {a}, учтено: {b}. Позиций: {c}, новых в каталоге: {d}; остаток изменится у {e}.", {
                a: r.rows,
                b: r.posted,
                c: r.items,
                d: r.newItems,
                e: r.adjustments,
              })}
            </p>
            {r.lastDate && (
              <p className="text-ink-3">{t("Движения по {date} остаются в Excel; следующие загрузки этого файла добавят только новые строки.", { date: dateRu(r.lastDate) })}</p>
            )}
          </>
        ) : (
          <>
            <p className="font-medium text-ink">
              {r.dryRun ? t("Новых строк: {a} (загружены раньше: {b}).", { a: r.new, b: r.known }) : t("Проведено строк: {n}.", { n: r.posted })}
            </p>
            {docs.length > 0 && <p className="text-ink-2">{t("Документы: {list}.", { list: docs.join(", ") })}</p>}
            {r.firstDate && r.lastDate && <p className="text-ink-3">{t("Движения с {a} по {b}.", { a: dateRu(r.firstDate), b: dateRu(r.lastDate) })}</p>}
          </>
        )}
        {(r.skipped > 0 || r.clamped > 0) && (
          <p className="text-ink-3">
            {[r.skipped > 0 && t("пропущено строк: {n}", { n: r.skipped }), r.clamped > 0 && t("заведены нулём (по файлу меньше нуля): {n}", { n: r.clamped })]
              .filter(Boolean)
              .join(" · ")}
          </p>
        )}
        {r.dryRun && <p className="text-ink-3">{t("Это проверка — на складе пока ничего не изменилось.")}</p>}
      </div>
      {r.newDepartments.length > 0 && (
        <p className="text-ink-2">
          <span className="text-ink">{t("Новые отделы ({n}):", { n: r.newDepartments.length })}</span> {r.newDepartments.join(", ")}
        </p>
      )}
      <ImportErrors errors={r.errors} />
      {r.dryRun && <ImportColumns columns={r.columns} />}
      {r.dryRun && r.sample.length > 0 && (
        <section>
          <h3 className="mb-1.5 text-xs font-semibold text-ink-3">{opening ? t("Начальные остатки — первые позиции") : t("Первые строки")}</h3>
          <ul className="divide-y divide-line rounded-xl border border-line">
            {r.sample.map((x, i) => (
              <li key={`${x.sku}-${i}`} className="flex items-baseline justify-between gap-3 px-3 py-2">
                <span className="min-w-0">
                  <span className="block truncate text-ink">
                    <span className="tabular-nums text-ink-3">{x.sku}</span> {x.name}
                  </span>
                  {!opening && (
                    <span className="block truncate text-xs text-ink-3">
                      {[dateRu(x.date), t(kinds[x.kind]), x.place].filter(Boolean).join(" · ")}
                    </span>
                  )}
                </span>
                <span className="shrink-0 tabular-nums text-ink">
                  {quantity(x.quantity)} {x.unitCode}
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}
