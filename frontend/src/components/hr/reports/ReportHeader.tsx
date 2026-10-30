"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { FileSpreadsheet, Loader2, RotateCw, TriangleAlert, X } from "lucide-react";
import { useT } from "@/i18n/client";
import type { HrSyncBrief } from "@/lib/hr";
import { reportSync, type ReportSync } from "@/lib/hr-report";
import { focus } from "../overview/bits";

const chipTone: Record<ReportSync["tone"], string> = {
  success: "bg-(--ts-success-soft) text-(--ts-success)",
  warning: "bg-(--ts-warning-soft) text-(--ts-warning)",
  danger: "bg-(--ts-danger-soft) text-(--ts-danger)",
  accent: "bg-(--ts-accent-soft) text-(--ts-accent)",
};
const dotTone: Record<ReportSync["tone"], string> = {
  success: "bg-(--ts-success)",
  warning: "bg-(--ts-warning)",
  danger: "bg-(--ts-danger)",
  accent: "bg-(--ts-accent)",
};

const dm = (value: string) => `${value.slice(8, 10)}.${value.slice(5, 7)}`;

/** Подробности свежести для подсказки: «синхронизация 30.10 17:24 · отметки загружены до 30.10 17:23 · табель с 26.09 по 30.10». */
function useDetails(s: ReportSync) {
  const t = useT();
  return s.details.map((d) => t(d.text, d.vars)).join(" · ");
}

/** Скачивание Excel: файл приходит целиком, пока идёт — спиннер; ошибка — тост с «Повторить». */
function useDownload(href: string | null) {
  const [busy, setBusy] = useState(false);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    if (!failed) return;
    const timer = setTimeout(() => setFailed(false), 10_000);
    return () => clearTimeout(timer);
  }, [failed]);

  const run = async () => {
    if (!href || busy) return;
    setBusy(true);
    setFailed(false);
    try {
      const res = await fetch(href, { credentials: "same-origin" });
      if (!res.ok) throw new Error(String(res.status));
      const blob = await res.blob();
      const header = res.headers.get("content-disposition") ?? "";
      const name = /filename\*=UTF-8''([^;]+)/i.exec(header)?.[1] ?? /filename="?([^";]+)"?/i.exec(header)?.[1] ?? "hr-report.xlsx";
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = decodeURIComponent(name);
      document.body.append(a);
      a.click();
      a.remove();
      setTimeout(() => URL.revokeObjectURL(url), 5_000);
    } catch {
      setFailed(true);
    } finally {
      setBusy(false);
    }
  };
  return { busy, failed, run, dismiss: () => setFailed(false) };
}

/**
 * Шапка отчёта. Ноутбук: крошки, «Отчёт по посещаемости», подзаголовок с табелем Verifix; справа чип свежести (подсказка — полная строка)
 * и «Скачать Excel». Телефон: заголовок, под ним точка и «Verifix · отметки до …», справа квадратная кнопка Excel.
 */
