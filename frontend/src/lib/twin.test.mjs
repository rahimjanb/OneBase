// Цифровой двойник (lib/twin.ts): node --test src/lib/twin.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import { cardOf, enterpriseKpis, signalOf, tasksTone, worst } from "./twin.ts";

const t = (text, vars) => (vars ? text.replace(/\{(\w+)\}/g, (_, k) => String(vars[k])) : text);
const ok = (data) => ({ state: "ok", data });
const no = (state = "noaccess") => ({ state, data: null });

const dept = (code, over = {}) => ({
  id: `id-${code}`,
  code,
  name: `Отдел ${code}`,
  tasks: ok({ open: 10, overdue: 0, review: 1, blocked: 0 }),
  hr: no("nodata"),
  supply: ok({ open: 2, approval: 1, urgent: 0, revision: 0, raw: 1, overdue: 0, purchaseTotal: 1000 }),
  spend: ok({ total: 2_500_000, lines: 3 }),
  users: null,
  ...over,
});

const snapshot = (over = {}) => ({
  generatedAt: "2026-11-04T10:00:00Z",
  canEdit: false,
  objects: [],
  departments: [dept("production"), dept("sales"), dept("finance"), dept("marketing"), dept("supply"), dept("hr")],
  sales: ok({ year: 2026, month: 11, closed: false, dataThrough: "2026-11-03", factKg: 120000, planKg: 1000000, execution: 0.12, forecastExecution: 0.95,
    level: "Good", levelBasis: "forecast", revenue: 1_500_000_000, akb: 4200, critical: 2, risk: 3, vacancies: 1 }),
  primary: ok({ year: 2026, month: 11, kg: 250000, sum: 9e9, planKg: null, execution: null, forecastExecution: null, hasPlan: false, asOf: "2026-11-03" }),
  hr: no(),
  tasks: ok({ open: 40, overdue: 3, review: 2, blocked: 1, dueThisWeek: 5, doneThisMonth: 7 }),
  supply: ok({ readsAll: true, open: 12, approval: 4, urgent: 1, revision: 0, raw: 3, overdueToBuy: 0, spendMonth: 8e6, spendFrom: "2026-11-01", spendTo: "2026-11-04" }),
  stock: ok({ warehouseId: "wh-1", warehouseName: "Центральный склад", items: 120, low: 4, empty: 0, withReserve: 2, toIssue: 1, rawLow: 2 }),
  plant: no(),
  finance: no(),
  promo: no("nodata"),
  alerts: ok({ items: [{ id: "a1", category: "sales", severity: "Critical", title: "Падение продаж в регионе", href: "/sales/regions" }] }),
  ...over,
});

const building = (links) => ({ key: "A", name: "Цех 1", defaultName: "Цех 1", links, updatedAt: null });
const link = (d) => ({ kind: "department", id: d, code: null, name: d });

test("worst и порог просрочек задач", () => {
  assert.equal(worst(["ok", "info", "warn"]), "warn");
  assert.equal(worst(["nodata", "info"]), "info");
  assert.equal(worst([]), "off");
  assert.equal(tasksTone({ open: 10, overdue: 0, review: 0, blocked: 0 }), "ok");
  assert.equal(tasksTone({ open: 10, overdue: 2, review: 0, blocked: 0 }), "warn");
  assert.equal(tasksTone({ open: 8, overdue: 2, review: 0, blocked: 0 }), "bad");
});

test("здание без привязок — «Не привязано», вне слоя — off", () => {
  const s = snapshot();
  assert.deepEqual(signalOf(building([]), s, "all", t), { tone: "off", value: "Не привязано", items: [] });
  assert.equal(signalOf(building([link("id-production")]), s, "sales", t).tone, "off");
});

test("производство: выпуска нет — серый «Нет данных», а не цифры", () => {
  const sig = signalOf(building([link("id-production")]), snapshot(), "production", t);
  assert.equal(sig.tone, "nodata");
  assert.equal(sig.value, "Нет данных о выпуске");
});

test("продажи: в идущем месяце цвет — по прогнозу с сервера", () => {
  const sig = signalOf(building([link("id-sales")]), snapshot(), "sales", t);
  assert.equal(sig.tone, "ok");
  assert.match(sig.value, /^Прогноз 95%/);
  const noPlan = snapshot({ sales: ok({ ...snapshot().sales.data, planKg: null, execution: null, forecastExecution: null, level: null }) });
  assert.equal(signalOf(building([link("id-sales")]), noPlan, "sales", t).tone, "info");
});

