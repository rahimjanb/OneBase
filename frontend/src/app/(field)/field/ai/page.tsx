import { headers } from "next/headers";
import { redirect } from "next/navigation";
import { MessageSquareText } from "lucide-react";
import { ParamSelect } from "@/components/field/DateSwitch";
import { GenerateButton, RecommendationList } from "@/components/field/Recommendations";
import { FieldPage, Pager, Panel, Tabs, buttonClass } from "@/components/field/ui";
import { fieldGet, fieldMe, qs, sp, type FieldSearchParams } from "@/lib/field/api";
import { oneBaseHref, requestHost } from "@/lib/field/host";
import { recommendationKindLabel } from "@/lib/field/labels";
import { apiTry } from "@/lib/server-api";
import type { FieldRecommendation, Page } from "@/lib/field/types";

export const metadata = { title: "AI-планирование" };

type AgentOption = { id: string; name: string; teamName: string | null; role: string };

/**
 * AI-планирование: рекомендации по правилам на продажах, визитах, планах и маршрутах. План сразу не меняется —
 * супервайзер или РМ подтверждает (можно с правкой) или отклоняет. Подтверждённая становится задачей.
 */
export default async function AiPage({ searchParams }: { searchParams: Promise<FieldSearchParams> }) {
  const params = await searchParams;
  const me = await fieldMe();
  if (me.role === "Agent") redirect("/field/tasks?by=Ai");
  const status = sp(params, "status") ?? "pending";
  const kind = sp(params, "kind");
  const page = Number(sp(params, "page") ?? 1) || 1;
  const [data, agents, session] = await Promise.all([
    fieldGet<Page<FieldRecommendation>>(`ai/recommendations${qs({ status, kind, page, pageSize: 30 })}`, "/field/ai"),
    fieldGet<AgentOption[]>("agents", "/field/ai"),
    apiTry<{ permissions: string[] }>("/api/auth/me"),
  ]);
  const canAsk = session?.permissions.includes("ai.agents.run") ?? false;
  const consultantHref = oneBaseHref(requestHost(await headers()), "/consultant");
  const link = (changes: Record<string, string | number | null>) => {
    const q = new URLSearchParams();
    for (const [k, v] of Object.entries({ status: status === "pending" ? null : status, kind, page: null, ...changes })) if (v !== null && v !== undefined && v !== "") q.set(k, String(v));
    const s = q.toString();
    return s ? `/field/ai?${s}` : "/field/ai";
  };

  return (
    <FieldPage title="AI-планирование" subtitle="Какие точки посетить, кому назначить, где агент не выполняет план" actions={<GenerateButton />}>
      <Tabs
        items={[
          { key: "pending", label: `Ждут решения${status === "pending" ? ` · ${data.total}` : ""}`, href: link({ status: null }) },
          { key: "decided", label: "Решённые", href: link({ status: "decided" }) },
          { key: "all", label: "Все", href: link({ status: "all" }) },
        ]}
        current={status}
      />
      <ParamSelect param="kind" value={kind ?? ""} label="Тип" options={[{ value: "", label: "Все типы" }, ...Object.entries(recommendationKindLabel).map(([k, l]) => ({ value: k, label: l }))]} />
      <RecommendationList items={data.items} agents={agents.filter((a) => a.role === "Agent").map((a) => ({ id: a.id, name: a.name, teamName: a.teamName }))} />
      <Pager page={data.page} pageSize={data.pageSize} total={data.total} href={(p) => link({ page: p })} />

      <Panel title="AI-консультант" hint="вопросы по данным продаж и команды">
        <p className="text-sm text-ink-2">«Почему команда сегодня не выполняет план?», «Какие магазины требуют визита?», «Кто из агентов хуже выполняет маршрут?», «Кому завтра дать больше точек?»</p>
        {canAsk && consultantHref ? (
          <a href={consultantHref} className={`${buttonClass.outline} mt-3`}>
            <MessageSquareText className="size-4" /> Открыть консультанта
          </a>
        ) : canAsk ? (
          <p className="mt-2 text-xs text-ink-3">Консультант открывается в OneBase.</p>
        ) : (
          <p className="mt-2 text-xs text-ink-3">Консультант доступен ролям с правом «AI-консультант» (у РМ оно есть по умолчанию).</p>
        )}
      </Panel>
    </FieldPage>
  );
}
