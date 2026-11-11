// «Отделы» (lib/departments-view.ts): node --test src/lib/departments-view.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import { barAriaLabel, cardAriaLabel, departmentList, hrCard, monthOptions, monthRange, noDataCard, parseMonth, salesCard, supplyCard } from "./departments-view.ts";

const t = (s, v) => (v ? s.replace(/\{(\w+)\}/g, (m, k) => (k in v ? String(v[k]) : m)) : s);
const names = ["январь", "февраль", "март", "апрель", "май", "июнь", "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь"];
const ctx = (current = true, month = "ноябрь 2026") => ({ t, month, current, monthLabel: (y, m) => `${names[m - 1]} ${y}` });
const ok = (data) => ({ state: "ok", data });

test("отделы — в прежнем порядке, без сортировки по статусу", () => {
  assert.deepEqual(departmentList.map((d) => d.code), ["production", "finance", "sales", "marketing", "supply", "hr"]);
});

test("месяц из адреса: неверный и будущий — текущий; прошлые — как есть", () => {
  assert.deepEqual(parseMonth(null, "2026-11-07"), { key: "2026-11", year: 2026, month: 11, current: true });
  assert.deepEqual(parseMonth("2026-10", "2026-11-07"), { key: "2026-10", year: 2026, month: 10, current: false });
  assert.equal(parseMonth("2026-12", "2026-11-07").key, "2026-11");
  assert.equal(parseMonth("2026-13", "2026-11-07").key, "2026-11");
  assert.equal(parseMonth("ноябрь", "2026-11-07").key, "2026-11");
  assert.deepEqual(monthOptions("2026-01-15", 3), ["2026-01", "2025-12", "2025-11"]);
  assert.deepEqual(monthRange(parseMonth(null, "2026-11-07"), "2026-11-07"), { from: "2026-11-01", to: "2026-11-07" });
  assert.deepEqual(monthRange(parseMonth("2028-02", "2028-05-01"), "2028-05-01"), { from: "2028-02-01", to: "2028-02-29" });
});

test("продажи: крупно — прогноз, полоса — факт и прогноз с легендой (нет расхождения «52% крупно / 14% полоса»)", () => {
  const card = salesCard(ok({ period: { year: 2026, month: 11, closed: false }, kpi: { planKg: 437_400, factKg: 62_300, execution: 0.1424, forecastExecution: 0.52 } }), ctx());
  assert.equal(card.value, 52);
  assert.equal(card.status, "attention");
  assert.deepEqual(card.bar, { value: 14, forecast: 52 });
  assert.deepEqual(card.legend, { fact: "Факт 62,3 из 437,4 т · 14%", forecast: "Прогноз 52%", short: "Факт 62,3 из 437,4 т (14%) · прогноз 52%" });
  assert.equal(card.period, "ноябрь 2026");
  assert.equal(card.label, "Прогноз выполнения плана");
  assert.equal(card.reason, "Цель — прогноз от 100% плана, сейчас 52%");
  assert.equal(barAriaLabel(card, t), "Продажи: факт 14%, прогноз 52%");
  assert.equal(cardAriaLabel(card, "Требует внимания"), "Продажи, Прогноз выполнения плана 52%, Требует внимания");
});

test("продажи: статусы по прежним правилам; закрытый месяц — выполнение без прогноза; плана нет — «Требует внимания»", () => {
  const sale = (execution, forecastExecution, closed = false) =>
    salesCard(ok({ period: { year: 2026, month: 10, closed }, kpi: { planKg: 100_000, factKg: execution * 100_000, execution, forecastExecution } }), ctx());
  assert.equal(sale(1.03, 1.2).status, "done");
  assert.equal(sale(0.6, 1.05).status, "ok");
  const closed = sale(0.94, 0.97, true);
  assert.deepEqual([closed.value, closed.label, closed.bar.forecast, closed.legend.forecast], [94, "Выполнение плана", null, null]);
  assert.equal(closed.legend.short, "Факт 94 из 100 т (94%)");
  const noPlan = salesCard(ok({ period: { year: 2026, month: 11, closed: false }, kpi: { planKg: null, factKg: 12_000, execution: null, forecastExecution: null } }), ctx());
  assert.deepEqual([noPlan.status, noPlan.value, noPlan.bar, noPlan.detail.text], ["attention", null, null, "Плана на ноябрь 2026 нет · факт 12 т"]);
});

test("показатели закрыты правами — карточка без числа и без слов о доступе, ошибка ответа — «Не удалось загрузить»", () => {
  const denied = salesCard({ state: "forbidden" }, ctx());
  assert.deepEqual([denied.status, denied.value, denied.detail, denied.bar], ["none", null, null, null]);
  assert.equal(salesCard({ state: "error" }, ctx()).status, "error");
  assert.equal(hrCard({ state: "error" }, ctx()).status, "error");
});

test("HR: явка за месяц, норма 80%; «Сегодня отметок пока нет» вместо 0%; прошлый месяц — без «сегодня»", () => {
  const morning = hrCard(ok({ monthRate: 82, presentPct: 0 }), ctx());
  assert.deepEqual([morning.value, morning.status, morning.bar.value, morning.detail.text], [82, "ok", 82, "Сегодня отметок пока нет"]);
  assert.equal(hrCard(ok({ monthRate: 82, presentPct: 91.3 }), ctx()).detail.text, "Явка сегодня: 91,3%");
  const past = hrCard(ok({ monthRate: 76, presentPct: 95 }), ctx(false, "октябрь 2026"));
  assert.deepEqual([past.status, past.label, past.detail, past.reason], ["attention", "Явка за месяц", null, "Норма ≥ 80%, сейчас 76%"]);
  const todayOnly = hrCard(ok({ monthRate: null, presentPct: 85 }), ctx());
  assert.deepEqual([todayOnly.value, todayOnly.label, todayOnly.period], [85, "Явка сегодня", null]);
  assert.equal(hrCard(ok({ monthRate: null, presentPct: null }), ctx()).detail.text, "Нет данных за ноябрь 2026");
});

test("снабжение: на сегодня; за прошлый месяц — «только на сегодня»; заявки не видны — карточка без числа", () => {
  const clean = supplyCard(ok({ requestsAwaitingOrder: 0, overdueToBuy: 0, onApprovalCount: 0 }), ctx());
  assert.deepEqual([clean.value, clean.status, clean.label, clean.detail.text], [100, "ok", "Согласованное закуплено", "К закупке 0 · просрочено 0"]);
  const late = supplyCard(ok({ requestsAwaitingOrder: 4, overdueToBuy: 1, onApprovalCount: 2 }), ctx());
  assert.deepEqual([late.value, late.status, late.detail.text], [75, "attention", "К закупке 4 · просрочено 1 · на согласовании 2"]);
  assert.equal(supplyCard(ok({ requestsAwaitingOrder: 4 }), ctx(false)).value, null);
  assert.match(supplyCard({ state: "ok", data: {} }, ctx(false)).detail.text, /только на сегодня/);
  const hidden = supplyCard(ok({ requestsAwaitingOrder: null }), ctx());
  assert.deepEqual([hidden.value, hidden.detail], [null, null]);
});

test("производство, финансы, маркетинг — «Нет данных за месяц», без чисел", () => {
  for (const code of ["production", "finance", "marketing"]) {
    const card = noDataCard(code, ctx());
    assert.deepEqual([card.status, card.value, card.bar, card.detail.text], ["none", null, null, "Нет данных за ноябрь 2026"]);
  }
  assert.equal(noDataCard("production", ctx()).label, "Выпуск продукции");
});
