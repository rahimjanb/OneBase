import { notFound } from "next/navigation";
import { TaskDetail } from "@/components/tasks/TaskDetail";
import { taskOptions } from "@/components/tasks/TaskListPage";
import { getT } from "@/i18n/server";
import { apiTry } from "@/lib/server-api";
import type { WorkTaskDetail } from "@/lib/tasks";
import type { Me } from "@/lib/users";

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
  const [{ departments, projects }, detail, me] = await Promise.all([taskOptions(returnTo), apiTry<WorkTaskDetail>(`/api/work/tasks/${id}`), apiTry<Me>("/api/auth/me")]);
  if (!detail) notFound();
  return <TaskDetail detail={detail} departments={departments} projects={projects} me={me ? { id: me.id, name: me.name || me.login } : null} />;
}
