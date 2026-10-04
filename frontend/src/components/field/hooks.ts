"use client";

import { useRouter } from "next/navigation";
import { useCallback, useState } from "react";
import { bff } from "@/lib/bff";

/** Действие с кнопки: занято/ошибка, после успеха — обновление серверных данных страницы. */
export function useAction() {
  const router = useRouter();
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const run = useCallback(
    async <T,>(key: string, fn: () => Promise<T>, opts?: { refresh?: boolean; onDone?: (result: T) => void }) => {
      setBusy(key);
      setError(null);
      try {
        const result = await fn();
        opts?.onDone?.(result);
        if (opts?.refresh !== false) router.refresh();
        return result;
      } catch (e) {
        setError(e instanceof Error ? e.message : String(e));
        return undefined;
      } finally {
        setBusy(null);
      }
    },
    [router],
  );

  return { busy, error, setError, run };
}

/** POST/PUT в API Sales Base через /bff. */
export const fieldApi = {
  get: <T,>(path: string) => bff<T>(`field/${path}`),
  post: <T,>(path: string, body?: unknown) => bff<T>(`field/${path}`, { method: "POST", body: body === undefined ? undefined : JSON.stringify(body) }),
  put: <T,>(path: string, body?: unknown) => bff<T>(`field/${path}`, { method: "PUT", body: body === undefined ? undefined : JSON.stringify(body) }),
};

export type Position = { latitude: number; longitude: number; accuracy: number };

/**
 * Координаты из браузера. Не блокирует работу: нет разрешения, сигнала или ответа за 12 секунд — null
 * (визит начнётся с отметкой «нет GPS»). Точность браузер сообщает сам — её учитывает проверка геозоны на сервере.
 */
export function getPosition(timeoutMs = 12000): Promise<Position | null> {
  return new Promise((resolve) => {
    if (typeof navigator === "undefined" || !navigator.geolocation) return resolve(null);
    let done = false;
    const timer = setTimeout(() => {
      if (!done) {
        done = true;
        resolve(null);
      }
    }, timeoutMs + 500);
    navigator.geolocation.getCurrentPosition(
      (p) => {
        if (done) return;
        done = true;
        clearTimeout(timer);
        resolve({ latitude: p.coords.latitude, longitude: p.coords.longitude, accuracy: p.coords.accuracy });
      },
      () => {
        if (done) return;
        done = true;
        clearTimeout(timer);
        resolve(null);
      },
      { enableHighAccuracy: true, timeout: timeoutMs, maximumAge: 30000 },
    );
  });
}

/** Сегодня по времени компании (UTC+5), yyyy-MM-dd. */
export function companyToday(): string {
  const now = new Date(Date.now() + 5 * 3600 * 1000);
  return now.toISOString().slice(0, 10);
}
