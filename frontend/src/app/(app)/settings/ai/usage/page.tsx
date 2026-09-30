import { AiSettingsNav } from "@/components/ai/AiSettingsNav";
import { UsageView } from "@/components/ai/UsageView";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { aiCrumbs, type AiAgentListItem, type AiUsageView } from "@/lib/ai";
import { apiGet } from "@/lib/server-api";

export const metadata = { title: "Использование AI · Настройки · OneBase" };

export default async function AiUsagePage({ searchParams }: { searchParams: Promise<{ days?: string }> }) {
  const { days } = await searchParams;
  const path = "/settings/ai/usage";
  const [usage, agents] = await Promise.all([
    apiGet<AiUsageView>(`/api/ai/usage?days=${Number(days) || 30}`, path),
    apiGet<AiAgentListItem[]>("/api/ai/agents/settings", path),
  ]);
  return (
    <>
      <PageHeader
        title="AI"
        subtitle="Запросы, токены и стоимость — по моделям, пользователям и AI-сотрудникам"
        back="/settings"
        breadcrumbs={[...aiCrumbs, { label: "AI", href: "/settings/ai" }, { label: "Использование" }]}
      />
      <PageBody>
        <AiSettingsNav />
        <UsageView data={usage} agentNames={Object.fromEntries(agents.map((a) => [a.code, a.name]))} />
      </PageBody>
    </>
  );
}
