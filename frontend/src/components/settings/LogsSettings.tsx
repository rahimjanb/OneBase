"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { ChevronDown, ChevronRight, Loader2, RefreshCw } from "lucide-react";
import { button } from "@/components/ai/form";
import { CancelSyncButton } from "@/components/sales/CancelSyncButton";
import { Note, Section } from "@/components/sales/bits";
import { bff } from "@/lib/bff";
import { progressText } from "@/lib/integrations";
import { sourceLabels, type LogEntry, type LogSource, type LogsView } from "@/lib/logs";
import { dateTime } from "@/lib/sales/format";
import type { SyncStatus } from "@/lib/sales/types";

const sources: (LogSource | "all")[] = ["all", "linko", "sync", "system"];

const levelView: Record<string, { label: string; className: string }> = {
  Critical: { label: "критическая", className: "bg-bad text-white" },
  Error: { label: "ошибка", className: "bg-bad-soft text-bad" },
  Warning: { label: "предупреждение", className: "bg-warn-soft text-warn" },
};

const select = "h-8 rounded-lg border border-line bg-surface px-2 text-xs text-ink focus:border-accent focus:outline-none";

const time = (iso: string) =>
  new Intl.DateTimeFormat("ru-RU", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit", second: "2-digit", timeZone: "Asia/Tashkent" }).format(
    new Date(iso),
  );

