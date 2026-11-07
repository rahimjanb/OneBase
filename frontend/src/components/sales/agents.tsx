"use client";

import Link from "next/link";
import { useMemo, useState } from "react";
import { Search, X } from "lucide-react";
import { FlagPills, levelClass } from "./bits";
import { useT } from "@/i18n/client";
import type { T } from "@/i18n/core";
import { kg, money, num, pct } from "@/lib/sales/format";
import { withQuery } from "@/lib/sales/query";
import type { AgentCardRow, AgentsView } from "@/lib/sales/agents";

// «Агенты»: все ТП месяца карточками — выполнение плана, прогноз и главные цифры; фильтры и поиск — на странице, без запросов к серверу.
// Цифры — те же, что в карточке агента (SalesAnalytics.AgentCard); карточка открывает её.

const control = "h-9 rounded-lg border border-line bg-surface px-3 text-sm text-ink shadow-sm max-lg:h-11";

type Status = "all" | "done" | "below" | "noPlan" | "critical" | "risk" | "ok" | "flags" | "vacancy";
type Sort = "execution" | "fact" | "revenue" | "strike" | "name";

const statuses: { key: Status; label: string }[] = [
  { key: "all", label: "Все" },
  { key: "done", label: "Выполняют план" },
  { key: "below", label: "Ниже плана" },
  { key: "noPlan", label: "Без плана" },
  { key: "critical", label: "Критично" },
  { key: "risk", label: "Риск" },
  { key: "ok", label: "В норме" },
  { key: "flags", label: "С замечаниями" },
  { key: "vacancy", label: "Вакансии" },
];

const sorts: { key: Sort; label: string }[] = [
  { key: "execution", label: "По выполнению" },
  { key: "fact", label: "По факту, кг" },
  { key: "revenue", label: "По выручке" },
  { key: "strike", label: "По страйку" },
  { key: "name", label: "По имени" },
];

/** Выполнение для статуса: в идущем месяце — по прогнозу (факт к середине месяца всегда «ниже плана»), в закрытом — по факту. */
const progressOf = (a: AgentCardRow) => a.forecastExecution ?? a.execution;

/**
 * «Критично / Риск / В норме» — как блок ТП на «Вторичке» (сервер, FlagCountsOf): действующие ТП без вакансий по худшему флагу;
 * «В норме» — без критичных и рисковых флагов (справочные не считаются).
 */
const worstOf = (a: AgentCardRow): "critical" | "risk" | "ok" =>
  a.flags.some((f) => f.severity === "Critical") ? "critical" : a.flags.some((f) => f.severity === "Risk") ? "risk" : "ok";

const matches = (a: AgentCardRow, s: Status) =>
  s === "all"
    ? true
    : s === "done"
      ? (progressOf(a) ?? 0) >= 1
      : s === "below"
        ? progressOf(a) != null && progressOf(a)! < 1
        : s === "noPlan"
          ? !a.planKg
          : s === "critical" || s === "risk" || s === "ok"
            ? !a.isVacancy && a.isSalesRep && worstOf(a) === s
            : s === "flags"
              ? a.flags.length > 0
              : a.isVacancy;

const normalize = (s: string) => s.toLocaleLowerCase("ru").replace(/ё/g, "е").trim();

