"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useCallback, useMemo, useState, useTransition } from "react";
import { CircleAlert } from "lucide-react";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { divisionOptions, type HrDivision } from "@/lib/hr";
import {
  isViolation,
  matchesSummary,
  presetOf,
  presetRange,
  sortRows,
  weekendColumns,
  type PeriodPreset,
  type SummaryFilter,
  type TimesheetData,
  type TimesheetSort,
} from "@/lib/hr-timesheet";
import { PageHeader } from "./PageHeader";
import { PAGE_SIZES } from "./TablePagination";
import { TimesheetFilters, type Density } from "./TimesheetFilters";
import { TimesheetSummary, TimesheetSummarySkeleton } from "./TimesheetSummary";
import { documentCodes, TimesheetTable } from "./TimesheetTable";

const SORTS: TimesheetSort[] = ["name", "present", "late", "absent", "leave", "worked"];
const SUMMARY: SummaryFilter[] = ["present", "late", "absent"];
/** Параметры, которые меняет сервер (новый запрос табеля), и параметры экрана (меняются без запроса). */
const SERVER = ["from", "to", "divisionId", "search"] as const;

/**
 * «Рабочее время и табели» (редизайн): шапка со статусом Verifix, фильтры, сводка, табель с карточкой дня и страницами.
 * Период, подразделение и поиск — запрос табеля на сервере (переход с сохранением прокрутки, на это время — skeleton); нарушения, фильтр
 * из сводки, сортировка, страница, размер страницы и плотность — только в браузере (адрес обновляется без запроса). Всё хранится в адресе.
 */
