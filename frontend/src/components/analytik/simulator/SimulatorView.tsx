"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  Archive,
  ArchiveRestore,
  Calendar,
  CalendarRange,
  ChartNoAxesColumnIncreasing,
  Check,
  ChevronDown,
  ChevronLeft,
  Copy,
  Download,
  Ellipsis,
  FileSpreadsheet,
  FileText,
  FolderOpen,
  GitCompare,
  GitCompareArrows,
  Loader2,
  Pencil,
  Plus,
  RefreshCw,
  RotateCcw,
  Save,
  SlidersHorizontal,
  Timer,
  TriangleAlert,
} from "lucide-react";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { date } from "@/lib/analytik";
import {
  autoName,
  changedCount,
  completeness,
  defaultPeriod,
  forecastRange,
  pct,
  periodDays,
  plural,
  presetLabel,
  quickPeriods,
  type BaselineView,
  type Draft,
  type ScenarioKind,
  type ScenarioParameter,
  type ScenarioView,
  type SimulationResult,
  type SimulatorCatalog,
} from "@/lib/simulator";
import { IssuesList, KindChips, KindTiles, MissingInputs, ParamGroups, TargetedChanges, targetName } from "./SimParameters";
import {
  AiPanel,
  BaselineDetails,
  ConstraintsCard,
  ConstraintsRow,
  DetailResults,
  Headline,
  IncompleteBanner,
  IncompleteDetails,
  KpiGrid,
  MoneyBars,
  RecommendationsCard,
  RisksAndRecsCompact,
  RisksCard,
  ShortCard,
  card,
} from "./SimResults";
import { ScenarioListSheet } from "./SimScenarios";
import { Sheet } from "./SimSheet";

type Meta = Pick<ScenarioView, "own" | "status" | "preset" | "revision" | "history" | "calculatedAt" | "formulaOutdated"> & { aiAt: string | null };

const emptyMeta: Meta = { own: true, status: "active", preset: "custom", revision: 0, history: [], calculatedAt: null, formulaOutdated: false, aiAt: null };

const horizons: { days: number; label: string }[] = [
  { days: 30, label: "30 дн." },
  { days: 90, label: "90 дн." },
  { days: 180, label: "180 дн." },
  { days: 365, label: "Год" },
];
const baseKeys = ["m1", "m3", "m6", "d90"];

/** «12.11, 21:53» — время сохранения в строке сценария. */
const savedAt = (iso: string) => {
  const d = new Date(iso);
  const p = (n: number) => String(n).padStart(2, "0");
  return `${p(d.getDate())}.${p(d.getMonth() + 1)}, ${p(d.getHours())}:${p(d.getMinutes())}`;
};

const desktop = () => typeof window !== "undefined" && window.matchMedia("(min-width: 1024px)").matches;

/**
 * «Аналитик → Симулятор» (What-If). Компьютер: строка сценария сверху, слева — что меняем, параметры по группам и данные, которых нет
 * в OneBase; справа — «Расчёт неполный», вывод, KPI, база и сценарий, ограничения, таблица, риски, рекомендации, AI-анализ.
 * Телефон и планшет: своя шапка, результаты, закреплённая панель «Параметры / Сохранить», параметры — в нижней (на планшете — левой)
 * панели с живой строкой пересчёта.
 * Каждое изменение пересчитывается на сервере через 400 мс после последнего (прежний запрос отменяется); AI — только по кнопке.
 * Сохранённый сценарий открывается со своим результатом и снимком данных — он не меняется, пока его не пересчитают.
 */
