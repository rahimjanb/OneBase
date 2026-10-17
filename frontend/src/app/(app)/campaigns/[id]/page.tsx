import { notFound } from "next/navigation";
import { CampaignEditor } from "@/components/campaigns/CampaignEditor";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import type { Campaign, CampaignAccount } from "@/lib/campaigns";
import { apiGet, apiGetOrNull } from "@/lib/server-api";

export const metadata = { title: "Рассылка · OneBase" };

export default async function CampaignPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const path = `/campaigns/${id}`;
  const [campaign, accounts] = await Promise.all([
    apiGetOrNull<Campaign>(`/api/telegram/campaigns/${encodeURIComponent(id)}`, path),
    apiGet<CampaignAccount[]>("/api/telegram/campaigns/accounts", path),
  ]);
  if (!campaign) notFound();
  return (
    <>
      <PageHeader
        title={campaign.name}
        subtitle={`Аккаунт «${campaign.accountName}»`}
        back="/campaigns"
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: "Рассылки", href: "/campaigns" }, { label: campaign.name }]}
      />
      <PageBody>
        <CampaignEditor key={campaign.id} initial={campaign} accounts={accounts} />
      </PageBody>
    </>
  );
}
