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
  if (!response.ok) throw new ApiRequestError(response.status, await response.text());
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
