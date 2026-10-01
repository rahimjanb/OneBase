import { LogsSettings } from "@/components/settings/LogsSettings";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import type { LogsView } from "@/lib/logs";
import type { SyncStatus } from "@/lib/sales/types";
import { apiGet, apiTry } from "@/lib/server-api";

export const metadata = { title: "Журнал ошибок · Настройки · OneBase" };

export default async function LogsSettingsPage() {
  const path = "/settings/logs";
  const [logs, sync] = await Promise.all([apiGet<LogsView>("/api/logs?days=7", path), apiTry<SyncStatus>("/api/sales/status")]);
  return (
    <>
      <PageHeader
        title="Журнал ошибок"
        subtitle="Ошибки интеграции Linko, синхронизации и сервера"
        back="/settings"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: "Настройки", href: "/settings" },
          { label: "Журнал ошибок" },
        ]}
      />
      <PageBody>
        <LogsSettings initial={logs} initialSync={sync} />
      </PageBody>
    </>
  );
}
