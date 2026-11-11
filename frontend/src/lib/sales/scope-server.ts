import { apiSession } from "@/lib/server-api";
import type { Me } from "@/lib/users";
import type { SalesScopeInfo } from "./access";

/** Охват «Продаж» текущего пользователя на сервере Next.js (из /api/auth/me, тот же ответ, что у оболочки — React cache). */
export async function mySalesScope(): Promise<SalesScopeInfo | null> {
  return (await apiSession<Me>("/api/auth/me"))?.salesScope ?? null;
}
