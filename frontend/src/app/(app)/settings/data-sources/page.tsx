import { DataSourcesSettings } from "@/components/settings/DataSourcesSettings";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import type { DataSourcesOverview } from "@/lib/data-sources";
import { apiGet } from "@/lib/server-api";

export const metadata = { title: "Источники данных · Настройки · OneBase" };

export default async function DataSourcesPage() {
  const overview = await apiGet<DataSourcesOverview>("/api/settings/data-sources", "/settings/data-sources");
  return (
    <>
      <PageHeader
        title="Источники данных"
        subtitle="Linko и Google Sheets: откуда каждый раздел продаж берёт данные и как часто они обновляются"
        back="/settings"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: "Настройки", href: "/settings" },
          { label: "Источники данных" },
        ]}
      />
      <PageBody>
        <DataSourcesSettings initial={overview} />
      </PageBody>
    </>
  );
}
