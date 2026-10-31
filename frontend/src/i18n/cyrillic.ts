/**
 * Узбекский: латиница → кириллица по правилам орфографии 1995 года (то же правило — в backend: OneBase.Application.Abstractions.UzbekCyrillic).
 * oʻ → ў, gʻ → ғ, sh → ш, ch → ч, ya / yo / yu / ye → я / ё / ю / е, e в начале слова и после гласной → э, тутуқ белгиси (ʼ) → ъ,
 * h → ҳ, q → қ, x → х. Русские заимствования — по корням («aksiya» → «акция», «sentabr» → «сентябр», «-tsiya» → «-ция»).
 * Латиницей остаются: подстановки {name}, ссылки, почта, @имена, идентификаторы (sales.read), бренды и технические сокращения (OneBase,
 * Linko, PDF, KPI), слова в верблюжьем регистре, английские слова (в узбекской латинице нет «c» без «h» и «w»), слова рядом с цифрами
 * (P50, 1Base), «P&L», «Ctrl+C», «A–Z». Узбекские сокращения (TP, AKB, SVR, QQS, JShShIR) — кириллицей, окончания после
 * латинского сокращения — кириллицей («PDFni» → «PDFни»). Кириллица и цифры проходят как есть.
 */

/** Остаются латиницей (без учёта регистра): бренды, технические и английские слова. */
const KEEP = new Set([
  "onebase", "linko", "telegram", "excel", "word", "pdf", "png", "jpg", "jpeg", "csv", "xlsx", "docx", "kpi", "dso", "sku", "id", "api", "url",
  "ai", "gpt", "openai", "claude", "anthropic", "google", "gmail", "verifix", "windows", "android", "ios", "iphone", "bitrix", "bitrix24", "qr",
  "sms", "pin", "ip", "http", "https", "pwa", "json", "sql", "markdown", "twizos", "email", "vpn", "usb", "docker", "chrome", "safari",
  "edge", "yandex", "cloudflare", "webdav", "smb", "nas", "mp3", "mp4", "zip", "html", "ssl", "dns", "utc", "gmt", "wi", "fi", "mb", "kb", "gb",
  "fcm", "apns", "macos", "linux", "nginx", "minio", "postgres", "postgresql", "sheets", "drive", "outlook", "whatsapp", "instagram",
  "facebook", "youtube", "skype", "zoom", "teams", "slack", "notion", "jira", "trello", "asana", "github", "gitlab", "hr", "crm", "erp", "sfa",
  "b2b", "b2c", "ceo", "cfo", "okr", "faq", "mvp", "ux", "ui", "abc", "xyz", "ebit", "ebitda", "roi", "enter", "esc", "tab", "shift", "ctrl",
  "alt", "cmd", "true", "false", "null", "env", "localhost", "oauth", "tools", "about", "reasoning", "sync", "month", "months", "bought", "year",
  "dealer", "assignee", "team", "reassign", "employee", "early", "read", "you", "appear", "data", "manager", "docs", "shoubox", "gps", "usd",
  "search", "oos", "spreadsheet", "spreadsheets", "yyyy", "system", "hklm", "vapid", "iana", "fifo", "peer", "flood", "export",
]);

/** Узбекские сокращения со строчными буквами внутри (диграфы) — целиком. */
const UZ_ABBR: [string, string][] = [
  ["JShShIR", "ЖШШИР"],
  ["MChJ", "МЧЖ"],
  ["YaTT", "ЯТТ"],
];

/**
 * Узбекские сокращения ЗАГЛАВНЫМИ (2–3 буквы) — кириллицей; другие короткие (PDF, SKU, DOC) — латиницей. Слова заглавными от 4 букв
 * («HAFTA», «ARBUZ») — кириллицей, кроме английских и технических (KEEP).
 */
