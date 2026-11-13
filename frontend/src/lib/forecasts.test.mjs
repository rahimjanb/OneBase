// «Аналитик → Прогнозы» (lib/forecasts.ts): node --test src/lib/forecasts.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import {
  accuracyTone,
  calendarOf,
  edgeOf,
  factorText,
  amount,
  backtestQuery,
  chartPoints,
  forecastQuery,
  horizonSteps,
  localizeUnits,
  methodTone,
  niceTicks,
  parseParams,
  pct,
  sameWeekday,
  scenarioCards,
  simulatorHref,
  toCsv,
  toQuery,
  wapeLevel,
} from "./forecasts.ts";
import { decodeDraft } from "./simulator.ts";

const all = ["sales.revenue", "sales.kg", "sales.orders", "finance.payments", "procurement.consumption"];

test("параметры: из адреса, по умолчанию и обратно в адрес", () => {
  assert.deepEqual(parseParams({}, all), { metric: "sales.revenue", item: null, step: "day", horizon: 30, whatIf: 0, scenario: "realistic", past: true });
  const p = parseParams(new URLSearchParams("metric=sales.kg&step=week&h=90&whatif=15&scenario=optimistic&past=0&item=x"), all);
  assert.deepEqual(p, { metric: "sales.kg", item: null, step: "week", horizon: 90, whatIf: 15, scenario: "optimistic", past: false }); // позиция — только у сырья
  assert.equal(toQuery(p), "metric=sales.kg&step=week&h=90&whatif=15&scenario=optimistic&past=0");
  assert.equal(toQuery(parseParams({}, all)), "metric=sales.revenue");
  // Чужое и неверное — по умолчанию; «что если» — в пределах ±50 %; показатель, закрытый пользователю, — первый открытый.
  const bad = parseParams({ metric: "finance.payments", step: "year", h: "45", whatif: "300", scenario: "x" }, ["procurement.consumption"]);
  assert.deepEqual(bad, { metric: "procurement.consumption", item: null, step: "day", horizon: 30, whatIf: 50, scenario: "realistic", past: true });
  assert.equal(parseParams({ metric: "procurement.consumption", item: "abc" }, all).item, "abc");
});

test("горизонт в шагах и запросы к API", () => {
  assert.deepEqual([30, 60, 90].map((d) => horizonSteps("day", d)), [30, 60, 90]);
  assert.deepEqual([30, 60, 90].map((d) => horizonSteps("week", d)), [5, 9, 13]);
  assert.deepEqual([30, 60, 90].map((d) => horizonSteps("month", d)), [1, 2, 3]);
  assert.equal(forecastQuery({ metric: "sales.revenue", item: null, step: "week", horizon: 60, whatIf: -10, scenario: "realistic", past: true }), "metric=sales.revenue&granularity=week&horizon=9&scenario=-10");
  assert.equal(backtestQuery({ metric: "procurement.consumption", step: "day", item: "i1" }), "metric=procurement.consumption&granularity=day&item=i1");
});

test("шкала ошибки и тона методов и месяцев", () => {
  assert.equal(wapeLevel(0.1).key, "excellent");
  assert.equal(wapeLevel(0.23).label, "надёжный");
  assert.equal(wapeLevel(0.23).filled, 2);
  assert.equal(wapeLevel(0.3).key, "rough");
  assert.equal(wapeLevel(0.55).tone, "bad");
  assert.equal(wapeLevel(null), null);
  assert.deepEqual([methodTone(0.23, true), methodTone(0.274, false), methodTone(0.356, false), methodTone(null, false)], ["ok", "warn", "bad", "muted"]);
  assert.deepEqual([accuracyTone(0.94), accuracyTone(0.87), accuracyTone(0), accuracyTone(null)], ["ok", "warn", "bad", "muted"]);
});

const view = {
  granularity: "day",
  history: Array.from({ length: 70 }, (_, i) => ({ date: `2026-09-${String((i % 30) + 1).padStart(2, "0")}`, value: 400 })),
  result: {
    points: Array.from({ length: 30 }, (_, i) => ({ date: `2026-11-${String(i + 13).padStart(2, "0")}`, value: 420, lower: 280, upper: 560 })),
    total: 12_600,
    totalLower: 8_400,
    totalUpper: 16_800,
  },
};

test("сценарии: P10 / P50 / P90 и изменение к прошлым 30 дням", () => {
  const cards = scenarioCards(view);
  assert.deepEqual(cards.map((c) => c.key), ["pessimistic", "realistic", "optimistic"]);
  assert.deepEqual(cards.map((c) => c.value), [8_400, 12_600, 16_800]);
  // Прошлые 30 дней: 30 × 400 = 12 000.
  assert.deepEqual(cards.map((c) => Math.round(c.delta * 100)), [-30, 5, 40]);
  assert.equal(scenarioCards({ ...view, history: view.history.slice(0, 10) })[1].delta, null);
  assert.equal(pct(-0.34), "−34%");
  assert.equal(pct(0.3125), "+31%");
  assert.equal(pct(0), "0%");
});

