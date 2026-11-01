// Раздел «Заявки» (lib/procurement.ts): node --test src/lib/procurement.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import { checkDraft, compactMoney, dateRu, draftTotal, lineSum, money, orderTotals, parseNumber, periodOf, purchaseTotal, quantity, ratingText, receiptLine, reportCell, split, stageIndex, stockCover } from "./procurement.ts";

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
  // Пустая строка позиции в конце формы не мешает.
  assert.equal(checkDraft({ ...ok, items: [item(), item({ name: "", quantity: "", price: "" })] }, true), null);
});

test("этапы заявки: доработка — на шаге «Отправлена», отмена — вне полосы", () => {
  assert.equal(stageIndex("Draft"), 0);
  assert.equal(stageIndex("RevisionRequired"), 1);
  assert.equal(stageIndex("DirectorApproval"), 2);
  assert.equal(stageIndex("PartiallyFulfilled"), 3);
  assert.equal(stageIndex("Fulfilled"), 4);
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
