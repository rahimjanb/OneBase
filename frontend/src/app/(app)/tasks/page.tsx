import Link from "next/link";
import { redirect } from "next/navigation";
import { AlertTriangle, ArrowRight, Ban, CalendarClock, CheckCircle2, ClipboardCheck, ListTodo, Lock, Route as RouteIcon, Sparkles, UserRound } from "lucide-react";
import { DepartmentIcon } from "@/components/departments";
import { PushPrompt } from "@/components/shell/AppBridge";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { NewTaskButton } from "@/components/tasks/TaskForm";
import { taskOptions } from "@/components/tasks/TaskListPage";
import { PriorityBadge, StatusBadge } from "@/components/tasks/TaskBadges";
import { getT } from "@/i18n/server";
import { canOpen, homeFor } from "@/lib/navigation";
import { apiGet, apiSession, apiTry } from "@/lib/server-api";
import { qs, shortDate, todayInTashkent, type PageResult, type WorkOverview } from "@/lib/tasks";
import type { Me } from "@/lib/users";
import type { FieldTask } from "@/lib/field/types";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Задачи · OneBase") };
}

const plural = (n: number, one: string, few: string, many: string) => {
  const a = Math.abs(n) % 100;
  const b = a % 10;
  return a > 10 && a < 20 ? many : b > 1 && b < 5 ? few : b === 1 ? one : many;
};

/**
 * «Задачи → Обзор»: сводка по доступным задачам (открытые, в работе, на проверке, просрочки, блокировки, сроки недели, выполнено за месяц),
 * мои ближайшие сроки, отделы с открытыми и просроченными, предложения AI-диспетчера руководителю и кнопки отделов (в «Продажах» —
 * ещё задачи и маршруты полевой команды Sales Base).
 */
