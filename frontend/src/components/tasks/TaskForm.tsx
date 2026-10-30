"use client";

import { useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { Loader2, Plus } from "lucide-react";
import { field, primary, button } from "@/components/ai/form";
import { Sheet } from "@/components/field/Sheet";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { todayInTashkent, workPriorities, workPriorityLabel, type DepartmentTasks, type WorkAssignee, type WorkProject, type WorkTask, type WorkTaskPriority } from "@/lib/tasks";
import { AssigneePicker } from "./AssigneePicker";
import { ErrorNote } from "./TaskBadges";

type Draft = {
  department: string;
  projectId: string;
  title: string;
  description: string;
  assigneeId: string;
  priority: WorkTaskPriority;
  startDate: string;
  dueDate: string;
};

const label = "mb-1.5 block text-sm font-medium text-ink";

/**
 * Новая задача и правка: отдел (из тех, где можно ставить задачи), проект отдела, исполнитель — сотрудник отдела или участник проекта
 * (список с сервера), приоритет, даты начала и срока. Сервер ещё раз проверяет всё это и права — форма только помогает не ошибиться.
 */
export function TaskForm({
  open,
  onClose,
  departments,
  projects,
  task,
  defaultDepartment,
  defaultProject,
  prefill,
  onCreated,
}: {
  open: boolean;
  onClose: () => void;
  departments: DepartmentTasks[];
  projects: WorkProject[];
  task?: WorkTask;
  defaultDepartment?: string;
  defaultProject?: string;
  /** Новая задача с подставленными полями (например, из рекомендации AI-консультанта). */
  prefill?: { title: string; description: string; department?: string | null; dueDate?: string | null };
  /** Задача создана: id новой задачи (вместо обновления страницы). */
  onCreated?: (id: string) => void;
}) {
  const t = useT();
  const router = useRouter();
  const creatable = useMemo(() => departments.filter((d) => d.hasTasks || projects.some((p) => p.departmentCode === d.code && !p.archived)), [departments, projects]);
  const prefillDepartment = prefill?.department && creatable.some((d) => d.code === prefill.department) ? prefill.department : undefined;
  const initial = (): Draft => ({
    department: task?.departmentCode ?? prefillDepartment ?? defaultDepartment ?? creatable.find((d) => d.isMine)?.code ?? creatable[0]?.code ?? "",
    projectId: task?.projectId ?? defaultProject ?? "",
    title: task?.title ?? prefill?.title ?? "",
    description: task?.description ?? prefill?.description ?? "",
    assigneeId: task?.assigneeId ?? "",
    priority: task?.priority ?? "Medium",
    startDate: task?.startDate ?? "",
    dueDate: task?.dueDate ?? prefill?.dueDate ?? "",
  });
  const [draft, setDraft] = useState<Draft>(initial);
  const [assignees, setAssignees] = useState<WorkAssignee[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Открыли заново — форма с начальными значениями (правка другой задачи или новая).
  useEffect(() => {
    if (open) {
      setDraft(initial());
      setError(null);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, task?.id]);

  // Исполнители — все действующие пользователи OneBase; сотрудники отдела и участники выбранного проекта — первыми.
  useEffect(() => {
    if (!open || !draft.department) return;
    let alive = true;
    bff<WorkAssignee[]>(`work/assignees?department=${encodeURIComponent(draft.department)}${draft.projectId ? `&projectId=${draft.projectId}` : ""}`)
      .then((list) => alive && setAssignees(list))
      .catch(() => alive && setAssignees([]));
    return () => {
      alive = false;
    };
  }, [open, draft.department, draft.projectId]);

  const departmentProjects = projects.filter((p) => p.departmentCode === draft.department && (!p.archived || p.id === draft.projectId));
  const set = <K extends keyof Draft>(key: K, value: Draft[K]) => setDraft((d) => ({ ...d, [key]: value }));
  const today = todayInTashkent();

  const save = async () => {
    if (!draft.title.trim()) {
      setError(t("Напишите название задачи."));
      return;
    }
    if (draft.startDate && draft.dueDate && draft.dueDate < draft.startDate) {
      setError(t("Срок не может быть раньше даты начала."));
      return;
    }
    setBusy(true);
    setError(null);
    const body = {
      title: draft.title.trim(),
      description: draft.description.trim() || null,
      assigneeId: draft.assigneeId || null,
      priority: draft.priority,
      startDate: draft.startDate || null,
      dueDate: draft.dueDate || null,
      projectId: draft.projectId || null,
    };
    try {
      let created: { id: string } | null = null;
      if (task) {
        await bff(`work/tasks/${task.id}`, { method: "PUT", body: JSON.stringify(body) });
      } else {
        created = await bff<{ id: string }>("work/tasks", { method: "POST", body: JSON.stringify({ ...body, department: draft.department }) });
      }
      onClose();
      if (created && onCreated) onCreated(created.id);
      else router.refresh();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Sheet
      open={open}
      onClose={onClose}
      title={task ? t("Изменить задачу") : t("Новая задача")}
      footer={
        <div className="flex justify-end gap-2">
          <button type="button" className={button} onClick={onClose} disabled={busy}>
            {t("Отмена")}
          </button>
          <button type="button" className={primary} onClick={save} disabled={busy || !draft.department}>
            {busy && <Loader2 className="size-4 animate-spin" />}
            {task ? t("Сохранить") : t("Поставить задачу")}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <ErrorNote text={error} />
        <label className="block">
          <span className={label}>{t("Название")}</span>
          <input className={field} value={draft.title} onChange={(e) => set("title", e.target.value)} maxLength={300} autoFocus placeholder={t("Что нужно сделать")} />
        </label>
        <label className="block">
          <span className={label}>{t("Описание")}</span>
          <textarea className={`${field} h-auto min-h-24 py-2`} value={draft.description} onChange={(e) => set("description", e.target.value)} maxLength={4000} placeholder={t("Подробности, результат, ссылки")} />
        </label>
        <div className="grid gap-4 sm:grid-cols-2">
          <label className="block">
            <span className={label}>{t("Отдел")}</span>
            <select
              className={field}
              value={draft.department}
              disabled={!!task}
              onChange={(e) => setDraft((d) => ({ ...d, department: e.target.value, projectId: "", assigneeId: "" }))}
            >
              {creatable.map((d) => (
                <option key={d.code} value={d.code}>
                  {d.name}
                </option>
              ))}
            </select>
          </label>
          <label className="block">
            <span className={label}>{t("Проект")}</span>
            <select className={field} value={draft.projectId} onChange={(e) => set("projectId", e.target.value)}>
              <option value="">{t("Без проекта")}</option>
              {departmentProjects.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
          </label>
          <div className="block sm:col-span-2">
            <span className={label}>{t("Исполнитель")}</span>
            <AssigneePicker
              value={draft.assigneeId}
              onChange={(id) => set("assigneeId", id)}
              assignees={assignees}
              current={task?.assigneeId ? { id: task.assigneeId, name: task.assigneeName } : null}
            />
            <span className="mt-1 block text-xs text-ink-3">
              {t("Любой пользователь OneBase. Исполнитель сразу получит уведомление в OneBase и push на все устройства, где он вошёл и включил уведомления.")}
            </span>
          </div>
          <label className="block">
            <span className={label}>{t("Приоритет")}</span>
            <select className={field} value={draft.priority} onChange={(e) => set("priority", e.target.value as WorkTaskPriority)}>
              {workPriorities.map((p) => (
                <option key={p} value={p}>
                  {t(workPriorityLabel[p])}
                </option>
              ))}
            </select>
          </label>
          <label className="block">
            <span className={label}>{t("Дата начала")}</span>
            <input type="date" className={field} value={draft.startDate} onChange={(e) => set("startDate", e.target.value)} />
          </label>
          <label className="block">
            <span className={label}>{t("Срок")}</span>
            <input type="date" className={field} value={draft.dueDate} min={task?.dueDate && task.dueDate < today ? undefined : today} onChange={(e) => set("dueDate", e.target.value)} />
          </label>
        </div>
      </div>
    </Sheet>
  );
}

/** Кнопка «Новая задача» с формой. */
export function NewTaskButton({
  departments,
  projects,
  defaultDepartment,
  defaultProject,
  compact = false,
}: {
  departments: DepartmentTasks[];
  projects: WorkProject[];
  defaultDepartment?: string;
  defaultProject?: string;
  compact?: boolean;
}) {
  const t = useT();
  const [open, setOpen] = useState(false);
  if (!departments.some((d) => d.hasTasks) && projects.length === 0) return null;
  return (
    <>
      <button type="button" className={primary} onClick={() => setOpen(true)}>
        <Plus className="size-4" />
        {compact ? <span className="sr-only sm:not-sr-only">{t("Новая задача")}</span> : t("Новая задача")}
      </button>
      <TaskForm open={open} onClose={() => setOpen(false)} departments={departments} projects={projects} defaultDepartment={defaultDepartment} defaultProject={defaultProject} />
    </>
  );
}
