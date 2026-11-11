import { TelegramSettings } from "@/components/telegram/TelegramSettings";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { TelegramOverview } from "@/lib/telegram";
import { apiGet, apiSession } from "@/lib/server-api";
import type { Me } from "@/lib/users";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Telegram · Настройки · OneBase") };
}

export default async function TelegramSettingsPage() {
  const [overview, me] = await Promise.all([apiGet<TelegramOverview>("/api/settings/telegram", "/settings/telegram"), apiSession<Me>("/api/auth/me")]);
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
        {/* Автоматические отчёты строятся по данным «Продаж»: без права на них ссылок нет (без ответа API — показываем, страница проверит сама). */}
        <TelegramSettings initial={overview} reports={!me || me.permissions.includes("sales.read")} />
      </PageBody>
    </>
  );
}
