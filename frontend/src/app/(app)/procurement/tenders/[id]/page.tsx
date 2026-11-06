import { notFound } from "next/navigation";
import { TenderDetailView } from "@/components/procurement/TenderDetailView";
import { requireRequests } from "@/components/requests/RequestListPage";
import { getT } from "@/i18n/server";
import type { RequestNav, TenderDetail, WarehousesView } from "@/lib/procurement";
import { apiGetOrNull, apiTry } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Тендер · OneBase") };
}

/** Карточка тендерной закупки: доступ решает API (снабжение, директор, кладовщик). */
export default async function TenderPage({ params }: { params: Promise<{ id: string }> }) {
  await requireRequests();
  const { id } = await params;
  const tender = await apiGetOrNull<TenderDetail>(`/api/procurement/tenders/${id}`, `/procurement/tenders/${id}`);
  if (!tender) notFound();
  const [nav, stores] = await Promise.all([apiTry<RequestNav>("/api/procurement/nav"), tender.can.receive ? apiTry<WarehousesView>("/api/procurement/warehouses") : null]);
  return (
    <TenderDetailView
      tender={tender}
      warehouses={stores?.warehouses ?? []}
      seesStock={nav?.seesStock ?? false}
      seesRequests={Boolean(nav?.readsAll || nav?.isDirector)}
    />
  );
}