const UZ_CAPS = new Set([
  "TP", "RM", "AKB", "ROP", "SVR", "SV", "SN", "KPU", "DMP", "TT", "TOP", "QQS", "STIR", "XATO", "SINOV", "JO'NATMASI", "DIQQAT", "OGOHLANTIRISH", "OK",
]);

/** Русские заимствования: латинский корень → кириллический (дальше — по правилам). Длинные — первыми. */
const STEMS: [string, string][] = [
  ["kompaniya", "компания"],
  ["sentabr", "сентябр"],
  ["oktabr", "октябр"],
  ["noyabr", "ноябр"],
  ["dekabr", "декабр"],
  ["yanvar", "январ"],
  ["fevral", "феврал"],
  ["aksiya", "акция"],
  ["reestr", "реестр"],
  ["iyun", "июн"],
  ["iyul", "июл"],
  ["iyn", "июн"],
  ["iyl", "июл"],
];

/** Сочетания внутри слова: «-ksiya» → «-кция» (редакция, коллекция), «-tsiya» → «-ция» (операция, интеграция). */
const INFIXES: [string, string][] = [
  ["tsiya", "ция"],
  ["ksiya", "кция"],
];

const LETTERS: Record<string, string> = {
  a: "а", b: "б", c: "с", d: "д", e: "е", f: "ф", g: "г", h: "ҳ", i: "и", j: "ж", k: "к", l: "л", m: "м", n: "н", o: "о", p: "п",
  q: "қ", r: "р", s: "с", t: "т", u: "у", v: "в", w: "в", x: "х", y: "й", z: "з",
};

const VOWELS = new Set(["a", "e", "i", "o", "u", "ў"]);
const APOSTROPHES = "'ʻʼ’‘`";
const isApostrophe = (c: string) => c !== "" && APOSTROPHES.includes(c);

/** Английское слово: в узбекской латинице нет «c» без «h», «w», «ph», «ck» и «th» в начале. */
const ENGLISH = /c(?!h)|w|ph|ck|^th/i;

/** Одно слово в нижнем регистре (буквы и апострофы внутри) → кириллица. */
function wordToCyrillic(lower: string): string {
  for (const [latin, cyr] of STEMS) {
    if (lower.startsWith(latin)) return cyr + wordToCyrillic(lower.slice(latin.length)).replace(/^э/, "е");
  }

  let out = "";
  let prev = ""; // предыдущая буква (для «e» после гласной)
  for (let i = 0; i < lower.length; ) {
    const rest = lower.slice(i);
    const infix = INFIXES.find(([latin]) => rest.startsWith(latin));
    if (infix) {
      out += infix[1];
      i += infix[0].length;
      prev = "a";
      continue;
    }

    const c = lower[i];
    const next = lower[i + 1] ?? "";
    if ((c === "o" || c === "g") && isApostrophe(next)) {
      out += c === "o" ? "ў" : "ғ";
      prev = c === "o" ? "ў" : "g";
      i += 2;
      continue;
    }

    if (isApostrophe(c)) {
      out += "ъ";
      prev = "";
      i += 1;
      continue;
    }

    if ((c === "s" || c === "c") && next === "h") {
      out += c === "s" ? "ш" : "ч";
      prev = "h";
      i += 2;
      continue;
    }

    if (c === "y" && next !== "" && "aeu".includes(next)) {
      out += next === "a" ? "я" : next === "u" ? "ю" : "е";
      prev = next;
      i += 2;
      continue;
    }

    if (c === "y" && next === "o" && !isApostrophe(lower[i + 2] ?? "")) {
      out += "ё";
      prev = "o";
      i += 2;
      continue;
    }

    if (c === "e") {
      out += i === 0 || VOWELS.has(prev) ? "э" : "е";
      prev = "e";
      i += 1;
      continue;
    }

    out += LETTERS[c] ?? c;
    prev = c;
    i += 1;
  }

  return out;
}

const upper = (latin: string) => wordToCyrillic(latin.toLowerCase()).toUpperCase();
const tail = (latin: string) => (latin ? wordToCyrillic(latin.toLowerCase()) : "");

