// Service worker OneBase и Sales Base.
// OneBase: только офлайн-заглушка для переходов — данные не кэшируются, приложение всегда показывает свежие цифры.
// Sales Base (sales.* или /field): «сначала сеть, потом кэш» для страниц и данных — агент без связи откроет уже
// загруженный маршрут, задачи и карточки точек. Кэш очищается при выходе (/logout), на странице входа, при ответе 401
// или переадресации на вход и когда на устройстве сменился пользователь.
const CACHE = "onebase-offline-v5";
// v2: кэш v1 (до учёта пользователя) удаляется при активации.
const FIELD_CACHE = "sales-base-v2";
const USER_CACHE = "sales-base-user";
const USER_KEY = "/__sales-base-user";
const OFFLINE_URL = "/offline.html";
// Офлайн-страница и её иконки: без сети отдаются из кэша (иначе на «Нет соединения» пропадает логотип).
const PRECACHE = [OFFLINE_URL, "/icons/icon-192.png", "/icons/field-192.png"];
const FIELD_LIMIT = 80;

const isFieldHost = () => self.location.hostname.startsWith("sales.");
const isFieldPage = (url) => url.origin === self.location.origin && (isFieldHost() ? !url.pathname.startsWith("/login") : url.pathname === "/field" || url.pathname.startsWith("/field/"));
const isFieldData = (url) => url.origin === self.location.origin && url.pathname.startsWith("/bff/api/field/") && !url.pathname.includes("/photo");

self.addEventListener("install", (event) => {
  event.waitUntil(
    caches
      .open(CACHE)
      .then((cache) => cache.addAll(PRECACHE))
      .then(() => self.skipWaiting()),
  );
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) => Promise.all(keys.filter((key) => key !== CACHE && key !== FIELD_CACHE && key !== USER_CACHE).map((key) => caches.delete(key))))
      .then(() => self.clients.claim()),
  );
});

async function trim(cache) {
  const keys = await cache.keys();
  for (let i = 0; i < keys.length - FIELD_LIMIT; i++) await cache.delete(keys[i]);
}

const clearField = () => Promise.all([caches.delete(FIELD_CACHE), caches.delete(USER_CACHE)]);
const isLogin = (url) =>
  url.pathname === "/logout" || url.pathname === "/login" || url.pathname.startsWith("/login/") || url.pathname === "/field/login" || url.pathname.startsWith("/field/login/");

/** Сеть; успешный ответ — в кэш Sales Base; нет сети — из кэша, иначе fallback. Сессия кончилась (401 или переадресация на вход) — кэш очищается. */
async function networkFirst(request, fallback) {
  const cache = await caches.open(FIELD_CACHE);
  try {
    const response = await fetch(request);
    if (response.status === 401 || (response.redirected && isLogin(new URL(response.url)))) {
      await clearField();
      return response;
    }
    // Сервер упал или Cloudflare отдал 52x — лучше последняя сохранённая копия, чем страница ошибки.
    if (response.status >= 500) {
      const cached = await cache.match(request, { ignoreVary: true });
      if (cached) return cached;
    }
    if (response.ok && !response.redirected && response.type === "basic") {
      cache.put(request, response.clone()).then(() => trim(cache));
    }
    return response;
  } catch (error) {
    const cached = await cache.match(request, { ignoreVary: true });
    if (cached) return cached;
    if (fallback) return fallback();
    throw error;
  }
}

// Страница Sales Base сообщает, кто вошёл; другой пользователь — кэш предыдущего удаляется.
self.addEventListener("message", (event) => {
  const data = event.data;
  if (!data || data.type !== "sales-base-user" || typeof data.id !== "string") return;
  event.waitUntil(
    (async () => {
      const users = await caches.open(USER_CACHE);
      const stored = await users.match(USER_KEY);
      const previous = stored ? await stored.text() : null;
      if (previous === data.id) return;
      // previous = null: кэш очищен при входе — всё, что в нём сейчас, уже этого пользователя.
      if (previous !== null) await caches.delete(FIELD_CACHE);
      await users.put(USER_KEY, new Response(data.id));
    })(),
  );
});

self.addEventListener("fetch", (event) => {
  const request = event.request;
  if (request.method !== "GET") return;
  const url = new URL(request.url);

  if (request.mode === "navigate") {
    // Выход и страница входа — чистим кэш Sales Base: на устройстве не должно остаться чужих данных.
    if (url.origin === self.location.origin && isLogin(url)) {
      event.respondWith(clearField().then(() => fetch(request)).catch(() => caches.match(OFFLINE_URL)));
      return;
    }
    if (isFieldPage(url)) {
      event.respondWith(networkFirst(request, () => caches.match(OFFLINE_URL)));
      return;
    }
    // Переход в OneBase: сеть, а если её нет — страница «Нет соединения». Перенаправления браузер выполняет сам.
    event.respondWith(fetch(request).catch(() => caches.match(OFFLINE_URL)));
    return;
  }

  if (isFieldData(url)) {
    event.respondWith(networkFirst(request));
    return;
  }

  if (url.origin === self.location.origin && PRECACHE.includes(url.pathname)) {
    event.respondWith(fetch(request).catch(() => caches.match(url.pathname)));
  }
});