export function TimesheetView({
  data,
  error,
  divisions,
  today,
  now,
  canManage,
  demo,
}: {
  data: TimesheetData | null;
  error: string | null;
  divisions: HrDivision[];
  today: string;
  now: number;
  canManage: boolean;
  demo: boolean;
}) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [pending, startTransition] = useTransition();
  const [retrying, setRetrying] = useState(false);
  const [retryError, setRetryError] = useState<string | null>(null);

  const from = data?.from ?? params.get("from") ?? `${today.slice(0, 8)}01`;
  const to = data?.to ?? params.get("to") ?? today;
  const divisionId = params.get("divisionId") ?? "";
  const search = params.get("search") ?? "";
  const violations = params.get("violations") === "1";
  const summary = SUMMARY.find((s) => s === params.get("only")) ?? null;
  const sort = SORTS.find((s) => s === params.get("sort")) ?? "name";
  const dir = params.get("dir") === "desc" ? "desc" : "asc";
  const size = PAGE_SIZES.find((n) => String(n) === params.get("size")) ?? 25;
  const density: Density = params.get("density") === "compact" ? "compact" : "normal";
  const requestedPage = Math.max(1, Number(params.get("page")) || 1);

  const build = useCallback(
    (patch: Record<string, string | null>) => {
      const next = new URLSearchParams(params.toString());
      for (const [k, v] of Object.entries(patch)) {
        if (v === null || v === "") next.delete(k);
        else next.set(k, v);
      }
      const q = next.toString();
      return q ? `${pathname}?${q}` : pathname;
    },
    [params, pathname],
  );

  /** Новый запрос табеля: период, подразделение, поиск; страница — с первой. */
  const setServer = useCallback(
    (patch: Partial<Record<(typeof SERVER)[number], string | null>>) => {
      const url = build({ ...patch, page: null });
      startTransition(() => router.replace(url, { scroll: false }));
    },
    [build, router],
  );

  /** Параметры экрана — адрес обновляется без запроса к серверу. */
  const setClient = useCallback((patch: Record<string, string | null>) => window.history.replaceState(null, "", build(patch)), [build]);

  const refresh = () => startTransition(() => router.refresh());

  const retry = async () => {
    setRetryError(null);
    if (canManage && !demo && data?.sync.lastStatus === "Failed") {
      setRetrying(true);
      try {
        await bff("integrations/verifix/sync", { method: "POST" });
      } catch (e) {
        setRetryError((e as Error).message);
      } finally {
        setRetrying(false);
      }
    }
    refresh();
  };

  const rows = useMemo(() => {
    let list = data?.rows ?? [];
    if (violations) list = list.filter((r) => r.codes.some(isViolation));
    if (summary) list = list.filter((r) => matchesSummary(r, summary));
    return sortRows(list, sort, dir);
  }, [data, violations, summary, sort, dir]);

  const pages = Math.max(1, Math.ceil(rows.length / size));
  const page = Math.min(requestedPage, pages);
  const pageRows = rows.slice((page - 1) * size, page * size);
  const weekend = useMemo(() => (data ? weekendColumns(data) : []), [data]);
  const documents = useMemo(() => documentCodes(data?.rows ?? []), [data]);
  const exportHref = demo || !data ? null : `/bff/api/hr/reports/export?from=${from}&to=${to}${divisionId ? `&divisionId=${divisionId}` : ""}`;
  const syncFailed = data?.sync.lastStatus === "Failed";

  const sync = data?.sync ?? { configured: true, enabled: true, running: false, lastSuccessAt: null, lastRunAt: null, lastStatus: null, lastMessage: null, tracksThrough: null, timesheetFrom: null, timesheetTo: null };

  return (
    <div className="flex flex-col gap-5 px-4 pb-8 pt-5 sm:px-8 sm:pt-6">
      <PageHeader sync={sync} now={now} refreshing={pending || retrying} onRefresh={refresh} exportHref={exportHref} />

      <TimesheetFilters
        from={from}
        to={to}
        today={today}
        preset={presetOf(from, to, today)}
        divisions={divisionOptions(divisions)}
        divisionId={divisionId}
        search={search}
        violations={violations}
        density={density}
        onPreset={(p: PeriodPreset) => setServer(presetRange(p, today))}
        onPeriod={(f, tt) => setServer({ from: f, to: tt })}
        onDivision={(id) => setServer({ divisionId: id || null })}
        onSearch={(q) => setServer({ search: q || null })}
        onViolations={(on) => setClient({ violations: on ? "1" : null, page: null })}
        onDensity={(d) => setClient({ density: d === "compact" ? "compact" : null })}
      />

      {demo && (
        <p className="-mt-2 text-xs font-medium text-(--ts-warning)">{t("Демо-данные для проверки вёрстки (только в режиме разработки).")}</p>
      )}

      {(error || syncFailed) && (
        <div role="alert" className="flex flex-wrap items-center gap-3 rounded-[12px] border border-(--ts-danger)/30 bg-(--ts-danger-soft) px-4 py-3 text-[13.5px] text-(--ts-text-2)">
          <CircleAlert aria-hidden className="size-4 shrink-0 text-(--ts-danger)" />
          <span className="min-w-0 flex-1">
            {error ? t("Не удалось загрузить табель: {message}", { message: t(error) }) : t("Ошибка синхронизации Verifix: {message}", { message: t(data?.sync.lastMessage ?? "синхронизация не прошла") })}
            {retryError && <span className="block text-(--ts-danger)">{retryError}</span>}
          </span>
          <button
            type="button"
            onClick={retry}
            disabled={pending || retrying}
            className="h-8 cursor-pointer rounded-[8px] border border-(--ts-danger)/40 bg-(--ts-surface) px-3 text-[13px] font-semibold text-(--ts-danger) transition-colors duration-150 hover:bg-(--ts-danger-soft) focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-(--ts-accent) disabled:opacity-60"
          >
            {t("Повторить")}
          </button>
        </div>
      )}

      {pending || !data ? (
        <TimesheetSummarySkeleton />
      ) : (
        <TimesheetSummary employees={data.employeesTotal ?? data.rows.length} total={data.total} filter={summary} onFilter={(f) => setClient({ only: f, page: null })} />
      )}

      {data || pending ? (
        <TimesheetTable
          from={from}
          to={to}
          dates={data?.dates ?? []}
          weekend={weekend}
          today={today}
          rows={pageRows}
          filtered={rows.length}
          employees={data?.employeesTotal ?? data?.rows.length ?? 0}
          documents={documents}
          density={density}
          sort={sort}
          dir={dir}
          page={page}
          size={size}
          loading={pending || !data}
          demo={demo}
          onSort={(key) => setClient({ sort: key === "name" ? null : key, dir: sort === key ? (dir === "asc" ? "desc" : null) : key === "name" ? null : "desc", page: null })}
          onPage={(n) => {
            setClient({ page: n > 1 ? String(n) : null });
            document.querySelector("[data-app-scroll]")?.scrollTo({ top: 0, behavior: "smooth" });
          }}
          onSize={(n) => setClient({ size: n === 25 ? null : String(n), page: null })}
          onReset={() => startTransition(() => router.replace(pathname, { scroll: false }))}
        />
      ) : null}
    </div>
  );
}
