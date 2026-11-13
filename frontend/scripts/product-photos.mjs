// Фото товаров «Продаж» из папки макетов (Mockups/<цех>/<бренд>/<упаковка>/<код>.png) — первичный набор при сервере:
// backend/src/OneBase.Api/SeedData/product-photos (WebP и manifest.json: код товара Linko → фото, первое — главное). Сервер при запуске
// кладёт их в хранилище — один раз на код товара (SalesProductPhotoService.SeedAsync); дальше фото меняют в интерфейсе («Продажи → Ассортимент»).
// Набор поменяли — новые коды получат фото при следующем запуске; кому набор уже ставили, не трогаются (меняйте в интерфейсе).
//
// Запуск (из корня репозитория):
//   docker exec onebase-postgres-1 psql -U onebase -d onebase -At -F $'\t' \
//     -c 'select p."Id", coalesce(p."Code",'"''"'), p."Name", coalesce(t."Name",'"''"') from linko."Products" p left join linko."ProductTypes" t on t."Id" = p."TypeId"' \
//     > /tmp/products.tsv
//   node frontend/scripts/product-photos.mjs --products /tmp/products.tsv [--src Mockups]
//
// Как файл находит товар:
// - имя файла = код Linko целиком («248.png», «184-1.png»);
// - «304-318.png» — одна картинка двух кодов; «043-Laura.png», «480-Арбуз.png» — ещё одно фото кода 043 / 480;
// - файлы без кода и файлы, чей номер — код упаковки другого веса, — по таблице OVERRIDES (что на картинке = товар);
// - цех папки должен совпасть с типом товара Linko («Помадные конфеты» → «Помадка…»): «1.png» в конфетах — не «Вентилятор (Bonus)» с кодом 1.
// Какое фото главное: упаковка, а не «(первичка)» (штучная обёртка); коробка (TV box, Show box) — раньше штуки; папка с брендом, которого
// нет в названии товара, отбрасывается, если у кода есть фото из папки, чей бренд в названии есть (126 «Yamelly Cherry» лежит и в
// «Magnolia»); «Old Pomadka» — конфеты в фантике, как «(первичка)».
// Картинки: прозрачные поля обрезаются, товар вписывается в квадрат 160 / 400 / 800 px (без увеличения), WebP с прозрачностью.
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync } from "node:fs";
import { basename, dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import sharp from "sharp";

const here = dirname(fileURLToPath(import.meta.url));
const repo = resolve(here, "..", "..");
const arg = (name, fallback) => {
  const i = process.argv.indexOf(`--${name}`);
  return i > 0 ? process.argv[i + 1] : fallback;
};
const src = resolve(repo, arg("src", "Mockups"));
const productsFile = arg("products");
if (!productsFile) {
  console.error("Нужен --products: выгрузка товаров Linko (см. заголовок скрипта).");
  process.exit(1);
}

export const SIZES = [160, 400, 800];
const out = join(repo, "backend", "src", "OneBase.Api", "SeedData", "product-photos");
const manifestFile = join(out, "manifest.json");

/** Цех (первая папка) → начало названия типа товара в Linko. */
const SHOPS = {
  "Экструзионный цех": "Бамбук",
  "Цех трубочки": "Трубочки",
  "Цех Кекс": "Кекс",
  "Цех сахарного печенья": "Печенье",
  "Помадные конфеты": "Помадка",
  "Шоколадные конфеты": "Шоколад",
  Twizos: "Снек",
};

/** Бренды в названиях папок и как они пишутся в названиях товаров Linko. */
const BRANDS = [
  ["yamelly"],
  ["magnolia", "magnoliya"],
  ["shafran"],
  ["mazzza"],
  ["frully"],
  ["nillo"],
  ["panda", "eddy"],
  ["keko"],
  ["bonsi"],
  ["azara"],
  ["lilya"],
  ["laura"],
  ["delisio"],
  ["azaliya"],
  ["manzur"],
  ["mis nat", "misnat"],
  ["twizos"],
  ["bamboo", "banboo", "bambuk", "bambo"],
  ["hrustik", "xrustik"],
  ["kartofelki"],
];

/** Файлы без кода в имени: путь от папки макетов → коды. extra — только дополнительным фото (у кода есть упаковка). */
const OVERRIDES = {
  "Цех трубочки/MIs Nat Max/Mis Nat 180 gr/Mis Nat 180 gr.png": { codes: ["493"] },
  "Шоколадные конфеты/Delisio/Delisio/1 (13).png": { codes: ["044", "506"] },
  "Цех Кекс/Nillo/Nillo Panda/Nillo Panda Первичка/Eddy Panda (chocolate)).png": { codes: ["324", "341"], extra: true },
  "Цех Кекс/Nillo/Nillo Panda/Nillo Panda Первичка/Eddy Panda (Banan)).png": { codes: ["450", "492"], extra: true },
  // Шоубокс шоколадного Nillo — коробка 315 (у карамельного 316 своя «tv box»).
  "Цех Кекс/Nillo/Nillo/Nillo Show Box/0.png": { codes: ["315"] },
  // Коробка Frully всех вкусов — весовые 2 кг (065–068 и карамельный 460); «без этикетки» (346–349) — не она.
  "Цех сахарного печенья/Frully Tv box/Безымянный-2.png": { codes: ["065", "066", "067", "068", "460"] },
  // «Frully 180 gr»: на пакетах напечатано 180 г — это 342–345; 254–256 в Linko — по 300 г (им — обёртка того же печенья), 460 — коробка 2 кг.
  "Цех сахарного печенья/Frully 180 gr/254.png": { codes: ["342"] },
  "Цех сахарного печенья/Frully 180 gr/255.png": { codes: ["343"] },
  "Цех сахарного печенья/Frully 180 gr/256.png": { codes: ["344"] },
  "Цех сахарного печенья/Frully 180 gr/460.png": { codes: ["345"] },
  "Цех сахарного печенья/Frully (Первичка)/254.png": { codes: ["254", "342"] },
  "Цех сахарного печенья/Frully (Первичка)/255.png": { codes: ["255", "343"] },
  "Цех сахарного печенья/Frully (Первичка)/256.png": { codes: ["256", "344"] },
  // «Nillo New Bruni-Mini-Maffin»: номера папок «180 gr» и «Tv Box» перепутаны. Пакеты 180 г (Brauni, Mini, Maffin) — 518–520; шоубоксы со
  // штучными кексами — весовые 515–517 по 1,5 кг (как у Bonsi и «Brauni and Mini TV BOX»), штучные обёртки — к ним же.
  "Цех Кекс/Nillo/Nillo New Bruni-Mini-Maffin/Bruni-Mini-Maffin 180 gr/515.png": { codes: ["518"] },
  "Цех Кекс/Nillo/Nillo New Bruni-Mini-Maffin/Bruni-Mini-Maffin 180 gr/516.png": { codes: ["519"] },
  "Цех Кекс/Nillo/Nillo New Bruni-Mini-Maffin/Bruni-Mini-Maffin 180 gr/517.png": { codes: ["520"] },
  "Цех Кекс/Nillo/Nillo New Bruni-Mini-Maffin/Tv Box/518.png": { codes: ["515"] },
  "Цех Кекс/Nillo/Nillo New Bruni-Mini-Maffin/Tv Box/519.png": { codes: ["516"] },
  "Цех Кекс/Nillo/Nillo New Bruni-Mini-Maffin/Tv Box/520.png": { codes: ["517"] },
  "Цех Кекс/Nillo/Nillo New Bruni-Mini-Maffin/пепрвичка/518.png": { codes: ["515"], extra: true },
  "Цех Кекс/Nillo/Nillo New Bruni-Mini-Maffin/пепрвичка/519.png": { codes: ["516"], extra: true },
  "Цех Кекс/Nillo/Nillo New Bruni-Mini-Maffin/пепрвичка/520.png": { codes: ["517"], extra: true },
  // Пары Keko — «1 кг — 2,5 кг одного вида» (304-318 декор, 323-322 зебра); на «309-318» — Keko без декора: только 309, не 318 «декор».
  "Цех Кекс/Keko/309-318.png": { codes: ["309"] },
  // Обёртка DARK: «1.png» — не код 1 (в Linko это «Вентилятор (Bonus)»), а конфета 472 DARK.
  "Помадные конфеты/Old Pomadka/Shokolad/1.png": { codes: ["472"], extra: true },
};

const nfc = (s) => s.normalize("NFC");
const products = new Map();
for (const line of readFileSync(productsFile, "utf8").split("\n")) {
  const [, code, name, type] = line.split("\t");
  if (code) products.set(code.trim(), { name: nfc(name ?? ""), type: nfc(type ?? "") });
}

function* walk(dir) {
  for (const entry of readdirSync(dir)) {
    const path = join(dir, entry);
    if (statSync(path).isDirectory()) yield* walk(path);
    else if (/\.(png|jpe?g|webp)$/i.test(entry)) yield path;
  }
}

/** Коды Linko, к которым относится файл, и главное ли это фото кода (а не дополнительное). */
function codesOf(rel, stem) {
  const override = OVERRIDES[rel];
  if (override) return override.codes.map((code) => ({ code, alt: !!override.extra }));
  if (products.has(stem)) return [{ code: stem, alt: false }];
  const pair = stem.match(/^(\d+)-(\d+)$/);
  if (pair) return pair.slice(1).filter((c) => products.has(c)).map((code) => ({ code, alt: false }));
  const prefix = stem.match(/^(\d+)[-_ ]/);
  if (prefix && products.has(prefix[1])) return [{ code: prefix[1], alt: true }];
  return [];
}

/** Штучная обёртка, а не упаковка: «(первичка)» и папка «Old Pomadka» (конфеты в фантике). */
const firstPack = /п[еe]п?рвичка|old pomadka/i;

/**
 * Бренд папки и название товара: own — бренд папки есть в названии; alien — у папки есть бренд, но не этот («Magnolia/126» у «Yamelly
 * Cherry»). Чужие отбрасываются, только если у кода есть фото из «своей» папки: пакет «Yamelly 1kg» с конфетами MAZZZA внутри — упаковка 471.
 */
function score(rel, code, alt) {
  const path = rel.toLowerCase();
  const name = products.get(code).name.toLowerCase();
  const brands = BRANDS.filter((aliases) => aliases.some((a) => path.includes(a)));
  const own = brands.some((aliases) => aliases.some((a) => name.includes(a)));
  const alien = brands.length > 0 && !own;
  let s = own ? 2 : 0;
  s += firstPack.test(rel) ? -1 : 1;
  if (/(tv|show)\s*box/i.test(rel)) s += 0.5;
  if (alt) s -= 0.25;
  return { s, own, alien };
}

const candidates = new Map();
const skipped = [];
for (const file of walk(src)) {
  const rel = nfc(relative(src, file));
  const stem = nfc(basename(file).replace(/\.[^.]+$/, ""));
  const shop = SHOPS[rel.split("/")[0]];
  const codes = codesOf(rel, stem).filter(({ code }) => !shop || products.get(code).type.startsWith(shop));
  if (codes.length === 0) {
    skipped.push(rel);
    continue;
  }
  const hash = createHash("md5").update(readFileSync(file)).digest("hex");
  for (const { code, alt } of codes) {
    const list = candidates.get(code) ?? [];
    list.push({ file, rel, hash, ...score(rel, code, alt) });
    candidates.set(code, list);
  }
}

/**
 * «Похожие» фото товарам без своего: фото товара той же линейки — тот же бренд и вкус (цвет этикетки), другой вес или упаковка. Источник —
 * код (берётся его главное фото) или файл из папки макетов. В интерфейсе такое фото помечено «похожее», своя загрузка его убирает.
 * Бренды, у которых нет ни одного фото (Песочное, Roller, Jondi, Kunde kids…), без фото: чужой бренд не ставим.
 */
const SIMILAR = {
  // Помадка 2 и 4,5 кг — пакет 0,42 кг того же бренда; MAZZZA 0,5 / 4,5 кг — пакет 0,42 кг молочной MAZZZA.
  ...Object.fromEntries([["403", "479"], ["434", "479"], ["406", "482"], ["437", "482"], ["407", "483"], ["438", "483"], ["410", "486"], ["441", "486"], ["161", "486"],
    ["411", "487"], ["442", "487"], ["162", "487"], ["431", "476"], ["432", "477"], ["433", "478"], ["435", "480"], ["436", "481"], ["439", "484"],
    ["122", "485"], ["440", "485"]]),
  // MELTY (шоколад), VOYAGE (сгущёнка), VIVA (варёное молоко) — своих фото нет: пакет той же линейки Yamelly с похожим вкусом.
  ...Object.fromEntries([["163", "471"], ["163-1", "471"], ["443", "471"], ["164", "485"], ["413", "485"], ["444", "485"], ["165", "485"], ["445", "485"]]),
  // Magnolia 4,5 кг — пакет 0,5 кг того же вкуса; сгущёнка и варёная сгущёнка (своих фото нет) — молочный крем.
  ...Object.fromEntries([["451", "131"], ["452", "132"], ["453", "131"], ["454", "131"], ["455", "135"], ["133", "131"], ["134", "131"]]),
  // Shafran 4,5 кг — пакет 0,5 кг того же вкуса; Yamelly 1 кг — пакет 420 г того же вкуса.
  ...Object.fromEntries([["456", "113"], ["457", "114"], ["458", "115"], ["459", "116"], ["507", "497"], ["508", "498"], ["509", "499"]]),
  // Микс 230 г и 3 кг — пакеты Yamelly 230 г из макетов (кодов в именах нет).
  ...Object.fromEntries(["118", "130", "t118", "T130", "118-3"].map((c) => [c, { file: "Помадные конфеты/230 gr Havas/Yamelly meva 230 gr sariq.png" }])),
  "130-3": { file: "Помадные конфеты/230 gr Havas/Yamelly shokoladli 230 gr.png" },
  // Шоколад 4 кг — 0,5 кг того же бренда и цвета; Kartofelki.
  ...Object.fromEntries([["500", "038"], ["501", "039"], ["502", "040"], ["503", "041"], ["504", "042"], ["505", "043"], ["005", "004"], ["049", "004"]]),
  // Трубочки: MIS NAT по цвету этикетки, MANZUR, AZALIYA BAMBO — AZALIYA того же цвета глазури, фисташковые — вафельные трубочки 1,5 кг.
  ...Object.fromEntries([["180", "187-1"], ["267", "187-1"], ["259", "184-1"], ["189", "264"], ["189-1", "264"], ["263", "264"], ["514", "264"], ["188", "264"],
    ["194", "193"], ["178", "182"], ["179", "183"], ["474", "230"]]),
  // Кекс: Nillo mini / Brauni / Maffin других весов, Cnollcake — Nillo того же вкуса, Bonsi 1,2 кг — 1,5 кг того же вкуса, KEKO.
  ...Object.fromEntries([["330", "510"], ["496", "510"], ["523", "510"], ["510-1", "516"], ["495", "475"], ["521", "515"], ["522", "517"], ["512", "315"], ["513", "316"],
    ["339", "337"], ["340", "338"], ["319", "309"], ["331", "309"], ["332", "309"]]),
  // Печенье Frully 2 кг без этикетки — коробка Frully.
  ...Object.fromEntries(["346", "347", "348", "349"].map((c) => [c, "065"])),
  // Бамбук: ROHAT BAMBUK — Stix bamboo, ROHAT XRUSTIK — Hrustik, 494 «Bamboo» с молоком — Laytos Bamboo из макетов.
  ...Object.fromEntries(["002", "236", "238", "240", "242", "250"].map((c) => [c, "248"])),
  ...Object.fromEntries(["001", "249"].map((c) => [c, "006"])),
  "494": { file: "Экструзионный цех/Bamboo Laytos/111.png" },
  // Twizos 140 г и 35 г — пачка 200 г того же вкуса.
  ...Object.fromEntries([["601", "600"], ["602", "600"], ["604", "603"], ["605", "603"], ["610", "609"], ["611", "609"], ["613", "612"], ["614", "612"],
    ["616", "615"], ["617", "615"]]),
};

rmSync(out, { recursive: true, force: true });
mkdirSync(out, { recursive: true });
const safe = (code) => code.replace(/[^0-9A-Za-z-]/g, "_");

/** Файл → WebP всех размеров (без прозрачных полей, товар вписан в квадрат); размер самого крупного варианта. */
async function write(file, id) {
  const base = sharp(file).rotate().trim({ threshold: 1 });
  let w = 0;
  let h = 0;
  for (const size of SIZES) {
    const { data, info } = await base.clone().resize(size, size, { fit: "inside", withoutEnlargement: true }).webp({ quality: 78, alphaQuality: 85, effort: 5 })
      .toBuffer({ resolveWithObject: true });
    writeFileSync(join(out, `${id}-${size}.webp`), data);
    bytes += data.length;
    if (info.width * info.height > w * h) [w, h] = [info.width, info.height];
  }
  return { w, h };
}
const manifest = {};
const report = [];
let bytes = 0;
for (const [code, list] of [...candidates].sort(([a], [b]) => a.localeCompare(b, "en", { numeric: true }))) {
  const hasOwn = list.some((c) => c.own);
  const chosen = [];
  for (const c of [...list].filter((c) => !c.alien || !hasOwn).sort((a, b) => b.s - a.s || a.rel.localeCompare(b.rel))) {
    if (!chosen.some((x) => x.hash === c.hash) && chosen.length < 4) chosen.push(c);
  }
  const photos = [];
  for (const [i, c] of chosen.entries()) {
    const id = i === 0 ? safe(code) : `${safe(code)}_${i + 1}`;
    photos.push({ file: id, ...(await write(c.file, id)) });
  }
  manifest[code] = photos;
  report.push(`${code}  ${products.get(code).name.slice(0, 60)}\n    ${chosen.map((c, i) => `${i === 0 ? "★" : "+"} ${c.rel}`).join("\n    ")}`);
}

// Похожие: главное фото источника (код) или файл из макетов; у товара со своим фото — не ставятся.
const similarReport = [];
const fromFiles = new Map();
for (const [code, source] of Object.entries(SIMILAR)) {
  if (!products.has(code) || manifest[code]) continue;
  let item;
  if (typeof source === "string") {
    const main = manifest[source]?.[0];
    if (!main) {
      similarReport.push(`  ${code}: у ${source} нет фото — пропущен`);
      continue;
    }
    item = { ...main, similar: true, from: source };
  } else {
    if (!fromFiles.has(source.file)) {
      const id = `similar_${fromFiles.size + 1}`;
      fromFiles.set(source.file, { file: id, ...(await write(join(src, source.file), id)) });
    }
    item = { ...fromFiles.get(source.file), similar: true, from: basename(source.file).replace(/\.[^.]+$/, "") };
  }
  manifest[code] = [item];
  similarReport.push(`  ${code} ${products.get(code).name.slice(0, 55)} ← ${item.from}`);
}

writeFileSync(manifestFile, `${JSON.stringify({ sizes: SIZES, photos: manifest }, null, 0)}\n`);
console.log(`\nПохожие фото:\n${similarReport.join("\n")}`);
console.log(report.join("\n"));
console.log(`\nНе подошли (нет кода в Linko или цех не совпал с типом товара):\n  ${skipped.join("\n  ")}`);
const own = Object.values(manifest).filter((list) => !list[0].similar).length;
console.log(`\nКодов с фото: ${Object.keys(manifest).length} (своё — ${own}, похожее — ${Object.keys(manifest).length - own}), ${(bytes / 1024 / 1024).toFixed(1)} МБ`);
