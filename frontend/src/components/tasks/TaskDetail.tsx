"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useRef, useState } from "react";
import {
  Archive,
  ArchiveRestore,
  ArrowLeft,
  Ban,
  Building2,
  Check,
  ChevronDown,
  ChevronLeft,
  ChevronUp,
  CircleCheckBig,
  ClipboardCheck,
  Copy,
  Ellipsis,
  Link2,
  Loader2,
  LockOpen,
  OctagonX,
  Pencil,
  Play,
  Send,
  Undo2,
  X,
  type LucideIcon,
} from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { stageSteps, stampParts, neighbors as neighborsOf } from "@/lib/task-board";
import { markOpenedFromList, openedFromList, readTaskList, type TaskListContext } from "@/lib/task-list";
import { statusActionLabel, todayInTashkent, workStatusLabel, type DepartmentTasks, type WorkAssignee, type WorkProject, type WorkTaskDetail, type WorkTaskStatus } from "@/lib/tasks";
import {
  ActivityCard,
  AttachmentsCard,
  ChecklistCard,
  Composer,
  DescriptionCard,
  DetailsCard,
  inputOf,
  ProgressCard,
  type TaskInput,
} from "./TaskDetailParts";
import { PriorityPill, StatusPill, statusKey, statusLook } from "./TaskKit";
import { TaskForm } from "./TaskForm";

type Toast = { text: string; tone: "error" | "ok" };
type Asking = { to: WorkTaskStatus };
type MenuItem = { key: string; label: string; icon: LucideIcon; danger?: boolean; run: () => void } | "divider";

const open = (status: WorkTaskStatus) => status === "New" || status === "InProgress" || status === "Review";

/**
 * Карточка задачи (ТЗ tasks-kanban-detail). Сверху — «Назад» в список с фильтрами, «1 из 5» и соседние задачи (↑↓, J/K); название —
 * заголовок страницы (правится по месту); полоса этапов; плашка главного действия для этого пользователя (проверить, отправить на
 * проверку, взять в работу, снять блокировку); редкие и опасные действия — в «Ещё». Ниже — описание, чек-лист и активность; справа —
 * детали (правка по месту), выполнение и вложения. Все действия и права — те же, что на сервере (nextStatuses, canManage, canWork):
 * недоступные кнопки скрыты. Телефон — своя шапка, кнопки проверки во всю ширину, поле комментария над нижней панелью.
 */
