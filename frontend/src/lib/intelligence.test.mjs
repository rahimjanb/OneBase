import { test } from "node:test";
import assert from "node:assert/strict";
import { count, decodeDraft, duration, groupDecisions, share, usd, waves } from "./intelligence.ts";

test("длительность, стоимость и доли — как в сводке качества", () => {
  assert.equal(duration(850), "850 мс");
  assert.equal(duration(12_400), "12,4 с");
  assert.equal(duration(125_000), "2 мин 05 с");
  assert.equal(duration(null), "—");
  assert.equal(usd(0.004), "< $0,01");
  assert.equal(usd(1.236), "$1,24");
  assert.equal(usd(null), "—");
  assert.equal(share(0.725), "73 %");
});

test("волны плана: зависимые шаги — следующей волной, цикл не зацикливает", () => {
  const s = (id, deps = []) => ({ id, question: id, analyst: "analyst.sales", dependsOn: deps, round: 0 });
  assert.deepEqual(waves([s("s1"), s("s2", ["s1"]), s("s3"), s("s4", ["s2", "s3"])]), [["s1", "s3"], ["s2"], ["s4"]]);
  assert.deepEqual(waves([s("a", ["b"]), s("b", ["a"])]).flat().sort(), ["a", "b"]);
  // Шаг перепланирования (раунд 1) — после всех волн исходного плана, даже без зависимостей.
  assert.deepEqual(waves([s("s1"), s("s2", ["s1"]), { ...s("r1"), round: 1 }]), [["s1"], ["s2"], ["r1"]]);
});

test("черновик решения из ссылки AI: кириллица туда и обратно, мусор — null", () => {
  const draft = { title: "Вернуть точку 5 в Хорезме", metric: "revenue", expectedDirection: "up", expectedChangePct: 15, decidedOn: "2026-11-13", reviewOn: "2026-12-13" };
  const encoded = Buffer.from(JSON.stringify(draft)).toString("base64url");
  assert.deepEqual(decodeDraft(encoded), draft);
  assert.equal(decodeDraft("%%%"), null);
  assert.equal(decodeDraft(Buffer.from(JSON.stringify({ title: "x", metric: "revenue", expectedDirection: "sideways" })).toString("base64url")), null);
});

test("решения по разделам и счётчики", () => {
  const d = (id, status, due) => ({ id, status, due });
  const g = groupDecisions([d("1", "active", true), d("2", "active", false), d("3", "evaluated", false), d("4", "cancelled", false)]);
  assert.deepEqual([g.due.length, g.active.length, g.evaluated.length, g.cancelled.length], [1, 1, 1, 1]);
  assert.equal(count([{ key: "rejected", count: 3 }], "rejected"), 3);
  assert.equal(count([], "x"), 0);
});
