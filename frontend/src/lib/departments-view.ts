/**
 * «Отделы» без React: порядок отделов, месяц из адреса, карточка каждого отдела (статус, значение с периодом, полоса с прогнозом, строка
 * деталей, порог для подсказки). Расчёт и пороги — прежние: продажи — прогноз выполнения плана (выполнен — «План выполнен», прогноз от 100% —
 * «В норме»), HR — явка, норма 80%, снабжение — заявки к закупке без просрочки. У отделов без источника данных — «Нет данных».
 * Проверяется тестами (departments-view.test.mjs).
 */

import type { T } from "@/i18n/core";

export type DeptStatus = "done" | "ok" | "attention" | "none" | "error";
export type DeptDetailIcon = "cart" | "attendance" | "info" | "lock";

export type DeptCard = {
  code: string;
  /** Название отдела, переведённое. */
  name: string;
  status: DeptStatus;
  /** Порог для подсказки у метки: «Норма ≥ 80%, сейчас 82%». */
  reason: string | null;
  /** Крупное значение, %; null — «—». */
  value: number | null;
  /** Период показателя рядом со значением: «ноябрь 2026». */
  period: string | null;
  /** Название показателя под значением. */
  label: string;
  /** Полоса: value — факт (заливка цветом статуса), forecast — прогноз бледнее поверх трека; null — пустая полоса. */
  bar: { value: number; forecast: number | null } | null;
  /** Легенда двухслойной полосы (продажи); short — одной строкой для телефона. */
  legend: { fact: string; forecast: string | null; short: string } | null;
  detail: { icon: DeptDetailIcon; text: string } | null;
};

/** Ответ API для карточки: данные, нет доступа (раздела или права) или ошибка — у ошибки «Повторить». */
export type Load<D> = { state: "ok"; data: D } | { state: "forbidden" } | { state: "error" };

/** Отделы — в прежнем порядке; показатель — что считается (или будет считаться) у отдела. */
export const departmentList = [
  { code: "production", name: "Производство", label: "Выпуск продукции" },
  { code: "finance", name: "Финансы", label: "Платёжная дисциплина" },
  { code: "sales", name: "Продажи", label: "Прогноз выполнения плана" },
  { code: "marketing", name: "Маркетинг", label: "Квалифицированные лиды" },
  { code: "supply", name: "Снабжение", label: "Согласованное закуплено" },
  { code: "hr", name: "HR", label: "Явка с начала месяца" },
] as const;

export type DeptCode = (typeof departmentList)[number]["code"];

// ---------- Месяц ----------

export type Month = { key: string; year: number; month: number; current: boolean };

/** Месяц из адреса (?month=2026-11): неверный или будущий — текущий по Ташкенту (today — «2026-11-07»). */
export function parseMonth(raw: string | null | undefined, today: string): Month {
  const now = { year: Number(today.slice(0, 4)), month: Number(today.slice(5, 7)) };
  const m = /^(\d{4})-(\d{2})$/.exec(raw ?? "");
  const year = m ? Number(m[1]) : now.year;
  const month = m ? Number(m[2]) : now.month;
  const valid = m && month >= 1 && month <= 12 && year * 12 + month <= now.year * 12 + now.month && year >= 2020;
  const pick = valid ? { year, month } : now;
  return { key: `${pick.year}-${String(pick.month).padStart(2, "0")}`, ...pick, current: pick.year === now.year && pick.month === now.month };
}

/** Последние count месяцев, начиная с текущего: «2026-11», «2026-10», … */
export function monthOptions(today: string, count = 12): string[] {
  let year = Number(today.slice(0, 4));
  let month = Number(today.slice(5, 7));
  const out: string[] = [];
  for (let i = 0; i < count; i++) {
    out.push(`${year}-${String(month).padStart(2, "0")}`);
    month -= 1;
    if (month === 0) {
      month = 12;
      year -= 1;
    }
  }
  return out;
}

/** Дни месяца для отчётов: с 1-го по последний; текущий — по сегодня. */
export function monthRange(m: Month, today: string): { from: string; to: string } {
  const from = `${m.key}-01`;
  if (m.current) return { from, to: today };
  const last = new Date(Date.UTC(m.year, m.month, 0)).getUTCDate();
  return { from, to: `${m.key}-${String(last).padStart(2, "0")}` };
}

// ---------- Карточки ----------

export type Ctx = {
  t: T;
  /** Выбранный месяц: «ноябрь 2026». */
  month: string;
  current: boolean;
  /** Название месяца: (2026, 11) → «ноябрь 2026». */
  monthLabel: (year: number, month: number) => string;
};