export function TaskDetail({
  detail,
  departments,
  projects,
  me,
}: {
  detail: WorkTaskDetail;
  departments: DepartmentTasks[];
  projects: WorkProject[];
  me: { id: string; name: string } | null;
}) {
  const t = useT();
  const router = useRouter();
  const task = detail.task;
  const [busy, setBusy] = useState<string | null>(null);
  const [toast, setToast] = useState<Toast | null>(null);
  const [fieldError, setFieldError] = useState<{ field: string; text: string } | null>(null);
  const [view, setView] = useState(task);
  const [editing, setEditing] = useState(false);
  const [asking, setAsking] = useState<Asking | null>(null);
  const [note, setNote] = useState("");
  const [blocking, setBlocking] = useState(false);
  const [reason, setReason] = useState("");
  const [menu, setMenu] = useState(false);
  const [sheetMenu, setSheetMenu] = useState(false);
  const [titleEdit, setTitleEdit] = useState<string | null>(null);
  const [list, setList] = useState<TaskListContext | null>(null);
  const [people, setPeople] = useState<WorkAssignee[]>([]);
  const menuBox = useRef<HTMLDivElement>(null);

  // Свежие данные с сервера заменяют оптимистичные значения.
  useEffect(() => setView(detail.task), [detail]);
  useEffect(() => setList(readTaskList()), [task.id]);
  useEffect(() => {
    if (!toast) return;
    const timer = setTimeout(() => setToast(null), 6000);
    return () => clearTimeout(timer);
  }, [toast]);

  // Кого можно упомянуть через @ — те, кто видит задачу.
  useEffect(() => {
    if (!detail.canComment) return;
    let alive = true;
    bff<WorkAssignee[]>(`work/tasks/${task.id}/people`)
      .then((p) => alive && setPeople(p))
      .catch(() => undefined);
    return () => {
      alive = false;
    };
  }, [task.id, detail.canComment]);

  const archived = !!view.archivedAt;
  const canEdit = view.canManage && !archived;
  const canWork = view.canWork && !archived;
  const key = statusKey(view);
  const nav = list ? neighborsOf(list.ids, task.id) : null;

  const run = useCallback(
    async (name: string, fn: () => Promise<unknown>) => {
      setBusy(name);
      try {
        await fn();
        router.refresh();
        return true;
      } catch (e) {
        setToast({ tone: "error", text: (e as Error).message });
        return false;
      } finally {
        setBusy(null);
      }
    },
    [router],
  );
  const post = (path: string, body?: unknown) => bff(`work/tasks/${task.id}/${path}`, { method: "POST", body: body === undefined ? undefined : JSON.stringify(body) });

  // Правка по месту: значение меняется сразу; ошибка возвращает прежнее, пишется у поля и во всплывающем сообщении.
  const saveFields = async (patch: Partial<TaskInput>, field: string) => {
    const before = view;
    setFieldError(null);
    setView((v) => ({ ...v, ...patch }));
    try {
      await bff(`work/tasks/${task.id}`, { method: "PUT", body: JSON.stringify({ ...inputOf(before), ...patch }) });
      router.refresh();
      return true;
    } catch (e) {
      setView(before);
      setFieldError({ field, text: (e as Error).message });
      setToast({ tone: "error", text: (e as Error).message });
      return false;
    }
  };

  const changeStatus = (to: WorkTaskStatus, text?: string) => {
    if ((to === "Cancelled" || (view.status === "Review" && to === "InProgress")) && text === undefined) {
      setNote("");
      setAsking({ to });
      return;
    }
    void run(`status-${to}`, () => post("status", { status: to, comment: text || null })).then(
      (ok) => ok && to === "Done" && view.status === "Review" && setToast({ tone: "ok", text: t("Результат принят") }),
    );
  };

  const setProgress = (value: number) => {
    const before = view;
    setView((v) => ({ ...v, progress: value }));
    void run("progress", () => post("progress", { progress: value })).then((ok) => !ok && setView(before));
  };

  const upload = async (files: FileList) => {
    for (const file of Array.from(files)) {
      if (file.size > 20 * 1024 * 1024) {
        setToast({ tone: "error", text: t("«{name}» больше {n} МБ.", { name: file.name, n: 20 }) });
        continue;
      }
      const form = new FormData();
      form.append("file", file);
      const ok = await run("upload", async () => {
        // multipart — без Content-Type: браузер сам ставит границу.
        const response = await fetch(`/bff/api/work/tasks/${task.id}/attachments`, { method: "POST", body: form });
        if (!response.ok) {
          const json = await response.json().catch(() => ({}));
          throw new Error(json.error ? t(json.error) : t("Ошибка {status}", { status: response.status }));
        }
      });
      if (!ok) break;
    }
  };

  const sendComment = (text: string, mentions: string[]) => run("comment", () => post("comments", { text, mentions }));

  const go = useCallback(
    (id: string | null) => {
      if (!id) return;
      markOpenedFromList(id);
      router.replace(`/tasks/task/${id}`, { scroll: false });
    },
    [router],
  );

  const back = () => {
    if (list && openedFromList() === task.id && window.history.length > 1) router.back();
    else router.push(list?.href ?? "/tasks/all");
  };

  // J/K и ↑↓ (вне полей ввода) — соседние задачи списка.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const target = e.target as HTMLElement | null;
      if (e.metaKey || e.ctrlKey || e.altKey || target?.closest("input, textarea, select, [contenteditable=true], [role=dialog]")) return;
      if (e.key === "j" || e.key === "J" || e.key === "о" || e.key === "О") go(nav?.next ?? null);
      if (e.key === "k" || e.key === "K" || e.key === "л" || e.key === "Л") go(nav?.prev ?? null);
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [nav?.next, nav?.prev, go]);

  useEffect(() => {
    if (!menu) return;
    const onDown = (e: PointerEvent) => !menuBox.current?.contains(e.target as Node) && setMenu(false);
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setMenu(false);
    document.addEventListener("pointerdown", onDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("pointerdown", onDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [menu]);

  const duplicate = () =>
    run("duplicate", async () => {
      const today = todayInTashkent();
      const created = await bff<{ id: string }>("work/tasks", {
        method: "POST",
        body: JSON.stringify({
          department: view.departmentCode,
          title: `${view.title} (${t("копия")})`.slice(0, 300),
          description: view.description,
          assigneeId: view.assigneeId,
          priority: view.priority,
          startDate: view.startDate,
          dueDate: view.dueDate && view.dueDate >= today ? view.dueDate : null,
          projectId: view.projectId,
        }),
      });
      for (const item of detail.checklist) {
        await bff(`work/tasks/${created.id}/checklist`, { method: "POST", body: JSON.stringify({ title: item.title }) });
      }
      router.push(`/tasks/task/${created.id}`);
    });

  const copyLink = async () => {
    try {
      await navigator.clipboard.writeText(`${window.location.origin}/tasks/task/${task.id}`);
      setToast({ tone: "ok", text: t("Ссылка скопирована") });
    } catch {
      setToast({ tone: "error", text: t("Не удалось скопировать ссылку") });
    }
  };

  // «Ещё»: редкие и опасные действия — только доступные этому пользователю.
  const canCreateHere = departments.some((d) => d.code === view.departmentCode && d.hasTasks);
  const menuItems: MenuItem[] = [
    ...(canWork && open(view.status)
      ? [
          view.isBlocked
            ? { key: "unblock", label: t("Разблокировать"), icon: LockOpen, run: () => void run("unblock", () => post("block", { blocked: false })) }
            : { key: "block", label: t("Заблокировать"), icon: OctagonX, run: () => (setReason(""), setBlocking(true)) },
        ]
      : []),
    ...(view.nextStatuses.includes("Done") && view.status !== "Review" ? [{ key: "done", label: t("Завершить"), icon: CircleCheckBig, run: () => changeStatus("Done") }] : []),
    ...((view.status === "Done" || view.status === "Cancelled") && view.nextStatuses.includes("InProgress")
      ? [{ key: "reopen", label: t("Вернуть в работу"), icon: Undo2, run: () => changeStatus("InProgress") }]
      : []),
    ...(canCreateHere ? [{ key: "duplicate", label: t("Дублировать"), icon: Copy, run: () => void duplicate() }] : []),
    { key: "link", label: t("Копировать ссылку"), icon: Link2, run: () => void copyLink() },
    ...(detail.canArchive ? [{ key: "archive", label: t("В архив"), icon: Archive, run: () => void run("archive", () => post("archive")) }] : []),
    ...(detail.canRestore ? [{ key: "restore", label: t("Вернуть из архива"), icon: ArchiveRestore, run: () => void run("restore", () => post("restore")) }] : []),
    ...(view.nextStatuses.includes("Cancelled") ? ["divider" as const, { key: "cancel", label: t("Отменить задачу"), icon: Ban, danger: true, run: () => changeStatus("Cancelled") }] : []),
  ];

  const names = [...new Set([...people.map((p) => p.name), ...detail.comments.map((c) => c.authorName), view.assigneeName, view.createdByName, me?.name].filter(Boolean) as string[])];
  const returning = asking?.to === "InProgress";

  const saveTitle = async () => {
    const title = titleEdit?.trim();
    if (!title || title === view.title) {
      setTitleEdit(null);
      return;
    }
    if (await saveFields({ title }, "title")) setTitleEdit(null);
  };

  return (
    <div className="tasks-ui min-h-full text-ink max-lg:bg-ai-bg">
      {/* Телефон: своя шапка вместо глобальной. */}
      <header className="sticky top-0 z-20 flex h-[calc(env(safe-area-inset-top)+60px)] items-center gap-3 border-b border-line bg-ai-bg/90 px-3 pt-[env(safe-area-inset-top)] backdrop-blur-xl lg:hidden">
        <button type="button" onClick={back} aria-label={t("Назад")} className="grid size-10 shrink-0 place-items-center rounded-full border border-line bg-surface text-ink-2">
          <ChevronLeft className="size-5" aria-hidden />
        </button>
        <div className="min-w-0 flex-1 text-center">
          <p className="text-[15px] font-bold leading-tight">{t("Задача")}</p>
          <p className="truncate text-[11px] text-ink-3">{view.departmentName}</p>
        </div>
        <button type="button" onClick={() => setSheetMenu(true)} aria-label={t("Действия")} className="grid size-10 shrink-0 place-items-center rounded-full border border-line bg-surface text-ink-2">
          <Ellipsis className="size-5" aria-hidden />
        </button>
      </header>

      <div className="mx-auto flex max-w-[1800px] flex-col gap-4 px-4 pb-6 pt-4 lg:gap-5 lg:px-8 lg:pb-10 lg:pt-6">
        {/* Компьютер: «Назад», крошки, место в списке. */}
        <div className="hidden items-center gap-3 lg:flex">
          <button type="button" onClick={back} className="inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3 text-[13px] font-semibold text-ink hover:bg-surface-2">
            <ArrowLeft className="size-4" aria-hidden />
            {t("Назад")}
          </button>
          <nav aria-label={t("Навигация")} className="flex min-w-0 items-center gap-1.5 text-[13px] text-subtle">
            <Link href="/" className="hover:text-ink">
              OneBase
            </Link>
            <span aria-hidden>/</span>
            <Link href="/tasks" className="hover:text-ink">
              {t("Задачи")}
            </Link>
            <span aria-hidden>/</span>
            <Link href={`/tasks/${view.departmentCode}`} className="truncate hover:text-ink">
              {view.departmentName}
            </Link>
          </nav>
          {nav && (
            <div className="ml-auto flex items-center gap-1.5">
              <span className="mr-1 text-[13px] tabular-nums text-subtle">{t("{n} из {total}", { n: nav.index + 1, total: nav.total })}</span>
              <button type="button" onClick={() => go(nav.prev)} disabled={!nav.prev} aria-label={t("Предыдущая задача (K)")} title={t("Предыдущая задача (K)")} className="grid size-9 place-items-center rounded-lg border border-line bg-surface text-ink-2 hover:text-ink disabled:opacity-40">
                <ChevronUp className="size-4" aria-hidden />
              </button>
              <button type="button" onClick={() => go(nav.next)} disabled={!nav.next} aria-label={t("Следующая задача (J)")} title={t("Следующая задача (J)")} className="grid size-9 place-items-center rounded-lg border border-line bg-surface text-ink-2 hover:text-ink disabled:opacity-40">
                <ChevronDown className="size-4" aria-hidden />
              </button>
            </div>
          )}
        </div>

        {/* Шапка задачи */}
        <div className="flex items-start justify-between gap-6">
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <StatusPill status={key} className="lg:px-2.5 lg:py-1 lg:text-[13px]" />
              <PriorityPill priority={view.priority} className="lg:px-2.5 lg:py-1 lg:text-[13px]" />
              <span className="hidden items-center gap-1.5 rounded-md bg-surface-2 px-2.5 py-1 text-[13px] font-medium text-ink-2 lg:inline-flex">
                <Building2 className="size-3.5" aria-hidden />
                {view.departmentName}
              </span>
              {archived && <span className="rounded-md bg-surface-2 px-2 py-0.5 text-xs font-semibold text-ink-3">{t("в архиве")}</span>}
            </div>
            {titleEdit !== null ? (
              <form
                className="mt-3 flex items-center gap-2"
                onSubmit={(e) => {
                  e.preventDefault();
                  void saveTitle();
                }}
              >
                <input
                  value={titleEdit}
                  onChange={(e) => setTitleEdit(e.target.value)}
                  onKeyDown={(e) => e.key === "Escape" && setTitleEdit(null)}
                  maxLength={300}
                  autoFocus
                  aria-label={t("Название задачи")}
                  className="h-12 min-w-0 flex-1 rounded-xl border border-accent bg-surface px-3 text-xl font-bold text-ink focus:outline-none lg:text-2xl"
                />
                <button type="submit" aria-label={t("Сохранить")} className="grid size-11 place-items-center rounded-xl bg-accent text-white">
                  <Check className="size-5" aria-hidden />
                </button>
                <button type="button" onClick={() => setTitleEdit(null)} aria-label={t("Отмена")} className="grid size-11 place-items-center rounded-xl border border-line text-ink-3">
                  <X className="size-5" aria-hidden />
                </button>
              </form>
            ) : (
              <div className="group mt-2.5 flex items-start gap-2">
                <h1 className="min-w-0 break-words text-2xl font-bold leading-tight tracking-tight lg:text-[30px]">{view.title}</h1>
                {canEdit && (
                  <button type="button" onClick={() => setTitleEdit(view.title)} aria-label={t("Изменить название")} className="mt-1 grid size-8 shrink-0 place-items-center rounded-lg text-subtle hover:bg-surface-2 hover:text-ink lg:mt-1.5">
                    <Pencil className="size-4" aria-hidden />
                  </button>
                )}
              </div>
            )}
            {fieldError?.field === "title" && <p className="mt-1 text-xs text-bad">{fieldError.text}</p>}
          </div>
          <div className="hidden shrink-0 items-center gap-2 pt-1 lg:flex">
            {canEdit && (
              <button type="button" onClick={() => setEditing(true)} className="inline-flex h-10 items-center gap-2 rounded-xl border border-line-strong bg-surface px-4 text-sm font-semibold text-ink hover:bg-surface-2">
                <Pencil className="size-4" aria-hidden />
                {t("Изменить")}
              </button>
            )}
            <div ref={menuBox} className="relative">
              <button type="button" onClick={() => setMenu((m) => !m)} aria-expanded={menu} aria-haspopup="menu" className="inline-flex h-10 items-center gap-2 rounded-xl border border-line-strong bg-surface px-4 text-sm font-semibold text-ink hover:bg-surface-2">
                {t("Ещё")}
                <ChevronDown className="size-4" aria-hidden />
              </button>
              {menu && (
                <div role="menu" className="absolute right-0 top-full z-30 mt-1.5 w-60 rounded-xl border border-line-strong bg-surface-solid p-1.5 shadow-[0_16px_40px_rgb(0_0_0/0.35)]">
                  {menuItems.map((item, i) =>
                    item === "divider" ? (
                      <div key={`d${i}`} role="separator" className="my-1 h-px bg-line" />
                    ) : (
                      <button
                        key={item.key}
                        type="button"
                        role="menuitem"
                        onClick={() => {
                          setMenu(false);
                          item.run();
                        }}
                        className={`flex h-10 w-full items-center gap-2.5 rounded-lg px-2.5 text-left text-sm font-medium hover:bg-surface-2 ${item.danger ? "text-bad" : "text-ink"}`}
                      >
                        <item.icon className="size-4" aria-hidden />
                        {item.label}
                      </button>
                    ),
                  )}
                </div>
              )}
            </div>
          </div>
        </div>

        <StageBar detail={detail} status={view.status} blocked={view.isBlocked && open(view.status)} reason={view.blockedReason} canUnblock={canWork} onUnblock={() => void run("unblock", () => post("block", { blocked: false }))} onReopen={view.nextStatuses.includes("InProgress") ? () => changeStatus("InProgress") : null} busy={busy} />

        <ActionPlate detail={detail} view={view} me={me} busy={busy} archived={archived} onStatus={changeStatus} onRestore={() => void run("restore", () => post("restore"))} />

        {/* Тело: на компьютере две колонки; на телефоне — одна, в порядке макета (детали и выполнение выше описания). */}
        <div className="flex flex-col gap-4 lg:grid lg:grid-cols-[minmax(0,1fr)_360px] lg:items-start lg:gap-5">
          <div className="contents lg:flex lg:flex-col lg:gap-5">
            <DescriptionCard task={view} canEdit={canEdit} onSave={(description) => saveFields({ description }, "description")} className="order-3 lg:order-none" />
            <ChecklistCard task={view} items={detail.checklist} canWork={canWork} run={run} className="order-4 lg:order-none" />
            <ActivityCard
              detail={detail}
              names={names}
              className="order-6 lg:order-none"
              composer={
                detail.canComment && (
                  <Composer variant="desktop" meName={me?.name ?? null} people={people} onSend={sendComment} onAttach={(f) => void upload(f)} canAttach={detail.canAttach && !archived} />
                )
              }
            />
          </div>
          <div className="contents lg:flex lg:flex-col lg:gap-5">
            <DetailsCard task={view} projects={projects} canEdit={canEdit} onSave={saveFields} error={fieldError} className="order-1 lg:order-none" />
            <ProgressCard task={view} canSet={canWork && open(view.status)} onSet={setProgress} className="order-2 lg:order-none" />
            <AttachmentsCard
              detail={detail}
              uploading={busy === "upload"}
              onUpload={(f) => void upload(f)}
              onDelete={(id, name) => {
                if (window.confirm(t("Удалить вложение «{name}»?", { name }))) void run(`file-${id}`, () => bff(`work/attachments/${id}`, { method: "DELETE" }));
              }}
              className="order-5 lg:order-none"
            />
          </div>
        </div>
      </div>

      {/* Телефон: поле комментария закреплено внизу, над нижней панелью (и над клавиатурой). */}
      {detail.canComment && (
        <div className="sticky bottom-0 z-20 bg-gradient-to-t from-ai-bg via-ai-bg/95 to-transparent px-3 pb-[calc(env(safe-area-inset-bottom)+10px)] pt-5 lg:hidden">
          <Composer variant="mobile" meName={me?.name ?? null} people={people} onSend={sendComment} onAttach={(f) => void upload(f)} canAttach={detail.canAttach && !archived} />
        </div>
      )}

      {toast && (
        <div
          role={toast.tone === "error" ? "alert" : "status"}
          className="fixed inset-x-4 bottom-[calc(env(safe-area-inset-bottom)+150px)] z-50 mx-auto flex max-w-[460px] items-start gap-3 rounded-2xl border border-line-strong bg-surface-solid px-4 py-3 text-sm text-ink shadow-xl lg:bottom-6 lg:left-auto lg:right-6 lg:mx-0"
        >
          {toast.tone === "error" ? <Ban className="mt-0.5 size-4 shrink-0 text-bad" aria-hidden /> : <CircleCheckBig className="mt-0.5 size-4 shrink-0 text-ok" aria-hidden />}
          <span className="flex-1">{toast.text}</span>
          <button type="button" onClick={() => setToast(null)} aria-label={t("Закрыть")} className="text-ink-3 hover:text-ink">
            <X className="size-4" aria-hidden />
          </button>
        </div>
      )}

      {editing && <TaskForm open onClose={() => setEditing(false)} departments={departments} projects={projects} task={view} />}

      <Sheet open={sheetMenu} onClose={() => setSheetMenu(false)} title={t("Действия")} subtitle={view.title}>
        <div className="flex flex-col gap-1">
          {canEdit && (
            <SheetAction icon={Pencil} label={t("Изменить")} onClick={() => (setSheetMenu(false), setEditing(true))} />
          )}
          {menuItems.map((item, i) =>
            item === "divider" ? (
              <div key={`d${i}`} className="my-1 h-px bg-line" />
            ) : (
              <SheetAction key={item.key} icon={item.icon} label={item.label} danger={item.danger} onClick={() => (setSheetMenu(false), item.run())} />
            ),
          )}
        </div>
      </Sheet>

      <Sheet
        open={asking !== null}
        onClose={() => setAsking(null)}
        title={asking ? (asking.to === "Cancelled" ? t("Отменить задачу?") : t(statusActionLabel(view.status, asking.to))) : ""}
        subtitle={view.title}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" className="h-10 rounded-xl border border-line px-4 text-sm font-semibold text-ink-2" onClick={() => setAsking(null)}>
              {asking?.to === "Cancelled" ? t("Не отменять") : t("Отмена")}
            </button>
            <button
              type="button"
              className={`h-10 rounded-xl px-4 text-sm font-semibold text-white disabled:opacity-50 ${asking?.to === "Cancelled" ? "bg-bad" : "bg-accent"}`}
              disabled={returning && !note.trim()}
              onClick={() => {
                const to = asking?.to;
                setAsking(null);
                if (to) changeStatus(to, note.trim());
              }}
            >
              {asking?.to === "Cancelled" ? t("Отменить задачу") : t("Вернуть на доработку")}
            </button>
          </div>
        }
      >
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{returning ? t("Что доработать") : t("Причина отмены (необязательно)")}</span>
          <textarea
            className="min-h-24 w-full rounded-xl border border-line bg-surface px-3 py-2 text-sm text-ink focus:border-accent focus:outline-none"
            value={note}
            onChange={(e) => setNote(e.target.value)}
            maxLength={4000}
            autoFocus
          />
          <span className="mt-1 block text-xs text-ink-3">{t("Комментарий попадёт в активность задачи и в уведомление исполнителю.")}</span>
        </label>
      </Sheet>

      <Sheet
        open={blocking}
        onClose={() => setBlocking(false)}
        title={t("Заблокировать задачу")}
        subtitle={view.title}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" className="h-10 rounded-xl border border-line px-4 text-sm font-semibold text-ink-2" onClick={() => setBlocking(false)}>
              {t("Отмена")}
            </button>
            <button
              type="button"
              className="h-10 rounded-xl bg-bad px-4 text-sm font-semibold text-white disabled:opacity-50"
              disabled={!reason.trim()}
              onClick={async () => {
                if (await run("block", () => post("block", { blocked: true, reason: reason.trim() }))) setBlocking(false);
              }}
            >
              {t("Заблокировать")}
            </button>
          </div>
        }
      >
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Что мешает работе")}</span>
          <textarea
            className="min-h-20 w-full rounded-xl border border-line bg-surface px-3 py-2 text-sm text-ink focus:border-accent focus:outline-none"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            maxLength={500}
            autoFocus
            placeholder={t("Кого или что ждёт задача")}
          />
        </label>
      </Sheet>
    </div>
  );
}

