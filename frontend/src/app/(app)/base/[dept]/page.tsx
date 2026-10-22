import { notFound } from "next/navigation";
import { FilesBrowser } from "@/components/files/FilesBrowser";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { FolderView } from "@/lib/files";
import { apiGetOrNull } from "@/lib/server-api";

export default async function DepartmentFilesPage({ params }: { params: Promise<{ dept: string }> }) {
  const { dept } = await params;
  const view = await apiGetOrNull<FolderView>(`/api/files/departments/${encodeURIComponent(dept)}`, `/base/${dept}`);
  if (!view) notFound();
  const t = await getT();

  return (
    <>
      <PageHeader
        title={t(view.department.name)}
        subtitle={t("Файлы отдела «{name}»", { name: t(view.department.name) })}
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Файлы"), href: "/base" },
          { label: t(view.department.name) },
        ]}
      />
      <PageBody>
        <FilesBrowser initial={view} />
      </PageBody>
    </>
  );
}
