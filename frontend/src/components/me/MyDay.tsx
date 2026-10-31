import { Suspense } from "react";
import { getT } from "@/i18n/server";
import type { T } from "@/i18n/core";
import { headerClock } from "@/lib/dashboardClock";
import { dayPill, shortDayLabel, todayIn, type CalendarItem, type MyTasks, type MyWorkday } from "@/lib/my-day";
import { apiTry } from "@/lib/server-api";
import { initials, type Me } from "@/lib/users";
import { AiPlanCard } from "./AiPlanCard";
import { BlockError } from "./BlockError";
import { CalendarBlock } from "./CalendarBlock";
import { DocumentsBlock } from "./DocumentsBlock";
import { MyDayHeader } from "./MyDayHeader";
import { NotificationsBlock } from "./NotificationsBlock";
import { BlockSkeleton, Sk, toneSoft, toneText } from "./parts";
import { TasksSkeleton, WorkdaySkeleton } from "./Skeletons";
import { EfficiencyBlock, TasksBlock } from "./TasksBlock";
import { WorkdayCard } from "./WorkdayCard";

/** Пилюля статуса дня в шапке — ждёт только рабочий день. */
async function Pill({ data, t }: { data: Promise<MyWorkday | null>; t: T }) {
  const pill = dayPill((await data)?.today ?? null, t);
  if (!pill) return null;
  return (
    <span className={`inline-flex min-h-[30px] items-center gap-2 rounded-full px-3.5 text-[13px] font-semibold lg:min-h-[40px] lg:px-5 lg:text-[14px] ${toneSoft[pill.tone]} ${toneText[pill.tone]}`}>
      <span aria-hidden className="size-[7px] rounded-full bg-current" />
      {pill.label}
    </span>
  );
}

async function Workday({ data, today, locale, t }: { data: Promise<MyWorkday | null>; today: string; locale: string; t: T }) {
  return (await data) ? <WorkdayCard data={data} today={today} locale={locale} t={t} /> : <BlockError title="Рабочий день" />;
}

/**
 * «Мой день» сотрудника (ТЗ employee-dashboard): шапка с приветствием и статусом дня; слева — рабочий день (Verifix), мои задачи, личная
 * эффективность; справа (400 px) — план на день от AI, календарь, уведомления, мои документы. На телефоне — одна колонка в порядке макета:
 * рабочий день, AI, задачи, эффективность, календарь, уведомления, документы. Каждый блок грузится сам по себе (свой запрос /api/me/* и
 * своя заготовка): медленный блок не держит остальные, ошибка одного — «Повторить» только в нём. Данные — только текущего пользователя.
 */
export async function MyDay({ me, now, locale }: { me: Me | null; now: Date; locale: string }) {
  const t = await getT();
  const loading = t("Загрузка");
  const workday = apiTry<MyWorkday>("/api/me/workday");
  const tasks = apiTry<MyTasks>("/api/me/tasks");
  const calendar = apiTry<CalendarItem[]>("/api/me/calendar");
  const today = todayIn(now);
  const name = me?.firstName || me?.name || me?.login || "";
  const fullName = me ? [me.lastName, me.firstName].filter(Boolean).join(" ") : "";

  return (
    <div data-mobile-home className="me-ui min-h-full">
      <div className="mx-auto flex max-w-[1440px] flex-col gap-4 px-4 pb-10 pt-2 sm:px-6 lg:gap-5 lg:px-8 lg:pb-12 lg:pt-8">
        <MyDayHeader
          name={name}
          fullName={fullName}
          position={me?.position ?? null}
          department={me?.departmentName ?? null}
          initials={me ? initials(me) : ""}
          initial={headerClock(now, locale)}
          initialShort={shortDayLabel(now, locale)}
          locale={locale}
          pill={
            <Suspense fallback={<Sk className="h-[30px] w-40 rounded-full lg:h-10 lg:w-56" />}>
              <Pill data={workday} t={t} />
            </Suspense>
          }
        />

        <div className="grid grid-cols-1 gap-4 lg:grid-cols-[minmax(0,1fr)_400px] lg:items-start lg:gap-5">
          <div className="contents lg:flex lg:min-w-0 lg:flex-col lg:gap-5">
            <div className="order-1 min-w-0 lg:order-none">
              <Suspense fallback={<WorkdaySkeleton label={loading} />}>
                <Workday data={workday} today={today} locale={locale} t={t} />
              </Suspense>
            </div>
            <div className="order-3 min-w-0 lg:order-none">
              <Suspense fallback={<TasksSkeleton label={loading} />}>
                <TasksBlock data={tasks} today={today} locale={locale} t={t} />
              </Suspense>
            </div>
            <div className="order-4 min-w-0 empty:hidden lg:order-none">
              <Suspense fallback={<BlockSkeleton rows={2} label={loading} />}>
                <EfficiencyBlock data={tasks} t={t} />
              </Suspense>
            </div>
          </div>
          <div className="contents lg:flex lg:min-w-0 lg:flex-col lg:gap-5">
            <div className="order-2 min-w-0 lg:order-none">
              <AiPlanCard />
            </div>
            <div className="order-5 min-w-0 lg:order-none">
              <Suspense fallback={<BlockSkeleton rows={3} label={loading} />}>
                <CalendarBlock data={calendar} today={today} locale={locale} t={t} />
              </Suspense>
            </div>
            <div className="order-6 min-w-0 lg:order-none">
              <NotificationsBlock />
            </div>
            <div className="order-7 min-w-0 lg:order-none">
              <DocumentsBlock />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
