"use client";

// «Из чего складывается объём» региона по категориям — как вкладка региона в «Полевом контроле» (с v500). Все числа считает сервер
// (SalesAnalytics.VolumeOf): здесь только таблица. Колонки в кг — на лёгкой синей подложке, колонки АКБ — на зелёной, группы разделены линией.
import { useT } from "@/i18n/client";
import { levelClass, Note, Section } from "./bits";
import { planSourceText } from "./blocks";
import { kg, num, pct } from "@/lib/sales/format";
import type { VolumeRow, VolumeTable as Volume } from "@/lib/sales/types";

const kgCell = "bg-accent-soft/50 px-3 py-2 text-right tabular-nums";
const akbCell = "bg-ok-soft/50 px-3 py-2 text-right tabular-nums";
const groupStart = "border-l border-line-strong";

/** Доля категории в кг региона: число и полоска. */
function ShareCell({ share }: { share: number | null }) {
  return (
    <td className="px-3 py-2 text-right tabular-nums">
      <span className="inline-flex items-center justify-end gap-2">
        <span className="hidden h-1.5 w-14 overflow-hidden rounded-full bg-muted sm:inline-block" aria-hidden>
          <span className="block h-full rounded-full bg-accent" style={{ width: `${Math.max(0, Math.min(1, share ?? 0)) * 100}%` }} />
        </span>
        {pct(share, 1)}
      </span>
    </td>
  );
}

function Row({ row, kind }: { row: VolumeRow; kind: "category" | "group" | "total" }) {
  const t = useT();
  const name = row.categoryId == null ? t(row.name) : row.name;
  const weight = kind === "total" ? "font-semibold text-ink" : kind === "group" ? "italic text-ink-2" : "text-ink";
  // У строк-планов на несколько категорий АКБ не считается (точки по ним не делятся) — прочерк, а не ноль.
  const noAkb = kind === "group";
  return (
    <tr className={`${kind === "total" ? "border-t-2 border-line-strong" : "border-t border-line"} ${weight}`}>
      <th scope="row" className="px-3 py-2 text-left font-[inherit]">
        <span className="block min-w-[140px]">{name}</span>
        {kind === "group" && <span className="block text-[11px] not-italic text-ink-3">{t("план Linko на несколько категорий")}</span>}
      </th>
      <ShareCell share={row.share} />
      <td className={`${kgCell} ${groupStart}`}>{kg(row.planKg)}</td>
      <td className={kgCell}>{kg(row.factKg)}</td>
      <td className={kgCell}>
        <span className={row.execution != null ? levelClass(row.executionLevel) : "text-ink-3"}>{pct(row.execution)}</span>
      </td>
      <td className={kgCell}>{kg(row.forecastKg)}</td>
      <td className={kgCell}>
        <span className={row.forecastExecution != null ? levelClass(row.forecastLevel) : "text-ink-3"}>{pct(row.forecastExecution)}</span>
      </td>
      <td className={`${akbCell} ${groupStart}`}>{row.akbPlan != null ? num(row.akbPlan) : "—"}</td>
      <td className={akbCell}>{noAkb ? "—" : num(row.akb)}</td>
      <td className={akbCell}>{noAkb ? "—" : pct(row.akbCoverage, 1)}</td>
    </tr>
  );
}

export function VolumeTable({ volume, closed }: { volume: Volume | null | undefined; closed: boolean }) {
  const t = useT();
  if (!volume || (volume.rows.length === 0 && volume.planGroups.length === 0)) return null;
  const headBase = "px-3 py-2 text-[11px] font-semibold uppercase tracking-[0.06em] text-ink-3";
  const head = `${headBase} text-right`;
  return (
    <Section title={t("Из чего складывается объём")} hint={t("категории за месяц: доля в кг, план и факт, АКБ и охват")}>
      <div className="-mx-4 overflow-x-auto sm:mx-0">
        <table className="w-full min-w-[860px] border-collapse text-sm">
          <thead>
            <tr>
              <th className={`${headBase} text-left`} />
              <th className={head} />
              <th colSpan={5} className={`${headBase} ${groupStart} bg-accent-soft/50 text-center`}>
                {t("Объём, кг")}
              </th>
              <th colSpan={3} className={`${headBase} ${groupStart} bg-ok-soft/50 text-center`}>
                {t("АКБ")}
              </th>
            </tr>
            <tr className="border-t border-line">
              <th scope="col" className={`${headBase} text-left`}>{t("Категория")}</th>
              <th scope="col" className={head}>{t("Доля")}</th>
              <th scope="col" className={`${head} ${groupStart} bg-accent-soft/50`}>{t("План, кг")}</th>
              <th scope="col" className={`${head} bg-accent-soft/50`}>{t("Факт, кг")}</th>
              <th scope="col" className={`${head} bg-accent-soft/50`}>{t("Вып.")}</th>
              <th scope="col" className={`${head} bg-accent-soft/50`}>{t("Прогноз, кг")}</th>
              <th scope="col" className={`${head} bg-accent-soft/50`}>{t("Прогноз вып.")}</th>
              <th scope="col" className={`${head} ${groupStart} bg-ok-soft/50`}>{t("План АКБ")}</th>
              <th scope="col" className={`${head} bg-ok-soft/50`}>{t("АКБ")}</th>
              <th scope="col" className={`${head} bg-ok-soft/50`}>{t("Охват АКБ")}</th>
            </tr>
          </thead>
          <tbody>
            {volume.rows.map((r) => (
              <Row key={r.categoryId ?? r.name} row={r} kind="category" />
            ))}
            {volume.planGroups.map((r) => (
              <Row key={`group-${r.name}`} row={r} kind="group" />
            ))}
            <Row row={volume.total} kind="total" />
          </tbody>
        </table>
      </div>
      <Note>
        {t(
          "Доля — кг категории ÷ все кг региона. План — {source}; факт — все продажи категории в регионе, выполнение = факт ÷ план. Прогноз — факт по текущему темпу на конец месяца{closed}. План АКБ категории — показатели АКБ ТП из Linko по категории («АКБ Twizos»), в итоге — сумма планов АКБ ТП. АКБ — точки с чистой покупкой; охват АКБ = АКБ категории ÷ АКБ региона. Курсивом — планы Linko на несколько категорий: «Тотал» без Могуля и Twizos и планы вида «Могуль + Шоколад»; их факт — все продажи этих категорий.",
          { source: t(planSourceText[volume.planSource]), closed: closed ? t(" (у закрытого месяца прогноза нет)") : "" },
        )}
      </Note>
    </Section>
  );
}
