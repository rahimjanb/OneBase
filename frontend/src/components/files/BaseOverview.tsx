"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { FolderPlus, MonitorUp } from "lucide-react";
import { Button, EmptyState, SearchInput } from "@/components/ui";
import { useT } from "@/i18n/client";
import { fileSize, filesLabel, foldersLabel } from "@/lib/format";
import { accessLabel, filesApi, modifiedLabel, sharedAccessLabel, type DepartmentSummary, type SearchRow, type SharedSummary } from "@/lib/files";
import { FileIcon, FolderIcon } from "./file-meta";
import { NameDialog, PreviewDialog } from "./FileDialogs";
import { PersonalLinkButton } from "./WindowsConnect";

const connectionBadge = {
  active: { label: "Windows подключён", cls: "bg-ok/15 text-ok" },
  revoked: { label: "Windows отозван", cls: "bg-bad/10 text-bad" },
  none: null,
} as const;

/**
 * «Файлы»: отделы, к которым есть доступ, общие папки (видны только тем, кому выданы) и поиск по всем сразу. canCreateShared —
 * администратор: создаёт общие папки и выдаёт к ним доступ.
 */
export function BaseOverview({ departments, shared = [], canCreateShared = false }: { departments: DepartmentSummary[]; shared?: SharedSummary[]; canCreateShared?: boolean }) {
  const t = useT();
  const router = useRouter();
  const [creating, setCreating] = useState(false);
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
      <div className="flex flex-wrap items-center gap-2">
        <SearchInput
          className="w-full sm:max-w-md"
          placeholder={t("Поиск файлов: имя, тип, папка, отдел, автор")}
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />
        <div className="ml-auto flex flex-wrap gap-2">
          {canCreateShared && (
            <Button onClick={() => setCreating(true)}>
              <FolderPlus className="size-4" />
              {t("Новая общая папка")}
            </Button>
          )}
          <PersonalLinkButton />
        </div>
      </div>
      {error && <p className="mt-3 text-sm text-bad">{error}</p>}

      {found ? (
        found.length === 0 ? (
          <div className="mt-6"><EmptyState>{t("Ничего не найдено")}</EmptyState></div>
        ) : (
          <div className="mt-4 divide-y divide-line">
            {found.map((row) => (
              <button key={row.file.id} type="button" onClick={() => setPreview(row)} className="flex w-full items-center gap-3 py-3 text-left text-sm hover:text-accent">
                <FileIcon extension={row.file.extension} />
                <span className="min-w-0 flex-1">
                  <span className="block truncate">{row.file.name}</span>
                  <span className="block truncate text-xs text-ink-3">{row.folderPath} · {modifiedLabel(row.file.updatedAt, t)}</span>
                </span>
                <span className="text-xs text-ink-3">{fileSize(row.file.sizeBytes)}</span>
              </button>
            ))}
          </div>
        )
      ) : departments.length === 0 ? (
        <div className="mt-6"><EmptyState>{t("Нет отделов, к файлам которых у вас есть доступ")}</EmptyState></div>
      ) : (
        // Отделы — папки верхнего уровня: та же таблица, что внутри отдела («Файлы» → «Производство» → «Отчёты»).
        <div className="mt-5 overflow-x-auto">
          <div className="min-w-[640px]">
            <div className="grid grid-cols-[minmax(0,2fr)_130px_minmax(0,1.3fr)_90px_minmax(0,1.2fr)] gap-3 border-b border-line py-2 text-[11px] font-semibold uppercase tracking-wide text-ink-3">
              <span>{t("Отдел")}</span>
              <span>{t("Доступ")}</span>
              <span>{t("Содержимое")}</span>
              <span>{t("Размер")}</span>
              <span>{t("Изменён")}</span>
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
                    <span className="truncate font-medium group-hover:text-accent">{t(d.name)}</span>
                    {badge && (
                      <span className={`hidden shrink-0 items-center gap-1 rounded-full px-2 py-0.5 text-[11px] sm:inline-flex ${badge.cls}`} title={t(badge.label)}>
                        <MonitorUp className="size-3" />
                        Windows
                      </span>
                    )}
                  </span>
                  <span className="text-xs text-ink-2">{t(accessLabel[d.access])}</span>
                  <span className="text-xs text-ink-2">
                    {foldersLabel(d.folders, t)} · {filesLabel(d.files, t)}
                  </span>
                  <span className="text-xs text-ink-2">{d.files > 0 ? fileSize(d.sizeBytes) : "—"}</span>
                  <span className="text-xs text-ink-2">{d.updatedAt ? modifiedLabel(d.updatedAt, t) : t("файлов пока нет")}</span>
                </Link>
              );
            })}
          </div>
        </div>
      )}
      {!found && (shared.length > 0 || canCreateShared) && (
        <section className="mt-8">
          <h2 className="text-sm font-semibold">{t("Общие файлы")}</h2>
          <p className="mt-0.5 text-xs text-ink-3">{t("Папки для нескольких отделов и сотрудников: видны только тем, кому выдан доступ, и не смешиваются с папками отделов.")}</p>
          {shared.length === 0 ? (
            <div className="mt-3"><EmptyState>{t("Общих папок пока нет — создайте первую и выдайте доступ")}</EmptyState></div>
          ) : (
            <div className="mt-3 overflow-x-auto">
              <div className="min-w-[640px]">
                {shared.map((f) => (
                  <Link
                    key={f.id}
                    href={`/base/shared/${f.id}`}
                    className="group grid grid-cols-[minmax(0,2fr)_130px_minmax(0,1.3fr)_90px_minmax(0,1.2fr)] items-center gap-3 border-b border-line py-3 text-sm hover:bg-muted/50"
                  >
                    <span className="flex min-w-0 items-center gap-3">
                      <FolderIcon />
                      <span className="truncate font-medium group-hover:text-accent">{f.name}</span>
                    </span>
                    <span className="text-xs text-ink-2">{t(sharedAccessLabel[f.access])}</span>
                    <span className="text-xs text-ink-2">
                      {foldersLabel(f.folders, t)} · {filesLabel(f.files, t)}
                    </span>
                    <span className="text-xs text-ink-2">{f.files > 0 ? fileSize(f.sizeBytes) : "—"}</span>
                    <span className="text-xs text-ink-2">{f.updatedAt ? modifiedLabel(f.updatedAt, t) : t("файлов пока нет")}</span>
                  </Link>
                ))}
              </div>
            </div>
          )}
        </section>
      )}
      {preview && <PreviewDialog file={preview.file} onClose={() => setPreview(null)} />}
      {creating && (
        <NameDialog
          title={t("Новая общая папка")}
          action={t("Создать")}
          onClose={() => setCreating(false)}
          onSubmit={async (name) => {
            const created = await filesApi.createShared(name);
            router.push(`/base/shared/${created.id}`);
          }}
        />
      )}
    </>
  );
}
