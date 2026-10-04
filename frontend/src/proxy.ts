import { NextResponse, type NextRequest } from "next/server";
import { FIELD_LOGIN_PATH, fieldLoginPath, isFieldHost, requestHost } from "@/lib/field/host";

/** Имя cookie сессии — как SESSION_COOKIE в lib/server-api.ts (proxy не должен тянуть серверные модули). */
const SESSION_COOKIE = "onebase_token";

/** Страницы, открытые без входа. */
const PUBLIC_PATHS = ["/login", "/logout", FIELD_LOGIN_PATH];

/**
 * Без сессии — сразу на вход, с возвратом на запрошенную страницу. Иначе страницы, которые не запрашивают данные
 * (например, главная), открывались бы без входа. Недействительную сессию (401 от API) обрабатывает layout приложения.
 *
 * Домен Sales Base (sales.1base.uz): адреса вне /field переписываются в /field — «/» это главная Sales Base,
 * «/tasks» — /field/tasks. У Sales Base своя страница входа (/field/login; на его домене — «/login»), выход общий.
 */
export function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;
  const isPublic = PUBLIC_PATHS.some((p) => pathname === p || pathname.startsWith(`${p}/`));
  const fieldHost = isFieldHost(requestHost(request.headers));
  const fieldPath = pathname === "/field" || pathname.startsWith("/field/");
  if (!isPublic && !request.cookies.get(SESSION_COOKIE)?.value) {
    // Домен — из X-Forwarded-Host/Host, а не request.url: за nginx тот указывает на внутренний адрес сервера,
    // и sales.1base.uz уводил бы на чужой домен. Proxy требует абсолютный адрес.
    const host = requestHost(request.headers)?.split(",")[0].trim();
    const proto = request.headers.get("x-forwarded-proto")?.split(",")[0].trim() ?? request.nextUrl.protocol.replace(":", "");
    const login = new URL(fieldHost || fieldPath ? fieldLoginPath(host) : "/login", host ? `${proto}://${host}` : request.url);
    login.searchParams.set("next", pathname + search);
    return NextResponse.redirect(login);
  }

  // Домен Sales Base: «/login» — его собственная страница входа.
  if (fieldHost && (pathname === "/login" || pathname.startsWith("/login/"))) {
    const url = request.nextUrl.clone();
    url.pathname = FIELD_LOGIN_PATH;
    return NextResponse.rewrite(url);
  }

  if (!isPublic && fieldHost && !fieldPath) {
    const url = request.nextUrl.clone();
    url.pathname = pathname === "/" ? "/field" : `/field${pathname}`;
    return NextResponse.rewrite(url);
  }

  return NextResponse.next();
}

export const config = {
  // Кроме статики, картинок, /bff (прокси к API сам отвечает 401 без сессии) и файлов веб-приложения:
  // манифест, service worker, офлайн-страницу и иконки браузер запрашивает без входа — иначе установка не работает.
  matcher: [
    "/((?!_next/static|_next/image|bff/|favicon.ico|robots.txt|manifest.webmanifest|sw.js|offline.html|icons/|brand/|.*\\.(?:png|jpg|jpeg|gif|svg|ico|webp|txt|woff2?|css|js|map)$).*)",
  ],
};
