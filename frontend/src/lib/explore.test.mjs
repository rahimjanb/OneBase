import { test } from "node:test";
import assert from "node:assert/strict";
import { bucketEnd, cellText, chartModel, compactValue, decodeQuery, drill, encodeQuery, groupRows, quickPeriods, sortRows, toCsv } from "./explore.ts";

const nb = " ";

test("запрос в адресе: туда и обратно с кириллицей, мусор — null, лимит не сохраняется", () => {
  const q = { metrics: ["revenue", "kg"], dimensions: ["date", "product_group"], grain: "day", dateFrom: "2026-08-01", dateTo: "2026-11-11", filters: [{ dimension: "region", op: "in", values: ["Самарканд"] }], sort: [{ by: "revenue", dir: "desc" }], limit: 500 };
  const back = decodeQuery(encodeQuery(q));
  assert.deepEqual(back.metrics, ["revenue", "kg"]);
  assert.deepEqual(back.filters, [{ dimension: "region", op: "in", values: ["Самарканд"] }]);
  assert.equal(back.dateFrom, "2026-08-01");
  assert.equal(back.limit, undefined);
  assert.equal(decodeQuery("%%%"), null);
  assert.equal(decodeQuery(encodeQuery({ metrics: [] })), null);
  // Ссылка AI-аналитика (сервер, camelCase без null) читается так же.
  const server = Buffer.from(JSON.stringify({ metrics: ["revenue"], dimensions: ["region"], dateFrom: "2026-10-01", dateTo: "2026-10-31", includeTotals: true, fillZeros: false })).toString("base64url");
  assert.deepEqual(decodeQuery(server).dimensions, ["region"]);
});

test("периоды и конец шага: месяц, прошлый месяц, 30 дней; неделя пн–вс, квартал", () => {
  const p = quickPeriods(new Date(2026, 10, 12));
  assert.deepEqual(p.find((x) => x.key === "last_month"), { key: "last_month", label: "Прошлый месяц", from: "2026-10-01", to: "2026-10-31" });
  assert.deepEqual(p.find((x) => x.key === "d30"), { key: "d30", label: "30 дней", from: "2026-10-14", to: "2026-11-12" });
  assert.equal(bucketEnd("2026-11-09", "week"), "2026-11-15");
  assert.equal(bucketEnd("2026-10-01", "quarter"), "2026-12-31");
  assert.equal(bucketEnd("2026-02-01", "month"), "2026-02-28");
});

test("переход вглубь: регион → агенты региона, группа → товары, месяц → дни; артикул — по товару", () => {
  const q = { metrics: ["revenue"], dimensions: ["region", "product_group"], filters: [{ dimension: "region", values: ["x"] }] };
  const r = drill(q, "region", "01a1-uuid");
  assert.deepEqual(r.dimensions, ["agent", "product_group"]);
  assert.deepEqual(r.filters, [{ dimension: "region", op: "in", values: ["01a1-uuid"] }]);
  const g = drill(q, "product_group", -10);
  assert.deepEqual(g.dimensions, ["region", "product"]);
  assert.deepEqual(g.filters.at(-1), { dimension: "product_group", op: "in", values: ["-10"] });
  const m = drill({ metrics: ["kg"], dimensions: ["date"], grain: "month" }, "date", "2026-10-01");
  assert.equal(m.grain, "day");
  assert.equal(m.dateTo, "2026-10-31");
  assert.equal(drill({ metrics: ["kg"], dimensions: ["date"], grain: "day" }, "date", "2026-10-01"), null);
  assert.deepEqual(drill({ metrics: ["kg"], dimensions: ["sku"] }, "sku", 100).filters, [{ dimension: "product", op: "in", values: ["100"] }]);
  assert.deepEqual(drill({ metrics: ["kg"], dimensions: ["region"] }, "region", null).filters[0].values, ["Без региона"]);
});

const result = {
  columns: [
    { code: "date", title: "Дата", kind: "dimension", unit: null, format: "date" },
    { code: "product_group", title: "Товарная группа", kind: "dimension", unit: null, format: "text" },
    { code: "revenue", title: "Выручка", kind: "metric", unit: "сум", format: "money" },
    { code: "outlets", title: "АКБ", kind: "metric", unit: "точек", format: "integer" },
  ],
  rows: [
    ["2026-08-01", "Кекс", 100, 3],
    ["2026-08-01", "Бамбук", 50, 2],
    ["2026-08-02", "Кекс", 120, 4],
  ],
  keys: [["2026-08-01", -10], ["2026-08-01", -4], ["2026-08-02", -10]],
};

test("график: по датам — серии второго измерения; без даты — столбцы по убыванию", () => {
  const line = chartModel(result);
  assert.equal(line.kind, "line");
  assert.deepEqual(line.labels, ["2026-08-01", "2026-08-02"]);
  assert.deepEqual(line.series.map((s) => [s.name, s.values]), [["Кекс", [100, 120]], ["Бамбук", [50, null]]]);
  const bars = chartModel({ ...result, columns: result.columns.slice(1), rows: [["Кекс", 220, 5], ["Бамбук", 50, 2]], keys: [[-10], [-4]] });
  assert.equal(bars.kind, "bars");
  assert.deepEqual(bars.bars.map((b) => b.label), ["Кекс", "Бамбук"]);
});

test("группировка: подытоги складывают суммы, но не АКБ; сортировка с пустыми в конце", () => {
  const groups = groupRows(result);
  assert.deepEqual(groups.map((g) => [g.label, g.subtotal[2], g.subtotal[3]]), [["2026-08-01", 150, null], ["2026-08-02", 120, null]]);
  assert.deepEqual(sortRows(result, "revenue", "desc"), [2, 0, 1]);
  assert.deepEqual(sortRows({ ...result, rows: [["a", "x", null, 1], ["b", "y", 5, 1]] }, "revenue", "asc"), [1, 0]);
});

test("форматы и CSV: разряды, проценты, даты; «;» и кавычки, BOM для Excel", () => {
  assert.equal(cellText(13509685348, "money"), `13${nb}509${nb}685${nb}348`);
  assert.equal(cellText(7.94, "percent"), `7,9${nb}%`);
  assert.equal(cellText("2026-08-01", "date"), "01.08.2026");
  assert.equal(compactValue(13_509_685_348, "money"), `13,51${nb}млрд`);
  const csv = toCsv(result.columns.slice(1, 3), [['Кекс "А"; новый', 1.5]]);
  assert.ok(csv.startsWith("﻿"));
  assert.ok(csv.includes('"Кекс ""А""; новый";1,5'));
});