function SheetAction({ icon: Icon, label, danger = false, onClick }: { icon: LucideIcon; label: string; danger?: boolean; onClick: () => void }) {
  return (
    <button type="button" onClick={onClick} className={`flex min-h-12 items-center gap-3 rounded-xl px-3 text-left text-[15px] font-medium hover:bg-surface-2 ${danger ? "text-bad" : "text-ink"}`}>
      <Icon className="size-5" aria-hidden />
      {label}
    </button>
  );
}

/**
 * Полоса этапов: Новая → В работе → На проверке → Выполнена. Пройденный — галочка и дата, текущий — цвет статуса и «с …», будущие —
 * приглушены. Заблокированная или отменённая задача — вместо полосы плашка с причиной.
 */
function StageBar({
  detail,
  status,
  blocked,
  reason,
  canUnblock,
  onUnblock,
  onReopen,
  busy,
}: {
  detail: WorkTaskDetail;
  status: WorkTaskStatus;
  blocked: boolean;
  reason: string | null;
  canUnblock: boolean;
  onUnblock: () => void;
  onReopen: (() => void) | null;
  busy: string | null;
}) {
  const t = useT();
  if (blocked) {
    return (
      <Plate tone="danger" icon={OctagonX} title={t("Работа заблокирована")} text={reason ?? t("Причина не указана")}>
        {canUnblock && (
          <button type="button" onClick={onUnblock} disabled={busy !== null} className="inline-flex h-11 items-center justify-center gap-2 rounded-xl border border-bad/40 bg-surface px-4 text-sm font-semibold text-ink disabled:opacity-60 lg:h-10">
            {busy === "unblock" ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <LockOpen className="size-4" aria-hidden />}
            {t("Разблокировать")}
          </button>
        )}
      </Plate>
    );
  }
  if (status === "Cancelled") {
    const cancelled = detail.history.find((h) => h.kind === "status" && h.text.startsWith("Задача отменена"));
    const why = cancelled?.text.replace(/^Задача отменена:?\s*/, "") || null;
    return (
      <Plate tone="muted" icon={Ban} title={t("Задача отменена")} text={why ? t("Причина: {text}", { text: why }) : cancelled ? `${cancelled.actorName} · ${stampParts(cancelled.createdAt).day}` : ""}>
        {onReopen && (
          <button type="button" onClick={onReopen} disabled={busy !== null} className="inline-flex h-11 items-center justify-center gap-2 rounded-xl border border-line-strong bg-surface px-4 text-sm font-semibold text-ink disabled:opacity-60 lg:h-10">
            <Undo2 className="size-4" aria-hidden />
            {t("Вернуть в работу")}
          </button>
        )}
      </Plate>
    );
  }

  const steps = stageSteps(status, detail.stages, detail.task.createdAt);
  const index = steps.findIndex((s) => s.state === "current");
  const short: Record<WorkTaskStatus, string> = { New: "Новая", InProgress: "В работе", Review: "Проверка", Done: "Готово", Cancelled: "Отменена" };

  return (
    <section aria-label={t("Этап {n} из {total}: {name}", { n: index + 1, total: steps.length, name: t(workStatusLabel[status]) })} className="rounded-2xl border border-line bg-surface px-[18px] py-3.5">
      {/* Компьютер */}
      <ol className="hidden items-center lg:flex">
        {steps.map((s, i) => {
          const look = statusLook[s.status];
          const when = s.at ? stampParts(s.at) : null;
          return (
            <li key={s.status} className={`flex items-center ${i < steps.length - 1 ? "flex-1" : ""}`}>
              <div className="flex shrink-0 items-center gap-2.5">
                <StepCircle state={s.state} icon={look.icon} tone={look.dot} index={i} size={26} />
                <div className="leading-tight">
                  <p className={`text-[13px] ${s.state === "current" ? "font-bold text-ink" : s.state === "done" ? "font-medium text-ink" : "text-subtle"}`}>{t(workStatusLabel[s.status])}</p>
                  {when && (
                    <p className="text-[11px] tabular-nums text-subtle">
                      {s.state === "current" && s.status !== "Done" ? t("с {date}", { date: `${when.day} ${when.time}` }) : `${when.day} ${when.time}`}
                    </p>
                  )}
                </div>
              </div>
              {i < steps.length - 1 && <span aria-hidden className={`mx-3 h-px flex-1 ${s.state === "done" ? "bg-ok/35" : "bg-line-strong"}`} />}
            </li>
          );
        })}
      </ol>
      {/* Телефон */}
      <ol className="grid grid-cols-4 lg:hidden">
        {steps.map((s, i) => {
          const look = statusLook[s.status];
          return (
            <li key={s.status} className="relative flex flex-col items-center gap-1.5">
              {i < steps.length - 1 && <span aria-hidden className={`absolute left-[calc(50%+16px)] right-[calc(-50%+16px)] top-3 h-px ${s.state === "done" ? "bg-ok/35" : "bg-line-strong"}`} />}
              <StepCircle state={s.state} icon={look.icon} tone={look.dot} index={i} size={24} />
              <span className={`text-[11px] ${s.state === "current" ? "font-bold text-ink" : s.state === "done" ? "text-ink-2" : "text-subtle"}`}>{t(short[s.status])}</span>
            </li>
          );
        })}
      </ol>
    </section>
  );
}

