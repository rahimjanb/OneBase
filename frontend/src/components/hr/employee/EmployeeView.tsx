"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useMemo, useState, useTransition } from "react";
import { CalendarX2, TriangleAlert } from "lucide-react";
import { useT } from "@/i18n/client";
import { addDays, qs, VERIFIX_SETTINGS, type HrEmployeeCard } from "@/lib/hr";
import { displayName, insightOf, marksByDay, periodStats, type DayFilter } from "@/lib/hr-employee";
import styles from "@/components/hr/timesheet/timesheet.module.css";
import { card, focus } from "../overview/bits";
import { DayStrip } from "./DayStrip";
import { DaysTable } from "./DaysTable";
import { InsightCard } from "./InsightCard";
import { MarksJournal } from "./MarksJournal";
import { PeriodBar } from "./PeriodBar";
import { ProfileHeader, ReportButton } from "./ProfileHeader";
import { NoteCard, VerifixCard } from "./SideCards";
import { StatsCards } from "./StatsCards";

/** «28.10 в 09:10» по Ташкенту. */
const moment = (iso: string) =>
  new Intl.DateTimeFormat("ru-RU", { timeZone: "Asia/Tashkent", day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" }).format(new Date(iso)).replace(",", " в");

/**
 * Карточка сотрудника HR: профиль, период, итоги, подсказка, полоса дней, посещаемость по дням и все отметки периода. Две колонки (данные Verifix и заметка слева, период справа) — когда содержимому хватает ширины (от 1100 px: с боковым
 * меню приложения — экран от ~1400); уже — одна колонка, данные и заметка уходят вниз. Период — в адресе (?from=&to=), смена сразу загружает
 * данные (на это время — скелетоны). Фильтр таблицы и подсветка дня — только в браузере.
 */
export function EmployeeView({ data, today, manages, backHref, demo }: { data: HrEmployeeCard; today: string; manages: boolean; backHref: string; demo: boolean }) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [pending, startTransition] = useTransition();
  const [filter, setFilter] = useState<DayFilter>("all");
  const [highlight, setHighlight] = useState<string | null>(null);

  const name = displayName(data.employee.fullName);
  const marks = useMemo(() => marksByDay(data.days, data.events), [data]);
  const stats = useMemo(() => periodStats(data, marks), [data, marks]);
  const insight = useMemo(() => insightOf(data, stats, t), [data, stats, t]);
  const reportHref = demo ? null : `/bff/api/hr/reports/export${qs({ from: data.from, to: data.to, employeeId: data.employee.id })}`;
  const hasData = data.days.some((d) => d.firstIn || d.lastOut || d.status === "Absent" || d.status === "OnLeave") || data.events.length > 0;
  const stale = !data.sync.configured || data.sync.lastStatus === "Failed";

  const setPeriod = (from: string, to: string) => {
    const next = new URLSearchParams(params.toString());
    next.set("from", from);
    next.set("to", to);
    setFilter("all");
    startTransition(() => router.replace(`${pathname}?${next.toString()}`, { scroll: false }));
  };

  /** Клик по дню в полосе: строка таблицы (на телефоне — списка) прокручивается в видимую часть и подсвечивается. */
  const pickDay = (date: string) => {
    setFilter("all");
    setHighlight(date);
    requestAnimationFrame(() => {
      const row = document.getElementById(`day-${date}`);
      const target = row && row.offsetParent ? row : document.getElementById(`day-m-${date}`);
      target?.scrollIntoView({ behavior: "smooth", block: "center" });
    });
    setTimeout(() => setHighlight((h) => (h === date ? null : h)), 1800);
  };

  const lastMonth = (() => {
    const start = `${addDays(`${data.from.slice(0, 8)}01`, -1).slice(0, 8)}01`;
    return [start, addDays(`${data.from.slice(0, 8)}01`, -1)] as const;
  })();

  return (
    <div className="@container mx-auto flex max-w-[1600px] flex-col gap-4 px-4 pb-24 pt-0 md:gap-6 md:px-6 md:pb-10 md:pt-5 lg:px-8 lg:pt-6">
      <ProfileHeader card={data} name={name} backHref={backHref} reportHref={reportHref} />

      {demo && <p className="-mt-2 text-xs font-medium text-(--ts-warning)">{t("Демо-данные для проверки вёрстки (только в режиме разработки).")}</p>}

      {stale && (
        <div role="status" className="flex flex-wrap items-center gap-x-3 gap-y-1 rounded-[14px] border border-(--ts-warning)/20 bg-(--ts-warning-soft) px-4 py-3 text-[13px] text-(--ts-text-2)">
          <TriangleAlert aria-hidden className="size-4 shrink-0 text-(--ts-warning)" />
          <span className="min-w-0 flex-1">
            {data.sync.lastSuccessAt ? t("Данные могут быть неактуальны · обновлено {at}", { at: moment(data.sync.lastSuccessAt) }) : t("Данные могут быть неактуальны")}
            {data.sync.lastStatus === "Failed" && data.sync.lastMessage && <span className="text-(--ts-muted)"> · {t(data.sync.lastMessage)}</span>}
          </span>
          {manages && (
            <Link href={VERIFIX_SETTINGS} className={`rounded font-semibold text-(--ts-warning) hover:underline ${focus}`}>
              {t("Настройки интеграции")}
            </Link>
          )}
        </div>
      )}

      <div className="grid items-start gap-5 @min-[1100px]:grid-cols-[300px_minmax(0,1fr)] @min-[1260px]:grid-cols-[340px_minmax(0,1fr)]">
        <aside className="order-last grid gap-4 md:grid-cols-2 @min-[1100px]:sticky @min-[1100px]:top-6 @min-[1100px]:order-first @min-[1100px]:grid-cols-1">
          <VerifixCard data={data} today={today} />
          <NoteCard data={data} demo={demo} />
        </aside>

        <div className="flex min-w-0 flex-col gap-4">
          <PeriodBar from={data.from} to={data.to} today={today} onChange={setPeriod} />
          {pending ? (
            <EmployeeSkeleton />
          ) : !hasData ? (
            <section className={`${card} flex flex-col items-center gap-3 px-6 py-12 text-center`}>
              <span className="grid size-12 place-items-center rounded-full bg-(--ts-surface-2) text-(--ts-muted)">
                <CalendarX2 aria-hidden className="size-6" />
              </span>
              <h2 className="text-base font-semibold text-(--ts-text)">{t("За выбранный период нет отметок")}</h2>
              <button
                type="button"
                onClick={() => setPeriod(...lastMonth)}
                className={`mt-1 inline-flex h-10 cursor-pointer items-center rounded-[10px] border border-(--ts-border-strong) bg-(--ts-surface) px-4 text-sm font-medium text-(--ts-text) hover:bg-(--ts-surface-2) ${focus}`}
              >
                {t("Показать прошлый месяц")}
              </button>
            </section>
          ) : (
            <>
              <StatsCards s={stats} filter={filter} onFilter={setFilter} />
              {insight && <InsightCard insight={insight} storageKey={`hr-insight:${data.employee.id}:${data.from}:${data.to}:${insight.type}`} verifixUrl={data.verifixUrl} />}
              <DayStrip days={data.days} today={today} onPick={pickDay} />
              <DaysTable days={data.days} marks={marks} stats={stats} filter={filter} onFilter={setFilter} highlight={highlight} />
              <MarksJournal events={data.events} />
            </>
          )}
        </div>
      </div>

      {/* Телефон: «Скачать табель» закреплена над нижней панелью приложения. */}
      <div className="sticky bottom-3 z-20 md:hidden">
        <ReportButton href={reportHref} label="Скачать табель за период" className="h-12 w-full shadow-[0_8px_24px_#00000040]" />
      </div>
    </div>
  );
}

/** Пока данные загружаются: KPI, полоса дней и 8 строк таблицы серыми плашками. */
export function EmployeeSkeleton() {
  const bar = (className: string) => <span className={`block rounded ${styles.skeleton} ${className}`} />;
  return (
    <div aria-busy="true" className="flex flex-col gap-4">
      <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
        {Array.from({ length: 4 }, (_, i) => (
          <div key={i} className={`${card} flex h-[92px] flex-col gap-2.5 px-4 py-3.5`}>
            {bar("h-3 w-20")}
            {bar("h-6 w-14")}
            {bar("h-2.5 w-24")}
          </div>
        ))}
      </div>
      <div className={`${card} flex h-[118px] flex-col gap-3 px-[18px] py-4`}>
        {bar("h-3.5 w-32")}
        {bar("h-[58px] w-full rounded-[10px]")}
      </div>
      <div className={`${card} overflow-hidden`}>
        <div className="flex h-14 items-center px-5">{bar("h-4 w-48")}</div>
        {Array.from({ length: 8 }, (_, i) => (
          <div key={i} className="flex h-[58px] items-center gap-4 border-t border-(--ts-border) px-5">
            {bar("h-3.5 w-14")}
            {bar("h-5 w-24 rounded-[6px]")}
            {bar("ml-auto h-3.5 w-20")}
          </div>
        ))}
      </div>
    </div>
  );
}
