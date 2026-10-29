"use client";

import Link from "next/link";
import { PlugZap, RefreshCw, UsersRound } from "lucide-react";
import { useT } from "@/i18n/client";
import { VERIFIX_SETTINGS } from "@/lib/hr";
import styles from "@/components/hr/timesheet/timesheet.module.css";
import layout from "./overview.module.css";
import { card, focus } from "./bits";

const primary = `inline-flex h-10 cursor-pointer items-center gap-2 rounded-[10px] bg-(--ts-accent) px-4 text-sm font-semibold text-(--ts-on-accent) transition-opacity duration-150 hover:opacity-90 disabled:cursor-default disabled:opacity-60 ${focus}`;

/** Verifix не подключён: одна карточка вместо блоков с нулями; администратору интеграций — кнопка в настройки. */
export function NotConnected({ manages }: { manages: boolean }) {
  const t = useT();
  return (
    <section className={`${card} flex flex-col items-center gap-3 px-6 py-12 text-center`}>
      <span className="grid size-12 place-items-center rounded-full bg-(--ts-warning-soft) text-(--ts-warning)">
        <PlugZap aria-hidden className="size-6" />
      </span>
      <h2 className="text-lg font-semibold text-(--ts-text)">{t("Подключите Verifix, чтобы видеть посещаемость")}</h2>
      {manages ? (
        <>
          <p className="max-w-md text-sm text-(--ts-muted)">{t("Укажите client_id и client_secret и проверьте подключение")}</p>
          <Link href={VERIFIX_SETTINGS} className={`mt-2 ${primary}`}>
            {t("Открыть настройки")}
          </Link>
        </>
      ) : (
        <p className="max-w-md text-sm text-(--ts-muted)">{t("Данные HR появятся, когда администратор подключит Verifix.")}</p>
      )}
    </section>
  );
}

/** Подключено, сотрудников нет: в «Явке» — подсказка и «Синхронизировать» (администратору интеграций). */
export function NoStaff({ onSync, syncing, running }: { onSync?: () => void; syncing: boolean; running: boolean }) {
  const t = useT();
  return (
    <section className={`${card} flex flex-col items-center gap-3 px-6 py-12 text-center`}>
      <span className="grid size-12 place-items-center rounded-full bg-(--ts-accent-soft) text-(--ts-accent)">
        <UsersRound aria-hidden className="size-6" />
      </span>
      <h2 className="text-lg font-semibold text-(--ts-text)">{running ? t("Идёт синхронизация с Verifix…") : t("Сотрудники появятся после первой синхронизации")}</h2>
      {onSync && (
        <button type="button" onClick={onSync} disabled={syncing || running} className={`mt-2 ${primary}`}>
          <RefreshCw aria-hidden className={`size-4 ${syncing || running ? "animate-spin" : ""}`} />
          {t("Синхронизировать")}
        </button>
      )}
    </section>
  );
}

const bar = (className: string) => <span className={`block rounded ${styles.skeleton} ${className}`} />;

/** Пока данные загружаются: те же карточки и размеры, вместо значений — серые плашки. */
export function OverviewSkeleton({ header = true }: { header?: boolean }) {
  return (
    <div aria-busy="true" className="flex flex-col gap-4 lg:gap-5">
      {header && (
        <div className="flex flex-col gap-2.5 lg:flex-row lg:items-end lg:justify-between">
          <div className="flex flex-col gap-2.5">
            {bar("h-3 w-24 max-lg:hidden")}
            {bar("h-8 w-48")}
            {bar("h-3.5 w-80 max-w-full")}
          </div>
          <div className="flex gap-2.5 max-lg:hidden">
            {bar("h-10 w-56 rounded-[10px]")}
            {bar("h-10 w-32 rounded-[10px]")}
            {bar("h-10 w-40 rounded-[10px]")}
          </div>
        </div>
      )}
      <div className={layout.grid}>
        <div className={`${layout.kpi} grid grid-cols-2 gap-3 md:grid-cols-4 lg:gap-4`}>
          {Array.from({ length: 4 }, (_, i) => (
            <div key={i} className={`${card} flex h-[108px] flex-col gap-3 p-4 lg:h-[128px] lg:p-5`}>
              {bar("h-3 w-24")}
              {bar("h-7 w-20")}
              {bar("h-3 w-28")}
            </div>
          ))}
        </div>
        <div className={`${layout.att} ${card} flex h-[340px] flex-col gap-5 p-5 lg:h-[392px] lg:px-6 lg:py-[26px]`}>
          {bar("h-4 w-32")}
          {bar("h-12 w-48")}
          {bar("h-3.5 w-full rounded-[7px]")}
          <div className="grid flex-1 grid-cols-2 gap-4 lg:grid-cols-6">
            {Array.from({ length: 6 }, (_, i) => (
              <div key={i} className="flex flex-col gap-2">
                {bar("h-3 w-20")}
                {bar("h-6 w-10")}
              </div>
            ))}
          </div>
        </div>
        <div className={`${layout.warn} ${card} flex h-[320px] flex-col gap-4 p-5 lg:h-auto`}>
          {bar("h-4 w-40")}
          {Array.from({ length: 4 }, (_, i) => (
            <div key={i} className="flex items-center gap-3">
              <span className={`size-[34px] shrink-0 rounded-full ${styles.skeleton}`} />
              {bar("h-3 flex-1")}
            </div>
          ))}
        </div>
        <div className={`${layout.div} ${card} flex h-[420px] flex-col gap-5 p-5 lg:px-6`}>
          {bar("h-4 w-44")}
          {Array.from({ length: 6 }, (_, i) => (
            <span key={i}>{bar("h-4 w-full")}</span>
          ))}
        </div>
        <div className={layout.side}>
          <div className={`${layout.hours} ${card} flex h-[180px] flex-col gap-4 p-5`}>
            {bar("h-4 w-36")}
            {bar("h-24 w-full lg:h-[84px]")}
          </div>
          <div className={`${layout.branches} ${card} flex h-[220px] flex-1 flex-col gap-4 p-5`}>
            {bar("h-4 w-28")}
            {bar("h-3 w-full")}
            {bar("h-3 w-full")}
            {bar("h-3 w-full")}
          </div>
        </div>
      </div>
    </div>
  );
}
