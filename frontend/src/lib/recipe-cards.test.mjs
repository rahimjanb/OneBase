// «Технолог → Техкарты» (lib/recipe-cards.ts): node --test src/lib/recipe-cards.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import {
  effectiveDate, filterCounts, normBase, parseFilter, parseSort, parseView, photoFileError, photoSrc, photoSrcSet, recipeTone, sortRecipes,
} from "./recipe-cards.ts";

const row = (code, name, extra = {}) => ({
  id: code, code, versionNo: 1, name, outputName: name, outputUnit: "кг", outputQuantity: 1000, status: "Draft", lines: 1, activatedAt: null, draftId: code,
  versions: 1, techCardNo: null, technologistName: null, effectiveFrom: null, stages: 0, draftStatus: "Draft", ...extra,
});

test("база норм и дата начала действия: неразрывные пробелы, длинная и короткая дата", () => {
  assert.equal(normBase(1000, "кг"), "1 000 кг");
  assert.equal(normBase(1250.5, "кг"), "1 250,5 кг");
  assert.equal(effectiveDate("2026-11-09"), "09.11.2026");
  assert.equal(effectiveDate("2026-11-09", true), "09.11");
  assert.equal(effectiveDate(null), null);
  assert.equal(effectiveDate("вчера"), null);
});

test("фильтр, сортировка и вид из адреса или браузера: чужое значение — по умолчанию", () => {
  assert.equal(parseFilter("pending"), "pending");
  assert.equal(parseFilter("all"), "");
  assert.equal(parseFilter(null), "");
  assert.equal(parseSort("code"), "code");
  assert.equal(parseSort("random"), "new");
  assert.equal(parseView("list"), "list");
  assert.equal(parseView("tiles"), "grid");
});

test("тон значка: утверждена — зелёный, черновик — жёлтый, на утверждении — синий, архив — приглушённый", () => {
  assert.deepEqual(recipeTone, { Active: "ok", Draft: "warn", PendingApproval: "link", Archived: "subtle" });
});

test("счётчики: от сервера как есть; без них — по карточкам", () => {
  const server = { all: 6, active: 1, draft: 5, pending: 0, archived: 0 };
  assert.equal(filterCounts(server, []), server);
  const rows = [row("A", "a", { status: "Active", draftStatus: "Draft" }), row("B", "b"), row("C", "c", { status: "Archived", draftStatus: null })];
  assert.deepEqual(filterCounts(undefined, rows), { all: 3, active: 1, draft: 2, pending: 0, archived: 1 });
});

test("сортировка: новые — по последнему изменению; название и код — по-русски, числа по значению", () => {
  const rows = [
    row("W-602", "Снеки (35×28)", { updatedAt: "2026-11-01T10:00:00Z" }),
    row("105-098", "Глазурь кора", { updatedAt: "2026-11-09T10:00:00Z" }),
    row("K-521", "кекс с начинкой", { updatedAt: null }),
    row("101-72", "Сечка", { updatedAt: "2026-11-05T10:00:00Z" }),
  ];
  assert.deepEqual(sortRecipes(rows, "new").map((r) => r.code), ["105-098", "101-72", "W-602", "K-521"]);
  assert.deepEqual(sortRecipes(rows, "name").map((r) => r.code), ["105-098", "K-521", "101-72", "W-602"]);
  assert.deepEqual(sortRecipes(rows, "code").map((r) => r.code), ["101-72", "105-098", "K-521", "W-602"]);
  assert.equal(rows[0].code, "W-602"); // исходный массив не трогаем
});

test("фото: адреса превью и srcset; файл — тип, пустой, до 10 МБ, HEIC без типа — по расширению", () => {
  const photo = { id: "abc" };
  assert.equal(photoSrc(photo, 960), "/bff/api/production/recipe-photos/abc/960");
  assert.equal(photoSrcSet(photo), "/bff/api/production/recipe-photos/abc/480 480w, /bff/api/production/recipe-photos/abc/960 960w");
  assert.equal(photoFileError({ name: "a.jpg", type: "image/jpeg", size: 3_000_000 }), null);
  assert.equal(photoFileError({ name: "IMG_1.HEIC", type: "", size: 2_000_000 }), null);
  assert.equal(photoFileError({ name: "a.pdf", type: "application/pdf", size: 10 }), "Фото — JPEG, PNG, WebP или HEIC.");
  assert.equal(photoFileError({ name: "a.png", type: "image/png", size: 0 }), "Файл пустой — выберите фото.");
  assert.equal(photoFileError({ name: "a.png", type: "image/png", size: 10 * 1024 * 1024 + 1 }), "Фото — до 10 МБ.");
});
