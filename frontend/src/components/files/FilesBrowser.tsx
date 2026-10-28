"use client";

import Link from "next/link";
import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import {
  ArrowDown,
  ArrowUp,
  ChevronRight,
  Download,
  Eye,
  FolderInput,
  FolderPlus,
  History,
  Loader2,
  Lock,
  LockOpen,
  MoreHorizontal,
  Pencil,
  RotateCcw,
  Trash2,
  Upload,
} from "lucide-react";
import { Button, EmptyState, ProgressBar, SearchInput } from "@/components/ui";
import { useT } from "@/i18n/client";
import { fileSize, filesLabel, foldersLabel } from "@/lib/format";
import {
  accessLabel,
  downloadUrl,
  filesApi,
  modifiedLabel,
  sharedAccessLabel,
  spaceHref,
  typeFilters,
  typeGroup,
  uploadFiles,
  type FileRow,
  type FolderRow,
  type FolderView,
  type SearchRow,
  type TrashRow,
  type TypeFilter,
} from "@/lib/files";
import { FileIcon, FolderIcon, typeShort } from "./file-meta";
import { MoveDialog, NameDialog, PreviewDialog, VersionsDialog } from "./FileDialogs";
import { SharedAccessButton } from "./SharedAccess";
import { PersonalLinkButton, WindowsConnectButton } from "./WindowsConnect";

type SortKey = "name" | "type" | "size" | "modified";
type Dialog =
  | { kind: "newFolder" }
  | { kind: "renameFile"; file: FileRow }
  | { kind: "renameFolder"; folder: FolderRow }
  | { kind: "moveFile"; file: FileRow }
  | { kind: "moveFolder"; folder: FolderRow }
  | { kind: "preview"; file: FileRow }
  | { kind: "versions"; file: FileRow };

const REFRESH_MS = 15_000;

/**
 * Папка отдела или общая папка: те же файлы, что в подключённой папке Windows. Список перечитывается раз в 15 секунд — файлы, положенные
 * через Проводник, появляются сами, без «Обновить». Файл, открытый в Word или Excel, помечен замком: пока он открыт, его не перезапишут.
 */
