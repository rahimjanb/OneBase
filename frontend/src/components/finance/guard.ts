import { redirect } from "next/navigation";
import { financeQuery, one, todayIso, type FinanceOrganization, type SearchParams } from "@/lib/finance";
import { canOpen, homeFor } from "@/lib/navigation";
import { apiGet, apiSession } from "@/lib/server-api";
import type { Me } from "@/lib/users";

/** Страницы «Финансов»: право finance.read; без него — на стартовую страницу пользователя. Данные проверяет сервер. */
export async function requireFinance(): Promise<Me | null> {
  const me = await apiSession<Me>("/api/auth/me");
  if (me && !canOpen("/finance", me.permissions)) redirect(homeFor(me.permissions));
  return me;
}

/** Отбор страницы: организация и период (по умолчанию — текущий месяц по Ташкенту) и организации 1С для выбора. */
export async function financeContext(sp: SearchParams, path: string) {
  const today = todayIso();
  const to = one(sp, "to") ?? today;
  const from = one(sp, "from") ?? `${to.slice(0, 7)}-01`;
  const organizations = await apiGet<FinanceOrganization[]>("/api/finance/organizations", path);
  const query = financeQuery({ ...sp, from, to });
  return { from, to, organization: one(sp, "organization"), organizations, query };
}
