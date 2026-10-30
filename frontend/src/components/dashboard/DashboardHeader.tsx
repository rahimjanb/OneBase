"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Download, Moon, Plus, Sun } from "lucide-react";
import { useT } from "@/i18n/client";
import { headerClock, type HeaderClock } from "@/lib/dashboardClock";
import { plural } from "@/lib/format";
import { outlineButton } from "./blocks";

/**
 * Шапка главной: дата и время, приветствие по имени, выполнение плана и число отделов, требующих внимания; справа «Экспорт» и
 * «Новая задача». Время идёт: первая отрисовка — с сервера, дальше обновляется раз в 30 с.
 */
export function DashboardHeader({
  name,
  planShare,
  attention,
  initial,
  locale,
}: {
  name: string;
  /** Выполнение плана продаж, %; нет доступа к продажам — null, строка только об отделах. */
  planShare: number | null;
  attention: number;
  initial: HeaderClock;
  locale: string;
}) {
  const t = useT();
  const [clock, setClock] = useState(initial);

  useEffect(() => {
    const tick = () => setClock(headerClock(new Date(), locale));
    tick();
    const timer = window.setInterval(tick, 30_000);
    return () => window.clearInterval(timer);
  }, [locale]);

  const Icon = clock.night ? Moon : Sun;
  const share = planShare?.toLocaleString("ru-RU", { minimumFractionDigits: 1, maximumFractionDigits: 1 });

  return (
    <div className="flex flex-wrap items-center justify-between gap-4">
      <div className="flex min-w-0 flex-col gap-2">
        <div className="flex items-center gap-1.5 text-[11.5px] font-semibold uppercase tracking-[1px] text-ink-3">
          <Icon className="size-[13px] text-warn" strokeWidth={2} />
          <span suppressHydrationWarning>{clock.label}</span>
        </div>
        <h1 className="text-[26px] font-semibold leading-tight tracking-[-0.8px] text-ink sm:text-[32px]">
          <span suppressHydrationWarning>{t(clock.greeting)}</span>
          {name ? `, ${name}` : ""}
        </h1>
        <p className="text-sm text-ink-3">
          {share != null && (
            <>
              {t("Компания выполняет план на")} <span className="font-semibold text-ok">{share}%</span> ·{" "}
            </>
          )}
          {attention} {t(plural(attention, ["отдел требует внимания", "отдела требуют внимания", "отделов требуют внимания"]))}
        </p>
      </div>
      <div className="flex shrink-0 items-center gap-2.5">
        <button type="button" onClick={() => window.print()} className={`${outlineButton} h-[38px] px-3.5`}>
          <Download className="size-[15px]" strokeWidth={1.75} />
          {t("Экспорт")}
        </button>
        <Link
          href="/tasks"
          className="inline-flex h-[38px] items-center gap-2 rounded-[10px] bg-accent px-4 text-[13px] font-semibold text-white transition-[filter] duration-150 hover:brightness-110 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2 focus-visible:ring-offset-page"
        >
          <Plus className="size-4" strokeWidth={2} />
          {t("Новая задача")}
        </Link>
      </div>
    </div>
  );
}
