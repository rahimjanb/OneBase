import { test } from "node:test";
import assert from "node:assert/strict";
import {
  aiParagraphs,
  aiSections,
  autoName,
  changedCount,
  completeness,
  explainResult,
  forecastRange,
  groupedParameters,
  isKeyRow,
  kpiMetrics,
  metricRows,
  missingReason,
  paramValue,
  pct,
  periodDays,
  plural,
  shortValue,
  sliderRange,
  targetedChanges,
  valueParts,
  chainTitles,
  decodeDraft,
  defaultPeriod,
  deltaTone,
  encodeDraft,
  inputParameters,
  panelParameters,
  quickPeriods,
  setParameter,
  simDelta,
  simValue,
  targetedParameters,
  valueOf,
} from "./simulator.ts";

const nb = "\u00a0";

const def = (code, extra = {}) => ({ code, title: code, unit: "%", mode: "change", min: -90, max: 300, step: 1, default: 0, node: "in.x", targetKind: null, kinds: ["sales", "combined"], description: "", ...extra });
const catalog = [
  def("sales.demand"),
  def("sales.region", { targetKind: "region" }),
  def("production.capacity", { kinds: ["production", "combined"] }),
  def("input.cash", { mode: "input", unit: "сум", min: -1e12, max: 1e12, kinds: ["sales", "production", "combined"] }),
  def("procurement.supplier", { targetKind: "item_supplier", min: 0, max: 0, unit: "", mode: "value", kinds: ["procurement", "combined"] }),
];

test("панели вида сценария: общие изменения, точечные и исходные данные — отдельно", () => {
  assert.deepEqual(panelParameters(catalog, "sales").map((d) => d.code), ["sales.demand"]);
  assert.deepEqual(panelParameters(catalog, "combined").map((d) => d.code), ["sales.demand", "production.capacity"]);
  assert.deepEqual(targetedParameters(catalog, "sales").map((d) => d.code), ["sales.region"]);
  assert.deepEqual(inputParameters(catalog).map((d) => d.code), ["input.cash"]);
});

test("параметр: ноль снимает изменение, пределы соблюдаются, цель отделяет значения, исходное значение 0 остаётся", () => {
  let list = setParameter([], catalog[0], 25);
  assert.equal(valueOf(list, "sales.demand"), 25);
  list = setParameter(list, catalog[0], 900);
  assert.equal(valueOf(list, "sales.demand"), 300);
  list = setParameter(list, catalog[1], 10, "r1");
  list = setParameter(list, catalog[1], -5, "r2");
  assert.equal(valueOf(list, "sales.region", "r1"), 10);
  assert.equal(valueOf(list, "sales.region", "r2"), -5);
  list = setParameter(list, catalog[0], 0);
  assert.equal(valueOf(list, "sales.demand"), null);
  list = setParameter(list, catalog[3], 0);
  assert.equal(valueOf(list, "input.cash"), 0);
  assert.equal(changedCount(list, catalog), 2);
  list = setParameter(list, catalog[4], 0, "item/s2");
  assert.equal(valueOf(list, "procurement.supplier", "item/s2"), 0);
  assert.equal(setParameter(list, catalog[3], null).some((p) => p.code === "input.cash"), false);
  // Изменение уже заданного параметра не переставляет его (порядок — имя сценария).
  const ordered = setParameter(setParameter(setParameter([], catalog[0], 5), catalog[2], 10), catalog[0], 7);
  assert.deepEqual(ordered.map((p) => `${p.code}=${p.value}`), ["sales.demand=7", "production.capacity=10"]);
});

