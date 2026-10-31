"use client";

import { useEffect, useState } from "react";
import { Moon, Sun } from "lucide-react";
import { useT } from "@/i18n/client";
import { headerClock, type HeaderClock } from "@/lib/dashboardClock";
import { shortDayLabel } from "@/lib/my-day";

/**
 * Шапка «Моего дня»: аватар с кольцом, строка даты («СУББОТА · 31 ОКТЯБРЯ · 10:24»), «Доброе утро, {Имя}» по времени суток Ташкента,
 * «должность · отдел · Фамилия Имя» и справа пилюля статуса дня (pill). На телефоне — аватар 46, приветствие 19/700, ниже строка даты и
 * пилюля. Время идёт: первая отрисовка — с сервера, дальше раз в 30 секунд.
 */
export function MyDayHeader({
  name,
  fullName,
  position,
  department,
  initials,
  initial,
  initialShort,
  locale,
  pill,
}: {
  name: string;
  fullName: string;
  position: string | null;
  department: string | null;
  initials: string;
  initial: HeaderClock;
  initialShort: string;
  locale: string;
  pill: React.ReactNode;
}) {
  const t = useT();
  const [clock, setClock] = useState({ full: initial, short: initialShort });

  useEffect(() => {
    const tick = () => {
      const now = new Date();
      setClock({ full: headerClock(now, locale), short: shortDayLabel(now, locale) });
    };
    const timer = window.setInterval(tick, 30_000);
    return () => window.clearInterval(timer);
  }, [locale]);

  const Icon = clock.full.night ? Moon : Sun;
  const role = [position, department].filter(Boolean).join(" · ");

  return (
    <header className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between lg:gap-6">
      <div className="flex min-w-0 items-center gap-3 lg:gap-4">
        <span className="grid size-[46px] shrink-0 place-items-center rounded-full bg-gradient-to-br from-[#f472b6] to-[#a78bfa] p-[2px] lg:size-[60px]">
          <span className="grid size-full place-items-center rounded-full bg-gradient-to-br from-[#f472b6] to-[#ec4899] text-[15px] font-bold text-white ring-2 ring-page lg:text-[19px]">
            {initials}
          </span>
        </span>
        <div className="min-w-0">
          <p className="hidden items-center gap-1.5 text-[11px] font-semibold uppercase tracking-[0.8px] text-ink-3 lg:flex">
            <Icon aria-hidden className="size-[13px] text-warn" strokeWidth={2.2} />
            <span suppressHydrationWarning>{clock.full.label}</span>
          </p>
          <h1 className="truncate text-[19px] font-bold leading-tight tracking-[-0.3px] text-ink lg:mt-1 lg:text-[28px] lg:tracking-[-0.5px]">
            <span suppressHydrationWarning>{t(clock.full.greeting)}</span>
            {name ? `, ${name}` : ""}
          </h1>
          {(role || fullName) && (
            <p className="mt-0.5 truncate text-[13px] text-ink-3 lg:mt-1">
              {role && <span className="font-medium text-ink-2">{role}</span>}
              {fullName && <span className="hidden lg:inline">{role ? " · " : ""}{fullName}</span>}
            </p>
          )}
        </div>
      </div>
      <div className="flex min-h-[28px] items-center justify-between gap-3 lg:justify-end">
        <p className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-[0.8px] text-ink-3 lg:hidden">
          <Icon aria-hidden className="size-[13px] text-warn" strokeWidth={2.2} />
          <span suppressHydrationWarning>{clock.short}</span>
        </p>
        {pill}
      </div>
    </header>
  );
}
