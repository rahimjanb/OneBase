import { NextResponse, type NextRequest } from "next/server";
import { fieldLoginPath, isFieldHost, requestHost } from "@/lib/field/host";
import { API_URL, SESSION_COOKIE } from "@/lib/server-api";

/**
 * Выход: сеанс закрывается на сервере (токен больше не действует, даже скопированный), cookie очищается, дальше — страница входа. Из Sales Base (его домен или ?to=field) — на вход Sales Base.
 * Адрес — относительный: за nginx в Docker request.url указывает на адрес, который слушает сервер (0.0.0.0:3000),
 * а не на домен сайта; браузер сам подставит текущий домен.
 */
export async function GET(request: NextRequest) {
  const token = request.cookies.get(SESSION_COOKIE)?.value;
  if (token) {
    // Сбой сети или уже закрытый сеанс не мешают выйти: cookie очищается в любом случае.
    await fetch(`${API_URL}/api/auth/logout`, {
      method: "POST",
      headers: { Authorization: `Bearer ${token}` },
      cache: "no-store",
      signal: AbortSignal.timeout(5000),
    }).catch(() => undefined);
  }

  const host = requestHost(request.headers);
  const field = isFieldHost(host) || request.nextUrl.searchParams.get("to") === "field";
  const response = new NextResponse(null, { status: 307, headers: { Location: field ? fieldLoginPath(host) : "/login" } });
  response.cookies.delete(SESSION_COOKIE);
  return response;
}
