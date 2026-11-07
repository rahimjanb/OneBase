// «Продажи → Вторичка» (lib/sales-secondary.ts): node --test src/lib/sales-secondary.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import { agentsSplit, monthDays, planBlock, planLink, planNote, targetKpi, visitsNoOrder } from "./sales-secondary.ts";

// Как на макете: ноябрь 2026, данные по 07.11 — 52 991 из 437 410 кг, прогноз 52%, план ТП из Linko.
const kpi = (over = {}) => ({
  factKg: 60_000,
  planKg: 437_410,
  execution: 52_991 / 437_410,
  forecastKg: 257_000,
  forecastExecution: 0.52,
  planFactKg: 52_991,
  planForecastKg: 227_100,
  planSource: "linko",
  executionLevel: "Bad",
  pace: { expected: 7 / 30, paceKg: 7_570, requiredKg: 16_714, level: "Bad" },
  ...over,
});
const open = { closed: false };

test("идёт месяц, отстаём: метка по уровню прогноза, полоса — факт, отметка «должно быть к дате» и прогноз; нужно в день больше темпа", () => {
  const block = planBlock(kpi(), open);
  assert.deepEqual([block.kind, block.tone, block.label, block.short], ["plan", "bad", "Отстаём от темпа", "Отстаём"]);
  assert.equal(Math.round(block.bar.fact * 1000) / 10, 12.1);
  assert.equal(block.bar.expected, 7 / 30);
  assert.equal(block.bar.forecast, 0.52);
  assert.deepEqual(block.rows.map((r) => r.key), ["pace", "required", "forecast"]);
  assert.deepEqual(block.rows[1], { key: "required", kg: 16_714, behind: true });
  assert.deepEqual(block.rows[2], { key: "forecast", kg: 227_100, share: 0.52 });
});

test("метки — по порогам сервера: зелёный «Идём по плану», жёлтый «Близко к темпу»; прогноза ещё нет — «Прогноза пока нет»", () => {
  const level = (level) => planBlock(kpi({ pace: { expected: 0.5, paceKg: 10_000, requiredKg: 9_000, level } }), open);
  assert.deepEqual([level("Good").tone, level("Good").label], ["ok", "Идём по плану"]);
  assert.deepEqual([level("Warning").tone, level("Warning").label], ["warn", "Близко к темпу"]);
  assert.equal(level("Good").rows[1].behind, false); // нужно в день меньше текущего темпа — не предупреждение
  const first = planBlock(kpi({ forecastExecution: null, planForecastKg: null, pace: { expected: 0, paceKg: null, requiredKg: 14_580, level: null } }), open);
  assert.deepEqual([first.tone, first.label, first.bar.forecast, first.bar.expected], ["none", "Прогноза пока нет", null, null]);
});

test("план выполнен: «План выполнен ✓», полоса полная, вместо «нужно в день» — перевыполнение", () => {
  const done = planBlock(kpi({ planFactKg: 459_280, execution: 1.05, pace: { expected: 0.9, paceKg: 17_000, requiredKg: null, level: "Good" } }), open);
  assert.deepEqual([done.kind, done.tone, done.label], ["done", "ok", "План выполнен ✓"]);
  assert.deepEqual(done.bar, { fact: 1, expected: null, forecast: null });
  assert.equal(Math.round(done.over * 100), 5);
  assert.deepEqual(done.rows.map((r) => r.key), ["pace", "over", "forecast"]);
  const exact = planBlock(kpi({ planFactKg: 437_410, execution: 1 }), open);
  assert.deepEqual([exact.kind, exact.over, exact.rows.map((r) => r.key)], ["done", null, ["pace", "forecast"]]);
});

test("плана нет ни в одном источнике: без процента и полосы — факт, темп и прогноз в кг", () => {
  const none = planBlock(kpi({ planKg: null, execution: null, planFactKg: null, planForecastKg: null, forecastExecution: null }), open);
  assert.deepEqual([none.kind, none.tone, none.label, none.bar], ["noPlan", "none", "Плана нет", null]);
  assert.deepEqual(none.rows, [{ key: "pace", kg: 7_570, closed: false }, { key: "forecast", kg: 257_000, share: null }]);
  assert.equal(planNote(kpi({ planKg: null })), "none");
});

