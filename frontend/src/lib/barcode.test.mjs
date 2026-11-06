// Сканер штрихкода (lib/barcode.ts): node --test src/lib/barcode.test.mjs.
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { cameraProblem, cleanCode, createConfirmer } from "./barcode.ts";

test("линейный код — после двух одинаковых чтений подряд, QR — сразу", () => {
  const confirm = createConfirmer();
  assert.equal(confirm("4600000000017", "ean_13"), null);
  assert.equal(confirm("4600000000018", "ean_13"), null); // ошибка в полосе — счёт заново
  assert.equal(confirm("4600000000018", "ean_13"), "4600000000018");
  assert.equal(createConfirmer()("PRD-2026-00012", "qr_code"), "PRD-2026-00012");
  assert.equal(createConfirmer()("", "ean_13"), null);
});

test("код очищается от служебных символов и пробелов, пустой и слишком длинный — нет", () => {
  assert.equal(cleanCode(" 4600000000017\n"), "4600000000017");
  assert.equal(cleanCode("\u001d0104600000000017"), "0104600000000017"); // разделитель GS1
  assert.equal(cleanCode("  "), null);
  assert.equal(cleanCode("x".repeat(65)), null);
});

test("ошибки камеры — понятным текстом", () => {
  assert.match(cameraProblem({ name: "NotAllowedError" }), /Разрешите камеру/);
  assert.match(cameraProblem({ name: "NotFoundError" }), /не найдена/);
  assert.match(cameraProblem({ name: "NotReadableError" }), /занята/);
  assert.match(cameraProblem(new Error("x")), /не удалось/);
  assert.match(cameraProblem(null, false), /https/);
});

test("распознавание лежит в public/zxing и совпадает с версией библиотеки (контрольная сумма из zxing-wasm)", () => {
  const file = readFileSync(new URL("../../public/zxing/zxing_reader.wasm", import.meta.url));
  const sum = createHash("sha256").update(file).digest("hex");
  const source = readFileSync(new URL("../../node_modules/zxing-wasm/dist/es/reader/index.js", import.meta.url), "utf8");
  const expected = source.match(/"([0-9a-f]{64})"/g)?.map((s) => s.slice(1, -1)) ?? [];
  assert.ok(expected.includes(sum), "public/zxing/zxing_reader.wasm устарел — скопируйте из node_modules/zxing-wasm/dist/reader/");
});
