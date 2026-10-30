"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { Ban, Check, ChevronDown, ChevronRight, CircleCheckBig, GripVertical, Inbox, Loader2, Plus, SearchX, Undo2, X } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { plural } from "@/lib/notifications";
import { boardOrder, boardStatuses, canReview, columnTasks, dayMonth, mobileOrder, showProgress } from "@/lib/task-board";
import { markOpenedFromList, rememberTaskList } from "@/lib/task-list";
import { needsComment, statusActionLabel, workPriorityLabel, workStatusLabel, type DepartmentTasks, type WorkProject, type WorkTask, type WorkTaskStatus } from "@/lib/tasks";
import { DepartmentChip, DueLabel, PriorityPill, statusLook, UserAvatar } from "./TaskKit";
import { TaskForm } from "./TaskForm";

/** Сколько выполненных показывать в колонке — остальные в «Архиве и завершённых». */
const DoneLimit = 30;
const COLLAPSED = "onebase-board-collapsed";

type Asking = { task: WorkTask; to: WorkTaskStatus };

/**
 * Канбан (ТЗ tasks-kanban-detail). Компьютер — 4 колонки на всю высоту окна, карточки прокручиваются внутри колонки; перетаскивание
 * меняет статус (только разрешённые сервером переходы — nextStatuses: запрещённая колонка не подсвечивается, курсор «нельзя»); то же —
 * меню «Сменить статус» на карточке и Alt+←/→. Телефон — группы по статусам (сначала «На проверке»), мини-счётчики, долгое нажатие —
 * шторка «Сменить статус», свайп вправо по карточке на проверке — «Принять». Карточка переезжает сразу; ошибка сервера возвращает её
 * и показывает причину. «Вернуть» и отмена спрашивают комментарий. «+» в колонке — новая задача сразу в этом статусе.
 */
