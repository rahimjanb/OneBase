import { AiSettingsNav } from "@/components/ai/AiSettingsNav";
import { ToolsSettings } from "@/components/ai/ToolsSettings";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { aiCrumbs, type AiAgentListItem, type AiToolView } from "@/lib/ai";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Инструменты AI · Настройки · OneBase") };
}

export default async function AiToolsPage() {
  const t = await getT();
  const path = "/settings/ai/tools";
  const [tools, agents] = await Promise.all([apiGet<AiToolView[]>("/api/ai/tools", path), apiGet<AiAgentListItem[]>("/api/ai/agents/settings", path)]);
  return (
    <>
      <PageHeader
        title="AI"
        subtitle={t("Инструменты, через которые AI-сотрудники читают данные OneBase")}
        back="/settings"
        breadcrumbs={[...aiCrumbs, { label: "AI", href: "/settings/ai" }, { label: t("Инструменты") }]}
      />
      <PageBody>
        <AiSettingsNav />
        <ToolsSettings tools={tools} agents={agents.map((a) => ({ code: a.code, name: a.name }))} />
      </PageBody>
    </>
  );
}
