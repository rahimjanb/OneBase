import { redirect } from "next/navigation";
import { OverviewView } from "@/components/procurement/OverviewView";
import { requireRequests } from "@/components/requests/RequestListPage";
import { getT } from "@/i18n/server";
import type { AiStatus } from "@/lib/consultant";
import type { Board, RequestNav } from "@/lib/procurement";
import { apiGet } from "@/lib/server-api";
import { qs } from "@/lib/tasks";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Снабжение · OneBase") };
}

/**
 * «Снабжение → Обзор»: что требует действия, путь закупки, показатели и аналитика периода, сотрудники — по правам роли (GET /api/procurement/board).
 * Период — в адресе (from, to); без него — текущий месяц. Supply AI — тем, кому открыт AI-консультант; не настроен — поле неактивно.
 */
export default async function ProcurementPage({ searchParams }: { searchParams: Promise<{ from?: string; to?: string }> }) {
  await requireRequests();
  const { from, to } = await searchParams;
  const nav = await apiGet<RequestNav>("/api/procurement/nav", "/procurement");
  if (!nav.seesStock && !nav.isDirector) redirect("/requests");
  const [board, me] = await Promise.all([
    apiGet<Board>(`/api/procurement/board${qs({ from, to })}`, "/procurement"),
    apiGet<{ permissions: string[] }>("/api/auth/me", "/procurement"),
  ]);
  const ask = me.permissions.includes("ai.agents.run");
  const ai = ask ? await apiGet<AiStatus>("/api/ai/status", "/procurement").catch(() => null) : null;
  return <OverviewView board={board} ask={ask} aiReady={ai?.ready ?? false} />;
}
