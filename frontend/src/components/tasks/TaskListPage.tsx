import { LayoutGrid } from "lucide-react";
import Link from "next/link";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { apiGet, apiTry } from "@/lib/server-api";
import { qs, taskFilterKeys, type DepartmentTasks, type PageResult, type WorkProject, type WorkTask } from "@/lib/tasks";
import { NewTaskButton } from "./TaskForm";
import { TaskFilters, TaskTable, type FilterSet } from "./TaskTable";

export type SearchParams = Record<string, string | string[] | undefined>;

export const one = (sp: SearchParams, key: string) => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

/** Данные, общие для страниц раздела: отделы (с доступом и руководством) и проекты. */
export async function taskOptions(returnTo: string) {
  const [departments, projects] = await Promise.all([
    apiGet<DepartmentTasks[]>("/api/work/departments", returnTo),
    apiTry<WorkProject[]>("/api/work/projects").then((p) => p ?? []),
  ]);
  return { departments, projects, heads: departments.some((d) => d.isHead) };
}

/**
 * Страница-список раздела «Задачи» (все, мои, отдела, архив): вкладки раздела, «Новая задача», фильтры и таблица. Данные — с сервера по
 * параметрам адреса; права решает API (страница показывает только то, что он вернул).
 */
export async function TaskListPage({
  scope,
  title,
  subtitle,
  sp,
  returnTo,
  show,
  empty,
  department,
  breadcrumbs,
}: {
  scope: "all" | "my" | "department" | "archive";
  title: string;
  subtitle: string;
  sp: SearchParams;
  returnTo: string;
  show: FilterSet;
  empty: string;
  department?: DepartmentTasks;
  breadcrumbs?: { label: string; href?: string }[];
}) {
  const t = await getT();
  const { departments, projects } = await taskOptions(returnTo);
  const filters = Object.fromEntries(taskFilterKeys.map((k) => [k, one(sp, k)]));
  const data = await apiGet<PageResult<WorkTask>>(
    `/api/work/tasks${qs({ ...filters, scope, department: department?.code ?? filters.department, mine: scope === "my" ? true : undefined, pageSize: 25 })}`,
    returnTo,
  );
  const boardHref = `/tasks/board${qs({ department: department?.code ?? filters.department, mine: scope === "my" ? true : undefined })}`;

  return (
    <>
      <PageHeader
        title={title}
        subtitle={subtitle}
        breadcrumbs={breadcrumbs ?? [{ label: "OneBase", href: "/" }, { label: t("Задачи"), href: "/tasks" }, { label: title }]}
        actions={
          <div className="flex items-center gap-2">
            {scope !== "archive" && (
              <Link href={boardHref} className="inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3 text-sm font-medium text-ink hover:bg-muted">
                <LayoutGrid className="size-4" /> {t("Канбан")}
              </Link>
            )}
            {scope !== "archive" && <NewTaskButton departments={departments} projects={projects} defaultDepartment={department?.code} />}
          </div>
        }
      />
      <PageBody>
        <TaskFilters departments={departments} show={show} />
        <TaskTable data={data} empty={empty} />
      </PageBody>
    </>
  );
}
