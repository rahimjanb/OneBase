"use client";

import { useState } from "react";
import { Info, Plus } from "lucide-react";
import { Button, EmptyState, SearchInput } from "@/components/ui";
import type { Department, FileEntry, Folder } from "@/lib/demo-data";
import { fileSize, filesLabel, foldersLabel } from "@/lib/format";
import { FileIcon, kindShort } from "./file-meta";
import { FolderCard } from "./FolderCard";
import { UploadButton, UploadToast, useUpload } from "./upload";

export function DepartmentWorkspace({
  department,
  folders,
  recent,
}: {
  department: Department;
  folders: Folder[];
  recent: FileEntry[];
}) {
  const [query, setQuery] = useState("");
  const [files, setFiles] = useState(recent);
  const { upload, start } = useUpload(department.code, (file) => setFiles((list) => [file, ...list]));

  const q = query.trim().toLowerCase();
  const visibleFolders = folders.filter((f) => f.name.toLowerCase().includes(q));
  const visibleFiles = files.filter((f) => f.name.toLowerCase().includes(q));

  return (
    <>
      <div className="flex flex-wrap items-center gap-3">
        <SearchInput
          className="min-w-[240px] flex-1 sm:max-w-md"
          placeholder={`Поиск в отделе «${department.name}»`}
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />
        <div className="ml-auto flex gap-2">
          <Button>
            <Plus className="size-4" />
            Новая папка
          </Button>
          <UploadButton onFile={start} />
        </div>
      </div>
      <p className="mt-3 text-xs text-ink-3">
        {foldersLabel(department.foldersCount)} · {filesLabel(department.filesCount)}
      </p>

      <h2 className="mt-8 text-lg font-semibold">Папки</h2>
      <div className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
        {visibleFolders.map((folder) => (
          <FolderCard
            key={folder.id}
            folder={folder}
            href={`/base/${department.code}/${folder.id}`}
            departmentName={department.name}
          />
        ))}
      </div>
      {visibleFolders.length === 0 && <EmptyState>Папки не найдены</EmptyState>}

      <h2 className="mt-10 text-lg font-semibold">Недавние файлы</h2>
      {visibleFiles.length === 0 ? (
        <div className="mt-4">
          <EmptyState>{q ? "Файлы не найдены" : "В отделе пока нет недавних файлов"}</EmptyState>
        </div>
      ) : (
        <div className="mt-3">
          <div className="grid grid-cols-[minmax(0,1fr)_32px] gap-4 border-b border-line py-2 text-[11px] font-semibold uppercase tracking-wide text-ink-3 md:grid-cols-[minmax(0,2fr)_minmax(0,1fr)_minmax(0,1.4fr)_32px]">
            <span>Файл</span>
            <span className="hidden md:block">Тип и размер</span>
            <span className="hidden md:block">Загрузил</span>
            <span />
          </div>
          {visibleFiles.map((file) => (
            <div
              key={file.id}
              className="grid grid-cols-[minmax(0,1fr)_32px] items-center gap-4 border-b border-line py-4 text-sm md:grid-cols-[minmax(0,2fr)_minmax(0,1fr)_minmax(0,1.4fr)_32px]"
            >
              <span className="flex min-w-0 items-center gap-3">
                <FileIcon kind={file.kind} />
                <span className="truncate text-ink">{file.name}</span>
              </span>
              <span className="hidden text-xs text-ink-2 md:block">
                {kindShort(file.kind)} · {fileSize(file.sizeBytes)}
              </span>
              <span className="hidden text-xs text-ink-2 md:block">
                {file.uploadedBy} · {file.modified}
              </span>
              <button type="button" aria-label="Сведения о файле" className="justify-self-end text-ink-3 hover:text-ink-2">
                <Info className="size-4" strokeWidth={1.75} />
              </button>
            </div>
          ))}
        </div>
      )}

      <UploadToast upload={upload} />
    </>
  );
}
