import { TelegramSettings } from "@/components/telegram/TelegramSettings";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { TelegramOverview } from "@/lib/telegram";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Telegram · Настройки · OneBase") };
}

export default async function TelegramSettingsPage() {
  const overview = await apiGet<TelegramOverview>("/api/settings/telegram", "/settings/telegram");
  const t = await getT();
  return (
    <>
      <PageHeader
        title="Telegram"
        subtitle={t("Аккаунты Telegram, AI-агент, кому и как он отвечает")}
        back="/settings"
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Настройки"), href: "/settings" }, { label: "Telegram" }]}
      />
      <PageBody>
        <TelegramSettings initial={overview} />
      </PageBody>
    </>
  );
}
