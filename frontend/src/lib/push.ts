/**
 * Push-уведомления в браузере (Web Push). Разрешение браузер помнит сам — приложение спрашивает его только по нажатию
 * «Включить уведомления», а при каждом запуске тихо проверяет, что подписка этого устройства есть и принадлежит
 * вошедшему пользователю. На iPhone push работает только у приложения, добавленного на экран «Домой».
 */

export type PushSupport = "supported" | "ios-install" | "unsupported";

const PROMPT_KEY = "onebase-push-prompt";
/** Пользователь сам отключил уведомления на этом устройстве — тихая проверка при запуске их снова не включает. */
const OFF_KEY = "onebase-push-off";

function setOptOut(off: boolean) {
  try {
    if (off) localStorage.setItem(OFF_KEY, "1");
    else localStorage.removeItem(OFF_KEY);
  } catch {
    // хранилище недоступно
  }
}

function optedOut(): boolean {
  try {
    return localStorage.getItem(OFF_KEY) === "1";
  } catch {
    return false;
  }
}

export function isStandalone(): boolean {
  if (typeof window === "undefined") return false;
  return window.matchMedia("(display-mode: standalone)").matches || (navigator as Navigator & { standalone?: boolean }).standalone === true;
}

export function isIos(): boolean {
  if (typeof navigator === "undefined") return false;
  return /iPad|iPhone|iPod/.test(navigator.userAgent) || (navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1);
}

export function pushSupport(): PushSupport {
  if (typeof window === "undefined") return "unsupported";
  if (isIos() && !isStandalone()) return "ios-install";
  return "serviceWorker" in navigator && "PushManager" in window && "Notification" in window ? "supported" : "unsupported";
}

/** Разрешение на уведомления; на iPhone в Safari вне приложения Notification нет вовсе. */
export function notificationPermission(): NotificationPermission | "unsupported" {
  return typeof Notification === "undefined" ? "unsupported" : Notification.permission;
}

let keyPromise: Promise<string> | null = null;

/** Открытый ключ VAPID — загружаем заранее, чтобы по нажатию сразу звать subscribe (iOS требует жест). */
export function prefetchPushKey(): Promise<string> {
  keyPromise ??= fetch("/bff/api/push/key")
    .then((r) => (r.ok ? r.json() : Promise.reject(new Error(`push key ${r.status}`))))
    .then((j: { publicKey: string }) => j.publicKey);
  keyPromise.catch(() => {
    keyPromise = null;
  });
  return keyPromise;
}

function keyBytes(base64url: string): Uint8Array<ArrayBuffer> {
  const padded = (base64url + "=".repeat((4 - (base64url.length % 4)) % 4)).replace(/-/g, "+").replace(/_/g, "/");
  const raw = atob(padded);
  const bytes = new Uint8Array(new ArrayBuffer(raw.length));
  for (let i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i);
  return bytes;
}

function sameKey(subscription: PushSubscription, key: string): boolean {
  const current = subscription.options?.applicationServerKey;
  if (!current) return true;
  const a = new Uint8Array(current);
  const b = keyBytes(key);
  return a.length === b.length && a.every((v, i) => v === b[i]);
}

async function saveSubscription(subscription: PushSubscription) {
  const json = subscription.toJSON();
  const response = await fetch("/bff/api/push/subscriptions", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ endpoint: json.endpoint, keys: json.keys, origin: location.origin, userAgent: navigator.userAgent }),
  });
  if (!response.ok) throw new Error(`push save ${response.status}`);
}

/**
 * Подписать это устройство (разрешение уже выдано) и сохранить подписку за вошедшим пользователем.
 * stopped — пока ждали браузер, уведомления на устройстве отключили («Отключить»): подписку не оставляем.
 */
