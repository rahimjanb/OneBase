"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { useT } from "@/i18n/client";
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
 * выбрать нельзя: сервер взял бы планы ТП из Linko. Если такой вид уже выбран (ссылка, смена месяца), кнопка остаётся нажатой. Нажатие на кнопку
 * плана, которого нет, открывает подсказку: чего не хватает и где это завести (подсказка в title на телефоне не видна).
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
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const available = useAvailablePlans(year, month);
  const exit = useMonthExit(pathname);
  // Подсказка о недостающем плане — для месяца, в котором её открыли: смена месяца её закрывает.
  const [hint, setHint] = useState<{ plan: PlanKind; month: string } | null>(null);
  const shown = hint && hint.month === `${year}-${month}` && available != null && !available.includes(hint.plan) ? hint.plan : null;

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
    <div className="relative flex flex-wrap items-center gap-2 max-lg:w-full">
      <select
        aria-label={t("Период")}
        value={`${year}-${month}`}
        onChange={(e) => {
          const [y, m] = e.target.value.split("-");
          changeMonth(y, m);
        }}
        className="h-9 rounded-lg border border-line bg-surface px-3 text-sm font-medium text-ink max-lg:h-11 max-lg:flex-1"
      >
        {options.map((m) => (
          <option key={`${m.year}-${m.month}`} value={`${m.year}-${m.month}`}>
            {monthLabel(m.year, m.month, t)}
          </option>
        ))}
      </select>
      {showPlan && (
        <div className="flex h-9 overflow-hidden rounded-lg border border-line text-sm max-lg:h-11" role="group" aria-label={t("План")}>
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
                aria-expanded={missing ? shown === p.key : undefined}
                title={missing ? t("{missing} — план ТП из Linko", { missing: planMissingText(p.key, t) }) : undefined}
                onClick={() => {
                  if (missing) setHint(shown === p.key ? null : { plan: p.key, month: `${year}-${month}` });
                  else go({ plan: p.key === "rop" ? null : p.key });
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
                {t(p.label)}
              </button>
            );
          })}
        </div>
      )}
      {showPlan && shown && (
        <div
          role="status"
          className="absolute left-0 top-full z-30 mt-2 w-[min(26rem,calc(100vw-2rem))] rounded-lg border border-line bg-surface-solid p-3 text-sm text-ink-2 shadow-lg"
        >
          <p>
            {shown === "factory"
              ? t("Плана «Завод» на {month} в OneBase нет — выполнение считается по планам ТП из Linko. Задайте итоги завода по категориям в «Настройках продаж» (по регионам они разложатся по плану РОП) или привяжите лист с планом «Завод» по регионам в «Источниках данных».", {
                  month: monthLabel(year, month, t).toLowerCase(),
                })
              : t("Плана РОП на {month} в OneBase нет — выполнение считается по планам ТП из Linko. Загрузите его: привязка Google Sheets в «Источниках данных» или AI Data Manager.", {
                  month: monthLabel(year, month, t).toLowerCase(),
                })}
          </p>
          <div className="mt-2 flex flex-wrap items-center gap-3">
            {shown === "factory" && (
              <Link href="/sales/setup" className="font-medium text-accent hover:underline">
                {t("Настройки продаж")}
              </Link>
            )}
            <Link href="/settings/data-sources" className="font-medium text-accent hover:underline">
              {t("Источники данных")}
            </Link>
            <button type="button" className="ml-auto text-ink-3 hover:text-ink" onClick={() => setHint(null)}>
              {t("Закрыть")}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
