import { NextResponse, type NextRequest } from "next/server";

/** Имя cookie сессии — как SESSION_COOKIE в lib/server-api.ts (proxy не должен тянуть серверные модули). */
const SESSION_COOKIE = "onebase_token";

/** Страницы, открытые без входа. */
const PUBLIC_PATHS = ["/login", "/logout"];

/**
 * Без сессии — сразу на вход, с возвратом на запрошенную страницу. Иначе страницы, которые не запрашивают данные
 * (например, главная), открывались бы без входа. Недействительную сессию (401 от API) обрабатывает layout приложения.
 */
export function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;
  if (PUBLIC_PATHS.some((p) => pathname === p || pathname.startsWith(`${p}/`))) return NextResponse.next();
  if (request.cookies.get(SESSION_COOKIE)?.value) return NextResponse.next();

  const login = new URL("/login", request.url);
  login.searchParams.set("next", pathname + search);
  return NextResponse.redirect(login);
}

export const config = {
  // Кроме статики, картинок, /bff (прокси к API сам отвечает 401 без сессии) и файлов веб-приложения:
  // манифест, service worker, офлайн-страницу и иконки браузер запрашивает без входа — иначе установка не работает.
  matcher: [
    "/((?!_next/static|_next/image|bff/|favicon.ico|robots.txt|manifest.webmanifest|sw.js|offline.html|icons/|.*\\.(?:png|jpg|jpeg|gif|svg|ico|webp|txt|woff2?)$).*)",
  ],
};