/** Фраза с заглавной: в узбекском шаблоне месяц может стоять первым («noyabr 2026 uchun…»). */
const cap = (text: string) => text.charAt(0).toUpperCase() + text.slice(1);
const pct = (value: number) => `${value.toLocaleString("ru-RU", { maximumFractionDigits: 1 })}%`;
const tons = (kg: number) => (kg / 1000).toLocaleString("ru-RU", { maximumFractionDigits: 1 });

function base(code: DeptCode, ctx: Ctx): DeptCard {
  const d = departmentList.find((x) => x.code === code)!;
  return { code, name: ctx.t(d.name), status: "none", reason: null, value: null, period: ctx.month, label: ctx.t(d.label), bar: null, legend: null, detail: null };
}

/** Нет источника данных (производство, финансы, маркетинг) — «Нет данных за ноябрь 2026». */
export function noDataCard(code: DeptCode, ctx: Ctx): DeptCard {
  return { ...base(code, ctx), detail: { icon: "info", text: cap(ctx.t("Нет данных за {month}", { month: ctx.month })) } };
}

/** Нет доступа к данным раздела — «Нет данных» без числа; ошибка ответа — «Не удалось загрузить» (кнопка «Повторить» — в карточке). */
function unavailable(code: DeptCode, load: { state: "forbidden" | "error" }, ctx: Ctx, why = "Нет доступа к показателям отдела"): DeptCard {
  const card = base(code, ctx);
  return load.state === "error" ? { ...card, status: "error", period: null } : { ...card, period: null, detail: { icon: "lock", text: ctx.t(why) } };
}

export type SalesData = {
  period: { year: number; month: number; closed: boolean };
  kpi: { planKg: number | null; factKg: number; execution: number | null; forecastExecution: number | null };
};

/**
 * Продажи: крупно — прогноз выполнения плана (у закрытого месяца — выполнение); полоса двухслойная: ярко — факт к плану, бледно — прогноз;
 * легенда «Факт 62,3 из 437,4 т · 14%» и «Прогноз 52%». Статус: факт ≥ плана — «План выполнен», прогноз ≥ 100% — «В норме», ниже —
 * «Требует внимания»; плана нет — «Требует внимания».
 */
export function salesCard(load: Load<SalesData>, ctx: Ctx): DeptCard {
  if (load.state !== "ok") return unavailable("sales", load, ctx);
  const { t } = ctx;
  const { period, kpi: k } = load.data;
  const month = ctx.monthLabel(period.year, period.month);
  const card = { ...base("sales", ctx), period: month };
  if (k.planKg == null || k.planKg <= 0) {
    return {
      ...card,
      status: "attention",
      reason: cap(t("Плана на {month} нет", { month })),
      detail: { icon: "info", text: cap(t("Плана на {month} нет · факт {fact} т", { month, fact: tons(k.factKg) })) },
    };
  }

  const execution = k.execution ?? k.factKg / k.planKg;
  const forecast = period.closed ? null : (k.forecastExecution ?? null);
  const shown = forecast ?? execution;
  const value = Math.round(shown * 100);
  const fact = Math.round(execution * 100);
  const status: DeptStatus = execution >= 1 ? "done" : shown >= 1 ? "ok" : "attention";
  return {
    ...card,
    status,
    value,
    label: period.closed ? t("Выполнение плана") : t("Прогноз выполнения плана"),
    reason:
      status === "done"
        ? t("План выполнен: {pct}", { pct: pct(fact) })
        : period.closed
          ? t("Цель — 100% плана, выполнено {pct}", { pct: pct(fact) })
          : t("Цель — прогноз от 100% плана, сейчас {pct}", { pct: pct(value) }),
    bar: { value: Math.min(100, fact), forecast: forecast == null ? null : Math.min(100, value) },
    legend: {
      fact: t("Факт {fact} из {plan} т · {pct}", { fact: tons(k.factKg), plan: tons(k.planKg), pct: pct(fact) }),
      forecast: forecast == null ? null : t("Прогноз {pct}", { pct: pct(value) }),
      short:
        forecast == null
          ? t("Факт {fact} из {plan} т ({pct})", { fact: tons(k.factKg), plan: tons(k.planKg), pct: pct(fact) })
          : t("Факт {fact} из {plan} т ({pct}) · прогноз {forecast}", { fact: tons(k.factKg), plan: tons(k.planKg), pct: pct(fact), forecast: pct(value) }),
    },
  };
}

