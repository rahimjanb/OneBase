import { notFound, redirect } from "next/navigation";
import { FilesBrowser } from "@/components/files/FilesBrowser";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import type { FolderView } from "@/lib/files";
import { apiGetOrNull } from "@/lib/server-api";

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export default async function FolderPage({ params }: { params: Promise<{ dept: string; folder: string }> }) {
  const { dept, folder } = await params;
  if (!GUID.test(folder)) notFound();
  const view = await apiGetOrNull<FolderView>(`/api/files/folders/${folder}`, `/base/${dept}/${folder}`);
  if (!view) notFound();
  // Папка другого отдела по чужому адресу — на её настоящий адрес (доступ API уже проверил).
  if (view.department.code !== dept) redirect(view.parentId ? `/base/${view.department.code}/${view.id}` : `/base/${view.department.code}`);

  return (
    <>
      <PageHeader
        title={view.name}
        subtitle={`Папка отдела «${view.department.name}»`}
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: "Файлы", href: "/base" },
          { label: view.department.name, href: `/base/${view.department.code}` },
          { label: view.name },
        ]}
      />
      <PageBody>
        <FilesBrowser initial={view} />
      </PageBody>
    </>
  );
}
