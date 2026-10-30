import layout from "@/components/hr/overview/overview.module.css";
import { ReportSkeleton } from "@/components/hr/reports/ReportSkeleton";
import styles from "@/components/hr/timesheet/timesheet.module.css";
import { getT } from "@/i18n/server";

/** Открытие «Отчётов»: шапка, фильтры и блоки отчёта заготовками, пока сервер считает период. */
export default async function Loading() {
  const t = await getT();
  return (
    <div className={`${styles.root} ${layout.page} min-h-full`}>
      <div className="mx-auto flex max-w-[1600px] flex-col gap-4 px-4 pb-8 pt-5 md:px-6 lg:gap-5 lg:px-8 lg:pb-10 lg:pt-7">
        <ReportSkeleton header label={t("Загрузка")} />
      </div>
    </div>
  );
}
