/**
 * «Продажи → Вторичка» без React: статус и полоса «Выполнения плана», строки темпа, полосы и цвета KPI, разбивка ТП, ссылка «Задать план».
 * Цвета — по уровням сервера (пороги на сервере: прогноз — 100% и 90%, показатели с целью — 95% и 75%, выполнение закрытого месяца — 90% и 60%);
 * здесь только одно правило ТЗ, которого на сервере нет: «Визиты без заказа» — предупреждение, если их больше 40% визитов.
 * Подписи — русские ключи словаря, переводит компонент. Проверяется тестами (sales-secondary.test.mjs).
 */

import type { KpiTiles, Period, PlanKind, TargetLevel, TargetValue } from "@/lib/sales/types";

export type Tone = "ok" | "warn" | "bad" | "none";

export const toneOf = (level: TargetLevel | null | undefined): Tone =>
  level === "Good" ? "ok" : level === "Warning" ? "warn" : level === "Bad" ? "bad" : "none";

const clamp = (v: number) => Math.min(1, Math.max(0, v));

// ---------- Выполнение плана ----------

export type PaceRow =
  | { key: "pace"; kg: number | null; closed: boolean }
  | { key: "required"; kg: number; behind: boolean }
  | { key: "over"; share: number }
  | { key: "short"; kg: number }
  | { key: "forecast"; kg: number | null; share: number | null }
  | { key: "result"; kg: number; share: number | null };

export type PlanBlock = {
  /** plan — план есть, не выполнен; done — факт не ниже плана; noPlan — плана нет ни в одном источнике. */
  kind: "plan" | "done" | "noPlan";
  tone: Tone;
  /** Метка статуса и короткая — для телефона; null — без метки (в ответе сервера нет темпа: API старее интерфейса). */
  label: string | null;
  short: string | null;
  /** Полоса, доли 0..1: факт, отметка «должно быть к дате» (только идущий месяц), прогноз на конец месяца (только идущий). */
  bar: { fact: number; expected: number | null; forecast: number | null } | null;
  /** Перевыполнение: выполнение − 1, если план выполнен с запасом. */
  over: number | null;
  rows: PaceRow[];
};

/**
 * Главный блок. Метка идущего месяца — по уровню прогноза с сервера: зелёный — «Идём по плану», жёлтый — «Близко к темпу», красный —
 * «Отстаём от темпа»; факт уже не ниже плана — «План выполнен ✓». У закрытого месяца — по выполнению: «Почти выполнен» (от 90%) или
 * «План не выполнен». Пока прогноза нет (1-е число, полных дней ещё нет) — «Прогноза пока нет». Темпа в ответе нет (API ещё не обновлён) —
 * без метки и строки темпа, остальное как есть.
 */
export function planBlock(kpi: KpiTiles, period: Pick<Period, "closed">): PlanBlock {
  const pace = kpi.pace ?? null;
  const closed = period.closed;
  const paceRow: PaceRow = { key: "pace", kg: pace?.paceKg ?? null, closed };
  const lead: PaceRow[] = pace ? [paceRow] : [];

  if (kpi.planKg == null || kpi.planKg <= 0) {
    return {
      kind: "noPlan",
      tone: "none",
      label: "Плана нет",
      short: "Плана нет",
      bar: null,
      over: null,
      rows: closed ? [...lead, { key: "result", kg: kpi.factKg, share: null }] : [...lead, { key: "forecast", kg: kpi.forecastKg, share: null }],
    };
  }

  const fact = kpi.planFactKg ?? kpi.factKg;
  const execution = kpi.execution ?? fact / kpi.planKg;
  const forecastRow: PaceRow = { key: "forecast", kg: kpi.planForecastKg, share: kpi.forecastExecution };

  if (fact >= kpi.planKg) {
    const over = execution - 1 >= 0.0005 ? execution - 1 : null;
    return {
      kind: "done",
      tone: "ok",
      label: "План выполнен ✓",
      short: "Выполнен ✓",
      bar: { fact: 1, expected: null, forecast: null },
      over,
      rows: closed
        ? [...lead, ...(over != null ? [{ key: "over", share: over } as const] : []), { key: "result", kg: fact, share: execution }]
        : [...lead, ...(over != null ? [{ key: "over", share: over } as const] : []), forecastRow],
    };
  }

  if (closed) {
    const tone = toneOf(pace?.level ?? kpi.executionLevel);
    return {
      kind: "plan",
      tone,
      label: tone === "ok" ? "Почти выполнен" : "План не выполнен",
      short: tone === "ok" ? "Почти" : "Не выполнен",
      bar: { fact: clamp(execution), expected: null, forecast: null },
      over: null,
      rows: [...lead, { key: "short", kg: kpi.planKg - fact }, { key: "result", kg: fact, share: execution }],
    };
  }

  const tone = toneOf(pace?.level);
  const labels: Record<Tone, [string, string]> = {
    ok: ["Идём по плану", "По плану"],
    warn: ["Близко к темпу", "Близко"],
    bad: ["Отстаём от темпа", "Отстаём"],
    none: ["Прогноза пока нет", "Нет прогноза"],
  };
  const required = pace?.requiredKg;
  return {
    kind: "plan",
    tone,
    label: pace ? labels[tone][0] : null,
    short: pace ? labels[tone][1] : null,
    bar: {
      fact: clamp(execution),
      // 1-е число до первого полного дня: «должно быть 0%» ничего не говорит — отметки нет.
      expected: pace?.expected == null || pace.expected <= 0 ? null : clamp(pace.expected),
      forecast: kpi.forecastExecution == null ? null : clamp(kpi.forecastExecution),
    },
    over: null,
    rows: [
      ...lead,
      ...(required != null ? [{ key: "required", kg: required, behind: pace?.paceKg == null || required > pace.paceKg } as const] : []),
      forecastRow,
    ],
  };
}

