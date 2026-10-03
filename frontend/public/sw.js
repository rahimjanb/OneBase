// Service worker OneBase: только офлайн-заглушка для переходов между страницами.
// Данные и страницы не кэшируются — приложение всегда показывает свежие цифры с сервера.
const CACHE = "onebase-offline-v1";
const OFFLINE_URL = "/offline.html";

self.addEventListener("install", (event) => {
  event.waitUntil(
    caches
      .open(CACHE)
      .then((cache) => cache.addAll([OFFLINE_URL, "/icons/icon-192.png"]))
      .then(() => self.skipWaiting()),
  );
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) => Promise.all(keys.filter((key) => key !== CACHE).map((key) => caches.delete(key))))
      .then(() => self.clients.claim()),
  );
});

self.addEventListener("fetch", (event) => {
  if (event.request.mode !== "navigate") return;
  // Переход: сеть, а если её нет — страница «Нет соединения». Перенаправления (например, на вход) браузер выполняет сам.
  event.respondWith(fetch(event.request).catch(() => caches.match(OFFLINE_URL)));
});
