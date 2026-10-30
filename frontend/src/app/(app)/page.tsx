import { redirect } from "next/navigation";
import { Suspense } from "react";
import { ActivityFeed } from "@/components/dashboard/ActivityFeed";
import { AiFocusPanel } from "@/components/dashboard/AiFocusPanel";
import { DashboardHeader } from "@/components/dashboard/DashboardHeader";
import { DepartmentsTable } from "@/components/dashboard/DepartmentsTable";
import { FilterBar } from "@/components/dashboard/FilterBar";
import { KpiCard } from "@/components/dashboard/KpiCard";
import { KpiTrendChart } from "@/components/dashboard/KpiTrendChart";
import { LeadersCard } from "@/components/dashboard/LeadersCard";
import { MobileOverview } from "@/components/dashboard/mobile/MobileOverview";
import { TasksDonut } from "@/components/dashboard/TasksDonut";
import { UpcomingTasks } from "@/components/dashboard/UpcomingTasks";
import { DesktopOverviewSkeleton } from "@/components/shell/RouteSkeleton";
import { getLocale, getT } from "@/i18n/server";
import type { AiAlertsView } from "@/lib/ai";
import type { HrLeaders } from "@/lib/hr";
import { activityOf, insightsOf, upcomingOf, type NotificationRow } from "@/lib/dashboard";
import { departmentRows, kpis, summary, taskStatuses, trendSeries } from "@/lib/dashboard.mock";
import { headerClock } from "@/lib/dashboardClock";
import { canOpen, homeFor } from "@/lib/navigation";
import { apiSession, apiTry } from "@/lib/server-api";
import type { DepartmentTasks, PageResult, WorkTask } from "@/lib/tasks";
import type { Me } from "@/lib/users";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Обзор · OneBase") };
}

/**
 * Главная «Обзор» (ТЗ dashboard): приветствие и дата, фильтры, «Фокус дня» (находки консультанта), четыре KPI, динамика KPI и задачи по
 * статусу, состояние отделов, дедлайны, лидеры и лента. Люди и события — только из реальных данных (задачи отделов, уведомления,
 * консультант); где их нет — пустое состояние. Показатели в числах — из lib/dashboard.mock до подключения API.
 * На телефоне (< 768) — своя раскладка (MobileOverview, ТЗ 1base-mobile-home) из тех же данных; находки консультанта и задачи отделов
 * запрашиваются один раз на обе. Каждая раскладка грузится сама по себе: пока идут данные — заготовки, а не пустой экран.
 */
export default async function DashboardPage() {
  // Запросы данных — сразу, параллельно с проверкой пользователя (apiTry не бросает ошибок: нет ответа — null).
  const alerts = apiTry<AiAlertsView>("/api/ai/alerts");
  const departments = apiTry<DepartmentTasks[]>("/api/work/departments");
  const [me, locale] = await Promise.all([apiSession<Me>("/api/auth/me"), getLocale()]);
  // Роль без общих разделов (например, «Админ 2») начинает со своего первого раздела.
  if (me && !canOpen("/", me.permissions)) redirect(homeFor(me.permissions));
  const now = new Date();
  const name = me?.firstName || me?.name || me?.login || "";

  return (
    <>
      <div className="max-md:hidden">
        <Suspense fallback={<DesktopOverviewSkeleton />}>
          <DesktopOverview me={me} name={name} now={now} locale={locale} alerts={alerts} departments={departments} />
        </Suspense>
      </div>
      <MobileOverview name={name} now={now} locale={locale} apiOk={me !== null} alerts={alerts} departments={departments} />
    </>
  );
}

async function DesktopOverview({
  me,
  name,
  now,
  locale,
  alerts: alertsRequest,
  departments: departmentsRequest,
}: {
  me: Me | null;
  name: string;
  now: Date;
  locale: string;
  alerts: Promise<AiAlertsView | null>;
  departments: Promise<DepartmentTasks[] | null>;
}) {
  const [alerts, departments, notifications, leaders, t] = await Promise.all([
    alertsRequest,
    departmentsRequest,
    apiTry<{ items: NotificationRow[] }>("/api/notifications?take=8"),
    // Без доступа к HR сервер ответит 403 — блок покажет, кому виден рейтинг.
    apiTry<HrLeaders>("/api/hr/leaders?take=7"),
    getT(),
  ]);
  // Месяц рейтинга словом («октябрь»): в первые дни месяца сервер берёт прошлый месяц.
  const leadersMonth = (leaders ? new Date(`${leaders.from}T12:00:00Z`) : now).toLocaleDateString(locale, { month: "long", timeZone: leaders ? "UTC" : "Asia/Tashkent" });

  // Открытые задачи отделов, где они видны пользователю, — для дедлайнов недели.
  const withTasks = (departments ?? []).filter((d) => d.hasTasks).slice(0, 8);
  const pages = await Promise.all(withTasks.map((d) => apiTry<PageResult<WorkTask>>(`/api/work/tasks?department=${encodeURIComponent(d.code)}&pageSize=50`)));
  const tasks = withTasks.flatMap((d, i) => (pages[i]?.items ?? []).map((task) => ({ task, department: d })));
  const upcoming = upcomingOf(tasks, now, locale, t);
  const rows = departmentRows.map((r) => {
    const real = departments?.find((d) => d.code === r.code);
    return real ? { ...r, tasks: real.openTasks, overdue: real.overdueTasks } : r;
  });

  return (
    <div className="flex flex-col gap-5 px-4 pb-12 pt-6 sm:px-6 lg:px-8 lg:pt-8">
      <DashboardHeader name={name} planShare={summary.planShare} attention={summary.attention} initial={headerClock(now, locale)} locale={locale} />
      <FilterBar departments={departmentRows.map((d) => d.name)} updatedMinutesAgo={summary.updatedMinutesAgo} />
      <AiFocusPanel insights={alerts ? insightsOf(alerts, t) : []} available={alerts !== null} departments={summary.departments} tasks={summary.activeTasks} t={t} />

      <div className="grid grid-cols-2 gap-3 sm:gap-4 xl:grid-cols-4">
        {kpis.map((kpi) => (
          <KpiCard key={kpi.id} kpi={kpi} t={t} />
        ))}
      </div>

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-[minmax(0,1fr)_380px]">
        <KpiTrendChart series={trendSeries} />
        <TasksDonut total={taskStatuses.total} done={taskStatuses.done} slices={taskStatuses.slices} t={t} />
      </div>

      <DepartmentsTable rows={rows} t={t} />

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-[minmax(0,1fr)_280px_320px]">
        <UpcomingTasks items={upcoming} total={tasks.filter((x) => x.task.status === "New" || x.task.status === "InProgress").length} available={departments !== null} />
        <LeadersCard data={leaders} access={me?.hrAccess ?? false} month={leadersMonth} t={t} />
        <ActivityFeed events={notifications ? activityOf(notifications.items, now, t) : []} available={notifications !== null} t={t} />
      </div>
    </div>
  );
}
