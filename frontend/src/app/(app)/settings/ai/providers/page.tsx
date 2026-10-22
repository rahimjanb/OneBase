import { AiSettingsNav } from "@/components/ai/AiSettingsNav";
import { ProvidersSettings } from "@/components/ai/ProvidersSettings";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { aiCrumbs, type AiProviderView } from "@/lib/ai";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Провайдеры AI · Настройки · OneBase") };
}

export default async function AiProvidersPage() {
  const t = await getT();
  const providers = await apiGet<AiProviderView[]>("/api/ai/providers", "/settings/ai/providers");
  return (
    <>
      <PageHeader
        title="AI"
        subtitle={t("Ключи API OpenAI и Anthropic")}
        back="/settings"
        breadcrumbs={[...aiCrumbs, { label: "AI", href: "/settings/ai" }, { label: t("Провайдеры") }]}
      />
      <PageBody>
        <AiSettingsNav />
        <ProvidersSettings initial={providers} />
      </PageBody>
    </>
  );
}
