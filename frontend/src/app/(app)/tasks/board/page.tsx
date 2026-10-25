import Link from "next/link";
import { Table2 } from "lucide-react";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { TaskBoard } from "@/components/tasks/TaskBoard";
import { NewTaskButton } from "@/components/tasks/TaskForm";
import { one, taskOptions, type SearchParams } from "@/components/tasks/TaskListPage";
import { TaskFilters } from "@/components/tasks/TaskTable";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import { qs, type PageResult, type WorkTask } from "@/lib/tasks";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Канбан задач · OneBase") };
}

/** Канбан-доска: те же задачи и фильтры, что у таблицы (без архива и отменённых). */
export default async function TaskBoardPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const t = await getT();
  const sp = await searchParams;
  const { departments, projects } = await taskOptions("/tasks/board");
  const mine = one(sp, "mine") === "true";
  const data = await apiGet<PageResult<WorkTask>>(
    `/api/work/tasks${qs({
      scope: "board",
      department: one(sp, "department"),
      projectId: one(sp, "projectId"),
      priority: one(sp, "priority"),
      assigneeId: one(sp, "assigneeId"),
      search: one(sp, "search"),
      status: one(sp, "status"),
      mine: mine || undefined,
      pageSize: 500,
    })}`,
    "/tasks/board",
  );

  return (
    <>
      <PageHeader
        title={t("Канбан")}
        subtitle={mine ? t("Мои задачи по статусам") : t("Задачи по статусам: перетащите карточку, чтобы сменить статус")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Задачи"), href: "/tasks" }, { label: t("Канбан") }]}
        actions={
          <div className="flex items-center gap-2">
            <Link
              href={`/tasks/all${qs({ department: one(sp, "department"), priority: one(sp, "priority"), search: one(sp, "search") })}`}
              className="inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3 text-sm font-medium text-ink hover:bg-muted"
            >
              <Table2 className="size-4" /> {t("Таблица")}
            </Link>
            <NewTaskButton departments={departments} projects={projects} defaultDepartment={one(sp, "department")} />
          </div>
        }
      />
      <PageBody>
        <TaskFilters departments={departments} show={{ department: true, assignee: true, status: "all" }} />
        {data.total > data.items.length && (
          <p className="mb-2 text-xs text-ink-3">{t("Показаны первые {n} из {total} задач — уточните фильтр.", { n: data.items.length, total: data.total })}</p>
        )}
        <TaskBoard items={data.items} />
      </PageBody>
    </>
  );
}
