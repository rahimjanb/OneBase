import ts from "@/components/hr/timesheet/timesheet.module.css";
import { getT } from "@/i18n/server";
import type { AiAlertsView } from "@/lib/ai";
import type { HomeFast, HomeTrends } from "@/lib/dashboard-data";
import { homeDepartments, homeSeries, homeTasks, homeView } from "@/lib/dashboard-home";
import { DEPARTMENTS, focusOf, weeklyReportOf } from "@/lib/dashboard-mobile";
import { homeClock } from "@/lib/dashboardClock";
import type { WorkOverview } from "@/lib/tasks";
import { DataStatus, OfflineNote } from "./DataStatus";
import { DepartmentsCard } from "./DepartmentsCard";
import { Dynamics } from "./Dynamics";
import { Filters } from "./Filters";
import { FocusCard } from "./FocusCard";
import { Greeting } from "./Greeting";
import { KpiHero } from "./KpiHero";
import m from "./mobile.module.css";
import { QuickActions, type HomeExport } from "./QuickActions";
import { ReportBanner } from "./ReportBanner";
import { TasksCard } from "./TasksCard";

type Home = { fast: Promise<HomeFast>; trends: Promise<HomeTrends>; tasks: Promise<WorkOverview | null> };

type Props = {
  name: string;
  now: Date;
  locale: string;
  /** API ответил на /api/auth/me — иначе внизу «Не удалось обновить». */
  apiOk: boolean;
  alerts: Promise<AiAlertsView | null>;
  home: Home;
};

/**
 * Мобильный «Обзор» (< 768): приветствие, фильтры, KPI, быстрые действия, «Фокус дня», динамика, задачи, отделы, AI-отчёт и свежесть
 * данных. Цифры — настоящие (lib/dashboard-data): продажи, HR и задачи. Запросы блоков идут параллельно, страница открывается целиком —
 * без заготовок загрузки (прошлые месяцы продаж для динамики держит в кэше прогрев продаж).
 */
export async function MobileOverview({ name, now, locale, apiOk, alerts, home }: Props) {
  const t = await getT();
  const at = now.toISOString();
  // Таблица «Экспорт» — из тех же данных; кнопка ждёт их только при нажатии.
  const exportData: Promise<HomeExport> = Promise.all([home.fast, home.trends]).then(([fast, trends]) => {
    const view = homeView(fast, t);
    return { hero: view.hero, stats: view.stats, tasks: view.tasks, departments: homeDepartments(fast, trends, t) };
  });

  return (
    <div data-mobile-home className={`${ts.root} ${m.page} md:hidden`}>
      <div className="flex flex-col gap-[20px] px-[16px] pb-[40px] pt-[8px]">
        <OfflineNote at={at} />
        <Head name={name} now={now} locale={locale} fast={home.fast} />
        <Filters departments={DEPARTMENTS.map((d) => ({ code: d.code, name: d.name }))} />
        <Kpi fast={home.fast} />
        <QuickActions data={exportData} />
        <Focus alerts={alerts} tasks={home.tasks} />
        <Trends trends={home.trends} />
        <Tasks tasks={home.tasks} />
        <Departments home={home} />
        <Report alerts={alerts} now={now} />
        <DataStatus at={at} ok={apiOk} />
      </div>
      <div aria-hidden className={m.fade} />
    </div>
  );
}

async function Head({ name, now, locale, fast }: { name: string; now: Date; locale: string; fast: Promise<HomeFast> }) {
  const [data, t] = await Promise.all([fast, getT()]);
  return (
    <Greeting
      name={name}
      headline={homeView(data, t).headline}
      departments={homeDepartments(data, null, t).map((d) => ({ code: d.code, status: d.status }))}
      initial={homeClock(now, locale)}
      locale={locale}
    />
  );
}

async function Kpi({ fast }: { fast: Promise<HomeFast> }) {
  const [data, t] = await Promise.all([fast, getT()]);
  const view = homeView(data, t);
  return <KpiHero hero={view.hero} stats={view.stats} />;
}

/** Подпись «Фокуса дня» — по самим находкам: сколько отделов они затрагивают и сколько активных задач. */
async function Focus({ alerts, tasks }: { alerts: Promise<AiAlertsView | null>; tasks: Promise<WorkOverview | null> }) {
  const [found, work, t] = await Promise.all([alerts, tasks, getT()]);
  const departments = new Set(found?.alerts.map((a) => a.category)).size;
  return <FocusCard items={found ? focusOf(found, t) : null} departments={departments} activeTasks={work?.open ?? null} />;
}

async function Trends({ trends }: { trends: Promise<HomeTrends> }) {
  const [data, t] = await Promise.all([trends, getT()]);
  return <Dynamics series={homeSeries(data, t)} days={data.currentDays} />;
}

async function Tasks({ tasks }: { tasks: Promise<WorkOverview | null> }) {
  return <TasksCard tasks={homeTasks(await tasks)} />;
}

async function Departments({ home }: { home: Home }) {
  const [fast, trends, t] = await Promise.all([home.fast, home.trends, getT()]);
  return <DepartmentsCard departments={homeDepartments(fast, trends, t)} />;
}

async function Report({ alerts, now }: { alerts: Promise<AiAlertsView | null>; now: Date }) {
  return <ReportBanner report={weeklyReportOf(await alerts, now)} />;
}
