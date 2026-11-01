import { notFound } from "next/navigation";
import { RequestDetailView } from "@/components/requests/RequestDetailView";
import { requireRequests } from "@/components/requests/RequestListPage";
import { getT } from "@/i18n/server";
import type { RequestDetail, RequestNav } from "@/lib/procurement";
import { apiGet, apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Заявка · OneBase") };
}

/** Карточка заявки на закупку: доступ решает API (чужую заявку не показывает — «Нет доступа»). */
export default async function RequestPage({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<{ files?: string }> }) {
  await requireRequests();
  const { id } = await params;
  const [detail, nav] = await Promise.all([apiGetOrNull<RequestDetail>(`/api/procurement/requests/${id}`, `/requests/${id}`), apiGet<RequestNav>("/api/procurement/nav", `/requests/${id}`)]);
  if (!detail) notFound();
  const mine = detail.can.edit || detail.can.submit || detail.can.cancel;
  return <RequestDetailView detail={detail} filesFailed={(await searchParams).files === "failed"} backHref={mine || !nav.readsAll ? "/requests/my" : "/requests/all"} />;
}