export function AgentsReport({
  data,
  query,
  initial,
}: {
  data: AgentsView;
  query: string;
  initial: { q?: string; rm?: string; region?: string; status?: string };
}) {
  const t = useT();
  const [search, setSearch] = useState(initial.q ?? "");
  const [rm, setRm] = useState(initial.rm ?? "");
  const [region, setRegion] = useState(initial.region ?? "");
  const [status, setStatus] = useState<Status>(statuses.some((s) => s.key === initial.status) ? (initial.status as Status) : "all");
  const [sort, setSort] = useState<Sort>("execution");

  // Регионы выбранного РМ — в списке регионов только они.
  const regions = useMemo(
    () => (rm ? data.regions.filter((r) => data.agents.some((a) => a.directionId === rm && (a.regionId ?? "") === r.id)) : data.regions),
    [data, rm],
  );

  const scoped = useMemo(() => {
    const words = normalize(search).split(/\s+/).filter(Boolean);
    return data.agents.filter((a) => {
      if (rm && a.directionId !== rm) return false;
      if (region && (a.regionId ?? "00000000-0000-0000-0000-000000000000") !== region) return false;
      if (words.length) {
        const text = normalize(`${a.name} ${a.agentId} ${a.regionName} ${a.directionName ?? ""} ${a.supervisor ?? ""}`);
        if (!words.every((w) => text.includes(w))) return false;
      }
      return true;
    });
  }, [data, search, rm, region]);

  const shown = useMemo(() => {
    const list = scoped.filter((a) => matches(a, status));
    const by = (f: (a: AgentCardRow) => number | null) => (a: AgentCardRow, b: AgentCardRow) =>
      (f(b) ?? -Infinity) - (f(a) ?? -Infinity) || b.factKg - a.factKg;
    return [...list].sort(
      sort === "name"
        ? (a, b) => a.name.localeCompare(b.name, "ru")
        : sort === "fact"
          ? by((a) => a.factKg)
          : sort === "revenue"
            ? by((a) => a.revenue)
            : sort === "strike"
              ? by((a) => a.conversion)
              : by(progressOf),
    );
  }, [scoped, status, sort]);

  const filtered = search || rm || region || status !== "all";
  const reset = () => {
    setSearch("");
    setRm("");
    setRegion("");
    setStatus("all");
  };

  // Сводка выбранных ТП (без вакансий): сколько с планом, сколько выполняют, факт к плану.
  const team = shown.filter((a) => !a.isVacancy);
  const withPlan = team.filter((a) => a.planKg);
  const planSum = withPlan.reduce((s, a) => s + (a.planKg ?? 0), 0);
  const factSum = withPlan.reduce((s, a) => s + a.factKg, 0);

  return (
    <>
      <div className="flex flex-wrap items-center gap-2">
        <label className={`${control} flex min-w-[240px] flex-1 items-center gap-2 sm:max-w-sm`}>
          <Search className="size-4 shrink-0 text-ink-3" aria-hidden />
          <input
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={t("Имя, ID или регион")}
            aria-label={t("Поиск по имени или региону")}
            className="min-w-0 flex-1 bg-transparent outline-none placeholder:text-ink-3"
          />
        </label>
        {data.directions.length > 0 && (
          <select
            aria-label={t("РМ")}
            value={rm}
            onChange={(e) => {
              setRm(e.target.value);
              setRegion("");
            }}
            className={control}
          >
            <option value="">{t("Все РМ")}</option>
            {data.directions.map((d) => (
              <option key={d.id} value={d.id}>
                {d.name}
              </option>
            ))}
          </select>
        )}
        <select aria-label={t("Регион")} value={region} onChange={(e) => setRegion(e.target.value)} className={control}>
          <option value="">{t("Все регионы")}</option>
          {regions.map((r) => (
            <option key={r.id} value={r.id}>
              {r.name}
            </option>
          ))}
        </select>
        <select aria-label={t("Сортировка")} value={sort} onChange={(e) => setSort(e.target.value as Sort)} className={control}>
          {sorts.map((s) => (
            <option key={s.key} value={s.key}>
              {t(s.label)}
            </option>
          ))}
        </select>
        {filtered && (
          <button type="button" onClick={reset} className={`${control} inline-flex items-center gap-1.5 text-ink-2 hover:bg-muted`}>
            <X className="size-3.5" aria-hidden />
            {t("Сбросить")}
          </button>
        )}
      </div>

      <div className="mt-3 flex flex-wrap items-center gap-1.5" role="group" aria-label={t("Статус")}>
        {statuses.map((s) => {
          const count = scoped.filter((a) => matches(a, s.key)).length;
          const on = status === s.key;
          return (
            <button
              key={s.key}
              type="button"
              aria-pressed={on}
              onClick={() => setStatus(s.key)}
              className={`rounded-full border px-3 py-1 text-xs font-medium max-lg:py-2 ${on ? "border-accent bg-accent text-white" : "border-line bg-surface text-ink-2 hover:bg-muted"}`}
            >
              {t(s.label)} <span className={on ? "text-white/80" : "text-ink-3"}>{num(count)}</span>
            </button>
          );
        })}
      </div>

      <p className="mt-3 text-xs text-ink-3">
        {t("Показано {n} из {all} ТП", { n: num(shown.length), all: num(data.agents.length) })}
        {withPlan.length > 0 && (
          <>
            {" · "}
            {t("с планом {n}: факт {fact} из {plan} кг ({pct})", {
              n: num(withPlan.length),
              fact: kg(factSum),
              plan: kg(planSum),
              pct: pct(planSum ? factSum / planSum : null),
            })}
          </>
        )}
        {!data.period.closed && <> · {t("статус «выполняют план» в идущем месяце — по прогнозу")}</>}
      </p>

      {shown.length === 0 ? (
        <div className="mt-4 rounded-xl border border-dashed border-line px-4 py-10 text-center text-sm text-ink-3">
          {t("Таких ТП нет — измените фильтры или поиск.")}
        </div>
      ) : (
        <div className="mt-3 grid gap-3 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4">
          {shown.map((a) => (
            <AgentCard key={a.agentId} agent={a} href={withQuery(`/sales/agents/${a.agentId}`, query)} targets={data.targets} t={t} />
          ))}
        </div>
      )}
    </>
  );
}

