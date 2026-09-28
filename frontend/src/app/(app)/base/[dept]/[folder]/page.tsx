import { notFound } from "next/navigation";
import { FolderBrowser } from "@/components/files/FolderBrowser";
import { PageBody, PageHeader, type Crumb } from "@/components/shell/PageHeader";
import { childFolders, findDepartment, folderFiles, folderPath } from "@/lib/demo-data";

export default async function FolderPage({ params }: { params: Promise<{ dept: string; folder: string }> }) {
  const { dept, folder: folderId } = await params;
  const department = findDepartment(dept);
  const chain = folderPath(folderId);
  const folder = chain.at(-1);
  if (!department || !folder || folder.department !== department.code) notFound();

  const deptHref = `/base/${department.code}`;
  const path: Crumb[] = [
    { label: "Общая база", href: "/base" },
    { label: department.name, href: deptHref },
    ...chain.map((f, i) => ({ label: f.name, href: i < chain.length - 1 ? `${deptHref}/${f.id}` : undefined })),
  ];
  const parentHref = folder.parentId ? `${deptHref}/${folder.parentId}` : deptHref;

  return (
    <>
      <PageHeader title={folder.name} breadcrumbs={[{ label: "OneBase", href: "/" }, ...path]} />
      <PageBody>
        <FolderBrowser
          department={department}
          folder={folder}
          path={path}
          parentHref={parentHref}
          subfolders={childFolders(department.code, folder.id)}
          initialFiles={folderFiles(folder.id)}
        />
      </PageBody>
    </>
  );
}
