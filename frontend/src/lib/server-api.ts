import "server-only";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";

export const SESSION_COOKIE = "onebase_token";
export const API_URL = process.env.API_INTERNAL_URL ?? "http://localhost:5080";

export class ApiRequestError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
  }
}

/** Запрос к backend с сервера Next.js. Без сессии или при 401 — на страницу входа. */
export async function apiGet<T>(path: string, returnTo = "/sales"): Promise<T> {
  const token = (await cookies()).get(SESSION_COOKIE)?.value;
  if (!token) redirect(`/login?next=${encodeURIComponent(returnTo)}`);

  const response = await fetch(`${API_URL}${path}`, {
    headers: { Authorization: `Bearer ${token}` },
    cache: "no-store",
  });

  if (response.status === 401) redirect(`/login?next=${encodeURIComponent(returnTo)}`);
  if (response.status === 403) redirect(`/no-access?from=${encodeURIComponent(returnTo)}`);
  if (!response.ok) {
    const text = await response.text();
    throw new ApiRequestError(response.status, text || `Сервер OneBase ответил HTTP ${response.status}`);
  }
  return (await response.json()) as T;
}

/** То же, но 404 превращает в null (страница покажет notFound). */
export async function apiGetOrNull<T>(path: string, returnTo?: string): Promise<T | null> {
  try {
    return await apiGet<T>(path, returnTo);
  } catch (error) {
    if (error instanceof ApiRequestError && error.status === 404) return null;
    throw error;
  }
}

/**
 * Данные о текущей сессии для общего layout. Нет cookie — null (proxy уже отправил бы на вход).
 * 401 — токен недействителен или пользователь отключён: выход с очисткой cookie и страница входа.
 * API недоступен — null, без выхода: сессия может быть в порядке.
 */
export async function apiSession<T>(path: string): Promise<T | null> {
  const token = (await cookies()).get(SESSION_COOKIE)?.value;
  if (!token) return null;
  let response: Response;
  try {
    response = await fetch(`${API_URL}${path}`, { headers: { Authorization: `Bearer ${token}` }, cache: "no-store" });
  } catch {
    return null;
  }
  if (response.status === 401) redirect("/logout");
  return response.ok ? ((await response.json()) as T) : null;
}

/** Запрос для необязательного блока страницы: нет сессии, прав или ответа — null, без перехода на вход или «нет доступа». */
export async function apiTry<T>(path: string): Promise<T | null> {
  const token = (await cookies()).get(SESSION_COOKIE)?.value;
  if (!token) return null;
  try {
    const response = await fetch(`${API_URL}${path}`, { headers: { Authorization: `Bearer ${token}` }, cache: "no-store" });
    return response.ok ? ((await response.json()) as T) : null;
  } catch {
    return null;
  }
}