test("черновик в адресе (ссылка AI-аналитика): туда и обратно, кириллица, мусор — null", () => {
  const draft = { kind: "expansion", horizonDays: 60, parameters: [{ code: "expansion.plan", value: 650, target: null }, { code: "sales.region", value: 10, target: "r1" }], newRegionName: "Наманган", from: "2026-08-01", to: "2026-10-31" };
  assert.deepEqual(decodeDraft(encodeDraft(draft)), draft);
  assert.equal(decodeDraft("not-a-draft!!"), null);
  assert.equal(decodeDraft(undefined), null);
  const odd = decodeDraft(encodeDraft({ kind: "nope", horizonDays: 1.5, parameters: [{ code: 1 }, { code: "sales.demand", value: 5 }], newRegionName: 7, from: "12.10.2026", to: null }));
  assert.equal(odd.kind, "combined");
  assert.equal(odd.horizonDays, 30);
  assert.deepEqual(odd.parameters, [{ code: "sales.demand", value: 5, target: null }]);
  assert.equal(odd.newRegionName, null);
  assert.equal(odd.from, null);
});

test("значения и разница: суммы компактно, кг, проценты; нет значения — прочерк", () => {
  assert.equal(simValue(54_600_000, "сум"), `54,6${nb}млн${nb}сум`);
  assert.equal(simValue(46_200_000, "сум"), `46,2${nb}млн${nb}сум`);
  assert.equal(simValue(2_450_000_000, "сум"), `2,45${nb}млрд${nb}сум`);
  assert.equal(simValue(1820, "кг"), `1${nb}820${nb}кг`);
  assert.equal(simValue(12.25, "кг"), `12,3${nb}кг`);
  assert.equal(simValue(90.28, "%"), `90,3${nb}%`);
  assert.equal(simValue(null, "сум"), "—");
  assert.equal(simDelta(-340, "кг"), `−340${nb}кг`);
  assert.equal(simDelta(0, "кг"), null);
  assert.equal(simDelta(3.7, "%"), `+3,7${nb}п.п.`);
});

test("тон изменения: рост выручки хорошо, рост расходов, дефицита и потребности в деньгах плохо", () => {
  assert.equal(deltaTone("sales.revenue", 10), "ok");
  assert.equal(deltaTone("sales.revenue", -10), "bad");
  assert.equal(deltaTone("finance.cogs", 10), "bad");
  assert.equal(deltaTone("procurement.budget", 10), "bad");
  assert.equal(deltaTone("materials.items_short", 1), "bad");
  assert.equal(deltaTone("finance.financing_need", -5), "ok");
  assert.equal(deltaTone("production.utilization", 20), "muted");
  assert.equal(deltaTone("sales.kg", 0), "muted");
});

test("базовый период по умолчанию — 3 полных месяца; быстрые периоды", () => {
  assert.deepEqual(defaultPeriod(new Date(2026, 10, 12)), { from: "2026-08-01", to: "2026-10-31" });
  assert.deepEqual(defaultPeriod(new Date(2026, 1, 5)), { from: "2025-11-01", to: "2026-01-31" });
  const quick = quickPeriods(new Date(2026, 10, 12));
  assert.deepEqual(quick.find((q) => q.key === "m1"), { key: "m1", label: "Последний месяц", from: "2026-10-01", to: "2026-10-31" });
  assert.deepEqual(quick.find((q) => q.key === "d30"), { key: "d30", label: "30 дней", from: "2026-10-13", to: "2026-11-11" });
});

test("цепочка влияния и AI-текст: названия узлов, **жирный** без HTML", () => {
  assert.deepEqual(chainTitles(["sales.requested", "x"], [{ code: "sales.requested", title: "Запрошенный объём", dependsOn: [] }]), ["Запрошенный объём", "x"]);
  assert.deepEqual(aiParagraphs("**Вывод**: рост.\n\n<b>нет</b>"), [[{ bold: true, text: "Вывод" }, { bold: false, text: ": рост." }], [{ bold: false, text: "<b>нет</b>" }]]);
});

// ---------- редизайн экрана ----------

