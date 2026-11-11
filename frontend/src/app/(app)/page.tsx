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
import { HomeAttention } from "@/components/home/HomeAttention";
import { WorkHome } from "@/components/home/WorkHome";
import { MyDay } from "@/components/me/MyDay";
import { TasksDonut } from "@/components/dashboard/TasksDonut";
import { UpcomingTasks } from "@/components/dashboard/UpcomingTasks";
import { DesktopOverviewSkeleton } from "@/components/shell/RouteSkeleton";
import { getLocale, getT } from "@/i18n/server";
import type { AiAlertsView } from "@/lib/ai";
import type { HomeView } from "@/lib/arrivals";
import type { HrLeaders } from "@/lib/hr";
import { activityOf, insightsOf, upcomingOf, type NotificationRow } from "@/lib/dashboard";
import { loadHome } from "@/lib/dashboard-data";
import { desktopDepartments, desktopKpis, homeDepartments, homeSeries, homeView, minutesSince } from "@/lib/dashboard-home";
import { DEPARTMENTS } from "@/lib/dashboard-mobile";
import { headerClock } from "@/lib/dashboardClock";
import { canOpen, departmentOpen, homeFor, screenPermissions } from "@/lib/navigation";
import { apiSession, apiTry } from "@/lib/server-api";
import type { DepartmentTasks, PageResult, WorkTask } from "@/lib/tasks";
import type { Me } from "@/lib/users";

type SearchParams = Record<string, string | string[] | undefined>;

export async function generateMetadata({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const [t, me, sp] = await Promise.all([getT(), apiSession<Me>("/api/auth/me"), searchParams]);
  const work = me?.home === "work" && sp.view !== "company";
  return { title: t(me?.home === "my_day" ? "Мой день · OneBase" : work ? "Рабочий стол · OneBase" : "Обзор · OneBase") };
}

/**
 * Главная «Обзор» (ТЗ dashboard): приветствие и дата, фильтры, «Фокус дня» (находки консультанта), четыре KPI, динамика KPI и задачи по
 * статусу, состояние отделов, дедлайны, лидеры и лента. Люди и события — только из реальных данных (задачи отделов, уведомления,
 * консультант); где их нет — пустое состояние. Показатели — из разделов, где данные уже есть (lib/dashboard-data): продажи (план месяца,
 * недели), HR (явка) и задачи; отделы — те, по которым есть показатели.
 * На телефоне (< 768) — своя раскладка (MobileOverview, ТЗ 1base-mobile-home) из тех же данных; все запросы — один раз на обе.
 * Каждая раскладка грузится сама по себе: пока идут данные — заготовки, а не пустой экран.
 * Роль «Сотрудник» (me.home = my_day) видит здесь «Мой день» (components/me) — свои рабочее время, задачи, календарь, документы и AI-план.
 * Кладовщик, лаборатория, снабжение и аналитик (me.home = work) — «Рабочий стол» (components/home): машины на приёмке, ближайшие поставки,
 * проверки качества, согласования, принятое на склад; «Обзор» компании им открыт ссылкой (?view=company). Руководителю отдела, директору и
 * финансисту над «Обзором» — «Ждут вас»: согласования, «можно получить», принятое на склад.
 */
export default async function DashboardPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  // Кто вошёл — тот же ответ, что уже получил layout (apiSession кэширует его на запрос): у роли «Сотрудник» главная — «Мой день»,
  // сводку компании для неё не запрашиваем.
  const [me, locale, sp] = await Promise.all([apiSession<Me>("/api/auth/me"), getLocale(), searchParams]);
  if (me?.home === "my_day") return <MyDay me={me} now={new Date()} locale={locale} />;
  if (me?.home === "work" && sp.view !== "company") {
    const data = await apiTry<HomeView>("/api/procurement/home");
    if (data) {
      const now = new Date();
      const dateLabel = now.toLocaleDateString(locale, { weekday: "long", day: "numeric", month: "long", timeZone: "Asia/Tashkent" });
      return (
        <WorkHome
          data={data}
          name={me.firstName || me.name || me.login || ""}
          dateLabel={dateLabel.charAt(0).toUpperCase() + dateLabel.slice(1)}
          companyHref={canOpen("/", me.permissions) ? "/?view=company" : null}
        />
      );
    }
  }
  // Что пользователю открыто: блоки без доступа на главной не показываются (без ответа API — показываем, разделы проверят сами).
  const access = accessOf(me);
  // Запросы данных «Обзора» (apiTry не бросает ошибок: нет ответа — null).
  const alerts = access.consultant ? apiTry<AiAlertsView>("/api/ai/alerts") : Promise.resolve(null);
  const departments = access.tasks ? apiTry<DepartmentTasks[]>("/api/work/departments") : Promise.resolve(null);
  const home = loadHome();
  // «Ждут вас» над «Обзором» — одним запросом на обе раскладки.
  const attention = apiTry<HomeView>("/api/procurement/home");
  // Роль без общих разделов (например, «Админ 2») начинает со своего первого раздела.
  if (me && !canOpen("/", me.permissions)) redirect(homeFor(me.permissions));
  const now = new Date();
  const name = me?.firstName || me?.name || me?.login || "";

  return (
    <>
      <div className="max-md:hidden">
        <Suspense fallback={<DesktopOverviewSkeleton />}>
          <DesktopOverview access={access} name={name} now={now} locale={locale} alerts={alerts} departments={departments} home={home} attention={attention} />
        </Suspense>
      </div>
      <MobileOverview
        name={name}
        now={now}
        locale={locale}
        apiOk={me !== null}
        departments={access.departments}
        consultant={access.consultant}
        employees={access.leaders}
        alerts={alerts}
        home={home}
        attention={
          <Suspense fallback={null}>
            <HomeAttention data={attention} />
          </Suspense>
        }
      />
    </>
  );
}

