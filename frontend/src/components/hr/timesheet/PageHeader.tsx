"use client";

import Link from "next/link";
import { ChevronRight, FileSpreadsheet, RefreshCw } from "lucide-react";
import { useT } from "@/i18n/client";
import type { HrSyncBrief } from "@/lib/hr";
import { SyncStatus } from "./SyncStatus";

const focus = "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-(--ts-accent) focus-visible:ring-offset-2 focus-visible:ring-offset-(--ts-bg)";

/**
 * Шапка «Рабочего времени и табелей»: крошки, заголовок со статусом синхронизации Verifix, подзаголовок; справа «Обновить» (иконка крутится,
 * пока данные загружаются) и «Экспорт в Excel» (подразделения, табель по дням и отклонения за выбранный период).
 */
export function PageHeader({
  sync,
  now,
  refreshing,
  onRefresh,
  exportHref,
}: {
  sync: HrSyncBrief;
  now: number;
  refreshing: boolean;
  onRefresh: () => void;
  exportHref: string | null;
}) {
  const t = useT();
  return (
    <header className="flex flex-wrap items-end justify-between gap-x-6 gap-y-4">
      <div className="flex min-w-0 flex-col gap-2">
        <nav aria-label={t("Навигация")} className="flex items-center gap-1.5 text-[12.5px]">
          <Link href="/" className={`rounded text-(--ts-subtle) transition-colors duration-150 hover:text-(--ts-text-2) ${focus}`}>
            OneBase
          </Link>
          <ChevronRight aria-hidden className="size-3 text-(--ts-subtle)" />
          <Link href="/hr" className={`rounded text-(--ts-subtle) transition-colors duration-150 hover:text-(--ts-text-2) ${focus}`}>
            HR
          </Link>
          <ChevronRight aria-hidden className="size-3 text-(--ts-subtle)" />
          <span className="text-(--ts-muted)">{t("Рабочее время и табели")}</span>
        </nav>
        <div className="flex min-w-0 flex-wrap items-center gap-3">
          <h1 className="text-[22px] font-semibold tracking-[-0.6px] text-(--ts-text) sm:text-[26px]">{t("Рабочее время и табели")}</h1>
          <SyncStatus sync={sync} now={now} />
        </div>
        <p className="text-sm text-(--ts-muted)">{t("Явка, опоздания, отсутствия и отработанные часы по графикам Verifix")}</p>
      </div>
      <div className="flex shrink-0 items-center gap-2">
        <button
          type="button"
          onClick={onRefresh}
          disabled={refreshing}
          className={`inline-flex h-[38px] cursor-pointer items-center gap-2 rounded-[9px] border border-(--ts-border-strong) bg-(--ts-surface) px-3.5 text-[13.5px] font-medium text-(--ts-text-2) transition-colors duration-150 hover:bg-(--ts-surface-2) disabled:cursor-default ${focus}`}
        >
          <RefreshCw aria-hidden className={`size-[15px] ${refreshing ? "animate-spin" : ""}`} />
          {t("Обновить")}
        </button>
        {exportHref ? (
          <a
            href={exportHref}
            className={`inline-flex h-[38px] cursor-pointer items-center gap-2 rounded-[9px] bg-(--ts-text) px-3.5 text-[13.5px] font-semibold text-(--ts-surface) transition-opacity duration-150 hover:opacity-90 ${focus}`}
          >
            <FileSpreadsheet aria-hidden className="size-[15px]" />
            {t("Экспорт в Excel")}
          </a>
        ) : (
          <span
            aria-disabled
            title={t("Выгрузка недоступна в демо-режиме")}
            className="inline-flex h-[38px] items-center gap-2 rounded-[9px] bg-(--ts-text) px-3.5 text-[13.5px] font-semibold text-(--ts-surface) opacity-60"
          >
            <FileSpreadsheet aria-hidden className="size-[15px]" />
            {t("Экспорт в Excel")}
          </span>
        )}
      </div>
    </header>
  );
}
