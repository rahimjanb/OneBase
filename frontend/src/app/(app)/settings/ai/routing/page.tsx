import { AiSettingsNav } from "@/components/ai/AiSettingsNav";
import { RoutingView } from "@/components/ai/RoutingView";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { aiCrumbs, type AiRoutingView } from "@/lib/ai";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Модели по задачам · Настройки · OneBase") };
}

export default async function AiRoutingPage({ searchParams }: { searchParams: Promise<{ days?: string }> }) {
  const t = await getT();
  const { days } = await searchParams;
  const path = "/settings/ai/routing";
  const data = await apiGet<AiRoutingView>(`/api/ai/routing?days=${Number(days) || 30}`, path);
  return (
    <>
      <PageHeader
        title="AI"
        subtitle={t("Простые задачи на отдельной модели: сравнение с основной на одинаковых задачах")}
        back="/settings"
        breadcrumbs={[...aiCrumbs, { label: "AI", href: "/settings/ai" }, { label: t("Модели по задачам") }]}
      />
      <PageBody>
        <AiSettingsNav />
        <RoutingView initial={data} />
      </PageBody>
    </>
  );
}
