"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Building2, RefreshCw, Users } from "lucide-react";
import { useT } from "@/i18n/client";
import { periods, type PeriodKey } from "@/lib/dashboard.mock";

const selectClass =
  "h-[38px] appearance-auto rounded-[10px] border border-line bg-surface pl-9 pr-3 text-[13px] font-medium text-ink-2 transition-colors duration-150 hover:border-line-strong focus:border-accent focus:outline-none focus-visible:ring-2 focus-visible:ring-accent/40";

/**
 * Панель фильтров: период (сегмент-контрол), отделы и сотрудники (списки), справа — свежесть данных и обновление. Выбор пока
 * хранится в состоянии страницы: данные — из макета, при подключении API период и фильтры уйдут параметрами запроса.
 */
export function FilterBar({ departments, people = [], updatedMinutesAgo }: { departments: string[]; people?: string[]; updatedMinutesAgo: number }) {
  const t = useT();
  const router = useRouter();
  const [period, setPeriod] = useState<PeriodKey>("30d");
  const [refreshing, setRefreshing] = useState(false);

  const refresh = () => {
    setRefreshing(true);
    router.refresh();
    window.setTimeout(() => setRefreshing(false), 800);
  };

  return (
    <div className="flex flex-wrap items-center gap-2.5">
      <div role="radiogroup" aria-label={t("Период")} className="flex gap-0.5 rounded-[10px] border border-line bg-surface p-[3px]">
        {periods.map((p) => {
          const active = p.key === period;
          return (
            <button
              key={p.key}
              type="button"
              role="radio"
              aria-checked={active}
              onClick={() => setPeriod(p.key)}
              className={`h-[30px] rounded-[7px] px-3 text-[12.5px] transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent ${
                active ? "border border-line-strong bg-muted font-semibold text-ink" : "font-medium text-ink-3 hover:text-ink"
              }`}
            >
              {t(p.label)}
            </button>
          );
        })}
      </div>

      <label className="relative block">
        <Building2 className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" strokeWidth={1.75} />
        <select className={selectClass} aria-label={t("Отдел")} defaultValue="">
          <option value="">{t("Все отделы")}</option>
          {departments.map((d) => (
            <option key={d} value={d}>
              {t(d)}
            </option>
          ))}
        </select>
      </label>
      <label className="relative block">
        <Users className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" strokeWidth={1.75} />
        <select className={selectClass} aria-label={t("Сотрудник")} defaultValue="">
          <option value="">{t("Все сотрудники")}</option>
          {people.map((p) => (
            <option key={p} value={p}>
              {p}
            </option>
          ))}
        </select>
      </label>

      <div className="ml-auto flex items-center gap-2.5 text-[12.5px] text-ink-3">
        <span aria-hidden className="size-[7px] rounded-full bg-ok shadow-[0_0_8px_var(--color-ok)]" />
        <span>{t("Данные обновлены {n} мин назад", { n: updatedMinutesAgo })}</span>
        <button
          type="button"
          onClick={refresh}
          aria-label={t("Обновить данные")}
          className="grid size-7 place-items-center rounded-[7px] border border-line text-ink-3 transition-colors duration-150 hover:bg-muted hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
        >
          <RefreshCw className={`size-3.5 ${refreshing ? "animate-spin" : ""}`} strokeWidth={1.75} />
        </button>
      </div>
    </div>
  );
}
