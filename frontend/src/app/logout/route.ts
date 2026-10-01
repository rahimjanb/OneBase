import { NextResponse } from "next/server";
import { SESSION_COOKIE } from "@/lib/server-api";

/**
 * Выход: очистка cookie сессии и страница входа. Адрес — относительный: за nginx в Docker request.url указывает
 * на адрес, который слушает сервер (0.0.0.0:3000), а не на домен сайта; браузер сам подставит текущий домен.
 */
export async function GET() {
  const response = new NextResponse(null, { status: 307, headers: { Location: "/login" } });
  response.cookies.delete(SESSION_COOKIE);
  return response;
}
