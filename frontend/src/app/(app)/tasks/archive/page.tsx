import { TaskListPage, type SearchParams } from "@/components/tasks/TaskListPage";
import { getT } from "@/i18n/server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Архив задач · OneBase") };
}

/** «Архив»: выполненные, отменённые и отправленные в архив задачи. Ничего не удаляется автоматически. */
export default async function ArchiveTasksPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const t = await getT();
  return (
    <TaskListPage
      scope="archive"
      title={t("Архив и завершённые")}
      subtitle={t("Выполненные и отменённые задачи и архив — задачи не удаляются, их можно вернуть в работу")}
      sp={await searchParams}
      returnTo="/tasks/archive"
      show={{ department: true, assignee: true, status: "archive" }}
      empty="Завершённых задач по этому фильтру нет."
    />
  );
}
