import { notFound } from "next/navigation";
import { AgentEditor } from "@/components/agents/AgentEditor";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { AgentView } from "@/lib/agents";
import { apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("ИИ-агент · Настройки · OneBase") };
}

export default async function AgentPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const agent = await apiGetOrNull<AgentView>(`/api/agents/${encodeURIComponent(id)}`, `/settings/agents/${id}`);
  if (!agent) notFound();
  const t = await getT();
  return (
    <>
      <PageHeader
        title={agent.displayName ? `${agent.name} · ${agent.displayName}` : agent.name}
        subtitle={t("ИИ-агент отдела «{department}»", { department: agent.departmentName })}
        back="/settings/agents"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Настройки"), href: "/settings" },
          { label: t("ИИ-агенты"), href: "/settings/agents" },
          { label: agent.name },
        ]}
      />
      <PageBody>
        <AgentEditor key={agent.id} initial={agent} />
      </PageBody>
    </>
  );
}
