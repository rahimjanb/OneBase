"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { MonitorUp } from "lucide-react";
import { EmptyState, SearchInput } from "@/components/ui";
import { fileSize, filesLabel, foldersLabel } from "@/lib/format";
import { accessLabel, filesApi, modifiedLabel, type DepartmentSummary, type SearchRow } from "@/lib/files";
import { FileIcon } from "./file-meta";
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
        <div className="mt-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {departments.map((d) => {
            const badge = connectionBadge[d.connection.status];
            return (
              <Link key={d.id} href={`/base/${d.code}`} className="group rounded-xl border border-line bg-surface p-5 transition-colors hover:border-accent">
                <div className="flex items-start gap-3">
                  <span className="flex-1 text-base font-semibold group-hover:text-accent">{d.name}</span>
                  <span className="rounded-full bg-muted px-2 py-0.5 text-[11px] text-ink-2">{accessLabel[d.access]}</span>
                </div>
                <p className="mt-2 text-sm text-ink-2">
                  {foldersLabel(d.folders)} · {filesLabel(d.files)}
                  {d.files > 0 && ` · ${fileSize(d.sizeBytes)}`}
                </p>
                <div className="mt-4 flex items-center gap-2 text-xs text-ink-3">
                  <span className="flex-1">{d.updatedAt ? `Изменено: ${modifiedLabel(d.updatedAt)}` : "Файлов пока нет"}</span>
                  {badge && (
                    <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 ${badge.cls}`}>
                      <MonitorUp className="size-3" />
                      {badge.label}
                    </span>
                  )}
                </div>
              </Link>
            );
          })}
        </div>
      )}
      {preview && <PreviewDialog file={preview.file} onClose={() => setPreview(null)} />}
    </>
  );
}
