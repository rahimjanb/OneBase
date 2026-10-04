import Link from "next/link";
import { ArrowRight, Lock, Route as RouteIcon } from "lucide-react";
import { DepartmentIcon } from "@/components/departments";
import { PushPrompt } from "@/components/shell/AppBridge";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { apiGet, apiTry } from "@/lib/server-api";
import type { DepartmentTasks, PageResult } from "@/lib/tasks";
import type { FieldTask } from "@/lib/field/types";

export const metadata = { title: "Задачи · OneBase" };

const plural = (n: number, one: string, few: string, many: string) => {
  const a = Math.abs(n) % 100;
  const b = a % 10;
  return a > 10 && a < 20 ? many : b > 1 && b < 5 ? few : b === 1 ? one : many;
};

/** «Задачи»: кнопки всех отделов. В отделе — его задачи; в «Продажах» ещё задачи и маршруты полевой команды (Sales Base). */
export default async function TasksPage() {
  const departments = await apiGet<DepartmentTasks[]>("/api/work/departments", "/tasks");
  // Открытые задачи полевой команды — для карточки «Продажи», если есть доступ к Sales Base.
  const fieldOpen = departments.some((d) => d.hasField) ? await apiTry<PageResult<FieldTask>>("/api/field/tasks?status=open&pageSize=10") : null;

  return (
    <>
      <PageHeader title="Задачи" subtitle="Выберите отдел: его задачи, а в «Продажах» — задачи и маршруты агентов и супервайзеров" />
      <PageBody>
        <div className="mb-4">
          <PushPrompt appName="OneBase" text="Включите уведомления — новые задачи и выполненные поручения будут приходить сразу на это устройство." />
        </div>
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {departments.map((d) => {
            const body = (
              <div
                className={`flex h-full flex-col rounded-xl border bg-surface p-5 transition-colors ${d.canOpen ? "border-line group-hover:border-accent/40" : "border-dashed border-line opacity-70"}`}
              >
                <div className="flex items-center gap-3">
                  <DepartmentIcon code={d.code} />
                  <span className="min-w-0 flex-1">
                    <span className="block font-semibold text-ink">{d.name}</span>
                    {d.isMine && <span className="block text-xs text-ink-3">ваш отдел</span>}
                  </span>
                  {d.canOpen ? (
                    <ArrowRight className="size-4 text-ink-3 transition-transform group-hover:translate-x-0.5 group-hover:text-accent-strong" />
                  ) : (
                    <Lock className="size-4 text-ink-3" aria-label="Нет доступа" />
                  )}
                </div>
                <div className="mt-4 space-y-1.5 border-t border-line pt-3 text-sm">
                  {!d.canOpen && <p className="text-ink-3">Задачи видят сотрудники отдела и руководство</p>}
                  {d.hasTasks && (
                    <p className="flex flex-wrap gap-x-3 gap-y-1 text-ink-2">
                      <span>
                        {d.openTasks} {plural(d.openTasks, "открытая задача", "открытые задачи", "открытых задач")}
                      </span>
                      {d.myTasks > 0 && <span className="text-accent-strong">мне: {d.myTasks}</span>}
                      {d.overdueTasks > 0 && <span className="text-bad">просрочено: {d.overdueTasks}</span>}
                    </p>
                  )}
                  {d.hasField && (
                    <p className="flex items-start gap-1.5 text-ink-2">
                      <RouteIcon className="mt-0.5 size-4 shrink-0 text-ink-3" />
                      <span>
                        Полевая команда: задачи и маршруты
                        {fieldOpen ? <span className="text-ink-3"> · открыто {fieldOpen.total}</span> : null}
                      </span>
                    </p>
                  )}
                </div>
              </div>
            );
            return d.canOpen ? (
              <Link key={d.code} href={`/tasks/${d.code}`} className="group block max-lg:active:opacity-75">
                {body}
              </Link>
            ) : (
              <div key={d.code} aria-disabled="true">
                {body}
              </div>
            );
          })}
        </div>
      </PageBody>
    </>
  );
}
