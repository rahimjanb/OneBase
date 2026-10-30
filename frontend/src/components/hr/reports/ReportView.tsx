"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useCallback, useOptimistic, useState, useTransition } from "react";
import { CalendarX2, CircleAlert, Info } from "lucide-react";
import { useT } from "@/i18n/client";
import { addDays, qs, type HrDivision, type HrReport, type HrReportGroup } from "@/lib/hr";
import { parseDepts, periodOf, periodRange, timesheetRange, type PeriodKind, type SortKey } from "@/lib/hr-report";
import { card, focus } from "../overview/bits";
import { NotConnected } from "../overview/OverviewStates";
import { DivisionsList } from "./DivisionsList";
import { DivisionsTable, firstDir, rowId, type Sort } from "./DivisionsTable";
import { KpiCards, TopDivisions, TotalsCard, type SortTarget } from "./ReportCards";
import { ReportFilters } from "./ReportFilters";
import { ReportHeader } from "./ReportHeader";
import { ReportSkeleton } from "./ReportSkeleton";
import { AbsentCard, LateCard } from "./PeopleCards";

type Filters = { from: string; to: string; depts: number[]; period: PeriodKind };

/**
 * «Отчёт по посещаемости» (HR → Отчёты): шапка со свежестью Verifix и Excel, фильтры, KPI, топ-3 подразделений, таблица (телефон — список),
 * частые опоздания и отсутствия. Период и подразделения — в адресе (?from=&to=&period=&dept=1,2): смена сразу перезагружает отчёт, фильтры
 * показывают выбор сразу, данные под ними — скелетоны до ответа. Сортировка, поиск и подсветка — только в браузере.
 */
