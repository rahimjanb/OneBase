/**
 * Навигация модуля (ModuleNav): порядок вкладок с закреплёнными, сколько вкладок помещается в строку, поиск разделов, сохранённые
 * настройки пользователя. Без React — проверяется тестами (module-nav.test.mjs).
 */

/** Настройки панели модуля у пользователя: свой порядок разделов и закреплённые (всегда первыми, в порядке закрепления). */
export type NavPrefs = { order: string[]; pins: string[] };

export const emptyPrefs: NavPrefs = { order: [], pins: [] };

/** Ключ хранения: у каждого пользователя и модуля свой (серверных настроек пользователя в OneBase нет — localStorage). */
export const prefsKey = (userId: string, moduleId: string) => `nav:pins:${userId}:${moduleId}`;

/** Сохранённые настройки: только известные разделы, без повторов; испорченная запись — как пустая. */
export function readPrefs(raw: string | null, keys: readonly string[]): NavPrefs {
  if (!raw) return emptyPrefs;
  try {
    const data = JSON.parse(raw) as Partial<NavPrefs>;
    const clean = (list: unknown) => (Array.isArray(list) ? [...new Set(list.filter((k): k is string => typeof k === "string" && keys.includes(k)))] : []);
    return { order: clean(data.order), pins: clean(data.pins) };
  } catch {
    return emptyPrefs;
  }
}

/**
 * Порядок вкладок: сначала закреплённые (в порядке закрепления), затем остальные — в своём порядке пользователя, а разделы, которых
 * в нём нет (новые), — в порядке конфига.
 */
export function arrange(keys: readonly string[], prefs: NavPrefs): string[] {
  const pins = prefs.pins.filter((k) => keys.includes(k));
  const rest = keys.filter((k) => !pins.includes(k));
  const rank = (k: string) => {
    const i = prefs.order.indexOf(k);
    return i >= 0 ? i : prefs.order.length + keys.indexOf(k);
  };
  return [...pins, ...[...rest].sort((a, b) => rank(a) - rank(b))];
}

/** Закрепить: в конец закреплённых — в начало видимой части после ранее закреплённых. */
export const pin = (prefs: NavPrefs, key: string): NavPrefs => (prefs.pins.includes(key) ? prefs : { ...prefs, pins: [...prefs.pins, key] });

/** Открепить: раздел возвращается к своему месту среди остальных. */
export const unpin = (prefs: NavPrefs, key: string): NavPrefs => ({ ...prefs, pins: prefs.pins.filter((k) => k !== key) });

/**
 * Новый порядок из списка «Настроить панель» (перетаскивание или «выше/ниже»): закреплённые — в той же очерёдности, что в списке,
 * остальные — свой порядок.
 */
export function reorder(prefs: NavPrefs, list: readonly string[]): NavPrefs {
  return { pins: list.filter((k) => prefs.pins.includes(k)), order: list.filter((k) => !prefs.pins.includes(k)) };
}

/** Перенос пункта списка с места from на место to (для «Настроить панель»). */
export function move<T>(list: readonly T[], from: number, to: number): T[] {
  if (from === to || from < 0 || to < 0 || from >= list.length || to >= list.length) return [...list];
  const next = [...list];
  const [item] = next.splice(from, 1);
  next.splice(to, 0, item);
  return next;
}

/**
 * Сколько вкладок помещается в строку шириной available: все (без «Ещё») — если помещаются целиком, иначе столько, чтобы рядом
 * поместилась кнопка «Ещё» шириной more. Хотя бы одна вкладка — не меньше (иначе строка пустая).
 */
export function fit(widths: readonly number[], available: number, more: number, gap: number): number {
  const total = widths.reduce((sum, w) => sum + w, 0) + gap * Math.max(0, widths.length - 1);
  if (total <= available) return widths.length;
  let used = more;
  let count = 0;
  for (const w of widths) {
    const next = used + gap + w;
    if (next > available) break;
    used = next;
    count++;
  }
  return Math.max(count, Math.min(1, widths.length));
}

/** Для поиска: нижний регистр, «ё» = «е», без лишних пробелов. */
const norm = (text: string) => text.toLowerCase().replace(/ё/g, "е").replace(/\s+/g, " ").trim();

/** Поиск разделов по названию и пояснению (уже переведённым): каждое слово запроса — в начале слова названия или пояснения. */
export function search<T extends { label: string; description?: string }>(items: readonly T[], query: string): T[] {
  const words = norm(query).split(" ").filter(Boolean);
  if (words.length === 0) return [...items];
  return items.filter((item) => {
    const text = ` ${norm(`${item.label} ${item.description ?? ""}`)}`;
    return words.every((w) => text.includes(` ${w}`) || norm(item.label).includes(w));
  });
}
