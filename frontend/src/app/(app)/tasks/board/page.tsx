import { TaskBoard } from "@/components/tasks/TaskBoard";
import { TaskFilters } from "@/components/tasks/TaskFilterBar";
import { one, taskOptions, type SearchParams } from "@/components/tasks/TaskListPage";
import { BoardHeader } from "@/components/tasks/TaskScreenHeader";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import { qs, type PageResult, type WorkTask } from "@/lib/tasks";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Канбан задач · OneBase") };
}

/** Фильтры, общие у канбана и таблицы: переключение вида их сохраняет. */
const shared = ["search", "priority", "department", "assigneeId", "from", "to", "dateField", "mine"] as const;

/**
 * Канбан (ТЗ tasks-kanban-detail): те же задачи и фильтры, что у таблицы (без архива и отменённых). Компьютер — на всю высоту окна,
 * колонки прокручиваются внутри; телефон — своя шапка раздела и группы по статусам.
 */
export default async function TaskBoardPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const sp = await searchParams;
  const { departments, projects } = await taskOptions("/tasks/board");
  const filters = Object.fromEntries(shared.map((k) => [k, one(sp, k)]));
  const data = await apiGet<PageResult<WorkTask>>(
    `/api/work/tasks${qs({
      scope: "board",
      department: filters.department,
      projectId: one(sp, "projectId"),
      priority: filters.priority,
      assigneeId: filters.assigneeId,
      search: filters.search,
      from: filters.from,
      to: filters.to,
      dateField: filters.dateField,
      mine: filters.mine === "true" || undefined,
      pageSize: 500,
    })}`,
    "/tasks/board",
  );
  const filtered = Object.values(filters).some(Boolean);

  return (
    <div className="tasks-ui flex min-h-full flex-col gap-4 text-ink max-lg:bg-ai-bg lg:h-full lg:px-8 lg:py-6">
      <BoardHeader
        total={data.total}
        tableHref={`/tasks/all${qs(filters)}`}
        boardHref={`/tasks/board${qs(filters)}`}
        departments={departments}
        projects={projects}
        defaultDepartment={filters.department?.split(",")[0]}
      />
      <div className="px-4 lg:px-0">
        <TaskFilters departments={departments} show={{ department: true, assignee: true, mine: true }} />
      </div>
      <TaskBoard items={data.items} total={data.total} departments={departments} projects={projects} filtered={filtered} />
    </div>
  );
}
