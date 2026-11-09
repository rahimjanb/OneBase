import { notFound } from "next/navigation";
import { ArrivalDetailView } from "@/components/procurement/ArrivalDetailView";
import { requireRequests } from "@/components/requests/RequestListPage";
import { getT } from "@/i18n/server";
import type { ArrivalDetail } from "@/lib/arrivals";
import type { RequestNav } from "@/lib/procurement";
import { apiGetOrNull, apiTry } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Поставка · OneBase") };
}

/** Поставка (приезд машины): доступ решает API — кладовщик, лаборатория, снабжение, аналитик, директор, финансист. */
export default async function ArrivalPage({ params }: { params: Promise<{ id: string }> }) {
  await requireRequests();
  const { id } = await params;
  const [data, nav] = await Promise.all([apiGetOrNull<ArrivalDetail>(`/api/procurement/arrivals/${id}`, `/procurement/arrivals/${id}`), apiTry<RequestNav>("/api/procurement/nav")]);
  if (!data) notFound();
  return (
    <ArrivalDetailView
      data={data}
      seesRequests={Boolean(nav?.readsAll || nav?.isDirector || nav?.decides)}
      seesTenders={Boolean(nav?.readsAll || nav?.isDirector || nav?.warehouse)}
    />
  );
}
