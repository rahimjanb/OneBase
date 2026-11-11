import { test } from "node:test";
import assert from "node:assert/strict";
import { analytikQuestions, change, changeTone, compact, date, dateTime, kpiValue, num, periodQuery, polyline, severityRank } from "./analytik.ts";

const nb = " ";

test("числа: разряды, минус, дробная часть без лишних нулей, нет данных — прочерк", () => {
  assert.equal(num(1234567), `1${nb}234${nb}567`);
  assert.equal(num(-1500.5, 1), `−1${nb}500,5`);
  assert.equal(num(12.0, 2), "12");
  assert.equal(num(null), "—");
  assert.equal(num(Number.NaN), "—");
});

test("крупные суммы — млрд, млн, тыс.", () => {
  assert.equal(compact(13_488_246_629), `13,5${nb}млрд`);
  assert.equal(compact(845_000_000), `845${nb}млн`);
  assert.equal(compact(2_500_000), `2,5${nb}млн`);
  assert.equal(compact(12_345), `12${nb}тыс.`);
  assert.equal(compact(950), "950");
});

test("показатель с единицей: проценты, сумы, кг; нет значения — прочерк, а не ноль", () => {
  assert.equal(kpiValue(74.9, "%"), `74,9${nb}%`);
  assert.equal(kpiValue(4_089_796_504, "сум"), `4,09${nb}млрд${nb}сум`);
  assert.equal(kpiValue(111_845.5, "кг"), `112${nb}тыс.${nb}кг`);
  assert.equal(kpiValue(null, "сум"), "—");
  assert.equal(kpiValue(0, "шт"), `0${nb}шт`);
});

test("изменение и его тон: рост продаж хорошо, рост опозданий и возвратов плохо", () => {
  assert.equal(change(12.34), `+12${nb}%`);
  assert.equal(change(-4.5), `−4,5${nb}%`);
  assert.equal(change(null), null);
  assert.equal(changeTone("sales.revenue", 5), "ok");
  assert.equal(changeTone("sales.revenue", -5), "bad");
  assert.equal(changeTone("hr.late", 20), "bad");
  assert.equal(changeTone("sales.returns", -10), "ok");
  assert.equal(changeTone("sales.kg", 0), "muted");
});

test("даты: день и время по Ташкенту", () => {
  assert.equal(date("2026-11-11"), "11.11.2026");
  assert.equal(dateTime("2026-11-11T11:30:00Z"), "11.11.2026 16:30");
  assert.equal(date(null), "—");
});

test("строка периода: свой период с датами, прочие параметры", () => {
  assert.equal(periodQuery("month"), "?period=month");
  assert.equal(periodQuery("custom", "2026-10-01", "2026-10-31"), "?period=custom&from=2026-10-01&to=2026-10-31");
  assert.equal(periodQuery("week", "2026-10-01", undefined, { module: "sales", empty: undefined }), "?period=week&module=sales");
});

test("ломаная графика: края с отступом, одинаковые значения — середина не ломается", () => {
  assert.equal(polyline([0, 10], 100, 50, 0, 10, 0), "0.0,50.0 100.0,0.0");
  assert.equal(polyline([5, 5], 100, 50, 5, 5, 0), "0.0,50.0 100.0,50.0");
  assert.equal(polyline([], 100, 50, 0, 1), "");
});

test("порядок критичности и быстрые вопросы по правам", () => {
  assert.deepEqual(["info", "critical", "warning"].sort((a, b) => severityRank(a) - severityRank(b)), ["critical", "warning", "info"]);
  const hrOnly = analytikQuestions(["analytik.use", "hr.read"]).map((q) => q.text);
  assert.ok(!hrOnly.some((q) => q.includes("продажи")));
  assert.ok(hrOnly.some((q) => q.includes("всю компанию")));
  assert.ok(analytikQuestions(["analytik.use", "procurement.read"]).some((q) => q.text.includes("закупить")));
});
