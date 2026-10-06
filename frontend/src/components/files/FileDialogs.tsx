"use client";

import { useEffect, useState } from "react";
import { ChevronRight, Download, Loader2 } from "lucide-react";
import { Button } from "@/components/ui";
import { fileSize } from "@/lib/format";
import { downloadUrl, filesApi, modifiedLabel, typeGroup, type FilePreview, type FileRow, type FolderView, type VersionRow } from "@/lib/files";
import { FolderIcon } from "./file-meta";
import { Modal } from "./Modal";

/** Ввод имени: новая папка, переименование. */
export function NameDialog({
  title,
  initial = "",
  action,
  onSubmit,
  onClose,
}: {
  title: string;
  initial?: string;
  action: string;
  onSubmit: (name: string) => Promise<void>;
  onClose: () => void;
}) {
  const [name, setName] = useState(initial);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await onSubmit(name.trim());
      onClose();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal title={title} onClose={onClose}>
      <form onSubmit={submit} className="space-y-3">
        <input
          autoFocus
          value={name}
          onChange={(e) => setName(e.target.value)}
          onFocus={(e) => {
            const dot = e.target.value.lastIndexOf(".");
            e.target.setSelectionRange(0, dot > 0 ? dot : e.target.value.length);
          }}
          maxLength={255}
          className="h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm outline-none focus:border-accent"
        />
        {error && <p className="text-sm text-bad">{error}</p>}
        <div className="flex justify-end gap-2">
          <Button onClick={onClose}>Отмена</Button>
          <Button variant="primary" type="submit" disabled={busy || !name.trim()}>
            {busy && <Loader2 className="size-4 animate-spin" />}
            {action}
          </Button>
        </div>
      </form>
    </Modal>
  );
}

/** Выбор папки для переноса: обход дерева отдела от корня. */
export function MoveDialog({
  rootId,
  itemName,
  excludeFolderId,
  onMove,
  onClose,
}: {
  rootId: string;
  itemName: string;
  excludeFolderId?: string;
  onMove: (targetFolderId: string) => Promise<void>;
  onClose: () => void;
}) {
  const [view, setView] = useState<FolderView | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const open = (id: string) => filesApi.folder(id).then(setView, (e: Error) => setError(e.message));
  useEffect(() => {
    filesApi.folder(rootId).then(setView, (e: Error) => setError(e.message));
  }, [rootId]);

  const move = async () => {
    if (!view) return;
    setBusy(true);
    setError(null);
    try {
      await onMove(view.id);
      onClose();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal title={`Перенести «${itemName}»`} onClose={onClose}>
      {!view ? (
        <Loader2 className="size-5 animate-spin text-ink-3" />
      ) : (
        <div className="space-y-3">
          <nav className="flex flex-wrap items-center gap-1 text-sm">
            {view.path.map((p, i) => (
              <span key={p.id} className="flex items-center gap-1">
                {i > 0 && <ChevronRight className="size-3.5 text-ink-3" />}
                <button type="button" className="text-accent hover:underline" onClick={() => open(p.id)}>
                  {p.name}
                </button>
              </span>
            ))}
          </nav>
          <div className="max-h-72 divide-y divide-line overflow-y-auto rounded-lg border border-line">
            {view.folders.filter((f) => f.id !== excludeFolderId).length === 0 && <p className="px-3 py-3 text-sm text-ink-3">Вложенных папок нет</p>}
            {view.folders
              .filter((f) => f.id !== excludeFolderId)
              .map((f) => (
                <button key={f.id} type="button" onClick={() => open(f.id)} className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm hover:bg-muted">
                  <FolderIcon />
                  <span className="flex-1 truncate">{f.name}</span>
                  <ChevronRight className="size-3.5 text-ink-3" />
                </button>
              ))}
          </div>
          {error && <p className="text-sm text-bad">{error}</p>}
          <div className="flex justify-end gap-2">
            <Button onClick={onClose}>Отмена</Button>
            <Button variant="primary" onClick={move} disabled={busy || !view.canWrite}>
              {busy && <Loader2 className="size-4 animate-spin" />}
              Перенести в «{view.name}»
            </Button>
          </div>
        </div>
      )}
    </Modal>
  );
}

/** История версий: каждую можно скачать. */
export function VersionsDialog({ file, onClose }: { file: FileRow; onClose: () => void }) {
  const [rows, setRows] = useState<VersionRow[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    filesApi.versions(file.id).then(setRows, (e: Error) => setError(e.message));
  }, [file.id]);
  return (
    <Modal title={`Версии «${file.name}»`} onClose={onClose}>
      {error && <p className="text-sm text-bad">{error}</p>}
      {!rows && !error && <Loader2 className="size-5 animate-spin text-ink-3" />}
      <div className="divide-y divide-line">
        {rows?.map((v) => (
          <div key={v.number} className="flex items-center gap-3 py-2.5 text-sm">
            <span className="w-10 font-medium">v{v.number}</span>
            <span className="flex-1 text-ink-2">
              {modifiedLabel(v.createdAt)} · {v.by?.name ?? "—"} · {fileSize(v.sizeBytes)}
              {v.current && <span className="ml-2 rounded-full bg-accent/10 px-2 py-0.5 text-xs text-accent">текущая</span>}
            </span>
            <a href={downloadUrl(file, false, v.number)} className="rounded-md p-1.5 text-ink-3 hover:bg-muted hover:text-ink" aria-label={`Скачать версию ${v.number}`}>
              <Download className="size-4" />
            </a>
          </div>
        ))}
      </div>
    </Modal>
  );
}