async function subscribe(key: string, stopped: () => boolean = () => false) {
  const registration = await navigator.serviceWorker.ready;
  if (stopped()) return;
  let subscription = await registration.pushManager.getSubscription();
  // Подписка со старым ключом не примет наши уведомления — переподписываемся.
  if (subscription && !sameKey(subscription, key)) {
    await subscription.unsubscribe();
    subscription = null;
  }
  subscription ??= await registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: keyBytes(key) });
  if (stopped()) {
    await subscription.unsubscribe().catch(() => undefined);
    return;
  }
  await saveSubscription(subscription);
}

export type EnableResult = NotificationPermission | "unsupported" | "error";

/** По нажатию «Включить уведомления»: спросить разрешение (первым действием — так требует iOS) и подписаться. */
export async function enablePush(): Promise<EnableResult> {
  if (pushSupport() !== "supported") return "unsupported";
  const key = prefetchPushKey();
  const permission = Notification.permission === "granted" ? "granted" : await Notification.requestPermission();
  if (permission === "granted") setOptOut(false);
  if (permission !== "granted") {
    if (permission === "default") rememberPromptDismissed();
    return permission;
  }
  try {
    await subscribe(await key);
    return "granted";
  } catch {
    return "error";
  }
}

/**
 * При запуске приложения: разрешение уже есть — тихо проверить подписку (без запроса) и привязать её к пользователю.
 * Если на этом устройстве уведомления отключили кнопкой «Отключить», не подписываем снова.
 */
export async function ensurePush(): Promise<void> {
  if (pushSupport() !== "supported" || Notification.permission !== "granted" || optedOut()) return;
  try {
    await subscribe(await prefetchPushKey(), optedOut);
  } catch {
    // push-сервис или сеть недоступны — попробуем при следующем запуске
  }
}

/**
 * Удалить подписку устройства — на сервере и в браузере одновременно: запрос с keepalive дойдёт до сервера и после
 * ухода со страницы (выход), а отписка в браузере не ждёт ответа сервера на медленной связи.
 */
async function dropSubscription(): Promise<void> {
  if (typeof navigator === "undefined" || !("serviceWorker" in navigator)) return;
  try {
    const registration = await navigator.serviceWorker.getRegistration();
    const subscription = await registration?.pushManager.getSubscription();
    if (!subscription) return;
    const server = fetch(`/bff/api/push/subscriptions?endpoint=${encodeURIComponent(subscription.endpoint)}`, { method: "DELETE", keepalive: true }).catch(() => undefined);
    await Promise.all([subscription.unsubscribe().catch(() => undefined), server]);
  } catch {
    // нечего удалять
  }
}

/** «Отключить уведомления» на этом устройстве; выбор запоминается — запуск приложения не включит их снова. */
export async function disablePush(): Promise<void> {
  setOptOut(true);
  await dropSubscription();
}

/** Перед выходом: устройство больше не получает уведомления этого сотрудника (общий телефон). Не дольше timeout. */
export async function forgetDeviceBeforeLogout(timeoutMs = 1500): Promise<void> {
  await Promise.race([dropSubscription(), new Promise((resolve) => setTimeout(resolve, timeoutMs))]);
}

/** Проверочное уведомление себе. Ошибка — с текстом сервера, если он есть (например, «повторить через полминуты»). */
export async function sendTestPush(): Promise<number> {
  const response = await fetch("/bff/api/push/test", { method: "POST" }).catch(() => null);
  if (response?.ok) return ((await response.json()) as { devices: number }).devices;
  const body = response ? ((await response.json().catch(() => null)) as { error?: unknown } | null) : null;
  throw new Error(typeof body?.error === "string" ? body.error : "Не удалось отправить проверку.");
}

/** Баннер «Включите уведомления» не показываем 14 дней после «Не сейчас» или закрытого окна разрешения. */
export function rememberPromptDismissed() {
  try {
    localStorage.setItem(PROMPT_KEY, String(Date.now()));
  } catch {
    // хранилище недоступно
  }
}

export function promptRecentlyDismissed(): boolean {
  try {
    const at = Number(localStorage.getItem(PROMPT_KEY) ?? 0);
    return at > 0 && Date.now() - at < 14 * 24 * 3600 * 1000;
  } catch {
    return false;
  }
}
