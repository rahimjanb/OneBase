"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { usePathname, useRouter } from "next/navigation";
import {
  BellRing,
  Bold,
  Building2,
  CalendarCheck,
  CalendarDays,
  Check,
  ChevronLeft,
  ChevronRight,
  Folder,
  Link2,
  List,
  ListChecks,
  Loader2,
  Paperclip,
  Plus,
  Send,
  SquarePlus,
  UserRound,
  X,
  type LucideIcon,
} from "lucide-react";
import { primary } from "@/components/ai/form";
import { useLang, useT } from "@/i18n/client";
import { formatDate, localeOf } from "@/i18n/core";
import { bff } from "@/lib/bff";
import { qs, todayInTashkent, type DepartmentTasks, type PageResult, type WorkAssignee, type WorkProject, type WorkTask, type WorkTaskPriority, type WorkTaskStatus } from "@/lib/tasks";
import type { Me } from "@/lib/users";
import { AssigneePicker } from "./AssigneePicker";
import { statusLook, UserAvatar } from "./TaskKit";
import { ChecklistEditor, Chips, DateField, FieldError, FieldLabel, FileList, inputBox, PrioritySegments, TimeField, Toggle, trapTab, useKeyboardInset, useMediaQuery } from "./TaskFormParts";
import { showTaskToast } from "./TaskToast";

type Draft = {
  department: string;
  projectId: string;
  title: string;
  description: string;
  assigneeId: string;
  priority: WorkTaskPriority;
  startDate: string;
  dueDate: string;
  /** «15:00»; пусто — до конца дня. */
  dueTime: string;
  /** Пункты чек-листа новой задачи — добавятся после создания. */
  checklist: string[];
};

type Errors = { title?: string; due?: string; files?: string };
type Inner = "department" | "project" | "assignee" | "start" | "due";

/** Название новой задачи — до 120 символов (ТЗ формы); у старых задач при правке — до 300, как на сервере. */
const TITLE_NEW = 120;
const TITLE_EDIT = 300;
const MAX_FILE = 20 * 1024 * 1024;
/** Время срока по умолчанию — конец рабочего дня. */
const END_OF_DAY = "18:00";
const DRAFT_KEY = "onebase-task-draft";
const DRAFT_DAYS = 7;

type StoredDraft = { at: number; draft: Draft };

const readDraft = (): StoredDraft | null => {
  try {
    const stored = JSON.parse(localStorage.getItem(DRAFT_KEY) ?? "null") as StoredDraft | null;
    return stored?.draft?.title !== undefined && Date.now() - stored.at < DRAFT_DAYS * 86_400_000 ? stored : null;
  } catch {
    return null;
  }
};
const writeDraft = (draft: Draft) => {
  try {
    localStorage.setItem(DRAFT_KEY, JSON.stringify({ at: Date.now(), draft } satisfies StoredDraft));
  } catch {
    // хранилище недоступно (приватный режим) — без черновика
  }
};
const dropDraft = () => {
  try {
    localStorage.removeItem(DRAFT_KEY);
  } catch {
    // нет хранилища
  }
};

