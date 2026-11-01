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

/** Новая заявка на закупку: позиции, причина, приоритет, желаемая дата, вложения; черновик или сразу отправка. */
export default async function NewRequestPage() {
  await requireRequests();
  const t = await getT();
  const form = await apiGet<RequestFormData>("/api/procurement/form", "/requests/new");
  return (
    <>
      <PageHeader
        title={t("Новая заявка")}
        subtitle={t("Что нужно купить, сколько и зачем — снабжение проверит заявку и передаст директору")}
        back="/requests/my"
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Заявки"), href: "/requests" }, { label: t("Новая заявка") }]}
      />
      <PageBody>
        <RequestForm form={form} />
      </PageBody>
    </>
  );
}