function StepCircle({ state, icon: Icon, tone, index, size }: { state: "done" | "current" | "future"; icon: LucideIcon; tone: string; index: number; size: number }) {
  if (state === "done") {
    return (
      <span className="grid shrink-0 place-items-center rounded-full bg-ok-soft text-ok" style={{ width: size, height: size }}>
        <Check className="size-3.5" strokeWidth={2.5} aria-hidden />
      </span>
    );
  }
  if (state === "current") {
    return (
      <span className={`grid shrink-0 place-items-center rounded-full text-white ${tone}`} style={{ width: size, height: size }}>
        <Icon className="size-3.5" strokeWidth={2.25} aria-hidden />
      </span>
    );
  }
  return (
    <span className="grid shrink-0 place-items-center rounded-full border border-line-strong bg-surface-2 text-[11px] font-semibold text-subtle" style={{ width: size, height: size }}>
      {index + 1}
    </span>
  );
}

const plateTone = {
  violet: { box: "border-violet/35 bg-violet-soft", tile: "bg-violet/20 text-violet" },
  accent: { box: "border-accent/35 bg-accent-soft", tile: "bg-accent/20 text-accent-strong" },
  danger: { box: "border-bad/35 bg-bad-soft", tile: "bg-bad/20 text-bad" },
  muted: { box: "border-line bg-surface", tile: "bg-surface-2 text-ink-3" },
};

