import { BaseOverview } from "@/components/files/BaseOverview";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import type { DepartmentSummary } from "@/lib/files";
import { apiGet } from "@/lib/server-api";

export default async function FilesPage() {
  const departments = await apiGet<DepartmentSummary[]>("/api/files/departments", "/base");
  return (
    <>
      <PageHeader
        title="Файлы"
        subtitle="Файлы отделов — на сайте и в подключённой папке Windows, это одно хранилище"
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: "Файлы" }]}
      />
      <PageBody>
        <BaseOverview departments={departments} />
      </PageBody>
    </>
  );
}