export function FilesBrowser({ initial }: { initial: FolderView }) {
  const t = useT();
  const [view, setView] = useState(initial);
  const [dialog, setDialog] = useState<Dialog | null>(null);
  const [query, setQuery] = useState("");
  const [found, setFound] = useState<SearchRow[] | null>(null);
  const [type, setType] = useState<TypeFilter>("all");
  const [sort, setSort] = useState<{ key: SortKey; desc: boolean }>({ key: "name", desc: false });
  const [trash, setTrash] = useState<TrashRow[] | null>(null);
  const [upload, setUpload] = useState<{ names: string; loaded: number; total: number } | null>(null);
  const [dragging, setDragging] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const input = useRef<HTMLInputElement>(null);

  const code = view.department.code;
  const shared = !!view.department.shared;
  const manage = view.department.access === "manage";
  const rootId = view.path[0]?.id ?? view.id;
  const hrefOf = (folderId: string) => spaceHref(view.department, folderId === rootId ? undefined : folderId);
  const loadTrash = () => (shared ? filesApi.sharedTrash(view.department.id) : filesApi.trash(code));

  const reload = useCallback(async () => {
    try {
      setView(await filesApi.folder(view.id));
    } catch (e) {
      setError((e as Error).message);
    }
  }, [view.id]);

  useEffect(() => setView(initial), [initial]);

  useEffect(() => {
    if (dialog || upload || trash) return;
    const timer = window.setInterval(() => document.visibilityState === "visible" && reload(), REFRESH_MS);
    return () => window.clearInterval(timer);
  }, [dialog, upload, trash, reload]);

  useEffect(() => {
    const q = query.trim();
    if (q.length < 2) return setFound(null);
    const timer = window.setTimeout(
      () => (shared ? filesApi.search(q, undefined, view.department.id) : filesApi.search(q, code)).then(setFound, (e: Error) => setError(e.message)),
      300,
    );
    return () => window.clearTimeout(timer);
  }, [query, code, shared, view.department.id]);

  const act = async (action: () => Promise<unknown>) => {
    setError(null);
    try {
      await action();
      await reload();
    } catch (e) {
      setError((e as Error).message);
    }
  };

  const startUpload = async (list: FileList | File[]) => {
    const files = Array.from(list);
    if (files.length === 0 || !view.canWrite) return;
    setError(null);
    const names = files.length === 1 ? files[0].name : `${files.length} ${t(files.length < 5 ? "файла" : "файлов")}`;
    setUpload({ names, loaded: 0, total: files.reduce((s, f) => s + f.size, 0) });
    try {
      await uploadFiles(view.id, files, (loaded, total) => setUpload({ names, loaded, total }), t);
      await reload();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setUpload(null);
    }
  };

  const toggleSort = (key: SortKey) => setSort((s) => (s.key === key ? { key, desc: !s.desc } : { key, desc: key === "modified" }));

  const files = useMemo(() => {
    const q = query.trim().toLowerCase();
    const list = view.files.filter((f) => !f.hidden && (type === "all" || typeGroup(f.extension) === type) && (!q || f.name.toLowerCase().includes(q)));
    const value = (f: FileRow) =>
      sort.key === "size" ? f.sizeBytes : sort.key === "modified" ? f.updatedAt : sort.key === "type" ? f.extension : f.name.toLowerCase();
    return list.sort((a, b) => {
      const [x, y] = [value(a), value(b)];
      const cmp = typeof x === "number" && typeof y === "number" ? x - y : String(x).localeCompare(String(y), "ru");
      return sort.desc ? -cmp : cmp;
    });
  }, [view.files, query, type, sort]);

  const folders = view.folders.filter((f) => !query.trim() || f.name.toLowerCase().includes(query.trim().toLowerCase()));

  return (
    <div
      onDragOver={(e) => {
        if (!view.canWrite || !e.dataTransfer.types.includes("Files")) return;
        e.preventDefault();
        setDragging(true);
      }}
      onDragLeave={(e) => e.currentTarget === e.target && setDragging(false)}
      onDrop={(e) => {
        e.preventDefault();
        setDragging(false);
        startUpload(e.dataTransfer.files);
      }}
      className={`relative rounded-xl transition-colors ${dragging ? "bg-accent/5 ring-2 ring-accent ring-offset-4" : ""}`}
    >
      <nav className="mb-4 flex flex-wrap items-center gap-1 text-sm" aria-label={t("Путь к папке")}>
        <Link href="/base" className="text-ink-3 hover:text-ink">{t("Файлы")}</Link>
        {view.path.map((p, i) => (
          <span key={p.id} className="flex items-center gap-1">
            <ChevronRight className="size-3.5 text-ink-3" />
            {i === view.path.length - 1 ? <span className="font-medium">{p.name}</span> : <Link href={hrefOf(p.id)} className="text-ink-2 hover:text-ink">{p.name}</Link>}
          </span>
        ))}
        <span className="ml-2 rounded-full bg-muted px-2 py-0.5 text-xs text-ink-2">{t((shared ? sharedAccessLabel : accessLabel)[view.department.access])}</span>
      </nav>

      <div className="flex flex-wrap items-center gap-2">
        <SearchInput
          className="min-w-[220px] flex-1 sm:max-w-sm"
          placeholder={shared ? t("Поиск в папке «{name}»", { name: view.department.name }) : t("Поиск в отделе «{name}»", { name: t(view.department.name) })}
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />
        <div className="ml-auto flex flex-wrap gap-2">
          <Button onClick={() => (trash ? setTrash(null) : loadTrash().then(setTrash, (e: Error) => setError(e.message)))}>
            <Trash2 className="size-4" />
            {trash ? t("К файлам") : t("Корзина")}
          </Button>
          {view.canWrite && (
            <>
              <Button onClick={() => setDialog({ kind: "newFolder" })}>
                <FolderPlus className="size-4" />
                {t("Новая папка")}
              </Button>
              <Button variant="primary" onClick={() => input.current?.click()} disabled={!!upload}>
                <Upload className="size-4" />
                {t("Загрузить")}
              </Button>
              <input ref={input} type="file" multiple hidden onChange={(e) => (startUpload(e.target.files ?? []), (e.target.value = ""))} />
            </>
          )}
          {shared ? (
            <>
              {manage && <SharedAccessButton space={view.department} />}
              <PersonalLinkButton />
            </>
          ) : (
            <WindowsConnectButton department={view.department} />
          )}
        </div>
      </div>

      {upload && (
        <div className="mt-4 rounded-lg border border-line p-3 text-sm">
          <div className="mb-2 flex items-center gap-2">
            <Loader2 className="size-4 animate-spin text-accent" />
            <span className="flex-1 truncate">{t("Загрузка: {names}", { names: upload.names })}</span>
            <span className="text-xs text-ink-3">{t("{loaded} из {total}", { loaded: fileSize(upload.loaded), total: fileSize(upload.total) })}</span>
          </div>
          <ProgressBar value={upload.total ? (upload.loaded / upload.total) * 100 : 0} />
        </div>
      )}
      {error && (
        <p className="mt-4 rounded-lg bg-bad/10 px-3 py-2 text-sm text-bad" role="alert">
          {error}
          <button type="button" className="ml-3 underline" onClick={() => setError(null)}>{t("Скрыть")}</button>
        </p>
      )}

      {trash ? (
        <TrashList rows={trash} canWrite={view.canWrite} onRestore={(row) => act(async () => {
          await (row.isFolder ? filesApi.restoreFolder(row.id) : filesApi.restoreFile(row.id));
          setTrash(await loadTrash());
        })} />
      ) : found ? (
        <SearchResults rows={found} onOpen={(file) => setDialog({ kind: "preview", file })} />
      ) : (
        <>
          <div className="mt-5 flex flex-wrap items-center gap-1.5">
            {typeFilters.map((filter) => (
              <button
                key={filter.key}
                type="button"
                onClick={() => setType(filter.key)}
                className={`rounded-full px-3 py-1 text-xs ${type === filter.key ? "bg-accent text-white" : "bg-muted text-ink-2 hover:text-ink"}`}
              >
                {t(filter.label)}
              </button>
            ))}
            <span className="ml-auto text-xs text-ink-3">
              {foldersLabel(view.folders.length, t)} · {filesLabel(view.files.filter((f) => !f.hidden).length, t)}
              {view.canWrite && ` · ${t("перетащите файлы сюда, чтобы загрузить")}`}
            </span>
          </div>

          <div className="mt-3 overflow-x-auto">
            <div className="min-w-[640px]">
              <div className="grid grid-cols-[minmax(0,2.4fr)_90px_90px_minmax(0,1.4fr)_40px] gap-3 border-b border-line py-2 text-[11px] font-semibold uppercase tracking-wide text-ink-3">
                <SortHeader label={t("файл::Имя")} k="name" sort={sort} onSort={toggleSort} />
                <SortHeader label={t("Тип")} k="type" sort={sort} onSort={toggleSort} />
                <SortHeader label={t("Размер")} k="size" sort={sort} onSort={toggleSort} />
                <SortHeader label={t("Изменён")} k="modified" sort={sort} onSort={toggleSort} />
                <span />
              </div>
              {type === "all" && folders.map((folder) => (
                <div key={folder.id} className="grid grid-cols-[minmax(0,2.4fr)_90px_90px_minmax(0,1.4fr)_40px] items-center gap-3 border-b border-line py-3 text-sm">
                  <Link href={hrefOf(folder.id)} className="flex min-w-0 items-center gap-3 hover:text-accent">
                    <FolderIcon />
                    <span className="truncate font-medium">{folder.name}</span>
                  </Link>
                  <span className="text-xs text-ink-2">{t("Папка")}</span>
                  <span className="text-xs text-ink-2">{folder.files + folder.folders > 0 ? t("{n} эл.", { n: folder.files + folder.folders }) : t("пусто")}</span>
                  <span className="text-xs text-ink-2">{modifiedLabel(folder.updatedAt, t)}</span>
                  {view.canWrite ? (
                    <RowMenu
                      items={[
                        { label: t("Переименовать"), icon: Pencil, onClick: () => setDialog({ kind: "renameFolder", folder }) },
                        { label: t("Перенести"), icon: FolderInput, onClick: () => setDialog({ kind: "moveFolder", folder }) },
                        { label: t("В корзину"), icon: Trash2, danger: true, onClick: () => window.confirm(t("Папку «{name}» со всем содержимым — в корзину?", { name: folder.name })) && act(() => filesApi.deleteFolder(folder.id)) },
                      ]}
                    />
                  ) : <span />}
                </div>
              ))}
              {files.map((file) => (
                <div key={file.id} className="grid grid-cols-[minmax(0,2.4fr)_90px_90px_minmax(0,1.4fr)_40px] items-center gap-3 border-b border-line py-3 text-sm">
                  <button type="button" onClick={() => setDialog({ kind: "preview", file })} className="flex min-w-0 items-center gap-3 text-left hover:text-accent">
                    <FileIcon extension={file.extension} />
                    <span className="truncate">{file.name}</span>
                    {file.version > 1 && <span className="shrink-0 rounded bg-muted px-1.5 text-[10px] text-ink-3">v{file.version}</span>}
                    {file.lock && (
                      <span
                        className="inline-flex shrink-0 items-center gap-1 rounded-full bg-warn/15 px-2 py-0.5 text-[11px] text-ink-2"
                        title={t("Открыт для редактирования до {time} — пока он открыт, другие видят его только для чтения", { time: modifiedLabel(file.lock.until, t) })}
                      >
                        <Lock className="size-3" />
                        {file.lock.owner ? t("открыт: {owner}", { owner: file.lock.owner }) : t("открыт для редактирования")}
                      </span>
                    )}
                  </button>
                  <span className="text-xs text-ink-2">{t(typeShort(file.extension))}</span>
                  <span className="text-xs text-ink-2">{fileSize(file.sizeBytes)}</span>
                  <span className="truncate text-xs text-ink-2" title={file.updatedBy?.name}>
                    {modifiedLabel(file.updatedAt, t)}
                    {file.updatedBy && <span className="text-ink-3"> · {file.updatedBy.name}</span>}
                  </span>
                  <RowMenu
                    items={[
                      { label: t("Просмотр"), icon: Eye, onClick: () => setDialog({ kind: "preview", file }) },
                      { label: t("Скачать"), icon: Download, href: downloadUrl(file) },
                      { label: t("Версии"), icon: History, onClick: () => setDialog({ kind: "versions", file }) },
                      ...(view.canWrite
                        ? [
                            { label: t("Переименовать"), icon: Pencil, onClick: () => setDialog({ kind: "renameFile", file }) },
                            { label: t("Перенести"), icon: FolderInput, onClick: () => setDialog({ kind: "moveFile", file }) },
                            { label: t("В корзину"), icon: Trash2, danger: true, onClick: () => window.confirm(t("«{name}» — в корзину?", { name: file.name })) && act(() => filesApi.deleteFile(file.id)) },
                          ]
                        : []),
                      ...(file.lock && manage
                        ? [
                            {
                              label: t("Снять блокировку"),
                              icon: LockOpen,
                              onClick: () =>
                                window.confirm(t("Снять блокировку «{name}»? Это нужно, если Word или Excel завис и файл не освободился. Если файл ещё открыт, его правки могут не сохраниться.", { name: file.name })) &&
                                act(() => filesApi.unlock(file.id)),
                            },
                          ]
                        : []),
                    ]}
                  />
                </div>
              ))}
            </div>
          </div>
          {folders.length === 0 && files.length === 0 && (
            <div className="mt-4">
              <EmptyState>{query || type !== "all" ? t("Ничего не найдено") : view.canWrite ? t("Папка пустая — загрузите файлы или перетащите их сюда") : t("Папка пустая")}</EmptyState>
            </div>
          )}
        </>
      )}

      {dialog?.kind === "newFolder" && (
        <NameDialog title={t("Новая папка")} action={t("Создать")} onClose={() => setDialog(null)} onSubmit={(name) => act(() => filesApi.createFolder(view.id, name)).then(() => undefined)} />
      )}
      {dialog?.kind === "renameFile" && (
        <NameDialog title={t("Переименовать файл")} initial={dialog.file.name} action={t("Сохранить")} onClose={() => setDialog(null)}
          onSubmit={async (name) => { await filesApi.renameFile(dialog.file.id, name); await reload(); }} />
      )}
      {dialog?.kind === "renameFolder" && (
        <NameDialog title={t("Переименовать папку")} initial={dialog.folder.name} action={t("Сохранить")} onClose={() => setDialog(null)}
          onSubmit={async (name) => { await filesApi.renameFolder(dialog.folder.id, name); await reload(); }} />
      )}
      {dialog?.kind === "moveFile" && (
        <MoveDialog rootId={rootId} itemName={dialog.file.name} onClose={() => setDialog(null)}
          onMove={async (target) => { await filesApi.moveFile(dialog.file.id, target); await reload(); }} />
      )}
      {dialog?.kind === "moveFolder" && (
        <MoveDialog rootId={rootId} itemName={dialog.folder.name} excludeFolderId={dialog.folder.id} onClose={() => setDialog(null)}
          onMove={async (target) => { await filesApi.moveFolder(dialog.folder.id, target); await reload(); }} />
      )}
      {dialog?.kind === "preview" && <PreviewDialog file={dialog.file} onClose={() => setDialog(null)} />}
      {dialog?.kind === "versions" && <VersionsDialog file={dialog.file} onClose={() => setDialog(null)} />}
    </div>
  );
}

