"use client";

import { ChevronLeft, Plus, Sparkles } from "lucide-react";
import { usePathname } from "next/navigation";
import { MobileOverviewSkeleton } from "@/components/dashboard/mobile/Skeletons";
import { MyDaySkeleton } from "@/components/me/Skeletons";
import { EmployeeSkeleton } from "@/components/hr/employee/EmployeeView";
import { OverviewSkeleton } from "@/components/hr/overview/OverviewStates";
import layout from "@/components/hr/overview/overview.module.css";
import { ReportSkeleton } from "@/components/hr/reports/ReportSkeleton";
import ts from "@/components/hr/timesheet/timesheet.module.css";
import { salesTabs } from "@/components/sales/tabs";
import { useT } from "@/i18n/client";
import { useHomeKind } from "./HomeKind";
import { PageBody, PageHeader, type Crumb } from "./PageHeader";
import { requestTabs, taskTabs } from "./sections";

/** Плашка на месте данных: мягкое мерцание (globals.css, .skel). */
function Sk({ className = "", style }: { className?: string; style?: React.CSSProperties }) {
  return <span aria-hidden className={`skel ${className}`} style={style} />;
}

/** Макет ждёт данных: aria-busy — по нему восстановление прокрутки (ScrollManager) ждёт настоящую страницу. */
function Busy({ className = "", children }: { className?: string; children: React.ReactNode }) {
  const t = useT();
  return (
    <div role="status" aria-busy="true" aria-label={t("Загрузка")} className={className}>
      {children}
    </div>
  );
}

const card = "rounded-xl border border-line bg-surface";
const ONEBASE: Crumb = { label: "OneBase", href: "/" };
/** «Продажи», «Снабжение» и HR открываются из «Отделов». */
const DEPARTMENTS: Crumb = { label: "Отделы", href: "/departments" };

/** Заголовки страниц, известные до данных: макет открывается с настоящим названием. Страницы записей (регион, сотрудник) — плашкой. */
const titles: Record<string, string> = {
  "/sales/republic": "Республика",
  "/sales/export": "Экспорт и опт",
  "/sales/setup": "Настройки продаж",
  "/sales/method": "Как считается",
  "/hr/employees": "Сотрудники",
  "/hr/attendance": "Посещаемость",
  "/hr/deviations": "Опоздания и отсутствия",
  "/tasks": "Задачи",
  "/tasks/archive": "Архив и завершённые",
  "/settings": "Настройки",
  "/settings/users": "Пользователи и роли",
  "/settings/integrations": "Интеграции",
  "/settings/data-sources": "Источники данных",
  "/settings/logs": "Журнал ошибок",
  "/settings/telegram": "Telegram",
  "/settings/telegram/reports": "Автоматические отчёты",
  "/base": "Файлы",
  "/departments": "Отделы",
  "/campaigns": "Рассылки",
  "/calendar": "Календарь",
  "/requests": "Заявки",
  "/requests/new": "Новая заявка",
  "/requests/categories": "Настройки заявок",
  "/requests/approvals": "Согласования",
  "/procurement/stock": "Склад",
  "/procurement/batches": "Партии",
  "/procurement/orders": "Заказы поставщикам",
  "/procurement/orders/new": "Новый заказ поставщику",
  "/procurement/suppliers": "Поставщики",
  "/procurement/movements": "Движения",
  "/procurement/issue": "К выдаче",
  "/procurement/buy": "К закупке",
  "/procurement/tenders": "Тендеры",
  "/procurement/tenders/new": "Новый тендер",
  "/procurement": "Снабжение",
  "/procurement/reports": "Отчёты",
  "/procurement/digests": "Ежедневные сводки",
  "/procurement/receipts": "Приёмка",
  "/procurement/receipts/new": "Приёмка по заказу",
  "/production": "Производство",
  "/production/quality": "Качество",
  "/production/orders": "Задания",
  "/production/recipes": "Техкарты",
  "/production/technologist": "Технолог",
  "/production/shifts": "Смены",
  "/production/reports": "Отчёты производства",
  "/reports": "Отчёты",
  "/ai": "AI Dashboard",
  ...Object.fromEntries(salesTabs.map((tab) => [tab.href, tab.label])),
  ...Object.fromEntries(taskTabs.filter((tab) => tab.key !== "overview" && tab.key !== "archive").map((tab) => [tab.href, tab.label])),
  ...Object.fromEntries(requestTabs.filter((tab) => tab.key !== "overview" && tab.key !== "categories").map((tab) => [tab.href, tab.label])),
};