test("точки графика: 60 дней истории, линия выбранного сценария, прошлые прогнозы", () => {
  const past = { past: [{ date: view.history.at(-1).date, value: 380 }] };
  const points = chartPoints(view, "optimistic", past);
  assert.equal(points.filter((p) => p.kind === "fact").length, 60);
  assert.equal(points.filter((p) => p.kind === "forecast").length, 30);
  const f = points.find((p) => p.kind === "forecast");
  assert.deepEqual([f.forecast, f.lower, f.upper, f.median], [560, 280, 560, 420]);
  assert.equal(points.find((p) => p.kind === "fact" && p.past != null).past, 380);
  assert.equal(chartPoints(view, "pessimistic", null).at(-1).forecast, 280);
});

test("тот же день недели, деления оси, суммы и CSV", () => {
  // 2026-11-05, -12 — четверги; 2026-11-06 — пятница.
  const h = [{ date: "2026-11-05", value: 400 }, { date: "2026-11-06", value: 900 }, { date: "2026-11-12", value: 460 }];
  assert.equal(sameWeekday(h, "2026-11-26"), 430);
  assert.equal(sameWeekday([], "2026-11-26"), null);
  assert.deepEqual(niceTicks(1_450_000_000), [0, 400_000_000, 800_000_000, 1_200_000_000, 1_600_000_000]);
  assert.deepEqual(niceTicks(90), [0, 25, 50, 75, 100]);
  assert.deepEqual(niceTicks(2_050_000_000), [0, 1_000_000_000, 2_000_000_000, 3_000_000_000]);
  assert.deepEqual(niceTicks(1_010_000_000), [0, 300_000_000, 600_000_000, 900_000_000, 1_200_000_000]);
  assert.deepEqual(niceTicks(1_000, 2), [0, 500, 1000]);
  assert.equal(localizeUnits("12,6 млрд сум", (w) => ({ млрд: "mlrd", сум: "so'm" })[w] ?? w), "12,6 mlrd so'm");
  assert.deepEqual(amount(12_600_000_000, "сум"), { head: "12,6", tail: "млрд сум" });
  assert.deepEqual(amount(830, "шт"), { head: "830", tail: "шт" });
  const csv = toCsv(chartPoints(view, "realistic", null).slice(-1), ["Дата", "Факт", "Прогноз", "P10", "P90", "Прошлый прогноз"]);
  assert.ok(csv.startsWith("﻿Дата;Факт;Прогноз;P10;P90;Прошлый прогноз\r\n"));
  assert.ok(csv.includes("2026-11-42;;420;280;560;\r\n") || csv.includes(";;420;280;560;\r\n"));
});

test("в Симулятор: вид по показателю, горизонт, «что если» продаж — изменение спроса", () => {
  const draft = (href) => decodeDraft(new URL(href, "https://x").searchParams.get("draft"));
  const sales = draft(simulatorHref({ metric: "sales.revenue", horizon: 60, whatIf: 10 }));
  assert.equal(sales.kind, "sales");
  assert.equal(sales.horizonDays, 60);
  assert.deepEqual(sales.parameters, [{ code: "sales.demand", value: 10, target: null }]);
  assert.deepEqual(draft(simulatorHref({ metric: "sales.kg", horizon: 30, whatIf: 0 })).parameters, []);
  assert.equal(draft(simulatorHref({ metric: "finance.payments", horizon: 30, whatIf: 5 })).kind, "finance");
  assert.deepEqual(draft(simulatorHref({ metric: "procurement.consumption", horizon: 90, whatIf: 5 })).parameters, []);
});

test("край месяца: поле ответа, множитель текстом, дни края", () => {
  assert.deepEqual(calendarOf({ result: {} }), []);
  assert.deepEqual(calendarOf(null), []);
  assert.deepEqual(calendarOf({ result: { calendar: [{ kind: "last", factor: 1.966, months: 6 }] } }), [{ kind: "last", factor: 1.966, months: 6 }]);
  assert.equal(factorText(1.966), "×2,0");
  assert.equal(factorText(0.608), "×0,61");
  assert.deepEqual(["2026-11-30", "2026-12-01", "2026-12-31", "2027-02-28", "2028-02-28", "2026-11-26"].map(edgeOf), ["last", "first", "last", "last", null, null]);
});
