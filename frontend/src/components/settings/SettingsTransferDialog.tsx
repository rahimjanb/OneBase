"use client";

import { useState } from "react";
import { FileUp, KeyRound, Loader2 } from "lucide-react";
import { Modal } from "@/components/files/Modal";
import { useT } from "@/i18n/client";
import { dataSourcesApi, type ImportItem, type ImportResult } from "@/lib/data-sources";

const primary = "inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-medium text-white hover:bg-accent-strong disabled:opacity-50";
const button = "inline-flex h-9 items-center gap-2 rounded-lg border border-line px-4 text-sm text-ink hover:bg-muted disabled:opacity-50";

const statusStyle: Record<ImportItem["status"], { label: string; className: string }> = {
  created: { label: "добавится", className: "bg-ok-soft text-ok" },
  updated: { label: "обновится", className: "bg-accent-soft text-accent-strong" },
  unchanged: { label: "без изменений", className: "bg-muted text-ink-2" },
  skipped: { label: "пропущено", className: "bg-warn-soft text-warn" },
  error: { label: "ошибка", className: "bg-bad-soft text-bad" },
};

const doneLabel: Partial<Record<ImportItem["status"], string>> = { created: "добавлено", updated: "обновлено" };

const kindLabel: Record<ImportItem["kind"], string> = { connection: "Таблицы Google", dealer: "Дилеры регионов", binding: "Привязки" };

/**
 * Перенос настроек «Источников данных» файлом (с рабочего компьютера на сервер): таблицы без ключей, привязки, дилеры регионов.
 * Сначала «Проверить» — сервер показывает, что добавит и обновит, ничего не записывая; затем «Перенести». Ключ JSON сервисного аккаунта
 * нужен только для закрытых таблиц, которых здесь ещё нет. Данные не синхронизируются — привязки пойдут по своему расписанию.
 */
export function SettingsTransferDialog({ onClose, onDone }: { onClose: () => void; onDone: (text: string, ok: boolean) => void }) {
  const t = useT();
  const [settings, setSettings] = useState<unknown>(null);
  const [fileName, setFileName] = useState<string | null>(null);
  const [key, setKey] = useState<string | null>(null);
  const [keyName, setKeyName] = useState<string | null>(null);
  const [result, setResult] = useState<ImportResult | null>(null);
  const [busy, setBusy] = useState<"check" | "import" | null>(null);
  const [error, setError] = useState<string | null>(null);

  const readFile = async (file: File | undefined, target: "settings" | "key") => {
    if (!file) return;
    setError(null);
    setResult(null);
    const text = await file.text();
    if (target === "key") {
      setKey(text);
      setKeyName(file.name);
      return;
    }
    try {
      setSettings(JSON.parse(text));
      setFileName(file.name);
    } catch {
      setSettings(null);
      setFileName(null);
      setError(t("Файл не похож на настройки OneBase: это не JSON."));
    }
  };

  const run = async (dryRun: boolean) => {
    setBusy(dryRun ? "check" : "import");
    setError(null);
    try {
      const r = await dataSourcesApi.importSettings(settings, key, dryRun);
      setResult(r);
      if (!dryRun) {
        const done = r.items.filter((i) => i.status === "created" || i.status === "updated").length;
        onDone(r.errors > 0 ? t("Перенесено: {done}, с ошибками: {errors} — см. окно переноса.", { done, errors: r.errors }) : t("Настройки перенесены: {done}.", { done }), r.errors === 0);
      }
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(null);
    }
  };

  const groups = result ? (["connection", "dealer", "binding"] as const).map((k) => [k, result.items.filter((i) => i.kind === k)] as const).filter(([, items]) => items.length > 0) : [];

  return (
    <Modal title={t("Перенос настроек из файла")} onClose={onClose}>
      <div className="space-y-4">
        <p className="text-sm text-ink-2">
          {t("Файл — «Экспорт настроек» с другого OneBase: таблицы Google (без ключей), привязки и дилеры регионов. Уже подключённые таблицы и совпадающие привязки обновятся, остальное добавится. Данные не синхронизируются сразу — привязки пойдут по своему расписанию.")}
        </p>
        <label className="flex cursor-pointer items-center gap-3 rounded-xl border border-dashed border-line p-3 hover:bg-muted/50">
          <FileUp className="size-5 shrink-0 text-ink-3" />
          <span className="min-w-0 flex-1">
            <span className="block text-sm font-medium text-ink">{fileName ?? t("Выбрать файл настроек (.json)")}</span>
            <span className="block text-xs text-ink-3">{t("onebase-data-sources-ГГГГ-ММ-ДД.json")}</span>
          </span>
          <input type="file" accept=".json,application/json" className="sr-only" onChange={(e) => void readFile(e.target.files?.[0], "settings")} />
        </label>
        <label className="flex cursor-pointer items-center gap-3 rounded-xl border border-dashed border-line p-3 hover:bg-muted/50">
          <KeyRound className="size-5 shrink-0 text-ink-3" />
          <span className="min-w-0 flex-1">
            <span className="block text-sm font-medium text-ink">{keyName ?? t("Ключ JSON сервисного аккаунта — если нужен")}</span>
            <span className="block text-xs text-ink-3">{t("Для закрытых таблиц, которых здесь ещё нет. Хранится на сервере зашифрованным.")}</span>
          </span>
          <input type="file" accept=".json,application/json" className="sr-only" onChange={(e) => void readFile(e.target.files?.[0], "key")} />
        </label>

        {error && <div className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</div>}

        {result && (
          <div className="max-h-[45vh] space-y-3 overflow-y-auto rounded-xl border border-line p-3">
            <p className="text-sm font-medium text-ink">
              {result.dryRun ? t("Проверка: ничего не записано") : t("Перенос выполнен")}
              {result.errors > 0 && <span className="ml-2 text-bad">{t("ошибок: {n}", { n: result.errors })}</span>}
            </p>
            {groups.map(([kind, items]) => (
              <div key={kind}>
                <p className="mb-1 text-[11px] font-semibold uppercase tracking-wide text-ink-3">{t(kindLabel[kind])}</p>
                <ul className="space-y-1">
                  {items.map((i, n) => (
                    <li key={n} className="flex items-start gap-2 text-xs">
                      <span className={`mt-px shrink-0 rounded px-1.5 py-0.5 font-medium ${statusStyle[i.status].className}`}>
                        {t(!result.dryRun && doneLabel[i.status] ? doneLabel[i.status]! : statusStyle[i.status].label)}
                      </span>
                      <span className="min-w-0">
                        <span className="font-medium text-ink">{i.name}</span>
                        <span className="block text-ink-3">{t(i.message)}</span>
                      </span>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </div>
        )}

        <div className="flex flex-wrap justify-end gap-2 border-t border-line pt-4">
          <button type="button" onClick={onClose} className={button}>
            {result && !result.dryRun ? t("Закрыть") : t("Отмена")}
          </button>
          <button type="button" onClick={() => void run(true)} disabled={!settings || busy !== null} className={button}>
            {busy === "check" && <Loader2 className="size-4 animate-spin" />}
            {t("Проверить")}
          </button>
          <button type="button" onClick={() => void run(false)} disabled={!settings || busy !== null || !result?.dryRun} className={primary}
            title={!result?.dryRun ? t("Сначала проверьте файл") : undefined}>
            {busy === "import" && <Loader2 className="size-4 animate-spin" />}
            {t("Перенести")}
          </button>
        </div>
      </div>
    </Modal>
  );
}
