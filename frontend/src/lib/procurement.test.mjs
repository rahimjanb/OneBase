// Раздел «Заявки» (lib/procurement.ts): node --test src/lib/procurement.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import { activeWarehouses, batchExpiry, batchOptionLabel, batchesTotal, checkDraft, compactMoney, costToBase, dateRu, draftTotal, kindAccepts, lineSum, money, operationWarehouse, orderTotals, parseNumber, periodOf, purchaseTotal, quantity, ratingText, rawRouteProblem, receiptLine, reversible, reportCell, split, stageIndex, stockCover, unitToBase } from "./procurement.ts";

const item = (over = {}) => ({ key: "k", id: null, name: "Бумага А4", categoryId: "c1", unitCode: "пачка", quantity: "20", price: "45000", comment: "", ...over });

test("money и quantity — пробелы по разрядам, без лишних нулей", () => {
  assert.equal(money(1250000), "1 250 000");
  assert.equal(money(12000.5), "12 000,5");
  assert.equal(money(0), "0");
  assert.equal(money(null), "—");
  assert.equal(quantity(20), "20");
  assert.equal(quantity(2.5), "2,5");
  assert.equal(quantity(1500.125), "1 500,125");
});

test("parseNumber понимает запятую, точку и пробелы", () => {
  assert.equal(parseNumber("1 250 000"), 1250000);
  assert.equal(parseNumber("2,5"), 2.5);
  assert.equal(parseNumber("2.5"), 2.5);
  assert.equal(parseNumber(""), null);
  assert.equal(parseNumber("abc"), null);
});

test("сумма позиции и заявки — только по позициям с ценой", () => {
  assert.equal(lineSum(item()), 900000);
  assert.equal(lineSum(item({ price: "" })), null);
  assert.equal(draftTotal([item(), item({ quantity: "5", price: "300 000" }), item({ price: "" })]), 2400000);
});

test("checkDraft — то же, что проверит сервер", () => {
  const ok = { title: "Канцелярия", reason: "Закончилась бумага", items: [item()] };
  assert.equal(checkDraft(ok, true), null);
  assert.match(checkDraft({ ...ok, title: " " }, false), /название/);
  assert.match(checkDraft({ ...ok, items: [item({ name: "", quantity: "", price: "" })] }, false), /хотя бы одну позицию/);
  assert.match(checkDraft({ ...ok, items: [item({ quantity: "0" })] }, false), /Позиция 1: количество/);
  assert.match(checkDraft({ ...ok, items: [item({ quantity: "1,2345" })] }, false), /трёх знаков/);
  assert.match(checkDraft({ ...ok, items: [item({ price: "-5" })] }, false), /цена/);
  assert.match(checkDraft({ ...ok, items: [item({ categoryId: "" })] }, false), /категорию/);
  assert.match(checkDraft({ ...ok, reason: "" }, true), /причину/);
  assert.equal(checkDraft({ ...ok, reason: "" }, false), null); // черновик — без причины можно
  // Цена у части к закупке обязательна при отправке (черновик — без неё); всё со склада — цена не нужна.
  assert.match(checkDraft({ ...ok, items: [item({ price: "" })] }, true), /Позиция 1: укажите примерную цену/);
  assert.equal(checkDraft({ ...ok, items: [item({ price: "" })] }, false), null);
  assert.equal(checkDraft({ ...ok, items: [item({ price: "", itemId: "i1", available: 100, fromStock: "20", quantity: "20" })] }, true), null);
  // Пустая строка позиции в конце формы не мешает.
  assert.equal(checkDraft({ ...ok, items: [item(), item({ name: "", quantity: "", price: "" })] }, true), null);
});

test("этапы заявки: любой шаг маршрута и доработка — «Согласование», отмена — вне полосы", () => {
  assert.equal(stageIndex("Draft"), 0);
  assert.equal(stageIndex("RevisionRequired"), 1);
  assert.equal(stageIndex("HeadApproval"), 1);
  assert.equal(stageIndex("AnalystApproval"), 1);
  assert.equal(stageIndex("FinanceApproval"), 1);
  assert.equal(stageIndex("DirectorApproval"), 1);
  assert.equal(stageIndex("PartiallyFulfilled"), 2);
  assert.equal(stageIndex("Fulfilled"), 3);
  assert.equal(stageIndex("Cancelled"), -1);
  assert.equal(stageIndex("Rejected"), -1);
  assert.equal(dateRu("2026-11-15"), "15.11.2026");
});

test("склад в позиции: сколько можно взять, со склада и к закупке (сценарий ТЗ: 100 запрошено, 40 доступно)", () => {
  const paper = item({ quantity: "100", price: "45000", itemId: "i1", available: 40, fromStock: "40" });
  assert.equal(stockCover(paper), 40);
  assert.deepEqual(split(paper), { fromStock: 40, toPurchase: 60 });
  assert.equal(purchaseTotal([paper]), 60 * 45000);
  // Своё наименование (не из каталога) — всё к закупке, даже если ввели «со склада».
  assert.deepEqual(split(item({ quantity: "10", fromStock: "5" })), { fromStock: 0, toPurchase: 10 });
  assert.equal(stockCover(item({ quantity: "10", itemId: "i1", available: 40 })), 10);
});

