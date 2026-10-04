"use client";

import { useCallback, useEffect, useState } from "react";
import { Bell, BellOff, CheckCircle2, MapPin, Smartphone } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { geoPermission, locate, type GeoState } from "@/lib/geo";
import { disablePush, enablePush, isIos, notificationPermission, prefetchPushKey, pushSupport, sendTestPush, type PushSupport } from "@/lib/push";

const button =
  "inline-flex h-10 items-center justify-center gap-2 rounded-lg border border-line bg-surface px-3.5 text-sm font-medium text-ink hover:bg-muted disabled:opacity-50";
const primary = "inline-flex h-10 items-center justify-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50";

function Status({ ok, children }: { ok: boolean | null; children: React.ReactNode }) {
  return (
    <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${ok === true ? "bg-ok-soft text-ok" : ok === false ? "bg-bad-soft text-bad" : "bg-muted text-ink-3"}`}>{children}</span>
  );
}

/**
 * Разрешения этого устройства: уведомления и геолокация. Браузер помнит решение сам — здесь видно, что он запомнил,
 * можно включить (окно разрешения — только по нажатию) и прочитать, как вернуть доступ, если он запрещён.
 */
export function PermissionsSheet({ open, onClose, appName }: { open: boolean; onClose: () => void; appName: string }) {
  const [support, setSupport] = useState<PushSupport>("unsupported");
  const [notify, setNotify] = useState<NotificationPermission | "unsupported">("unsupported");
  const [subscribed, setSubscribed] = useState(false);
  const [geo, setGeo] = useState<GeoState>("prompt");
  // Браузер отказал при попытке, хотя сообщает «не задано» (iPhone до первого определения места) — показываем, как включить.
  const [geoBlocked, setGeoBlocked] = useState(false);
  const [busy, setBusy] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const ios = typeof navigator !== "undefined" && isIos();

  const refresh = useCallback(async () => {
    setSupport(pushSupport());
    setNotify(notificationPermission());
    setGeo(await geoPermission());
    try {
      const registration = "serviceWorker" in navigator ? await navigator.serviceWorker.getRegistration() : undefined;
      setSubscribed(!!(await registration?.pushManager?.getSubscription()));
    } catch {
      setSubscribed(false);
    }
  }, []);

  useEffect(() => {
    if (!open) return;
    setMessage(null);
    setGeoBlocked(false);
    void refresh();
    if (pushSupport() === "supported") void prefetchPushKey().catch(() => undefined);
  }, [open, refresh]);

  const turnOn = async () => {
    setBusy("notify");
    const result = await enablePush();
    setBusy(null);
    setMessage(
      result === "granted" ? "Уведомления включены на этом устройстве." : result === "denied" ? "Уведомления запрещены — как включить, написано ниже." : result === "error" ? "Не удалось подписаться — попробуйте ещё раз." : null,
    );
    await refresh();
  };

  const turnOff = async () => {
    setBusy("notify");
    await disablePush();
    setBusy(null);
    setMessage("Уведомления на этом устройстве отключены.");
    await refresh();
  };

  const test = async () => {
    setBusy("test");
    try {
      const devices = await sendTestPush();
      setMessage(devices > 0 ? "Проверочное уведомление отправлено — оно придёт через несколько секунд." : "Устройств с включёнными уведомлениями нет.");
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Не удалось отправить проверку.");
    }
    setBusy(null);
  };

  const askGeo = async () => {
    setBusy("geo");
    // Явное нажатие: окно разрешения показываем, даже если раньше его закрывали.
    const { reason } = await locate({ reuseMs: 0, force: true });
    setBusy(null);
    setGeoBlocked(reason === "denied");
    setMessage(reason === null ? "Геолокация разрешена." : reason === "denied" ? "Геолокация запрещена — как включить, написано ниже." : "Не удалось определить местоположение — проверьте, что GPS включён.");
    await refresh();
  };

  return (
    <Sheet open={open} onClose={onClose} title="Уведомления и геолокация">
      <div className="space-y-5 text-sm">
        <section className="space-y-2">
          <div className="flex items-center gap-2">
            <Bell className="size-4 text-ink-3" />
            <span className="flex-1 font-medium text-ink">Уведомления о задачах</span>
            {support === "ios-install" ? (
              <Status ok={null}>нужна установка</Status>
            ) : notify === "granted" && subscribed ? (
              <Status ok>включены</Status>
            ) : notify === "denied" ? (
              <Status ok={false}>запрещены</Status>
            ) : (
              <Status ok={null}>выключены</Status>
            )}
          </div>
          {support === "ios-install" && (
            <p className="flex gap-2 rounded-lg bg-muted px-3 py-2 text-ink-2">
              <Smartphone className="mt-0.5 size-4 shrink-0" />
              На iPhone уведомления приходят только в установленное приложение: в Safari нажмите «Поделиться» → «На экран „Домой“» и открывайте {appName} с иконки.
            </p>
          )}
          {support === "unsupported" && <p className="text-ink-3">Этот браузер не поддерживает уведомления. Откройте {appName} в Chrome или установите приложение.</p>}
          {support === "supported" && notify !== "denied" && (
            <div className="flex flex-wrap gap-2">
              {notify === "granted" && subscribed ? (
                <>
                  <button type="button" disabled={busy !== null} onClick={test} className={button}>
                    <CheckCircle2 className="size-4" /> {busy === "test" ? "Отправляем…" : "Проверить"}
                  </button>
                  <button type="button" disabled={busy !== null} onClick={turnOff} className={button}>
                    <BellOff className="size-4" /> Отключить
                  </button>
                </>
              ) : (
                <button type="button" disabled={busy !== null} onClick={turnOn} className={primary}>
                  <Bell className="size-4" /> {busy === "notify" ? "Включаем…" : "Включить уведомления"}
                </button>
              )}
            </div>
          )}
          {notify === "denied" && (
            <p className="rounded-lg bg-muted px-3 py-2 text-ink-2">
              {ios
                ? `Настройки iPhone → «Уведомления» → ${appName} → «Допуск уведомлений». Если вы отказали в окне разрешения, удалите приложение с экрана «Домой» и добавьте заново.`
                : "Chrome: значок слева от адреса → «Разрешения» → «Уведомления» → «Разрешить». В установленном приложении: долгое нажатие на иконку → «О приложении» → «Уведомления»."}
            </p>
          )}
        </section>

        <section className="space-y-2 border-t border-line pt-4">
          <div className="flex items-center gap-2">
            <MapPin className="size-4 text-ink-3" />
            <span className="flex-1 font-medium text-ink">Геолокация</span>
            {geo === "granted" ? <Status ok>разрешена</Status> : geo === "denied" || geoBlocked ? <Status ok={false}>запрещена</Status> : geo === "unsupported" ? <Status ok={null}>нет</Status> : <Status ok={null}>не задана</Status>}
          </div>
          <p className="text-ink-3">Нужна, чтобы отметить визит у точки и построить маршрут от вашего места.</p>
          {geo === "prompt" && (
            <>
              <button type="button" disabled={busy !== null} onClick={askGeo} className={primary}>
                <MapPin className="size-4" /> {busy === "geo" ? "Определяем…" : "Разрешить геолокацию"}
              </button>
              {!ios && <p className="text-xs text-ink-3">В окне разрешения выберите «Разрешить при использовании сайта», а не «Только в этот раз» — тогда спрашивать больше не будут.</p>}
            </>
          )}
          {(geo === "denied" || geoBlocked) && (
            <p className="rounded-lg bg-muted px-3 py-2 text-ink-2">
              {ios
                ? "Настройки iPhone → «Конфиденциальность» → «Службы геолокации» → «Сайты Safari» → «При использовании». В Safari: «аА» → «Настройки веб-сайта» → «Геопозиция» → «Разрешить»."
                : "Chrome: значок слева от адреса → «Разрешения» → «Местоположение» → «Разрешить». Ещё в Android: «Настройки» → «Приложения» → Chrome → «Разрешения» → «Местоположение» → «Разрешить только во время использования». После этого обновите страницу."}
            </p>
          )}
        </section>

        {message && <p className="rounded-lg bg-accent-soft px-3 py-2 text-accent-strong">{message}</p>}
      </div>
    </Sheet>
  );
}
