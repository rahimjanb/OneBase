import Link from "next/link";
import { redirect } from "next/navigation";
import { ArrowRight } from "lucide-react";
import { DepartmentIcon } from "@/components/departments";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { taskOptions } from "@/components/tasks/TaskListPage";
import { getT } from "@/i18n/server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Задачи отдела · OneBase") };
}

/** «Задачи отдела»: свой отдел — сразу его задачи; руководителю нескольких отделов и руководству — выбор отдела. */
export default async function DepartmentPickerPage() {
  const t = await getT();
  const { departments, heads } = await taskOptions("/tasks/department");
  const open = departments.filter((d) => d.hasTasks);
  if (open.length === 1) redirect(`/tasks/${open[0].code}`);
  const mine = open.find((d) => d.isMine);
  if (mine && !heads) redirect(`/tasks/${mine.code}`);

  return (
    <>
      <PageHeader
        title={t("Задачи отдела")}
        subtitle={t("Выберите отдел")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Задачи"), href: "/tasks" }, { label: t("Задачи отдела") }]}
      />
      <PageBody>
        {open.length === 0 ? (
          <p className="rounded-xl border border-dashed border-line bg-surface px-4 py-10 text-center text-sm text-ink-3">
            {t("Вы не состоите в отделе и не руководите отделом — задачи отделов вам не видны. Ваши задачи — во вкладке «Мои задачи».")}
          </p>
        ) : (
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            {open.map((d) => (
              <Link key={d.code} href={`/tasks/${d.code}`} className="group flex items-center gap-3 rounded-xl border border-line bg-surface p-4 hover:border-accent/40">
                <DepartmentIcon code={d.code} />
                <span className="min-w-0 flex-1">
                  <span className="block font-semibold text-ink">{d.name}</span>
                  <span className="block text-xs text-ink-3">
                    {t("открытых {n}", { n: d.openTasks })}
                    {d.overdueTasks > 0 && ` · ${t("просрочено {n}", { n: d.overdueTasks })}`}
                    {d.isHead && ` · ${t("вы руководитель")}`}
                  </span>
                </span>
                <ArrowRight className="size-4 text-ink-3 group-hover:text-accent-strong" />
              </Link>
            ))}
          </div>
        )}
      </PageBody>
    </>
  );
}
