import { redirect } from "next/navigation";
import { AgentsOverview } from "@/components/agents/AgentsOverview";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { AgentDepartment } from "@/lib/agents";
import { apiGet } from "@/lib/server-api";
import type { Me } from "@/lib/users";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("ИИ-агенты · Настройки · OneBase") };
}

export default async function AgentsPage() {
  const me = await apiGet<Me>("/api/auth/me", "/settings/agents");
  if (!me.agentDepartments?.length) redirect("/no-access?from=%2Fsettings%2Fagents");
  const departments = await apiGet<AgentDepartment[]>("/api/agents/departments", "/settings/agents");
  const t = await getT();
  return (
    <>
      <PageHeader
        title={t("ИИ-агенты")}
        subtitle={t("Агенты отделов: переписка в Telegram от имени отдела, их настройки, ограничения и ответы")}
        back="/settings"
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Настройки"), href: "/settings" }, { label: t("ИИ-агенты") }]}
      />
      <PageBody>
        <AgentsOverview initial={departments} />
      </PageBody>
    </>
  );
}
