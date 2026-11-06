import { notFound, redirect } from "next/navigation";
import { QualityCheckView } from "@/components/quality/QualityCheckView";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { canOpen, homeFor } from "@/lib/navigation";
import type { QualityCheckCard } from "@/lib/quality";
import { apiGetOrNull, apiSession } from "@/lib/server-api";
import type { Me } from "@/lib/users";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Проверка качества · OneBase") };
}

/** Проверка качества партии: шаги по порядку (документы, лаборатория, решение), спецификация, результаты, документы, история. */
export default async function QualityCheckPage({ params }: { params: Promise<{ id: string }> }) {
  const me = await apiSession<Me>("/api/auth/me");
  if (me && !canOpen("/production", me.permissions)) redirect(homeFor(me.permissions));
  const { id } = await params;
  const card = await apiGetOrNull<QualityCheckCard>(`/api/quality/checks/${id}`, `/production/quality/${id}`);
  if (!card) notFound();
  const t = await getT();
  return (
    <>
      <PageHeader
        title={card.check.number}
        subtitle={`${card.check.itemName} · ${card.check.batchNumber}`}
        back="/production/quality"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Производство"), href: "/production" },
          { label: t("Качество"), href: "/production/quality" },
          { label: card.check.number },
        ]}
      />
      <PageBody>
        <QualityCheckView card={card} />
      </PageBody>
    </>
  );
}
