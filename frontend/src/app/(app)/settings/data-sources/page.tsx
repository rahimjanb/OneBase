import { DataSourcesSettings } from "@/components/settings/DataSourcesSettings";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { DataSourcesOverview } from "@/lib/data-sources";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Источники данных · Настройки · OneBase") };
}

export default async function DataSourcesPage() {
  const overview = await apiGet<DataSourcesOverview>("/api/settings/data-sources", "/settings/data-sources");
  const t = await getT();
  return (
    <>
      <PageHeader
        title={t("Источники данных")}
        subtitle={t("Linko и Google Sheets: откуда каждый раздел продаж берёт данные и как часто они обновляются")}
        back="/settings"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Настройки"), href: "/settings" },
          { label: t("Источники данных") },
        ]}
      />
      <PageBody>
        <DataSourcesSettings initial={overview} />
      </PageBody>
    </>
  );
}
