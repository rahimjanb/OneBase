import { Suspense } from "react";
import ts from "@/components/hr/timesheet/timesheet.module.css";
import { getT } from "@/i18n/server";
import type { AiAlertsView } from "@/lib/ai";
import { summary } from "@/lib/dashboard.mock";
import { daysIntoWeek, focusOf, mobileDepartments, mobileKpi, mobileTasks, mobileTrends, weeklyReportOf } from "@/lib/dashboard-mobile";
import { homeClock } from "@/lib/dashboardClock";
import type { DepartmentTasks } from "@/lib/tasks";
import { DataStatus, OfflineNote } from "./DataStatus";
import { DepartmentsCard } from "./DepartmentsCard";
import { Dynamics } from "./Dynamics";
import { Filters } from "./Filters";
import { FocusCard } from "./FocusCard";
import { Greeting } from "./Greeting";
import { KpiHero } from "./KpiHero";
import m from "./mobile.module.css";
import { QuickActions } from "./QuickActions";
import { ReportBanner } from "./ReportBanner";
import { DepartmentsSkeleton, FocusSkeleton, TasksSkeleton } from "./Skeletons";
import { TasksCard } from "./TasksCard";

type Props = {
  name: string;
  now: Date;
  locale: string;
  /** API ответил на /api/auth/me — иначе внизу «Не удалось обновить». */
  apiOk: boolean;
  alerts: Promise<AiAlertsView | null>;
  departments: Promise<DepartmentTasks[] | null>;
};

/**
 * Мобильный «Обзор» (< 768): приветствие, фильтры, KPI, быстрые действия, «Фокус дня», динамика, задачи, отделы, AI-отчёт и свежесть
 * данных. Показатели макета есть сразу; блоки с данными API («Фокус дня» и отчёт — находки консультанта, задачи и отделы — задачи
 * отделов) приходят сами по себе, каждый со своей заготовкой: медленный ответ не держит остальную страницу.
 */
export async function MobileOverview({ name, now, locale, apiOk, alerts, departments }: Props) {
  const t = await getT();
  const loading = t("Загрузка");
  const kpi = mobileKpi();
  const tasks = mobileTasks();
  const base = mobileDepartments(null);
  const at = now.toISOString();

  return (
    <div data-mobile-home className={`${ts.root} ${m.page} md:hidden`}>
      <div className="flex flex-col gap-[20px] px-[16px] pb-[40px] pt-[8px]">
        <OfflineNote at={at} />
        <Greeting name={name} planShare={kpi.plan.value} departments={base} initial={homeClock(now, locale)} locale={locale} />
        <Filters departments={base.map((d) => ({ code: d.code, name: d.name }))} />
        <KpiHero kpi={kpi} />
        <QuickActions kpi={kpi} tasks={tasks} departments={base} />
        <Suspense fallback={<FocusSkeleton label={loading} />}>
          <Focus alerts={alerts} />
        </Suspense>
        <Dynamics series={mobileTrends()} days={daysIntoWeek(now)} />
        <Suspense fallback={<TasksSkeleton label={loading} />}>
          <Tasks departments={departments} />
        </Suspense>
        <Suspense fallback={<DepartmentsSkeleton label={loading} />}>
          <Departments departments={departments} />
        </Suspense>
        <Suspense fallback={null}>
          <Report alerts={alerts} now={now} />
        </Suspense>
        <DataStatus at={at} ok={apiOk} />
      </div>
      <div aria-hidden className={m.fade} />
    </div>
  );
}

async function Focus({ alerts }: { alerts: Promise<AiAlertsView | null> }) {
  const [data, t] = await Promise.all([alerts, getT()]);
  return <FocusCard items={data ? focusOf(data, t) : null} departments={summary.departments} activeTasks={summary.activeTasks} />;
}

async function Tasks({ departments }: { departments: Promise<DepartmentTasks[] | null> }) {
  return <TasksCard tasks={mobileTasks()} departments={mobileDepartments(await departments)} />;
}

async function Departments({ departments }: { departments: Promise<DepartmentTasks[] | null> }) {
  return <DepartmentsCard departments={mobileDepartments(await departments)} />;
}

async function Report({ alerts, now }: { alerts: Promise<AiAlertsView | null>; now: Date }) {
  return <ReportBanner report={weeklyReportOf(await alerts, now)} />;
}
