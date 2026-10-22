"use client";

import { useT } from "@/i18n/client";
import type { PeriodFilter } from "@/lib/data-sources";

const input = "h-9 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink focus:border-accent/60 focus:outline-none";
const label = "mb-1 block text-xs font-medium text-ink-2";

/** Месяц «ГГГГ-ММ» со сдвигом от текущего. */
function monthKey(shift: number) {
  const d = new Date();
  d.setDate(1);
  d.setMonth(d.getMonth() + shift);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}`;
}

/**
 * Какие месяцы листа брать: текущий год, текущий месяц, последние N месяцев, диапазон или все. «Текущий» сдвигается сам (по Ташкенту на
 * момент синхронизации). Строки других месяцев не записываются и не удаляются.
 */
export function PeriodSelect({ value, onChange }: { value: PeriodFilter | null | undefined; onChange: (p: PeriodFilter) => void }) {
  const t = useT();
  const mode = value?.mode ?? "all";
  return (
    <div className="grid gap-3 sm:grid-cols-4">
      <div className="sm:col-span-2">
        <label className={label}>{t("Какие месяцы брать из листа")}</label>
        <select
          aria-label={t("Период")}
          value={mode}
          onChange={(e) => {
            const m = e.target.value as PeriodFilter["mode"];
            onChange(m === "last" ? { mode: m, months: value?.months ?? 3 } : m === "range" ? { mode: m, from: value?.from ?? monthKey(-2), to: value?.to ?? monthKey(0) } : { mode: m });
          }}
          className={input}
        >
          <option value="year">{t("текущий год (январь–декабрь, с 1 января — новый год)")}</option>
          <option value="current">{t("текущий месяц")}</option>
          <option value="last">{t("последние N месяцев, включая текущий")}</option>
          <option value="range">{t("период: с … по …")}</option>
          <option value="all">{t("все месяцы листа (за раз — не больше 24)")}</option>
        </select>
      </div>
      {mode === "last" && (
        <div>
          <label className={label}>{t("Месяцев")}</label>
          <input type="number" min={1} max={24} value={value?.months ?? 3} onChange={(e) => onChange({ mode, months: Math.min(24, Math.max(1, Number(e.target.value) || 1)) })} className={input} />
        </div>
      )}
      {mode === "range" && (
        <>
          <div>
            <label className={label}>{t("С месяца")}</label>
            <input type="month" value={value?.from ?? ""} onChange={(e) => onChange({ ...value!, mode, from: e.target.value })} className={input} />
          </div>
          <div>
            <label className={label}>{t("По месяц")}</label>
            <input type="month" value={value?.to ?? ""} onChange={(e) => onChange({ ...value!, mode, to: e.target.value })} className={input} />
          </div>
        </>
      )}
      <p className="text-xs text-ink-3 sm:col-span-4">{t("Строки других месяцев не записываются и не удаляются — история в OneBase остаётся как есть.")}</p>
    </div>
  );
}