export function ReportView({
  report,
  error,
  divisions,
  today,
  now,
  manages,
}: {
  report: HrReport | null;
  error: string | null;
  divisions: HrDivision[];
  today: string;
  now: number;
  manages: boolean;
}) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [pending, startTransition] = useTransition();

  const from = report?.from ?? params.get("from") ?? `${today.slice(0, 8)}01`;
  const to = report?.to ?? params.get("to") ?? today;
  const current: Filters = { from, to, depts: parseDepts(params.get("dept")), period: periodOf(from, to, today, params.get("period")) };
  const [filters, setFilters] = useOptimistic(current);

  const [sort, setSort] = useState<Sort>({ key: "name", dir: "asc" });
  const [mobileSort, setMobileSort] = useState<Sort>({ key: "attendance", dir: "asc" });
  const [expanded, setExpanded] = useState(false);
  const [highlight, setHighlight] = useState<string | null>(null);

  const go = useCallback(
    (next: Filters) => {
      const q = new URLSearchParams();
      q.set("from", next.from);
      q.set("to", next.to);
      if (next.period !== "custom") q.set("period", next.period);
      if (next.depts.length) q.set("dept", next.depts.join(","));
      setExpanded(false);
      setHighlight(null);
      startTransition(() => {
        setFilters(next);
        router.replace(`${pathname}?${q.toString()}`, { scroll: false });
      });
    },
    [pathname, router, setFilters],
  );

  const onPeriod = (kind: Exclude<PeriodKind, "custom">) => go({ ...filters, ...periodRange(kind, filters.to, today), period: kind });
  const onRange = (f: string, tt: string) => go({ ...filters, from: f, to: tt, period: periodOf(f, tt, today) });
  const onDepts = (depts: number[]) => go({ ...filters, depts });

  const onTableSort = (key: SortKey) => setSort((s) => (s.key === key ? { key, dir: s.dir === "asc" ? "desc" : "asc" } : { key, dir: firstDir(key) }));

  /** «Опоздания» и «Отсутствия» в KPI: таблица (и список на телефоне) — по этой колонке по убыванию, и прокрутка к ней. */
  const onKpiSort = (target: SortTarget) => {
    setSort({ key: target, dir: "desc" });
    setMobileSort({ key: target, dir: "desc" });
    requestAnimationFrame(() => {
      const table = document.getElementById("divisions-title");
      const target_ = table?.offsetParent ? table : document.getElementById("divisions-m-title");
      target_?.closest("section")?.scrollIntoView({ behavior: "smooth", block: "start" });
    });
  };

  /** Отдел из топа: строка таблицы (на телефоне — списка) прокручивается в видимую часть и подсвечивается. */
  const pick = (g: HrReportGroup) => {
    const id = rowId(g);
    setExpanded(true);
    setHighlight(id);
    requestAnimationFrame(() => {
      const row = document.getElementById(id);
      const target = row?.offsetParent ? row : document.getElementById(`m-${id}`);
      target?.scrollIntoView({ behavior: "smooth", block: "center" });
    });
    setTimeout(() => setHighlight((h) => (h === id ? null : h)), 2200);
  };

  const empty = !!report && report.total.planDays === 0 && report.total.presentDays === 0 && report.total.absentDays === 0;
  const exportHref = (() => {
    if (!report || pending || empty) return null;
    const q = new URLSearchParams({ from: report.from, to: report.to });
    for (const d of current.depts) q.append("divisionId", String(d));
    return `/bff/api/hr/reports/export?${q.toString()}`;
  })();

  const period = { from: filters.from, to: filters.to };
  const sheetRange = timesheetRange(filters.from, filters.to);
  const one = filters.depts.length === 1 ? filters.depts[0] : undefined;
  const hrefOf = (g: HrReportGroup) => (g.divisionId == null ? null : `/hr/timesheets${qs({ ...sheetRange, divisionId: g.divisionId })}`);
  const absentHref = `/hr/deviations${qs({ kind: "absent", ...sheetRange, divisionId: one })}`;

  const single = !!report && report.divisions.length <= 1;
  const lastMonth = (() => {
    const start = `${addDays(`${today.slice(0, 8)}01`, -1).slice(0, 8)}01`;
    return { from: start, to: addDays(`${today.slice(0, 8)}01`, -1) };
  })();

  return (
    <div className="mx-auto flex max-w-[1600px] flex-col gap-4 px-4 pb-8 pt-5 md:px-6 lg:gap-5 lg:px-8 lg:pb-10 lg:pt-7">
      <ReportHeader sync={report?.sync ?? null} now={now} exportHref={exportHref} />

      <ReportFilters
        from={filters.from}
        to={filters.to}
        today={today}
        period={filters.period}
        depts={filters.depts}
        divisions={divisions}
        onPeriod={onPeriod}
        onRange={onRange}
        onDepts={onDepts}
      />

      {error && (
        <div role="alert" className="flex flex-wrap items-center gap-3 rounded-[12px] border border-(--ts-danger)/30 bg-(--ts-danger-soft) px-4 py-3 text-[13.5px] text-(--ts-text-2)">
          <CircleAlert aria-hidden className="size-4 shrink-0 text-(--ts-danger)" />
          <span className="min-w-0 flex-1">{t("Не удалось загрузить отчёт: {message}", { message: t(error) })}</span>
          <button
            type="button"
            onClick={() => startTransition(() => router.refresh())}
            className={`h-8 cursor-pointer rounded-[8px] border border-(--ts-danger)/40 bg-(--ts-surface) px-3 text-[13px] font-semibold text-(--ts-danger) hover:bg-(--ts-danger-soft) ${focus}`}
          >
            {t("Повторить")}
          </button>
        </div>
      )}

      {pending ? (
        <ReportSkeleton label={t("Загрузка")} />
      ) : !report ? null : !report.sync.configured ? (
        <NotConnected manages={manages} />
      ) : empty ? (
        <section className={`${card} flex flex-col items-center gap-3 px-6 py-12 text-center`}>
          <span className="grid size-12 place-items-center rounded-full bg-(--ts-surface-2) text-(--ts-muted)">
            <CalendarX2 aria-hidden className="size-6" />
          </span>
          <h2 className="text-base font-semibold text-(--ts-text)">{t("За выбранный период нет данных табеля")}</h2>
          <p className="max-w-[420px] text-sm text-(--ts-muted)">{t("Табель Verifix загружается с начала подключения. Выберите другой период.")}</p>
          <button
            type="button"
            onClick={() => go({ ...filters, ...lastMonth, period: "month" })}
            className={`mt-1 h-10 cursor-pointer rounded-[10px] bg-(--ts-accent) px-4 text-sm font-semibold text-(--ts-on-accent) hover:opacity-90 ${focus}`}
          >
            {t("Прошлый месяц")}
          </button>
        </section>
      ) : (
        <>
          <KpiCards report={report} onSort={onKpiSort} />
          {!single && <TopDivisions groups={report.divisions} onPick={pick} />}
          <DivisionsTable groups={report.divisions} sort={sort} onSort={onTableSort} highlight={highlight} single={single} hrefOf={hrefOf} />
          <DivisionsList
            groups={report.divisions}
            sort={mobileSort}
            onSort={setMobileSort}
            expanded={expanded}
            onExpand={() => setExpanded(true)}
            highlight={highlight}
            single={single}
            hrefOf={hrefOf}
          />
          <div className="grid items-stretch gap-4 lg:grid-cols-2">
            <LateCard byCount={report.topLate} byTime={report.topLateTime ?? report.topLate} period={period} />
            <AbsentCard rows={report.topAbsent} wholePeriod={report.absentWholePeriod ?? 0} period={period} allHref={absentHref} />
          </div>
          <TotalsCard report={report} />
        </>
      )}

      <p className="flex items-start gap-2 text-xs text-(--ts-subtle)">
        <Info aria-hidden className="mt-px size-3.5 shrink-0" />
        <span className="max-md:hidden">
          {t("Явка — доля рабочих дней с приходом среди дней с ясным итогом (пришёл или отсутствовал). В Excel: итоги подразделений, табель сотрудников по дням и список отклонений.")}
        </span>
        <span className="md:hidden">{t("Явка — доля рабочих дней с приходом среди дней с ясным итогом. В Excel: итоги подразделений, табель по дням и отклонения.")}</span>
      </p>
    </div>
  );
}
