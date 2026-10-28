import { notFound, redirect } from "next/navigation";
import { FilesBrowser } from "@/components/files/FilesBrowser";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { spaceHref, type FolderView } from "@/lib/files";
import { apiGetOrNull } from "@/lib/server-api";

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export default async function SharedSubfolderPage({ params }: { params: Promise<{ id: string; folder: string }> }) {
  const { id, folder } = await params;
  if (!GUID.test(id) || !GUID.test(folder)) notFound();
  const view = await apiGetOrNull<FolderView>(`/api/files/folders/${folder}`, `/base/shared/${id}/${folder}`);
  if (!view) notFound();
  // Папка другого места по чужому адресу — на её настоящий адрес (доступ API уже проверил).
  if (!view.department.shared || view.department.id !== id) redirect(spaceHref(view.department, view.parentId ? view.id : undefined));
  const t = await getT();

  return (
    <>
      <PageHeader
        title={view.name}
        subtitle={t("Папка в общей папке «{name}»", { name: view.department.name })}
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Файлы"), href: "/base" },
          { label: view.department.name, href: spaceHref(view.department) },
          { label: view.name },
        ]}
      />
      <PageBody>
        <FilesBrowser initial={view} />
      </PageBody>
    </>
  );
}
