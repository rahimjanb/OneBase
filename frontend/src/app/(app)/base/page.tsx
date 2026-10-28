import { BaseOverview } from "@/components/files/BaseOverview";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { DepartmentSummary, SharedSummary } from "@/lib/files";
import { apiGet } from "@/lib/server-api";

export default async function FilesPage() {
  const [departments, shared] = await Promise.all([
    apiGet<DepartmentSummary[]>("/api/files/departments", "/base"),
    apiGet<SharedSummary[]>("/api/files/shared", "/base"),
  ]);
  const t = await getT();
  return (
    <>
      <PageHeader
        title={t("Файлы")}
        subtitle={t("Файлы отделов — на сайте и в подключённой папке Windows, это одно хранилище")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Файлы") }]}
      />
      <PageBody>
        {/* Подключения отделов создаёт администратор — он же создаёт общие папки. */}
        <BaseOverview departments={departments} shared={shared} canCreateShared={departments.some((d) => d.connection.canManage)} />
      </PageBody>
    </>
  );
}
