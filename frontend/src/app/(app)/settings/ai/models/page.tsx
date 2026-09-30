import { AiSettingsNav } from "@/components/ai/AiSettingsNav";
import { ModelsSettings } from "@/components/ai/ModelsSettings";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { aiCrumbs, type AiModelView, type AiProviderView } from "@/lib/ai";
import { apiGet } from "@/lib/server-api";

export const metadata = { title: "Модели AI · Настройки · OneBase" };

export default async function AiModelsPage() {
  const path = "/settings/ai/models";
  const [models, providers] = await Promise.all([apiGet<AiModelView[]>("/api/ai/models", path), apiGet<AiProviderView[]>("/api/ai/providers", path)]);
  return (
    <>
      <PageHeader
        title="AI"
        subtitle="Какие модели провайдеров можно назначать консультанту и AI-сотрудникам"
        back="/settings"
        breadcrumbs={[...aiCrumbs, { label: "AI", href: "/settings/ai" }, { label: "Модели" }]}
      />
      <PageBody>
        <AiSettingsNav />
        <ModelsSettings initial={models} providers={providers} />
      </PageBody>
    </>
  );
}
