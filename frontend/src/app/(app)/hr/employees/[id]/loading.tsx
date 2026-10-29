import { EmployeeSkeleton } from "@/components/hr/employee/EmployeeView";
import layout from "@/components/hr/overview/overview.module.css";
import styles from "@/components/hr/timesheet/timesheet.module.css";

/** Пока загружается карточка сотрудника: профиль, KPI, полоса дней и строки таблицы серыми плашками. */
export default function Loading() {
  const bar = (className: string) => <span className={`block rounded ${styles.skeleton} ${className}`} />;
  return (
    <div className={`${styles.root} ${layout.page} min-h-full`}>
      <div className="mx-auto flex max-w-[1600px] flex-col gap-5 px-4 pb-10 pt-5 md:gap-6 md:px-6 lg:px-8 lg:pt-6">
        <div className="flex items-center gap-4">
          <span className={`size-14 shrink-0 rounded-full md:size-[60px] ${styles.skeleton}`} />
          <div className="flex flex-col gap-2.5">
            {bar("h-6 w-72 max-w-[60vw]")}
            {bar("h-3.5 w-96 max-w-[70vw]")}
          </div>
        </div>
        {bar("h-10 w-full max-w-[460px] rounded-[12px]")}
        <EmployeeSkeleton />
      </div>
    </div>
  );
}
