import { AiSettingsNav } from "@/components/ai/AiSettingsNav";
import { LogsView } from "@/components/ai/LogsView";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { aiCrumbs, type AiAgentListItem, type AiLogRow } from "@/lib/ai";
import { apiGet } from "@/lib/server-api";

export const metadata = { title: "Журнал AI · Настройки · OneBase" };

export default async function AiLogsPage() {
  const path = "/settings/ai/logs";
  const [logs, agents] = await Promise.all([apiGet<AiLogRow[]>("/api/ai/logs?take=50", path), apiGet<AiAgentListItem[]>("/api/ai/agents/settings", path)]);
  return (
    <>
      <PageHeader
        title="AI"
        subtitle="Журнал запросов к консультанту и AI-сотрудникам"
        back="/settings"
        breadcrumbs={[...aiCrumbs, { label: "AI", href: "/settings/ai" }, { label: "Журнал" }]}
      />
      <PageBody>
        <AiSettingsNav />
        <LogsView initial={logs} agentNames={Object.fromEntries(agents.map((a) => [a.code, a.name]))} />
      </PageBody>
    </>
  );
}
