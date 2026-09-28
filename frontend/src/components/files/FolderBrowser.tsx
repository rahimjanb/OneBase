"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import {
  ArrowLeft,
  ArrowRight,
  ArrowUp,
  ArrowUpDown,
  ChevronRight,
  Ellipsis,
  LayoutGrid,
  Plus,
  Share2,
} from "lucide-react";
import { Button, Card, EmptyState, SearchInput } from "@/components/ui";
import type { Crumb } from "@/components/shell/PageHeader";
import type { Department, FileEntry, Folder } from "@/lib/demo-data";
import { fileSize, filesLabel, foldersLabel } from "@/lib/format";
import { FileIcon, kindLong } from "./file-meta";
import { FolderCard } from "./FolderCard";
import { UploadButton, UploadToast, useUpload } from "./upload";

const columns =
  "grid-cols-[minmax(0,1fr)_72px_40px] md:grid-cols-[minmax(0,2fr)_minmax(0,1fr)_minmax(0,1.1fr)_80px_40px]";

type SortMode = "recent" | "asc" | "desc";
const sortModes: SortMode[] = ["recent", "asc", "desc"];
const sortLabels: Record<SortMode, string> = { recent: "новые", asc: "А → Я", desc: "Я → А" };

type MenuItem ={ label: string; action?: "info" | "copy"; danger?: boolean; separatorBefore?: boolean };

const menuItems: MenuItem[] = [
  { label: "Открыть" },
  { label: "Скачать" },
  { label: "Копировать ссылку", action: "copy" },
  { label: "Переименовать", separatorBefore: true },
  { label: "Переместить" },
  { label: "Информация", action: "info" },
  { label: "Удалить", danger: true, separatorBefore: true },
];

