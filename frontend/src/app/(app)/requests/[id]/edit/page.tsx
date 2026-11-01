import { notFound, redirect } from "next/navigation";
import { RequestForm } from "@/components/requests/RequestForm";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { RequestDetail, RequestFormData } from "@/lib/procurement";
import { apiGet, apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Изменить заявку · OneBase") };
}

/** Правка черновика или заявки, возвращённой на доработку (только инициатор; иначе — в карточку заявки). */
export default async function EditRequestPage({ params }: { params: Promise<{ id: string }> }) {
  await requireRequests();
  const { id } = await params;
  const t = await getT();
  const [request, form] = await Promise.all([
    apiGetOrNull<RequestDetail>(`/api/procurement/requests/${id}`, `/requests/${id}/edit`),
    apiGet<RequestFormData>("/api/procurement/form", `/requests/${id}/edit`),
  ]);
  if (!request) notFound();
  if (!request.can.edit) redirect(`/requests/${id}`);
  return (
    <>
      <PageHeader
        title={t("Изменить заявку")}
        subtitle={`${request.number} · ${request.title}`}
        back={`/requests/${id}`}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Заявки"), href: "/requests" }, { label: request.number, href: `/requests/${id}` }, { label: t("Изменить") }]}
      />
      <PageBody>
        <RequestForm form={form} initial={request} />
      </PageBody>
    </>
  );
}