/** Названия страниц «Аналитика» — как в их PageHeader. */
const analytikTitles: Record<string, string> = {
  "/analytik": "Аналитик",
  "/analytik/sales": "Продажи",
  "/analytik/production": "Производство",
  "/analytik/warehouse": "Склад",
  "/analytik/procurement": "Снабжение",
  "/analytik/finance": "Финансы",
  "/analytik/hr": "Персонал",
  "/analytik/forecasts": "Прогнозы",
  "/analytik/risks": "Риски и отклонения",
  "/analytik/reports": "Отчёты",
  "/analytik/history": "История",
  "/analytik/data": "Карта данных",
};

function titleOf(pathname: string): string | undefined {
  if (titles[pathname]) return titles[pathname];
  if (analytikTitles[pathname]) return analytikTitles[pathname];
  if (pathname.startsWith("/sales/primary/")) return "Первичка";
  if (pathname.startsWith("/settings/ai")) return "AI";
  return undefined;
}

/** Крошки над заголовком (на компьютере) — как у самих страниц раздела, чтобы заголовок не сдвигался, когда придут данные. */
function crumbsOf(pathname: string, title: string | undefined): Crumb[] | undefined {
  const last = title ? [{ label: title }] : [];
  if (pathname.startsWith("/sales")) return [ONEBASE, DEPARTMENTS, { label: "Продажи", href: "/sales" }, ...(pathname === "/sales" ? [] : last)];
  if (pathname.startsWith("/hr")) return [ONEBASE, DEPARTMENTS, { label: "HR", href: "/hr" }, ...last];
  if (pathname.startsWith("/tasks/")) return [ONEBASE, { label: "Задачи", href: "/tasks" }, ...last];
  if (pathname.startsWith("/requests/")) return [ONEBASE, { label: "Заявки", href: "/requests" }, ...last];
  if (pathname.startsWith("/procurement")) return [ONEBASE, DEPARTMENTS, { label: "Снабжение", href: "/procurement" }, ...last];
  if (pathname.startsWith("/production")) return [ONEBASE, DEPARTMENTS, { label: "Производство", href: "/production" }, ...last];
  if (pathname === "/analytik") return [ONEBASE, { label: "Аналитик" }];
  if (pathname.startsWith("/analytik/")) return [ONEBASE, { label: "Аналитик", href: "/analytik" }, ...last];
  if (pathname === "/settings") return [ONEBASE, { label: "Настройки" }];
  if (pathname.startsWith("/settings")) return [ONEBASE, { label: "Настройки", href: "/settings" }, ...last];
  if (pathname.startsWith("/base")) return [ONEBASE, { label: "Файлы", href: "/base" }];
  if (pathname.startsWith("/campaigns") || pathname === "/calendar") return [ONEBASE, ...last];
  return undefined;
}

/** Шапка страницы — та же, что у страниц (PageHeader): настоящее название, подзаголовок — плашкой. */
function Header({ pathname }: { pathname: string }) {
  const title = titleOf(pathname);
  return (
    <PageHeader
      title={title ?? <Sk className="h-[1.25em] w-64 max-w-full" />}
      subtitle={<Sk className="h-5 w-[26rem] max-w-full" />}
      breadcrumbs={crumbsOf(pathname, title)}
    />
  );
}

/** Ряд фильтров: поиск и выпадающие списки. */
function Filters({ widths = [256, 200, 180, 160] }: { widths?: number[] }) {
  return (
    <div className="mb-4 flex flex-wrap gap-2.5">
      {widths.map((w, i) => (
        <Sk key={i} className="h-9 max-w-full rounded-lg" style={{ width: w }} />
      ))}
    </div>
  );
}

/** Плитки показателей: подпись, число, пояснение. */
function Tiles({ count, className }: { count: number; className: string }) {
  return (
    <div className={`grid gap-3 ${className}`}>
      {Array.from({ length: count }, (_, i) => (
        <div key={i} className={`${card} flex h-28 flex-col justify-between p-4`}>
          <Sk className="h-3.5 w-24" />
          <Sk className="h-7 w-28" />
          <Sk className="h-3 w-32 max-w-full" />
        </div>
      ))}
    </div>
  );
}

