"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState, useTransition } from "react";
import { CircleAlert } from "lucide-react";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { addDays, qs, type HrOverview } from "@/lib/hr";
import { dataAsOf } from "@/lib/hr-overview";
import { AttendanceCard } from "./AttendanceCard";
import { AttentionCard } from "./AttentionCard";
import { DivisionsCard } from "./DivisionsCard";
import { KpiRow } from "./KpiRow";
import { NoStaff, NotConnected, OverviewSkeleton } from "./OverviewStates";
import { OverviewHeader } from "./OverviewHeader";
import { BranchesCard, HoursChart } from "./PassesCards";
import { StatusNote } from "./StatusNote";
import layout from "./overview.module.css";

/** Обновление данных: раз в минуту, пока идёт синхронизация — чаще, чтобы показать её итог сразу. */
const REFRESH_MS = 60_000;
const SYNCING_MS = 10_000;

/**
 * «Обзор HR»: шапка, KPI, явка, «Требует внимания», подразделения, проходы по часам и филиалам, пояснение о статусах.
 * День — параметр адреса ?date= (сегодня — без параметра); пока грузится другой день — скелетоны. Сегодняшние данные обновляются сами.
 * Verifix не подключён — одна карточка с переходом в настройки; сотрудников ещё нет — подсказка и «Синхронизировать».
 */
export function OverviewView({
  o,
  error,
  today,
  now,
  manages,
  demo,
}: {
  o: HrOverview | null;
  error: string | null;
  today: string;
  now: number;
  manages: boolean;
  demo: boolean;
}) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [navigating, startNav] = useTransition();
  const [, startRefresh] = useTransition();
  const [clock, setClock] = useState(now);
  const [syncing, setSyncing] = useState(false);
  const [syncError, setSyncError] = useState<string | null>(null);

  // «синхр. 2 мин назад» — по часам браузера; в демо время зафиксировано.
  useEffect(() => {
    if (demo) return;
    setClock(Date.now());
    const timer = setInterval(() => setClock(Date.now()), 30_000);
    return () => clearInterval(timer);
  }, [demo]);

  const live = !!o && o.isToday && o.sync.configured && !demo;
  const running = !!o?.sync.running;
  useEffect(() => {
    if (!live) return;
    const timer = setInterval(() => document.visibilityState === "visible" && startRefresh(() => router.refresh()), running ? SYNCING_MS : REFRESH_MS);
    return () => clearInterval(timer);
  }, [live, running, router]);

  const setDate = (day: string) => {
    const next = new URLSearchParams(params.toString());
    if (day === today) next.delete("date");
    else next.set("date", day);
    const q = next.toString();
    startNav(() => router.push(q ? `${pathname}?${q}` : pathname, { scroll: false }));
  };

  /** Запуск синхронизации (администратор интеграций): «Повторить» после ошибки и «Синхронизировать» без сотрудников. */
  const sync = manages && !demo
    ? async () => {
        setSyncError(null);
        setSyncing(true);
        try {
          await bff("integrations/verifix/sync", { method: "POST" });
        } catch (e) {
          setSyncError((e as Error).message);
        } finally {
          setSyncing(false);
        }
        startRefresh(() => router.refresh());
      }
    : undefined;

  const date = o?.date ?? today;
  const connected = !!o?.sync.configured;
  const attendanceHref = (status: string) => `/hr/attendance${qs({ status, date })}`;

  return (
    <div className="mx-auto flex max-w-[1600px] flex-col gap-4 px-4 pb-8 pt-5 md:px-6 lg:gap-5 lg:px-8 lg:pb-9 lg:pt-7">
      {o ? (
        <OverviewHeader
          date={date}
          today={today}
          yesterday={addDays(today, -1)}
          asOf={dataAsOf(o)}
          isToday={o.isToday}
          sync={o.sync}
          now={clock}
          reportHref={demo ? null : `/bff/api/hr/reports/export${qs({ from: date, to: date })}`}
          connected={connected}
          onDate={setDate}
          onRetry={sync}
          retrying={syncing || running}
          note={o.seesAll ? null : t("Показаны сотрудники подразделений вашего отдела.")}
        />
      ) : (
        <h1 className="text-[26px] font-bold tracking-[-0.5px] text-(--ts-text) lg:text-[30px]">{t("Обзор HR")}</h1>
      )}

      {demo && <p className="-mt-1 text-xs font-medium text-(--ts-warning)">{t("Демо-данные для проверки вёрстки (только в режиме разработки).")}</p>}

      {(error || syncError) && (
        <div role="alert" className="flex flex-wrap items-center gap-3 rounded-[12px] border border-(--ts-danger)/30 bg-(--ts-danger-soft) px-4 py-3 text-[13.5px] text-(--ts-text-2)">
          <CircleAlert aria-hidden className="size-4 shrink-0 text-(--ts-danger)" />
          <span className="min-w-0 flex-1">{error ? t("Не удалось загрузить обзор: {message}", { message: t(error) }) : syncError}</span>
          {error && (
            <button
              type="button"
              onClick={() => startNav(() => router.refresh())}
              className="h-8 cursor-pointer rounded-[8px] border border-(--ts-danger)/40 bg-(--ts-surface) px-3 text-[13px] font-semibold text-(--ts-danger) hover:bg-(--ts-danger-soft) focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-(--ts-accent)"
            >
              {t("Повторить")}
            </button>
          )}
        </div>
      )}

      {!o ? null : !connected ? (
        <NotConnected manages={manages} />
      ) : navigating ? (
        <OverviewSkeleton header={false} />
      ) : o.active === 0 ? (
        <NoStaff onSync={sync} syncing={syncing} running={running} />
      ) : (
        <div className={layout.grid}>
          <div className={layout.kpi}>
            <KpiRow o={o} />
          </div>
          <div className={layout.att}>
            <AttendanceCard o={o} attendanceHref={attendanceHref} />
          </div>
          <div className={layout.warn}>
            <div className={layout.warnFill}>
              <AttentionCard rows={o.attention} total={o.attentionTotal} allHref={`/hr/deviations${qs({ from: date, to: date })}`} />
            </div>
          </div>
          <div className={layout.div}>
            <DivisionsCard divisions={o.divisions} attendanceHref={(divisionId) => `/hr/attendance${qs({ divisionId, date })}`} />
          </div>
          <div className={layout.side}>
            <div className={layout.hours}>
              <HoursChart o={o} />
            </div>
            <div className={`${layout.branches} flex flex-1 flex-col`}>
              <BranchesCard o={o} />
            </div>
          </div>
          <div className={layout.note}>
            <StatusNote o={o} />
          </div>
        </div>
      )}
    </div>
  );
}
