import "server-only";
import { cache } from "react";
import { cookies, headers } from "next/headers";
import { notFound, redirect } from "next/navigation";
import { API_URL, SESSION_COOKIE } from "@/lib/server-api";
import { fieldLoginPath, requestHost } from "./host";
import type { FieldMe } from "./types";

/** Нет доступа к Sales Base (403): страница показывает объяснение, а не уводит в «нет доступа» OneBase. */
export class FieldAccessError extends Error {}

/**
 * Запрос к API Sales Base с сервера Next.js. Без сессии или 401 — на вход с возвратом; 403 — FieldAccessError;
 * 404 — notFound() (чужие данные выглядят как несуществующие); 204 — null.
 */
export async function fieldGet<T>(path: string, returnTo: string): Promise<T> {
  // Вход — на страницу Sales Base, а не OneBase.
  const login = async () => redirect(`${fieldLoginPath(requestHost(await headers()))}?next=${encodeURIComponent(returnTo)}`);
  const token = (await cookies()).get(SESSION_COOKIE)?.value;
  if (!token) return await login();

  const response = await fetch(`${API_URL}/api/field/${path}`, { headers: { Authorization: `Bearer ${token}` }, cache: "no-store" });
  if (response.status === 401) return await login();
  if (response.status === 403) {
    let message = "Нет доступа к этому разделу Sales Base.";
    try {
      message = ((await response.json()) as { error?: string }).error ?? message;
    } catch {
      // не JSON
    }
    throw new FieldAccessError(message);
  }
  if (response.status === 404) notFound();
  if (response.status === 204) return null as T;
  if (!response.ok) throw new Error((await response.text()) || `Sales Base: HTTP ${response.status}`);
  return (await response.json()) as T;
}

/** То же, но 403 → null (для блоков, которые видны не всем ролям). */
export async function fieldTry<T>(path: string, returnTo: string): Promise<T | null> {
  try {
    return await fieldGet<T>(path, returnTo);
  } catch (error) {
    if (error instanceof FieldAccessError) return null;
    throw error;
  }
}

/** Строка запроса из параметров страницы: пустые значения не пишутся. */
export function qs(params: Record<string, string | number | boolean | null | undefined>): string {
  const q = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== null && value !== undefined && value !== "" && value !== false) q.set(key, String(value));
  }
  const s = q.toString();
  return s ? `?${s}` : "";
}

export type FieldSearchParams = Record<string, string | string[] | undefined>;

export const sp = (params: FieldSearchParams, key: string) => {
  const v = params[key];
  return Array.isArray(v) ? v[0] : v;
};

/** Профиль Sales Base — один запрос на рендер (layout и страница получают один ответ). */
export const fieldMe = cache(() => fieldGet<FieldMe>("me", "/field"));
