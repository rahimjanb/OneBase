"use server";

import { cookies, headers } from "next/headers";
import { redirect } from "next/navigation";
import { API_URL, SESSION_COOKIE } from "@/lib/server-api";

export type LoginState = { error?: string };

export async function login(_: LoginState, form: FormData): Promise<LoginState> {
  const login = String(form.get("login") ?? "").trim();
  const password = String(form.get("password") ?? "");
  const next = String(form.get("next") ?? "/sales");

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

  // Только путь этого сайта: «//host» и «/\host» браузер понял бы как другой сайт.
  const safeNext = next.startsWith("/") && !next.startsWith("//") && !next.startsWith("/\\") ? next : "/sales";
  redirect(safeNext);
}
