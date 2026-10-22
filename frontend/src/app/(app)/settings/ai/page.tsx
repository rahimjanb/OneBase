import { AiSettingsNav } from "@/components/ai/AiSettingsNav";
import { GeneralSettings } from "@/components/ai/GeneralSettings";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { aiCrumbs, type AiModelView, type AiProviderView, type AiSettingsView } from "@/lib/ai";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("AI · Настройки · OneBase") };
}

export default async function AiSettingsPage() {
  const t = await getT();
  const path = "/settings/ai";
  const [settings, models, providers] = await Promise.all([
    apiGet<AiSettingsView>("/api/ai/settings", path),
    apiGet<AiModelView[]>("/api/ai/models", path),
    apiGet<AiProviderView[]>("/api/ai/providers", path),
  ]);
  return (
    <>
      <PageHeader title="AI" subtitle={t("Консультант и AI-сотрудники: провайдеры, модели, параметры")} back="/settings" breadcrumbs={[...aiCrumbs, { label: "AI" }]} />
      <PageBody>
        <AiSettingsNav />
        <GeneralSettings initial={settings} models={models} providers={providers} />
      </PageBody>
    </>
  );
}