function AgentCard({ agent: a, href, targets, t }: { agent: AgentCardRow; href: string; targets: AgentsView["targets"]; t: T }) {
  const share = a.execution ?? 0;
  const forecast = a.forecastExecution;
  const bar = (v: number) => `${Math.min(100, Math.max(0, v * 100))}%`;
  const barTone =
    (a.forecastExecutionLevel ?? a.executionLevel) === "Good" ? "bg-ok" : (a.forecastExecutionLevel ?? a.executionLevel) === "Warning" ? "bg-warn" : "bg-bad";

  return (
    <Link
      href={href}
      className="group flex min-w-0 flex-col rounded-xl border border-line bg-surface p-4 shadow-sm transition-colors hover:border-accent/50 hover:bg-muted/40"
    >
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <div className="truncate font-semibold text-ink group-hover:text-accent">{a.name}</div>
          <div className="truncate text-xs text-ink-3">
            {[a.regionName, a.directionName, a.supervisor ? t("СВ {name}", { name: a.supervisor }) : null].filter(Boolean).join(" · ")}
          </div>
        </div>
        {a.isVacancy && <span className="shrink-0 rounded-full bg-muted px-2 py-0.5 text-[11px] font-medium text-ink-2">{t("вакансия")}</span>}
      </div>

      <div className="mt-3">
        {a.planKg ? (
          <>
            <div className="flex items-baseline justify-between gap-2">
              <span className="text-[11px] font-medium uppercase tracking-[0.08em] text-ink-3">{t("Выполнение плана")}</span>
              <span className={`text-xl font-semibold tabular-nums ${levelClass(a.executionLevel)}`}>{pct(a.execution)}</span>
            </div>
            <div className="relative mt-1.5 h-2 overflow-hidden rounded-full bg-muted" aria-hidden>
              {forecast != null && <div className="absolute inset-y-0 left-0 rounded-full bg-ink-3/25" style={{ width: bar(forecast) }} />}
              <div className={`absolute inset-y-0 left-0 rounded-full ${barTone}`} style={{ width: bar(share) }} />
            </div>
            <div className="mt-1.5 text-xs text-ink-2">
              {t("{fact} из {plan} кг", { fact: kg(a.factKg), plan: kg(a.planKg) })}
              {forecast != null && (
                <span className="text-ink-3">
                  {" · "}
                  {t("прогноз {kg} кг", { kg: kg(a.forecastKg) })} <span className={levelClass(a.forecastExecutionLevel)}>({pct(forecast)})</span>
                </span>
              )}
            </div>
          </>
        ) : (
          <>
            <div className="flex items-baseline justify-between gap-2">
              <span className="text-[11px] font-medium uppercase tracking-[0.08em] text-ink-3">{t("Факт")}</span>
              <span className="text-xl font-semibold tabular-nums text-ink">{t("{kg} кг", { kg: kg(a.factKg) })}</span>
            </div>
            <div className="mt-1.5 text-xs text-ink-3">{t("плана ТП в Linko на месяц нет")}</div>
          </>
        )}
      </div>

      <dl className="mt-3 grid grid-cols-3 gap-x-3 gap-y-2 border-t border-line pt-3 text-xs">
        <Metric label={t("Выручка")} value={money(a.revenue, t)} sub={a.revenueExecution != null ? t("{pct} плана", { pct: pct(a.revenueExecution) }) : null} />
        <Metric
          label={t("АКБ")}
          value={num(a.akb)}
          sub={t("цель {n}", { n: num(targets.akbPerAgent) })}
          tone={a.akb >= targets.akbPerAgent ? "text-ok" : undefined}
        />
        <Metric
          label={t("Страйк")}
          value={pct(a.conversion)}
          sub={t("цель {pct}", { pct: pct(targets.conversion) })}
          tone={a.conversion == null ? undefined : a.conversion >= targets.conversion ? "text-ok" : "text-bad"}
        />
        <Metric label={t("Визиты")} value={num(a.visits)} sub={t("{n} с заказом", { n: num(a.visitsWithOrder) })} />
        <Metric label={t("Заказы")} value={num(a.orders)} sub={a.sumPerVisit != null ? t("{sum}/визит", { sum: money(a.sumPerVisit, t) }) : null} />
        <Metric
          label={t("Темп")}
          value={pct(a.tempo)}
          sub={t("{n} категорий", { n: num(a.categories) })}
          tone={a.tempo == null ? undefined : a.tempo >= 1 ? "text-ok" : "text-bad"}
        />
      </dl>

      {(a.flags.length > 0 || !a.isVacancy) && (
        <div className="mt-3">
          <FlagPills flags={a.flags} empty={a.isVacancy ? null : "Замечаний нет"} />
        </div>
      )}
    </Link>
  );
}

function Metric({ label, value, sub, tone }: { label: string; value: string; sub?: string | null; tone?: string }) {
  return (
    <div className="min-w-0">
      <dt className="truncate text-[10px] font-medium uppercase tracking-[0.06em] text-ink-3">{label}</dt>
      <dd className={`truncate font-semibold tabular-nums ${tone ?? "text-ink"}`}>{value}</dd>
      {sub && <dd className="truncate text-[11px] text-ink-3">{sub}</dd>}
    </div>
  );
}
