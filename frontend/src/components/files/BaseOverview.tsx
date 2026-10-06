"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { MonitorUp } from "lucide-react";
import { EmptyState, SearchInput } from "@/components/ui";
import { fileSize, filesLabel, foldersLabel } from "@/lib/format";
import { accessLabel, filesApi, modifiedLabel, type DepartmentSummary, type SearchRow } from "@/lib/files";
import { FileIcon, FolderIcon } from "./file-meta";
import { PreviewDialog } from "./FileDialogs";

const connectionBadge = {
  active: { label: "Windows подключён", cls: "bg-ok/15 text-ok" },
  revoked: { label: "Windows отозван", cls: "bg-bad/10 text-bad" },
  none: null,
} as const;

/** «Файлы»: отделы, к которым есть доступ, и поиск по всем сразу. */
export function BaseOverview({ departments }: { departments: DepartmentSummary[] }) {
  const [query, setQuery] = useState("");
  const [found, setFound] = useState<SearchRow[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [preview, setPreview] = useState<SearchRow | null>(null);

  useEffect(() => {
    const q = query.trim();
    if (q.length < 2) return setFound(null);
    const timer = window.setTimeout(() => filesApi.search(q).then(setFound, (e: Error) => setError(e.message)), 300);
    return () => window.clearTimeout(timer);
  }, [query]);

  return (
    <>
      <SearchInput
        className="w-full sm:max-w-md"
        placeholder="Поиск файлов: имя, тип, папка, отдел, автор"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
      />
      {error && <p className="mt-3 text-sm text-bad">{error}</p>}

      {found ? (
        found.length === 0 ? (
          <div className="mt-6"><EmptyState>Ничего не найдено</EmptyState></div>
        ) : (
          <div className="mt-4 divide-y divide-line">
            {found.map((row) => (
              <button key={row.file.id} type="button" onClick={() => setPreview(row)} className="flex w-full items-center gap-3 py-3 text-left text-sm hover:text-accent">
                <FileIcon extension={row.file.extension} />
                <span className="min-w-0 flex-1">
                  <span className="block truncate">{row.file.name}</span>
                  <span className="block truncate text-xs text-ink-3">{row.folderPath} · {modifiedLabel(row.file.updatedAt)}</span>
                </span>
                <span className="text-xs text-ink-3">{fileSize(row.file.sizeBytes)}</span>
              </button>
            ))}
          </div>
        )
      ) : departments.length === 0 ? (
        <div className="mt-6"><EmptyState>Нет отделов, к файлам которых у вас есть доступ</EmptyState></div>
      ) : (
        // Отделы — папки верхнего уровня: та же таблица, что внутри отдела («Файлы» → «Производство» → «Отчёты»).
        <div className="mt-5 overflow-x-auto">
          <div className="min-w-[640px]">
            <div className="grid grid-cols-[minmax(0,2fr)_130px_minmax(0,1.3fr)_90px_minmax(0,1.2fr)] gap-3 border-b border-line py-2 text-[11px] font-semibold uppercase tracking-wide text-ink-3">
              <span>Отдел</span>
              <span>Доступ</span>
              <span>Содержимое</span>
              <span>Размер</span>
              <span>Изменён</span>
            </div>
            {departments.map((d) => {
              const badge = connectionBadge[d.connection.status];
              return (
                <Link
                  key={d.id}
                  href={`/base/${d.code}`}
                  className="group grid grid-cols-[minmax(0,2fr)_130px_minmax(0,1.3fr)_90px_minmax(0,1.2fr)] items-center gap-3 border-b border-line py-3 text-sm hover:bg-muted/50"
                >
                  <span className="flex min-w-0 items-center gap-3">
                    <FolderIcon />
                    <span className="truncate font-medium group-hover:text-accent">{d.name}</span>
                    {badge && (
                      <span className={`hidden shrink-0 items-center gap-1 rounded-full px-2 py-0.5 text-[11px] sm:inline-flex ${badge.cls}`} title={badge.label}>
                        <MonitorUp className="size-3" />
                        Windows
                      </span>
                    )}
                  </span>
                  <span className="text-xs text-ink-2">{accessLabel[d.access]}</span>
                  <span className="text-xs text-ink-2">
                    {foldersLabel(d.folders)} · {filesLabel(d.files)}
                  </span>
                  <span className="text-xs text-ink-2">{d.files > 0 ? fileSize(d.sizeBytes) : "—"}</span>
                  <span className="text-xs text-ink-2">{d.updatedAt ? modifiedLabel(d.updatedAt) : "файлов пока нет"}</span>
                </Link>
              );
            })}
          </div>
        </div>
      )}
      {preview && <PreviewDialog file={preview.file} onClose={() => setPreview(null)} />}
    </>
  );
}
