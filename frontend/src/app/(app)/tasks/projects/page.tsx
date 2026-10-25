import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { ProjectsView } from "@/components/tasks/ProjectsView";
import { taskOptions } from "@/components/tasks/TaskListPage";
import { getT } from "@/i18n/server";
import { apiSession, apiTry } from "@/lib/server-api";
import type { WorkDepartmentHeads, WorkProject } from "@/lib/tasks";
import type { Me } from "@/lib/users";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Проекты · Задачи · OneBase") };
}

/** Проекты (участники, PM) и руководители отделов. */
export default async function ProjectsPage() {
  const t = await getT();
  const [me, options, archived, heads] = await Promise.all([
    apiSession<Me>("/api/auth/me"),
    taskOptions("/tasks/projects"),
    apiTry<WorkProject[]>("/api/work/projects?archived=true"),
    apiTry<WorkDepartmentHeads[]>("/api/work/heads"),
  ]);
  return (
    <>
      <PageHeader
        title={t("Проекты")}
        subtitle={t("Проекты отделов, их участники и руководители; руководители отделов")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Задачи"), href: "/tasks" }, { label: t("Проекты") }]}
      />
      <PageBody>
        <ProjectsView
          projects={archived ?? options.projects}
          departments={options.departments}
          heads={heads ?? []}
          canSetHeads={me?.permissions.includes("users.manage") ?? false}
        />
      </PageBody>
    </>
  );
}