/** Слово латиницей → кириллица или как есть (бренд, сокращение, английское слово, ВерблюжийРегистр). */
function convertWord(word: string): string {
  const lower = word.toLowerCase();
  // «Linko'da», «OneBase'da»: бренд остаётся, узбекское окончание — кириллицей.
  const apostrophe = word.search(/['ʻʼ’‘`]/);
  if (apostrophe > 0) {
    const head = word.slice(0, apostrophe);
    if (KEEP.has(head.toLowerCase()) || /[a-z][A-Z]/.test(head) || ENGLISH.test(head)) return head + "'" + tail(word.slice(apostrophe + 1));
  }

  if (KEEP.has(lower)) return word;
  for (const [abbr, cyr] of UZ_ABBR) {
    if (word.startsWith(abbr)) return cyr + tail(word.slice(abbr.length));
  }

  const letters = word.replace(/[^A-Za-z]/g, "");
  if (letters.length > 1 && letters === letters.toUpperCase()) {
    return UZ_CAPS.has(word.replace(/[ʻʼ’‘`]/g, "'")) || (letters.length >= 4 && !ENGLISH.test(word)) ? upper(word) : word;
  }

  // Сокращение с окончанием: «TPlarining» → «ТПларининг», «PDFni» → «PDFни».
  const head = /^[A-Z]{2,}(?=[a-z])/.exec(word)?.[0];
  if (head) return (UZ_CAPS.has(head) ? upper(head) : head) + tail(word.slice(head.length));
  if (/[a-z][A-Z]/.test(word) || ENGLISH.test(word)) return word;

  const cyr = wordToCyrillic(lower);
  return /^[A-Z]/.test(word) ? cyr.charAt(0).toUpperCase() + cyr.slice(1) : cyr;
}

/**
 * Куски, которые не переводятся: {подстановки}, ссылки, почта, @имена, идентификаторы через точку или подчёркивание (строчными, части от
 * 2 знаков: sales.read, telegram.manage), «P&L», «Ctrl+C», «A–Z», ключи настроек (Telegram:Enabled=false), коды ошибок (PEER_FLOOD). «F.I.Sh.» — сокращение, переводится.
 */
const PROTECTED =
  /\{[^{}]*\}|https?:\/\/\S+|www\.\S+|[\w.+-]+@[\w-]+\.[\w.]+|@\w+|\b[a-z][a-z0-9]+(?:[._][A-Za-z0-9]{2,})+\b|\b[A-Z]&[A-Z]\b|\b(?:Ctrl|Alt|Shift|Cmd|Win)\+\S+|\bA[–-]Z\b|\b[A-Za-z]+:[A-Za-z]+=\S+|\b[A-Z][A-Z0-9]*_[A-Z0-9_]+\b/g;

/** Слово: латинские буквы с апострофами внутри (oʻ, gʻ, maʼlumot). */
const WORD = /[A-Za-z](?:[A-Za-z]|['ʻʼ’‘`](?=[A-Za-z]))*/g;

/** Кусок без защищённых мест: слова переводятся, слово рядом с цифрой («P50», «1Base», «y2») — как есть. */
const convertFree = (part: string) =>
  part.replace(WORD, (w: string, offset: number, whole: string) =>
    (offset > 0 && /\d/.test(whole[offset - 1])) || /\d/.test(whole[offset + w.length] ?? "") ? w : convertWord(w),
  );

/** Текст узбекской латиницей → узбекская кириллица; русский текст, цифры и защищённые куски не меняются. */
export function toCyrillic(text: string): string {
  if (!/[A-Za-z]/.test(text)) return text;
  let out = "";
  let last = 0;
  for (const m of text.matchAll(PROTECTED)) {
    out += convertFree(text.slice(last, m.index)) + m[0];
    last = m.index! + m[0].length;
  }

  return out + convertFree(text.slice(last));
}
