"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { Download, Ellipsis, RefreshCw } from "lucide-react";
import { formatDate } from "@/i18n/core";
import { useLocale, useT } from "@/i18n/client";
import { capitalize, syncChip, type SyncChipState } from "@/lib/hr-overview";
import type { HrSyncBrief } from "@/lib/hr";
import { focus } from "./bits";
import { DayCalendar, DayPickerButton } from "./DayPicker";

const chipTone: Record<SyncChipState["tone"], string> = {
  success: "bg-(--ts-success-soft) text-(--ts-success)",
  warning: "bg-(--ts-warning-soft) text-(--ts-warning)",
  danger: "bg-(--ts-danger-soft) text-(--ts-danger)",
  accent: "bg-(--ts-accent-soft) text-(--ts-accent)",
};
const chipDot: Record<SyncChipState["tone"], string> = {
  success: "bg-(--ts-success)",
  warning: "bg-(--ts-warning)",
  danger: "bg-(--ts-danger)",
  accent: "bg-(--ts-accent)",
};

/** Чип Verifix; при ошибке — причина в подсказке (и для экранного диктора) и «Повторить» для администратора интеграций. */
function SyncChip({ sync, now, short, onRetry, retrying }: { sync: HrSyncBrief; now: number; short?: boolean; onRetry?: () => void; retrying: boolean }) {
  const t = useT();
  const s = syncChip(sync, now);
  const text = t(short ? s.short : s.text, s.vars);
  const reason = s.error ? t(s.error) : null;
  return (
    <span className="flex min-w-0 items-center gap-2">
      <span className="group/tip relative min-w-0">
        <span
          tabIndex={reason ? 0 : undefined}
          aria-label={reason ? `${text}. ${reason}` : undefined}
          className={`inline-flex max-w-full items-center gap-2 rounded-[10px] font-medium ${short ? "h-7 px-2.5 text-xs" : "h-10 px-3.5 text-[13px]"} ${chipTone[s.tone]} ${reason ? focus : ""}`}
        >
          <span aria-hidden className={`size-2 shrink-0 rounded-full ${chipDot[s.tone]} ${sync.running ? "animate-pulse" : ""}`} />
          <span className="truncate">{text}</span>
        </span>
        {reason && (
          <span
            role="tooltip"
            className="pointer-events-none absolute right-0 top-[calc(100%+8px)] z-30 hidden w-max max-w-[320px] rounded-[8px] bg-(--ts-pop-bg) px-2.5 py-1.5 text-xs text-(--ts-pop-text) shadow-(--ts-pop-shadow) group-hover/tip:block group-focus-within/tip:block"
          >
            {reason}
          </span>
        )}
      </span>
      {s.failed && onRetry && (
        <button
          type="button"
          onClick={onRetry}
          disabled={retrying}
          className={`inline-flex shrink-0 cursor-pointer items-center gap-1.5 rounded-[10px] border border-(--ts-danger)/40 bg-(--ts-surface) font-semibold text-(--ts-danger) transition-colors duration-150 hover:bg-(--ts-danger-soft) disabled:cursor-default disabled:opacity-60 ${short ? "h-7 px-2.5 text-xs" : "h-10 px-3 text-[13px]"} ${focus}`}
        >
          <RefreshCw aria-hidden className={`size-3.5 ${retrying ? "animate-spin" : ""}`} />
          {t("Повторить")}
        </button>
      )}
    </span>
  );
}

/**
 * Шапка «Обзора HR». Ноутбук: крошки, «Обзор HR», «Пятница, 30 октября 2026 · данные на 10:24»; справа чип Verifix, выбор дня и «Отчёт за день».
 * Телефон и планшет: заголовок с меню «⋯» (день и отчёт), под ним дата и короткий чип. Без подключения Verifix выбирать день и выгружать нечего.
 */
