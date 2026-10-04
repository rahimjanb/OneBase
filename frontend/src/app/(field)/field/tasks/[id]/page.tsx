import Link from "next/link";
import { TaskList } from "@/components/field/TaskBoard";
import { FieldPage, Panel } from "@/components/field/ui";
import { fieldGet } from "@/lib/field/api";
import { actorLabel } from "@/lib/field/labels";
import type { FieldTask } from "@/lib/field/types";

export const metadata = { title: "Задача" };

export default async function TaskPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const task = await fieldGet<FieldTask>(`tasks/${id}`, `/field/tasks/${id}`);
  return (
    <FieldPage title={task.title} back={{ href: "/field/tasks", label: "Задачи" }}>
      <TaskList tasks={[task]} />
      <Panel title="Подробности">
        <dl className="grid gap-3 text-sm sm:grid-cols-2">
          <div>
            <dt className="text-xs text-ink-3">Исполнитель</dt>
            <dd className="text-ink">{task.assignedTo}</dd>
          </div>
          <div>
            <dt className="text-xs text-ink-3">Поставил</dt>
            <dd className="text-ink">
              {task.createdBy ?? actorLabel[task.createdByType]} · {new Date(task.createdAt).toLocaleString("ru-RU")}
            </dd>
          </div>
          {task.marketName && (
            <div>
              <dt className="text-xs text-ink-3">Точка</dt>
              <dd>
                <Link href={`/field/customers/${task.marketId}`} className="text-accent-strong underline">
                  {task.marketName}
                </Link>
              </dd>
            </div>
          )}
          {task.completedAt && (
            <div>
              <dt className="text-xs text-ink-3">Выполнена</dt>
              <dd className="text-ink">{new Date(task.completedAt).toLocaleString("ru-RU")}</dd>
            </div>
          )}
          {task.recommendationId && (
            <div>
              <dt className="text-xs text-ink-3">Основание</dt>
              <dd>
                <Link href="/field/ai?status=decided" className="text-accent-strong underline">
                  рекомендация AI
                </Link>
              </dd>
            </div>
          )}
        </dl>
        {task.description && <p className="mt-4 whitespace-pre-wrap rounded-lg bg-muted px-3 py-2 text-sm text-ink-2">{task.description}</p>}
      </Panel>
    </FieldPage>
  );
}
