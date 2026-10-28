import { notFound, redirect } from "next/navigation";
import { FilesBrowser } from "@/components/files/FilesBrowser";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { spaceHref, type FolderView } from "@/lib/files";
import { apiGetOrNull } from "@/lib/server-api";

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export default async function FolderPage({ params }: { params: Promise<{ dept: string; folder: string }> }) {
  const { dept, folder } = await params;
  if (!GUID.test(folder)) notFound();
  const view = await apiGetOrNull<FolderView>(`/api/files/folders/${folder}`, `/base/${dept}/${folder}`);
  if (!view) notFound();
  // Папка другого отдела по чужому адресу — на её настоящий адрес (доступ API уже проверил).
  if (view.department.shared || view.department.code !== dept) redirect(spaceHref(view.department, view.parentId ? view.id : undefined));
  const t = await getT();

  return (
    <>
      <PageHeader
        title={view.name}
        subtitle={t("Папка отдела «{name}»", { name: t(view.department.name) })}
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Файлы"), href: "/base" },
          { label: t(view.department.name), href: `/base/${view.department.code}` },
          { label: view.name },
        ]}
      />
      <PageBody>
        <FilesBrowser initial={view} />
      </PageBody>
    </>
  );
}