// ---------- Пометка о плане и «Задать план» ----------

/**
 * Пометка под полосой: plan — выбранного плана регионов на месяц нет, выполнение — по планам ТП из Linko; none — плана нет ни в одном источнике.
 * null — план РОП или «Завод» на месяц есть.
 */
export function planNote(kpi: KpiTiles): "linko" | "none" | null {
  if (kpi.planKg == null || kpi.planKg <= 0) return "none";
  return kpi.planSource === "linko" ? "linko" : null;
}

/**
 * Куда вести «Задать план»: план РОП грузится привязкой таблицы в «Источниках данных» (integrations.manage) или AI Data Manager
 * (ai.data.manage); план «Завод» — итогами по категориям в «Настройках продаж» (sales.manage) или привязкой таблицы. Нет прав — ссылки нет.
 */
export function planLink(kind: PlanKind, permissions: readonly string[]): string | null {
  const has = (p: string) => permissions.includes(p);
  if (kind === "factory" && has("sales.manage")) return "/sales/setup";
  if (has("integrations.manage")) return "/settings/data-sources";
  if (kind === "rop" && has("ai.data.manage")) return "/data-manager";
  return null;
}

// ---------- KPI ----------

export type KpiBar = { fact: number; forecast: number | null };

/**
 * Показатель с целью (конверсия, выручка на ТТ, АКБ на агента): яркая часть полосы — значение к цели, бледная — прогноз к цели
 * (только в идущем месяце и если сервер его дал). Цвет — уровень сервера (у копящихся показателей — по прогнозу).
 */
export function targetKpi(tv: TargetValue, closed: boolean): { tone: Tone; bar: KpiBar | null; share: number | null; forecastShare: number | null } {
  const share = tv.value == null || tv.target <= 0 ? null : tv.value / tv.target;
  const forecastShare = closed || tv.forecast == null || tv.target <= 0 ? null : tv.forecast / tv.target;
  return {
    tone: toneOf(tv.level),
    bar: share == null ? null : { fact: clamp(share), forecast: forecastShare == null ? null : clamp(forecastShare) },
    share,
    forecastShare,
  };
}

/** Доля визитов без заказа, выше которой — предупреждение (ТЗ «Вторички»). */
export const VISITS_NO_ORDER_WARN = 0.4;

/** «Визиты без заказа»: доля от визитов ТП; больше 40% — предупреждение, иначе без статусного цвета. */
export function visitsNoOrder(kpi: Pick<KpiTiles, "visitsWithoutOrder" | "visitsDone">): { share: number | null; tone: Tone; bar: KpiBar | null } {
  const share = kpi.visitsDone > 0 ? kpi.visitsWithoutOrder / kpi.visitsDone : null;
  return { share, tone: share != null && share > VISITS_NO_ORDER_WARN ? "warn" : "none", bar: share == null ? null : { fact: clamp(share), forecast: null } };
}

// ---------- Торговые представители ----------

export type AgentSegment = { key: "critical" | "risk" | "ok"; label: string; n: number; share: number };

/**
 * Блок ТП: требуют внимания — с критичным или рисковым флагом (у каждого ТП считается худший флаг, как на сервере), в норме — остальные
 * действующие. Доли — для полосы; пустые сегменты не рисуются.
 */
export function agentsSplit(activeAgents: number, flags: { critical: number; risk: number }) {
  const attention = flags.critical + flags.risk;
  const ok = Math.max(0, activeAgents - attention);
  const total = Math.max(activeAgents, attention);
  const seg = (key: AgentSegment["key"], label: string, n: number): AgentSegment => ({ key, label, n, share: total > 0 ? n / total : 0 });
  return {
    attention,
    total,
    segments: [seg("critical", "Критично", flags.critical), seg("risk", "Риск", flags.risk), seg("ok", "В норме", ok)],
    tone: (flags.critical > 0 ? "bad" : flags.risk > 0 ? "warn" : "ok") as Tone,
  };
}

// ---------- Дни месяца ----------

/** Мини-полоса у «Данные по»: прошедшие дни (по отчётный день включительно) и их доля месяца. */
export function monthDays(period: Pick<Period, "workedDays" | "daysInMonth">): { worked: number; days: number; share: number } {
  const days = Math.max(1, period.daysInMonth);
  const worked = Math.min(days, Math.max(0, period.workedDays));
  return { worked, days, share: worked / days };
}
