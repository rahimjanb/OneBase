import { notFound } from "next/navigation";
import { FilesBrowser } from "@/components/files/FilesBrowser";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import type { FolderView } from "@/lib/files";
import { apiGetOrNull } from "@/lib/server-api";

export default async function DepartmentFilesPage({ params }: { params: Promise<{ dept: string }> }) {
  const { dept } = await params;
  const view = await apiGetOrNull<FolderView>(`/api/files/departments/${encodeURIComponent(dept)}`, `/base/${dept}`);
  if (!view) notFound();

  return (
    <>
      <PageHeader
        title={view.department.name}
        subtitle={`Файлы отдела «${view.department.name}»`}
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: "Файлы", href: "/base" },
          { label: view.department.name },
        ]}
      />
      <PageBody>
        <FilesBrowser initial={view} />
      </PageBody>
    </>
  );
}
