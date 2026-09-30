import { AiSettingsNav } from "@/components/ai/AiSettingsNav";
import { ToolsSettings } from "@/components/ai/ToolsSettings";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { aiCrumbs, type AiAgentListItem, type AiToolView } from "@/lib/ai";
import { apiGet } from "@/lib/server-api";

export const metadata = { title: "Инструменты AI · Настройки · OneBase" };

export default async function AiToolsPage() {
  const path = "/settings/ai/tools";
  const [tools, agents] = await Promise.all([apiGet<AiToolView[]>("/api/ai/tools", path), apiGet<AiAgentListItem[]>("/api/ai/agents/settings", path)]);
  return (
    <>
      <PageHeader
        title="AI"
        subtitle="Инструменты, через которые AI-сотрудники читают данные OneBase"
        back="/settings"
        breadcrumbs={[...aiCrumbs, { label: "AI", href: "/settings/ai" }, { label: "Инструменты" }]}
      />
      <PageBody>
        <AiSettingsNav />
        <ToolsSettings tools={tools} agents={agents.map((a) => ({ code: a.code, name: a.name }))} />
      </PageBody>
    </>
  );
}