/** Таблица: строка заголовков и строки с данными. */
function Table({ rows = 8 }: { rows?: number }) {
  return (
    <div className={`${card} overflow-hidden`}>
      <div className="flex h-11 items-center gap-6 border-b border-line bg-muted/60 px-4">
        {[96, 160, 120, 100].map((w, i) => (
          <Sk key={i} className="h-3" style={{ width: w }} />
        ))}
      </div>
      {Array.from({ length: rows }, (_, r) => (
        <div key={r} className="flex h-[52px] items-center gap-6 border-b border-line px-4 last:border-b-0">
          <Sk className="h-3.5 w-48 max-w-[40%]" />
          <Sk className="h-3.5 w-28 max-sm:hidden" />
          <Sk className="h-3.5 w-24" />
          <Sk className="ml-auto h-3.5 w-16" />
        </div>
      ))}
    </div>
  );
}

/** Карточка блока: заголовок и строки. */
function Block({ rows = 5, className = "" }: { rows?: number; className?: string }) {
  return (
    <div className={`${card} p-5 ${className}`}>
      <Sk className="h-4 w-40" />
      <div className="mt-5 flex flex-col gap-4">
        {Array.from({ length: rows }, (_, i) => (
          <Sk key={i} className="h-3.5" style={{ width: `${92 - ((i * 17) % 40)}%` }} />
        ))}
      </div>
    </div>
  );
}

/** «Обзор» на компьютере: шапка, фильтры, «Фокус дня», четыре KPI, график и задачи, отделы. */
export function DesktopOverviewSkeleton() {
  return (
    <Busy className="flex flex-col gap-5 px-4 pb-12 pt-6 sm:px-6 lg:px-8 lg:pt-8">
      <div className="flex flex-col gap-2">
        <Sk className="h-3.5 w-56" />
        <Sk className="h-9 w-80 rounded-lg" />
        <Sk className="h-4 w-96 max-w-full" />
      </div>
      <Sk className="h-[38px] w-[520px] max-w-full rounded-[10px]" />
      <Block rows={3} className="h-44 rounded-[14px]" />
      <div className="grid grid-cols-2 gap-3 sm:gap-4 xl:grid-cols-4">
        {[0, 1, 2, 3].map((i) => (
          <div key={i} className={`${card} flex h-[136px] flex-col justify-between rounded-[14px] p-5`}>
            <Sk className="h-3.5 w-28" />
            <Sk className="h-8 w-32" />
            <Sk className="h-1.5 w-full rounded-full" />
          </div>
        ))}
      </div>
      <div className="grid grid-cols-1 gap-4 xl:grid-cols-[minmax(0,1fr)_380px]">
        <div className={`${card} flex h-[380px] flex-col rounded-[14px] p-5`}>
          <Sk className="h-4 w-40" />
          <div className="mt-6 flex flex-1 items-end gap-2">
            {[45, 60, 52, 70, 64, 80, 72, 86, 66, 90, 84, 76].map((v, i) => (
              <Sk key={i} className="flex-1 rounded-b-none rounded-t-[5px]" style={{ height: `${v}%` }} />
            ))}
          </div>
        </div>
        <Block rows={7} className="h-[380px] rounded-[14px]" />
      </div>
      <Block rows={8} className="h-[440px] rounded-[14px]" />
    </Busy>
  );
}

/** Продажи: плитки KPI и две карточки. outside — макет вне layout раздела (переход из другого раздела): с местом под панель периода. */
function SalesSkeleton({ pathname, outside }: { pathname: string; outside: boolean }) {
  return (
    <Busy>
      {outside && (
        <div className="border-b border-line bg-surface">
          <div className="mx-auto flex h-[60px] max-w-[1800px] items-center gap-2.5 px-4 sm:px-6">
            <Sk className="h-9 w-44 rounded-lg" />
            <Sk className="h-9 w-32 rounded-lg" />
            <Sk className="ml-auto h-9 w-36 rounded-lg" />
          </div>
        </div>
      )}
      <Header pathname={pathname} />
      <PageBody>
        <Tiles count={6} className="grid-cols-2 lg:grid-cols-3 2xl:grid-cols-6" />
        <div className="mt-5 grid gap-4 md:grid-cols-2">
          <Block rows={6} />
          <Block rows={6} />
        </div>
      </PageBody>
    </Busy>
  );
}