export function TaskBoard({
  items,
  total,
  departments,
  projects,
  filtered,
}: {
  items: WorkTask[];
  total: number;
  departments: DepartmentTasks[];
  projects: WorkProject[];
  filtered: boolean;
}) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [tasks, setTasks] = useState(items);
  const [dragging, setDragging] = useState<string | null>(null);
  const [over, setOver] = useState<WorkTaskStatus | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [toast, setToast] = useState<{ text: string; tone: "error" | "ok" } | null>(null);
  const [asking, setAsking] = useState<Asking | null>(null);
  const [comment, setComment] = useState("");
  const [creating, setCreating] = useState<WorkTaskStatus | null>(null);
  const [menu, setMenu] = useState<WorkTask | null>(null);
  const [collapsed, setCollapsed] = useState<Partial<Record<WorkTaskStatus, boolean>>>({});

  // Сервер прислал свежие данные (после действия или фильтра) — доска их показывает.
  useEffect(() => setTasks(items), [items]);

  // Порядок карточек — для «1 из 5» и листания в карточке задачи.
  const query = params.toString();
  useEffect(() => rememberTaskList(`${pathname}${query ? `?${query}` : ""}`, boardOrder(tasks).map((x) => x.id)), [tasks, pathname, query]);

  useEffect(() => {
    try {
      setCollapsed(JSON.parse(localStorage.getItem(COLLAPSED) ?? "{}"));
    } catch {
      // нет сохранённого — пустые группы свёрнуты
    }
  }, []);

  useEffect(() => {
    if (!toast) return;
    const timer = setTimeout(() => setToast(null), 6000);
    return () => clearTimeout(timer);
  }, [toast]);

  const canCreate = departments.some((d) => d.hasTasks) || projects.length > 0;

  const move = async (task: WorkTask, to: WorkTaskStatus, note?: string) => {
    if (task.status === to) return;
    if (!task.nextStatuses.includes(to)) {
      setToast({ tone: "error", text: t("«{title}»: перевести в «{status}» нельзя — нет прав или такой переход не предусмотрен.", { title: task.title, status: t(workStatusLabel[to]) }) });
      return;
    }
    if (needsComment(task.status, to) && note === undefined) {
      setComment("");
      setAsking({ task, to });
      return;
    }
    const before = tasks;
    setBusy(task.id);
    setTasks((list) =>
      to === "Cancelled"
        ? list.filter((x) => x.id !== task.id)
        : list.map((x) => (x.id === task.id ? { ...x, status: to, nextStatuses: [], isBlocked: to === "Done" ? false : x.isBlocked, progress: to === "Done" ? 100 : x.progress } : x)),
    );
    try {
      await bff(`work/tasks/${task.id}/status`, { method: "POST", body: JSON.stringify({ status: to, comment: note || null }) });
      if (to === "Done" && task.status === "Review") setToast({ tone: "ok", text: t("Результат принят: {title}", { title: task.title }) });
      router.refresh();
    } catch (e) {
      setTasks(before);
      setToast({ tone: "error", text: `«${task.title}»: ${(e as Error).message}` });
    } finally {
      setBusy(null);
    }
  };

  const toggleGroup = (status: WorkTaskStatus, value: boolean) => {
    setCollapsed((c) => {
      const next = { ...c, [status]: value };
      try {
        localStorage.setItem(COLLAPSED, JSON.stringify(next));
      } catch {
        // без запоминания
      }
      return next;
    });
  };

  const dragged = tasks.find((x) => x.id === dragging) ?? null;
  const counts = Object.fromEntries(boardStatuses.map((s) => [s, tasks.filter((x) => x.status === s).length])) as Record<WorkTaskStatus, number>;
  const reset = () => router.push(pathname, { scroll: false });
  const handlers = { busy, onMove: move, onMenu: setMenu, onReturn: (task: WorkTask) => move(task, "InProgress") };

  if (tasks.length === 0 && filtered) {
    return (
      <div className="grid flex-1 place-items-center px-4 py-16 text-center">
        <div>
          <span className="mx-auto grid size-12 place-items-center rounded-full bg-surface-2 text-ink-3">
            <SearchX className="size-6" aria-hidden />
          </span>
          <p className="mt-3 text-base font-semibold text-ink">{t("Задачи не найдены")}</p>
          <p className="mt-1 text-sm text-ink-3">{t("По этим фильтрам задач нет.")}</p>
          <button type="button" onClick={reset} className="mt-4 h-10 rounded-xl bg-accent px-4 text-sm font-semibold text-white">
            {t("Сбросить фильтры")}
          </button>
        </div>
      </div>
    );
  }

  return (
    <>
      {total > items.length && (
        <p className="px-4 text-xs text-ink-3 lg:px-0">{t("Показаны первые {n} из {total} задач — уточните фильтр.", { n: items.length, total })}</p>
      )}

      {/* Компьютер: 4 колонки на всю высоту. */}
      <div className="hidden min-h-[360px] flex-1 grid-cols-4 gap-3.5 lg:grid">
        {boardStatuses.map((status) => {
          const look = statusLook[status];
          const list = columnTasks(tasks, status);
          const shown = status === "Done" ? list.slice(0, DoneLimit) : list;
          const allowed = !!dragged && dragged.status !== status && dragged.nextStatuses.includes(status);
          const target = over === status && allowed;
          return (
            <section
              key={status}
              aria-labelledby={`col-${status}`}
              onDragOver={(e) => {
                if (!dragged) return;
                e.preventDefault();
                e.dataTransfer.dropEffect = allowed ? "move" : "none";
                setOver(status);
              }}
              onDragLeave={(e) => {
                if (!e.currentTarget.contains(e.relatedTarget as Node | null)) setOver((o) => (o === status ? null : o));
              }}
              onDrop={(e) => {
                e.preventDefault();
                setOver(null);
                const task = tasks.find((x) => x.id === e.dataTransfer.getData("text/plain"));
                setDragging(null);
                if (task && allowed) void move(task, status);
              }}
              className={`relative flex min-h-0 flex-col overflow-hidden rounded-[14px] border transition-colors ${
                target ? `${look.border} ${look.over}` : "border-line bg-[var(--tasks-column)]"
              }`}
            >
              <span aria-hidden className={`absolute inset-x-0 top-0 h-0.5 ${look.dot}`} />
              <header className="flex shrink-0 items-center gap-2 pb-2.5 pl-3.5 pr-3 pt-3">
                <span aria-hidden className={`size-2 rounded-full ${look.dot}`} />
                <h2 id={`col-${status}`} className="text-sm font-semibold text-ink">
                  {t(look.column)}
                </h2>
                <span className={`rounded-md bg-surface-2 px-1.5 text-xs font-semibold tabular-nums leading-5 ${list.length ? "text-ink-2" : "text-subtle"}`}>{list.length}</span>
                {canCreate && (
                  <button
                    type="button"
                    onClick={() => setCreating(status)}
                    aria-label={t("Новая задача в колонке «{status}»", { status: t(look.column) })}
                    className="ml-auto grid size-7 place-items-center rounded-lg border border-line bg-surface text-ink-2 hover:bg-surface-2 hover:text-ink"
                  >
                    <Plus className="size-4" aria-hidden />
                  </button>
                )}
              </header>
              <div role="list" aria-label={t(look.column)} className="flex min-h-0 flex-1 flex-col gap-2 overflow-y-auto overscroll-contain px-2.5 pb-2.5">
                {shown.map((task) => (
                  <BoardCard
                    key={task.id}
                    task={task}
                    variant="desktop"
                    dragging={dragging === task.id}
                    onDragStart={() => setDragging(task.id)}
                    onDragEnd={() => {
                      setDragging(null);
                      setOver(null);
                    }}
                    {...handlers}
                  />
                ))}
                {list.length === 0 && <EmptyColumn onAdd={canCreate ? () => setCreating(status) : null} />}
                {list.length > shown.length && (
                  <Link href="/tasks/archive" className="px-2 py-1 text-center text-xs font-medium text-link hover:underline">
                    {t("Ещё {n} — в «Архиве и завершённых»", { n: list.length - shown.length })}
                  </Link>
                )}
              </div>
            </section>
          );
        })}
      </div>

      {/* Телефон: мини-счётчики и группы по статусам. */}
      <div className="flex flex-col gap-4 px-4 pb-10 lg:hidden">
        <div className="grid grid-cols-4 gap-2">
          {boardStatuses.map((status) => {
            const look = statusLook[status];
            const n = counts[status];
            return (
              <button
                key={status}
                type="button"
                onClick={() => {
                  toggleGroup(status, false);
                  requestAnimationFrame(() => document.getElementById(`group-${status}`)?.scrollIntoView({ behavior: "smooth", block: "start" }));
                }}
                aria-label={`${t(look.column)}: ${n}`}
                className="flex min-h-[64px] flex-col items-start gap-1.5 rounded-xl border border-line bg-surface px-3 pb-2 pt-2.5 text-left"
              >
                <span aria-hidden className={`h-[3px] w-full rounded-full ${n ? look.dot : "bg-surface-2"}`} />
                <span className={`text-lg font-bold leading-none tabular-nums ${n ? "text-ink" : "text-subtle"}`}>{n}</span>
                <span className={`text-[11px] leading-none ${n ? "text-ink-2" : "text-subtle"}`}>{t(look.short)}</span>
              </button>
            );
          })}
        </div>

        {mobileOrder.map((status) => {
          const look = statusLook[status];
          const list = columnTasks(tasks, status);
          const shown = status === "Done" ? list.slice(0, DoneLimit) : list;
          const closed = collapsed[status] ?? list.length === 0;
          return (
            <section key={status} id={`group-${status}`} aria-labelledby={`grp-${status}`} className="scroll-mt-4">
              <div className="flex items-center gap-1">
                <button
                  type="button"
                  onClick={() => toggleGroup(status, !closed)}
                  aria-expanded={!closed}
                  className="flex min-h-11 min-w-0 flex-1 items-center gap-2.5 text-left"
                >
                  {closed ? <ChevronRight className="size-5 shrink-0 text-ink-3" aria-hidden /> : <ChevronDown className="size-5 shrink-0 text-ink-3" aria-hidden />}
                  <span aria-hidden className={`size-2 shrink-0 rounded-full ${look.dot}`} />
                  <h2 id={`grp-${status}`} className="truncate text-[15px] font-bold text-ink">
                    {t(look.column)}
                  </h2>
                  <span className={`rounded-md bg-surface-2 px-2 text-[13px] font-semibold tabular-nums leading-6 ${list.length ? "text-ink-2" : "text-subtle"}`}>{list.length}</span>
                </button>
                {canCreate && (
                  <button
                    type="button"
                    onClick={() => setCreating(status)}
                    aria-label={t("Новая задача в колонке «{status}»", { status: t(look.column) })}
                    className="grid size-11 shrink-0 place-items-center rounded-full text-ink-3"
                  >
                    <Plus className="size-5" aria-hidden />
                  </button>
                )}
              </div>
              {!closed && (
                <div role="list" aria-label={t(look.column)} className="mt-2 flex flex-col gap-2.5">
                  {shown.map((task) => (
                    <BoardCard key={task.id} task={task} variant="mobile" {...handlers} />
                  ))}
                  {list.length === 0 && <p className="rounded-2xl border border-dashed border-line-strong px-4 py-5 text-center text-[13px] text-subtle">{t("Нет задач")}</p>}
                  {list.length > shown.length && (
                    <Link href="/tasks/archive" className="py-1 text-center text-[13px] font-medium text-link">
                      {t("Ещё {n} — в «Архиве и завершённых»", { n: list.length - shown.length })}
                    </Link>
                  )}
                </div>
              )}
            </section>
          );
        })}
        <p className="text-xs text-subtle">{t("Удерживайте карточку, чтобы сменить статус")}</p>
      </div>

      {toast && (
        <div
          role={toast.tone === "error" ? "alert" : "status"}
          className={`fixed inset-x-4 bottom-[calc(env(safe-area-inset-bottom)+110px)] z-50 mx-auto flex max-w-[460px] items-start gap-3 rounded-2xl border px-4 py-3 text-sm shadow-xl lg:bottom-6 lg:left-auto lg:right-6 lg:mx-0 ${
            toast.tone === "error" ? "border-bad/30 bg-surface-solid text-ink" : "border-ok/30 bg-surface-solid text-ink"
          }`}
        >
          {toast.tone === "error" ? <Ban className="mt-0.5 size-4 shrink-0 text-bad" aria-hidden /> : <CircleCheckBig className="mt-0.5 size-4 shrink-0 text-ok" aria-hidden />}
          <span className="flex-1">{toast.text}</span>
          <button type="button" onClick={() => setToast(null)} aria-label={t("Закрыть")} className="text-ink-3 hover:text-ink">
            <X className="size-4" aria-hidden />
          </button>
        </div>
      )}

      <Sheet open={menu !== null} onClose={() => setMenu(null)} title={t("Сменить статус")} subtitle={menu?.title}>
        <div className="flex flex-col gap-1.5">
          {menu?.nextStatuses.map((s) => {
            const look = statusLook[s];
            return (
              <button
                key={s}
                type="button"
                onClick={() => {
                  const task = menu;
                  setMenu(null);
                  void move(task, s);
                }}
                className={`flex min-h-12 items-center gap-3 rounded-xl border border-line px-3.5 text-left text-sm font-semibold ${s === "Cancelled" ? "text-bad" : "text-ink"} hover:bg-surface-2`}
              >
                <look.icon className={`size-[18px] ${look.text}`} aria-hidden />
                <span className="flex-1">{t(statusActionLabel(menu.status, s))}</span>
                <span className="text-xs font-normal text-ink-3">{t(workStatusLabel[s])}</span>
              </button>
            );
          })}
          {menu && menu.nextStatuses.length === 0 && <p className="text-sm text-ink-3">{t("Сменить статус этой задачи вы не можете.")}</p>}
        </div>
      </Sheet>

      <Sheet
        open={asking !== null}
        onClose={() => setAsking(null)}
        title={asking ? t(statusActionLabel(asking.task.status, asking.to)) : ""}
        subtitle={asking?.task.title}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" className="h-10 rounded-xl border border-line px-4 text-sm font-semibold text-ink-2" onClick={() => setAsking(null)}>
              {t("Отмена")}
            </button>
            <button
              type="button"
              className="h-10 rounded-xl bg-accent px-4 text-sm font-semibold text-white disabled:opacity-50"
              disabled={asking?.task.status === "Review" && asking.to === "InProgress" && !comment.trim()}
              onClick={() => {
                const a = asking;
                setAsking(null);
                if (a) void move(a.task, a.to, comment.trim());
              }}
            >
              {asking?.to === "InProgress" ? t("Вернуть") : t("Подтвердить")}
            </button>
          </div>
        }
      >
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">
            {asking?.task.status === "Review" && asking.to === "InProgress" ? t("Что доработать") : t("Комментарий (необязательно)")}
          </span>
          <textarea
            className="min-h-24 w-full rounded-xl border border-line bg-surface px-3 py-2 text-sm text-ink focus:border-accent focus:outline-none"
            value={comment}
            onChange={(e) => setComment(e.target.value)}
            maxLength={4000}
            autoFocus
          />
          <span className="mt-1 block text-xs text-ink-3">{t("Комментарий попадёт в активность задачи и в уведомление исполнителю.")}</span>
        </label>
      </Sheet>

      {creating && (
        <TaskForm
          open
          onClose={() => setCreating(null)}
          departments={departments}
          projects={projects}
          defaultDepartment={params.get("department")?.split(",")[0] || undefined}
          status={creating}
          onStatusError={(text) => setToast({ tone: "error", text })}
        />
      )}
    </>
  );
}

