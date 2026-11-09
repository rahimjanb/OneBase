// Поставки (lib/arrivals.ts): node --test src/lib/arrivals.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import { acceptedSum, byDue, dueGroup, shortfall, stockOf } from "./arrivals.ts";

test("на склад — вес у весовой строки, иначе места × коэффициент", () => {
  const flour = { weighed: true, factor: 50 };
  assert.equal(stockOf(flour, 119, 5940), 5940);
  assert.equal(stockOf(flour, 119, null), null); // вес не указан — не посчитать
  assert.equal(stockOf(flour, 0, null), 0); // ничего не принято
  const boxes = { weighed: false, factor: 12 };
  assert.equal(stockOf(boxes, 3, null), 36);
  assert.equal(stockOf({ weighed: false, factor: 1 }, 1000, null), 1000);
  assert.equal(stockOf({ weighed: false, factor: 0.3333 }, 0.1, null), null); // больше трёх знаков — как на сервере
  assert.equal(stockOf(boxes, null, null), null);
});

test("расхождение с накладной: минус — недостача, плюс — излишек", () => {
  assert.equal(shortfall(6000, 5940), -60);
  assert.equal(shortfall(1000, 1000), 0);
  assert.equal(shortfall(250, 250.5), 0.5);
  assert.equal(shortfall(250, null), null);
});

test("сумма приёмки по цене тендера за единицу карточки — как на сервере", () => {
  // 5 940 кг × (310 000 ÷ 50) + 1 000 кг × 9 000 = 45 828 000.
  assert.equal(acceptedSum([{ stock: 5940, price: 310000, factor: 50 }, { stock: 1000, price: 9000, factor: 1 }]), 45828000);
  assert.equal(acceptedSum([{ stock: null, price: 310000, factor: 50 }, { stock: 0, price: 9000, factor: 1 }]), 0);
});

test("ожидаемые поставки по срокам: просрочено, сегодня, завтра, неделя, позже, без срока", () => {
  assert.deepEqual([-2, 0, 1, 7, 8, null].map(dueGroup), ["overdue", "today", "tomorrow", "week", "later", "none"]);
  const groups = byDue([{ id: "a", daysLeft: 3 }, { id: "b", daysLeft: -1 }, { id: "c", daysLeft: null }, { id: "d", daysLeft: 5 }]);
  assert.deepEqual(groups.map((g) => [g.group, g.rows.map((r) => r.id)]), [["overdue", ["b"]], ["week", ["a", "d"]], ["none", ["c"]]]);
});

test("роли на рабочем столе и блок над «Обзором»", async () => {
  const { roleNames, hasAttention } = await import("./arrivals.ts");
  const none = { warehouse: false, quality: false, decidesQuality: false, supply: false, analyst: false, head: false, director: false, financier: false };
  assert.deepEqual(roleNames({ ...none, warehouse: true, decidesQuality: true }), ["Кладовщик", "Лаборатория"]);
  const home = { roles: none, acceptedCount: 2, requests: { awaitingMe: 0, departmentToPickUp: 0, mineToPickUp: 0 } };
  assert.equal(hasAttention(home), false); // принято на склад — только директору и финансисту
  assert.equal(hasAttention({ ...home, roles: { ...none, financier: true } }), true);
  assert.equal(hasAttention({ ...home, requests: { ...home.requests, awaitingMe: 1 } }), true);
});