/** HR со своим оформлением (обзор, табель, отчёты, карточка сотрудника) и списки HR в общей шапке. */
function HrSkeleton({ pathname }: { pathname: string }) {
  const t = useT();
  const frame = (children: React.ReactNode, className = "mx-auto flex max-w-[1600px] flex-col gap-4 px-4 pb-8 pt-5 md:px-6 lg:gap-5 lg:px-8 lg:pb-10 lg:pt-7") => (
    <div className={`${ts.root} ${layout.page} min-h-full`}>
      <div className={className}>{children}</div>
    </div>
  );
  if (pathname === "/hr") return frame(<OverviewSkeleton />, "mx-auto max-w-[1600px] px-4 pb-8 pt-5 md:px-6 lg:px-8 lg:pb-9 lg:pt-7");
  if (pathname === "/hr/reports") return frame(<ReportSkeleton header label={t("Загрузка")} />);
  if (pathname === "/hr/timesheets") return <TimesheetSkeleton />;
  if (/^\/hr\/employees\/[^/]+$/.test(pathname)) {
    const bar = (className: string) => <span className={`block rounded ${ts.skeleton} ${className}`} />;
    return frame(
      <>
        <div className="flex items-center gap-4">
          <span className={`size-14 shrink-0 rounded-full md:size-[60px] ${ts.skeleton}`} />
          <div className="flex flex-col gap-2.5">
            {bar("h-6 w-72 max-w-[60vw]")}
            {bar("h-3.5 w-96 max-w-[70vw]")}
          </div>
        </div>
        {bar("h-10 w-full max-w-[460px] rounded-[12px]")}
        <EmployeeSkeleton />
      </>,
      "mx-auto flex max-w-[1600px] flex-col gap-5 px-4 pb-10 pt-5 md:gap-6 md:px-6 lg:px-8 lg:pt-6",
    );
  }
  return (
    <Busy>
      <Header pathname={pathname} />
      <PageBody>
        <Filters />
        <Table rows={10} />
      </PageBody>
    </Busy>
  );
}

