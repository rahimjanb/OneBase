/**
 * Геолокация телефона. Разрешение помнит браузер; приложение не спрашивает его само по себе — только по нажатию
 * (визит, маршрут, «моё местоположение»), заранее проверяет состояние и не дёргает API лишний раз: свежие координаты
 * переиспользуются. Отказ отличается от таймаута, чтобы интерфейс мог показать, как включить доступ.
 * Chrome после трёх закрытых окон разрешения блокирует геолокацию сайта на неделю — поэтому никаких запросов при открытии
 * страниц, а после двух закрытых без ответа окон приложение само больше не спрашивает: только кнопкой «Разрешить
 * геолокацию» в «Уведомления и геолокация» (force).
 */

export type GeoState = "granted" | "denied" | "prompt" | "unsupported";
export type GeoFix = { latitude: number; longitude: number; accuracy: number; at: number };
/** dismissed — окно разрешения дважды закрыли без ответа, и без force приложение его больше не показывает. */
export type GeoOutcome = { position: GeoFix | null; reason: null | "denied" | "dismissed" | "timeout" | "unavailable" | "unsupported" };

const DISMISS_KEY = "onebase-geo-dismissed";

let last: GeoFix | null = null;
let inflight: Promise<GeoOutcome> | null = null;

/** Состояние разрешения. iOS отвечает «prompt», пока в этой загрузке страницы геолокацией не пользовались. */
export async function geoPermission(): Promise<GeoState> {
  if (typeof navigator === "undefined" || !navigator.geolocation) return "unsupported";
  try {
    const status = await navigator.permissions?.query({ name: "geolocation" });
    return (status?.state as GeoState | undefined) ?? "prompt";
  } catch {
    return "prompt";
  }
}

/** Последние координаты этого сеанса, если они не старше maxAgeMs. */
export function lastFix(maxAgeMs = 60_000): GeoFix | null {
  return last && Date.now() - last.at <= maxAgeMs ? last : null;
}

/**
 * Координаты по действию пользователя. Свежие (reuseMs) — без нового запроса; одновременно — один запрос.
 * Пока на экране может быть окно разрешения, запасной таймер ждёт дольше: медленное «Разрешить» не превращается в «нет GPS»
 * (timeout самого браузера время окна не считает). force — спросить, даже если окно уже дважды закрывали.
 */
export function locate({ timeoutMs = 12_000, reuseMs = 20_000, force = false }: { timeoutMs?: number; reuseMs?: number; force?: boolean } = {}): Promise<GeoOutcome> {
  const fresh = lastFix(reuseMs);
  if (fresh) return Promise.resolve({ position: fresh, reason: null });
  if (inflight) return inflight;
  inflight = (async (): Promise<GeoOutcome> => {
    if (typeof navigator === "undefined" || !navigator.geolocation) return { position: null, reason: "unsupported" };
    const state = await geoPermission();
    if (state === "denied") return { position: null, reason: "denied" };
    if (state === "prompt" && !force && geoDismissals() >= 2) return { position: null, reason: "dismissed" };
    const limit = state === "granted" ? timeoutMs : Math.max(timeoutMs, 45_000);
    return new Promise<GeoOutcome>((resolve) => {
      let done = false;
      const timer = setTimeout(() => {
        if (done) return;
        done = true;
        resolve({ position: null, reason: "timeout" });
      }, limit + 500);
      navigator.geolocation.getCurrentPosition(
        (p) => {
          if (done) return;
          done = true;
          clearTimeout(timer);
          last = { latitude: p.coords.latitude, longitude: p.coords.longitude, accuracy: p.coords.accuracy, at: Date.now() };
          clearDismissed();
          resolve({ position: last, reason: null });
        },
        (e) => {
          if (done) return;
          done = true;
          clearTimeout(timer);
          if (e.code === e.PERMISSION_DENIED) void noteDismissal();
          resolve({ position: null, reason: e.code === e.PERMISSION_DENIED ? "denied" : e.code === e.TIMEOUT ? "timeout" : "unavailable" });
        },
        { enableHighAccuracy: true, timeout: timeoutMs, maximumAge: 30_000 },
      );
    });
  })().finally(() => {
    inflight = null;
  });
  return inflight;
}

/** Отказ без решения (окно просто закрыли) — запоминаем: после двух таких показываем инструкцию, а не новое окно. */
async function noteDismissal() {
  if ((await geoPermission()) !== "prompt") return;
  try {
    const count = Number(localStorage.getItem(DISMISS_KEY) ?? 0) + 1;
    localStorage.setItem(DISMISS_KEY, String(count));
  } catch {
    // хранилище недоступно
  }
}

function clearDismissed() {
  try {
    localStorage.removeItem(DISMISS_KEY);
  } catch {
    // хранилище недоступно
  }
}

export function geoDismissals(): number {
  try {
    return Number(localStorage.getItem(DISMISS_KEY) ?? 0);
  } catch {
    return 0;
  }
}
