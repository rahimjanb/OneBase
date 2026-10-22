import { notFound } from "next/navigation";
import { AgentSettings } from "@/components/ai/AgentSettings";
import { AiSettingsNav } from "@/components/ai/AiSettingsNav";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { aiCrumbs, type AiAgentSettings, type AiModelView, type AiProviderView } from "@/lib/ai";
import { apiGet, apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("AI-сотрудник · Настройки · OneBase") };
}

export default async function AiAgentSettingsPage({ params }: { params: Promise<{ code: string }> }) {
  const t = await getT();
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
        title={t(agent.name)}
        subtitle={agent.role ? t(agent.role) : undefined}
        back="/settings/ai/agents"
        breadcrumbs={[...aiCrumbs, { label: "AI", href: "/settings/ai" }, { label: t("Агенты"), href: "/settings/ai/agents" }, { label: t(agent.name) }]}
      />
      <PageBody>
        <AiSettingsNav />
        <AgentSettings key={agent.code} initial={agent} models={models} providers={providers} />
      </PageBody>
    </>
  );
}