export type HrData = {
  /** Явка с начала месяца (за прошлый месяц — за весь месяц), %. */
  monthRate: number | null;
  /** Явка сегодня, % — только у текущего месяца; null — сегодня не загружено. */
  presentPct: number | null;
};

/**
 * HR: крупно и полоса — явка за месяц (у текущего месяца без месячной — явка сегодня), норма 80%: от неё — «В норме», ниже — «Требует
 * внимания». У текущего месяца — строка «Явка сегодня: 91%», а пока отметок нет — «Сегодня отметок пока нет» (не «0%»). Прошлый месяц — без
 * строки «сегодня».
 */
export function hrCard(load: Load<HrData>, ctx: Ctx): DeptCard {
  if (load.state !== "ok") return unavailable("hr", load, ctx);
  const { t } = ctx;
  const { monthRate, presentPct } = load.data;
  const today = ctx.current ? presentPct : null;
  const score = monthRate ?? today;
  if (score == null) return noDataCard("hr", ctx);
  const detail = ctx.current
    ? { icon: "attendance" as const, text: presentPct == null || presentPct <= 0 ? t("Сегодня отметок пока нет") : t("Явка сегодня: {pct}", { pct: pct(presentPct) }) }
    : null;
  return {
    ...base("hr", ctx),
    status: score >= 80 ? "ok" : "attention",
    value: Math.round(score),
    period: monthRate == null ? null : ctx.month,
    label: monthRate == null ? t("Явка сегодня") : ctx.current ? t("Явка с начала месяца") : t("Явка за месяц"),
    reason: t("Норма ≥ 80%, сейчас {pct}", { pct: pct(Math.round(score)) }),
    bar: { value: Math.min(100, Math.round(score)), forecast: null },
    detail: monthRate == null ? null : detail,
  };
}

export type SupplyData = { requestsAwaitingOrder: number | null; overdueToBuy?: number | null; onApprovalCount?: number | null };

/**
 * Снабжение: доля заявок к закупке без просрочки (к закупке ничего — 100%); есть просроченные — «Требует внимания», нет — «В норме». Это
 * показатель на сегодня: за прошлый месяц — «Показатель только на сегодня». Заявки видят снабжение и директор — у остальных «Нет данных».
 */
export function supplyCard(load: Load<SupplyData>, ctx: Ctx): DeptCard {
  const { t } = ctx;
  if (!ctx.current) return { ...base("supply", ctx), period: null, detail: { icon: "info", text: t("Показатель только на сегодня — за прошлые месяцы его нет") } };
  if (load.state !== "ok") return unavailable("supply", load, ctx);
  const p = load.data;
  if (p.requestsAwaitingOrder == null) return unavailable("supply", { state: "forbidden" }, ctx, "Заявки к закупке видят снабжение и директор");
  const toBuy = p.requestsAwaitingOrder;
  const overdue = Math.min(p.overdueToBuy ?? 0, toBuy);
  const onTime = toBuy > 0 ? Math.round((100 * (toBuy - overdue)) / toBuy) : 100;
  const approval = p.onApprovalCount ?? 0;
  return {
    ...base("supply", ctx),
    status: overdue > 0 ? "attention" : "ok",
    value: onTime,
    period: null,
    label: toBuy > 0 ? t("Заявки к закупке без просрочки") : t("Согласованное закуплено"),
    reason: t("Цель — без просроченных заявок, просрочено {n}", { n: overdue }),
    bar: { value: onTime, forecast: null },
    detail: {
      icon: "cart",
      text:
        approval > 0
          ? t("К закупке {a} · просрочено {b} · на согласовании {c}", { a: toBuy, b: overdue, c: approval })
          : t("К закупке {a} · просрочено {b}", { a: toBuy, b: overdue }),
    },
  };
}

/** Подпись карточки для экранного диктора: «Продажи, Прогноз выполнения плана 52%, Требует внимания». */
export function cardAriaLabel(card: DeptCard, statusLabel: string): string {
  return `${card.name}, ${card.label} ${card.value == null ? "—" : `${card.value}%`}, ${statusLabel}`;
}

/** Подпись полосы: «Продажи: факт 14%, прогноз 52%». */
export function barAriaLabel(card: DeptCard, t: T): string {
  if (!card.bar) return t("{name}: нет данных", { name: card.name });
  return card.bar.forecast == null
    ? t("{name}: {pct}", { name: card.name, pct: `${card.bar.value}%` })
    : t("{name}: факт {fact}, прогноз {forecast}", { name: card.name, fact: `${card.bar.value}%`, forecast: `${card.bar.forecast}%` });
}