function Plate({ tone, icon: Icon, title, text, children }: { tone: keyof typeof plateTone; icon: LucideIcon; title: string; text: string; children?: React.ReactNode }) {
  const look = plateTone[tone];
  return (
    <section className={`flex flex-col gap-3.5 rounded-2xl border p-4 lg:flex-row lg:items-center lg:gap-4 lg:px-[22px] lg:py-[18px] ${look.box}`}>
      <div className="flex min-w-0 flex-1 items-center gap-3">
        <span className={`grid size-11 shrink-0 place-items-center rounded-xl ${look.tile}`}>
          <Icon className="size-5" aria-hidden />
        </span>
        <div className="min-w-0">
          <p className="text-[15px] font-bold text-ink lg:text-base">{title}</p>
          {text && <p className="mt-0.5 text-[13px] text-ink-2">{text}</p>}
        </div>
      </div>
      {children && <div className="flex flex-col gap-2 lg:shrink-0 lg:flex-row-reverse lg:items-center lg:gap-2.5">{children}</div>}
    </section>
  );
}

/**
 * Плашка главного действия для этого пользователя: проверяющему — «Принять результат» и «Вернуть на доработку»; исполнителю в работе —
 * «Отправить на проверку»; новой задачи — «Взять в работу». Нет действия — нет плашки (кроме «на проверке у постановщика» и архива).
 */