test("checkDraft: со склада не больше доступного, меньше доступного — с обоснованием при отправке", () => {
  const base = { title: "Канцелярия", reason: "Нужно для отчётов" };
  const ok = item({ quantity: "100", itemId: "i1", available: 40, fromStock: "40" });
  assert.equal(checkDraft({ ...base, items: [ok] }, true), null);
  assert.match(checkDraft({ ...base, items: [{ ...ok, fromStock: "41" }] }, true), /доступно 40/);
  assert.match(checkDraft({ ...base, items: [{ ...ok, fromStock: "0" }] }, true), /обоснуйте закупку/);
  assert.equal(checkDraft({ ...base, items: [{ ...ok, fromStock: "0" }] }, false), null); // черновик — можно
  assert.equal(checkDraft({ ...base, items: [{ ...ok, fromStock: "0", purchaseReason: "Нужна бумага другого формата" }] }, true), null);
  assert.match(checkDraft({ ...base, items: [{ ...ok, fromStock: "101" }] }, true), /от 0 до количества/);
});

test("orderTotals считает как сервер: скидка до НДС, доставка без НДС, «цены с НДС» — без начисления", () => {
  const lines = [
    { quantity: 60, price: 40000 },
    { quantity: 2, price: 250000 },
  ];
  assert.deepEqual(orderTotals(lines, 100000, 12, false, 50000), { subtotal: 2900000, vat: 336000, total: 3186000 });
  assert.equal(orderTotals(lines, 100000, 12, true, 50000).total, 2850000);
  assert.equal(orderTotals([{ quantity: 1, price: 1000 }], 5000, 12, false, 0).total, 0);
});

test("ratingText: 0–100 → «из 5» с запятой, нет данных — прочерк", () => {
  assert.equal(ratingText(84), "4,2");
  assert.equal(ratingText(100), "5");
  assert.equal(ratingText(null), "—");
});

test("receiptLine как сервер: принято = пришло − брак, лишнее сверх ожидаемого не принимается", () => {
  assert.deepEqual(receiptLine("Бумага", "пачка", 62, 2, 0, 100), { accepted: 60, error: null });
  assert.deepEqual(receiptLine("Бумага", "пачка", 0, 0, 5, 40), { accepted: 0, error: null });
  assert.deepEqual(receiptLine("Бумага", "пачка", 42, 2, 0, 40), { accepted: 40, error: null });
  assert.match(receiptLine("Бумага", "пачка", 41, 0, 0, 40).error, /лишнее не принимается/);
  assert.match(receiptLine("Бумага", "пачка", 2, 3, 0, 40).error, /брак/);
  assert.ok(receiptLine("Бумага", "пачка", 1.0001, 0, 0, 40).error);
});

test("periodOf: этот и прошлый месяц, 3 месяца, год — даты по Ташкенту", () => {
  assert.deepEqual(periodOf("month", "2026-11-01"), { from: "2026-11-01", to: "2026-11-01" });
  assert.deepEqual(periodOf("prev", "2026-11-15"), { from: "2026-10-01", to: "2026-10-31" });
  assert.deepEqual(periodOf("prev", "2026-01-10"), { from: "2025-12-01", to: "2025-12-31" });
  assert.deepEqual(periodOf("quarter", "2026-11-15"), { from: "2026-09-01", to: "2026-11-15" });
  assert.deepEqual(periodOf("year", "2026-11-15"), { from: "2025-11-16", to: "2026-11-15" });
});

test("compactMoney и reportCell: крупные суммы коротко, ячейки как в Excel", () => {
  assert.equal(compactMoney(1_250_000), "1,3 млн");
  assert.equal(compactMoney(60_000_000), "60 млн");
  assert.equal(compactMoney(12_900), "12,9 тыс.");
  assert.equal(compactMoney(9_500), "9 500");
  assert.equal(compactMoney(2_000_000_000, { bn: "mlrd", mn: "mln", th: "ming" }), "2 mlrd");
  assert.equal(reportCell(27.84, "percent"), "27,8 %");
  assert.equal(reportCell("2026-11-01", "date"), "01.11.2026");
  assert.equal(reportCell(4400000, "money"), "4 400 000");
  assert.equal(reportCell(null, "text"), "");
});

const store = (over = {}) => ({ id: "w", code: "w", name: "Склад", kind: "Other", departmentId: null, departmentName: null, address: null, isDefault: false, isActive: true, items: 0, keepers: [], posts: true, version: 1, ...over });

