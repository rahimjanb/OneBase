"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  AlertTriangle,
  FileUp,
  GitCompareArrows,
  Globe,
  LayoutGrid,
  ListFilter,
  Loader2,
  Rows3,
  Search,
  Sparkles,
  X,
} from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { Sheet } from "@/components/field/Sheet";
import { ErrorText, useAction } from "@/components/recruitment/ui";
import { useT } from "@/i18n/client";
import { day, vacancyStatusLabel, when, type RecruitmentContext } from "@/lib/recruitment";
import { initials } from "@/lib/recruitment-view";
import { screeningApi, type Comparison, type ComparisonBrief, type ScreeningPage, type ScreeningRow, type ScreeningVacancy } from "@/lib/screening";
import {
  activeFilters,
  apiQuery,
  EMPTY_FILTERS,
  experienceText,
  filtersQuery,
  MAX_COMPARE,
  parseFilters,
  SETTABLE,
  statusLabel,
  STATUSES,
  toggleCompare,
  type ScreeningFilters,
  type ScreeningStatus,
} from "@/lib/screening-view";
import { CompareSheet } from "./CompareSheet";
import { ImportSheet } from "./ImportSheet";
import { HhStrip, Kpi, ScoreRing, StatusPill, toneClass } from "./parts";

/*
 * HR → «Подбор» → «AI-скрининг». Сверху — HH (подключение, синхронизация, бесплатные запросы и заблокированные платные операции), пять
 * показателей, слева — вакансии со статистикой, справа — кандидаты карточками или таблицей: сортировка по рейтингу, фильтры (должность,
 * стаж, навыки, город, статус, рейтинг) — в адресе. Выбор до пяти кандидатов одной вакансии — сравнение. Платные вызовы AI — только по кнопке:
 * «Анализировать» у кандидата или «Проанализировать ожидающих» (до 50 за раз, очередью). Раскладка — по ширине области содержимого.
 */

const SCORE_OPTIONS = [
  { value: "", label: "Любой рейтинг" },
  { value: "90", label: "90+ · отличные" },
  { value: "80", label: "80+ · очень хорошие" },
  { value: "70", label: "70+ · подходят" },
  { value: "50", label: "50+ · частично" },
];

const EXPERIENCE_OPTIONS = [
  { value: "", label: "Любой стаж" },
  { value: "1", label: "от 1 года" },
  { value: "3", label: "от 3 лет" },
  { value: "5", label: "от 5 лет" },
  { value: "10", label: "от 10 лет" },
];

const SORT_OPTIONS = [
  { value: "score", label: "По рейтингу" },
  { value: "recent", label: "Сначала новые" },
  { value: "experience", label: "По стажу" },
  { value: "name", label: "По имени" },
];

type Selected = { applicationId: string; vacancyId: string };

