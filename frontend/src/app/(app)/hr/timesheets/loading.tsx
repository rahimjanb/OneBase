import styles from "@/components/hr/timesheet/timesheet.module.css";

/** Пока загружается табель: каркас страницы — шапка, фильтры, сводка и строки таблицы серыми плашками. */
export default function Loading() {
  const bar = (className: string) => <span className={`block rounded ${styles.skeleton} ${className}`} />;
  return (
    <div className={`${styles.root} min-h-full`} aria-busy="true">
      <div className="flex flex-col gap-5 px-4 pb-8 pt-5 sm:px-8 sm:pt-6">
        <div className="flex flex-col gap-2.5">
          {bar("h-3 w-48")}
          {bar("h-7 w-80")}
          {bar("h-3.5 w-96 max-w-full")}
        </div>
        <div className="flex flex-wrap gap-2.5">
          {bar("h-[38px] w-64 rounded-[9px]")}
          {bar("h-[38px] w-[236px] rounded-[9px]")}
          {bar("h-[38px] w-[220px] rounded-[9px]")}
          {bar("h-[38px] w-[260px] rounded-[9px]")}
        </div>
        <div className="grid grid-cols-2 gap-px overflow-clip rounded-[12px] border border-(--ts-border) bg-(--ts-border) sm:grid-cols-3 xl:grid-cols-6">
          {Array.from({ length: 6 }, (_, i) => (
            <div key={i} className="flex flex-col gap-2 bg-(--ts-surface) px-5 py-4">
              {bar("h-3.5 w-28")}
              {bar("h-6 w-20")}
            </div>
          ))}
        </div>
        <div className="overflow-clip rounded-[12px] border border-(--ts-border) bg-(--ts-surface)">
          <div className="flex h-14 items-center border-b border-(--ts-border) px-5">{bar("h-4 w-48")}</div>
          {Array.from({ length: 8 }, (_, r) => (
            <div key={r} className="flex h-[52px] items-center gap-3 border-b border-(--ts-border) px-5 last:border-b-0">
              <span className={`size-[30px] shrink-0 rounded-full ${styles.skeleton}`} />
              {bar("h-3 w-40")}
              <span className="ml-6 flex gap-1 overflow-hidden">
                {Array.from({ length: 20 }, (_, c) => (
                  <span key={c} className={`h-[22px] w-7 shrink-0 rounded-[5px] ${styles.skeleton}`} />
                ))}
              </span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
