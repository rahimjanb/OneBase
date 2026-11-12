// Охват «Продаж» в интерфейсе (lib/sales/access.ts): node --test src/lib/sales-access.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import { isLimited, salesPathAllowed, salesTabAllowed, salesTabHref, visibleBack, visibleCrumbs } from "./sales/access.ts";

const org = { level: "Organization", home: "/sales" };
const rm = { level: "Rm", home: "/sales/directions/d1" };
const sv = { level: "Supervisor", home: "/sales/regions/r1" };
const tp = { level: "Agent", home: "/sales/agents/51" };

test("вся организация и без охвата — все вкладки, как раньше", () => {
  for (const scope of [org, null, undefined]) {
    for (const tab of ["analytics", "primary", "stock", "pnl", "system", "skuSales"]) assert.equal(salesTabAllowed(tab, scope), true);
    assert.equal(isLimited(scope), false);
    assert.equal(salesTabHref("analytics", "/sales", scope), "/sales");
  }
});

test("РМ и СВР: своя вторичка, агенты, ассортимент, планы, KPI, проблемные; страны — нет", () => {
  for (const scope of [rm, sv]) {
    assert.deepEqual(["analytics", "agents", "assortment", "plans", "kpi", "problems"].map((t) => salesTabAllowed(t, scope)), [true, true, true, true, true, true]);
    for (const tab of ["primary", "stock", "outstock", "promo", "dmp", "system", "skuSales", "pnl", "akt", "dso"]) assert.equal(salesTabAllowed(tab, scope), false, tab);
  }
  assert.equal(salesTabHref("analytics", "/sales", rm), "/sales/directions/d1");
  assert.equal(salesTabHref("analytics", "/sales", sv), "/sales/regions/r1");
  assert.equal(salesTabHref("kpi", "/sales/kpi", rm), "/sales/kpi");
});

test("ТП: без ассортимента региона; «Вторичка» — его карточка", () => {
  assert.equal(salesTabAllowed("assortment", tp), false);
  assert.equal(salesTabAllowed("agents", tp), true);
  assert.equal(salesTabHref("analytics", "/sales", tp), "/sales/agents/51");
});

test("прямые адреса: страницы страны закрыты, свои — открыты", () => {
  assert.equal(salesPathAllowed("/sales", rm), false);
  assert.equal(salesPathAllowed("/sales/republic", rm), false);
  assert.equal(salesPathAllowed("/sales/primary/republic", sv), false);
  assert.equal(salesPathAllowed("/sales/regions/r1", sv), true);
  assert.equal(salesPathAllowed("/sales/assortment/product/100", rm), true);
  assert.equal(salesPathAllowed("/sales/assortment", tp), false);
  assert.equal(salesPathAllowed("/sales/kpi", tp), true);
  assert.equal(salesPathAllowed("/sales/stock", org), true);
});

test("крошки и «Назад»: без страны и чужого уровня", () => {
  const crumbs = [{ label: "Республика", href: "/sales/republic?year=2026" }, { label: "Восток", href: "/sales/directions/d1" }, { label: "Самарканд", href: "/sales/regions/r1" }, { label: "Азиз" }];
  assert.deepEqual(visibleCrumbs(crumbs, rm).map((c) => c.label), ["Восток", "Самарканд", "Азиз"]);
  assert.deepEqual(visibleCrumbs(crumbs, sv).map((c) => c.label), ["Самарканд", "Азиз"]);
  assert.deepEqual(visibleCrumbs(crumbs, tp).map((c) => c.label), ["Азиз"]);
  assert.deepEqual(visibleCrumbs(crumbs, org).map((c) => c.label), ["Республика", "Восток", "Самарканд", "Азиз"]);
  assert.equal(visibleBack("/sales/republic?year=2026", rm), "/sales/directions/d1?year=2026");
  assert.equal(visibleBack("/sales/regions/r1", tp), "/sales/agents/51");
  assert.equal(visibleBack("/sales/regions/r1", sv), "/sales/regions/r1");
  assert.equal(salesPathAllowed("/sales/directions/d1", sv), false);
  assert.equal(salesPathAllowed("/sales/regions/r1", tp), false);
});

test("«Мои регионы»: РМ и СВР с несколькими регионами — своя страница, крошка страны заменяется ею", () => {
  const rmMany = { level: "Rm", home: "/sales/my" };
  const svMany = { level: "Supervisor", home: "/sales/my" };
  for (const scope of [rmMany, svMany]) {
    assert.equal(salesTabHref("analytics", "/sales", scope), "/sales/my");
    assert.equal(salesPathAllowed("/sales/my", scope), true);
    assert.equal(salesPathAllowed("/sales/regions/r2", scope), true);
    assert.equal(salesPathAllowed("/sales/republic", scope), false);
    assert.equal(visibleBack("/sales/republic?year=2026&month=11", scope), "/sales/my?year=2026&month=11");
  }
  assert.equal(salesPathAllowed("/sales/my", tp), false);
  assert.equal(salesPathAllowed("/sales/my", org), true);

  const crumbs = [{ label: "Республика", href: "/sales/republic?year=2026" }, { label: "Восток", href: "/sales/directions/d1" }, { label: "Самарканд", href: "/sales/regions/r1" }, { label: "Азиз" }];
  assert.deepEqual(visibleCrumbs(crumbs, svMany, "Мои регионы"), [{ label: "Мои регионы", href: "/sales/my?year=2026" }, { label: "Самарканд", href: "/sales/regions/r1" }, { label: "Азиз" }]);
  assert.deepEqual(visibleCrumbs(crumbs, rmMany, "Мои регионы").map((c) => c.label), ["Мои регионы", "Восток", "Самарканд", "Азиз"]);
  // Один регион и вся организация — как раньше: подпись не нужна.
  assert.deepEqual(visibleCrumbs(crumbs, sv, "Мои регионы").map((c) => c.label), ["Самарканд", "Азиз"]);
  assert.deepEqual(visibleCrumbs(crumbs, org, "Мои регионы").map((c) => c.label), ["Республика", "Восток", "Самарканд", "Азиз"]);
});
