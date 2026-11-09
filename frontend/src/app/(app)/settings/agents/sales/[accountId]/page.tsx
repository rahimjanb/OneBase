import { notFound } from "next/navigation";
import { SalesAgentPanel } from "@/components/agents/SalesAgentPanel";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { TelegramOverview } from "@/lib/telegram";
import { apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Агент продаж · Настройки · OneBase") };
}

/** Агент продаж номера Telegram — те же настройки, что во «Настройки → Telegram», собранные в разделе «ИИ-агенты». */
export default async function SalesAgentPage({ params }: { params: Promise<{ accountId: string }> }) {
  const { accountId } = await params;
  const overview = await apiGetOrNull<TelegramOverview>("/api/settings/telegram", `/settings/agents/sales/${accountId}`);
  const account = overview?.accounts.find((a) => a.id === accountId);
  if (!account) notFound();
  const t = await getT();
  return (
    <>
      <PageHeader
        title={`${t("Агент продаж")} · ${account.name}`}
        subtitle={t("Отвечает в Telegram сотрудникам и клиентам по данным OneBase — с правами, которые им выданы")}
        back="/settings/agents"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Настройки"), href: "/settings" },
          { label: t("ИИ-агенты"), href: "/settings/agents" },
          { label: account.name },
        ]}
      />
      <PageBody>
        <SalesAgentPanel initial={account} />
      </PageBody>
    </>
  );
}
