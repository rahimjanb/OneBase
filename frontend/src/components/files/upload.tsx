"use client";

import { useEffect, useRef, useState } from "react";
import { Upload } from "lucide-react";
import { Button, ProgressBar } from "@/components/ui";
import { currentUser, type FileEntry } from "@/lib/demo-data";
import { fileSize } from "@/lib/format";
import { kindFromName } from "./file-meta";

type UploadState = { name: string; size: number; progress: number };

/**
 * Загрузка файлов. Пока нет Files API, прогресс имитируется локально,
 * а файл добавляется только в список на странице (не сохраняется).
 */
export function useUpload(folderId: string, onUploaded: (file: FileEntry) => void) {
  const [upload, setUpload] = useState<UploadState | null>(null);
  const timers = useRef<number[]>([]);

  useEffect(() => () => timers.current.forEach((t) => window.clearInterval(t)), []);

  function start(file: File) {
    let progress = 0;
    setUpload({ name: file.name, size: file.size, progress });

    const interval = window.setInterval(() => {
      progress = Math.min(100, progress + 12);
      setUpload({ name: file.name, size: file.size, progress });
      if (progress < 100) return;

      window.clearInterval(interval);
      onUploaded({
        id: crypto.randomUUID(),
        folderId,
        name: file.name,
        kind: kindFromName(file.name),
        sizeBytes: file.size,
        modified: "только что",
        uploadedBy: currentUser.login,
        uploadedAt: new Date().toLocaleString("ru-RU", { dateStyle: "short", timeStyle: "short" }),
        version: 1,
      });
      timers.current.push(window.setTimeout(() => setUpload(null), 1500));
    }, 180);
    timers.current.push(interval);
  }

  return { upload, start };
}

export function UploadButton({
  onFile,
  variant = "primary",
  size = "md",
}: {
  onFile: (file: File) => void;
  variant?: "primary" | "outline";
  size?: "sm" | "md";
}) {
  const input = useRef<HTMLInputElement>(null);
  return (
    <>
      <Button variant={variant} size={size} onClick={() => input.current?.click()}>
        <Upload className="size-4" />
        Загрузить
      </Button>
      <input
        ref={input}
        type="file"
        hidden
        onChange={(e) => {
          const file = e.target.files?.[0];
          if (file) onFile(file);
          e.target.value = "";
        }}
      />
    </>
  );
}

export function UploadToast({ upload }: { upload: UploadState | null }) {
  if (!upload) return null;
  const done = upload.progress >= 100;
  return (
    <div
      role="status"
      className="fixed bottom-6 right-6 z-40 w-[calc(100%-3rem)] max-w-xs rounded-xl border border-line bg-white p-4 shadow-lg"
    >
      <div className="text-sm font-semibold text-ink">
        {done ? "Файл загружен" : `Загрузка файлов · ${upload.progress}%`}
      </div>
      <div className="mt-1 truncate text-xs text-ink-3">
        {upload.name} · {fileSize(upload.size)}
      </div>
      <div className="mt-3">
        <ProgressBar value={upload.progress} />
      </div>
    </div>
  );
}
