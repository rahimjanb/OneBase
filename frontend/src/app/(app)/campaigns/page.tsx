import { CampaignsList } from "@/components/campaigns/CampaignsList";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import type { CampaignAccount, CampaignListItem } from "@/lib/campaigns";
import { apiGet } from "@/lib/server-api";

export const metadata = { title: "Рассылки · OneBase" };

export default async function CampaignsPage() {
  const path = "/campaigns";
  const [campaigns, accounts] = await Promise.all([
    apiGet<CampaignListItem[]>("/api/telegram/campaigns", path),
    apiGet<CampaignAccount[]>("/api/telegram/campaigns/accounts", path),
  ]);
  return (
    <>
      <PageHeader title="Рассылки" subtitle="Сообщения, файлы и отчёты в чаты Telegram — сразу или по расписанию" breadcrumbs={[{ label: "OneBase", href: "/" }, { label: "Рассылки" }]} />
      <PageBody>
        <CampaignsList initial={campaigns} accounts={accounts} />
      </PageBody>
    </>
  );
}
