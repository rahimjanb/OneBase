import { redirect } from "next/navigation";
import { canOpen, homeFor } from "@/lib/navigation";
import { apiSession } from "@/lib/server-api";
import type { Me } from "@/lib/users";

/** Страницы «Аналитика»: раздел открыт по праву analytik.use; без него — на стартовую страницу пользователя. Данные проверяет сервер. */
export async function requireAnalytik(): Promise<Me | null> {
  const me = await apiSession<Me>("/api/auth/me");
  if (me && !canOpen("/analytik", me.permissions)) redirect(homeFor(me.permissions));
  return me;
}

export type SearchParams = Record<string, string | string[] | undefined>;

export const one = (sp: SearchParams, key: string): string | undefined => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

/** Период из адреса страницы → строка запроса API. */
export function periodParams(sp: SearchParams): string {
  const params = new URLSearchParams();
  params.set("period", one(sp, "period") ?? "month");
  for (const key of ["from", "to"]) {
    const v = one(sp, key);
    if (v) params.set(key, v);
  }
  return params.toString();
}