test("склад: ниже минимума — жёлтый, нет в наличии — красный, чужой склад — нет данных", () => {
  const wh = { kind: "warehouse", id: "wh-1", code: null, name: "Центральный склад" };
  assert.equal(signalOf(building([wh]), snapshot(), "warehouse", t).tone, "warn");
  const empty = snapshot({ stock: ok({ ...snapshot().stock.data, empty: 1 }) });
  assert.equal(signalOf(building([wh]), empty, "warehouse", t).tone, "bad");
  assert.equal(signalOf(building([{ ...wh, id: "wh-2" }]), snapshot(), "warehouse", t).tone, "nodata");
});

test("«Все» — худший сигнал по слоям; нет доступа — серый с подписью", () => {
  const s = snapshot({ departments: [dept("production", { tasks: ok({ open: 4, overdue: 2, review: 0, blocked: 0 }) })] });
  const sig = signalOf(building([link("id-production")]), s, "all", t);
  assert.equal(sig.tone, "bad");
  assert.equal(sig.value, "Просрочено: 2");
  const hidden = snapshot({ departments: [dept("production", { tasks: no() })] });
  assert.deepEqual(signalOf(building([link("id-production")]), hidden, "tasks", t).items[0], { tone: "nodata", value: "Нет доступа", source: "Отдел production" });
});

test("KPI: выпуск всегда «Нет данных», без права — плитки нет", () => {
  const keys = enterpriseKpis(snapshot(), t).map((k) => k.key);
  assert.ok(keys.includes("output"));
  assert.ok(!keys.includes("hr"));
  assert.ok(!keys.includes("finance"));
  const output = enterpriseKpis(snapshot(), t).find((k) => k.key === "output");
  assert.equal(output.value, "Нет данных");
  const tasks = enterpriseKpis(snapshot(), t).find((k) => k.key === "tasks");
  assert.equal(tasks.value, "40");
  assert.equal(tasks.sub, "просрочено 3");
});

test("карточка производства: план и выпуск — «Нет данных», первичка подписана как отгрузки, бюджет — нет данных", () => {
  const card = cardOf(building([link("id-production")]), snapshot(), t, { departmentHref: (c) => `/tasks/${c}` });
  const production = card.sections.find((s) => s.key === "domain-production");
  assert.deepEqual(production.metrics.slice(0, 4).map((m) => m.state), ["nodata", "nodata", "nodata", "nodata"]);
  assert.match(production.metrics[4].hint, /не выпуск/);
  const own = card.sections.find((s) => s.kind === "department");
  assert.equal(own.href, "/tasks/production");
  assert.equal(own.metrics.find((m) => m.label === "Бюджет подразделения").value, "Нет данных");
  assert.equal(own.metrics.find((m) => m.label === "Сотрудники").hint, "подразделения Verifix не привязаны к отделу");
});

test("проблемы: просрочки, находки AI отдела; находки чужих отделов не попадают", () => {
  const s = snapshot({ departments: [dept("sales", { tasks: ok({ open: 10, overdue: 1, review: 0, blocked: 0 }) }), dept("hr")] });
  const sales = cardOf(building([link("id-sales")]), s, t, { departmentHref: () => "/sales" }).problems;
  assert.deepEqual(sales.map((p) => p.text), ["Падение продаж в регионе", "Просроченных задач: 1", "Агентов в критической зоне: 2"]);
  const hr = cardOf(building([link("id-hr")]), s, t, { departmentHref: () => "/hr" }).problems;
  assert.equal(hr.length, 0);
});

test("находки AI: по отделу не больше трёх, остальные — одной строкой со ссылкой на AI Dashboard", () => {
  const items = Array.from({ length: 5 }, (_, i) => ({ id: `a${i}`, category: "sales", severity: "Warning", title: `Находка ${i}`, href: null }));
  const s = snapshot({ alerts: ok({ items }), departments: [dept("sales")], sales: no() });
  const problems = cardOf(building([link("id-sales")]), s, t, { departmentHref: () => "/sales" }).problems;
  assert.deepEqual(problems.map((p) => p.text), ["Находка 0", "Находка 1", "Находка 2", "Ещё находок AI: 2"]);
  assert.equal(problems[3].href, "/ai");
});
