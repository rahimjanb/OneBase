import { test } from "node:test";
import assert from "node:assert/strict";
import { analytikQuestions, change, changeTone, compact, date, dateTime, kpiValue, num, originLabel, periodQuery, polyline, readinessLabel, severityRank } from "./analytik.ts";

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

test("свежесть данных: подпись статуса (свежие — без подписи) и название источника для кнопки «Обновить»", () => {
  assert.equal(readinessLabel("ok"), null);
  assert.equal(readinessLabel("updating"), "обновляется");
  assert.equal(readinessLabel("stale"), "устарели");
  assert.equal(readinessLabel("error"), "ошибка обмена");
  assert.equal(readinessLabel("not_connected"), "не подключено");
  assert.equal(readinessLabel("missing"), "не заведены");
  assert.equal(originLabel("linko"), "Linko");
  assert.equal(originLabel("verifix"), "Verifix");
  assert.equal(originLabel(null), "OneBase");
  // «Показать октябрь» из баннера нехватки данных — свой период с датами, остальные параметры страницы сохраняются.
  assert.equal(periodQuery("custom", "2026-10-01", "2026-10-31", { tab: "regions" }), "?period=custom&from=2026-10-01&to=2026-10-31&tab=regions");
});

// ---------- «Обзор» по макету ----------
import {
  compactRange,
  dayMonth,
  forecastAccuracy,
  healthFactors,
  healthLead,
  kpiParts,
  niceTicks,
  overviewRecommendations,
  planDays,
  planTone,
  plural,
  purchaseOf,
  rawRequestHref,
  shortDate,
  shortWhen,
  stockTone,
} from "./analytik.ts";

test("показатель раздельно: крупное число и мелкая единица по макету", () => {
  assert.deepEqual(kpiParts(4_690_000_000, "сум"), { value: "4,69", unit: `млрд${nb}сум` });
  assert.deepEqual(kpiParts(124_300, "кг"), { value: "124", unit: `тыс.${nb}кг` });
  assert.deepEqual(kpiParts(24.3, "%"), { value: "24,3", unit: "%" });
  assert.deepEqual(kpiParts(1, "позиций"), { value: "1", unit: "позиция" });
  assert.deepEqual(kpiParts(3, "позиций"), { value: "3", unit: "позиции" });
  assert.deepEqual(kpiParts(960, "случ."), { value: "960", unit: "случ." });
  assert.equal(kpiParts(null, "сум"), null);
  assert.equal(plural(11, "a", "b", "c"), "c");
  assert.equal(plural(22, "a", "b", "c"), "b");
});

test("план месяца: прошло дней по дню данных, тон прогноза — по порогам оценки компании", () => {
  assert.deepEqual(planDays({ year: 2026, month: 11, dataThrough: "2026-11-12" }), { day: 12, days: 30, percent: 40 });
  assert.deepEqual(planDays({ year: 2026, month: 10, dataThrough: "2026-11-02" }), { day: 31, days: 31, percent: 100 });
  assert.equal(planTone(96), "ok");
  assert.equal(planTone(85), "warn");
  assert.equal(planTone(60.8), "bad");
  assert.equal(planTone(null), "muted");
  assert.equal(stockTone(12), "warn");
  assert.equal(stockTone(21), "ok");
});

test("что снижает оценку: штраф, серьёзность, доля полосы и куда ведёт; главное влияние — два самых больших", () => {
  const health = {
    components: [
      { title: "Критические риски", points: -36, reason: "20 активных" },
      { title: "Предупреждения", points: -20, reason: "7 активных" },
      { title: "План продаж", points: -15, reason: "прогноз выполнения 60,8%" },
      { title: "Сырьё", points: -10, reason: "1 позиций в критичном дефиците" },
    ],
  };
  const f = healthFactors(health);
  assert.deepEqual(f.map((x) => [x.penalty, x.severity, x.href]), [
    [36, "danger", "/analytik/risks"],
    [20, "warning", "/analytik/risks"],
    [15, "warning", "/analytik/sales"],
    [10, "warning", "/analytik/procurement"],
  ]);
  assert.equal(f[0].share, 1);
  assert.deepEqual(healthLead(health), ["Критические риски", "Предупреждения"]);
  assert.deepEqual(healthLead({ components: [] }), []);
});

