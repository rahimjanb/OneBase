import { notFound } from "next/navigation";
import { TenderDetailView } from "@/components/procurement/TenderDetailView";
import { requireRequests } from "@/components/requests/RequestListPage";
import { getT } from "@/i18n/server";
import type { TenderDetail } from "@/lib/procurement";
import { apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Тендер · OneBase") };
}

/** Карточка тендерной закупки: доступ решает API (снабжение, директор). */
export default async function TenderPage({ params }: { params: Promise<{ id: string }> }) {
  await requireRequests();
  const { id } = await params;
  const tender = await apiGetOrNull<TenderDetail>(`/api/procurement/tenders/${id}`, `/procurement/tenders/${id}`);
  if (!tender) notFound();
  return <TenderDetailView tender={tender} />;
}
