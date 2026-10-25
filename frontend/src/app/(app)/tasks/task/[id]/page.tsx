import { notFound } from "next/navigation";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { TaskCard } from "@/components/tasks/TaskCard";
import { taskOptions } from "@/components/tasks/TaskListPage";
import { getT } from "@/i18n/server";
import { apiTry } from "@/lib/server-api";
import type { WorkTaskDetail } from "@/lib/tasks";

export async function generateMetadata({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const detail = await apiTry<WorkTaskDetail>(`/api/work/tasks/${id}`);
  const t = await getT();
  return { title: detail ? t("{name} · Задачи · OneBase", { name: detail.task.title }) : t("Задачи · OneBase") };
}

/** Карточка задачи (ссылки из уведомлений ведут сюда). Нет доступа — как будто задачи нет. */
export default async function TaskPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const returnTo = `/tasks/task/${id}`;
  const { departments, projects } = await taskOptions(returnTo);
  const detail = await apiTry<WorkTaskDetail>(`/api/work/tasks/${id}`);
  if (!detail) notFound();
  const t = await getT();

  return (
    <>
      <PageHeader
        title={t("Задача")}
        subtitle={`${detail.task.departmentName}${detail.task.projectName ? ` · ${detail.task.projectName}` : ""}`}
        back="/tasks/all"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Задачи"), href: "/tasks" },
          { label: detail.task.departmentName, href: `/tasks/${detail.task.departmentCode}` },
          { label: detail.task.title },
        ]}
      />
      <PageBody>
        <TaskCard detail={detail} departments={departments} projects={projects} />
      </PageBody>
    </>
  );
}