test("operationWarehouse: выбранный склад, если по нему можно проводить, иначе по умолчанию, иначе первый доступный", () => {
  const list = [store({ id: "central", isDefault: true }), store({ id: "raw" }), store({ id: "old", isActive: false }), store({ id: "shop", posts: false })];
  assert.equal(operationWarehouse(list, "raw"), "raw");
  assert.equal(operationWarehouse(list, null), "central");
  assert.equal(operationWarehouse(list, "shop"), "central"); // чужой склад — по умолчанию
  assert.equal(operationWarehouse(list, "old"), "central"); // отключённый — по умолчанию
  // Закреплённый только за складом сырья: склад по умолчанию ему недоступен.
  assert.equal(operationWarehouse([store({ id: "central", isDefault: true, posts: false }), store({ id: "raw" })], null), "raw");
  assert.equal(operationWarehouse([store({ id: "central", isDefault: true, posts: false })], null), null);
  assert.deepEqual(activeWarehouses(list).map((w) => w.id), ["central", "raw", "shop"]);
  assert.deepEqual(activeWarehouses(null), []);
});

test("партии: сумма, срок по дате производства, сторно и подпись — как на сервере", () => {
  const d = (quantity, over = {}) => ({ key: "k", no: "", made: "", expires: "", quantity, ...over });
  assert.equal(batchesTotal([d("40"), d("60,5")]), 100.5);
  assert.equal(batchesTotal([d("40"), d("")]), null);
  assert.equal(batchesTotal([d("0")]), null);
  assert.equal(batchExpiry("2027-04-01", "", null), "2027-04-01");
  assert.equal(batchExpiry("", "2026-11-05", 180), "2027-05-04");
  assert.equal(batchExpiry("", "2026-11-05", null), null);
  const m = (over = {}) => ({ documentKind: "Receipt", orderId: null, requestId: null, reversedBy: null, ...over });
  assert.equal(reversible(m()), true);
  assert.equal(reversible(m({ documentKind: "Transfer" })), true);
  assert.equal(reversible(m({ orderId: "o" })), false); // приёмка по заказу
  assert.equal(reversible(m({ documentKind: "Issue", requestId: "r" })), false); // выдача по заявке
  assert.equal(reversible(m({ documentKind: "BatchEntry" })), false);
  assert.equal(reversible(m({ reversedBy: "STN-2026-00001" })), false);
  assert.equal(reversible(m({ tenderId: "t" })), false); // поставка по тендеру
  assert.equal(
    batchOptionLabel({ id: "b", number: "LOT-2026-00012", supplierBatchNo: "A-1", expiresOn: "2027-04-01", onHand: 40, usable: false }, "кг"),
    "LOT-2026-00012 · A-1 · до 01.04.2027 · 40 кг · просрочена",
  );
});

test("единицы закупки: пересчёт в единицу карточки до трёх знаков, цена за единицу карточки — как на сервере", () => {
  assert.equal(unitToBase(4, 25), 100); // 4 мешка по 25 кг
  assert.equal(unitToBase(1.5, 0.5), 0.75);
  assert.equal(unitToBase(0.1, 0.0125), null); // 0,00125 — больше трёх знаков: не округляем молча
  assert.equal(unitToBase(3, 1), 3);
  assert.equal(costToBase(250000, 25), 10000); // мешок за 250 000 — 10 000 за кг
  assert.equal(costToBase(100, 3), 33.33);
});

test("тип заявки: сырьё — только в заявке на сырьё, остальное — только в обычной", () => {
  const raw = { isProduction: true };
  const parts = { isProduction: false };
  assert.equal(kindAccepts("Raw", raw), true);
  assert.equal(kindAccepts("Raw", parts), false);
  assert.equal(kindAccepts("Regular", raw), false);
  assert.equal(kindAccepts("Regular", parts), true);
  assert.equal(kindAccepts("Regular", undefined), true); // категория неизвестна (не из списка формы) — как не сырьё
  assert.equal(kindAccepts("Raw", {}), false);
});

test("маршрут сырья: у каждого автора шаги, без повторов, директор один раз, свой шаг автору нельзя", () => {
  const ok = { supply: ["analyst", "finance"], analyst: ["finance"], finance: ["analyst"] };
  assert.equal(rawRouteProblem(ok), null);
  assert.equal(rawRouteProblem({ ...ok, supply: ["analyst", "finance", "director_limit"] }), null);
  assert.match(rawRouteProblem({ ...ok, supply: [] }), /хотя бы один шаг/);
  assert.match(rawRouteProblem({ ...ok, supply: ["finance", "finance"] }), /один раз/);
  assert.match(rawRouteProblem({ ...ok, finance: ["director", "director_limit"] }), /один раз/);
  assert.match(rawRouteProblem({ ...ok, analyst: ["analyst"] }), /собственный шаг/);
  assert.match(rawRouteProblem({ ...ok, finance: ["finance", "analyst"] }), /собственный шаг/);
});
