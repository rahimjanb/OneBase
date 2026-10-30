// Картинки запуска для установленного на iPhone и iPad приложения OneBase (apple-touch-startup-image): пока iOS поднимает приложение,
// вместо белого экрана — цвет заставки 1Base (#001042), следом сразу анимация (Splash.tsx). Без логотипа: иначе он мелькнул бы
// и пропал перед сборкой букв.
// iOS берёт картинку, только если её размер в пикселях ровно равен экрану, — по одной на экран и ориентацию.
// Запуск: node frontend/scripts/splash-screens.mjs — после правки src/components/shell/splashScreens.json (новые устройства).
// Ссылки на картинки layout.tsx строит из того же списка. Установленное ранее приложение подхватит картинки, только если добавить его
// на экран «Домой» заново: iOS запоминает их при установке.
import { mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { deflateSync } from "node:zlib";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const screens = JSON.parse(readFileSync(join(root, "src/components/shell/splashScreens.json"), "utf8"));
const out = join(root, "public/splash/startup");
const COLOR = [0x00, 0x10, 0x42]; // #001042 — фон заставки

const crcTable = Array.from({ length: 256 }, (_, n) => {
  let c = n;
  for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
  return c >>> 0;
});
function crc32(buf) {
  let c = 0xffffffff;
  for (const b of buf) c = crcTable[(c ^ b) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}
function chunk(type, data) {
  const head = Buffer.alloc(8);
  head.writeUInt32BE(data.length, 0);
  head.write(type, 4, "ascii");
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(Buffer.concat([head.subarray(4), data])), 0);
  return Buffer.concat([head, data, crc]);
}

/** PNG одного цвета: палитра из одного цвета, 1 бит на пиксель — меньше килобайта даже для 2064×2752. */
function solidPng(width, height) {
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(width, 0);
  ihdr.writeUInt32BE(height, 4);
  ihdr[8] = 1; // бит на пиксель
  ihdr[9] = 3; // цвет из палитры
  const raw = Buffer.alloc((1 + Math.ceil(width / 8)) * height); // каждая строка: фильтр 0 и нули — цвет №0 палитры
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk("IHDR", ihdr),
    chunk("PLTE", Buffer.from(COLOR)),
    chunk("IDAT", deflateSync(raw, { level: 9 })),
    chunk("IEND", Buffer.alloc(0)),
  ]);
}

mkdirSync(out, { recursive: true });
for (const file of readdirSync(out)) if (file.endsWith(".png")) rmSync(join(out, file));
let total = 0;
for (const { width, height, ratio } of screens) {
  const w = width * ratio;
  const h = height * ratio;
  for (const [pw, ph] of [[w, h], [h, w]]) {
    const png = solidPng(pw, ph);
    writeFileSync(join(out, `${pw}x${ph}.png`), png);
    total += png.length;
  }
}
console.log(`${screens.length * 2} картинок запуска в public/splash/startup, всего ${(total / 1024).toFixed(1)} КБ`);
