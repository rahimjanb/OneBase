import { NextResponse, type NextRequest } from "next/server";
import { fieldLoginPath, isFieldHost, requestHost } from "@/lib/field/host";
import { SESSION_COOKIE } from "@/lib/server-api";

/**
 * Выход: очистка cookie сессии и страница входа. Из Sales Base (его домен или ?to=field) — на вход Sales Base.
 * Адрес — относительный: за nginx в Docker request.url указывает на адрес, который слушает сервер (0.0.0.0:3000),
 * а не на домен сайта; браузер сам подставит текущий домен.
 */
export async function GET(request: NextRequest) {
  const host = requestHost(request.headers);
  const field = isFieldHost(host) || request.nextUrl.searchParams.get("to") === "field";
  const response = new NextResponse(null, { status: 307, headers: { Location: field ? fieldLoginPath(host) : "/login" } });
  response.cookies.delete(SESSION_COOKIE);
  return response;
}
