/**
 * Ссылки на Яндекс Карты: маршрут по точкам в заданном порядке (на машине). На телефоне ссылка https://yandex.ru/maps/
 * открывает приложение «Яндекс Карты», если оно установлено (universal link / app link), иначе — сайт; из Узбекистана
 * yandex.ru сам перенаправляет на yandex.uz с теми же параметрами. Координаты в rtext — «широта,долгота», точки через «~».
 */
const BASE = "https://yandex.ru/maps/";

/**
 * Точек в одной ссылке, включая старт. Потолок Яндекса — 20 (справка мобильных Карт: «максимум 20 точек, включая начальную
 * и конечную»; сайт сверх 20 молча выкидывает точки). Но передачу ссылки в приложение на телефоне никто не проверял, а
 * старая справка сайта говорит о 10 — берём 10: лишняя кнопка части дешевле молча пропавших точек. Проверено на телефоне,
 * что приложение принимает 20 — можно поднять.
 */
export const YANDEX_MAX_POINTS = 10;

/** n — номер точки в маршруте (для подписи частей); по умолчанию — позиция в массиве с 1. */
export type YandexPoint = { lat: number | null; lon: number | null; n?: number };

export type YandexRouteLink = {
  url: string;
  /** Номера первой и последней точки части (номера маршрута) — для подписи «точки 1–19». */
  from: number;
  to: number;
};

type Located = { lat: number; lon: number };

const hasCoords = (p: { lat: number | null; lon: number | null }): p is Located =>
  p.lat != null && p.lon != null && Number.isFinite(p.lat) && Number.isFinite(p.lon) && !(p.lat === 0 && p.lon === 0) && Math.abs(p.lat) <= 90 && Math.abs(p.lon) <= 180;

const coord = (p: Located) => `${p.lat.toFixed(6)},${p.lon.toFixed(6)}`;

const routeUrl = (stops: string[]) => `${BASE}?rtext=${stops.join("~")}&rtt=auto`;

/** Маршрут от текущего местоположения до одной точки (кнопка «Навигатор» у точки): пустая первая точка — «от меня» в приложении. */
export function yandexNavigateTo(p: YandexPoint): string | null {
  return hasCoords(p) ? routeUrl(["", coord(p)]) : null;
}

/**
 * Маршрут по точкам в порядке массива.
 * fromHere — от текущего местоположения (агенту: оставшиеся точки), иначе от первой точки (супервайзеру: весь маршрут).
 * start — координаты телефона: идут первой точкой явно. Без них маршрут из нескольких точек начинается с первой точки: пустую
 * первую точку («~») сайт Яндекса понимает по-разному, а при передаче в приложение она теряется. Для одной точки «~» остаётся —
 * «от меня до точки» (так ссылку передаёт и сам Яндекс).
 * Больше max точек — несколько ссылок; соседние части делят крайнюю точку, чтобы маршрут не рвался.
 * Точки без координат пропускаются — их число в skipped.
 */
export function yandexRouteLinks(
  points: YandexPoint[],
  opts: { fromHere: boolean; start?: Located | null; max?: number },
): { links: YandexRouteLink[]; skipped: number } {
  const located = points.map((p, i) => ({ p, n: p.n ?? i + 1 })).filter((x) => hasCoords(x.p)) as { p: Located; n: number }[];
  const skipped = points.length - located.length;
  const size = Math.max(2, opts.max ?? YANDEX_MAX_POINTS);
  const links: YandexRouteLink[] = [];
  if (located.length === 0) {
    return { links, skipped };
  }

  const start = opts.fromHere && opts.start && hasCoords(opts.start) ? coord(opts.start) : null;
  // Одна точка без известного старта: «от меня до точки» (и агенту, и супервайзеру).
  if (located.length === 1 && start === null) {
    const only = located[0];
    return { links: [{ url: routeUrl(["", coord(only.p)]), from: only.n, to: only.n }], skipped };
  }

  let begin = 0;
  let first = true;
  while (begin < located.length) {
    // Первая часть от телефона: одно место в ссылке занимает текущее местоположение.
    const here = first && start !== null;
    const take = here ? size - 1 : size;
    const chunk = located.slice(begin, begin + take);
    // Продолжение без новых точек (часть совпала бы с концом предыдущей) не нужно.
    if (!first && chunk.length < 2) break;
    links.push({ url: routeUrl([...(here ? [start] : []), ...chunk.map((x) => coord(x.p))]), from: chunk[0].n, to: chunk[chunk.length - 1].n });
    if (begin + take >= located.length) break;
    // Следующая часть начинается с последней точки этой.
    begin += take - 1;
    first = false;
  }

  return { links, skipped };
}