export function OverviewHeader({
  date,
  today,
  yesterday,
  asOf,
  isToday,
  sync,
  now,
  reportHref,
  connected,
  onDate,
  onRetry,
  retrying,
  note,
}: {
  date: string;
  today: string;
  yesterday: string;
  asOf: string;
  isToday: boolean;
  sync: HrSyncBrief;
  now: number;
  reportHref: string | null;
  connected: boolean;
  onDate: (day: string) => void;
  onRetry?: () => void;
  retrying: boolean;
  note?: string | null;
}) {
  const t = useT();
  const locale = useLocale();
  const at = new Date(`${date}T12:00:00Z`);
  const fmt = (o: Intl.DateTimeFormatOptions) => formatDate(at, locale, { ...o, timeZone: "UTC" });
  const when = isToday ? t("данные на {time}", { time: asOf }) : t("данные на конец дня");
  const long = `${capitalize(fmt({ weekday: "long" }))}, ${fmt({ day: "numeric", month: "long" })} ${date.slice(0, 4)} · ${when}`;
  const short = `${capitalize(fmt({ weekday: "short" }))}, ${fmt({ day: "numeric", month: "long" })} · ${isToday ? asOf : t("конец дня")}`;

  const report = (className: string) =>
    reportHref ? (
      <a href={reportHref} download className={className}>
        <Download aria-hidden className="size-4" />
        {t("Отчёт за день")}
      </a>
    ) : (
      <span aria-disabled title={t("Выгрузка недоступна в демо-режиме")} className={`${className} opacity-60`}>
        <Download aria-hidden className="size-4" />
        {t("Отчёт за день")}
      </span>
    );

  return (
    <header className="flex flex-col gap-1.5 lg:flex-row lg:items-end lg:justify-between lg:gap-6">
      <div className="flex min-w-0 flex-col gap-1.5 lg:gap-2">
        <nav aria-label={t("Навигация")} className="flex items-center gap-1.5 text-xs font-medium text-(--ts-subtle) max-lg:hidden">
          <Link href="/" className={`rounded hover:text-(--ts-text-2) ${focus}`}>
            OneBase
          </Link>
          <span aria-hidden>/</span>
          <span>HR</span>
        </nav>
        <div className="flex items-center justify-between gap-3">
          <h1 className="text-[26px] font-bold leading-tight tracking-[-0.5px] text-(--ts-text) lg:text-[30px]">{t("Обзор HR")}</h1>
          {connected && <MobileMenu date={date} today={today} onDate={onDate} report={report} />}
        </div>
        <p className="text-sm text-(--ts-muted) max-lg:hidden">{long}</p>
        <div className="flex items-center justify-between gap-3 lg:hidden">
          <p className="truncate text-[13px] text-(--ts-muted)">{short}</p>
          <SyncChip sync={sync} now={now} short onRetry={onRetry} retrying={retrying} />
        </div>
        {note && <p className="text-xs text-(--ts-subtle)">{note}</p>}
      </div>

      <div className="flex shrink-0 items-center gap-2.5 max-lg:hidden">
        <SyncChip sync={sync} now={now} onRetry={onRetry} retrying={retrying} />
        {connected && (
          <>
            <DayPickerButton value={date} today={today} yesterday={yesterday} onChange={onDate} />
            {report(
              `inline-flex h-10 items-center gap-2 rounded-[10px] bg-(--ts-accent) px-4 text-sm font-semibold text-(--ts-on-accent) transition-opacity duration-150 hover:opacity-90 ${focus}`,
            )}
          </>
        )}
      </div>
    </header>
  );
}

/** Меню «⋯» на телефоне и планшете: календарь дня и «Отчёт за день». */
function MobileMenu({ date, today, onDate, report }: { date: string; today: string; onDate: (day: string) => void; report: (className: string) => React.ReactNode }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const box = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onDown = (e: PointerEvent) => !box.current?.contains(e.target as Node) && setOpen(false);
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    document.addEventListener("pointerdown", onDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("pointerdown", onDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  return (
    <div ref={box} className="relative lg:hidden">
      <button
        type="button"
        aria-label={t("Действия")}
        aria-expanded={open}
        aria-haspopup="dialog"
        onClick={() => setOpen((v) => !v)}
        className={`grid size-10 cursor-pointer place-items-center rounded-[10px] border border-(--ts-border-strong) bg-(--ts-surface) text-(--ts-text-2) hover:bg-(--ts-surface-2) ${focus}`}
      >
        <Ellipsis className="size-5" />
      </button>
      {open && (
        <div role="dialog" aria-label={t("Действия")} className="absolute right-0 top-[calc(100%+6px)] z-40 w-[292px] rounded-[14px] border border-(--ts-border) bg-(--ts-surface) p-3 shadow-[0_12px_32px_#0e13221f]">
          <DayCalendar
            value={date}
            today={today}
            onPick={(day) => {
              setOpen(false);
              onDate(day);
            }}
          />
          <div className="mt-3 border-t border-(--ts-border) pt-3">
            {report(`flex h-10 w-full items-center justify-center gap-2 rounded-[10px] bg-(--ts-accent) text-sm font-semibold text-(--ts-on-accent) ${focus}`)}
          </div>
        </div>
      )}
    </div>
  );
}