const full = [
  def("sales.demand"),
  def("sales.price", { min: -50, max: 100, step: 0.5 }),
  def("sales.discount", { unit: "п.п.", min: -30, max: 50 }),
  def("sales.elasticity", { unit: "коэф.", mode: "value", min: -5, max: 0 }),
  def("sales.region", { targetKind: "region" }),
  def("procurement.supplier_terms", { unit: "дн.", min: -180, max: 180, kinds: ["procurement", "finance", "combined"] }),
  def("production.utilization", { mode: "value", min: 10, max: 100, default: 100, kinds: ["production", "combined"] }),
  def("workforce.headcount", { targetKind: "division", kinds: ["workforce", "combined"] }),
  def("workforce.salary", { min: -50, kinds: ["workforce", "combined"] }),
  def("expansion.plan", { unit: "кг/мес", mode: "value", min: 0, max: 1e7, kinds: ["expansion", "combined"] }),
  def("expansion.reference", { targetKind: "region", mode: "value", min: 0, max: 0, unit: "", kinds: ["expansion", "combined"] }),
  def("finance.future", { kinds: ["finance", "combined"] }),
  def("input.cash", { mode: "input", unit: "сум", min: -1e12, max: 1e12, kinds: ["combined"] }),
];

test("группы параметров: смысловые аккордеоны, численность — и общая, и точечная, новый параметр сервера не теряется", () => {
  const groups = groupedParameters(full, "combined");
  assert.deepEqual(groups.map((g) => g.key), ["sales", "finance", "production", "costs", "expansion"]);
  assert.deepEqual(groups[0].params.map((p) => p.code), ["sales.demand", "sales.price", "sales.discount", "sales.elasticity"]);
  assert.deepEqual(groups[1].params.map((p) => p.code), ["procurement.supplier_terms", "finance.future"]);
  assert.deepEqual(groups[3].params.map((p) => p.code), ["workforce.salary", "workforce.headcount"]);
  assert.deepEqual(groups[4].params.map((p) => p.code), ["expansion.plan", "expansion.reference"]);
  assert.deepEqual(targetedChanges(full, "combined").map((p) => p.code), ["sales.region", "workforce.headcount"]);
  assert.deepEqual(groupedParameters(full, "sales").map((g) => g.key), ["sales"]);
});

test("ползунок: ноль в центре (±50 %, ±20 п.п., ±60 дн.), значения — от минимума, суммы и коэффициенты — без ползунка", () => {
  assert.deepEqual(sliderRange(full[0]), { min: -50, max: 50, centered: true });
  assert.deepEqual(sliderRange(full[2]), { min: -20, max: 20, centered: true });
  assert.deepEqual(sliderRange(full[5]), { min: -60, max: 60, centered: true });
  assert.deepEqual(sliderRange(def("x", { min: -30, max: 300 })), { min: -30, max: 30, centered: true });
  assert.deepEqual(sliderRange(full[6]), { min: 10, max: 100, centered: false });
  assert.equal(sliderRange(full[3]), null);
  assert.equal(sliderRange(full[9]), null);
});

test("значение параметра и имя сценария: «Цена +5%, спрос −3%, отсрочка +15 дн.», цели по названию, больше трёх — «и ещё»", () => {
  assert.equal(paramValue(5, full[1]), "+5%");
  assert.equal(paramValue(-3, full[0]), "−3%");
  assert.equal(paramValue(15, full[5]), `+15${nb}дн.`);
  assert.equal(paramValue(1.1, full[2]), `+1,1${nb}п.п.`);
  assert.equal(paramValue(85, full[6]), "85%");
  const t = (s, v) => (v ? s.replace(/\{(\w+)\}/g, (_, k) => String(v[k])) : s);
  const names = (d, id) => (id === "r1" ? "Бекобод" : id);
  const list = [
    { code: "sales.price", value: 5, target: null },
    { code: "sales.demand", value: -3, target: null },
    { code: "input.cash", value: 0, target: null },
    { code: "procurement.supplier_terms", value: 15, target: null },
  ];
  assert.equal(autoName(list, full, t, names), `Цена +5%, спрос −3%, отсрочка +15${nb}дн.`);
  assert.equal(autoName([{ code: "sales.region", value: 10, target: "r1" }], full, t, names), "Бекобод: продажи +10%");
  assert.equal(autoName([...list, { code: "workforce.salary", value: 10, target: null }, { code: "expansion.plan", value: 500, target: null }], full, t, names), `Цена +5%, спрос −3%, отсрочка +15${nb}дн. и ещё 2`);
  assert.equal(autoName([{ code: "input.cash", value: 1, target: null }], full, t, names), "Базовый сценарий");
  assert.equal(autoName([{ code: "expansion.plan", value: 500, target: null }], full, t, names, "Наманган"), "Новый регион «Наманган»");
});

