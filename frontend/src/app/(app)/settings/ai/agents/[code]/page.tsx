import { notFound } from "next/navigation";
import { AgentSettings } from "@/components/ai/AgentSettings";
import { AiSettingsNav } from "@/components/ai/AiSettingsNav";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { aiCrumbs, type AiAgentSettings, type AiModelView, type AiProviderView } from "@/lib/ai";
import { apiGet, apiGetOrNull } from "@/lib/server-api";

export const metadata = { title: "AI-сотрудник · Настройки · OneBase" };

export default async function AiAgentSettingsPage({ params }: { params: Promise<{ code: string }> }) {
  const { code } = await params;
  const path = `/settings/ai/agents/${code}`;
  const [agent, models, providers] = await Promise.all([
    apiGetOrNull<AiAgentSettings>(`/api/ai/agents/${encodeURIComponent(code)}/settings`, path),
    apiGet<AiModelView[]>("/api/ai/models", path),
    apiGet<AiProviderView[]>("/api/ai/providers", path),
  ]);
  if (!agent) notFound();
  return (
    <>
      <PageHeader
        title={agent.name}
        subtitle={agent.role ?? undefined}
        back="/settings/ai/agents"
        breadcrumbs={[...aiCrumbs, { label: "AI", href: "/settings/ai" }, { label: "Агенты", href: "/settings/ai/agents" }, { label: agent.name }]}
      />
      <PageBody>
        <AiSettingsNav />
        <AgentSettings key={agent.code} initial={agent} models={models} providers={providers} />
      </PageBody>
    </>
  );
}