/**
 * Доступ к блокам главной: отделы (фильтры) — доступные пользователю (lib/navigation departmentOpen), находки консультанта — с правом
 * консультанта, задачи — с разделом «Задачи», лидеры месяца — с доступом к HR. me = null (API не ответил) — всё видно.
 */
type HomeAccess = {
  departments: typeof DEPARTMENTS;
  consultant: boolean;
  tasks: boolean;
  /** Доступ к HR: лидеры месяца и фильтр по сотрудникам. */
  leaders: boolean;
};

function accessOf(me: Me | null): HomeAccess {
  if (!me) return { departments: DEPARTMENTS, consultant: true, tasks: true, leaders: true };
  const permissions = screenPermissions(me);
  return {
    departments: DEPARTMENTS.filter((d) => departmentOpen(d.code, permissions, me.departments)),
    consultant: canOpen("/consultant", permissions),
    tasks: canOpen("/tasks", permissions),
    leaders: Boolean(me.hrAccess),
  };
}

async function DesktopOverview({
  access,
  name,
  now,
  locale,
  alerts: alertsRequest,
  departments: departmentsRequest,
  home,
  attention,
}: {
  access: HomeAccess;
  name: string;
  now: Date;
  locale: string;
  alerts: Promise<AiAlertsView | null>;
  departments: Promise<DepartmentTasks[] | null>;
  home: ReturnType<typeof loadHome>;
  attention: Promise<HomeView | null>;
}) {
  const [alerts, departments, fast, trends, notifications, leaders, t] = await Promise.all([
    alertsRequest,
    departmentsRequest,
    home.fast,
    home.trends,
    apiTry<{ items: NotificationRow[] }>("/api/notifications?take=8"),
    // Рейтинг — только с доступом к HR (без него блока нет).
    access.leaders ? apiTry<HrLeaders>("/api/hr/leaders?take=7") : null,
    getT(),
  ]);
  // Месяц рейтинга словом («октябрь»): в первые дни месяца сервер берёт прошлый месяц.
  const leadersMonth = (leaders ? new Date(`${leaders.from}T12:00:00Z`) : now).toLocaleDateString(locale, { month: "long", timeZone: leaders ? "UTC" : "Asia/Tashkent" });

  // Открытые задачи отделов, где они видны пользователю, — для дедлайнов недели.
  const withTasks = (departments ?? []).filter((d) => d.hasTasks).slice(0, 8);
  const pages = await Promise.all(withTasks.map((d) => apiTry<PageResult<WorkTask>>(`/api/work/tasks?department=${encodeURIComponent(d.code)}&pageSize=50`)));
  const tasks = withTasks.flatMap((d, i) => (pages[i]?.items ?? []).map((task) => ({ task, department: d })));
  const upcoming = upcomingOf(tasks, now, locale, t);
  const view = homeView(fast, t);
  const units = homeDepartments(fast, trends, t);
  const rows = desktopDepartments(units, trends, departments);
  const kpis = desktopKpis(fast, trends, t);

  return (
    <div className="flex flex-col gap-5 px-4 pb-12 pt-6 sm:px-6 lg:px-8 lg:pt-8">
      <DashboardHeader
        name={name}
        planShare={view.headline?.kind === "sales" ? view.headline.value : null}
        attention={units.filter((u) => u.status === "attention").length}
        initial={headerClock(now, locale)}
        locale={locale}
      />
      <Suspense fallback={null}>
        <HomeAttention data={attention} />
      </Suspense>
      <FilterBar departments={access.departments.map((d) => d.name)} employees={access.leaders} updatedMinutesAgo={minutesSince(fast.hr?.syncedAt, now)} />
      {access.consultant && (
        <AiFocusPanel insights={alerts ? insightsOf(alerts, t) : []} available={alerts !== null} departments={units.length} tasks={view.activeTasks ?? 0} t={t} />
      )}

      {kpis.length > 0 && (
        <div className="grid grid-cols-2 gap-3 sm:gap-4 xl:grid-cols-4">
          {kpis.map((kpi) => (
            <KpiCard key={kpi.id} kpi={kpi} t={t} />
          ))}
        </div>
      )}

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-[minmax(0,1fr)_380px]">
        <KpiTrendChart series={homeSeries(trends, t)} />
        {view.tasks && <TasksDonut total={view.tasks.total} done={view.tasks.done} slices={view.tasks.slices} overdue={view.tasks.overdue} t={t} />}
      </div>

      {rows.length > 0 && <DepartmentsTable rows={rows} t={t} />}

      {/* Нижний ряд — только доступные блоки; колонки — по их числу. */}
      <div className={`grid grid-cols-1 gap-4 ${bottomColumns(access)}`}>
        {access.tasks && (
          <UpcomingTasks items={upcoming} total={tasks.filter((x) => x.task.status === "New" || x.task.status === "InProgress").length} available={departments !== null} />
        )}
        {access.leaders && <LeadersCard data={leaders} month={leadersMonth} t={t} />}
        <ActivityFeed events={notifications ? activityOf(notifications.items, now, t) : []} available={notifications !== null} t={t} />
      </div>
    </div>
  );
}

/** Колонки нижнего ряда (ширину карточкам задают они): дедлайны тянутся, лидеры — 280, лента — 320; одна лента — во всю ширину. */
function bottomColumns(access: HomeAccess): string {
  if (access.tasks && access.leaders) return "md:grid-cols-2 xl:grid-cols-[minmax(0,1fr)_280px_320px]";
  if (access.tasks) return "md:grid-cols-2 xl:grid-cols-[minmax(0,1fr)_320px]";
  if (access.leaders) return "md:grid-cols-2 xl:grid-cols-[280px_minmax(0,1fr)]";
  return "";
}