test("форматы результата: проценты с одним знаком, число отдельно от единицы, короткие суммы, склонение", () => {
  assert.equal(pct(1.85), "+1,9%");
  assert.equal(pct(-3), "−3,0%");
  assert.equal(pct(0.01), "0%");
  assert.equal(pct(null), "—");
  assert.deepEqual(valueParts(13_759_614_526, "сум"), { value: "13,76", unit: "млрд сум" });
  assert.deepEqual(valueParts(354_000_000, "сум"), { value: "354,0", unit: "млн сум" });
  assert.deepEqual(valueParts(367_025.7, "кг"), { value: `367${nb}026`, unit: "кг" });
  assert.deepEqual(valueParts(0, "сум"), { value: "0", unit: "сум" });
  assert.equal(shortValue(13_509_685_348, "сум"), `13,51${nb}млрд`);
  assert.equal(shortValue(362_472_513, "сум"), `362,5${nb}млн`);
  assert.equal(plural(1, "параметр", "параметра", "параметров"), "параметр");
  assert.equal(plural(3, "параметр", "параметра", "параметров"), "параметра");
  assert.equal(plural(12, "параметр", "параметра", "параметров"), "параметров");
  assert.equal(plural(22, "параметр", "параметра", "параметров"), "параметра");
});

const metric = (code, group, base, scenario, extra = {}) => ({
  code,
  group,
  title: code,
  unit: "сум",
  node: code,
  base,
  scenario,
  delta: base != null && scenario != null ? scenario - base : null,
  deltaPercent: base && scenario != null ? Math.round(((scenario - base) / base) * 1000) / 10 : null,
  kind: "calculated",
  note: null,
  ...extra,
});
const noCost = "Себестоимость неизвестна: нет техкарт с ценами сырья и не введена себестоимость кг.";
const mockResult = {
  metrics: [
    metric("sales.requested_kg", "sales", 378377, 367025.7, { unit: "кг" }),
    metric("sales.kg", "sales", 378377, 367025.7, { unit: "кг" }),
    metric("sales.revenue", "sales", 13509685348, 13759614526),
    metric("sales.avg_price", "sales", 35704, 37490, { unit: "сум/кг" }),
    metric("sales.lost_revenue", "sales", 0, 0),
    metric("sales.plan_kg", "sales", null, null, { unit: "кг", note: "Планов продаж на месяц в OneBase нет." }),
    metric("sales.plan_execution", "sales", null, null, { unit: "%" }),
    metric("production.required", "production", null, null, { unit: "кг", note: "Ни один товар продаж не связан с действующей техкартой — производство и сырьё от продаж не пересчитываются." }),
    metric("finance.cost_per_kg", "finance", null, null, { note: noCost }),
    metric("finance.cogs", "finance", null, null, { note: noCost }),
    metric("finance.gross_profit", "finance", null, null, { note: noCost }),
    metric("finance.net_cash", "finance", 10874175401, 11055230421),
    metric("finance.min_cash", "finance", 362472513, 354000000),
    metric("procurement.budget", "procurement", 0, 0),
    metric("workforce.headcount", "workforce", 254, 254, { unit: "чел.", kind: "fact" }),
  ],
};

test("строки «Показателей»: одинаковые причины «Нужны данные» объединяются, без своей причины — к предыдущей; свёрнуто — главное и изменённое", () => {
  const groups = metricRows(mockResult.metrics);
  const sales = groups.find((g) => g.group === "sales").rows;
  const plan = sales.find((r) => r.type === "missing");
  assert.deepEqual(plan.metrics.map((m) => m.code), ["sales.plan_kg", "sales.plan_execution"]);
  assert.equal(plan.reason, "планов продаж на месяц в OneBase нет");
  const finance = groups.find((g) => g.group === "finance").rows;
  assert.deepEqual(finance[0].metrics.map((m) => m.code), ["finance.cost_per_kg", "finance.cogs", "finance.gross_profit"]);
  assert.equal(finance[0].reason, "нет техкарт с ценами сырья");
  assert.equal(isKeyRow(sales.find((r) => r.type === "metric" && r.metric.code === "sales.kg")), true);
  assert.equal(isKeyRow({ type: "metric", metric: metric("finance.cash_out", "finance", 0, 0) }), false);
  assert.equal(missingReason({ note: "Средняя зарплата не введена (ФОТ в OneBase нет)." }), "введите среднюю зарплату");
  assert.equal(missingReason({ note: null }), "нет данных");
});