/** Табель: шапка, фильтры, сводка и строки таблицы. */
function TimesheetSkeleton() {
  const bar = (className: string) => <span className={`block rounded ${ts.skeleton} ${className}`} />;
  return (
    <div className={`${ts.root} min-h-full`} aria-busy="true">
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
              <span className={`size-[30px] shrink-0 rounded-full ${ts.skeleton}`} />
              {bar("h-3 w-40")}
              <span className="ml-6 flex gap-1 overflow-hidden">
                {Array.from({ length: 20 }, (_, c) => (
                  <span key={c} className={`h-[22px] w-7 shrink-0 rounded-[5px] ${ts.skeleton}`} />
                ))}
              </span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

/** Канбан: шапка, фильтры, четыре колонки на компьютере, группы по статусам на телефоне. */
function BoardSkeleton() {
  const t = useT();
  return (
    <Busy className="tasks-ui flex min-h-full flex-col gap-4 text-ink max-lg:bg-ai-bg lg:h-full lg:px-8 lg:py-6">
      <header className="hidden lg:block">
        <p className="flex items-center gap-1.5 text-xs text-subtle">
          OneBase <span aria-hidden>/</span> {t("Задачи")}
        </p>
        <h1 className="mt-1.5 text-[28px] font-bold leading-tight tracking-tight text-ink">{t("Канбан")}</h1>
        <p className="mt-1 text-[13px] text-ink-3">{t("Перетащите карточку в другую колонку, чтобы сменить статус")}</p>
      </header>
      <div className="lg:hidden">
        <div className="flex items-center gap-2.5 px-4 pb-3 pt-[calc(env(safe-area-inset-top)+14px)]">
          <h1 className="min-w-0 flex-1 truncate text-[28px] font-bold leading-tight tracking-tight text-ink">{t("Канбан")}</h1>
        </div>
      </div>
      <div className="flex gap-2 overflow-hidden px-4 lg:px-0">
        {[300, 146, 112, 160, 128, 148].map((w, i) => (
          <Sk key={i} className="h-10 shrink-0 rounded-[10px]" style={{ width: w }} />
        ))}
      </div>
      <div className="hidden min-h-[360px] flex-1 grid-cols-4 gap-3.5 lg:grid">
        {[3, 4, 2, 3].map((n, c) => (
          <section key={c} className="flex min-h-0 flex-col overflow-hidden rounded-[14px] border border-line bg-[var(--tasks-column)]">
            <header className="flex items-center gap-2 pb-2.5 pl-3.5 pr-3 pt-3">
              <Sk className="h-4 w-24" />
            </header>
            <div className="flex flex-col gap-2 px-2.5 pb-2.5">
              {Array.from({ length: n }, (_, i) => (
                <div key={i} className="flex flex-col gap-2.5 rounded-xl border border-line bg-surface p-3">
                  <Sk className="h-3.5 w-[85%]" />
                  <Sk className="h-3 w-1/2" />
                  <div className="flex items-center justify-between">
                    <Sk className="size-6 rounded-full" />
                    <Sk className="h-3 w-16" />
                  </div>
                </div>
              ))}
            </div>
          </section>
        ))}
      </div>
      <div className="flex flex-col gap-2.5 px-4 pb-10 lg:hidden">
        {[0, 1, 2, 3].map((i) => (
          <div key={i} className="flex min-h-[64px] flex-col gap-2 rounded-xl border border-line bg-surface px-3 py-3">
            <Sk className="h-3.5 w-28" />
            <Sk className="h-3 w-44" />
          </div>
        ))}
      </div>
    </Busy>
  );
}

/** Карточка задачи: шапка, название, этапы, описание и активность слева, детали справа. */
function TaskSkeleton() {
  return (
    <Busy className="tasks-ui min-h-full text-ink max-lg:bg-ai-bg">
      <div className="sticky top-0 z-20 flex h-[calc(env(safe-area-inset-top)+60px)] items-center gap-3 border-b border-line bg-ai-bg/90 px-3 pt-[env(safe-area-inset-top)] backdrop-blur-xl lg:hidden">
        <span className="grid size-10 shrink-0 place-items-center rounded-full border border-line bg-surface text-ink-2">
          <ChevronLeft className="size-5" aria-hidden />
        </span>
        <div className="flex min-w-0 flex-1 flex-col items-center gap-1.5">
          <Sk className="h-3.5 w-32" />
          <Sk className="h-2.5 w-24" />
        </div>
        <span className="size-10 shrink-0" />
      </div>
      <div className="mx-auto flex max-w-[1800px] flex-col gap-4 px-4 pb-6 pt-4 lg:gap-5 lg:px-8 lg:pb-10 lg:pt-6">
        <div className="hidden items-center gap-3 lg:flex">
          <Sk className="h-9 w-24 rounded-lg" />
          <Sk className="h-3.5 w-64" />
        </div>
        <div className="flex flex-col gap-2.5">
          <Sk className="h-7 w-[32rem] max-w-full rounded-lg" />
          <div className="flex gap-2">
            {[90, 110, 80].map((w, i) => (
              <Sk key={i} className="h-6 rounded-full" style={{ width: w }} />
            ))}
          </div>
        </div>
        <div className={`${card} flex gap-2 p-4`}>
          {[0, 1, 2, 3, 4].map((i) => (
            <Sk key={i} className="h-2 flex-1 rounded-full" />
          ))}
        </div>
        <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_380px] lg:gap-5">
          <div className="flex flex-col gap-4 lg:gap-5">
            <Block rows={4} />
            <Block rows={6} />
          </div>
          <Block rows={8} />
        </div>
      </div>
    </Busy>
  );
}

/** «Задачи → Обзор»: семь плиток, сроки и отделы. */
function TasksOverviewSkeleton({ pathname }: { pathname: string }) {
  return (
    <Busy>
      <Header pathname={pathname} />
      <PageBody>
        <Tiles count={7} className="grid-cols-2 sm:grid-cols-4 xl:grid-cols-7" />
        <div className="mt-5 grid gap-4 lg:grid-cols-2">
          <Block rows={6} />
          <Block rows={6} />
        </div>
      </PageBody>
    </Busy>
  );
}

/** Уведомления: шапка экрана, фильтры и список. */
function NotificationsSkeleton() {
  const t = useT();
  return (
    <Busy className="notif-ui min-h-full bg-ai-bg text-ink">
      <header className="sticky top-0 z-10 flex items-center gap-2.5 border-b border-line bg-ai-bg/90 px-3 pb-2 pt-[calc(env(safe-area-inset-top)+8px)] backdrop-blur-xl lg:static lg:mx-auto lg:max-w-[760px] lg:border-0 lg:bg-transparent lg:px-4 lg:pt-6 lg:backdrop-blur-none">
        <span className="grid size-10 shrink-0 place-items-center rounded-full border border-line bg-surface text-ink-2">
          <ChevronLeft className="size-5" aria-hidden />
        </span>
        <h1 className="flex min-w-0 flex-1 items-center gap-2 text-xl font-bold">{t("Уведомления")}</h1>
      </header>
      <div className="mx-auto flex max-w-[760px] flex-col gap-4 px-4 pb-8 pt-2">
        <div className="flex gap-2 overflow-hidden">
          {[72, 132, 84, 52, 64].map((w, i) => (
            <Sk key={i} className="h-9 shrink-0 rounded-full" style={{ width: w }} />
          ))}
        </div>
        <ul className="divide-y divide-line overflow-hidden rounded-[18px] border border-line bg-surface">
          {[0, 1, 2, 3, 4].map((i) => (
            <li key={i} className="flex gap-3 px-4 py-4">
              <Sk className="size-[38px] shrink-0 rounded-[12px]" />
              <div className="flex flex-1 flex-col gap-2">
                <Sk className="h-4 w-[70%]" />
                <Sk className="h-3.5 w-[90%]" />
                <Sk className="h-3 w-24" />
              </div>
            </li>
          ))}
        </ul>
      </div>
    </Busy>
  );
}

/** Консультант: колонка чатов (компьютер), шапка чата, приветствие или переписка и поле вопроса внизу. */
function ConsultantSkeleton({ chat }: { chat: boolean }) {
  const t = useT();
  return (
    <Busy className="ai-chat relative flex h-full overflow-hidden">
      <aside className="hidden w-[300px] shrink-0 border-r border-line bg-[var(--ai-sidebar)] lg:block">
        <div className="flex h-full flex-col gap-3.5 px-3.5 pb-3.5 pt-[18px]">
          <div className="flex items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-[#3d7bff] to-[#5b8cff] p-3 text-sm font-semibold text-white shadow-[0_6px_18px_rgb(61_123_255/0.25)]">
            <Plus className="size-4" aria-hidden />
            {t("Новый чат")}
          </div>
          <Sk className="h-10 rounded-[10px]" />
          <Sk className="h-[42px] rounded-[10px]" />
          <div className="mt-2 flex flex-col gap-4 px-2.5">
            {Array.from({ length: 7 }, (_, i) => (
              <Sk key={i} className="h-3.5" style={{ width: `${88 - ((i * 13) % 35)}%` }} />
            ))}
          </div>
        </div>
      </aside>
      <section className="relative flex min-w-0 flex-1 flex-col">
        <header className="flex shrink-0 items-center gap-3 border-b border-line px-7 lg:h-[68px] max-lg:gap-2.5 max-lg:px-3 max-lg:pb-2 max-lg:pt-[calc(env(safe-area-inset-top)+8px)]">
          <span className="size-10 shrink-0 rounded-full border border-line bg-surface lg:hidden" />
          <span className="hidden size-9 shrink-0 lg:block" />
          <span className="hidden size-[38px] shrink-0 place-items-center rounded-[10px] bg-violet-soft text-violet lg:grid">
            <Sparkles className="size-[18px]" aria-hidden />
          </span>
          <div className="min-w-0 flex-1">
            {chat ? <Sk className="h-4 w-56 max-w-full" /> : <h1 className="truncate text-base font-bold text-ink">{t("AI-консультант")}</h1>}
            <Sk className="mt-1.5 h-3 w-48 max-w-full" />
          </div>
        </header>
        <div className="flex min-h-0 flex-1 flex-col overflow-hidden px-4 pb-[150px]">
          {chat ? (
            <div className="mx-auto flex w-full max-w-[840px] flex-col gap-6 py-8">
              <Sk className="ml-auto h-10 w-[55%] rounded-2xl" />
              <div className="flex flex-col gap-3">
                {[96, 88, 92, 70, 84].map((w, i) => (
                  <Sk key={i} className="h-3.5" style={{ width: `${w}%` }} />
                ))}
              </div>
              <Block rows={4} />
            </div>
          ) : (
            <div className="m-auto w-full max-w-[840px] text-center">
              <div className="ai-gradient mx-auto grid size-[54px] place-items-center rounded-[17px] text-white shadow-[0_10px_32px_rgb(109_92_255/0.45)]">
                <Sparkles className="size-6" aria-hidden />
              </div>
              <h2 className="mt-5 text-[22px] font-bold tracking-tight text-ink">{t("Чем помочь бизнесу?")}</h2>
              <p className="mx-auto mt-2 max-w-[420px] text-sm leading-relaxed text-ink-3">
                {t("Спросите обычным языком — отвечу по данным OneBase и отделю факты от рекомендаций.")}
              </p>
              <div className="mt-7 text-left">
                <h3 className="mb-3 px-1 text-[15px] font-bold text-ink">{t("Быстрые вопросы")}</h3>
                <div className="grid grid-cols-2 gap-2.5 lg:grid-cols-3 lg:gap-3">
                  {Array.from({ length: 6 }, (_, i) => (
                    <Sk key={i} className="h-[56px] rounded-[14px]" />
                  ))}
                </div>
              </div>
            </div>
          )}
        </div>
        <div className="pointer-events-none absolute inset-x-0 bottom-0 z-10 px-3 pb-3 lg:px-6 max-lg:pb-[22px]">
          <div className="mx-auto max-w-[840px]">
            <div className="h-[98px] rounded-[18px] border border-line bg-surface" />
            <Sk className="mx-auto mt-2.5 h-3 w-[440px] max-w-full" />
          </div>
        </div>
      </section>
    </Busy>
  );
}

/** «Заявки»: обзор — плитки и блоки; новая заявка и правка — форма и итог справа; карточка — этапы, позиции и сведения. */
function RequestsSkeleton({ pathname }: { pathname: string }) {
  const form = pathname.endsWith("/new") || pathname.endsWith("/edit");
  return (
    <Busy>
      <Header pathname={pathname} />
      <PageBody>
        {pathname === "/requests" ? (
          <>
            <Tiles count={6} className="grid-cols-2 sm:grid-cols-3 xl:grid-cols-6" />
            <div className="mt-4 grid gap-4 xl:grid-cols-[3fr_2fr]">
              <Block rows={5} />
              <Block rows={5} />
            </div>
          </>
        ) : (
          <div className="mx-auto grid max-w-6xl gap-4 lg:grid-cols-[minmax(0,1fr)_300px]">
            <div className="flex flex-col gap-4">
              {!form && <Sk className="h-16 w-full rounded-2xl" />}
              <Block rows={form ? 4 : 6} />
              <Block rows={form ? 5 : 3} />
            </div>
            <Block rows={form ? 3 : 7} />
          </div>
        )}
      </PageBody>
    </Busy>
  );
}

/** «Снабжение → Обзор»: Supply AI, путь закупки из пяти этапов, согласование и показатели, аналитика. */
function ProcurementOverviewSkeleton({ pathname }: { pathname: string }) {
  return (
    <Busy>
      <Header pathname={pathname} />
      <PageBody>
        <div className="flex flex-col gap-4">
          <div className={`${card} p-4`}>
            <Sk className="h-11 w-full" />
            <Sk className="mt-3 h-7 w-2/3 max-lg:hidden" />
          </div>
          <div className={`${card} p-5`}>
            <Sk className="h-4 w-40" />
            <div className="mt-4 grid gap-2 lg:grid-cols-5">
              {Array.from({ length: 5 }, (_, i) => (
                <Sk key={i} className="h-16" />
              ))}
            </div>
          </div>
          <div className="grid gap-4 lg:grid-cols-[440px_minmax(0,1fr)]">
            <Block rows={4} />
            <Block rows={4} />
          </div>
          <Block rows={6} />
        </div>
      </PageBody>
    </Busy>
  );
}

/** Любая другая страница: шапка с названием, плитки и блоки. */
function PageSkeleton({ pathname }: { pathname: string }) {
  if (pathname === "/procurement") return <ProcurementOverviewSkeleton pathname={pathname} />;
  const list =
    pathname.startsWith("/tasks/") || pathname.startsWith("/settings/") || pathname.startsWith("/campaigns") || pathname.startsWith("/requests/") || pathname.startsWith("/procurement") || pathname.startsWith("/production");
  return (
    <Busy>
      <Header pathname={pathname} />
      <PageBody>
        {list ? (
          <>
            <Filters widths={[256, 180, 160]} />
            <Table rows={10} />
          </>
        ) : (
          <>
            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
              {Array.from({ length: 6 }, (_, i) => (
                <Block key={i} rows={3} />
              ))}
            </div>
          </>
        )}
      </PageBody>
    </Busy>
  );
}

/** Календарь: шапка, переключатель недели и семь дней (на телефоне — список). */
function CalendarSkeleton() {
  return (
    <Busy>
      <div className="me-ui min-h-full">
        <Header pathname="/calendar" />
        <PageBody>
          <div className="flex items-center gap-2">
            <Sk className="h-9 w-9" />
            <Sk className="h-9 w-9" />
            <Sk className="ml-1 h-6 w-48" />
          </div>
          <div className="mt-5 grid gap-3 lg:grid-cols-7 lg:gap-2.5">
            {Array.from({ length: 7 }, (_, i) => (
              <div key={i} className={`${card} p-3 lg:min-h-[340px]`}>
                <Sk className="h-3.5 w-24" />
                <Sk className="mt-3 h-14 w-full" />
              </div>
            ))}
          </div>
        </PageBody>
      </div>
    </Busy>
  );
}

/** Рабочий стол (кладовщик, лаборатория, снабжение, аналитик): шапка, четыре показателя, машины и поставки слева, решения справа. */
function WorkHomeSkeleton() {
  return (
    <Busy className="mx-auto flex max-w-[1440px] flex-col gap-5 px-4 pb-12 pt-4 sm:px-6 lg:px-8 lg:pt-8">
      <div className="space-y-2">
        <Sk className="h-4 w-64" />
        <Sk className="h-8 w-80" />
      </div>
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {[0, 1, 2, 3].map((i) => (
          <Sk key={i} className="h-[104px] rounded-2xl" />
        ))}
      </div>
      <div className="grid grid-cols-1 gap-5 lg:grid-cols-[minmax(0,1fr)_380px]">
        <div className="space-y-2">
          <Sk className="h-5 w-48" />
          {[0, 1, 2].map((i) => (
            <Sk key={i} className="h-24 rounded-2xl" />
          ))}
        </div>
        <div className="space-y-2">
          <Sk className="h-5 w-40" />
          <Sk className="h-48 rounded-2xl" />
        </div>
      </div>
    </Busy>
  );
}

/**
 * Макет страницы на время перехода (loading.tsx): по нажатию сразу открывается новая страница — её шапка с настоящим названием,
 * рамки карточек и таблиц, на месте данных мягкие плашки; данные приходят следом и встают на те же места. Макет выбирается по адресу.
 * inSales — макет внутри layout «Продаж» (переход между их страницами): панель периода уже на месте.
 */
export function RouteSkeleton({ inSales = false }: { inSales?: boolean }) {
  const t = useT();
  const pathname = usePathname();
  const home = useHomeKind();
  // Главная сотрудника и прямая ссылка — «Мой день».
  if ((pathname === "/" && home === "my_day") || pathname === "/me") return <MyDaySkeleton label={t("Загрузка")} />;
  if (pathname === "/" && home === "work") return <WorkHomeSkeleton />;
  if (pathname === "/") {
    return (
      <>
        <div className="max-md:hidden">
          <DesktopOverviewSkeleton />
        </div>
        <MobileOverviewSkeleton label={t("Загрузка")} />
      </>
    );
  }
  if (pathname.startsWith("/sales")) return <SalesSkeleton pathname={pathname} outside={!inSales} />;
  if (pathname === "/hr" || pathname.startsWith("/hr/")) return <HrSkeleton pathname={pathname} />;
  if (pathname === "/tasks") return <TasksOverviewSkeleton pathname={pathname} />;
  if (pathname === "/tasks/board") return <BoardSkeleton />;
  if (pathname.startsWith("/tasks/task/")) return <TaskSkeleton />;
  if (pathname === "/notifications") return <NotificationsSkeleton />;
  if (pathname === "/calendar") return <CalendarSkeleton />;
  if (
    pathname === "/requests" ||
    pathname === "/requests/new" ||
    pathname === "/procurement/orders/new" ||
    /^\/requests\/[0-9a-f-]{36}(\/edit|\/offers)?$/.test(pathname) ||
    pathname === "/procurement/receipts/new" ||
    /^\/procurement\/(orders|suppliers|receipts)\/[0-9a-f-]{36}(\/edit)?$/.test(pathname)
  )
    return <RequestsSkeleton pathname={pathname} />;
  if (pathname === "/consultant" || pathname.startsWith("/consultant/")) return <ConsultantSkeleton chat={pathname !== "/consultant"} />;
  if (pathname === "/analytik/chat" || pathname.startsWith("/analytik/chat/")) return <ConsultantSkeleton chat={pathname !== "/analytik/chat"} />;
  return <PageSkeleton pathname={pathname} />;
}
