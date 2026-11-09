import { RequestForm } from "@/components/requests/RequestForm";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { RequestFormData } from "@/lib/procurement";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Новая заявка · OneBase") };
}

/**
 * Новая заявка на закупку: тип, позиции, причина, приоритет, желаемая дата, вложения; черновик или сразу отправка. ?kind=raw — сразу
 * «Закупка сырья» (если она пользователю разрешена).
 */
export default async function NewRequestPage({ searchParams }: { searchParams: Promise<{ kind?: string }> }) {
  await requireRequests();
  const t = await getT();
  const sp = await searchParams;
  const form = await apiGet<RequestFormData>("/api/procurement/form", "/requests/new");
  return (
    <>
      <PageHeader
        title={t("Новая заявка")}
        subtitle={t("Что нужно купить, сколько и зачем — заявку согласуют по маршруту и передадут в снабжение")}
        back="/requests/my"
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Заявки"), href: "/requests" }, { label: t("Новая заявка") }]}
      />
      <PageBody>
        <RequestForm form={form} defaultKind={sp.kind === "raw" ? "Raw" : "Regular"} />
      </PageBody>
    </>
  );
}