test("закрытый месяц: без отметки и прогноза; метка — по выполнению; «не хватило» и итог", () => {
  const closed = { closed: true };
  const short = planBlock(kpi({ planFactKg: 372_260, planKg: 385_259, execution: 0.966, pace: { expected: 1, paceKg: 12_008, requiredKg: null, level: "Good" } }), closed);
  assert.deepEqual([short.tone, short.label, short.bar.expected, short.bar.forecast], ["ok", "Почти выполнен", null, null]);
  assert.deepEqual(short.rows.map((r) => r.key), ["pace", "short", "result"]);
  assert.equal(short.rows[1].kg, 385_259 - 372_260);
  assert.equal(short.rows[0].closed, true);
  const low = planBlock(kpi({ planFactKg: 200_000, planKg: 385_259, execution: 0.52, pace: { expected: 1, paceKg: 6_451, requiredKg: null, level: "Bad" } }), closed);
  assert.deepEqual([low.tone, low.label], ["bad", "План не выполнен"]);
});

test("пометка о плане и «Задать план» — по источнику и правам", () => {
  assert.equal(planNote(kpi()), "linko");
  assert.equal(planNote(kpi({ planSource: "rop" })), null);
  assert.equal(planLink("rop", ["integrations.manage", "ai.data.manage"]), "/settings/data-sources");
  assert.equal(planLink("rop", ["ai.data.manage"]), "/data-manager");
  assert.equal(planLink("factory", ["sales.manage", "integrations.manage"]), "/sales/setup");
  assert.equal(planLink("factory", ["ai.data.manage"]), null);
  assert.equal(planLink("rop", ["sales.read"]), null);
});

test("KPI с целью: полоса «факт + прогноз» к цели, цвет — уровень сервера; у закрытого месяца прогноза нет", () => {
  const perOutlet = targetKpi({ value: 491_000, target: 1_440_000, ratio: 0.58, level: "Bad", forecast: 836_000 }, false);
  assert.equal(perOutlet.tone, "bad");
  assert.deepEqual([Math.round(perOutlet.bar.fact * 100), Math.round(perOutlet.bar.forecast * 100)], [34, 58]);
  const conversion = targetKpi({ value: 0.49, target: 0.7, ratio: 0.7, level: "Bad" }, false);
  assert.deepEqual([Math.round(conversion.bar.fact * 100), conversion.bar.forecast], [70, null]);
  assert.equal(targetKpi({ value: 38.4, target: 125, ratio: 0.78, level: "Warning", forecast: 97.3 }, true).bar.forecast, null);
  assert.equal(targetKpi({ value: 1.4, target: 1, ratio: 1.4, level: "Good" }, false).bar.fact, 1);
  assert.equal(targetKpi({ value: null, target: 0.7, ratio: null, level: null }, false).bar, null);
});

test("визиты без заказа: больше 40% визитов — предупреждение, иначе без статусного цвета", () => {
  const many = visitsNoOrder({ visitsWithoutOrder: 4_603, visitsDone: 9_017 });
  assert.deepEqual([Math.round(many.share * 100), many.tone], [51, "warn"]);
  assert.equal(visitsNoOrder({ visitsWithoutOrder: 400, visitsDone: 1_000 }).tone, "none");
  assert.deepEqual(visitsNoOrder({ visitsWithoutOrder: 0, visitsDone: 0 }), { share: null, tone: "none", bar: null });
});

test("ТП: требуют внимания = критично + риск, в норме — остальные действующие, доли — для полосы", () => {
  const split = agentsSplit(123, { critical: 49, risk: 18 });
  assert.equal(split.attention, 67);
  assert.deepEqual(split.segments.map((s) => [s.key, s.n]), [["critical", 49], ["risk", 18], ["ok", 56]]);
  assert.equal(split.segments.reduce((s, x) => s + x.share, 0), 1);
  assert.equal(split.tone, "bad");
  assert.equal(agentsSplit(10, { critical: 0, risk: 0 }).tone, "ok");
  assert.equal(agentsSplit(0, { critical: 0, risk: 0 }).segments[2].share, 0);
});

test("дни месяца: прошло 7 из 30 — 23%", () => {
  assert.deepEqual(monthDays({ workedDays: 7, daysInMonth: 30 }), { worked: 7, days: 30, share: 7 / 30 });
  assert.equal(monthDays({ workedDays: 31, daysInMonth: 31 }).share, 1);
});

test("в ответе нет темпа (API старее интерфейса): без метки и строки темпа, полоса и прогноз — как есть", () => {
  const old = planBlock(kpi({ pace: undefined }), open);
  assert.deepEqual([old.tone, old.label, old.short, old.bar.expected, old.bar.forecast], ["none", null, null, null, 0.52]);
  assert.deepEqual(old.rows.map((r) => r.key), ["forecast"]);
});