function ActionPlate({
  detail,
  view,
  me,
  busy,
  archived,
  onStatus,
  onRestore,
}: {
  detail: WorkTaskDetail;
  view: WorkTaskDetail["task"];
  me: { id: string; name: string } | null;
  busy: string | null;
  archived: boolean;
  onStatus: (to: WorkTaskStatus) => void;
  onRestore: () => void;
}) {
  const t = useT();
  const next = view.nextStatuses;
  const spinner = (to: WorkTaskStatus) => busy === `status-${to}` && <Loader2 className="size-4 animate-spin" aria-hidden />;
  const primary = "inline-flex h-12 items-center justify-center gap-2 rounded-xl px-5 text-sm font-semibold disabled:opacity-60 lg:h-11";
  const secondary = `${primary} border border-line-strong bg-surface/60 text-ink hover:bg-surface-2`;

  if (archived) {
    return (
      <Plate tone="muted" icon={Archive} title={t("Задача в архиве")} text={t("Чтобы менять задачу, верните её из архива.")}>
        {detail.canRestore && (
          <button type="button" onClick={onRestore} disabled={busy !== null} className={secondary}>
            <ArchiveRestore className="size-4" aria-hidden />
            {t("Вернуть из архива")}
          </button>
        )}
      </Plate>
    );
  }
  if (view.isBlocked && open(view.status)) return null;

  if (view.status === "Review") {
    const review = detail.stages.find((s) => s.status === "Review");
    const when = review ? stampParts(review.at) : null;
    const reviewer = next.includes("Done") && next.includes("InProgress");
    const sent = when ? t("сдано {day} в {time}", { day: when.day, time: when.time }) : t("сдано на проверку");
    return (
      <Plate
        tone="violet"
        icon={ClipboardCheck}
        title={reviewer ? t("Задача ждёт вашей проверки") : t("Задача на проверке")}
        text={[review?.actorName, sent, t("выполнение {n}%", { n: view.progress })].filter(Boolean).join(" · ") + (reviewer ? "" : ` · ${t("ждёт решения постановщика")}`)}
      >
        {reviewer && (
          <>
            <button type="button" onClick={() => onStatus("Done")} disabled={busy !== null} className={`${primary} bg-ok text-[#04150c] hover:brightness-110`}>
              {spinner("Done") || <Check className="size-4" strokeWidth={2.5} aria-hidden />}
              {t("Принять результат")}
            </button>
            <button type="button" onClick={() => onStatus("InProgress")} disabled={busy !== null} className={secondary}>
              <Undo2 className="size-4" aria-hidden />
              {t("Вернуть на доработку")}
            </button>
          </>
        )}
      </Plate>
    );
  }

  if (view.status === "InProgress" && next.includes("Review")) {
    const mine = !!me && view.assigneeId === me.id;
    return (
      <Plate
        tone="accent"
        icon={Play}
        title={mine ? t("Задача у вас в работе") : t("Задача в работе")}
        text={[view.assigneeName && !mine ? view.assigneeName : null, t("выполнение {n}%", { n: view.progress }), view.dueDate ? t("срок {date}", { date: view.dueDate.slice(8, 10) + "." + view.dueDate.slice(5, 7) }) : null].filter(Boolean).join(" · ")}
      >
        <button type="button" onClick={() => onStatus("Review")} disabled={busy !== null} className={`${primary} bg-accent text-white hover:brightness-110`}>
          {spinner("Review") || <Send className="size-4" aria-hidden />}
          {t("Отправить на проверку")}
        </button>
      </Plate>
    );
  }

  if (view.status === "New" && next.includes("InProgress")) {
    const mine = !!me && view.assigneeId === me.id;
    return (
      <Plate
        tone="muted"
        icon={statusLook.New.icon}
        title={t("Новая задача")}
        text={mine ? t("Назначена вам") : view.assigneeName ? t("Исполнитель: {name}", { name: view.assigneeName }) : t("Исполнитель не назначен — задачу возьмёт тот, кто начнёт")}
      >
        <button type="button" onClick={() => onStatus("InProgress")} disabled={busy !== null} className={`${primary} bg-accent text-white hover:brightness-110`}>
          {spinner("InProgress") || <Play className="size-4" aria-hidden />}
          {t("Взять в работу")}
        </button>
      </Plate>
    );
  }
  return null;
}