function EmptyColumn({ onAdd }: { onAdd: (() => void) | null }) {
  const t = useT();
  return (
    <div className="flex flex-col items-center gap-1 rounded-xl border border-line-strong bg-[var(--tasks-ghost)] p-[22px] text-center">
      <Inbox className="size-5 text-subtle" aria-hidden />
      <p className="mt-1 text-[13px] font-semibold text-ink-2">{t("Нет задач")}</p>
      <p className="text-xs text-subtle">{t("Перетащите карточку сюда")}</p>
      {onAdd && (
        <button type="button" onClick={onAdd} className="mt-1.5 inline-flex items-center gap-1 text-xs font-semibold text-link hover:underline">
          <Plus className="size-3.5" aria-hidden />
          {t("Добавить задачу")}
        </button>
      )}
    </div>
  );
}

/**
 * Карточка задачи на доске. Нажатие открывает задачу; кнопки внутри (принять, вернуть, сменить статус) — свои. На компьютере — перетаскивание
 * и меню «Сменить статус» (значок захвата), Alt+←/→ — в соседнюю колонку; на телефоне — долгое нажатие и свайп вправо («Принять»).
 */
function BoardCard({
  task,
  variant,
  busy,
  dragging = false,
  onDragStart,
  onDragEnd,
  onMove,
  onMenu,
  onReturn,
}: {
  task: WorkTask;
  variant: "desktop" | "mobile";
  busy: string | null;
  dragging?: boolean;
  onDragStart?: () => void;
  onDragEnd?: () => void;
  onMove: (task: WorkTask, to: WorkTaskStatus) => void;
  onMenu: (task: WorkTask) => void;
  onReturn: (task: WorkTask) => void;
}) {
  const t = useT();
  const done = task.status === "Done";
  const review = canReview(task);
  const working = busy === task.id;
  const movable = task.nextStatuses.length > 0 && !working;
  const press = useRef<{ timer: ReturnType<typeof setTimeout> | null; x: number; y: number; long: boolean; swipe: boolean }>({ timer: null, x: 0, y: 0, long: false, swipe: false });
  const [shift, setShift] = useState(0);

  const label = [
    task.title,
    t("приоритет {p}", { p: t(workPriorityLabel[task.priority]).toLowerCase() }),
    task.dueDate ? t("срок {date}", { date: dayMonth(task.dueDate) }) : t("без срока"),
    task.assigneeName ? t("исполнитель {name}", { name: task.assigneeName }) : t("не назначен"),
  ].join(", ");

  const onKey = (e: React.KeyboardEvent) => {
    if (!e.altKey || (e.key !== "ArrowRight" && e.key !== "ArrowLeft")) return;
    const i = boardStatuses.indexOf(task.status);
    const to = boardStatuses[i + (e.key === "ArrowRight" ? 1 : -1)];
    if (!to) return;
    e.preventDefault();
    onMove(task, to);
  };

  // Телефон: удержание 0,5 с — шторка «Сменить статус»; свайп вправо по карточке на проверке — «Принять».
  const touch = variant === "mobile";
  const onTouchStart = (e: React.TouchEvent) => {
    const p = e.touches[0];
    press.current = { timer: null, x: p.clientX, y: p.clientY, long: false, swipe: false };
    if (movable) {
      press.current.timer = setTimeout(() => {
        press.current.long = true;
        navigator.vibrate?.(10);
        onMenu(task);
      }, 500);
    }
  };
  const onTouchMove = (e: React.TouchEvent) => {
    const p = e.touches[0];
    const dx = p.clientX - press.current.x;
    const dy = p.clientY - press.current.y;
    if (Math.abs(dx) > 10 || Math.abs(dy) > 10) {
      if (press.current.timer) clearTimeout(press.current.timer);
      press.current.timer = null;
    }
    if (review && !working && (press.current.swipe || (dx > 12 && Math.abs(dx) > Math.abs(dy) * 1.5))) {
      press.current.swipe = true;
      setShift(Math.max(0, Math.min(dx, 140)));
    }
  };
  const onTouchEnd = () => {
    if (press.current.timer) clearTimeout(press.current.timer);
    if (press.current.swipe) {
      if (shift >= 90) onMove(task, "Done");
      setShift(0);
    }
  };

  const titleLink = (
    <Link
      href={`/tasks/task/${task.id}`}
      draggable={false}
      onClick={(e) => {
        if (press.current.long || press.current.swipe) {
          e.preventDefault();
          press.current.long = false;
          press.current.swipe = false;
          return;
        }
        markOpenedFromList(task.id);
      }}
      className={`after:absolute after:inset-0 after:rounded-[inherit] after:content-[''] focus-visible:outline-none ${
        variant === "mobile" ? "text-sm" : "text-sm"
      } line-clamp-3 font-semibold leading-[1.35] ${done ? "text-ink-2" : "text-ink"}`}
    >
      {task.title}
    </Link>
  );

  const actions = review && (
    <div className={`relative z-10 grid grid-cols-2 gap-2 ${variant === "desktop" ? "border-t border-line pt-2.5" : ""}`}>
      <button
        type="button"
        disabled={working}
        onClick={() => onMove(task, "Done")}
        className={`inline-flex items-center justify-center gap-1.5 rounded-lg bg-ok-soft text-[13px] font-semibold text-ok hover:brightness-110 disabled:opacity-60 ${
          variant === "mobile" ? "h-11 rounded-xl border border-ok/30" : "h-8"
        }`}
      >
        {working ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Check className="size-4" aria-hidden />}
        {t("Принять")}
      </button>
      <button
        type="button"
        disabled={working}
        onClick={() => onReturn(task)}
        className={`inline-flex items-center justify-center gap-1.5 rounded-lg bg-surface-2 text-[13px] font-semibold text-ink-2 hover:text-ink disabled:opacity-60 ${
          variant === "mobile" ? "h-11 rounded-xl border border-line-strong" : "h-8"
        }`}
      >
        <Undo2 className="size-4" aria-hidden />
        {t("Вернуть")}
      </button>
    </div>
  );

  const blocked = task.isBlocked && (
    <p className="flex items-start gap-1.5 text-xs text-bad">
      <Ban className="mt-px size-3.5 shrink-0" aria-hidden />
      <span className="line-clamp-2">{task.blockedReason ?? t("Заблокирована")}</span>
    </p>
  );

  const progress = showProgress(task.progress) && (
    <span className="flex items-center gap-2" title={`${task.progress}%`}>
      <span className="block h-1 flex-1 overflow-hidden rounded-full bg-surface-2">
        <span className="block h-full rounded-full bg-accent" style={{ width: `${task.progress}%` }} />
      </span>
      <span className="text-[11px] tabular-nums text-ink-3">{task.progress}%</span>
    </span>
  );

  if (variant === "mobile") {
    return (
      <div className="relative overflow-hidden rounded-2xl">
        {review && shift > 0 && (
          <div aria-hidden className="absolute inset-0 flex items-center gap-2 rounded-2xl bg-ok-soft pl-5 text-sm font-semibold text-ok">
            <Check className="size-5" /> {t("Принять")}
          </div>
        )}
        <article
          role="listitem"
          aria-label={label}
          onTouchStart={onTouchStart}
          onTouchMove={onTouchMove}
          onTouchEnd={onTouchEnd}
          onTouchCancel={onTouchEnd}
          onContextMenu={(e) => movable && e.preventDefault()}
          style={{ transform: shift ? `translateX(${shift}px)` : undefined, transition: shift ? "none" : "transform 160ms ease-out" }}
          className={`relative flex select-none flex-col gap-2.5 rounded-2xl border bg-[var(--tasks-card)] p-3.5 [-webkit-touch-callout:none] [touch-action:pan-y] ${
            task.status === "Review" ? "border-violet/30" : task.isBlocked ? "border-bad/40" : "border-line"
          } ${working ? "opacity-70" : ""}`}
        >
          <div className="flex items-start gap-2.5">
            {done && <CircleCheckBig className="mt-px size-[18px] shrink-0 text-ok" aria-hidden />}
            <div className="min-w-0 flex-1">{titleLink}</div>
            <UserAvatar name={task.assigneeName} size={26} />
          </div>
          {blocked}
          <div className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 text-xs text-ink-3">
            <PriorityPill priority={task.priority} />
            <span className="truncate">{task.departmentName}</span>
            <span aria-hidden className="text-subtle">•</span>
            <DueLabel task={task} prefix icon={false} />
          </div>
          {progress}
          {actions}
        </article>
      </div>
    );
  }

  return (
    <article
      role="listitem"
      aria-label={label}
      draggable={movable}
      onDragStart={(e) => {
        e.dataTransfer.setData("text/plain", task.id);
        e.dataTransfer.effectAllowed = "move";
        onDragStart?.();
      }}
      onDragEnd={onDragEnd}
      onKeyDown={onKey}
      className={`group relative flex flex-col gap-2.5 rounded-xl border bg-[var(--tasks-card)] p-3 transition-[border-color,opacity] focus-within:border-accent/60 hover:border-line-strong ${
        task.status === "Review" ? "border-violet/30" : task.isBlocked ? "border-bad/40" : "border-line"
      } ${movable ? "cursor-grab active:cursor-grabbing" : ""} ${dragging ? "opacity-40" : ""} ${working ? "opacity-70" : ""}`}
    >
      <div className="flex items-center gap-2">
        <PriorityPill priority={task.priority} />
        {working && <Loader2 className="size-3.5 animate-spin text-ink-3" aria-hidden />}
        {task.nextStatuses.length > 0 && (
          <button
            type="button"
            onClick={() => onMenu(task)}
            aria-label={t("Сменить статус: {title}", { title: task.title })}
            title={t("Сменить статус")}
            className="relative z-10 ml-auto grid size-6 place-items-center rounded-md text-subtle opacity-0 transition-opacity hover:bg-surface-2 hover:text-ink focus-visible:opacity-100 group-focus-within:opacity-100 group-hover:opacity-100"
          >
            <GripVertical className="size-3.5" aria-hidden />
          </button>
        )}
      </div>
      <div className="flex items-start gap-2">
        {done && <CircleCheckBig className="mt-px size-4 shrink-0 text-ok" aria-hidden />}
        {titleLink}
      </div>
      {blocked}
      <div className="flex min-w-0 items-center gap-2.5 text-[11px]">
        <DepartmentChip name={task.departmentName} />
        <DueLabel task={task} />
        <span className="ml-auto">
          <UserAvatar name={task.assigneeName} />
        </span>
      </div>
      {progress}
      {actions}
    </article>
  );
}
