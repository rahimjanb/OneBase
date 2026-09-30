import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { UsersSettings } from "@/components/settings/UsersSettings";
import { apiGet } from "@/lib/server-api";
import type { UsersView } from "@/lib/users";

export const metadata = { title: "Пользователи и роли · Настройки · OneBase" };

export default async function UsersSettingsPage() {
  const data = await apiGet<UsersView>("/api/users", "/settings/users");
  return (
    <>
      <PageHeader
        title="Пользователи и роли"
        subtitle="Кто входит в OneBase и что ему доступно"
        back="/settings"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: "Настройки", href: "/settings" },
          { label: "Пользователи и роли" },
        ]}
      />
      <PageBody>
        <UsersSettings initial={data} />
      </PageBody>
    </>
  );
}
