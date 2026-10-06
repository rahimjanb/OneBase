"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useMonthExit } from "./MonthExit";
import { useAvailablePlans } from "./PlanAvailability";
import { monthLabel, planMissingText } from "@/lib/sales/format";
import { planOf } from "@/lib/sales/query";
import type { PlanKind, SalesMonth } from "@/lib/sales/types";

const plans: { key: PlanKind; label: string }[] = [
  { key: "rop", label: "План РОП" },
  { key: "factory", label: "План «Завод»" },
];

/**
 * Период (месяц) и план (РОП / «Завод»). Меняют параметры адреса — сервер пересчитывает страницу.
 * План — знаменатель выполнения у республики, РМ и региона. Вид плана, которого на месяц нет (сообщает страница — period.availablePlans),
 * выбрать нельзя: сервер взял бы планы ТП из Linko. Если такой вид уже выбран (ссылка, смена месяца), кнопка остаётся нажатой и объясняет.
 * Смена месяца со страницы ТП или магазина уходит к региону (нет региона — к республике), план сохраняется (DOC-filters §12; страница сообщает
 * адрес через ReportMonthExit).
 */
export function SalesToolbar({
  months,
  year,
  month,
  plan,
  showPlan,
}: {
  months: SalesMonth[];
  year: number;
  month: number;
  plan: PlanKind;
  /** Переключатель только на страницах с планом подразделений (см. usesPlan). */
  showPlan: boolean;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const available = useAvailablePlans(year, month);
  const exit = useMonthExit(pathname);

  const go = (changes: Record<string, string | null>, path = pathname) => {
    const next = new URLSearchParams(params);
    for (const [key, value] of Object.entries(changes)) {
      if (value == null) next.delete(key);
      else next.set(key, value);
    }
    // План в адресе — только plan=factory (см. planOf): rop и неизвестные значения не переносятся.
    if (planOf(next.get("plan")) === "factory") next.set("plan", "factory");
    else next.delete("plan");
    router.push(`${path}?${next}`);
  };

  const changeMonth = (y: string, m: string) => {
    // Смена месяца сбрасывает период календаря (from/to) и разбор дня первички (day, dealer).
    if (exit) {
      // Со страницы ТП или магазина — к региону: только период и план.
      const next = new URLSearchParams({ year: y, month: m });
      if (planOf(params.get("plan")) === "factory") next.set("plan", "factory");
      router.push(`${exit}?${next}`);
      return;
    }
    go({ year: y, month: m, from: null, to: null, day: null, dealer: null });
  };

  const options = months.some((m) => m.year === year && m.month === month) ? months : [{ year, month }, ...months];

  return (
    <div className="flex flex-wrap items-center gap-2 max-lg:w-full">
      <select
        aria-label="Период"
        value={`${year}-${month}`}
        onChange={(e) => {
          const [y, m] = e.target.value.split("-");
          changeMonth(y, m);
        }}
        className="h-9 rounded-lg border border-line bg-surface px-3 text-sm font-medium text-ink max-lg:h-11 max-lg:flex-1"
      >
        {options.map((m) => (
          <option key={`${m.year}-${m.month}`} value={`${m.year}-${m.month}`}>
            {monthLabel(m.year, m.month)}
          </option>
        ))}
      </select>
      {showPlan && (
        <div className="flex h-9 overflow-hidden rounded-lg border border-line text-sm max-lg:h-11" role="group" aria-label="План">
          {plans.map((p) => {
            const selected = plan === p.key;
            const missing = available != null && !available.includes(p.key);
            // aria-disabled, а не disabled: у отключённой кнопки браузер может не показать подсказку, почему её нельзя выбрать.
            const blocked = missing && !selected;
            return (
              <button
                key={p.key}
                type="button"
                aria-pressed={selected}
                aria-disabled={blocked || undefined}
                title={missing ? `${planMissingText(p.key)} — план ТП из Linko` : undefined}
                onClick={() => {
                  if (!blocked) go({ plan: p.key === "rop" ? null : p.key });
                }}
                className={`whitespace-nowrap px-3 font-medium ${
                  selected
                    ? missing
                      ? "bg-accent-soft text-accent-strong"
                      : "bg-accent text-white"
                    : missing
                      ? "cursor-not-allowed bg-surface text-ink-3"
                      : "bg-surface text-ink hover:bg-muted"
                }`}
              >
                {p.label}
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}
