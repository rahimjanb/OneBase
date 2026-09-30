import { type NextRequest } from "next/server";
import { API_URL, SESSION_COOKIE } from "@/lib/server-api";

/**
 * Прокси для запросов из браузера: /bff/api/... → backend /api/... с токеном из httpOnly-cookie.
 * Токен не попадает в JavaScript страницы.
 */
async function proxy(request: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
  const { path } = await params;
  if (path[0] !== "api") return new Response("Not found", { status: 404 });

  const token = request.cookies.get(SESSION_COOKIE)?.value;
  if (!token) return Response.json({ error: "Нет сессии" }, { status: 401 });

  const headers = new Headers({ Authorization: `Bearer ${token}` });
  const contentType = request.headers.get("content-type");
  if (contentType) headers.set("content-type", contentType);

  const hasBody = !["GET", "HEAD"].includes(request.method);
  const upstream = await fetch(`${API_URL}/${path.join("/")}${request.nextUrl.search}`, {
    method: request.method,
    headers,
    body: hasBody ? await request.arrayBuffer() : undefined,
    cache: "no-store",
  });

  const responseHeaders = new Headers();
  // cache-control и x-accel-buffering — чтобы поток ответа консультанта (text/event-stream) не буферизовался и не сжимался.
  for (const name of ["content-type", "content-disposition", "cache-control", "x-accel-buffering"]) {
    const value = upstream.headers.get(name);
    if (value) responseHeaders.set(name, value);
  }
  return new Response(upstream.body, { status: upstream.status, headers: responseHeaders });
}

export { proxy as GET, proxy as POST, proxy as PUT, proxy as PATCH, proxy as DELETE };
