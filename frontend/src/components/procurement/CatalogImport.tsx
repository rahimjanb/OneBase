"use client";

import { useT } from "@/i18n/client";
import { qualityControlLabel, type QualityControl } from "@/lib/procurement";
import { FileImport, ImportColumns, ImportErrors } from "./FileImport";

/** Итог загрузки каталога: добавится, обновится, без изменений, пропущено, ошибки строк, как будут записаны первые позиции. */
export type CatalogImportResult = {
  dryRun: boolean;
  columns: string[];
  created: number;
  updated: number;
  unchanged: number;
  skipped: number;
  errors: string[];
  sample: { row: number; sku: string; name: string; unitCode: string; category: string; shelfLifeDays: number | null; qualityControl: QualityControl; tracksBatches: boolean; isNew: boolean }[];
};

/**
 * «Загрузить каталог» на «Складе» (кладовщик, снабжение): позиции из Excel или CSV по артикулу — новые добавятся, найденные обновятся;
 * пустая ячейка поле не меняет. Сначала проверка без сохранения, затем загрузка.
 */
export function CatalogImport() {
  const t = useT();
  return (
    <FileImport<CatalogImportResult>
      endpoint="/bff/api/procurement/items/import"
      label="Загрузить каталог"
      checkTitle="Проверка каталога"
      doneTitle="Загрузка каталога"
      hint="Каталог материалов из Excel: артикул, наименование, единица, категория, минимум, «по партиям», срок хранения (дней), контроль качества (нет, документы, лаборатория), условия хранения. Позиции находятся по артикулу: новые добавляются, найденные обновляются; пустая ячейка поле не меняет. Остатки не меняются — их вносят приходом или загрузкой журнала."
      ready={(r) => r.created + r.updated > 0}
      report={(r) => (
        <div className="space-y-3 text-sm">
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
            {[
              { label: r.dryRun ? "Добавится" : "Добавлено", value: r.created },
              { label: r.dryRun ? "Обновится" : "Обновлено", value: r.updated },
              { label: "Без изменений", value: r.unchanged },
              { label: "Пропущено", value: r.skipped, tone: r.skipped > 0 ? "text-warn" : "" },
            ].map((x) => (
              <div key={x.label} className="rounded-xl bg-muted px-3 py-2">
                <div className={`text-lg font-semibold tabular-nums text-ink ${x.tone ?? ""}`}>{x.value}</div>
                <div className="text-xs text-ink-3">{t(x.label)}</div>
              </div>
            ))}
          </div>
          <ImportColumns columns={r.columns} />
          <ImportErrors errors={r.errors} />
          {r.sample.length > 0 && (
            <ul className="divide-y divide-line rounded-xl border border-line text-xs">
              {r.sample.map((s) => (
                <li key={s.row} className="flex items-center justify-between gap-2 px-3 py-1.5">
                  <span className="min-w-0 truncate text-ink">
                    <span className="tabular-nums text-ink-3">{s.sku}</span> {s.name}
                  </span>
                  <span className="shrink-0 text-ink-3">
                    {[s.unitCode, s.category, s.tracksBatches ? t("по партиям") : null, s.shelfLifeDays ? t("{n} дн.", { n: s.shelfLifeDays }) : null,
                      s.qualityControl !== "None" ? t(qualityControlLabel[s.qualityControl]) : null, s.isNew ? t("новая") : t("обновится")]
                      .filter(Boolean)
                      .join(" · ")}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    />
  );
}
