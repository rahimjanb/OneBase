import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { UsersSettings } from "@/components/settings/UsersSettings";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import type { UsersView } from "@/lib/users";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Пользователи и роли · Настройки · OneBase") };
}

export default async function UsersSettingsPage() {
  const data = await apiGet<UsersView>("/api/users", "/settings/users");
  const t = await getT();
  return (
    <>
      <PageHeader
        title={t("Пользователи и роли")}
        subtitle={t("Кто входит в OneBase и что ему доступно")}
        back="/settings"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Настройки"), href: "/settings" },
          { label: t("Пользователи и роли") },
        ]}
      />
      <PageBody>
        <UsersSettings initial={data} />
      </PageBody>
    </>
  );
}