export function SimulatorView({ catalog, initial, draft }: { catalog: SimulatorCatalog; initial: ScenarioView | null; draft: Draft | null }) {
  const t = useT();
  const router = useRouter();
  const today = useMemo(() => new Date(), []);
  const [scenarioId, setScenarioId] = useState<string | null>(initial?.id ?? null);
  const [meta, setMeta] = useState<Meta>(initial ? { ...initial, aiAt: initial.aiAt } : emptyMeta);
  const [name, setName] = useState(initial?.name ?? "");
  const [named, setNamed] = useState(!!initial);
  const [editingName, setEditingName] = useState(false);
  const [kind, setKind] = useState<ScenarioKind>(initial?.kind ?? draft?.kind ?? "combined");
  const [horizon, setHorizon] = useState(initial?.horizonDays ?? draft?.horizonDays ?? 30);
  const [parameters, setParameters] = useState<ScenarioParameter[]>(initial?.parameters ?? draft?.parameters ?? []);
  const [regionName, setRegionName] = useState(initial?.newRegionName ?? draft?.newRegionName ?? "");
  const [period, setPeriod] = useState(() =>
    initial ? { from: initial.baseline.from, to: initial.baseline.to } : draft?.from && draft.to ? { from: draft.from, to: draft.to } : defaultPeriod(today),
  );
  const [baseline, setBaseline] = useState<BaselineView | null>(initial?.baseline ?? null);
  const [baselineLoading, setBaselineLoading] = useState(false);
  const [baselineError, setBaselineError] = useState<string | null>(null);
  const [baselineRetry, setBaselineRetry] = useState(0);
  const [result, setResult] = useState<SimulationResult | null>(initial?.result ?? null);
  const [running, setRunning] = useState(false);
  const [runError, setRunError] = useState<string | null>(null);
  const [retry, setRetry] = useState(0);
  const [dirty, setDirty] = useState(!initial);
  const [busy, setBusy] = useState<string | null>(null);
  const [notice, setNotice] = useState<{ tone: "ok" | "bad"; text: string } | null>(null);
  const [listKey, setListKey] = useState(0);
  const [menu, setMenu] = useState<"export" | "more" | "dates" | "data" | null>(null);
  const [sheet, setSheet] = useState<"params" | "list" | "compare" | "incomplete" | "period" | null>(null);
  const [highlight, setHighlight] = useState(false);
  const [ai, setAi] = useState<string | null>(initial?.aiAnalysis ?? null);
  const [aiStale, setAiStale] = useState(false);
  const [aiLoading, setAiLoading] = useState(false);
  const [aiError, setAiError] = useState<string | null>(null);
  const skipRun = useRef(!!initial?.result);
  const periodKey = `${period.from}:${period.to}`;
  const loadedKey = useRef<string | null>(initial ? periodKey : null);
  const archived = meta.status === "archived";
  const changed = changedCount(parameters, catalog.parameters);
  const entered = parameters.length - changed;
  const names = targetName(baseline);
  const shownName = named ? name : autoName(parameters, catalog.parameters, t, names, regionName);

  // Снимок исходных данных за базовый период (сервер кэширует его на 10 минут).
  useEffect(() => {
    if (loadedKey.current === periodKey) return;
    let alive = true;
    setBaselineLoading(true);
    setBaselineError(null);
    bff<BaselineView>("analytik/simulator/baseline", { method: "POST", body: JSON.stringify(period) })
      .then((b) => {
        if (!alive) return;
        loadedKey.current = periodKey;
        setBaseline(b);
      })
      .catch((e: Error) => alive && setBaselineError(e.message))
      .finally(() => alive && setBaselineLoading(false));
    return () => {
      alive = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [periodKey, baselineRetry]);

  // Расчёт: 400 мс после последнего изменения, прежний запрос отменяется.
  useEffect(() => {
    if (!baseline) return;
    if (skipRun.current) {
      skipRun.current = false;
      return;
    }
    const controller = new AbortController();
    const timer = setTimeout(() => {
      setRunning(true);
      bff<SimulationResult>("analytik/simulator/run", {
        method: "POST",
        body: JSON.stringify({ baselineId: baseline.id, kind, horizonDays: horizon, parameters, newRegionName: regionName || null }),
        signal: controller.signal,
      })
        .then((r) => {
          setResult(r);
          setRunError(null);
        })
        .catch((e: Error) => !controller.signal.aborted && setRunError(e.message))
        .finally(() => !controller.signal.aborted && setRunning(false));
    }, 400);
    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [baseline, kind, horizon, parameters, regionName, retry]);

  const edit = () => {
    setDirty(true);
    if (ai) setAiStale(true);
    setNotice(null);
  };

  const apply = (v: ScenarioView) => {
    skipRun.current = true;
    loadedKey.current = `${v.baseline.from}:${v.baseline.to}`;
    setScenarioId(v.id);
    setMeta({ ...v, aiAt: v.aiAt });
    setName(v.name);
    setNamed(true);
    setKind(v.kind);
    setHorizon(v.horizonDays);
    setParameters(v.parameters);
    setRegionName(v.newRegionName ?? "");
    setPeriod({ from: v.baseline.from, to: v.baseline.to });
    setBaseline(v.baseline);
    setResult(v.result);
    setAi(v.aiAnalysis);
    setAiStale(false);
    setDirty(false);
  };

  const act = async (key: string, run: () => Promise<void>) => {
    setBusy(key);
    setNotice(null);
    setMenu(null);
    try {
      await run();
    } catch (e) {
      setNotice({ tone: "bad", text: (e as Error).message });
    } finally {
      setBusy(null);
    }
  };

  const canSave = !!baseline && !!result?.valid && busy == null && !archived && dirty && (changed > 0 || entered > 0 || !!scenarioId);
  const save = () => {
    if (!canSave) return;
    return act("save", async () => {
      const own = scenarioId && meta.own;
      const body = JSON.stringify({ name: shownName, kind, preset: meta.own ? meta.preset : "custom", baselineId: baseline!.id, horizonDays: horizon, parameters, newRegionName: regionName || null });
      const v = await bff<ScenarioView>(own ? `analytik/simulator/scenarios/${scenarioId}` : "analytik/simulator/scenarios", { method: own ? "PUT" : "POST", body });
      apply(v);
      setListKey((k) => k + 1);
      setNotice({ tone: "ok", text: "Сценарий сохранён." });
      if (!own) router.replace(`/analytik/simulator?id=${v.id}`, { scroll: false });
    });
  };
  const saveRef = useRef(save);
  useEffect(() => {
    saveRef.current = save;
  });

  const post = (path: string) => bff<ScenarioView>(`analytik/simulator/scenarios/${scenarioId}/${path}`, { method: "POST" });

  const explain = async () => {
    setAiLoading(true);
    setAiError(null);
    try {
      const r =
        scenarioId && !dirty && meta.own
          ? await bff<{ text: string }>(`analytik/simulator/scenarios/${scenarioId}/ai`, { method: "POST" })
          : await bff<{ text: string }>("analytik/simulator/ai", {
              method: "POST",
              body: JSON.stringify({ baselineId: baseline!.id, kind, horizonDays: horizon, parameters, newRegionName: regionName || null }),
            });
      setAi(r.text);
      setAiStale(false);
      if (scenarioId && !dirty) setMeta((m) => ({ ...m, aiAt: new Date().toISOString() }));
    } catch (e) {
      setAiError((e as Error).message);
    } finally {
      setAiLoading(false);
    }
  };

  // Несохранённые изменения: подтверждение при уходе (перезагрузка, закрытие, переход по ссылке приложения).
  const unsaved = dirty && !archived && (changed > 0 || entered > 0 || !!scenarioId);
  const leaveText = t("Сценарий не сохранён. Уйти без сохранения?");
  useEffect(() => {
    if (!unsaved) return;
    const onUnload = (e: BeforeUnloadEvent) => {
      e.preventDefault();
      e.returnValue = "";
    };
    const onClick = (e: MouseEvent) => {
      if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
      const a = (e.target as HTMLElement | null)?.closest?.("a[href]") as HTMLAnchorElement | null;
      if (!a || a.target === "_blank" || a.hasAttribute("download")) return;
      const url = new URL(a.href, location.href);
      if (url.origin !== location.origin || (url.pathname === location.pathname && url.search === location.search)) return;
      if (!window.confirm(leaveText)) {
        e.preventDefault();
        e.stopPropagation();
      }
    };
    window.addEventListener("beforeunload", onUnload);
    document.addEventListener("click", onClick, true);
    return () => {
      window.removeEventListener("beforeunload", onUnload);
      document.removeEventListener("click", onClick, true);
    };
  }, [unsaved, leaveText]);
  const go = (href: string) => (!unsaved || window.confirm(leaveText)) && router.push(href);

  // Cmd/Ctrl + S — сохранить.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "s") {
        e.preventDefault();
        saveRef.current();
      }
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, []);

  // Меню и поповеры: закрываются щелчком мимо и Esc.
  useEffect(() => {
    if (!menu) return;
    const onDown = (e: MouseEvent) => !(e.target as HTMLElement).closest?.("[data-popover]") && setMenu(null);
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setMenu(null);
    document.addEventListener("mousedown", onDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [menu]);

  const setParams = (p: ScenarioParameter[]) => (setParameters(p), edit());
  const resetChanges = () => setParams(parameters.filter((p) => catalog.parameters.find((d) => d.code === p.code)?.mode === "input"));
  const confirmReset = () => changed > 0 && window.confirm(t("Сбросить все изменения параметров? Введённые исходные данные останутся.")) && resetChanges();

  // «Ввести данные»: компьютер — к карточке и подсветка, телефон — нижняя панель параметров на этой карточке.
  const toInputs = useCallback(() => {
    setSheet(desktop() ? null : "params");
    setTimeout(() => {
      document.getElementById("sim-inputs")?.scrollIntoView({ behavior: "smooth", block: "center" });
      setHighlight(true);
      setTimeout(() => setHighlight(false), 1800);
    }, desktop() ? 0 : 250);
  }, []);
  const toParams = () => (desktop() ? document.getElementById("sim-params")?.scrollIntoView({ behavior: "smooth", block: "start" }) : setSheet("params"));
  const onAction = (a: "inputs" | "params") => (a === "inputs" ? toInputs() : toParams());
  const showResult = () => {
    setSheet(null);
    setTimeout(() => document.getElementById(desktop() || window.innerWidth >= 768 ? "sim-headline" : "sim-headline-m")?.scrollIntoView({ behavior: "smooth", block: "start" }), 150);
  };

  const kindView = catalog.kinds.find((k) => k.code === kind);
  const exportable = !!scenarioId && !dirty && !!result?.valid;
  const errors = result?.issues.filter((i) => i.severity === "error") ?? [];
  const warnings = result?.issues.filter((i) => i.severity === "warning") ?? [];
  const quick = quickPeriods(today).filter((q) => baseKeys.includes(q.key));
  const quickNow = quick.find((q) => q.from === period.from && q.to === period.to);
  const stale = baseline?.sources.filter((x) => x.status !== "ok").length ?? 0;
  const forecast = result ? { from: result.from, to: result.to } : baseline ? forecastRange(baseline.asOf, horizon) : null;
  const info = useMemo(() => completeness(baseline, result, parameters), [baseline, result, parameters]);
  const uncheckedText = info.unchecked ? (info.issues.some((i) => i.code === "recipes") ? "производство и сырьё" : "мощность") : null;
  const revenue = result?.metrics.find((m) => m.code === "sales.revenue");
  const cash = result?.metrics.find((m) => m.code === "finance.net_cash");
  const staleText = t(plural(stale, "{n} источник отстаёт", "{n} источника отстают", "{n} источников отстают"), { n: stale });
  const changedText = t(plural(changed, "{n} параметр изменён", "{n} параметра изменено", "{n} параметров изменено"), { n: changed });
  const periodEdit = (p: { from: string; to: string }) => (setPeriod(p), edit());

  const statusPills = (
    <>
      {archived ? (
        <Pill tone="warn">{t("В архиве")}</Pill>
      ) : dirty || !scenarioId ? (
        <Pill tone="warn">
          <span className="size-1.5 rounded-full bg-warn" aria-hidden />
          {scenarioId ? t("Не сохранено") : t("Не сохранён")}
        </Pill>
      ) : (
        <Pill tone="ok">
          <Check className="size-3" aria-hidden />
          {t("Сохранён")} {meta.calculatedAt ? savedAt(meta.calculatedAt) : ""}
        </Pill>
      )}
      {changed > 0 ? (
        <Pill tone="accent">
          <SlidersHorizontal className="size-3" aria-hidden />
          {changedText}
        </Pill>
      ) : (
        <Pill tone="muted">{t("Базовый сценарий")}</Pill>
      )}
      {scenarioId && meta.preset !== "custom" && <Pill tone="muted">{t(presetLabel[meta.preset] ?? meta.preset)}</Pill>}
      {!meta.own && <Pill tone="violet">{t("Чужой — сохранение создаст ваш")}</Pill>}
    </>
  );

  const nameField = (size: "lg" | "md") =>
    editingName ? (
      <input
        autoFocus
        defaultValue={shownName}
        maxLength={200}
        aria-label={t("Название сценария")}
        onKeyDown={(e) => {
          if (e.key === "Enter") {
            const v = (e.target as HTMLInputElement).value.trim();
            if (v) (setName(v), setNamed(true), edit());
            setEditingName(false);
          }
          if (e.key === "Escape") setEditingName(false);
        }}
        onBlur={(e) => {
          const v = e.target.value.trim();
          if (v && v !== shownName) (setName(v), setNamed(true), edit());
          setEditingName(false);
        }}
        className={`min-w-0 flex-1 rounded-lg border border-accent bg-page px-2 font-bold text-ink outline-none ${size === "lg" ? "h-9 text-[18px]" : "h-8 text-[15px]"}`}
      />
    ) : (
      <button type="button" disabled={archived} onClick={() => setEditingName(true)} title={t("Переименовать")} className="group flex min-w-0 items-center gap-2 text-left disabled:cursor-default">
        <span className={`min-w-0 font-bold text-ink ${size === "lg" ? "truncate text-[18px]" : "line-clamp-2 text-[15px]"}`}>{shownName}</span>
        {!archived && <Pencil className="size-3.5 shrink-0 text-ink-4 group-hover:text-ink-2" aria-hidden />}
      </button>
    );

  const runIndicator = (running || baselineLoading) && (
    <span className="inline-flex items-center gap-1.5 text-[12px] font-medium text-accent-strong" aria-live="polite">
      <Loader2 className="size-3.5 animate-spin" aria-hidden />
      {baselineLoading ? t("Загружаю данные…") : t("Пересчёт…")}
    </span>
  );

  const scenarioActions = scenarioId
    ? [
        { key: "new", icon: Plus, label: "Новый сценарий", run: () => go("/analytik/simulator") },
        { key: "copy", icon: Copy, label: "Копировать", run: () => act("copy", async () => router.push(`/analytik/simulator?id=${(await post("copy")).id}`)) },
        ...(meta.own && !archived
          ? [{ key: "recalc", icon: RefreshCw, label: "Пересчитать на актуальных данных", hint: "Новый снимок данных; прежний расчёт — в истории", run: () => act("recalc", async () => (apply(await post("recalculate")), setNotice({ tone: "ok", text: "Пересчитано на актуальных данных." }))) }]
          : []),
        {
          key: "set",
          icon: GitCompareArrows,
          label: "Базовый, оптимистичный и пессимистичный",
          hint: "Спрос — по разбросу прогноза продаж",
          run: () =>
            act("set", async () => {
              const set = await bff<ScenarioView[]>(`analytik/simulator/scenarios/${scenarioId}/set`, { method: "POST" });
              router.push(`/analytik/simulator/compare?ids=${[scenarioId, ...set.map((s) => s.id)].join(",")}`);
            }),
        },
        ...(meta.own
          ? archived
            ? [{ key: "restore", icon: ArchiveRestore, label: "Вернуть из архива", run: () => act("restore", async () => apply(await post("restore"))) }]
            : [{ key: "archive", icon: Archive, label: "В архив", run: () => act("archive", async () => (apply(await post("archive")), setListKey((k) => k + 1))) }]
          : []),
      ]
    : [];

  return (
    <div className="pb-[calc(110px+env(safe-area-inset-bottom))] lg:pb-0">
      {/* Телефон и планшет: своя шапка вместо общей */}
      <header className="-mx-4 -mt-3.5 mb-1 flex min-h-14 items-center gap-2.5 px-3 pt-[env(safe-area-inset-top)] sm:-mx-6 md:-mt-[18px] lg:hidden">
        <Link href="/analytik" aria-label={t("Назад")} className="grid size-10 shrink-0 place-items-center rounded-full bg-surface text-ink">
          <ChevronLeft className="size-5" aria-hidden />
        </Link>
        <div className="min-w-0 flex-1 text-center">
          <h1 className="truncate text-[15px] font-bold text-ink">{t("Симулятор сценариев")}</h1>
          <p className="text-[11px] font-medium text-ink-3">{t("Аналитик")} · What-If</p>
        </div>
        <button type="button" onClick={() => setSheet("list")} aria-label={t("Мои сценарии")} className="grid size-10 shrink-0 place-items-center rounded-full bg-surface text-ink">
          <FolderOpen className="size-[18px]" aria-hidden />
        </button>
      </header>

      {/* Строка сценария — компьютер и планшет */}
      <section className={`@container/bar px-5 py-4 max-md:hidden ${card}`}>
        <div className="flex flex-wrap items-center gap-x-3 gap-y-2.5">
          <div className="flex min-w-0 max-w-full flex-1 basis-[320px] flex-wrap items-center gap-2">
            <div className="flex min-w-0 max-w-full items-center">{nameField("lg")}</div>
            {statusPills}
            {runIndicator}
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <button type="button" onClick={confirmReset} disabled={changed === 0 || archived} className="inline-flex h-9 items-center gap-1.5 rounded-[10px] px-2.5 text-[13px] text-ink-3 hover:text-ink disabled:opacity-40">
              <RotateCcw className="size-4" aria-hidden />
              {t("Сбросить")}
            </button>
            <BarButton icon={FolderOpen} label="Мои сценарии" onClick={() => setSheet("list")} />
            <BarButton icon={GitCompare} label="Сравнить" onClick={() => setSheet("compare")} />
            <div className="relative" data-popover>
              <BarButton icon={Download} label="Экспорт" chevron onClick={() => setMenu(menu === "export" ? null : "export")} expanded={menu === "export"} />
              {menu === "export" && (
                <div role="menu" className="absolute right-0 z-30 mt-1.5 w-64 rounded-xl border border-line bg-surface-solid p-1.5 shadow-lg">
                  {exportable ? (
                    <>
                      <a role="menuitem" href={`/bff/api/analytik/simulator/scenarios/${scenarioId}/export/xlsx`} download className="flex items-center gap-2 rounded-lg px-2.5 py-2 text-[13px] text-ink hover:bg-muted">
                        <FileSpreadsheet className="size-4 text-ok" aria-hidden /> Excel
                      </a>
                      <a role="menuitem" href={`/bff/api/analytik/simulator/scenarios/${scenarioId}/export/pdf`} download className="flex items-center gap-2 rounded-lg px-2.5 py-2 text-[13px] text-ink hover:bg-muted">
                        <FileText className="size-4 text-bad" aria-hidden /> PDF
                      </a>
                    </>
                  ) : (
                    <p className="px-2.5 py-2 text-[12px] text-ink-3">{t("Сохраните сценарий — экспортируется сохранённая версия.")}</p>
                  )}
                </div>
              )}
            </div>
            {scenarioActions.length > 0 && (
              <div className="relative" data-popover>
                <button
                  type="button"
                  onClick={() => setMenu(menu === "more" ? null : "more")}
                  aria-expanded={menu === "more"}
                  aria-label={t("Ещё")}
                  className="grid size-9 place-items-center rounded-[10px] border border-line-strong bg-muted text-ink-2 hover:text-ink"
                >
                  {busy && busy !== "save" ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Ellipsis className="size-4" aria-hidden />}
                </button>
                {menu === "more" && (
                  <div role="menu" className="absolute right-0 z-30 mt-1.5 w-72 rounded-xl border border-line bg-surface-solid p-1.5 shadow-lg">
                    {scenarioActions.map((a) => (
                      <MenuItem key={a.key} icon={a.icon} label={a.label} hint={"hint" in a ? a.hint : undefined} onClick={() => (setMenu(null), a.run())} />
                    ))}
                  </div>
                )}
              </div>
            )}
            <button
              type="button"
              onClick={save}
              disabled={!canSave}
              title={`${t("Сохранить")} (⌘S)`}
              className="inline-flex h-9 items-center gap-1.5 rounded-[10px] bg-accent px-4 text-[13px] font-semibold text-white disabled:opacity-50"
            >
              {busy === "save" ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Save className="size-4" aria-hidden />}
              {t("Сохранить")}
            </button>
          </div>
        </div>
        <div className="mt-3.5 flex flex-wrap items-end gap-x-6 gap-y-3 border-t border-line pt-3.5">
          <Field label="Базовый период (факт)">
            <Segments items={quick.map((q) => ({ key: q.key, label: q.label }))} value={quickNow?.key ?? null} disabled={archived} onChange={(k) => periodEdit(quick.find((q) => q.key === k)!)} tone="accent" />
          </Field>
          <Field label="Даты базы">
            <div className="relative" data-popover>
              <button
                type="button"
                disabled={archived}
                onClick={() => setMenu(menu === "dates" ? null : "dates")}
                aria-expanded={menu === "dates"}
                className="inline-flex h-9 items-center gap-2 rounded-[10px] border border-line bg-page px-3 text-[12px] font-semibold text-ink hover:border-line-strong disabled:opacity-60"
              >
                <Calendar className="size-3.5 text-ink-3" aria-hidden />
                {date(period.from)} — {date(period.to)}
                <span className="font-normal text-ink-4">
                  {periodDays(period.from, period.to)} {t("дн.")}
                </span>
              </button>
              {menu === "dates" && <DatesPopover period={period} onApply={(p) => (periodEdit(p), setMenu(null))} />}
            </div>
          </Field>
          <Field label="Горизонт сценария">
            <Segments
              items={[...horizons.map((h) => ({ key: String(h.days), label: h.label })), ...(horizons.some((h) => h.days === horizon) ? [] : [{ key: String(horizon), label: `${horizon} ${t("дн.")}` }])]}
              value={String(horizon)}
              disabled={archived}
              onChange={(k) => (setHorizon(Number(k)), edit())}
              tone="outline"
            />
          </Field>
          <Field label="Период прогноза">
            <span className="flex h-9 items-center text-[13px] font-semibold text-ink">{forecast ? `${date(forecast.from).slice(0, 5)} — ${date(forecast.to)}` : "—"}</span>
          </Field>
          <div className="@min-[1180px]/bar:ml-auto">
            <Field label="Данные на">
              <div className="relative flex h-9 items-center gap-2" data-popover>
                <span className="text-[13px] font-semibold text-ink">{baseline ? date(baseline.asOf) : "—"}</span>
                {baseline && (
                  <button
                    type="button"
                    onClick={() => setMenu(menu === "data" ? null : "data")}
                    aria-expanded={menu === "data"}
                    className={`inline-flex h-6 items-center gap-1 rounded-md px-2 text-[11px] font-semibold ${stale > 0 ? "bg-warn-soft text-warn" : "bg-ok-soft text-ok"}`}
                  >
                    <Timer className="size-3" aria-hidden />
                    {stale > 0 ? staleText : t("источники свежие")}
                  </button>
                )}
                {menu === "data" && baseline && (
                  <div className="absolute left-0 top-full z-30 mt-1.5 w-[380px] max-w-[calc(100vw-2rem)] rounded-xl border border-line bg-surface-solid p-4 shadow-lg @min-[1180px]/bar:left-auto @min-[1180px]/bar:right-0">
                    <BaselineDetails baseline={baseline} />
                  </div>
                )}
              </div>
            </Field>
          </div>
        </div>
        {notice && <p className={`mt-3 rounded-lg px-3 py-2 text-[13px] ${notice.tone === "ok" ? "bg-ok-soft text-ok" : "bg-bad-soft text-bad"}`}>{t(notice.text)}</p>}
        {meta.formulaOutdated && !dirty && (
          <p className="mt-3 rounded-lg bg-warn-soft px-3 py-2 text-[13px] text-warn">{t("Формулы симулятора обновились после расчёта — показан сохранённый результат. Пересчитайте сценарий, чтобы увидеть его по новым формулам.")}</p>
        )}
      </section>

      {/* Карточка сценария — телефон */}
      <section className={`p-4 md:hidden ${card}`}>
        <div className="flex items-start gap-2">{nameField("md")}</div>
        <div className="mt-2 flex flex-wrap items-center gap-1.5">
          {statusPills}
          {runIndicator}
        </div>
        <button type="button" onClick={() => setSheet("period")} className="mt-3 flex w-full items-center gap-2.5 border-t border-line pt-3 text-left">
          <CalendarRange className="size-4 shrink-0 text-ink-3" aria-hidden />
          <span className="min-w-0 flex-1">
            <span className="block truncate text-[12px] font-medium text-ink">
              {t("База")}: {quickNow ? t(quickNow.label) : t("свой период")} · {date(period.from).slice(0, 5)} — {date(period.to).slice(0, 5)}
            </span>
            <span className="block truncate text-[11px] text-ink-4">
              {t("Прогноз")}: {horizon} {t("дн.")}
              {forecast ? ` · ${date(forecast.from).slice(0, 5)} — ${date(forecast.to)}` : ""}
            </span>
          </span>
          <span className="shrink-0 text-[13px] font-semibold text-accent-strong">{t("Изменить")}</span>
        </button>
        {notice && <p className={`mt-3 rounded-lg px-3 py-2 text-[13px] ${notice.tone === "ok" ? "bg-ok-soft text-ok" : "bg-bad-soft text-bad"}`}>{t(notice.text)}</p>}
        {meta.formulaOutdated && !dirty && <p className="mt-3 rounded-lg bg-warn-soft px-3 py-2 text-[12px] text-warn">{t("Формулы симулятора обновились после расчёта — показан сохранённый результат. Пересчитайте сценарий, чтобы увидеть его по новым формулам.")}</p>}
      </section>

      <div className="mt-4 grid gap-4 max-md:mt-3 max-md:gap-3 lg:grid-cols-[320px_minmax(0,1fr)] xl:grid-cols-[340px_minmax(0,1fr)] min-[1440px]:grid-cols-[360px_minmax(0,1fr)]">
        {/* Слева (компьютер): что меняем, параметры, данные, которых нет в OneBase */}
        <aside className="hidden space-y-4 lg:sticky lg:top-4 lg:block lg:max-h-[calc(100dvh-6rem)] lg:self-start lg:overflow-y-auto lg:overscroll-contain lg:pr-1 [scrollbar-width:thin]">
          <section className={`p-[18px] ${card}`}>
            <h2 className="mb-3 text-[15px] font-bold text-ink">{t("Что меняем")}</h2>
            <KindTiles kinds={catalog.kinds} value={kind} disabled={archived} onChange={(k) => (setKind(k), edit())} />
          </section>
          <section id="sim-params" className={`scroll-mt-4 px-[18px] pb-[18px] pt-4 ${card}`}>
            <div className="mb-1 flex items-center gap-2">
              <h2 className="text-[15px] font-bold text-ink">{t("Параметры")}</h2>
              {changed > 0 && <span className="rounded-md bg-accent-soft px-1.5 py-0.5 text-[11px] font-bold text-accent-strong">{t("{n} изменено", { n: changed })}</span>}
              <button type="button" onClick={resetChanges} disabled={changed === 0 || archived} className="ml-auto inline-flex items-center gap-1 text-[12px] text-ink-3 hover:text-ink disabled:opacity-40">
                <RotateCcw className="size-3" aria-hidden />
                {t("Сбросить")}
              </button>
            </div>
            <ParamGroups key={kind} catalog={catalog.parameters} kind={kind} parameters={parameters} onChange={setParams} baseline={baseline} regionName={regionName} onRegionName={(v) => (setRegionName(v), edit())} disabled={archived} />
            <TargetedChanges key={`t-${kind}`} catalog={catalog.parameters} kind={kind} parameters={parameters} onChange={setParams} baseline={baseline} disabled={archived} />
            {warnings.length > 0 && (
              <div className="mt-3">
                <IssuesList issues={warnings} />
              </div>
            )}
          </section>
          <MissingInputs catalog={catalog.parameters} parameters={parameters} onChange={setParams} baseline={baseline} disabled={archived} highlight={highlight} />
        </aside>

        {/* Справа: результаты */}
        <div className="@container/res min-w-0 space-y-4 max-md:space-y-3">
          {baselineError && <ErrorBanner title="Не удалось загрузить исходные данные" text={baselineError} onRetry={() => setBaselineRetry((n) => n + 1)} />}
          {runError && <ErrorBanner title="Не удалось пересчитать" text={runError} onRetry={() => setRetry((n) => n + 1)} />}
          {!result && !runError && !baselineError && <LoadingResults label={baselineLoading ? "Собираю исходные данные OneBase…" : "Считаю сценарий…"} />}
          {result && !result.valid && errors.length > 0 && (
            <section className={`p-5 ${card}`}>
              <h3 className="mb-2 flex items-center gap-2 text-[15px] font-bold text-ink">
                <TriangleAlert className="size-4 text-bad" aria-hidden />
                {t("Сценарий не посчитан")}
              </h3>
              <IssuesList issues={errors} />
            </section>
          )}
          {result?.valid && (
            <div className={`space-y-4 transition-opacity max-md:space-y-3 ${running ? "opacity-60" : ""}`}>
              <IncompleteBanner info={info} onInputs={toInputs} onOpen={() => setSheet("incomplete")} />
              <Headline result={result} baseline={baseline} changed={changed} running={running} />
              <KpiGrid result={result} kind={kindView} />
              <ConstraintsRow result={result} unchecked={uncheckedText} />
              <div className="grid gap-4 max-md:hidden @min-[920px]/res:grid-cols-[minmax(0,1fr)_420px]">
                <MoneyBars result={result} />
                <div className="grid content-start gap-4 @min-[640px]/res:grid-cols-2 @min-[920px]/res:grid-cols-1">
                  <ConstraintsCard result={result} unchecked={uncheckedText} />
                  <ShortCard result={result} />
                </div>
              </div>
              <DetailResults result={result} nodes={catalog.nodes} baseline={baseline} history={meta.history} />
              <div className="grid gap-4 max-md:hidden @min-[640px]/res:grid-cols-2">
                <RisksCard result={result} />
                <RecommendationsCard result={result} onAction={onAction} />
              </div>
              <RisksAndRecsCompact result={result} onAction={onAction} />
            </div>
          )}
          <AiPanel text={ai} loading={aiLoading} error={aiError} stale={aiStale} savedAt={meta.aiAt} disabled={!result?.valid || running} onRun={explain} />
        </div>
      </div>

      {/* Телефон и планшет: закреплённая панель действий вместо нижнего меню */}
      <div className="pointer-events-none fixed inset-x-0 bottom-0 z-30 lg:hidden">
        <div className="h-[140px] bg-gradient-to-t from-page via-page/85 to-transparent" aria-hidden />
        <div className="pointer-events-auto absolute inset-x-3 bottom-[calc(12px+env(safe-area-inset-bottom))] flex gap-2 rounded-[20px] border border-line bg-surface/95 p-2 shadow-[0_8px_30px_rgb(0_0_0/0.35)] backdrop-blur-xl md:mx-auto md:max-w-xl">
          <button type="button" onClick={() => setSheet("params")} className="inline-flex h-12 flex-1 items-center justify-center gap-2 rounded-2xl border border-line bg-muted text-[15px] font-semibold text-ink">
            <SlidersHorizontal className="size-[18px]" aria-hidden />
            {t("Параметры")}
            {changed > 0 && <span className="grid min-w-5 place-items-center rounded-full bg-accent px-1.5 text-[11px] font-bold text-white">{changed}</span>}
          </button>
          <button type="button" onClick={save} disabled={!canSave} className="inline-flex h-12 flex-1 items-center justify-center gap-2 rounded-2xl bg-accent text-[15px] font-semibold text-white disabled:opacity-50">
            {busy === "save" ? <Loader2 className="size-[18px] animate-spin" aria-hidden /> : <Save className="size-[18px]" aria-hidden />}
            {t("Сохранить")}
          </button>
        </div>
      </div>

      {/* Параметры — нижняя панель (телефон) или левая (планшет) */}
      <Sheet
        open={sheet === "params"}
        onClose={() => setSheet(null)}
        label="Параметры"
        side="left"
        header={
          <div className="flex items-center gap-2">
            <h2 className="text-[17px] font-bold text-ink">{t("Параметры")}</h2>
            {changed > 0 && <span className="rounded-md bg-accent-soft px-1.5 py-0.5 text-[11px] font-bold text-accent-strong">{t("{n} изменено", { n: changed })}</span>}
            <button type="button" onClick={resetChanges} disabled={changed === 0 || archived} className="ml-auto inline-flex items-center gap-1 text-[13px] text-ink-3 disabled:opacity-40">
              <RotateCcw className="size-3.5" aria-hidden />
              {t("Сбросить")}
            </button>
          </div>
        }
        footer={
          <div className="space-y-2.5">
            <p className="flex items-center gap-2 text-[13px] text-ink-3" aria-live="polite">
              {running ? (
                <>
                  <Loader2 className="size-3.5 animate-spin text-accent-strong" aria-hidden />
                  {t("Пересчёт…")}
                </>
              ) : (
                <>
                  <span className="size-2 rounded-full bg-ok" aria-hidden />
                  {revenue?.scenario != null && changed > 0 ? (
                    <span>
                      {t("Пересчитано")}: {t("выручка")} <strong className={(revenue.delta ?? 0) >= 0 ? "text-ok" : "text-bad"}>{pct(revenue.deltaPercent)}</strong>
                      {cash?.deltaPercent != null && (
                        <>
                          {" · "}
                          {t("деньги")} <strong className={(cash.delta ?? 0) >= 0 ? "text-ok" : "text-bad"}>{pct(cash.deltaPercent)}</strong>
                        </>
                      )}
                    </span>
                  ) : (
                    <span>{changed > 0 ? t("Пересчитано") : t("Базовый сценарий — измените параметры")}</span>
                  )}
                </>
              )}
            </p>
            <button type="button" onClick={showResult} className="inline-flex h-[50px] w-full items-center justify-center gap-2 rounded-2xl bg-accent text-[15px] font-semibold text-white">
              <ChartNoAxesColumnIncreasing className="size-[18px]" aria-hidden />
              {t("Показать результат")}
            </button>
          </div>
        }
      >
        <div className="pb-2 pt-3">
          <KindChips kinds={catalog.kinds} value={kind} disabled={archived} onChange={(k) => (setKind(k), edit())} />
        </div>
        <ParamGroups key={`m-${kind}`} catalog={catalog.parameters} kind={kind} parameters={parameters} onChange={setParams} baseline={baseline} regionName={regionName} onRegionName={(v) => (setRegionName(v), edit())} disabled={archived} touch />
        <TargetedChanges key={`mt-${kind}`} catalog={catalog.parameters} kind={kind} parameters={parameters} onChange={setParams} baseline={baseline} disabled={archived} touch />
        <MissingInputs catalog={catalog.parameters} parameters={parameters} onChange={setParams} baseline={baseline} disabled={archived} flat highlight={highlight} />
        {warnings.length > 0 && (
          <div className="pb-4">
            <IssuesList issues={warnings} />
          </div>
        )}
      </Sheet>

      {/* Период и горизонт — телефон */}
      <Sheet open={sheet === "period"} onClose={() => setSheet(null)} label="Период и горизонт" side="right" header={<h2 className="text-[17px] font-bold text-ink">{t("Период и горизонт")}</h2>}>
        <div className="space-y-5 py-4">
          <Field label="Базовый период (факт)">
            <Segments items={quick.map((q) => ({ key: q.key, label: q.label }))} value={quickNow?.key ?? null} disabled={archived} onChange={(k) => periodEdit(quick.find((q) => q.key === k)!)} tone="accent" wrap />
          </Field>
          <Field label="Даты базы">
            <DatesInline period={period} disabled={archived} onApply={periodEdit} />
          </Field>
          <Field label="Горизонт сценария">
            <Segments
              items={[...horizons.map((h) => ({ key: String(h.days), label: h.label })), ...(horizons.some((h) => h.days === horizon) ? [] : [{ key: String(horizon), label: `${horizon} ${t("дн.")}` }])]}
              value={String(horizon)}
              disabled={archived}
              onChange={(k) => (setHorizon(Number(k)), edit())}
              tone="outline"
              wrap
            />
          </Field>
          {baseline && (
            <Field label="Данные на">
              <BaselineDetails baseline={baseline} />
            </Field>
          )}
        </div>
      </Sheet>

      <Sheet open={sheet === "incomplete"} onClose={() => setSheet(null)} label="Расчёт неполный" side="right" header={<h2 className="text-[17px] font-bold text-ink">{t("Расчёт неполный")}</h2>}>
        <IncompleteDetails info={info} onInputs={toInputs} />
      </Sheet>

      <ScenarioListSheet
        open={sheet === "list" || sheet === "compare"}
        onClose={() => setSheet(null)}
        currentId={scenarioId}
        maxCompare={catalog.maxCompare}
        refreshKey={listKey}
        compare={sheet === "compare"}
        actions={
          scenarioActions.length > 0 ? (
            <div className="border-b border-line py-3 md:hidden">
              <div className="mb-2 text-[10px] font-bold uppercase tracking-wide text-ink-4">{t("Этот сценарий")}</div>
              <div className="grid grid-cols-2 gap-2">
                {exportable && (
                  <>
                    <a href={`/bff/api/analytik/simulator/scenarios/${scenarioId}/export/xlsx`} download className="inline-flex h-10 items-center justify-center gap-1.5 rounded-xl border border-line bg-muted text-[13px] text-ink">
                      <FileSpreadsheet className="size-4 text-ok" aria-hidden /> Excel
                    </a>
                    <a href={`/bff/api/analytik/simulator/scenarios/${scenarioId}/export/pdf`} download className="inline-flex h-10 items-center justify-center gap-1.5 rounded-xl border border-line bg-muted text-[13px] text-ink">
                      <FileText className="size-4 text-bad" aria-hidden /> PDF
                    </a>
                  </>
                )}
                {scenarioActions.map((a) => {
                  const Icon = a.icon;
                  return (
                    <button key={a.key} type="button" onClick={() => (setSheet(null), a.run())} className="inline-flex min-h-10 items-center gap-1.5 rounded-xl border border-line bg-muted px-3 py-2 text-left text-[12px] text-ink">
                      <Icon className="size-4 shrink-0 text-ink-3" aria-hidden />
                      {t(a.label)}
                    </button>
                  );
                })}
              </div>
            </div>
          ) : null
        }
      />
    </div>
  );
}

function Pill({ tone, children }: { tone: "warn" | "ok" | "accent" | "muted" | "violet"; children: React.ReactNode }) {
  const cls = { warn: "bg-warn-soft text-warn", ok: "bg-ok-soft text-ok", accent: "bg-accent-soft text-accent-strong", muted: "bg-muted text-ink-4", violet: "bg-violet-soft text-violet" }[tone];
  return <span className={`inline-flex h-6 shrink-0 items-center gap-1.5 rounded-lg px-2 text-[11px] font-semibold ${cls}`}>{children}</span>;
}

function BarButton({ icon: Icon, label, onClick, chevron, expanded }: { icon: React.ComponentType<{ className?: string }>; label: string; onClick: () => void; chevron?: boolean; expanded?: boolean }) {
  const t = useT();
  return (
    <button type="button" onClick={onClick} aria-expanded={expanded} className="inline-flex h-9 items-center gap-1.5 rounded-[10px] border border-line-strong bg-muted px-3 text-[13px] font-medium text-ink hover:border-accent">
      <Icon className="size-4 text-ink-2" />
      {t(label)}
      {chevron && <ChevronDown className="size-3.5 text-ink-3" />}
    </button>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  const t = useT();
  return (
    <div className="min-w-0">
      <div className="mb-1.5 text-[11px] font-semibold uppercase tracking-wide text-ink-4">{t(label)}</div>
      {children}
    </div>
  );
}

/** Сегментный выбор: базовый период (активный — акцент) и горизонт (активный — с обводкой). */
function Segments({ items, value, onChange, tone, disabled, wrap }: { items: { key: string; label: string }[]; value: string | null; onChange: (k: string) => void; tone: "accent" | "outline"; disabled?: boolean; wrap?: boolean }) {
  const t = useT();
  return (
    <div className={`inline-flex gap-1 rounded-[10px] border border-line bg-page p-1 ${wrap ? "flex-wrap" : ""}`} role="radiogroup">
      {items.map((x) => {
        const on = x.key === value;
        const cls = on ? (tone === "accent" ? "bg-accent font-semibold text-white" : "border border-line-strong bg-muted font-semibold text-ink") : "text-ink-3 hover:text-ink";
        return (
          <button key={x.key} type="button" role="radio" aria-checked={on} disabled={disabled} onClick={() => onChange(x.key)} className={`h-7 whitespace-nowrap rounded-md px-2.5 text-[12px] disabled:opacity-60 ${cls}`}>
            {t(x.label)}
          </button>
        );
      })}
    </div>
  );
}

function DatesInline({ period, onApply, disabled }: { period: { from: string; to: string }; onApply: (p: { from: string; to: string }) => void; disabled?: boolean }) {
  const t = useT();
  const [from, setFrom] = useState(period.from);
  const [to, setTo] = useState(period.to);
  const ok = from && to && from <= to && (from !== period.from || to !== period.to);
  return (
    <div className="flex flex-wrap items-center gap-2">
      <input type="date" value={from} max={to} disabled={disabled} onChange={(e) => setFrom(e.target.value)} aria-label={t("С")} className="h-9 rounded-lg border border-line bg-page px-2 text-[13px] text-ink" />
      <span className="text-ink-4">—</span>
      <input type="date" value={to} min={from} disabled={disabled} onChange={(e) => setTo(e.target.value)} aria-label={t("По")} className="h-9 rounded-lg border border-line bg-page px-2 text-[13px] text-ink" />
      <button type="button" disabled={!ok || disabled} onClick={() => onApply({ from, to })} className="h-9 rounded-lg bg-accent px-3 text-[13px] font-semibold text-white disabled:opacity-40">
        {t("Применить")}
      </button>
    </div>
  );
}

function DatesPopover({ period, onApply }: { period: { from: string; to: string }; onApply: (p: { from: string; to: string }) => void }) {
  const t = useT();
  return (
    <div className="absolute left-0 top-full z-30 mt-1.5 w-max max-w-[calc(100vw-2rem)] rounded-xl border border-line bg-surface-solid p-3 shadow-lg">
      <p className="mb-2 text-[11px] text-ink-4">{t("Свои даты базового периода (факт)")}</p>
      <DatesInline period={period} onApply={onApply} />
    </div>
  );
}

function MenuItem({ icon: Icon, label, hint, onClick }: { icon: React.ComponentType<{ className?: string }>; label: string; hint?: string; onClick: () => void }) {
  const t = useT();
  return (
    <button type="button" role="menuitem" onClick={onClick} className="flex w-full items-start gap-2 rounded-lg px-2.5 py-2 text-left text-[13px] text-ink hover:bg-muted">
      <Icon className="mt-0.5 size-4 shrink-0 text-ink-3" />
      <span>
        {t(label)}
        {hint && <span className="block text-[11px] text-ink-4">{t(hint)}</span>}
      </span>
    </button>
  );
}

function ErrorBanner({ title, text, onRetry }: { title: string; text: string; onRetry: () => void }) {
  const t = useT();
  return (
    <div className="flex flex-wrap items-center gap-3 rounded-2xl border border-bad/25 bg-bad-soft px-4 py-3" role="alert">
      <TriangleAlert className="size-5 shrink-0 text-bad" aria-hidden />
      <div className="min-w-0 flex-1">
        <p className="text-[13px] font-bold text-bad">{t(title)}</p>
        <p className="text-[12px] text-ink-2">{t(text)}</p>
      </div>
      <button type="button" onClick={onRetry} className="inline-flex h-9 items-center gap-1.5 rounded-[10px] bg-bad px-3.5 text-[13px] font-semibold text-white">
        <RefreshCw className="size-4" aria-hidden />
        {t("Повторить")}
      </button>
    </div>
  );
}

function LoadingResults({ label }: { label: string }) {
  const t = useT();
  return (
    <div className="space-y-4" aria-busy="true">
      <div className={`flex items-center gap-2 p-5 text-[13px] text-ink-3 ${card}`}>
        <Loader2 className="size-5 animate-spin" aria-hidden />
        {t(label)}
      </div>
      <div className="grid grid-cols-2 gap-3 @min-[640px]/res:grid-cols-3">
        {[0, 1, 2, 3, 4, 5].map((i) => (
          <div key={i} className={`h-32 animate-pulse ${card}`} />
        ))}
      </div>
    </div>
  );
}
