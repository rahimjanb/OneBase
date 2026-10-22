import Link from "next/link";
import { TaskList } from "@/components/field/TaskBoard";
import { FieldPage, Panel } from "@/components/field/ui";
import { getT } from "@/i18n/server";
import { fieldGet } from "@/lib/field/api";
import { actorLabel, taskDescription, taskTitle } from "@/lib/field/labels";
import type { FieldTask } from "@/lib/field/types";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Задача") };
}

export default async function TaskPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const t = await getT();
  const task = await fieldGet<FieldTask>(`tasks/${id}`, `/field/tasks/${id}`);
  return (
    <FieldPage title={taskTitle(t, task)} back={{ href: "/field/tasks", label: t("Задачи") }}>
      <TaskList tasks={[task]} />
      <Panel title={t("Подробности")}>
        <dl className="grid gap-3 text-sm sm:grid-cols-2">
          <div>
            <dt className="text-xs text-ink-3">{t("Исполнитель")}</dt>
            <dd className="text-ink">{task.assignedTo}</dd>
          </div>
          <div>
            <dt className="text-xs text-ink-3">{t("Поставил")}</dt>
            <dd className="text-ink">
              {t(task.createdBy ?? actorLabel[task.createdByType])} · {new Date(task.createdAt).toLocaleString("ru-RU")}
            </dd>
          </div>
          {task.marketName && (
            <div>
              <dt className="text-xs text-ink-3">{t("Точка")}</dt>
              <dd>
                <Link href={`/field/customers/${task.marketId}`} className="text-accent-strong underline">
                  {task.marketName}
                </Link>
              </dd>
            </div>
          )}
          {task.completedAt && (
            <div>
              <dt className="text-xs text-ink-3">{t("Выполнена")}</dt>
              <dd className="text-ink">{new Date(task.completedAt).toLocaleString("ru-RU")}</dd>
            </div>
          )}
          {task.recommendationId && (
            <div>
              <dt className="text-xs text-ink-3">{t("Основание")}</dt>
              <dd>
                <Link href="/field/ai?status=decided" className="text-accent-strong underline">
                  {t("рекомендация AI")}
                </Link>
              </dd>
            </div>
          )}
        </dl>
        {task.description && <p className="mt-4 whitespace-pre-wrap rounded-lg bg-muted px-3 py-2 text-sm text-ink-2">{taskDescription(t, task)}</p>}
      </Panel>
    </FieldPage>
  );
}
