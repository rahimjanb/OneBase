import { TaskListPage, type SearchParams } from "@/components/tasks/TaskListPage";
import { getT } from "@/i18n/server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Все задачи · OneBase") };
}

/** «Все задачи»: таблица всех доступных задач (без архива) с фильтрами, сортировкой и страницами. */
export default async function AllTasksPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const t = await getT();
  return (
    <TaskListPage
      scope="all"
      title={t("Все задачи")}
      subtitle={t("Задачи, доступные вам: ваших отделов, проектов, поставленные вами и назначенные вам")}
      sp={await searchParams}
      returnTo="/tasks/all"
      show={{ department: true, assignee: true, status: "all" }}
      empty="Задач по этому фильтру нет."
    />
  );
}
