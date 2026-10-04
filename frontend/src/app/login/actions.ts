"use server";

import { cookies, headers } from "next/headers";
import { redirect } from "next/navigation";
import { fieldHomePath, isFieldHost, requestHost } from "@/lib/field/host";
import { API_URL, SESSION_COOKIE } from "@/lib/server-api";

export type LoginState = { error?: string };

export async function login(_: LoginState, form: FormData): Promise<LoginState> {
  const login = String(form.get("login") ?? "").trim();
  const password = String(form.get("password") ?? "");
  // Вход Sales Base (его домен или его страница входа) — по умолчанию его главная, иначе — продажи OneBase.
  const host = requestHost(await headers());
  const fallback = isFieldHost(host) || form.get("product") === "field" ? fieldHomePath(host) : "/sales";
  const next = String(form.get("next") ?? fallback);

  // Вход идёт с сервера Next.js — без этого API видел бы один IP на всех, и лимит попыток был бы общим на компанию.
  // X-Real-IP ставит nginx (из CF-Connecting-IP), подделать его из браузера нельзя.
  const clientIp = (await headers()).get("x-real-ip");

  let response: Response;
  try {
    response = await fetch(`${API_URL}/api/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json", ...(clientIp ? { "X-Forwarded-For": clientIp } : {}) },
      body: JSON.stringify({ login, password }),
      cache: "no-store",
    });
  } catch {
    return { error: "Сервер OneBase недоступен. Попробуйте позже." };
  }

  if (response.status === 401) return { error: "Неверный логин или пароль." };
  if (response.status === 429) return { error: "Слишком много попыток. Подождите минуту." };
  if (!response.ok) return { error: `Ошибка входа (${response.status}).` };

  const { accessToken, expiresAt } = (await response.json()) as { accessToken: string; expiresAt: string };
  (await cookies()).set(SESSION_COOKIE, accessToken, {
    httpOnly: true,
    sameSite: "lax",
    secure: process.env.NODE_ENV === "production",
    path: "/",
    expires: new Date(expiresAt),
  });

  redirect(safePath(next, fallback));
}

/**
 * Только путь этого сайта. «//host», «/\host» и «/<TAB>/host» браузер понял бы как другой сайт (табы и переводы строк он выбрасывает),
 * поэтому обратные слэши и управляющие символы отклоняются, а итог сверяется с разбором URL относительно условного адреса.
 */
function safePath(next: string, fallback: string): string {
  // Код 92 — обратный слэш, до 32 и 127 — управляющие символы.
  if (!next.startsWith("/") || [...next].some((c) => c.charCodeAt(0) < 32 || c.charCodeAt(0) === 127 || c.charCodeAt(0) === 92)) return fallback;
  const base = "http://onebase.invalid";
  try {
    const url = new URL(next, base);
    const path = `${url.pathname}${url.search}${url.hash}`;
    // Разбор схлопывает точки: «/.//evil.com» превращается в «//evil.com» — такой результат тоже чужой сайт.
    return url.origin === base && !path.startsWith("//") ? path : fallback;
  } catch {
    return fallback;
  }
}
