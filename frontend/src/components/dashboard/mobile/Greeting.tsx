"use client";

import { useEffect, useState } from "react";
import { Moon, Sun } from "lucide-react";
import { useT } from "@/i18n/client";
import { fmt } from "@/lib/dashboard-mobile";
import { homeClock, type HomeClock } from "@/lib/dashboardClock";
import { plural } from "@/lib/format";
import { useHomeFilters } from "./Filters";

/**
 * Приветствие: «ПЯТНИЦА · 30 ОКТЯБРЯ» с солнцем или луной, «Добрый день, {имя}» по времени суток и итог — выполнение плана и сколько
 * отделов (из выбранных в фильтре) требуют внимания. Время идёт: первая отрисовка — с сервера, дальше раз в минуту.
 */
export function Greeting({
  name,
  planShare,
  departments,
  initial,
  locale,
}: {
  name: string;
  planShare: number;
  departments: { code: string; status: "ok" | "attention" }[];
  initial: HomeClock;
  locale: string;
}) {
  const t = useT();
  const [filters] = useHomeFilters();
  const [clock, setClock] = useState(initial);

  useEffect(() => {
    const tick = () => setClock(homeClock(new Date(), locale));
    tick();
    const timer = window.setInterval(tick, 60_000);
    return () => window.clearInterval(timer);
  }, [locale]);

  const scope = filters.departments.length ? departments.filter((d) => filters.departments.includes(d.code)) : departments;
  const attention = scope.filter((d) => d.status === "attention").length;
  const Icon = clock.night ? Moon : Sun;

  return (
    <header className="flex flex-col gap-[6px] px-[4px]">
      <p className="flex items-center gap-[6px] text-[11px] font-semibold uppercase tracking-[0.8px] text-(--ts-muted)">
        <Icon aria-hidden className="size-[13px] text-(--ts-warning)" strokeWidth={2.2} />
        <span suppressHydrationWarning>{clock.label}</span>
      </p>
      <h1 className="text-[26px] font-bold leading-[1.15] tracking-[-0.5px] text-(--ts-text)">
        <span suppressHydrationWarning>{t(clock.greeting)}</span>
        {name ? `, ${name}` : ""}
      </h1>
      <p className="text-[14px] leading-[1.4] text-(--ts-muted)">
        {t("План выполнен на")} <span className="font-bold text-(--ts-success)">{fmt(planShare, 1)}%</span>
        {" · "}
        {attention > 0 ? (
          <span className="font-medium text-(--ts-warning)">
            {attention} {t(plural(attention, ["отдел — внимание", "отдела — внимание", "отделов — внимание"]))}
          </span>
        ) : (
          <span className="font-medium text-(--ts-success)">{t("все отделы в норме")}</span>
        )}
      </p>
    </header>
  );
}
