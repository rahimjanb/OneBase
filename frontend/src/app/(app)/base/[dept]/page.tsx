import { notFound } from "next/navigation";
import { DepartmentWorkspace } from "@/components/files/DepartmentWorkspace";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { childFolders, findDepartment, recentFiles } from "@/lib/demo-data";

export default async function DepartmentBasePage({ params }: { params: Promise<{ dept: string }> }) {
  const { dept } = await params;
  const department = findDepartment(dept);
  if (!department) notFound();

  return (
    <>
      <PageHeader
        title={department.name}
        subtitle={`Файлы и документы отдела «${department.name}»`}
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: "Общая база", href: "/base" },
          { label: department.name },
        ]}
      />
      <PageBody>
        <DepartmentWorkspace
          department={department}
          folders={childFolders(department.code, null)}
          recent={recentFiles(department.code)}
        />
      </PageBody>
    </>
  );
}
