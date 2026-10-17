import { TelegramSettings } from "@/components/telegram/TelegramSettings";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import type { TelegramOverview } from "@/lib/telegram";
import { apiGet } from "@/lib/server-api";

export const metadata = { title: "Telegram · Настройки · OneBase" };

export default async function TelegramSettingsPage() {
  const overview = await apiGet<TelegramOverview>("/api/settings/telegram", "/settings/telegram");
  return (
    <>
      <PageHeader
        title="Telegram"
        subtitle="Аккаунты Telegram, AI-агент, кому и как он отвечает"
        back="/settings"
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: "Настройки", href: "/settings" }, { label: "Telegram" }]}
      />
      <PageBody>
        <TelegramSettings initial={overview} />
      </PageBody>
    </>
  );
}