const addDays = (iso: string, days: number) => {
  const d = new Date(`${iso}T12:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
};
const weekday = (iso: string) => new Date(`${iso}T12:00:00Z`).getUTCDay();

/** «Азиз Назаров» → «Азиз Н.» — коротко для чипа быстрого выбора. */
const shortName = (name: string) => {
  const [first, second] = name.trim().split(/\s+/);
  return second ? `${first} ${second.charAt(0)}.` : first;
};

/**
 * «Новая задача» и правка задачи: на компьютере (≥ 768 px) — окно 640 px с закреплёнными шапкой и подвалом, на телефоне — шторка на всю
 * высоту со строками свойств и вложенным выбором. Поля: название (обязательно), описание (Markdown), отдел и проект, исполнитель (быстрый
 * выбор: «Я» и недавние), приоритет, начало и срок (дата, время, быстрые варианты), у новой — чек-лист и файлы. Сервер ещё раз проверяет
 * всё это и права — форма только помогает не ошибиться. Подзаголовок — откуда открыта форма (колонка канбана, отдел, проект).
 * ⌘/Ctrl+Enter — отправить, Esc — закрыть (с вопросом, если что-то введено; введённое остаётся черновиком до следующего открытия).
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
  status,
  onStatusError,
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
  /** Новая задача сразу в этом статусе («+» в колонке канбана): после создания — переход по обычным правилам статусов. */
  status?: WorkTaskStatus;
  /** Задача создана, а перевести её в статус не вышло (нет прав на такой переход). */
  onStatusError?: (message: string) => void;
}) {
  const t = useT();
  const locale = localeOf(useLang());
  const router = useRouter();
  const pathname = usePathname();
  const desktop = useMediaQuery("(min-width: 768px)");
  const editing = !!task;
  const limit = editing ? TITLE_EDIT : TITLE_NEW;
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
    dueTime: task?.dueTime?.slice(0, 5) ?? "",
    checklist: [],
  });
  const [draft, setDraft] = useState<Draft>(initial);
  const [base, setBase] = useState(() => JSON.stringify(initial()));
  const [files, setFiles] = useState<File[]>([]);
  const [checkText, setCheckText] = useState("");
  const [checkOpen, setCheckOpen] = useState(false);
  const [descOpen, setDescOpen] = useState(false);
  const [assignees, setAssignees] = useState<WorkAssignee[]>([]);
  const [me, setMe] = useState<{ id: string; name: string } | null>(null);
  const [recent, setRecent] = useState<string[]>([]);
  const [errors, setErrors] = useState<Errors>({});
  const [serverError, setServerError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [again, setAgain] = useState(false);
  const [saved, setSaved] = useState<StoredDraft | null>(null);
  const [inner, setInner] = useState<Inner | null>(null);
  const [mac, setMac] = useState(true);
  const [dragY, setDragY] = useState(0);
  const panel = useRef<HTMLDivElement>(null);
  const titleInput = useRef<HTMLInputElement>(null);
  const titleArea = useRef<HTMLTextAreaElement>(null);
  const descArea = useRef<HTMLTextAreaElement>(null);
  const fileInput = useRef<HTMLInputElement>(null);
  const drag = useRef<number | null>(null);
  const keyboard = useKeyboardInset(open && !desktop);
  const today = todayInTashkent();

  // Открыли заново — форма с начальными значениями (правка другой задачи или новая); черновик — только у пустой новой задачи.
  useEffect(() => {
    if (!open) return;
    const init = initial();
    setDraft(init);
    setBase(JSON.stringify(init));
    setFiles([]);
    setCheckText("");
    setCheckOpen(false);
    setDescOpen(!!init.description);
    setErrors({});
    setServerError(null);
    setInner(null);
    setDragY(0);
    setSaved(!task && !prefill ? readDraft() : null);
    setMac(/Mac|iPhone|iPad/.test(navigator.userAgent));
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

  // Быстрый выбор: «Я» и недавние исполнители моих задач в этом отделе (из моих последних задач, без отдельного хранения).
  useEffect(() => {
    if (!open) return;
    let alive = true;
    bff<Me>("auth/me")
      .then((m) => alive && setMe({ id: m.id, name: m.name }))
      .catch(() => alive && setMe(null));
    return () => {
      alive = false;
    };
  }, [open]);
  useEffect(() => {
    if (!open || editing || !draft.department) return;
    let alive = true;
    bff<PageResult<WorkTask>>(`work/tasks${qs({ scope: "all", department: draft.department, createdByMe: true, sort: "created", desc: true, pageSize: 30 })}`)
      .then((page) => alive && setRecent([...new Set(page.items.map((x) => x.assigneeId).filter((id): id is string => !!id))]))
      .catch(() => alive && setRecent([]));
    return () => {
      alive = false;
    };
  }, [open, editing, draft.department]);

  // Окно забирает фокус (название — autoFocus), закрытое возвращает его туда, откуда открыли; фон не прокручивается.
  useEffect(() => {
    if (!open) return;
    const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const target = document.querySelector<HTMLElement>("[data-app-scroll]") ?? document.body;
    const overflow = target.style.overflow;
    target.style.overflow = "hidden";
    return () => {
      target.style.overflow = overflow;
      if (opener?.isConnected) opener.focus({ preventScroll: true });
    };
  }, [open]);

  // Описание растёт с текстом: от 72 до 240 px.
  useEffect(() => {
    const el = descArea.current;
    if (!el) return;
    el.style.height = "auto";
    el.style.height = `${Math.min(240, Math.max(72, el.scrollHeight))}px`;
  }, [draft.description, descOpen, desktop, open]);

  const departmentProjects = projects.filter((p) => p.departmentCode === draft.department && (!p.archived || p.id === draft.projectId));
  const set = <K extends keyof Draft>(key: K, value: Draft[K]) => setDraft((d) => ({ ...d, [key]: value }));
  const dirty = JSON.stringify(draft) !== base || files.length > 0 || checkText.trim() !== "";
  const titleLength = draft.title.length;
  const valid = !!draft.title.trim() && titleLength <= limit && !!draft.department;
  const deptName = (code: string) => creatable.find((d) => d.code === code)?.name ?? departments.find((d) => d.code === code)?.name ?? code;
  const projectName = projects.find((p) => p.id === draft.projectId)?.name ?? null;

  // Кнопка «Новая задача» в шапке канбана — задача попадёт в колонку «Новые».
  const column = status ?? (pathname.startsWith("/tasks/board") ? "New" : null);
  const subtitle = task
    ? [task.departmentName, task.projectName].filter(Boolean).join(" · ")
    : column
      ? t("Канбан · попадёт в колонку «{column}»", { column: t(statusLook[column].column) })
      : defaultProject
        ? t("Проект: {name}", { name: projects.find((p) => p.id === defaultProject)?.name ?? "—" })
        : prefill
          ? t("Из рекомендации AI-консультанта")
          : defaultDepartment
            ? t("Отдел: {name}", { name: deptName(defaultDepartment) })
            : null;

  // ---------- Сроки ----------
  const friday = addDays(today, (5 - weekday(today) + 7) % 7);
  const dueOptions = [
    { key: "today", label: t("Сегодня"), date: today },
    { key: "tomorrow", label: t("Завтра"), date: addDays(today, 1) },
    { key: "friday", label: t("Пятница"), date: friday },
    { key: "week", label: desktop ? t("Через неделю") : t("+1 неделя"), date: addDays(today, 7) },
    { key: "none", label: t("Без срока"), date: "" },
  ];
  const dueActive = dueOptions.find((o) => o.date === draft.dueDate)?.key ?? null;
  const pickDue = (key: string) => {
    const option = dueOptions.find((o) => o.key === key);
    if (!option) return;
    setDraft((d) => ({ ...d, dueDate: option.date, dueTime: option.date ? d.dueTime || END_OF_DAY : "" }));
    setErrors((e) => ({ ...e, due: undefined }));
  };
  const setDueDate = (value: string) => {
    setDraft((d) => ({ ...d, dueDate: value, dueTime: value ? d.dueTime || END_OF_DAY : "" }));
    setErrors((e) => ({ ...e, due: undefined }));
  };
  const dayText = (iso: string, short = false) => {
    if (!iso) return "";
    const date = new Date(`${iso}T12:00:00`);
    const dm = formatDate(date, locale, { day: "numeric", month: "long" });
    if (iso === today) return short ? t("Сегодня") : `${t("Сегодня")}, ${dm}`;
    if (iso === addDays(today, 1)) return short ? t("Завтра") : `${t("Завтра")}, ${dm}`;
    return short ? dm : formatDate(date, locale, { weekday: "short", day: "numeric", month: "long" });
  };
  const dueMin = task?.dueDate && task.dueDate < today ? undefined : today;

  // ---------- Исполнитель: быстрый выбор ----------
  const quick = useMemo(() => {
    const list: { id: string; label: string; name: string }[] = [];
    if (me && assignees.some((a) => a.id === me.id)) list.push({ id: me.id, label: t("Я"), name: me.name });
    for (const id of recent) {
      if (list.length >= 4 || id === me?.id) continue;
      const a = assignees.find((x) => x.id === id);
      if (a) list.push({ id: a.id, label: shortName(a.name), name: a.name });
    }
    return list;
  }, [me, recent, assignees, t]);
  const assigneeName = assignees.find((a) => a.id === draft.assigneeId)?.name ?? (task?.assigneeId === draft.assigneeId ? task?.assigneeName : null) ?? null;

  // ---------- Описание: панель Markdown ----------
  const edit = (change: (value: string, start: number, end: number) => { value: string; start: number; end: number }) => {
    const el = descArea.current;
    if (!el) return;
    const next = change(el.value, el.selectionStart, el.selectionEnd);
    set("description", next.value.slice(0, 4000));
    requestAnimationFrame(() => {
      el.focus();
      el.setSelectionRange(next.start, next.end);
    });
  };
  const wrap = (before: string, after: string, placeholder: string) =>
    edit((value, s, e) => {
      const selected = value.slice(s, e) || placeholder;
      return { value: value.slice(0, s) + before + selected + after + value.slice(e), start: s + before.length, end: s + before.length + selected.length };
    });
  const bullet = () =>
    edit((value, s, e) => {
      const from = value.lastIndexOf("\n", s - 1) + 1;
      const lines = value
        .slice(from, e)
        .split("\n")
        .map((line) => (line.startsWith("- ") ? line : `- ${line}`))
        .join("\n");
      return { value: value.slice(0, from) + lines + value.slice(e), start: from + lines.length, end: from + lines.length };
    });

  // ---------- Файлы ----------
  const addFiles = (list: FileList | null) => {
    if (!list) return;
    const ok: File[] = [];
    let tooBig = false;
    for (const file of Array.from(list)) {
      if (file.size > MAX_FILE) tooBig = true;
      else ok.push(file);
    }
    setFiles((current) => [...current, ...ok]);
    setErrors((e) => ({ ...e, files: tooBig ? t("Размер файла не должен превышать 20 МБ") : undefined }));
  };

  // ---------- Закрытие ----------
  const requestClose = useCallback(() => {
    if (busy) return false;
    if (dirty) {
      const question = editing ? t("Изменения не сохранятся. Закрыть?") : t("Закрыть форму? Введённое останется черновиком — его можно будет восстановить.");
      if (!window.confirm(question)) return false;
      if (!editing) writeDraft({ ...draft, checklist: checkText.trim() ? [...draft.checklist, checkText.trim()] : draft.checklist });
    }
    onClose();
    return true;
  }, [busy, dirty, editing, draft, checkText, onClose, t]);

  const restore = () => {
    if (!saved) return;
    const d = saved.draft;
    setDraft((current) => ({
      ...current,
      ...d,
      department: creatable.some((x) => x.code === d.department) ? d.department : current.department,
      checklist: Array.isArray(d.checklist) ? d.checklist : [],
    }));
    setDescOpen(!!d.description);
    setCheckOpen(Array.isArray(d.checklist) && d.checklist.length > 0);
    setSaved(null);
    dropDraft();
  };

  // ---------- Отправка ----------
  const validate = (): Errors => {
    const e: Errors = {};
    if (!draft.title.trim()) e.title = t("Введите название задачи");
    else if (titleLength > limit) e.title = t("Название — не длиннее {n} символов", { n: limit });
    if (draft.startDate && draft.dueDate && draft.dueDate < draft.startDate) e.due = t("Срок не может быть раньше даты начала.");
    return e;
  };

  const submit = async () => {
    if (busy || !draft.department) return;
    const found = validate();
    setErrors((e) => ({ ...found, files: e.files }));
    if (found.title || found.due) {
      if (found.title) (desktop ? titleInput.current : titleArea.current)?.focus();
      return;
    }
    setBusy(true);
    setServerError(null);
    const body = {
      title: draft.title.trim(),
      description: draft.description.trim() || null,
      assigneeId: draft.assigneeId || null,
      priority: draft.priority,
      startDate: draft.startDate || null,
      dueDate: draft.dueDate || null,
      dueTime: draft.dueDate && draft.dueTime ? `${draft.dueTime}:00` : null,
      projectId: draft.projectId || null,
    };
    if (task) {
      try {
        await bff(`work/tasks/${task.id}`, { method: "PUT", body: JSON.stringify(body) });
      } catch (e) {
        setServerError((e as Error).message);
        setBusy(false);
        return;
      }
      setBusy(false);
      onClose();
      router.refresh();
      return;
    }

    let id: string;
    try {
      id = (await bff<{ id: string }>("work/tasks", { method: "POST", body: JSON.stringify({ ...body, department: draft.department }) })).id;
    } catch (e) {
      setServerError((e as Error).message);
      setBusy(false);
      return;
    }
    // Задача уже создана: ошибки дальше не держат форму открытой — повторная отправка создала бы вторую задачу.
    let statusError: string | null = null;
    if (status && status !== "New") {
      await bff(`work/tasks/${id}/status`, { method: "POST", body: JSON.stringify({ status, comment: null }) }).catch((e: Error) => {
        statusError = e.message;
      });
    }
    const missed: string[] = [];
    const items = checkText.trim() ? [...draft.checklist, checkText.trim()] : draft.checklist;
    for (const title of items) {
      await bff(`work/tasks/${id}/checklist`, { method: "POST", body: JSON.stringify({ title }) }).catch(() => missed.push(t("пункт «{text}»", { text: title })));
    }
    for (const file of files) {
      const form = new FormData();
      form.append("file", file);
      // multipart — без Content-Type: браузер сам ставит границу.
      const response = await fetch(`/bff/api/work/tasks/${id}/attachments`, { method: "POST", body: form }).catch(() => null);
      if (!response?.ok) missed.push(t("файл «{name}»", { name: file.name }));
    }
    dropDraft();
    setBusy(false);

    if (again && !onCreated) {
      const next: Draft = { ...initial(), department: draft.department, projectId: draft.projectId, assigneeId: draft.assigneeId, title: "", description: "" };
      setDraft(next);
      setBase(JSON.stringify(next));
      setFiles([]);
      setCheckText("");
      setCheckOpen(false);
      setDescOpen(false);
      setErrors({});
      requestAnimationFrame(() => titleInput.current?.focus());
      router.refresh();
    } else {
      onClose();
      if (onCreated) onCreated(id);
      else router.refresh();
    }
    if (statusError) onStatusError?.(statusError);
    if (missed.length > 0) showTaskToast({ tone: "error", text: t("Задача создана, но не добавилось: {list}", { list: missed.join(", ") }), href: `/tasks/task/${id}` });
    else if (!onCreated) showTaskToast({ tone: "ok", text: t("Задача создана"), href: `/tasks/task/${id}`, action: t("Открыть") });
  };

  const onKeyDown = (e: React.KeyboardEvent<HTMLDivElement>) => {
    if (e.key === "Escape") {
      e.preventDefault();
      if (inner) setInner(null);
      else requestClose();
      return;
    }
    if (e.key === "Enter" && (e.metaKey || e.ctrlKey)) {
      e.preventDefault();
      void submit();
      return;
    }
    trapTab(e, panel.current);
  };

  if (!open) return null;

  const titleError = errors.title;
  const counter = (
    <span className={`text-[11px] tabular-nums ${titleLength > limit ? "font-semibold text-bad" : "text-subtle"}`} aria-live="polite">
      {titleLength} / {limit}
    </span>
  );
  const fileField = <input ref={fileInput} type="file" multiple className="hidden" onChange={(e) => (addFiles(e.target.files), (e.target.value = ""))} />;
  const draftBanner = saved && (
    <div className="flex flex-wrap items-center gap-x-3 gap-y-2 rounded-xl border border-accent/30 bg-accent-soft px-3.5 py-2.5 text-[13px] text-ink-2">
      <span className="min-w-0 flex-1">
        {saved.draft.title.trim() ? t("Есть черновик: «{title}»", { title: saved.draft.title.trim().slice(0, 60) }) : t("Есть несохранённый черновик задачи")}
      </span>
      <button type="button" onClick={restore} className="font-semibold text-link hover:underline">
        {t("Восстановить")}
      </button>
      <button
        type="button"
        onClick={() => {
          dropDraft();
          setSaved(null);
        }}
        className="text-subtle hover:text-ink"
      >
        {t("Удалить")}
      </button>
    </div>
  );
  const serverPlaque = serverError && (
    <div role="alert" className="flex items-start gap-3 border-t border-bad/30 bg-bad-soft px-5 py-3 text-[13px] text-bad">
      <span className="min-w-0 flex-1">{serverError}</span>
      <button type="button" onClick={() => void submit()} className="shrink-0 font-semibold underline-offset-2 hover:underline">
        {t("Повторить")}
      </button>
    </div>
  );
  const pushHint = (short: boolean) => (
    <p className="flex items-center gap-2 text-xs text-subtle">
      <BellRing className="size-3.5 shrink-0" aria-hidden />
      <span className={short ? "" : "truncate"}>
        {short ? t("Исполнитель сразу получит push-уведомление") : t("Исполнитель сразу получит уведомление в OneBase и push на устройства, где они включены")}
      </span>
    </p>
  );
  const extras = !editing && (
    <>
      {checkOpen && <ChecklistEditor items={draft.checklist} onChange={(items) => set("checklist", items)} text={checkText} onText={setCheckText} large={!desktop} />}
      <FileList files={files} onRemove={(i) => setFiles((list) => list.filter((_, j) => j !== i))} large={!desktop} />
      <FieldError text={errors.files} />
    </>
  );
  const submitLabel = editing ? t("Сохранить") : t("Поставить задачу");

  // ---------- Компьютер: окно 640 px ----------
  const desktopView = (
    <div className="tasks-ui task-form-fade fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-[#03060D]/80 px-4 py-10 backdrop-blur-[4px]" onMouseDown={(e) => e.target === e.currentTarget && requestClose()}>
      <div
        ref={panel}
        role="dialog"
        aria-modal="true"
        aria-labelledby="task-form-title"
        onKeyDown={onKeyDown}
        className="task-form-pop flex max-h-[calc(100dvh-80px)] w-full max-w-[640px] flex-col overflow-hidden rounded-[20px] border border-line-strong bg-surface-solid text-ink shadow-[0_30px_80px_#000000CC]"
      >
        <header className="flex shrink-0 items-center gap-3.5 border-b border-line pb-3.5 pl-[22px] pr-[18px] pt-[18px]">
          <span className="grid size-9 shrink-0 place-items-center rounded-[10px] bg-accent-soft text-link">
            <SquarePlus className="size-[18px]" aria-hidden />
          </span>
          <div className="min-w-0 flex-1">
            <h2 id="task-form-title" className="truncate text-[17px] font-bold leading-6 text-ink">
              {editing ? t("Изменить задачу") : t("Новая задача")}
            </h2>
            {subtitle && <p className="truncate text-xs text-ink-3">{subtitle}</p>}
          </div>
          <button type="button" onClick={requestClose} aria-label={t("Закрыть")} className="grid size-[34px] shrink-0 place-items-center rounded-[10px] bg-surface-2 text-ink-3 transition-colors duration-150 hover:text-ink">
            <X className="size-4" />
          </button>
        </header>

        <div className="flex flex-1 flex-col gap-4 overflow-y-auto px-[22px] py-5 [&>*]:shrink-0">
          {draftBanner}
          <div>
            <FieldLabel htmlFor="task-title" required aside={counter}>
              {t("Название")}
            </FieldLabel>
            <input
              ref={titleInput}
              id="task-title"
              autoFocus
              value={draft.title}
              aria-invalid={!!titleError || undefined}
              aria-describedby={titleError ? "task-title-error" : undefined}
              onChange={(e) => {
                set("title", e.target.value);
                if (titleError) setErrors((x) => ({ ...x, title: undefined }));
              }}
              onKeyDown={(e) => e.key === "Enter" && !e.metaKey && !e.ctrlKey && e.preventDefault()}
              placeholder={t("Что нужно сделать?")}
              className={`h-[52px] w-full px-3.5 text-base font-medium text-ink placeholder:text-subtle ${inputBox(!!titleError)}`}
            />
            <FieldError id="task-title-error" text={titleError} />
          </div>

          <div>
            <FieldLabel htmlFor="task-description">{t("Описание")}</FieldLabel>
            <div className="overflow-hidden rounded-xl border border-line-strong bg-page transition-colors duration-150 focus-within:border-accent focus-within:ring-[3px] focus-within:ring-accent/20">
              <textarea
                ref={descArea}
                id="task-description"
                value={draft.description}
                maxLength={4000}
                onChange={(e) => set("description", e.target.value)}
                placeholder={t("Подробности, ожидаемый результат, ссылки…")}
                className="block max-h-[240px] min-h-[72px] w-full resize-none bg-transparent px-3.5 py-3 text-sm leading-5 text-ink outline-none placeholder:text-subtle"
              />
              <div className="flex items-center gap-1 border-t border-line px-2 py-1.5">
                <ToolButton icon={Bold} label={t("Жирный")} onClick={() => wrap("**", "**", t("жирный текст"))} />
                <ToolButton icon={List} label={t("Список")} onClick={bullet} />
                {!editing && <ToolButton icon={ListChecks} label={t("Чек-лист")} onClick={() => setCheckOpen(true)} />}
                <ToolButton icon={Link2} label={t("Ссылка")} onClick={() => wrap("[", "](https://)", t("текст ссылки"))} />
                {!editing && <ToolButton icon={Paperclip} label={t("Прикрепить файл")} onClick={() => fileInput.current?.click()} />}
                <span className="ml-auto pr-1.5 text-[11px] text-subtle">Markdown</span>
              </div>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <FieldLabel htmlFor="task-department">{t("Отдел")}</FieldLabel>
              <SelectBox icon={Building2}>
                <select
                  id="task-department"
                  value={draft.department}
                  disabled={editing}
                  onChange={(e) => setDraft((d) => ({ ...d, department: e.target.value, projectId: "", assigneeId: "" }))}
                  className={`h-[52px] w-full pl-10 pr-3.5 text-sm font-medium text-ink disabled:opacity-60 ${inputBox()}`}
                >
                  {creatable.map((d) => (
                    <option key={d.code} value={d.code}>
                      {d.name}
                    </option>
                  ))}
                </select>
              </SelectBox>
            </div>
            <div>
              <FieldLabel htmlFor="task-project">{t("Проект")}</FieldLabel>
              <SelectBox icon={Folder}>
                <select
                  id="task-project"
                  value={draft.projectId}
                  onChange={(e) => set("projectId", e.target.value)}
                  className={`h-[52px] w-full pl-10 pr-3.5 text-sm font-medium ${draft.projectId ? "text-ink" : "text-subtle"} ${inputBox()}`}
                >
                  <option value="">{t("Без проекта")}</option>
                  {departmentProjects.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name}
                    </option>
                  ))}
                </select>
              </SelectBox>
            </div>
          </div>

          <div>
            <FieldLabel htmlFor="task-assignee">{t("Исполнитель")}</FieldLabel>
            <AssigneePicker
              id="task-assignee"
              look="form"
              value={draft.assigneeId}
              onChange={(id) => set("assigneeId", id)}
              assignees={assignees}
              current={task?.assigneeId ? { id: task.assigneeId, name: task.assigneeName } : null}
            />
            {quick.length > 0 && (
              <div className="mt-2.5 flex flex-wrap items-center gap-2">
                <span className="text-[13px] text-subtle">{t("Быстрый выбор:")}</span>
                {quick.map((q) => (
                  <button
                    key={q.id}
                    type="button"
                    aria-pressed={draft.assigneeId === q.id}
                    aria-label={t("Назначить: {name}", { name: q.name })}
                    onClick={() => set("assigneeId", q.id)}
                    className={`inline-flex h-8 items-center gap-2 rounded-[14px] border pl-1 pr-3 text-[13px] font-medium transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent ${
                      draft.assigneeId === q.id ? "border-accent/35 bg-accent-soft text-link" : "border-transparent bg-surface-2 text-ink-2 hover:text-ink"
                    }`}
                  >
                    <UserAvatar name={q.name} size={20} />
                    {q.label}
                  </button>
                ))}
              </div>
            )}
            <div className="mt-2.5">{pushHint(false)}</div>
          </div>

          <div>
            <FieldLabel id="task-priority-label">{t("Приоритет")}</FieldLabel>
            <PrioritySegments value={draft.priority} onChange={(p) => set("priority", p)} labelId="task-priority-label" />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <FieldLabel>{t("Дата начала")}</FieldLabel>
              <DateField
                value={draft.startDate}
                text={dayText(draft.startDate)}
                placeholder={t("Не указана")}
                ariaLabel={t("Дата начала")}
                icon={CalendarDays}
                onChange={(v) => (set("startDate", v), setErrors((x) => ({ ...x, due: undefined })))}
                onClear={() => set("startDate", "")}
              />
            </div>
            <div>
              <FieldLabel>{t("Срок")}</FieldLabel>
              <div className="grid grid-cols-[minmax(0,1fr)_96px] gap-2">
                <DateField
                  value={draft.dueDate}
                  text={dayText(draft.dueDate)}
                  placeholder={t("Выберите дату")}
                  ariaLabel={t("Дата срока")}
                  icon={CalendarCheck}
                  onChange={setDueDate}
                  min={dueMin}
                  invalid={!!errors.due}
                  describedBy={errors.due ? "task-due-error" : undefined}
                  chevron={false}
                />
                <TimeField value={draft.dueTime} onChange={(v) => set("dueTime", v)} disabled={!draft.dueDate} />
              </div>
              <FieldError id="task-due-error" text={errors.due} />
            </div>
          </div>

          <Chips label={t("Срок:")} options={dueOptions} active={dueActive} onPick={pickDue} />

          {!editing && (
            <div className="flex flex-wrap gap-2">
              <ExtraButton icon={ListChecks} label={t("Добавить чек-лист")} active={checkOpen} onClick={() => setCheckOpen(true)} />
              <ExtraButton icon={Paperclip} label={t("Прикрепить файл")} active={files.length > 0} onClick={() => fileInput.current?.click()} />
            </div>
          )}
          {extras}
          {fileField}
        </div>

        {serverPlaque}
        <footer className="flex shrink-0 items-center gap-3 border-t border-line bg-surface-2 px-[22px] py-3.5">
          {!editing && !onCreated && <Toggle checked={again} onChange={setAgain} label={t("Создать ещё одну")} />}
          <div className="ml-auto flex items-center gap-2.5">
            <button
              type="button"
              onClick={requestClose}
              disabled={busy}
              className="h-10 rounded-[10px] border border-line-strong bg-surface-solid px-4 text-sm font-semibold text-ink transition-colors duration-150 hover:bg-page disabled:opacity-50"
            >
              {t("Отмена")}
            </button>
            <button
              type="button"
              onClick={() => void submit()}
              disabled={!valid || busy}
              aria-keyshortcuts={mac ? "Meta+Enter" : "Control+Enter"}
              className="inline-flex h-10 items-center gap-2 rounded-[10px] bg-accent-fill px-4 text-sm font-semibold text-white transition-[filter,opacity] duration-150 hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}
              {submitLabel}
              <kbd className="rounded-md bg-white/20 px-1.5 py-0.5 font-sans text-[11px] font-semibold">{mac ? "⌘↵" : "Ctrl ↵"}</kbd>
            </button>
          </div>
        </footer>
      </div>
    </div>
  );

  // ---------- Телефон: шторка на всю высоту ----------
  const row = (key: Inner, icon: LucideIcon, label: string, value: React.ReactNode, tone: "ink" | "subtle" | "link" = "ink", disabled = false) => {
    const Icon = icon;
    return (
      <button
        type="button"
        disabled={disabled}
        onClick={() => setInner(key)}
        className="flex min-h-14 w-full items-center gap-3 px-3.5 py-[13px] text-left transition-colors duration-150 active:bg-surface-2 disabled:opacity-60"
      >
        <span className="grid size-[30px] shrink-0 place-items-center rounded-lg bg-surface-2 text-ink-2">
          <Icon className="size-4" aria-hidden />
        </span>
        <span className="flex-1 text-[15px] text-ink-2">{label}</span>
        <span className={`min-w-0 max-w-[55%] truncate text-right text-[15px] font-semibold ${tone === "subtle" ? "text-subtle" : tone === "link" ? "text-link" : "text-ink"}`}>{value}</span>
        {!disabled && <ChevronRight className="size-4 shrink-0 text-subtle" aria-hidden />}
      </button>
    );
  };
  const option = (selected: boolean, label: React.ReactNode, onPick: () => void, key: string, muted = false) => (
    <li key={key}>
      <button type="button" onClick={onPick} className="flex min-h-12 w-full items-center gap-3 rounded-lg px-3 text-left text-base active:bg-surface-2">
        <span className={`min-w-0 flex-1 truncate ${muted ? "text-subtle" : "text-ink"}`}>{label}</span>
        {selected && <Check className="size-5 shrink-0 text-link" aria-hidden />}
      </button>
    </li>
  );
  const innerTitle: Record<Inner, string> = { department: t("Отдел"), project: t("Проект"), assignee: t("Исполнитель"), start: t("Дата начала"), due: t("Срок") };
  const innerView = inner && (
    <div className="task-form-inner absolute inset-0 z-20 flex flex-col rounded-t-3xl bg-surface-solid">
      <div className="grid shrink-0 grid-cols-[1fr_auto_1fr] items-center border-b border-line px-2 pb-3 pt-4">
        <button type="button" onClick={() => setInner(null)} className="inline-flex h-11 items-center gap-1 justify-self-start rounded-lg px-2 text-[15px] text-link">
          <ChevronLeft className="size-5" aria-hidden />
          {t("Назад")}
        </button>
        <h3 className="text-base font-bold text-ink">{innerTitle[inner]}</h3>
        {inner === "start" || inner === "due" ? (
          <button type="button" onClick={() => setInner(null)} className="h-11 justify-self-end rounded-lg px-3 text-[15px] font-semibold text-link">
            {t("Готово")}
          </button>
        ) : (
          <span />
        )}
      </div>
      <div className="flex-1 overflow-y-auto overscroll-contain p-4">
        {inner === "department" && (
          <ul>
            {creatable.map((d) =>
              option(d.code === draft.department, d.name, () => (setDraft((x) => ({ ...x, department: d.code, projectId: x.department === d.code ? x.projectId : "", assigneeId: x.department === d.code ? x.assigneeId : "" })), setInner(null)), d.code),
            )}
          </ul>
        )}
        {inner === "project" && (
          <ul>
            {option(!draft.projectId, t("Без проекта"), () => (set("projectId", ""), setInner(null)), "none", true)}
            {departmentProjects.map((p) => option(p.id === draft.projectId, p.name, () => (set("projectId", p.id), setInner(null)), p.id))}
          </ul>
        )}
        {inner === "assignee" && (
          <AssigneePicker
            inline
            look="form"
            value={draft.assigneeId}
            onChange={(id) => (set("assigneeId", id), setInner(null))}
            assignees={assignees}
            current={task?.assigneeId ? { id: task.assigneeId, name: task.assigneeName } : null}
          />
        )}
        {inner === "start" && (
          <div className="flex flex-col gap-4">
            <DateField large value={draft.startDate} text={dayText(draft.startDate)} placeholder={t("Не указана")} ariaLabel={t("Дата начала")} icon={CalendarDays} onChange={(v) => set("startDate", v)} onClear={() => set("startDate", "")} />
            <Chips
              scroll
              options={[
                { key: "today", label: t("Сегодня") },
                { key: "tomorrow", label: t("Завтра") },
                { key: "none", label: t("Без даты") },
              ]}
              active={draft.startDate === today ? "today" : draft.startDate === addDays(today, 1) ? "tomorrow" : !draft.startDate ? "none" : null}
              onPick={(key) => set("startDate", key === "today" ? today : key === "tomorrow" ? addDays(today, 1) : "")}
            />
          </div>
        )}
        {inner === "due" && (
          <div className="flex flex-col gap-4">
            <div className="grid grid-cols-[minmax(0,1fr)_112px] gap-2">
              <DateField large value={draft.dueDate} text={dayText(draft.dueDate)} placeholder={t("Выберите дату")} ariaLabel={t("Дата срока")} icon={CalendarCheck} onChange={setDueDate} min={dueMin} invalid={!!errors.due} />
              <TimeField large value={draft.dueTime} onChange={(v) => set("dueTime", v)} disabled={!draft.dueDate} />
            </div>
            <FieldError text={errors.due} />
            <Chips scroll options={dueOptions} active={dueActive} onPick={pickDue} />
          </div>
        )}
      </div>
    </div>
  );

  const mobileView = (
    <div className="tasks-ui task-form-fade fixed inset-0 z-50 bg-black/70" onClick={(e) => e.target === e.currentTarget && requestClose()}>
      <div
        ref={panel}
        role="dialog"
        aria-modal="true"
        aria-labelledby="task-form-title"
        onKeyDown={onKeyDown}
        style={{ bottom: keyboard, transform: dragY ? `translateY(${dragY}px)` : undefined }}
        className="task-form-sheet absolute inset-x-0 top-11 flex flex-col overflow-hidden rounded-t-3xl bg-surface-solid text-ink"
      >
        <div
          className="shrink-0 touch-none"
          onTouchStart={(e) => (drag.current = e.touches[0].clientY)}
          onTouchMove={(e) => drag.current !== null && setDragY(Math.max(0, e.touches[0].clientY - drag.current))}
          onTouchEnd={() => {
            const moved = dragY;
            drag.current = null;
            if (moved > 110 && requestClose()) return;
            setDragY(0);
          }}
        >
          <div className="flex justify-center pb-1 pt-2" aria-hidden>
            <span className="h-[5px] w-10 rounded-full bg-line-strong" />
          </div>
          <div className="grid grid-cols-[1fr_auto_1fr] items-center border-b border-line px-2 pb-3">
            <button type="button" onClick={requestClose} disabled={busy} className="h-11 justify-self-start rounded-lg px-2 text-[15px] text-link disabled:opacity-50">
              {t("Отмена")}
            </button>
            <h2 id="task-form-title" className="text-base font-bold text-ink">
              {editing ? t("Изменить задачу") : t("Новая задача")}
            </h2>
            <button type="button" onClick={() => void submit()} disabled={busy} className={`h-11 justify-self-end rounded-lg px-2 text-[15px] ${valid ? "font-semibold text-link" : "text-subtle"}`}>
              {t("Сохранить")}
            </button>
          </div>
        </div>

        <div className="flex flex-1 flex-col gap-3.5 overflow-y-auto overscroll-contain p-4 [&>*]:shrink-0">
          {draftBanner}
          {subtitle && <p className="-mt-1 truncate text-[13px] text-ink-3">{subtitle}</p>}
          <div className={`rounded-[14px] border bg-page px-3.5 py-3 transition-colors duration-150 focus-within:border-accent focus-within:ring-[3px] focus-within:ring-accent/20 ${titleError ? "border-bad" : "border-line-strong"}`}>
            <textarea
              ref={titleArea}
              rows={1}
              autoFocus
              value={draft.title}
              aria-label={t("Название")}
              aria-required="true"
              aria-invalid={!!titleError || undefined}
              onChange={(e) => {
                set("title", e.target.value);
                e.target.style.height = "auto";
                e.target.style.height = `${e.target.scrollHeight}px`;
                if (titleError) setErrors((x) => ({ ...x, title: undefined }));
              }}
              onKeyDown={(e) => e.key === "Enter" && !e.metaKey && !e.ctrlKey && e.preventDefault()}
              placeholder={t("Что нужно сделать?")}
              className="block w-full resize-none bg-transparent text-lg font-semibold leading-6 text-ink outline-none placeholder:text-subtle"
            />
            {titleLength > limit - 20 && <div className="mt-1 text-right">{counter}</div>}
            {descOpen ? (
              <textarea
                ref={descArea}
                value={draft.description}
                maxLength={4000}
                aria-label={t("Описание")}
                onChange={(e) => set("description", e.target.value)}
                placeholder={t("Подробности, ожидаемый результат, ссылки…")}
                className="mt-2 block max-h-[240px] min-h-[72px] w-full resize-none border-t border-line bg-transparent pt-2 text-base leading-6 text-ink outline-none placeholder:text-subtle"
              />
            ) : (
              <button
                type="button"
                onClick={() => {
                  setDescOpen(true);
                  requestAnimationFrame(() => descArea.current?.focus());
                }}
                className="mt-1 block w-full py-1 text-left text-[13px] text-subtle"
              >
                {t("Добавить описание — подробности, результат, ссылки")}
              </button>
            )}
          </div>
          <FieldError text={titleError} />

          <div className="divide-y divide-line overflow-hidden rounded-[14px] border border-line-strong bg-page">
            {row("department", Building2, t("Отдел"), deptName(draft.department), "ink", editing)}
            {row("project", Folder, t("Проект"), projectName ?? t("Без проекта"), projectName ? "ink" : "subtle")}
            {row(
              "assignee",
              UserRound,
              t("Исполнитель"),
              assigneeName ? (
                <span className="inline-flex items-center gap-2">
                  <UserAvatar name={assigneeName} size={22} />
                  <span className="truncate">{shortName(assigneeName)}</span>
                </span>
              ) : (
                t("Любой сотрудник")
              ),
              assigneeName ? "ink" : "subtle",
            )}
            {row("start", CalendarDays, t("Начало"), draft.startDate ? dayText(draft.startDate, true) : t("Не указана"), draft.startDate ? "ink" : "subtle")}
            {row(
              "due",
              CalendarCheck,
              t("Срок"),
              draft.dueDate ? `${dayText(draft.dueDate, true)}${draft.dueTime ? `, ${draft.dueTime}` : ""}` : t("Без срока"),
              draft.dueDate ? "link" : "subtle",
            )}
          </div>
          <FieldError text={errors.due} />

          <Chips scroll options={dueOptions} active={dueActive} onPick={pickDue} />

          <div>
            <p id="task-priority-mobile" className="mb-2 text-[13px] font-semibold text-ink-2">
              {t("Приоритет")}
            </p>
            <PrioritySegments stacked value={draft.priority} onChange={(p) => set("priority", p)} labelId="task-priority-mobile" />
          </div>

          {!editing && (
            <div className="grid grid-cols-2 gap-2">
              <ExtraButton large icon={ListChecks} label={t("Чек-лист")} active={checkOpen} onClick={() => setCheckOpen(true)} />
              <ExtraButton large icon={Paperclip} label={t("Файл")} active={files.length > 0} onClick={() => fileInput.current?.click()} />
            </div>
          )}
          {extras}
          {fileField}
          {pushHint(true)}
        </div>

        {serverPlaque}
        <div className="shrink-0 border-t border-line px-4 pb-[calc(16px+env(safe-area-inset-bottom))] pt-3">
          <button
            type="button"
            onClick={() => void submit()}
            disabled={busy}
            aria-disabled={!valid}
            className={`flex h-[52px] w-full items-center justify-center gap-2.5 rounded-[14px] bg-accent-fill text-[15px] font-bold text-white transition-[filter,opacity] duration-150 active:brightness-110 ${valid ? "" : "opacity-50"}`}
          >
            {busy ? <Loader2 className="size-5 animate-spin" aria-hidden /> : <Send className="size-5" aria-hidden />}
            {submitLabel}
          </button>
        </div>
        {innerView}
      </div>
    </div>
  );

  if (typeof document === "undefined") return null;
  const host = document.querySelector<HTMLElement>("[data-app-shell]") ?? document.body;
  return createPortal(desktop ? desktopView : mobileView, host);
}

/** Поле-список с иконкой слева (отдел, проект). */
function SelectBox({ icon: Icon, children }: { icon: LucideIcon; children: React.ReactNode }) {
  return (
    <div className="relative">
      <Icon className="pointer-events-none absolute left-3.5 top-1/2 z-10 size-4 -translate-y-1/2 text-ink-3" aria-hidden />
      {children}
    </div>
  );
}

/** Кнопка панели Markdown 28×28. */
function ToolButton({ icon: Icon, label, onClick }: { icon: LucideIcon; label: string; onClick: () => void }) {
  return (
    <button type="button" onClick={onClick} aria-label={label} title={label} className="grid size-7 place-items-center rounded-md text-ink-3 transition-colors duration-150 hover:bg-surface-2 hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent">
      <Icon className="size-4" aria-hidden />
    </button>
  );
}

/** Дополнительно: чек-лист, файлы — кнопка-ссылка с рамкой. */
function ExtraButton({ icon: Icon, label, onClick, active, large = false }: { icon: LucideIcon; label: string; onClick: () => void; active: boolean; large?: boolean }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`inline-flex items-center justify-center gap-2 rounded-[10px] border font-medium transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent ${
        large ? "h-12 text-[15px]" : "h-9 px-3 text-xs"
      } ${active ? "border-accent/35 text-link" : "border-line-strong text-ink-2 hover:text-ink"}`}
    >
      <Icon className="size-4" aria-hidden />
      {label}
    </button>
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
