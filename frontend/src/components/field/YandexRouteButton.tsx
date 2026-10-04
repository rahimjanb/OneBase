"use client";

import { useEffect, useState } from "react";
import { Navigation } from "lucide-react";
import { plural } from "@/lib/field/labels";
import type { FieldRoute } from "@/lib/field/types";
import { yandexRouteLinks } from "@/lib/field/yandex";
import { buttonClass } from "./ui";

/**
 * Маршрут, который сформировала система, — в Яндекс Картах. Агенту (fromHere): оставшиеся точки по порядку от его места;
 * супервайзеру и РМ: все точки маршрута от первой. Длинный маршрут — несколько ссылок по частям (до 10 точек в каждой).
 */
export function YandexRouteButton({ route, fromHere, className = "" }: { route: FieldRoute; fromHere: boolean; className?: string }) {
  const points = route.points
    .filter((p) => (fromHere ? p.status === "Planned" && !p.linkoDone : p.status !== "Cancelled"))
    .map((p) => ({ lat: p.lat, lon: p.lon, n: p.sequence }));
  // Геолокация нужна, только если кнопка будет показана: иначе браузер зря спросит разрешение.
  const wantsPosition = fromHere && points.some((p) => p.lat != null && p.lon != null);

  // Местоположение телефона — первой точкой маршрута. Следим за ним, пока страница на экране: агент едет, и старт должен
  // быть свежим. Свернули страницу или геолокация отказала — сбрасываем: маршрут начнётся с первой оставшейся точки,
  // а не со старого места. Разрешение сами не запрашиваем (его спрашивает «Начать визит»): лишние запросы при открытии
  // страницы браузер после нескольких отказов превращает в блокировку геолокации для всего сайта.
  const [here, setHere] = useState<{ lat: number; lon: number } | null>(null);
  useEffect(() => {
    if (!wantsPosition || typeof navigator === "undefined" || !navigator.geolocation || !navigator.permissions) return;
    let cancelled = false;
    let permission: PermissionStatus | null = null;
    let watch: number | null = null;
    const stop = () => {
      if (watch !== null) navigator.geolocation.clearWatch(watch);
      watch = null;
    };
    const sync = () => {
      if (permission?.state === "granted" && document.visibilityState === "visible") {
        if (watch === null) {
          watch = navigator.geolocation.watchPosition(
            (pos) => setHere({ lat: pos.coords.latitude, lon: pos.coords.longitude }),
            () => setHere(null),
            { enableHighAccuracy: false, maximumAge: 60000 },
          );
        }
      } else {
        stop();
        setHere(null);
      }
    };
    navigator.permissions
      .query({ name: "geolocation" })
      .then((status) => {
        if (cancelled) return;
        permission = status;
        status.addEventListener("change", sync);
        sync();
      })
      .catch(() => undefined);
    document.addEventListener("visibilitychange", sync);
    return () => {
      cancelled = true;
      permission?.removeEventListener("change", sync);
      document.removeEventListener("visibilitychange", sync);
      stop();
    };
  }, [wantsPosition]);

  const { links, skipped } = yandexRouteLinks(points, { fromHere, start: here });
  if (links.length === 0) return null;

  const located = points.length - skipped;
  const title = fromHere
    ? `Открыть в Яндекс Картах ${located} ${plural(located, "оставшуюся точку", "оставшиеся точки", "оставшихся точек")} от вашего места`
    : `Открыть в Яндекс Картах ${located} ${plural(located, "точку", "точки", "точек")} маршрута`;
  const note = skipped > 0 ? `${skipped} ${plural(skipped, "точка", "точки", "точек")} без координат не ${plural(skipped, "вошла", "вошли", "вошли")}` : null;

  if (links.length === 1) {
    return (
      <span className={`inline-flex flex-col gap-1 ${className}`}>
        <a href={links[0].url} target="_blank" rel="noopener noreferrer" title={title} aria-label={title} className={buttonClass.outline}>
          <Navigation className="size-4" /> Яндекс Карты
          <span className="text-xs font-normal text-ink-3">{located}</span>
        </a>
        {note && <span className="text-xs text-ink-3">{note}</span>}
      </span>
    );
  }

  // Больше точек, чем принимает одна ссылка: части идут подряд и делят крайнюю точку.
  return (
    <div className={`w-full rounded-lg border border-line bg-surface px-3 py-2.5 ${className}`}>
      <div className="flex items-center gap-2 text-sm font-medium text-ink">
        <Navigation className="size-4" /> Яндекс Карты по частям
      </div>
      <p className="mt-0.5 text-xs text-ink-3">{title}. Маршрут разбит на части по 10 точек — откройте следующую часть, когда дойдёте до конца предыдущей.</p>
      <div className="mt-2 flex flex-wrap gap-1.5">
        {links.map((l, i) => (
          <a key={`${i}-${l.from}`} href={l.url} target="_blank" rel="noopener noreferrer" className={`${buttonClass.outline} h-9 px-3 lg:h-9`}>
            {i === 0 && fromHere && here ? `От меня до №${l.to}` : `Точки ${l.from}–${l.to}`}
          </a>
        ))}
      </div>
      {note && <p className="mt-1.5 text-xs text-ink-3">{note}</p>}
    </div>
  );
}
