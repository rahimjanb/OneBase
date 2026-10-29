import { OverviewSkeleton } from "@/components/hr/overview/OverviewStates";
import layout from "@/components/hr/overview/overview.module.css";
import styles from "@/components/hr/timesheet/timesheet.module.css";

/** Пока загружается «Обзор HR»: те же карточки серыми плашками — без прыжков вёрстки. Только для /hr: остальные вкладки HR — вне группы. */
export default function Loading() {
  return (
    <div className={`${styles.root} ${layout.page} min-h-full`}>
      <div className="mx-auto max-w-[1600px] px-4 pb-8 pt-5 md:px-6 lg:px-8 lg:pb-9 lg:pt-7">
        <OverviewSkeleton />
      </div>
    </div>
  );
}