/** Предпросмотр: картинки и PDF — прямо по ссылке скачивания, CSV/XLSX — таблица, DOCX/TXT — текст (готовые данные с сервера). */
export function PreviewDialog({ file, onClose }: { file: FileRow; onClose: () => void }) {
  const [preview, setPreview] = useState<FilePreview | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [sheet, setSheet] = useState(0);
  const group = typeGroup(file.extension);
  useEffect(() => {
    if (group === "images" || group === "pdf") return;
    filesApi.preview(file.id).then(setPreview, (e: Error) => setError(e.message));
  }, [file.id, group]);

  const current = preview?.sheets[sheet];
  return (
    <Modal title={file.name} onClose={onClose} wide>
      <div className="space-y-3">
        {group === "images" && (
          // eslint-disable-next-line @next/next/no-img-element -- файл из хранилища OneBase через /bff, не статический ресурс
          <img src={downloadUrl(file, true)} alt={file.name} className="mx-auto max-h-[70vh] rounded-lg object-contain" />
        )}
        {group === "pdf" && <iframe src={downloadUrl(file, true)} title={file.name} className="h-[75vh] w-full rounded-lg border border-line" />}
        {error && <p className="text-sm text-bad">{error}</p>}
        {group !== "images" && group !== "pdf" && !preview && !error && <Loader2 className="size-5 animate-spin text-ink-3" />}
        {preview?.kind === "table" && (
          <>
            {preview.sheets.length > 1 && (
              <div className="flex flex-wrap gap-1">
                {preview.sheets.map((s, i) => (
                  <button
                    key={s.name + i}
                    type="button"
                    onClick={() => setSheet(i)}
                    className={`rounded-md px-2.5 py-1 text-xs ${i === sheet ? "bg-accent text-white" : "bg-muted text-ink-2 hover:text-ink"}`}
                  >
                    {s.name}
                  </button>
                ))}
              </div>
            )}
            <div className="max-h-[65vh] overflow-auto rounded-lg border border-line">
              <table className="min-w-full text-xs">
                <tbody>
                  {current?.rows.map((row, r) => (
                    <tr key={r} className={r === 0 ? "bg-muted font-medium" : "border-t border-line"}>
                      <td className="w-8 border-r border-line px-2 py-1 text-right text-ink-3">{r + 1}</td>
                      {row.map((cell, c) => (
                        <td key={c} className="max-w-64 truncate whitespace-nowrap px-2 py-1" title={cell}>
                          {cell}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
              {current?.rows.length === 0 && <p className="p-3 text-sm text-ink-3">Лист пустой</p>}
            </div>
            {current?.truncated && <p className="text-xs text-ink-3">Показаны первые строки и столбцы — весь файл откройте после скачивания.</p>}
          </>
        )}
        {preview?.kind === "text" && (
          <div className="max-h-[65vh] space-y-2 overflow-auto rounded-lg border border-line p-4 text-sm">
            {preview.paragraphs.map((p, i) => (
              <p key={i} className="whitespace-pre-wrap">{p}</p>
            ))}
            {preview.truncated && <p className="text-xs text-ink-3">Показано начало документа.</p>}
          </div>
        )}
        {preview?.note && <p className="text-sm text-ink-2">{preview.note}</p>}
        <div className="flex justify-end">
          <a href={downloadUrl(file)} className="inline-flex h-9 items-center gap-2 rounded-lg border border-line px-4 text-sm font-medium hover:bg-muted">
            <Download className="size-4" />
            Скачать · {fileSize(file.sizeBytes)}
          </a>
        </div>
      </div>
    </Modal>
  );
}
