import { notFound } from "next/navigation";
import { FilesBrowser } from "@/components/files/FilesBrowser";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { FolderView } from "@/lib/files";
import { apiGetOrNull } from "@/lib/server-api";

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Общая папка («Общие файлы»): видна только тем, кому её выдали, — без доступа API отвечает 404, как несуществующей. */
export default async function SharedFolderPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!GUID.test(id)) notFound();
  const view = await apiGetOrNull<FolderView>(`/api/files/shared/${id}`, `/base/shared/${id}`);
  if (!view) notFound();
  const t = await getT();

  return (
    <>
      <PageHeader
        title={view.department.name}
        subtitle={t("Общая папка — для сотрудников и отделов, которым выдан доступ")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Файлы"), href: "/base" }, { label: t("Общие файлы") }, { label: view.department.name }]}
      />
      <PageBody>
        <FilesBrowser initial={view} />
      </PageBody>
    </>
  );
}