test("KPI: «Всё сразу» — шесть карточек макета; другой вид — сначала его главные показатели", () => {
  assert.deepEqual(kpiMetrics(mockResult, { code: "combined", focus: [] }).map((m) => m.code), ["sales.revenue", "sales.kg", "sales.avg_price", "finance.net_cash", "finance.gross_profit", "procurement.budget"]);
  assert.deepEqual(kpiMetrics(mockResult, { code: "finance", focus: ["finance.gross_profit", "finance.min_cash"] }).map((m) => m.code).slice(0, 3), ["finance.gross_profit", "finance.min_cash", "sales.revenue"]);
});

test("полнота расчёта: без техкарт и цен — «Расчёт неполный» с причинами; всё есть — полный", () => {
  const baseline = {
    sections: ["sales", "production", "materials", "finance"],
    sources: [{ code: "sales", status: "stale" }, { code: "hr", status: "ok" }],
    summary: { products: 155, recipeShare: 0, materials: 7, materialsPriced: 0, capacityKgDay: null },
  };
  const c = completeness(baseline, mockResult, []);
  assert.equal(c.complete, false);
  assert.deepEqual(c.missing, ["себестоимость", "прибыль", "производство"]);
  assert.deepEqual(c.issues.map((i) => i.code), ["recipes", "prices", "sources"]);
  assert.deepEqual(c.issues[0].vars, { p: "0" });
  assert.equal(c.unchecked, true);
  const ok = {
    metrics: mockResult.metrics.map((m) => (m.base == null ? { ...m, base: 1, scenario: 1, delta: 0, deltaPercent: 0 } : m)),
  };
  const done = completeness({ ...baseline, summary: { products: 10, recipeShare: 1, materials: 3, materialsPriced: 3, capacityKgDay: 500 } }, ok, []);
  assert.equal(done.complete, true);
  assert.deepEqual(done.issues, []);
});

test("пояснение к выводу — по числам движка: цена перекрывает объём, минимум денег", () => {
  const lines = explainResult(mockResult);
  assert.deepEqual(lines[0], { text: "Рост цены на {p} перекрывает падение объёма на {v}.", vars: { p: "5,0%", v: "3,0%" } });
  assert.deepEqual(lines[1], { text: "Минимум накопленного потока {d}.", vars: { d: `−8,5${nb}млн${nb}сум` } });
  assert.deepEqual(explainResult({ metrics: [metric("sales.revenue", "sales", 1, 1)] }), []);
});

test("периоды: прогноз со следующего дня после даты данных, дни базы включительно", () => {
  assert.deepEqual(forecastRange("2026-11-12", 30), { from: "2026-11-13", to: "2026-12-12" });
  assert.equal(periodDays("2026-08-01", "2026-10-31"), 92);
});

test("AI-анализ по разделам: **заголовок** открывает раздел, маркеры списка снимаются", () => {
  const s = aiSections("Вступление.\n**Основной вывод** — выручка +1,9 %.\n**Риски**:\n- спрос может упасть;\n- **цена** выше рынка.");
  assert.deepEqual(s.map((x) => x.title), [null, "Основной вывод", "Риски"]);
  assert.deepEqual(s[1].paragraphs, [[{ bold: false, text: "выручка +1,9 %." }]]);
  assert.deepEqual(s[2].paragraphs, [[{ bold: false, text: "спрос может упасть;" }], [{ bold: true, text: "цена" }, { bold: false, text: " выше рынка." }]]);
});