function SortHeader({ label, k, sort, onSort }: { label: string; k: SortKey; sort: { key: SortKey; desc: boolean }; onSort: (k: SortKey) => void }) {
  const Icon = sort.desc ? ArrowDown : ArrowUp;
  return (
    <button type="button" onClick={() => onSort(k)} className="flex items-center gap-1 text-left uppercase hover:text-ink">
      {label}
      {sort.key === k && <Icon className="size-3" />}
    </button>
  );
}

type MenuItem = { label: string; icon: typeof Eye; onClick?: () => unknown; href?: string; danger?: boolean };

/**
 * Меню действий строки. Рисуется в document.body с фиксированной позицией от кнопки: внутри таблицы с горизонтальной прокруткой
 * (overflow-x-auto) оно обрезалось бы её краем. Снизу не хватает места — открывается вверх; прокрутка и смена размера окна закрывают его.
 */
function RowMenu({ items }: { items: MenuItem[] }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const [position, setPosition] = useState<{ top: number; right: number } | null>(null);
  const button = useRef<HTMLButtonElement>(null);
  const menu = useRef<HTMLDivElement>(null);

  useLayoutEffect(() => {
    if (!open || !button.current || !menu.current) return;
    const rect = button.current.getBoundingClientRect();
    const height = menu.current.offsetHeight;
    const below = window.innerHeight - rect.bottom;
    const top = below >= height + 8 || rect.top < height + 8 ? rect.bottom + 4 : rect.top - height - 4;
    setPosition({ top: Math.max(8, top), right: Math.max(8, window.innerWidth - rect.right) });
  }, [open]);

  useEffect(() => {
    if (!open) return;
    const close = (e: Event) => {
      if (e.type === "mousedown" && (menu.current?.contains(e.target as Node) || button.current?.contains(e.target as Node))) return;
      setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    document.addEventListener("mousedown", close);
    document.addEventListener("keydown", onKey);
    window.addEventListener("scroll", close, true);
    window.addEventListener("resize", close);
    return () => {
      document.removeEventListener("mousedown", close);
      document.removeEventListener("keydown", onKey);
      window.removeEventListener("scroll", close, true);
      window.removeEventListener("resize", close);
    };
  }, [open]);

  const toggle = () => {
    setPosition(null);
    setOpen((o) => !o);
  };

  return (
    <div className="justify-self-end">
      <button ref={button} type="button" aria-label={t("Действия")} aria-expanded={open} onClick={toggle} className="rounded-md p-1.5 text-ink-3 hover:bg-muted hover:text-ink">
        <MoreHorizontal className="size-4" />
      </button>
      {open && createPortal(
        <div
          ref={menu}
          role="menu"
          style={{ top: position?.top ?? 0, right: position?.right ?? 0, visibility: position ? "visible" : "hidden" }}
          className="fixed z-50 w-48 rounded-lg border border-line bg-surface-solid py-1 shadow-lg"
        >
          {items.map((item) => {
            const content = (
              <>
                <item.icon className="size-4" />
                {item.label}
              </>
            );
            const cls = `flex w-full items-center gap-2 px-3 py-2 text-left text-sm hover:bg-muted ${item.danger ? "text-bad" : ""}`;
            return item.href ? (
              <a key={item.label} href={item.href} className={cls} onClick={() => setOpen(false)}>{content}</a>
            ) : (
              <button key={item.label} type="button" role="menuitem" className={cls} onClick={() => (setOpen(false), item.onClick?.())}>{content}</button>
            );
          })}
        </div>,
        document.body,
      )}
    </div>
  );
}

function TrashList({ rows, canWrite, onRestore }: { rows: TrashRow[]; canWrite: boolean; onRestore: (row: TrashRow) => void }) {
  const t = useT();
  if (rows.length === 0) return <div className="mt-4"><EmptyState>{t("Корзина пустая")}</EmptyState></div>;
  return (
    <div className="mt-4 divide-y divide-line">
      {rows.map((row) => (
        <div key={row.id} className="flex items-center gap-3 py-3 text-sm">
          {row.isFolder ? <FolderIcon /> : <FileIcon extension={row.name.split(".").pop()?.toLowerCase() ?? ""} />}
          <span className="min-w-0 flex-1">
            <span className="block truncate">{row.name}</span>
            <span className="block truncate text-xs text-ink-3">{row.folderPath} · {t("удалено {date}", { date: modifiedLabel(row.deletedAt, t) })}</span>
          </span>
          {canWrite && (
            <Button size="sm" onClick={() => onRestore(row)}>
              <RotateCcw className="size-3.5" />
              {t("Восстановить")}
            </Button>
          )}
        </div>
      ))}
    </div>
  );
}

function SearchResults({ rows, onOpen }: { rows: SearchRow[]; onOpen: (file: FileRow) => void }) {
  const t = useT();
  if (rows.length === 0) return <div className="mt-4"><EmptyState>{t("Ничего не найдено")}</EmptyState></div>;
  return (
    <div className="mt-4 divide-y divide-line">
      {rows.map(({ file, folderPath }) => (
        <button key={file.id} type="button" onClick={() => onOpen(file)} className="flex w-full items-center gap-3 py-3 text-left text-sm hover:text-accent">
          <FileIcon extension={file.extension} />
          <span className="min-w-0 flex-1">
            <span className="block truncate">{file.name}</span>
            <span className="block truncate text-xs text-ink-3">{folderPath} · {modifiedLabel(file.updatedAt, t)}{file.updatedBy && ` · ${file.updatedBy.name}`}</span>
          </span>
          <span className="text-xs text-ink-3">{fileSize(file.sizeBytes)}</span>
        </button>
      ))}
    </div>
  );
}
