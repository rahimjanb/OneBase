import { AiSettingsNav } from "@/components/ai/AiSettingsNav";
import { KnowledgeSettings } from "@/components/ai/KnowledgeSettings";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { aiCrumbs, type AiSettingsView, type KnowledgeList } from "@/lib/ai";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("База знаний AI · Настройки · OneBase") };
}

export default async function AiKnowledgePage() {
  const t = await getT();
  const path = "/settings/ai/knowledge";
  const [list, settings] = await Promise.all([apiGet<KnowledgeList>("/api/ai/knowledge/documents", path), apiGet<AiSettingsView>("/api/ai/settings", path)]);
  const embedding = settings.embedding ? `${settings.embedding.provider}/${settings.embedding.model}` : null;
  return (
    <>
      <PageHeader
        title="AI"
        subtitle={t("Документы, в которых AI-сотрудники ищут ответы: регламенты, отчёты, выгрузки")}
        back="/settings"
        breadcrumbs={[...aiCrumbs, { label: "AI", href: "/settings/ai" }, { label: t("База знаний") }]}
      />
      <PageBody>
        <AiSettingsNav />
        <KnowledgeSettings initial={list} embeddingModel={embedding} />
      </PageBody>
    </>
  );
}
