import { LogsSettings } from "@/components/settings/LogsSettings";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { LogsView } from "@/lib/logs";
import type { SyncStatus } from "@/lib/sales/types";
import { apiGet, apiTry } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Журнал ошибок · Настройки · OneBase") };
}

export default async function LogsSettingsPage() {
  const path = "/settings/logs";
  const [logs, sync] = await Promise.all([apiGet<LogsView>("/api/logs?days=7", path), apiTry<SyncStatus>("/api/sales/status")]);
  const t = await getT();
  return (
    <>
      <PageHeader
        title={t("Журнал ошибок")}
        subtitle={t("Ошибки интеграции Linko, синхронизации и сервера")}
        back="/settings"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Настройки"), href: "/settings" },
          { label: t("Журнал ошибок") },
        ]}
      />
      <PageBody>
        <LogsSettings initial={logs} initialSync={sync} />
      </PageBody>
    </>
  );
}
