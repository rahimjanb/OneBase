// Период обзора «Первички» (lib/primary-period.ts): node --test src/lib/primary-period.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import { choiceQuery, choose, detailMonth, periodWords, quarterOf, rangeLabel, readChoice } from "./primary-period.ts";

const months = ["январь", "февраль", "март", "апрель", "май", "июнь", "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь"];
const shorts = ["Янв", "Фев", "Мар", "Апр", "Май", "Июн", "Июл", "Авг", "Сен", "Окт", "Ноя", "Дек"];
const n = { month: (m) => months[m - 1], short: (m) => shorts[m - 1], t: (s, v) => (v ? s.replace(/\{(\w+)\}/g, (x, k) => String(v[k] ?? x)) : s) };

test("адрес: период, год, месяц, квартал, единицы; лишнее и неверное отбрасывается", () => {
  assert.deepEqual(readChoice({}), { period: "ytd", year: undefined, month: undefined, quarter: undefined, unit: "t" });
  assert.deepEqual(readChoice({ period: "month", year: "2026", month: "11", unit: "sum" }), { period: "month", year: 2026, month: 11, quarter: undefined, unit: "sum" });
  assert.equal(readChoice({ period: "week", month: "13", quarter: "5" }).period, "ytd");
  assert.equal(readChoice({ month: "13" }).month, undefined);
  assert.equal(choiceQuery({ period: "ytd", year: 2026, month: 11, unit: "t" }), "period=ytd&year=2026");
  assert.equal(choiceQuery({ period: "quarter", year: 2026, quarter: 4, month: 11, unit: "sum" }), "period=quarter&year=2026&quarter=4&unit=sum");
  assert.equal(choiceQuery({ period: "month", year: 2026, month: 3, unit: "sum" }, false), "period=month&year=2026&month=3");
  assert.deepEqual(choose({ period: "ytd", year: 2026, unit: "sum" }, { period: "month", month: 5 }), { period: "month", year: 2026, month: 5, unit: "sum" });
  assert.equal(quarterOf(11), 4);
  assert.equal(quarterOf(1), 1);
});

test("период словами и кнопка уточнения", () => {
  assert.equal(periodWords({ type: "ytd", year: 2026, month: null, quarter: null }, n), "2026 с начала года");
  assert.equal(periodWords({ type: "month", year: 2026, month: 11, quarter: null }, n), "ноябрь 2026");
  assert.equal(periodWords({ type: "quarter", year: 2026, month: null, quarter: 4 }, n), "IV квартал 2026");
  assert.equal(rangeLabel({ period: "ytd", year: 2026 }, "2026-11-05", n), "2026 · янв – 5 ноя");
  assert.equal(rangeLabel({ period: "ytd", year: 2025 }, "2026-11-05", n), "2025 · янв – дек");
  assert.equal(rangeLabel({ period: "quarter", year: 2026, quarter: 4 }, "2026-11-05", n), "IV квартал 2026 · окт – 5 ноя");
  assert.equal(rangeLabel({ period: "month", year: 2026, month: 11 }, "2026-11-05", n), "Ноябрь 2026 · 1–5 ноя");
  assert.equal(rangeLabel({ period: "month", year: 2026, month: 10 }, "2026-11-05", n), "Октябрь 2026 · 1–31 окт");
  assert.equal(rangeLabel({ period: "month", year: 2026, month: 12 }, "2026-11-05", n), "Декабрь 2026");
});

test("месяц детальной страницы: у месяца — он сам, у квартала и года — последний месяц с данными", () => {
  assert.deepEqual(detailMonth({ type: "month", year: 2026, month: 3, to: "2026-03-31", through: "2026-03-31" }), { year: 2026, month: 3 });
  assert.deepEqual(detailMonth({ type: "ytd", year: 2026, month: null, to: "2026-12-31", through: "2026-11-05" }), { year: 2026, month: 11 });
  assert.deepEqual(detailMonth({ type: "quarter", year: 2026, month: null, to: "2026-06-30", through: "2026-06-30" }), { year: 2026, month: 6 });
  assert.deepEqual(detailMonth({ type: "ytd", year: 2025, month: null, to: "2025-12-31", through: "2025-12-31" }), { year: 2025, month: 12 });
});