test("прогноз: точность — 100 % минус ошибка, сверенная с фактом важнее отложенной истории", () => {
  assert.equal(forecastAccuracy({ past: { evaluated: 10, wape: 0.13, from: null, to: null }, result: { wape: 0.2 } }), 87);
  assert.equal(forecastAccuracy({ past: null, result: { wape: 0.25 } }), 75);
  assert.equal(forecastAccuracy({ past: null, result: { wape: null } }), null);
  assert.equal(compactRange(10_100_000_000, 18_400_000_000), `10,1 – 18,4${nb}млрд`);
  assert.equal(compactRange(950_000_000, 1_200_000_000), `950${nb}млн – 1,2${nb}млрд`);
  assert.deepEqual(niceTicks(1_234), [0, 400, 800, 1200, 1600]);
  assert.deepEqual(niceTicks(0), [0, 1]);
});

test("даты обзора: «Факт с 1.11», ось «12.11», время находки — сегодня, вчера, дата", () => {
  assert.equal(dayMonth("2026-11-01"), "1.11");
  assert.equal(shortDate("2026-11-12"), "12.11");
  const now = new Date("2026-11-12T16:00:00Z");
  assert.deepEqual(shortWhen("2026-11-12T07:16:00Z", now), { day: "today", text: "12:16" });
  assert.deepEqual(shortWhen("2026-11-11T04:10:00Z", now), { day: "yesterday", text: "09:10" });
  assert.deepEqual(shortWhen("2026-11-08T19:36:00Z", now), { day: null, text: "09.11 00:36" });
  assert.deepEqual(shortWhen("2025-11-09T10:00:00Z", now), { day: null, text: "09.11.2025" });
});

test("закупка: 30 дней, сумма по цене, заявка с позицией, количеством и причиной", () => {
  const item = {
    itemId: "9b2f0c1e-1111-2222-3333-444455556666",
    name: "Тестовый товар",
    unit: "шт",
    orderBy: "2026-11-11",
    unitPrice: 7_600,
    daysOfCover: 2,
    horizons: [
      { days: 7, demand: 0, safetyStock: 0, incoming: 0, required: 300, cost: null },
      { days: 30, demand: 0, safetyStock: 0, incoming: 0, required: 1_200, cost: null },
    ],
  };
  assert.deepEqual(purchaseOf(item), { qty: 1_200, sum: 9_120_000, days: 2 });
  const href = new URL(rawRequestHref(item, "запас закончится 14.11"), "http://x");
  assert.equal(href.pathname, "/requests/new");
  assert.deepEqual(Object.fromEntries(href.searchParams), {
    kind: "raw",
    item: item.itemId,
    name: "Тестовый товар",
    unit: "шт",
    qty: "1200",
    price: "7600",
    by: "2026-11-11",
    reason: "запас закончится 14.11",
  });
});

test("рекомендации: из рисков с рекомендацией, без «к сведению», сырьё — с артикулом для заявки", () => {
  const risk = (id, severity, extra = {}) => ({
    id,
    kind: "risk",
    module: "procurement",
    rule: "supply.stockout",
    severity,
    title: `Риск ${id}`,
    description: "",
    cause: null,
    causeConfirmed: false,
    hypotheses: [],
    impact: 1200,
    impactUnit: "шт",
    consequences: null,
    recommendations: [`Сделать ${id}`],
    evidence: { sku: "RAW-1" },
    href: "/analytik/procurement",
    detectedAt: "",
    lastSeenAt: "",
    resolvedAt: null,
    occurrences: 1,
    source: "analytik",
    ...extra,
  });
  const recs = overviewRecommendations([risk("a", "critical"), risk("b", "info"), risk("c", "warning", { module: "sales", rule: "sales.drop", recommendations: [] }), risk("d", "warning", { module: "sales", rule: "sales.drop" })]);
  assert.deepEqual(recs.map((r) => [r.id, r.title, r.sku]), [
    ["a", "Сделать a", "RAW-1"],
    ["d", "Сделать d", null],
  ]);
  assert.equal(recs[0].impact, `1${nb}200${nb}шт`);
});
