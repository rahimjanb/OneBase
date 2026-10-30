import styles from "@/components/hr/timesheet/timesheet.module.css";
import { card } from "../overview/bits";

function Sk({ className = "", style }: { className?: string; style?: React.CSSProperties }) {
  return <span aria-hidden className={`block rounded-[6px] ${styles.skeleton} ${className}`} style={style} />;
}

/**
 * Заготовки отчёта тех же размеров, что блоки: KPI, топы, 10 строк таблицы и списки сотрудников. aria-busy — признак загрузки для полоски
 * перехода (NavProgress). header — со шапкой и фильтрами (открытие страницы), без — пока перезагружаются данные под фильтрами.
 */
export function ReportSkeleton({ header = false, label }: { header?: boolean; label: string }) {
  return (
    <div role="status" aria-busy="true" aria-label={label} className="flex flex-col gap-4 lg:gap-5">
      {header && (
        <>
          <div className="flex items-end justify-between gap-4">
            <div className="flex flex-col gap-2.5">
              <Sk className="hidden h-3 w-24 lg:block" />
              <Sk className="h-7 w-[240px] lg:h-9 lg:w-[360px]" />
              <Sk className="h-3.5 w-[200px] lg:w-[420px]" />
            </div>
            <Sk className="size-10 rounded-[10px] lg:w-[150px]" />
          </div>
          <div className="flex flex-col gap-2.5 md:flex-row">
            <Sk className="h-10 w-full rounded-[12px] md:w-[340px]" />
            <div className="grid grid-cols-2 gap-2.5 md:flex">
              <Sk className="h-10 rounded-[10px] md:w-[220px]" />
              <Sk className="h-10 rounded-[10px] md:w-[200px]" />
            </div>
          </div>
        </>
      )}

      {/* KPI */}
      <div className="hidden gap-3 md:grid md:grid-cols-3 lg:grid-cols-6">
        {Array.from({ length: 6 }, (_, i) => (
          <div key={i} className={`${card} flex h-[112px] flex-col gap-2.5 px-[18px] py-4`}>
            <Sk className="h-3 w-20" />
            <Sk className="h-6 w-24" />
            <Sk className="mt-auto h-2.5 w-full" />
          </div>
        ))}
      </div>
      <div className="flex flex-col gap-2.5 md:hidden">
        <div className={`${card} flex flex-col gap-3 p-4`}>
          <Sk className="h-3.5 w-full" />
          <Sk className="h-10 w-36" />
          <Sk className="h-1.5 w-full" />
        </div>
        <div className="grid grid-cols-2 gap-2.5">
          {Array.from({ length: 4 }, (_, i) => (
            <div key={i} className={`${card} flex h-[92px] flex-col gap-2.5 p-3.5`}>
              <Sk className="h-3 w-20" />
              <Sk className="h-6 w-16" />
              <Sk className="h-2.5 w-24" />
            </div>
          ))}
        </div>
      </div>

      {/* Топы */}
      <div className="flex gap-3 overflow-hidden md:grid md:grid-cols-3 md:gap-4">
        {Array.from({ length: 3 }, (_, i) => (
          <div key={i} className={`${card} flex w-[290px] shrink-0 flex-col gap-3 px-[18px] py-4 md:w-auto`}>
            <Sk className="h-7 w-40" />
            {Array.from({ length: 3 }, (_, j) => (
              <Sk key={j} className="h-3.5 w-full" />
            ))}
          </div>
        ))}
      </div>

      {/* Таблица: 10 строк */}
      <div className={`${card} overflow-hidden`}>
        <div className="flex items-center justify-between px-5 py-4">
          <Sk className="h-4 w-40" />
          <Sk className="hidden h-9 w-[240px] rounded-[9px] md:block" />
        </div>
        <Sk className="h-9 w-full rounded-none" />
        {Array.from({ length: 10 }, (_, i) => (
          <div key={i} className="flex items-center gap-4 border-t border-(--ts-border) px-5 py-3.5">
            <Sk className="h-3.5 w-32 md:w-44" />
            <Sk className="ml-auto h-1.5 w-24 md:ml-8" />
            <Sk className="hidden h-3.5 flex-1 md:block" />
          </div>
        ))}
      </div>

      {/* Сотрудники */}
      <div className="grid gap-4 lg:grid-cols-2">
        {Array.from({ length: 2 }, (_, i) => (
          <div key={i} className={`${card} flex flex-col`}>
            <div className="flex items-center gap-3 px-5 py-4">
              <Sk className="size-8 rounded-[9px]" />
              <Sk className="h-4 w-48" />
            </div>
            {Array.from({ length: 5 }, (_, j) => (
              <div key={j} className="flex items-center gap-3 border-t border-(--ts-border) px-5 py-3">
                <Sk className="hidden size-[30px] rounded-full md:block" />
                <Sk className="h-3.5 w-44" />
                <Sk className="ml-auto h-3.5 w-16" />
              </div>
            ))}
          </div>
        ))}
      </div>
    </div>
  );
}
