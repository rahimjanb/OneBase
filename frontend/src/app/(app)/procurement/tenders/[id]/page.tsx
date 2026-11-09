import { notFound } from "next/navigation";
import { TenderDetailView } from "@/components/procurement/TenderDetailView";
import { requireRequests } from "@/components/requests/RequestListPage";
import { getT } from "@/i18n/server";
import type { ArrivalPage, ExpectedDelivery } from "@/lib/arrivals";
import type { RequestNav, TenderDetail } from "@/lib/procurement";
import { apiGetOrNull, apiTry } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Тендер · OneBase") };
}

/** Карточка тендерной закупки: доступ решает API (снабжение, директор, кладовщик); машины по тендеру и «Машина приехала» — кладовщику. */
export default async function TenderPage({ params }: { params: Promise<{ id: string }> }) {
  await requireRequests();
  const { id } = await params;
  const tender = await apiGetOrNull<TenderDetail>(`/api/procurement/tenders/${id}`, `/procurement/tenders/${id}`);
  if (!tender) notFound();
  // Машины по тендеру и что ещё можно привезти (для «Машина приехала»): видят кладовщик, снабжение, директор — у кого нет, тем пусто.
  const [nav, expected, arrivals] = await Promise.all([
    apiTry<RequestNav>("/api/procurement/nav"),
    tender.status === "Contracted" ? apiTry<ExpectedDelivery[]>(`/api/procurement/arrivals/expected?tender=${id}`) : null,
    apiTry<ArrivalPage>(`/api/procurement/arrivals?status=all&tender=${id}&pageSize=50`),
  ]);
  return (
    <TenderDetailView
      tender={tender}
      expected={expected?.[0] ?? null}
      arrivals={arrivals?.items ?? []}
      seesStock={nav?.seesStock ?? false}
      seesRequests={Boolean(nav?.readsAll || nav?.isDirector)}
    />
  );
}