export function ScreeningView({ ctx }: { ctx: RecruitmentContext }) {
  const t = useT();
  const params = useSearchParams();
  const [filters, setFilters] = useState<ScreeningFilters>(() => parseFilters(params));
  const [query, setQuery] = useState<ScreeningFilters>(filters);
  const [data, setData] = useState<ScreeningPage | null>(null);
  const [loading, setLoading] = useState(true);
  const [selected, setSelected] = useState<Selected[]>([]);
  const [importing, setImporting] = useState(false);
  const [comparison, setComparison] = useState<Comparison | null>(null);
  const [comparisons, setComparisons] = useState<ComparisonBrief[]>([]);
  const [showFilters, setShowFilters] = useState(false);
  const [rejecting, setRejecting] = useState<ScreeningRow | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const { busy, error, setError, run } = useAction();
  const manage = ctx.canManage;

  // Текстовые поля — с паузой 300 мс; остальное — сразу. Фильтры — в адресе (без перезагрузки страницы).
  useEffect(() => {
    const id = window.setTimeout(() => setQuery(filters), 300);
    window.history.replaceState(null, "", `${window.location.pathname}${filtersQuery(filters)}`);
    return () => window.clearTimeout(id);
  }, [filters]);

  const seq = useRef(0);
  const load = useCallback(async (q: ScreeningFilters) => {
    const n = ++seq.current;
    setLoading(true);
    try {
      const page = await screeningApi.page(apiQuery(q));
      if (n === seq.current) {
        setData(page);
        setError(null);
      }
    } catch (e) {
      if (n === seq.current) setError((e as Error).message);
    } finally {
      if (n === seq.current) setLoading(false);
    }
  }, [setError]);

  useEffect(() => {
    void load(query);
  }, [query, load]);

  // Идёт анализ — обновляем раз в 8 секунд, пока не закончится.
  const analyzing = data?.items.some((r) => r.analyzing) ?? false;
  useEffect(() => {
    if (!analyzing) return;
    const id = window.setInterval(() => void load(query), 8000);
    return () => window.clearInterval(id);
  }, [analyzing, query, load]);

  useEffect(() => {
    if (!filters.vacancyId) {
      setComparisons([]);
      return;
    }
    screeningApi.comparisons(filters.vacancyId).then(setComparisons, () => setComparisons([]));
  }, [filters.vacancyId, comparison]);

  const set = (patch: Partial<ScreeningFilters>) => setFilters((f) => ({ ...f, ...patch }));
  const instant = (patch: Partial<ScreeningFilters>) => {
    const next = { ...filters, ...patch };
    setFilters(next);
    setQuery(next);
  };

  const vacancy = data?.vacancies.find((v) => v.id === filters.vacancyId) ?? null;
  const pendingRows = useMemo(() => data?.items.filter((r) => r.pending) ?? [], [data]);
  const rowsById = useMemo(() => new Map((data?.items ?? []).map((r) => [r.applicationId, r])), [data]);
  const selectedRows = selected.map((s) => rowsById.get(s.applicationId)).filter((r): r is ScreeningRow => !!r);
  const comparable = selectedRows.length >= 2 && selectedRows.every((r) => r.overall != null);

  const analyzeOne = (r: ScreeningRow) =>
    run(`analyze-${r.applicationId}`, async () => {
      await screeningApi.analyze(r.applicationId);
      await load(query);
    });

  const queuePending = () =>
    run("queue", async () => {
      const n = filters.vacancyId ? (data?.kpis.pending ?? 0) : Math.min(pendingRows.length, 50);
      if (!window.confirm(t("Проанализировать ожидающих кандидатов ({n})? Каждый анализ — платный вызов AI; не больше 50 за раз.", { n }))) return;
      const r = filters.vacancyId
        ? await screeningApi.queue({ vacancyId: filters.vacancyId, applicationIds: null })
        : await screeningApi.queue({ vacancyId: null, applicationIds: pendingRows.slice(0, 50).map((x) => x.applicationId) });
      setNotice(r.message);
      await load(query);
    });

  const changeStatus = (r: ScreeningRow, status: ScreeningStatus, reason: string | null = null) =>
    run(`status-${r.applicationId}`, async () => {
      await screeningApi.status(r.applicationId, status, null, reason);
      setRejecting(null);
      await load(query);
    });

  const compare = () =>
    run("compare", async () => {
      setComparison(await screeningApi.compare(selectedRows[0].vacancyId, selectedRows.map((r) => r.applicationId)));
    });

  if (!data) {
    return error ? <ErrorText error={error} /> : <ScreeningSkeleton />;
  }

  const k = data.kpis;
  const filterCount = activeFilters(filters);

  return (
    <div className="@container space-y-4 pb-20">
      <HhStrip hh={data.hh} canManage={manage} />

      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-[13px] text-ink-3">
          {vacancy ? `${vacancy.code} · ${vacancy.title}` : t("Все открытые вакансии")}
          {loading && <Loader2 aria-hidden className="ml-2 inline size-3.5 animate-spin" />}
        </p>
        {manage && (
          <div className="flex flex-wrap gap-2">
            <button type="button" className={button} disabled={!!busy || k.pending === 0} onClick={queuePending} title={t("Платные вызовы AI — только по кнопке")}>
              {busy === "queue" ? <Loader2 className="size-4 animate-spin" /> : <Sparkles className="size-4" />}
              {t("Проанализировать ожидающих ({n})", { n: k.pending })}
            </button>
            <button type="button" className={primary} onClick={() => setImporting(true)}>
              <FileUp className="size-4" />
              {t("Загрузить резюме")}
            </button>
          </div>
        )}
      </div>
      {notice && (
        <p className="flex items-start justify-between gap-2 rounded-lg bg-accent-soft px-3 py-2 text-[13px] text-ink">
          {t(notice)}
          <button type="button" aria-label={t("Закрыть")} onClick={() => setNotice(null)}>
            <X className="size-4 text-ink-3" />
          </button>
        </p>
      )}

      <div className="grid grid-cols-2 gap-3 @min-[700px]:grid-cols-5">
        <Kpi label="Кандидатов" value={k.candidates} tone="accent" active={!filters.status && !filters.minScore} onClick={() => instant({ status: "", minScore: "" })} />
        <Kpi label="Проанализировано" value={k.analyzed} tone="violet" hint={k.candidates > 0 ? t("{p}% откликов", { p: Math.round((k.analyzed / Math.max(1, k.candidates)) * 100) }) : undefined} />
        <Kpi label="Подходящих" value={k.suitable} tone="ok" hint="рейтинг AI от 70" active={filters.minScore === "70"} onClick={() => instant({ minScore: filters.minScore === "70" ? "" : "70", status: "" })} />
        <Kpi label="Рекомендованных" value={k.recommended} tone="violet" hint="отметка HR" active={filters.status === "recommended"}
          onClick={() => instant({ status: filters.status === "recommended" ? "" : "recommended", minScore: "" })} />
        <Kpi label="Ожидают анализа" value={k.pending} tone="warn" hint="есть резюме, нет анализа" />
      </div>

      <div className="grid grid-cols-1 gap-4 @min-[1100px]:grid-cols-[300px_minmax(0,1fr)]">
        <VacancyPanel
          vacancies={data.vacancies}
          selected={filters.vacancyId}
          onSelect={(id) => {
            instant({ vacancyId: id });
            setSelected([]);
          }}
          comparisons={comparisons}
          onComparison={(id) => run("comparison", async () => setComparison(await screeningApi.comparison(id)))}
        />

        <section className="min-w-0 space-y-3">
          <div className="flex flex-wrap items-center gap-2">
            <label className="relative min-w-[200px] flex-1">
              <Search aria-hidden className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-4" />
              <input className={`${field} pl-9`} placeholder={t("Поиск по имени")} value={filters.search} onChange={(e) => set({ search: e.target.value })} />
            </label>
            <select className={`${field} w-auto!`} aria-label={t("Статус")} value={filters.status} onChange={(e) => instant({ status: e.target.value })}>
              <option value="">{t("Все статусы")}</option>
              {STATUSES.map((s) => (
                <option key={s} value={s}>
                  {t(statusLabel[s])}
                </option>
              ))}
            </select>
            <select className={`${field} w-auto!`} aria-label={t("Порядок")} value={filters.sort} onChange={(e) => instant({ sort: e.target.value })}>
              {SORT_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {t(o.label)}
                </option>
              ))}
            </select>
            <button type="button" className={button} aria-expanded={showFilters} onClick={() => setShowFilters(!showFilters)}>
              <ListFilter className="size-4" />
              {t("Фильтры")}
              {filterCount > 0 && <span className="rounded-full bg-accent px-1.5 text-[11px] font-semibold text-white">{filterCount}</span>}
            </button>
            <div role="radiogroup" aria-label={t("Вид")} className="flex rounded-[10px] border border-line bg-surface p-0.5">
              {(["cards", "table"] as const).map((v) => {
                const Icon = v === "cards" ? LayoutGrid : Rows3;
                return (
                  <button
                    key={v}
                    type="button"
                    role="radio"
                    aria-checked={filters.view === v}
                    aria-label={t(v === "cards" ? "Карточки" : "Таблица")}
                    onClick={() => set({ view: v })}
                    className={`grid size-8 place-items-center rounded-[8px] outline-none focus-visible:ring-2 focus-visible:ring-accent ${filters.view === v ? "bg-accent text-white" : "text-ink-3 hover:bg-muted"}`}
                  >
                    <Icon aria-hidden className="size-4" />
                  </button>
                );
              })}
            </div>
          </div>

          {showFilters && (
            <div className="grid gap-2 rounded-[14px] border border-line bg-surface p-3 @min-[700px]:grid-cols-3">
              <input className={field} placeholder={t("Должность")} aria-label={t("Должность")} value={filters.position} onChange={(e) => set({ position: e.target.value })} />
              <input className={field} placeholder={t("Навык, например Excel")} aria-label={t("Навык")} value={filters.skill} onChange={(e) => set({ skill: e.target.value })} />
              <select className={field} aria-label={t("Город")} value={filters.city} onChange={(e) => instant({ city: e.target.value })}>
                <option value="">{t("Любой город")}</option>
                {data.cities.map((c) => (
                  <option key={c} value={c}>
                    {c}
                  </option>
                ))}
              </select>
              <select className={field} aria-label={t("Стаж")} value={filters.minExperience} onChange={(e) => instant({ minExperience: e.target.value })}>
                {EXPERIENCE_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>
                    {t(o.label)}
                  </option>
                ))}
              </select>
              <select className={field} aria-label={t("Рейтинг")} value={filters.minScore} onChange={(e) => instant({ minScore: e.target.value })}>
                {SCORE_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>
                    {t(o.label)}
                  </option>
                ))}
              </select>
              <button type="button" className={button} disabled={filterCount === 0} onClick={() => instant({ ...EMPTY_FILTERS, vacancyId: filters.vacancyId, view: filters.view, sort: filters.sort })}>
                {t("Сбросить фильтры")}
              </button>
            </div>
          )}

          <ErrorText error={error} />

          {data.items.length === 0 ? (
            <Empty filtered={filterCount > 0} manage={manage} onImport={() => setImporting(true)} />
          ) : filters.view === "table" ? (
            <ScreeningTable rows={data.items} showVacancy={!filters.vacancyId} selected={selected} onToggle={(r) => setSelected((s) => toggleCompare(s, r))} manage={manage} busy={busy}
              onAnalyze={analyzeOne} onStatus={(r, s) => (s === "rejected" ? setRejecting(r) : changeStatus(r, s))} />
          ) : (
            <div className="grid grid-cols-1 gap-3 @min-[760px]:grid-cols-2">
              {data.items.map((r) => (
                <CandidateTile key={r.applicationId} row={r} checked={selected.some((s) => s.applicationId === r.applicationId)} onToggle={() => setSelected((s) => toggleCompare(s, r))}
                  manage={manage} busy={busy} onAnalyze={() => analyzeOne(r)} onStatus={(s) => (s === "rejected" ? setRejecting(r) : changeStatus(r, s))} />
              ))}
            </div>
          )}
          {data.total > data.items.length && (
            <p className="text-center text-[12px] text-ink-3">{t("Показаны первые {n} из {total} — уточните фильтры.", { n: data.items.length, total: data.total })}</p>
          )}
        </section>
      </div>

      {selected.length > 0 && (
        <div className="fixed inset-x-0 bottom-4 z-30 mx-auto flex w-[min(640px,calc(100%-32px))] flex-wrap items-center gap-3 rounded-2xl border border-line bg-surface px-4 py-3 shadow-[0_8px_30px_rgba(0,0,0,0.18)]">
          <GitCompareArrows aria-hidden className="size-5 text-accent-strong" />
          <span className="min-w-0 flex-1 text-[13px] text-ink">
            {t("Выбрано {n} из {max}", { n: selected.length, max: MAX_COMPARE })}
            {!comparable && selected.length >= 2 && <span className="block text-[11px] text-warn">{t("Сравнить можно только проанализированных кандидатов одной вакансии.")}</span>}
            {selected.length < 2 && <span className="block text-[11px] text-ink-3">{t("Выберите ещё кандидатов той же вакансии.")}</span>}
          </span>
          <button type="button" className={button} onClick={() => setSelected([])}>
            {t("Снять")}
          </button>
          {manage && (
            <button type="button" className={primary} disabled={!comparable || !!busy} onClick={compare}>
              {busy === "compare" ? <Loader2 className="size-4 animate-spin" /> : <Sparkles className="size-4" />}
              {t("Сравнить (AI)")}
            </button>
          )}
        </div>
      )}

      <ImportSheet open={importing} onClose={() => setImporting(false)} vacancies={data.vacancies} vacancyId={filters.vacancyId} onDone={() => void load(query)} />
      <CompareSheet comparison={comparison} onClose={() => setComparison(null)} />
      <RejectSheet row={rejecting} busy={!!busy} onClose={() => setRejecting(null)} onReject={(reason) => rejecting && changeStatus(rejecting, "rejected", reason)} />
    </div>
  );
}

