// Права пользователя в разделах отделов и настройках (lib/users.ts): node --test src/lib/users.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import { rightsWithSections, toggleRight } from "./users.ts";

const catalog = [
  { key: "procurement.request", requires: [], needsSection: null },
  { key: "procurement.read", requires: [], needsSection: null },
  { key: "procurement.manage", requires: ["procurement.read"], needsSection: null },
  { key: "procurement.warehouse", requires: [], needsSection: null },
  { key: "production.read", requires: [], needsSection: null },
  { key: "production.operate", requires: ["production.read"], needsSection: null },
  { key: "sales.manage", requires: [], needsSection: "sales" },
];

test("отмеченное право включает то, без чего не работает; снятое — снимает зависящие", () => {
  assert.deepEqual(toggleRight(["procurement.warehouse"], "procurement.manage", true, catalog), ["procurement.read", "procurement.manage", "procurement.warehouse"]);
  assert.deepEqual(toggleRight(["procurement.read", "procurement.manage", "procurement.warehouse"], "procurement.read", false, catalog), ["procurement.warehouse"]);
  assert.deepEqual(toggleRight(["procurement.read", "procurement.manage"], "procurement.manage", false, catalog), ["procurement.read"]);
  assert.deepEqual(toggleRight([], "production.operate", true, catalog), ["production.read", "production.operate"]);
  assert.deepEqual(toggleRight(["procurement.request"], "procurement.request", true, catalog), ["procurement.request"]);
});

test("снятый раздел убирает права, которым он нужен", () => {
  assert.deepEqual(rightsWithSections(["sales.manage", "procurement.read"], ["tasks"], catalog), ["procurement.read"]);
  assert.deepEqual(rightsWithSections(["sales.manage"], ["sales"], catalog), ["sales.manage"]);
});
