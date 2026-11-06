import { redirect } from "next/navigation";
import { canOpen, homeFor } from "@/lib/navigation";
import type { ProductionNav } from "@/lib/production";
import { apiGet, apiSession } from "@/lib/server-api";
import type { Me } from "@/lib/users";

/**
 * Страницы «Производства» (обзор, задания, рецептуры, отчёты): раздел открыт и пользователь видит производство. Видит только контроль
 * качества — на «Качество»; иначе — на свою стартовую страницу.
 */
export async function requireProduction(path: string): Promise<ProductionNav> {
  const me = await apiSession<Me>("/api/auth/me");
  if (me && !canOpen("/production", me.permissions)) redirect(homeFor(me.permissions));
  const nav = await apiGet<ProductionNav>("/api/production/nav", path);
  if (!nav.sees) redirect(nav.quality ? "/production/quality" : me ? homeFor(me.permissions) : "/");
  return nav;
}
