"use client";

import { useRouter } from "next/navigation";
import { useRef, useState } from "react";
import { FileSpreadsheet, Loader2 } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";

const secondary = "inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3 text-sm font-medium text-ink hover:bg-muted disabled:opacity-50";

/**
 * Импорт из Excel в два шага: выбрали файл — сервер проверяет его (dryRun: что и куда попадёт, ничего не сохраняется), окно показывает
 * отчёт; «Импортировать» — та же загрузка по-настоящему. Тексты — русские ключи словаря.
 */
export function FileImport<R extends { dryRun: boolean }>({
  endpoint,
  checkTitle,
  doneTitle,
  hint,
  ready,
  report,
  label = "Импорт из Excel",
}: {
  /** Подпись кнопки. */
  label?: string;
  endpoint: string;
  checkTitle: string;
  doneTitle: string;
  hint: string;
  ready: (result: R) => boolean;
  report: (result: R) => React.ReactNode;
}) {
  const t = useT();
  const router = useRouter();
  const input = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<R | null>(null);
  const [error, setError] = useState<string | null>(null);

  const send = async (picked: File, dryRun: boolean) => {
    setBusy(true);
    setError(null);
    try {
      const form = new FormData();
      form.append("file", picked);
      const response = await fetch(`${endpoint}${dryRun ? `${endpoint.includes("?") ? "&" : "?"}dryRun=true` : ""}`, { method: "POST", body: form });
      const json = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(json.error ? t(json.error) : t("Ошибка {status}", { status: response.status }));
      setResult(json);
      if (!dryRun) router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setResult(null);
    } finally {
      setBusy(false);
    }
  };
  const close = () => {
    setFile(null);
    setResult(null);
    setError(null);
  };
  const checking = result?.dryRun !== false;

  return (
    <>
      <input
        ref={input}
        type="file"
        accept=".xlsx,.xlsm,.csv"
        className="sr-only"
        tabIndex={-1}
        onChange={(e) => {
          const picked = e.target.files?.[0];
          if (picked) {
            setFile(picked);
            void send(picked, true);
          }
          e.target.value = "";
        }}
      />
      <button type="button" onClick={() => input.current?.click()} disabled={busy} className={secondary}>
        {busy ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <FileSpreadsheet className="size-4" aria-hidden />}
        <span className="max-sm:sr-only">{t(label)}</span>
      </button>
      <Sheet
        open={file !== null}
        onClose={close}
        title={checking ? t(checkTitle) : t(doneTitle)}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" onClick={close} className="inline-flex h-10 items-center rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted">
              {checking ? t("Отмена") : t("Закрыть")}
            </button>
            {checking && (
              <button
                type="button"
                onClick={() => file && void send(file, false)}
                disabled={busy || result === null || !ready(result)}
                className="inline-flex h-10 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50"
              >
                {busy && result && <Loader2 className="size-4 animate-spin" aria-hidden />}
                {t("Импортировать")}
              </button>
            )}
          </div>
        }
      >
        {file && <p className="mb-3 truncate text-sm text-ink-3">{file.name}</p>}
        {busy && !result && !error && (
          <p className="flex items-center gap-2 text-sm text-ink-2">
            <Loader2 className="size-4 animate-spin" aria-hidden /> {t("Читаем файл…")}
          </p>
        )}
        <ErrorNote text={error} />
        {result && report(result)}
        <p className="mt-4 text-xs text-ink-3">{t(hint)}</p>
      </Sheet>
    </>
  );
}

/** «Какой столбец файла куда пошёл» — общий блок отчётов импорта. */
export function ImportColumns({ columns }: { columns: string[] | null }) {
  const t = useT();
  if (!columns || columns.length === 0) return null;
  return (
    <section>
      <h3 className="mb-1.5 text-xs font-semibold text-ink-3">{t("Столбцы файла")}</h3>
      <ul className="grid gap-x-4 gap-y-1 text-ink-2 sm:grid-cols-2">
        {columns.map((c) => {
          const [to, from] = c.split(" ← ");
          return (
            <li key={c} className="min-w-0 truncate">
              <span className="text-ink">{t(to)}</span> <span className="text-ink-3">← {from}</span>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

/** Ошибки и предупреждения импорта — прокручиваемый список. */
export function ImportErrors({ errors }: { errors: string[] }) {
  const t = useT();
  if (errors.length === 0) return null;
  return (
    <ul className="max-h-48 space-y-1 overflow-y-auto rounded-xl bg-muted px-3 py-2 text-xs text-ink-2">
      {errors.map((e) => (
        <li key={e}>{t(e)}</li>
      ))}
    </ul>
  );
}