export default async function TasksPage() {
  const me = await apiSession<Me>("/api/auth/me");
  if (me && !canOpen("/tasks", me.permissions)) redirect(homeFor(me.permissions));
  const { departments, projects, heads } = await taskOptions("/tasks");
  const overview = await apiGet<WorkOverview>("/api/work/overview", "/tasks");
  // Открытые задачи полевой команды — для карточки «Продажи», если есть доступ к Sales Base.
  const fieldOpen = departments.some((d) => d.hasField) ? await apiTry<PageResult<FieldTask>>("/api/field/tasks?status=open&pageSize=10") : null;
  const t = await getT();
  const today = todayInTashkent();
  const weekEnd = new Date(Date.parse(today) + 7 * 86400000).toISOString().slice(0, 10);

  const tiles = [
    { label: "Открытые", value: overview.open, icon: ListTodo, href: "/tasks/all?status=open", tone: "text-accent-strong" },
    { label: "В работе", value: overview.inProgress, icon: ClipboardCheck, href: "/tasks/all?status=InProgress", tone: "text-warn" },
    { label: "На проверке", value: overview.review, icon: CheckCircle2, href: "/tasks/all?status=Review", tone: "text-violet-light" },
    { label: "Просрочены", value: overview.overdue, icon: CalendarClock, href: "/tasks/all?status=overdue", tone: "text-bad" },
    { label: "Заблокированы", value: overview.blocked, icon: Ban, href: "/tasks/all?status=blocked", tone: "text-bad" },
    { label: "Срок в ближайшие 7 дней", value: overview.dueThisWeek, icon: CalendarClock, href: `/tasks/all${qs({ status: "open", dateField: "due", from: today, to: weekEnd, sort: "due" })}`, tone: "text-ink" },
    { label: "Выполнено в этом месяце", value: overview.doneThisMonth, icon: CheckCircle2, href: "/tasks/archive?status=Done", tone: "text-ok" },
  ];

  return (
    <>
      <PageHeader
        title={t("Задачи")}
        subtitle={t("Задачи отделов и проектов: таблица, канбан, мои задачи, архив и предложения AI-диспетчера")}
        actions={<NewTaskButton departments={departments} projects={projects} />}
      />
      <PageBody>
        <div className="mb-4">
          <PushPrompt appName="OneBase" text={t("Включите уведомления — новые задачи, сроки и результаты проверки будут приходить сразу на это устройство.")} />
        </div>

        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4 xl:grid-cols-7">
          {tiles.map((tile) => (
            <Link key={tile.label} href={tile.href} className="rounded-xl border border-line bg-surface p-4 hover:border-accent/40">
              <tile.icon className={`size-4 ${tile.tone}`} />
              <div className="mt-2 text-2xl font-semibold tabular-nums text-ink">{tile.value}</div>
              <div className="mt-0.5 text-xs text-ink-3">{t(tile.label)}</div>
            </Link>
          ))}
        </div>

        {heads && overview.pendingSuggestions > 0 && (
          <Link href="/tasks/suggestions" className="mt-4 flex items-center gap-3 rounded-xl border border-violet/30 bg-violet/10 p-4 hover:border-violet/60">
            <Sparkles className="size-5 shrink-0 text-violet-light" />
            <span className="min-w-0 flex-1 text-sm text-ink">
              <span className="font-semibold">{t("AI-диспетчер: {n} предложений ждут вашего решения", { n: overview.pendingSuggestions })}</span>
              <span className="block text-ink-3">{t("Задача появится, только когда вы подтвердите предложение.")}</span>
            </span>
            <ArrowRight className="size-4 text-ink-3" />
          </Link>
        )}

        <div className="mt-4 grid gap-4 xl:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
          <section className="rounded-xl border border-line bg-surface p-4 sm:p-5">
            <div className="mb-3 flex items-center justify-between gap-2">
              <h2 className="flex items-center gap-2 text-sm font-semibold text-ink">
                <UserRound className="size-4" /> {t("Мои ближайшие сроки")}
              </h2>
              <span className="text-xs text-ink-3">
                {t("моих открытых {n}", { n: overview.myOpen })}
                {overview.myOverdue > 0 && <span className="text-bad"> · {t("просрочено {n}", { n: overview.myOverdue })}</span>}
                {overview.awaitingMyReview > 0 && <span className="text-violet-light"> · {t("ждут моей проверки {n}", { n: overview.awaitingMyReview })}</span>}
              </span>
            </div>
            {overview.myUpcoming.length === 0 ? (
              <p className="py-6 text-center text-sm text-ink-3">{t("Открытых задач со сроком у вас нет.")}</p>
            ) : (
              <ul className="divide-y divide-line">
                {overview.myUpcoming.map((task) => (
                  <li key={task.id}>
                    <Link href={`/tasks/task/${task.id}`} className="flex items-center gap-3 py-2.5 hover:text-accent-strong">
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-sm font-medium text-ink">{task.title}</span>
                        <span className="text-xs text-ink-3">{task.departmentName}</span>
                      </span>
                      <PriorityBadge priority={task.priority} />
                      <StatusBadge status={task.status} />
                      <span className={`w-20 text-right text-xs tabular-nums ${task.overdue ? "font-semibold text-bad" : "text-ink-2"}`}>{shortDate(task.dueDate)}</span>
                    </Link>
                  </li>
                ))}
              </ul>
            )}
            <Link href="/tasks/my" className="mt-2 inline-flex items-center gap-1 text-sm text-accent-strong hover:underline">
              {t("Все мои задачи")} <ArrowRight className="size-3.5" />
            </Link>
          </section>

          <section className="rounded-xl border border-line bg-surface p-4 sm:p-5">
            <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold text-ink">
              <AlertTriangle className="size-4" /> {t("Открытые задачи по отделам")}
            </h2>
            {overview.departments.length === 0 ? (
              <p className="py-6 text-center text-sm text-ink-3">{t("Открытых задач нет.")}</p>
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                    <th className="py-2 pr-3 font-semibold">{t("Отдел")}</th>
                    <th className="py-2 pr-3 text-right font-semibold">{t("Открытые")}</th>
                    <th className="py-2 pr-3 text-right font-semibold">{t("Просрочены")}</th>
                    <th className="py-2 pr-3 text-right font-semibold">{t("На проверке")}</th>
                    <th className="py-2 text-right font-semibold">{t("Блок")}</th>
                  </tr>
                </thead>
                <tbody>
                  {overview.departments.map((d) => (
                    <tr key={d.code} className="border-b border-line last:border-0">
                      <td className="py-2 pr-3">
                        <Link href={`/tasks/all${qs({ department: d.code })}`} className="text-ink hover:text-accent-strong">
                          {d.name}
                        </Link>
                      </td>
                      <td className="py-2 pr-3 text-right tabular-nums">{d.open}</td>
                      <td className={`py-2 pr-3 text-right tabular-nums ${d.overdue > 0 ? "font-semibold text-bad" : "text-ink-3"}`}>{d.overdue}</td>
                      <td className="py-2 pr-3 text-right tabular-nums text-ink-2">{d.review}</td>
                      <td className={`py-2 text-right tabular-nums ${d.blocked > 0 ? "text-bad" : "text-ink-3"}`}>{d.blocked}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>
        </div>

        <h2 className="mb-3 mt-6 text-sm font-semibold text-ink">{t("Отделы")}</h2>
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
                    {(d.isMine || d.isHead) && <span className="block text-xs text-ink-3">{d.isHead ? t("вы руководитель") : t("ваш отдел")}</span>}
                  </span>
                  {d.canOpen ? (
                    <ArrowRight className="size-4 text-ink-3 transition-transform group-hover:translate-x-0.5 group-hover:text-accent-strong" />
                  ) : (
                    <Lock className="size-4 text-ink-3" aria-label={t("Нет доступа")} />
                  )}
                </div>
                <div className="mt-4 space-y-1.5 border-t border-line pt-3 text-sm">
                  {!d.canOpen && <p className="text-ink-3">{t("Задачи видят сотрудники отдела, руководитель отдела и руководство")}</p>}
                  {d.hasTasks && (
                    <p className="flex flex-wrap gap-x-3 gap-y-1 text-ink-2">
                      <span>
                        {d.openTasks} {t(plural(d.openTasks, "открытая задача", "открытые задачи", "открытых задач"))}
                      </span>
                      {d.myTasks > 0 && <span className="text-accent-strong">{t("мне: {n}", { n: d.myTasks })}</span>}
                      {d.overdueTasks > 0 && <span className="text-bad">{t("просрочено: {n}", { n: d.overdueTasks })}</span>}
                    </p>
                  )}
                  {d.hasField && (
                    <p className="flex items-start gap-1.5 text-ink-2">
                      <RouteIcon className="mt-0.5 size-4 shrink-0 text-ink-3" />
                      <span>
                        {t("Полевая команда: задачи и маршруты")}
                        {fieldOpen ? <span className="text-ink-3"> · {t("открыто {n}", { n: fieldOpen.total })}</span> : null}
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
