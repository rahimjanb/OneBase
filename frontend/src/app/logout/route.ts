import { NextResponse, type NextRequest } from "next/server";
import { SESSION_COOKIE } from "@/lib/server-api";

export async function GET(request: NextRequest) {
  const response = NextResponse.redirect(new URL("/login", request.url));
  response.cookies.delete(SESSION_COOKIE);
  return response;
}
