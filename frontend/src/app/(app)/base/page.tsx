import { BaseOverview } from "@/components/files/BaseOverview";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { DepartmentSummary } from "@/lib/files";
import { apiGet } from "@/lib/server-api";

export default async function FilesPage() {
  const departments = await apiGet<DepartmentSummary[]>("/api/files/departments", "/base");
  const t = await getT();
  return (
    <>
      <PageHeader
        title={t("Файлы")}
        subtitle={t("Файлы отделов — на сайте и в подключённой папке Windows, это одно хранилище")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Файлы") }]}
      />
      <PageBody>
        <BaseOverview departments={departments} />
      </PageBody>
    </>
  );
}
