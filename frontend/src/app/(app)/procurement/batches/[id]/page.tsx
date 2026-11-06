import { notFound, redirect } from "next/navigation";
import { BatchCardView } from "@/components/procurement/BatchesCard";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { BatchCard, RequestNav } from "@/lib/procurement";
import { apiGet, apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Партия · OneBase") };
}

/** Карточка партии: позиция, номер производителя, поставщик, даты, остаток по складам и вся история движений — по всем складам. */
export default async function BatchPage({ params }: { params: Promise<{ id: string }> }) {
  await requireRequests();
  const { id } = await params;
  const path = `/procurement/batches/${id}`;
  const nav = await apiGet<RequestNav>("/api/procurement/nav", path);
  if (!nav.seesStock) redirect("/requests");
  const card = await apiGetOrNull<BatchCard>(`/api/procurement/batches/${id}`, path);
  if (!card) notFound();
  const t = await getT();
  return (
    <>
      <PageHeader
        title={card.batch.number}
        subtitle={`${card.batch.itemName} · ${card.batch.sku}`}
        back="/procurement/batches"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Снабжение"), href: "/procurement" },
          { label: t("Партии"), href: "/procurement/batches" },
          { label: card.batch.number },
        ]}
      />
      <PageBody>
        <BatchCardView card={card} />
      </PageBody>
    </>
  );
}