export function LogsSettings({ initial, initialSync }: { initial: LogsView; initialSync: SyncStatus | null }) {
  const [data, setData] = useState(initial);
  const [source, setSource] = useState<LogSource | "all">("all");
  const [level, setLevel] = useState("");
  const [days, setDays] = useState(initial.days);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [open, setOpen] = useState<number | null>(null);
  const [sync, setSync] = useState(initialSync);

  const query = useCallback(
    (beforeId?: number) => {
      const p = new URLSearchParams({ days: String(days) });
      if (source !== "all") p.set("source", source);
      if (level) p.set("level", level);
      if (beforeId) p.set("beforeId", String(beforeId));
      return `logs?${p}`;
    },
    [source, level, days],
  );

  const load = useCallback(async () => {
    setBusy(true);
    setError(null);
    try {
      setData(await bff<LogsView>(query()));
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }, [query]);

  const more = async () => {
    const last = data.items.at(-1);
    if (!last) return;
    setBusy(true);
    try {
      const next = await bff<LogsView>(query(last.id));
      setData((d) => ({ ...next, items: [...d.items, ...next.items] }));
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  // Фильтры применяются сразу; при открытии страницы данные уже есть.
  const first = useRef(true);
  useEffect(() => {
    if (first.current) {
      first.current = false;
      return;
    }
    void load();
  }, [load]);

  // Пока идёт синхронизация — статус каждые 3 с; когда закончится, журнал перечитывается.
  useEffect(() => {
    if (!sync?.isRunning) return;
    const timer = setInterval(async () => {
      const next = await bff<SyncStatus>("sales/status").catch(() => null);
      if (!next) return;
      setSync(next);
      if (!next.isRunning) void load();
    }, 3000);
    return () => clearInterval(timer);
  }, [sync?.isRunning, load]);

  const total = (s: LogSource | "all") =>
    s === "all"
      ? Object.values(data.counts).reduce((n, c) => n + c.errors + c.warnings, 0)
      : data.counts[s].errors + data.counts[s].warnings;

  return (
    <>
      {sync && (
        <Section title="Синхронизация с Linko" hint={sync.dataAsOf ? `данные по ${dateTime(sync.dataAsOf)}` : "данных ещё нет"}>
          {sync.isRunning ? (
            <div className="flex flex-wrap items-center gap-3 text-sm">
              <Loader2 className="size-4 animate-spin text-accent-strong" />
              <span className="flex-1 text-ink">Идёт: {progressText(sync.progress)}</span>
              <CancelSyncButton cancelling={sync.isCancelling} onCancelled={() => setSync((s) => (s ? { ...s, isCancelling: true } : s))} />
            </div>
          ) : (
            <p className="text-sm text-ink-2">
              Сейчас не идёт.{" "}
              {sync.hasErrors ? <span className="text-warn">Последняя синхронизация завершилась с ошибкой — подробности ниже.</span> : "Последняя прошла без ошибок."}
            </p>
          )}
        </Section>
      )}

      <Section
        title="Журнал ошибок"
        hint={`за ${days} дн. · хранится 90 дней`}
        actions={
          <button className={button} onClick={() => void load()} disabled={busy}>
            <RefreshCw className={`size-3.5 ${busy ? "animate-spin" : ""}`} />
            Обновить
          </button>
        }
      >
        <div className="mb-4 flex flex-wrap items-center gap-2">
          {sources.map((s) => (
            <button
              key={s}
              type="button"
              onClick={() => setSource(s)}
              className={`inline-flex h-8 items-center gap-1.5 rounded-lg border px-3 text-xs font-medium ${
                source === s ? "border-accent bg-accent-soft text-accent-strong" : "border-line bg-surface text-ink-2 hover:bg-muted"
              }`}
            >
              {s === "all" ? "Все" : sourceLabels[s]}
              <span className="tabular-nums text-ink-3">{total(s)}</span>
            </button>
          ))}
          <select className={select} value={level} onChange={(e) => setLevel(e.target.value)} aria-label="Уровень">
            <option value="">Ошибки и предупреждения</option>
            <option value="error">Только ошибки</option>
            <option value="warning">Только предупреждения</option>
          </select>
          <select className={select} value={days} onChange={(e) => setDays(Number(e.target.value))} aria-label="Период">
            <option value={1}>Сутки</option>
            <option value={7}>7 дней</option>
            <option value={30}>30 дней</option>
            <option value={90}>90 дней</option>
          </select>
        </div>

        {error && <p className="mb-3 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}

        {data.items.length === 0 ? (
          <p className="rounded-lg border border-dashed border-line px-4 py-8 text-center text-sm text-ink-3">За выбранный период записей нет.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[760px] text-sm">
              <thead>
                <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                  <th className="w-6 py-2" />
                  <th className="py-2 pr-3 font-semibold">Время</th>
                  <th className="py-2 pr-3 font-semibold">Уровень</th>
                  <th className="py-2 pr-3 font-semibold">Источник</th>
                  <th className="py-2 font-semibold">Сообщение</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((e) => (
                  <Row key={e.id} entry={e} open={open === e.id} toggle={() => setOpen(open === e.id ? null : e.id)} />
                ))}
              </tbody>
            </table>
          </div>
        )}
        {data.hasMore && (
          <div className="mt-3">
            <button className={button} onClick={more} disabled={busy}>
              {busy && <Loader2 className="size-3.5 animate-spin" />}
              Показать ещё
            </button>
          </div>
        )}
        <Note>
          «Интеграция Linko» — ответы и доступность API Linko, проверка подключения, токены. «Синхронизация» — шаги загрузки данных в
          OneBase и отмены. «Система» — ошибки сервера OneBase. Токены, ключи и пароли из текста вырезаются при записи. Время — по Ташкенту.
        </Note>
      </Section>
    </>
  );
}

function Row({ entry: e, open, toggle }: { entry: LogEntry; open: boolean; toggle: () => void }) {
  const level = levelView[e.level] ?? levelView.Error;
  const Chevron = open ? ChevronDown : ChevronRight;
  return (
    <>
      <tr className="cursor-pointer border-b border-line align-top hover:bg-muted/50" onClick={toggle}>
        <td className="py-2 text-ink-3">
          <Chevron className="size-3.5" />
        </td>
        <td className="whitespace-nowrap py-2 pr-3 tabular-nums text-ink-2">{time(e.timestamp)}</td>
        <td className="py-2 pr-3">
          <span className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${level.className}`}>{level.label}</span>
        </td>
        <td className="whitespace-nowrap py-2 pr-3 text-ink-2">{sourceLabels[e.source] ?? e.source}</td>
        <td className={`py-2 text-ink ${open ? "" : "line-clamp-2"}`}>{e.message}</td>
      </tr>
      {open && (
        <tr className="border-b border-line bg-muted/40">
          <td />
          <td colSpan={4} className="py-3 pr-3 text-xs text-ink-2">
            <div className="mb-2 flex flex-wrap gap-x-6 gap-y-1">
              <span>
                Где: <span className="font-mono text-ink">{e.category}</span>
              </span>
              {e.traceId && (
                <span>
                  Запрос: <span className="font-mono text-ink">{e.traceId}</span>
                </span>
              )}
            </div>
            {e.exception ? (
              <pre className="max-h-80 overflow-auto whitespace-pre-wrap rounded-lg border border-line bg-surface p-3 font-mono text-[11px] leading-relaxed text-ink">
                {e.exception}
              </pre>
            ) : (
              <span className="text-ink-3">Без исключения — только сообщение.</span>
            )}
          </td>
        </tr>
      )}
    </>
  );
}
