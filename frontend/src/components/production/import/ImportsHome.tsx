"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useRef, useState } from "react";
import { FileSpreadsheet, Loader2, Upload } from "lucide-react";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { fileSize, importApi, type ImportListRow } from "@/lib/recipe-import";
import { card } from "../ProdKit";

const when = (iso: string | null) => (iso ? new Date(iso).toLocaleString("ru-RU", { dateStyle: "short", timeStyle: "short" }) : "—");

/**
 * Загрузка файла норм (.xlsx): перетащить или выбрать. Тот же файл второй раз не создаёт новую загрузку — открывается прежняя. Ниже — прошлые
 * загрузки: сколько рецептур, готовы, нужна проверка, черновики созданы.
 */
export function ImportsHome({ imports }: { imports: ImportListRow[] }) {
  const t = useT();
  const router = useRouter();
  const input = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [drag, setDrag] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const upload = async (file: File | undefined) => {
    if (!file || busy) return;
    setError(null);
    if (!file.name.toLowerCase().endsWith(".xlsx")) {
      setError(t("Нужен файл Excel .xlsx (старый .xls сохраните в Excel как .xlsx)."));
      return;
    }

    setBusy(true);
    try {
      const { id, existing } = await importApi.upload(file);
      router.push(`/production/recipes/import/${id}${existing ? "?existing=1" : ""}`);
    } catch (e) {
      setError((e as Error).message);
      setBusy(false);
    }
  };

  return (
    <div className="space-y-5">
      <section
        className={`${card} flex flex-col items-center gap-3 border-dashed px-6 py-10 text-center transition ${drag ? "border-accent bg-accent-soft/40" : ""}`}
        onDragOver={(e) => {
          e.preventDefault();
          setDrag(true);
        }}
        onDragLeave={() => setDrag(false)}
        onDrop={(e) => {
          e.preventDefault();
          setDrag(false);
          void upload(e.dataTransfer.files[0]);
        }}
      >
        <span className="grid size-12 place-items-center rounded-2xl bg-accent-soft text-accent-strong">
          {busy ? <Loader2 className="size-6 animate-spin" aria-hidden /> : <FileSpreadsheet className="size-6" aria-hidden />}
        </span>
        <div>
          <h2 className="text-base font-semibold text-ink">{busy ? t("Разбираю файл…") : t("Файл технологических норм")}</h2>
          <p className="mx-auto mt-1 max-w-xl text-sm text-ink-2">
            {t("Все листы с рецептурами: продукты — по столбцам, сырьё — по строкам, нормы — как в ячейках, без округления. Ничего не создаётся, пока вы не проверите сверку и не подтвердите.")}
          </p>
        </div>
        <input ref={input} type="file" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" className="hidden" onChange={(e) => void upload(e.target.files?.[0])} />
        <button
          type="button"
          disabled={busy}
          onClick={() => input.current?.click()}
          className="inline-flex h-10 items-center gap-2 rounded-xl bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-60"
        >
          <Upload className="size-4" aria-hidden /> {t("Выбрать файл .xlsx")}
        </button>
        <p className="text-xs text-ink-3">{t("или перетащите сюда · до 25 МБ")}</p>
        <ErrorNote text={error} />
      </section>

      {imports.length > 0 && (
        <section className={card}>
          <h2 className="mb-3 text-sm font-semibold text-ink">{t("Загрузки")}</h2>
          <ul className="divide-y divide-line">
            {imports.map((i) => (
              <li key={i.id}>
                <Link href={`/production/recipes/import/${i.id}`} className="grid gap-1 py-3 hover:bg-muted/40 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center sm:gap-4 sm:px-2">
                  <span className="min-w-0">
                    <span className="block truncate text-sm font-medium text-ink">{i.fileName}</span>
                    <span className="block text-xs text-ink-3">
                      {when(i.createdAt)} · {i.createdBy ?? "—"} · {fileSize(i.fileSize)}
                    </span>
                  </span>
                  <span className="flex flex-wrap gap-x-3 gap-y-1 text-xs tabular-nums text-ink-2">
                    <span>{t("рецептур: {n}", { n: i.recipes })}</span>
                    <span className="text-ok">{t("готовы: {n}", { n: i.ready })}</span>
                    <span className="text-warn">{t("проверить: {n}", { n: i.needsReview })}</span>
                    <span className="text-accent-strong">{t("черновиков: {n}", { n: i.applied })}</span>
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}
