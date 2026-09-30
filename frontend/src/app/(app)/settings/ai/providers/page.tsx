import { AiSettingsNav } from "@/components/ai/AiSettingsNav";
import { ProvidersSettings } from "@/components/ai/ProvidersSettings";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { aiCrumbs, type AiProviderView } from "@/lib/ai";
import { apiGet } from "@/lib/server-api";

export const metadata = { title: "Провайдеры AI · Настройки · OneBase" };

export default async function AiProvidersPage() {
  const providers = await apiGet<AiProviderView[]>("/api/ai/providers", "/settings/ai/providers");
  return (
    <>
      <PageHeader
        title="AI"
        subtitle="Ключи API OpenAI и Anthropic"
        back="/settings"
        breadcrumbs={[...aiCrumbs, { label: "AI", href: "/settings/ai" }, { label: "Провайдеры" }]}
      />
      <PageBody>
        <AiSettingsNav />
        <ProvidersSettings initial={providers} />
      </PageBody>
    </>
  );
}
