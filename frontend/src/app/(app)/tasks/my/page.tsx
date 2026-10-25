import { TaskListPage, type SearchParams } from "@/components/tasks/TaskListPage";
import { getT } from "@/i18n/server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Мои задачи · OneBase") };
}

/** «Мои задачи»: назначенные мне (без архива). */
export default async function MyTasksPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const t = await getT();
  return (
    <TaskListPage
      scope="my"
      title={t("Мои задачи")}
      subtitle={t("Задачи, где вы исполнитель")}
      sp={await searchParams}
      returnTo="/tasks/my"
      show={{ department: true, status: "all" }}
      empty="Назначенных вам задач по этому фильтру нет."
    />
  );
}
