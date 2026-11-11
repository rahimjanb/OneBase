// «Производство → Технолог» (lib/technologist-view.ts): node --test src/lib/technologist-view.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import {
  axisTicks, barsTotal, consumptionRatio, consumptionTone, completionTone, counters, defectTone, issueTone, kg, parsePeriod, pct, periodOfRange, periodRange,
  quickLinks, reportHref, share, shortDay,
} from "./technologist-view.ts";

const today = "2026-11-10";

test("период в адресе: известные значения, даты и обратное соответствие", () => {
  assert.equal(parsePeriod("3_months"), "3_months");
  assert.equal(parsePeriod("quarter"), null);
  assert.equal(parsePeriod(undefined), null);
  assert.deepEqual(periodRange("this_month", today), { from: "2026-11-01", to: "2026-11-10" });
  assert.deepEqual(periodRange("last_month", today), { from: "2026-10-01", to: "2026-10-31" });
  assert.deepEqual(periodRange("3_months", today), { from: "2026-09-01", to: "2026-11-10" });
  assert.equal(periodOfRange("2026-10-01", "2026-10-31", today), "last_month");
  assert.equal(periodOfRange("2026-10-05", "2026-10-31", today), null);
});

test("форматы: килограммы с пробелом, проценты с одним знаком, нет данных — null", () => {
  assert.equal(kg(1250), "1 250");
  assert.equal(kg(4820.46), "4 820,5");
  assert.equal(pct(96.4), "96,4%");
  assert.equal(pct(92), "92,0%");
  assert.equal(pct(null), null);
  assert.equal(consumptionRatio(1.2), 101.2);
  assert.equal(consumptionRatio(-3.45), 96.6);
  assert.equal(consumptionRatio(null), null);
});

test("цвета значений: план ниже 90 %, перерасход больше 5 %, брак", () => {
  assert.equal(completionTone(89.9), "warn");
  assert.equal(completionTone(96.4), null);
  assert.equal(completionTone(null), null);
  assert.equal(consumptionTone(5.1), "bad");
  assert.equal(consumptionTone(5), null);
  assert.equal(defectTone(0), null);
  assert.equal(defectTone(0.4), "bad");
});

test("кнопки быстрого перехода: счётчики, жёлтая подпись, когда есть что утверждать", () => {
  const zero = quickLinks({ activeOrders: 0, openShifts: 0, closedShifts: 0, pendingRecipes: 0 });
  assert.deepEqual(zero.map((q) => q.href), ["/production/recipes", "/production/shifts", "/production/reports"]);
  assert.deepEqual(zero[0].caption, { key: "на утверждении: {n}", vars: { n: 0 }, warn: false });
  assert.equal(zero[1].caption.key, "открытых: {n}");
  assert.deepEqual(zero[1].mobileCaption.vars, { n: 0, m: 0 });
  const busy = quickLinks({ activeOrders: 2, openShifts: 3, closedShifts: 1, pendingRecipes: 2 });
  assert.equal(busy[0].caption.warn, true);
  assert.deepEqual(busy[1].caption, { key: "открытых: {n} · ждут утверждения: {m}", vars: { n: 3, m: 1 }, warn: true });
  assert.equal(busy[2].caption.warn, false);
  assert.equal(quickLinks(null)[0].caption.vars.n, 0);
});

test("счётчики: ссылка только при N > 0, в отфильтрованный список", () => {
  const list = counters({ activeOrders: 0, openShifts: 4, closedShifts: 2, pendingRecipes: 1 });
  assert.deepEqual(list.map((c) => c.href), [null, "/production/shifts", "/production/shifts?status=Closed", "/production/recipes?status=pending"]);
});

test("разделы: итог в кг без штучных строк, доля для полосы, отчёт за период, тон внимания", () => {
  assert.deepEqual(barsTotal([{ value: 1200, unit: null }, { value: 3620.5, unit: "кг" }, { value: 40, unit: "шт" }]), { count: 3, kg: 4820.5 });
  assert.equal(share(50, 200), 25);
  assert.equal(share(1, 1000), 2);
  assert.equal(share(5, 0), 0);
  assert.equal(reportHref("defects", "2026-11-01", "2026-11-10"), "/production/reports?kind=defects&from=2026-11-01&to=2026-11-10");
  assert.equal(issueTone("approval"), "warn");
  assert.equal(issueTone("shortage"), "warn");
  assert.equal(issueTone("overconsumption"), "bad");
  assert.equal(issueTone("defect"), "bad");
});

test("ось графика: 0, середина, верх не ниже максимума", () => {
  assert.deepEqual(axisTicks(0), [0]);
  assert.deepEqual(axisTicks(1830), [0, 1000, 2000]);
  assert.deepEqual(axisTicks(410), [0, 250, 500]);
  assert.deepEqual(axisTicks(90), [0, 50, 100]);
  assert.equal(shortDay("2026-11-07"), "07.11");
});
