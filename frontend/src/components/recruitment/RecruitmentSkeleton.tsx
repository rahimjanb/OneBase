"use client";

import { usePathname } from "next/navigation";
import { RecruitmentShell } from "@/components/recruitment/RecruitmentShell";
import { LoadingList } from "@/components/recruitment/VacancyList";
import { RouteSkeleton } from "@/components/shell/RouteSkeleton";

const TITLES: [string, string][] = [
  ["/hr/recruitment/candidates", "Кандидаты"],
  ["/hr/recruitment/publications", "Публикации"],
  ["/hr/recruitment/interviews", "Собеседования"],
  ["/hr/recruitment/exceptions", "Исключения"],
  ["/hr/recruitment/analytics", "Аналитика"],
  ["/hr/recruitment/hh", "HH.uz"],
  ["/hr/recruitment/settings", "Настройки подбора"],
];

const bar = "block animate-pulse rounded-md bg-muted";

/**
 * Переход на страницу подбора: та же рамка сразу — шапка «Подбор» и кнопки разделов (иконки видны, подписи — заготовки, пока не пришли
 * счётчики), ниже — заготовка: у «Вакансий» — строка фильтров и 6 строк таблицы (на телефоне — 3 карточки), у остальных — блоки.
 * «Диалоги Telegram» и «Заявки на сотрудника» — со своим экраном, для них — общий макет HR.
 */
export function RecruitmentSkeleton() {
  const pathname = usePathname();
  if (pathname.startsWith("/hr/recruitment/telegram") || pathname.startsWith("/hr/recruitment/requests")) return <RouteSkeleton />;
  const vacancy = pathname.startsWith("/hr/recruitment/vacancies/");
  const candidate = /^\/hr\/recruitment\/candidates\/[^/]+/.test(pathname);
  const title = vacancy ? "Вакансия" : candidate ? "Кандидат" : (TITLES.find(([p]) => pathname.startsWith(p))?.[1] ?? "Вакансии");
  const parent = vacancy ? { label: "Вакансии", href: "/hr/recruitment" } : candidate ? { label: "Кандидаты", href: "/hr/recruitment/candidates" } : undefined;
  return (
    <RecruitmentShell title={title} canManage parent={parent} mobileAction={<span className="size-10 shrink-0" />}>
      {pathname === "/hr/recruitment" ? (
        <div aria-busy className="@container flex flex-col gap-4">
          <div className="flex items-center gap-2.5 max-md:hidden">
            <span className={`${bar} h-10 w-[300px] rounded-[10px]`} />
            <span className={`${bar} h-10 w-[380px] rounded-xl`} />
            <span className={`${bar} h-10 w-40 rounded-[10px]`} />
          </div>
          <div className="flex flex-col gap-3.5 md:hidden">
            <span className={`${bar} h-11 rounded-xl`} />
            <span className={`${bar} h-8 w-72 rounded-[10px]`} />
          </div>
          <LoadingList />
        </div>
      ) : (
        <div aria-busy className="flex flex-col gap-3">
          <span className={`${bar} h-10 w-72 rounded-[10px]`} />
          <span className={`${bar} h-40 rounded-2xl`} />
          <span className={`${bar} h-24 rounded-2xl`} />
        </div>
      )}
    </RecruitmentShell>
  );
}