export function FolderBrowser({
  department,
  folder,
  path,
  parentHref,
  subfolders,
  initialFiles,
}: {
  department: Department;
  folder: Folder;
  path: Crumb[];
  parentHref: string;
  subfolders: Folder[];
  initialFiles: FileEntry[];
}) {
  const router = useRouter();
  const [files, setFiles] = useState(initialFiles);
  const [query, setQuery] = useState("");
  const [sort, setSort] = useState<SortMode>("recent");
  const [selectedId, setSelectedId] = useState<string | null>(initialFiles[0]?.id ?? null);
  const [menuId, setMenuId] = useState<string | null>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const { upload, start } = useUpload(folder.id, (file) => {
    setFiles((list) => [file, ...list]);
    setSelectedId(file.id);
  });

  useEffect(() => {
    if (!menuId) return;
    const close = (e: MouseEvent) => {
      const target = e.target as Element;
      // Кнопку «⋯» обрабатывает её собственный onClick (переключение).
      if (!menuRef.current?.contains(target) && !target.closest("[data-menu-trigger]")) setMenuId(null);
    };
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setMenuId(null);
    document.addEventListener("mousedown", close);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", close);
      document.removeEventListener("keydown", onKey);
    };
  }, [menuId]);

  const q = query.trim().toLowerCase();
  const visibleFolders = subfolders.filter((f) => f.name.toLowerCase().includes(q));
  const visibleFiles = files.filter((f) => f.name.toLowerCase().includes(q));
  // "recent" — порядок из источника: новые сверху.
  if (sort !== "recent") {
    visibleFiles.sort((a, b) => (sort === "desc" ? -1 : 1) * a.name.localeCompare(b.name, "ru"));
  }
  const selected = files.find((f) => f.id === selectedId);

  function runMenu(item: MenuItem, file: FileEntry) {
    if (item.action === "info") setSelectedId(file.id);
    if (item.action === "copy") {
      void navigator.clipboard?.writeText(`${window.location.origin}${window.location.pathname}?file=${file.id}`);
    }
    setMenuId(null);
  }

  return (
    <>
      {/* Адресная строка и поиск */}
      <div className="flex flex-wrap gap-3">
        <div className="flex h-11 min-w-0 flex-1 items-center gap-1 rounded-lg border border-line bg-surface px-2">
          <button type="button" aria-label="Назад" onClick={() => router.back()} className="grid size-8 place-items-center rounded-md text-ink-3 hover:bg-muted hover:text-ink">
            <ArrowLeft className="size-4" />
          </button>
          <button type="button" aria-label="Вперёд" onClick={() => router.forward()} className="grid size-8 place-items-center rounded-md text-ink-3 hover:bg-muted hover:text-ink">
            <ArrowRight className="size-4" />
          </button>
          <Link href={parentHref} aria-label="Вверх" className="grid size-8 place-items-center rounded-md text-ink-3 hover:bg-muted hover:text-ink">
            <ArrowUp className="size-4" />
          </Link>
          <nav className="ml-3 flex min-w-0 items-center gap-1.5 overflow-x-auto text-sm text-ink">
            {path.map((crumb, i) => (
              <span key={`${crumb.label}-${i}`} className="flex shrink-0 items-center gap-1.5">
                {i > 0 && <ChevronRight className="size-3.5 text-ink-3" />}
                {crumb.href ? (
                  <Link href={crumb.href} className="hover:text-accent-strong">
                    {crumb.label}
                  </Link>
                ) : (
                  <span className="font-medium">{crumb.label}</span>
                )}
              </span>
            ))}
          </nav>
        </div>
        <SearchInput
          className="h-11 w-full sm:w-72 [&_input]:h-11"
          placeholder={`Поиск в папке «${folder.name}»`}
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />
      </div>

      {/* Панель действий */}
      <div className="mt-4 flex flex-wrap gap-2">
        <Button size="sm">
          <Plus className="size-3.5" />
          Новая папка
        </Button>
        <UploadButton onFile={start} variant="outline" size="sm" />
        <Button size="sm">
          <Share2 className="size-3.5" />
          Поделиться
        </Button>
        <Button size="sm" onClick={() => setSort((s) => sortModes[(sortModes.indexOf(s) + 1) % sortModes.length])}>
          <ArrowUpDown className="size-3.5" />
          Сортировка: {sortLabels[sort]}
        </Button>
        <Button size="sm">
          <LayoutGrid className="size-3.5" />
          Вид
        </Button>
        <Button size="sm" aria-label="Ещё">
          <Ellipsis className="size-3.5" />
        </Button>
      </div>

      <p className="mt-4 text-xs text-ink-3">
        {filesLabel(folder.filesCount)} · {foldersLabel(subfolders.length)} · обновлено {folder.updated}
      </p>

      {subfolders.length > 0 && (
        <>
          <h2 className="mt-6 text-base font-semibold">Папки</h2>
          <div className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {visibleFolders.map((f) => (
              <FolderCard
                key={f.id}
                folder={f}
                href={`/base/${department.code}/${f.id}`}
                departmentName={department.name}
                compact
              />
            ))}
          </div>
        </>
      )}

      <h2 className="mt-8 text-base font-semibold">Файлы</h2>
      {visibleFiles.length === 0 ? (
        <div className="mt-3">
          <EmptyState>{q ? "Файлы не найдены" : "В папке пока нет файлов"}</EmptyState>
        </div>
      ) : (
        <Card className="mt-3 px-2 pb-2">
          <div className={`grid ${columns} gap-4 border-b border-line px-3 py-3 text-[11px] font-semibold uppercase tracking-wide text-ink-3`}>
            <span>Имя</span>
            <span className="hidden md:block">Дата изменения</span>
            <span className="hidden md:block">Тип</span>
            <span>Размер</span>
            <span />
          </div>
          {visibleFiles.map((file) => {
            const isSelected = file.id === selectedId;
            return (
              <div
                key={file.id}
                onClick={() => setSelectedId(file.id)}
                onContextMenu={(e) => {
                  e.preventDefault();
                  setSelectedId(file.id);
                  setMenuId(file.id);
                }}
                className={`relative grid ${columns} cursor-default items-center gap-4 rounded-lg border-b border-line px-3 py-3.5 text-sm last:border-b-0 ${
                  isSelected ? "bg-accent-soft/70" : "hover:bg-muted"
                }`}
              >
                <span className="flex min-w-0 items-center gap-3">
                  <FileIcon kind={file.kind} />
                  <span className="truncate text-ink">{file.name}</span>
                </span>
                <span className="hidden text-xs text-ink-2 md:block">{file.modified}</span>
                <span className="hidden truncate text-xs text-ink-2 md:block">{kindLong(file.kind)}</span>
                <span className="text-xs text-ink-2">{fileSize(file.sizeBytes)}</span>
                <button
                  type="button"
                  data-menu-trigger
                  aria-label={`Действия с файлом ${file.name}`}
                  aria-haspopup="menu"
                  aria-expanded={menuId === file.id}
                  onClick={(e) => {
                    e.stopPropagation();
                    setSelectedId(file.id);
                    setMenuId(menuId === file.id ? null : file.id);
                  }}
                  className="grid size-8 place-items-center justify-self-end rounded-md text-ink-3 hover:bg-surface hover:text-ink"
                >
                  <Ellipsis className="size-4" />
                </button>

                {menuId === file.id && (
                  <div
                    ref={menuRef}
                    role="menu"
                    onClick={(e) => e.stopPropagation()}
                    className="absolute right-12 top-10 z-30 w-52 rounded-xl border border-line bg-surface py-1.5 shadow-xl"
                  >
                    {menuItems.map((item) => (
                      <div key={item.label}>
                        {item.separatorBefore && <div className="mx-3 my-1 border-t border-line" />}
                        <button
                          type="button"
                          role="menuitem"
                          onClick={() => runMenu(item, file)}
                          className={`block w-full px-4 py-2 text-left text-sm hover:bg-muted ${item.danger ? "text-bad" : "text-ink"}`}
                        >
                          {item.label}
                        </button>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            );
          })}
        </Card>
      )}

      {selected && (
        <>
          <p className="mt-6 text-xs text-ink-3">Выбран файл · {selected.name}</p>
          <Card className="mt-3 p-5">
            <h3 className="text-sm font-semibold text-ink">Сведения о файле</h3>
            <dl className="mt-3 flex flex-wrap gap-x-8 gap-y-2 text-xs">
              {[
                ["Загрузил", selected.uploadedBy],
                ["Отдел", department.name],
                ["Папка", path.slice(2).map((c) => c.label).join(" / ")],
                ["Тип", kindLong(selected.kind)],
                ["Размер", fileSize(selected.sizeBytes)],
                ["Загружен", selected.uploadedAt],
                ["Изменён", selected.modified],
                ["Версия", `v${selected.version}`],
              ].map(([label, value]) => (
                <div key={label} className="flex gap-1.5">
                  <dt className="text-ink-3">{label}:</dt>
                  <dd className="text-ink-2">{value}</dd>
                </div>
              ))}
            </dl>
          </Card>
        </>
      )}

      <UploadToast upload={upload} />
    </>
  );
}