export function ReportHeader({ sync, now, exportHref }: { sync: HrSyncBrief | null; now: number; exportHref: string | null }) {
  const t = useT();
  const s = sync ? reportSync(sync, now) : null;
  const details = useDetails(s ?? { tone: "success", text: "", details: [] });
  const download = useDownload(exportHref);
  const sheet = sync?.timesheetFrom && sync.timesheetTo ? t("табель Verifix с {from} по {to}", { from: dm(sync.timesheetFrom), to: dm(sync.timesheetTo) }) : null;
  const chipText = s ? t(s.text, s.vars) : null;

  return (
    <>
      <header className="flex items-start justify-between gap-4 lg:items-end lg:gap-6">
        <div className="flex min-w-0 flex-col gap-1.5 lg:gap-2">
          <nav aria-label={t("Навигация")} className="hidden items-center gap-1.5 text-xs font-medium text-(--ts-subtle) lg:flex">
            <Link href="/" className={`rounded transition-colors duration-150 hover:text-(--ts-text-2) ${focus}`}>
              OneBase
            </Link>
            <span aria-hidden>/</span>
            <Link href="/hr" className={`rounded transition-colors duration-150 hover:text-(--ts-text-2) ${focus}`}>
              HR
            </Link>
          </nav>
          <h1 className="text-[22px] font-bold leading-tight tracking-[-0.4px] text-(--ts-text) lg:text-[30px] lg:tracking-[-0.5px]">{t("Отчёт по посещаемости")}</h1>
          <p className="hidden text-sm text-(--ts-muted) lg:block">
            {t("Явка и дисциплина по подразделениям")}
            {sheet && <> · {sheet}</>}
          </p>
          {s && chipText && (
            <p className="flex min-w-0 items-center gap-1.5 text-xs text-(--ts-muted) lg:hidden" title={details || undefined}>
              <span aria-hidden className={`size-2 shrink-0 rounded-full ${dotTone[s.tone]} ${sync?.running ? "animate-pulse" : ""}`} />
              <span className="truncate">{chipText}</span>
            </p>
          )}
        </div>

        <div className="flex shrink-0 items-center gap-2.5">
          {s && chipText && (
            <span className="group/tip relative hidden lg:block">
              <span
                tabIndex={details ? 0 : undefined}
                aria-label={details ? `${chipText}. ${details}` : undefined}
                className={`inline-flex h-10 max-w-[360px] items-center gap-2 rounded-[10px] px-3.5 text-[13px] font-medium ${chipTone[s.tone]} ${details ? focus : ""}`}
              >
                <span aria-hidden className={`size-2 shrink-0 rounded-full ${dotTone[s.tone]} ${sync?.running ? "animate-pulse" : ""}`} />
                <span className="truncate">{chipText}</span>
              </span>
              {details && (
                <span
                  role="tooltip"
                  className="pointer-events-none absolute right-0 top-[calc(100%+8px)] z-30 hidden w-max max-w-[420px] rounded-[8px] border border-(--ts-pop-border) bg-(--ts-pop-bg) px-2.5 py-1.5 text-xs font-medium text-(--ts-pop-text) shadow-(--ts-pop-shadow) group-hover/tip:block group-focus-within/tip:block"
                >
                  {details}
                </span>
              )}
            </span>
          )}
          <button
            type="button"
            onClick={download.run}
            disabled={!exportHref}
            aria-busy={download.busy || undefined}
            aria-label={t("Скачать Excel")}
            title={exportHref ? undefined : t("Нет данных для выгрузки")}
            className={`inline-flex size-10 cursor-pointer items-center justify-center gap-2 rounded-[10px] bg-(--ts-accent) text-sm font-semibold text-(--ts-on-accent) transition-opacity duration-150 hover:opacity-90 disabled:cursor-default disabled:opacity-50 lg:w-auto lg:px-4 ${focus}`}
          >
            {download.busy ? <Loader2 aria-hidden className="size-[18px] animate-spin lg:size-4" /> : <FileSpreadsheet aria-hidden className="size-[18px] lg:size-4" />}
            <span className="max-lg:sr-only">{t("Скачать Excel")}</span>
          </button>
        </div>
      </header>

      {download.failed && (
        <div
          role="alert"
          className="fixed bottom-24 left-1/2 z-50 flex w-[min(92vw,420px)] -translate-x-1/2 items-center gap-3 rounded-[12px] bg-(--ts-pop-bg) py-2.5 pl-4 pr-2 text-sm font-medium text-(--ts-pop-text) shadow-(--ts-pop-shadow) md:bottom-8"
        >
          <TriangleAlert aria-hidden className="size-4 shrink-0 text-(--ts-pop-danger)" />
          <span className="min-w-0 flex-1">{t("Не удалось скачать Excel")}</span>
          <button
            type="button"
            onClick={download.run}
            className={`inline-flex h-8 shrink-0 cursor-pointer items-center gap-1.5 rounded-[8px] px-2.5 text-[13px] font-semibold text-(--ts-pop-link) hover:bg-(--ts-pop-neutral-bg) ${focus}`}
          >
            <RotateCw aria-hidden className="size-3.5" />
            {t("Повторить")}
          </button>
          <button
            type="button"
            onClick={download.dismiss}
            aria-label={t("Закрыть")}
            className={`grid size-8 shrink-0 cursor-pointer place-items-center rounded-[8px] text-(--ts-pop-muted) hover:bg-(--ts-pop-neutral-bg) ${focus}`}
          >
            <X aria-hidden className="size-4" />
          </button>
        </div>
      )}
    </>
  );
}
