import { CampaignsList } from "@/components/campaigns/CampaignsList";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { CampaignAccount, CampaignListItem } from "@/lib/campaigns";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Рассылки · OneBase") };
}

export default async function CampaignsPage() {
  const path = "/campaigns";
  const [campaigns, accounts] = await Promise.all([
    apiGet<CampaignListItem[]>("/api/telegram/campaigns", path),
    apiGet<CampaignAccount[]>("/api/telegram/campaigns/accounts", path),
  ]);
  const t = await getT();
  return (
    <>
      <PageHeader
        title={t("Рассылки")}
        subtitle={t("Сообщения, файлы и отчёты в чаты Telegram — сразу или по расписанию")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Рассылки") }]}
      />
      <PageBody>
        <CampaignsList initial={campaigns} accounts={accounts} />
      </PageBody>
    </>
  );
}
