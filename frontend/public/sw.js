// Service worker OneBase и Sales Base.
// OneBase: только офлайн-заглушка для переходов — данные не кэшируются, приложение всегда показывает свежие цифры.
// Sales Base (sales.* или /field): «сначала сеть, потом кэш» для страниц и данных — агент без связи откроет уже
// загруженный маршрут, задачи и карточки точек. Кэш очищается при выходе (/logout), на странице входа, при ответе 401
// или переадресации на вход и когда на устройстве сменился пользователь.
const CACHE = "onebase-offline-v6";
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

// ── Push-уведомления (Web Push) ──

/** Уведомление показываем всегда: без него Safari отзывает разрешение, а Chrome показывает своё «сайт обновлён в фоне». */
self.addEventListener("push", (event) => {
  let data = {};
  try {
    data = event.data ? event.data.json() : {};
  } catch {
    try {
      data = { notification: { body: event.data.text() } };
    } catch {
      data = {};
    }
  }
  const n = data.notification || data;
  const url = typeof n.navigate === "string" && n.navigate ? n.navigate : isFieldHost() ? "/field/notifications" : "/";
  const field = isFieldHost() || url.includes("/field");
  const icon = field ? "/icons/field-192.png" : "/icons/icon-192.png";
  event.waitUntil(
    (async () => {
      await self.registration.showNotification(n.title || (field ? "Sales Base" : "OneBase"), {
        body: n.body || "",
        icon,
        badge: icon,
        tag: n.tag || undefined,
        lang: "ru",
        data: { url },
      });
      // Открытые окна перечитывают данные: новая задача сразу появляется в списке и в колокольчике.
      const windows = await self.clients.matchAll({ type: "window", includeUncontrolled: true });
      for (const w of windows) w.postMessage({ type: "push-received" });
    })(),
  );
});

/** Страница приложения отвечает, что перейдёт сама (AppBridge); нет ответа — у окна нет оболочки (вход, офлайн, ошибка). */
function askToNavigate(client, url) {
  return new Promise((resolve) => {
    const channel = new MessageChannel();
    const timer = setTimeout(() => resolve(false), 1000);
    channel.port1.onmessage = () => {
      clearTimeout(timer);
      resolve(true);
    };
    client.postMessage({ type: "navigate", url }, [channel.port2]);
  });
}

/** Нажали на уведомление: открыть нужную страницу в уже открытом окне приложения, иначе — новое окно. */
self.addEventListener("notificationclick", (event) => {
  event.notification.close();
  const target = new URL((event.notification.data && event.notification.data.url) || "/", self.location.origin);
  event.waitUntil(
    (async () => {
      // Страница другого сайта (задача OneBase в уведомлении Sales Base) — новым окном: окна чужого сайта worker не видит.
      if (target.origin !== self.location.origin) {
        await self.clients.openWindow(target.href);
        return;
      }
      // Только окна под этим worker — их можно перевести на другую страницу. Право открыть окно даётся на одно действие,
      // и focus его расходует, поэтому без подходящего окна сразу открываем новое.
      const windows = await self.clients.matchAll({ type: "window" });
      const client = windows.find((w) => w.focused) || windows.find((w) => w.visibilityState === "visible") || windows[0];
      if (!client) {
        await self.clients.openWindow(target.href);
        return;
      }
      await client.focus().catch(() => undefined);
      // Переход внутри приложения: Next.js загрузит только данные. Окно без оболочки не ответит — переводим его само.
      if (await askToNavigate(client, target.pathname + target.search)) return;
      await client.navigate(target.href).catch(() => undefined);
    })(),
  );
});

function urlBase64ToBytes(base64) {
  const padded = (base64 + "=".repeat((4 - (base64.length % 4)) % 4)).replace(/-/g, "+").replace(/_/g, "/");
  const raw = atob(padded);
  return Uint8Array.from(raw, (c) => c.charCodeAt(0));
}

/** Браузер сменил подписку сам — подписываемся заново и сохраняем за вошедшим пользователем (cookie сессии). */
self.addEventListener("pushsubscriptionchange", (event) => {
  event.waitUntil(
    (async () => {
      try {
        const keyResponse = await fetch("/bff/api/push/key");
        if (!keyResponse.ok) return;
        const { publicKey } = await keyResponse.json();
        const subscription = await self.registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: urlBase64ToBytes(publicKey) });
        const json = subscription.toJSON();
        await fetch("/bff/api/push/subscriptions", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ endpoint: json.endpoint, keys: json.keys, origin: self.location.origin }),
        });
      } catch {
        // при следующем запуске приложение проверит подписку само
      }
    })(),
  );
});