function VacancyPanel({ vacancies, selected, onSelect, comparisons, onComparison }: {
  vacancies: ScreeningVacancy[];
  selected: string;
  onSelect: (id: string) => void;
  comparisons: ComparisonBrief[];
  onComparison: (id: string) => void;
}) {
  const t = useT();
  return (
    <aside className="min-w-0 space-y-2" aria-label={t("Вакансии")}>
      <p className="px-1 text-[12px] font-semibold uppercase tracking-wide text-ink-4">{t("Вакансии")}</p>
      <div className="no-scrollbar flex gap-2 overflow-x-auto @min-[1100px]:flex-col @min-[1100px]:overflow-visible">
        <button
          type="button"
          onClick={() => onSelect("")}
          aria-pressed={!selected}
          className={`shrink-0 rounded-[14px] border px-3 py-2.5 text-left outline-none focus-visible:ring-2 focus-visible:ring-accent @max-[1099px]:w-[160px] ${!selected ? "border-accent bg-accent-soft" : "border-line bg-surface hover:border-accent/60"}`}
        >
          <span className="block text-[13px] font-semibold text-ink">{t("Все вакансии")}</span>
          <span className="block text-[11px] text-ink-3">{t("откликов: {n}", { n: vacancies.reduce((s, v) => s + v.found, 0) })}</span>
        </button>
        {vacancies.map((v) => {
          const on = v.id === selected;
          return (
            <div key={v.id} className="shrink-0 @max-[1099px]:w-[260px]">
              <button
                type="button"
                onClick={() => onSelect(on ? "" : v.id)}
                aria-pressed={on}
                className={`w-full rounded-[14px] border px-3 py-2.5 text-left outline-none focus-visible:ring-2 focus-visible:ring-accent ${on ? "border-accent bg-accent-soft" : "border-line bg-surface hover:border-accent/60"}`}
              >
                <span className="flex items-start justify-between gap-2">
                  <span className="min-w-0">
                    <span className="block truncate text-[13px] font-semibold text-ink">{v.title}</span>
                    <span className="block text-[11px] text-ink-4">
                      {v.code} · {t(vacancyStatusLabel[v.status])}
                      {v.onHh && " · HH"}
                    </span>
                  </span>
                  {v.averageScore != null && <ScoreRing score={Math.round(v.averageScore)} size={40} />}
                </span>
                {v.requirements && <span className="mt-1 line-clamp-2 block text-[11px] leading-snug text-ink-3">{v.requirements}</span>}
                <span className="mt-1.5 flex flex-wrap gap-x-2.5 gap-y-0.5 text-[11px] text-ink-2">
                  <span>{t("найдено {n}", { n: v.found })}</span>
                  <span className="text-ok">{t("подходящих {n}", { n: v.suitable })}</span>
                  {v.averageScore != null && <span>{t("средний {n}", { n: v.averageScore })}</span>}
                </span>
                <span className="mt-0.5 block text-[11px] text-ink-4">
                  {v.onHh ? (v.lastSyncAt ? t("HH: синхронизация {when}", { when: when(v.lastSyncAt) }) : t("HH: ещё не синхронизирована")) : t("HH: не связана")}
                </span>
              </button>
              {on && comparisons.length > 0 && (
                <ul className="mt-1.5 space-y-1 px-1">
                  {comparisons.slice(0, 5).map((c) => (
                    <li key={c.id}>
                      <button type="button" onClick={() => onComparison(c.id)} className="w-full truncate text-left text-[11px] text-accent-strong hover:underline">
                        <GitCompareArrows aria-hidden className="mr-1 inline size-3" />
                        {day(c.createdAt)}: {c.names.join(", ")}
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          );
        })}
      </div>
    </aside>
  );
}

function StatusSelect({ row, busy, onStatus }: { row: ScreeningRow; busy: string | null; onStatus: (s: ScreeningStatus) => void }) {
  const t = useT();
  return (
    <select
      className={`${field} h-8! w-auto! py-0 text-[12px]`}
      aria-label={t("Статус кандидата")}
      value=""
      disabled={!!busy}
      onChange={(e) => e.target.value && onStatus(e.target.value as ScreeningStatus)}
    >
      <option value="">{t("Статус…")}</option>
      {SETTABLE.filter((s) => s !== row.status).map((s) => (
        <option key={s} value={s}>
          {t(statusLabel[s])}
        </option>
      ))}
    </select>
  );
}

function Flags({ row }: { row: ScreeningRow }) {
  const t = useT();
  return (
    <>
      {!row.hasResume && (
        <span className={`inline-flex items-center gap-1 rounded-[6px] px-1.5 py-0.5 text-[11px] font-medium ${toneClass.warn}`}>
          <FileUp aria-hidden className="size-3" />
          {t("Нет резюме — загрузите файл")}
        </span>
      )}
      {row.pending && !row.analyzing && !row.analysisError && <span className={`rounded-[6px] px-1.5 py-0.5 text-[11px] font-medium ${toneClass.neutral}`}>{t("Ожидает анализа")}</span>}
      {row.analysisError && !row.analyzing && (
        <span className={`inline-flex items-center gap-1 rounded-[6px] px-1.5 py-0.5 text-[11px] font-medium ${toneClass.bad}`} title={row.analysisError}>
          <AlertTriangle aria-hidden className="size-3" />
          {t("Анализ не выполнен")}
        </span>
      )}
      {row.version === 1 && <span className={`rounded-[6px] px-1.5 py-0.5 text-[11px] font-medium ${toneClass.neutral}`}>{t("Прежняя оценка")}</span>}
      {row.hhUrl && (
        <a href={row.hhUrl} target="_blank" rel="noreferrer noopener" className="inline-flex items-center gap-1 text-[11px] font-medium text-accent-strong hover:underline">
          <Globe aria-hidden className="size-3" />
          {t("Профиль на hh.uz")}
        </a>
      )}
    </>
  );
}

function CandidateTile({ row, checked, onToggle, manage, busy, onAnalyze, onStatus }: {
  row: ScreeningRow;
  checked: boolean;
  onToggle: () => void;
  manage: boolean;
  busy: string | null;
  onAnalyze: () => void;
  onStatus: (s: ScreeningStatus) => void;
}) {
  const t = useT();
  const exp = experienceText(row.experienceMonths);
  return (
    <article className={`flex min-w-0 flex-col gap-2.5 rounded-[16px] border bg-surface p-4 ${checked ? "border-accent ring-2 ring-accent/20" : "border-line"}`}>
      <div className="flex items-start gap-3">
        <input type="checkbox" className="mt-1 size-4 shrink-0 accent-[var(--color-accent)]" checked={checked} onChange={onToggle} aria-label={t("Выбрать для сравнения")} />
        <span aria-hidden className="grid size-10 shrink-0 place-items-center rounded-full bg-violet-soft text-[13px] font-bold text-violet">{initials(row.fullName)}</span>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <Link href={`/hr/recruitment/candidates/${row.candidateId}`} className="truncate text-[14px] font-semibold text-ink hover:underline">
              {row.fullName}
            </Link>
            <StatusPill status={row.status} />
          </div>
          <p className="truncate text-[12px] text-ink-2">{[row.desiredPosition, row.city].filter(Boolean).join(" · ") || t("Должность не указана")}</p>
          <p className="truncate text-[12px] text-ink-3">{[exp && t("стаж {x}", { x: exp }), row.lastJob].filter(Boolean).join(" · ") || "—"}</p>
        </div>
        <ScoreRing score={row.overall} />
      </div>
      {row.skills.length > 0 && (
        <div className="flex flex-wrap gap-1">
          {row.skills.slice(0, 6).map((s) => (
            <span key={s} className="rounded-[6px] bg-muted px-1.5 py-0.5 text-[11px] text-ink-2">
              {s}
            </span>
          ))}
          {row.skills.length > 6 && <span className="text-[11px] text-ink-4">+{row.skills.length - 6}</span>}
        </div>
      )}
      {row.summary && <p className="line-clamp-2 text-[12px] leading-relaxed text-ink-2">{row.summary}</p>}
      <div className="flex flex-wrap items-center gap-1.5">
        <Flags row={row} />
      </div>
      <div className="mt-auto flex flex-wrap items-center justify-between gap-2 border-t border-line pt-2.5">
        <span className="text-[11px] text-ink-4">
          {row.vacancyCode} · {t("отклик {d}", { d: day(row.appliedAt) })}
          {row.analyzedAt && ` · ${t("анализ {d}", { d: day(row.analyzedAt) })}`}
        </span>
        {manage && (
          <span className="flex gap-1.5">
            <button type="button" className={`${button} h-8 px-2.5 text-[12px]`} disabled={!!busy || row.analyzing || !row.hasResume} onClick={onAnalyze}
              title={row.hasResume ? t("Платный вызов AI") : t("Сначала загрузите резюме")}>
              {busy === `analyze-${row.applicationId}` || row.analyzing ? <Loader2 className="size-3.5 animate-spin" /> : <Sparkles className="size-3.5" />}
              {row.overall == null ? t("Анализировать") : t("Заново")}
            </button>
            <StatusSelect row={row} busy={busy} onStatus={onStatus} />
          </span>
        )}
      </div>
    </article>
  );
}

function ScreeningTable({ rows, showVacancy, selected, onToggle, manage, busy, onAnalyze, onStatus }: {
  rows: ScreeningRow[];
  showVacancy: boolean;
  selected: Selected[];
  onToggle: (r: ScreeningRow) => void;
  manage: boolean;
  busy: string | null;
  onAnalyze: (r: ScreeningRow) => void;
  onStatus: (r: ScreeningRow, s: ScreeningStatus) => void;
}) {
  const t = useT();
  return (
    <div className="@container overflow-x-auto rounded-[16px] border border-line bg-surface">
      <table className="w-full min-w-[640px] border-collapse text-left text-[12px]">
        <thead>
          <tr className="border-b border-line text-[11px] uppercase tracking-wide text-ink-4">
            <th className="w-10 px-3 py-2.5" />
            <th className="px-2 py-2.5 font-semibold">{t("Кандидат")}</th>
            {showVacancy && <th className="px-2 py-2.5 font-semibold">{t("Вакансия")}</th>}
            <th className="hidden px-2 py-2.5 font-semibold @min-[860px]:table-cell">{t("Город")}</th>
            <th className="px-2 py-2.5 font-semibold">{t("Стаж")}</th>
            <th className="hidden px-2 py-2.5 font-semibold @min-[960px]:table-cell">{t("Навыки")}</th>
            <th className="px-2 py-2.5 font-semibold">{t("Рейтинг")}</th>
            <th className="px-2 py-2.5 font-semibold">{t("Статус")}</th>
            {manage && <th className="px-2 py-2.5" />}
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => {
            const checked = selected.some((s) => s.applicationId === r.applicationId);
            return (
              <tr key={r.applicationId} className={`border-b border-line last:border-0 ${checked ? "bg-accent-soft/50" : "hover:bg-muted/40"}`}>
                <td className="px-3 py-2">
                  <input type="checkbox" className="size-4 accent-[var(--color-accent)]" checked={checked} onChange={() => onToggle(r)} aria-label={t("Выбрать для сравнения")} />
                </td>
                <td className="max-w-[240px] px-2 py-2">
                  <Link href={`/hr/recruitment/candidates/${r.candidateId}`} className="block truncate font-semibold text-ink hover:underline">
                    {r.fullName}
                  </Link>
                  <span className="block truncate text-ink-3">{r.desiredPosition ?? r.lastJob ?? "—"}</span>
                  <span className="mt-0.5 flex flex-wrap gap-1">
                    <Flags row={r} />
                  </span>
                </td>
                {showVacancy && <td className="px-2 py-2 text-ink-2" title={r.vacancyTitle}>{r.vacancyCode}</td>}
                <td className="hidden px-2 py-2 text-ink-2 @min-[860px]:table-cell">{r.city ?? "—"}</td>
                <td className="whitespace-nowrap px-2 py-2 text-ink-2">{experienceText(r.experienceMonths) ?? "—"}</td>
                <td className="hidden max-w-[180px] px-2 py-2 text-ink-2 @min-[960px]:table-cell">
                  <span className="line-clamp-2">{r.skills.slice(0, 4).join(", ") || "—"}</span>
                </td>
                <td className="px-2 py-2">
                  <ScoreRing score={r.overall} size={40} />
                </td>
                <td className="px-2 py-2">
                  <StatusPill status={r.status} />
                </td>
                {manage && (
                  <td className="whitespace-nowrap px-2 py-2">
                    <span className="flex gap-1.5">
                      <button type="button" className={`${button} h-8 px-2 text-[12px]`} disabled={!!busy || r.analyzing || !r.hasResume} onClick={() => onAnalyze(r)}
                        aria-label={t("Анализировать")} title={r.hasResume ? t("Анализировать (платный вызов AI)") : t("Сначала загрузите резюме")}>
                        {busy === `analyze-${r.applicationId}` || r.analyzing ? <Loader2 className="size-3.5 animate-spin" /> : <Sparkles className="size-3.5" />}
                      </button>
                      <StatusSelect row={r} busy={busy} onStatus={(s) => onStatus(r, s)} />
                    </span>
                  </td>
                )}
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function RejectSheet({ row, busy, onClose, onReject }: { row: ScreeningRow | null; busy: boolean; onClose: () => void; onReject: (reason: string) => void }) {
  const t = useT();
  const [reason, setReason] = useState("");
  if (!row) return null;
  return (
    <Sheet open onClose={onClose} title={t("Отказ")} subtitle={row.fullName}>
      <div className="space-y-3 pb-2">
        <label className="block">
          <span className="mb-1 block text-xs font-medium text-ink-2">{t("Причина отказа (обязательно; кандидату не показывается)")}</span>
          <input className={field} autoFocus value={reason} onChange={(e) => setReason(e.target.value)} />
        </label>
        <div className="flex gap-2">
          <button type="button" className={primary} disabled={busy || !reason.trim()} onClick={() => onReject(reason.trim())}>
            {t("Отказать")}
          </button>
          <button type="button" className={button} onClick={onClose}>
            {t("Отмена")}
          </button>
        </div>
      </div>
    </Sheet>
  );
}

function Empty({ filtered, manage, onImport }: { filtered: boolean; manage: boolean; onImport: () => void }) {
  const t = useT();
  return (
    <div className="rounded-[16px] border border-dashed border-line bg-surface px-6 py-10 text-center">
      <p className="text-[14px] font-semibold text-ink">{t(filtered ? "Под фильтры никто не подходит" : "Кандидатов пока нет")}</p>
      <p className="mx-auto mt-1 max-w-md text-[12px] text-ink-3">
        {t(filtered
          ? "Ослабьте фильтры или выберите другую вакансию."
          : "Кандидаты приходят из откликов HH (по связанным вакансиям), Telegram и ручного импорта. Загрузите резюме — AI разберёт его и оценит под вакансию.")}
      </p>
      {manage && !filtered && (
        <button type="button" className={`${primary} mx-auto mt-4`} onClick={onImport}>
          <FileUp className="size-4" />
          {t("Загрузить резюме")}
        </button>
      )}
    </div>
  );
}

export function ScreeningSkeleton() {
  return (
    <div className="space-y-4" aria-busy="true">
      <div className="h-11 animate-pulse rounded-[14px] bg-muted" />
      <div className="grid grid-cols-2 gap-3 md:grid-cols-5">
        {[0, 1, 2, 3, 4].map((i) => (
          <div key={i} className="h-[84px] animate-pulse rounded-[14px] bg-muted" />
        ))}
      </div>
      <div className="grid gap-4 lg:grid-cols-[300px_1fr]">
        <div className="h-64 animate-pulse rounded-[14px] bg-muted" />
        <div className="grid gap-3 md:grid-cols-2">
          {[0, 1, 2, 3].map((i) => (
            <div key={i} className="h-48 animate-pulse rounded-[16px] bg-muted" />
          ))}
        </div>
      </div>
    </div>
  );
}
