"use client";

import Link from "next/link";
import { useState } from "react";
import { ChevronDown, Loader2 } from "lucide-react";
import { Note } from "@/components/sales/bits";
import type { AiLogRow } from "@/lib/ai";
import { bff } from "@/lib/bff";
import { dateTime, num } from "@/lib/sales/format";
import { button } from "./form";

function Row({ row, agentName }: { row: AiLogRow; agentName: (code: string) => string }) {
  const [open, setOpen] = useState(false);
  const failed = row.status === "failed";
  return (
    <div className="border-b border-line last:border-0">
      <button type="button" onClick={() => setOpen((v) => !v)} className="flex w-full items-start gap-3 py-3 text-left">
        <span className={`mt-1.5 size-2 shrink-0 rounded-full ${failed ? "bg-bad" : "bg-ok"}`} />
        <span className="min-w-0 flex-1">
          <span className="block truncate text-sm font-medium text-ink">{row.question}</span>
          <span className="mt-0.5 block text-xs text-ink-3">
            {dateTime(row.createdAt)} · {row.user} · {row.kind === "consultant" ? "консультант" : "сотрудник"}
            {row.agents.length > 0 && ` · ${row.agents.map(agentName).join(", ")}`} · {num(row.inputTokens + row.outputTokens)} ток.
            {row.costUsd != null && ` · $${row.costUsd.toFixed(4)}`} · {num(row.durationMs / 1000, 1)} с
          </span>
        </span>
        <ChevronDown className={`mt-1 size-4 shrink-0 text-ink-3 transition-transform ${open ? "rotate-180" : ""}`} />
      </button>
      {open && (
        <div className="mb-3 ml-5 space-y-2 rounded-lg bg-muted/50 p-3 text-sm">
          <div>
            <span className="text-ink-3">Вопрос: </span>
            <span className="whitespace-pre-wrap text-ink">{row.question}</span>
          </div>
          {row.model && (
            <div>
              <span className="text-ink-3">Модель: </span>
              {row.model}
            </div>
          )}
          {row.tools.length > 0 && (
            <div>
              <span className="text-ink-3">Инструменты: </span>
              <span className="font-mono text-xs">{row.tools.join(", ")}</span>
            </div>
          )}
          {row.sources && row.sources.length > 0 && (
            <div>
              <span className="text-ink-3">Источники: </span>
              {row.sources.map((s, i) => (
                <span key={i}>
                  {i > 0 && "; "}
                  {s.href ? (
                    <Link href={s.href} className="text-accent-strong hover:underline">
                      {s.title}
                    </Link>
                  ) : (
                    s.title
                  )}
                  {s.period ? ` (${s.period})` : ""}
                </span>
              ))}
            </div>
          )}
          <div>
            <span className="text-ink-3">Токены: </span>
            {num(row.inputTokens)} вход · {num(row.outputTokens)} выход
          </div>
          {row.error && <div className="text-bad">Ошибка: {row.error}</div>}
          {row.responsePreview && (
            <div>
              <span className="text-ink-3">Ответ (начало): </span>
              <span className="whitespace-pre-wrap text-ink-2">{row.responsePreview}</span>
            </div>
          )}
          {row.conversationId && (
            <p className="text-xs text-ink-3">Полный ответ — в чате пользователя; чаты видит только их автор.</p>
          )}
        </div>
      )}
    </div>
  );
}

export function LogsView({ initial, agentNames }: { initial: AiLogRow[]; agentNames: Record<string, string> }) {
  const [rows, setRows] = useState(initial);
  const [status, setStatus] = useState<"" | "completed" | "failed">("");
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(initial.length < 50);
  const agentName = (code: string) => agentNames[code] ?? code;

  const load = async (next: typeof status, before?: string) => {
    setBusy(true);
    try {
      const q = new URLSearchParams({ take: "50" });
      if (next) q.set("status", next);
      if (before) q.set("before", before);
      const page = await bff<AiLogRow[]>(`ai/logs?${q}`);
      setRows((prev) => (before ? [...prev, ...page] : page));
      setDone(page.length < 50);
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <div className="mb-3 flex flex-wrap gap-2 text-sm">
        {(
          [
            ["", "Все"],
            ["completed", "Успешные"],
            ["failed", "С ошибкой"],
          ] as const
        ).map(([value, label]) => (
          <button
            key={value}
            type="button"
            onClick={() => {
              setStatus(value);
              void load(value);
            }}
            className={`rounded-full border px-3 py-1 ${status === value ? "border-accent bg-accent-soft text-accent-strong" : "border-line bg-surface text-ink-2 hover:text-ink"}`}
          >
            {label}
          </button>
        ))}
      </div>
      <div className="rounded-xl border border-line bg-surface px-4 sm:px-5">
        {rows.map((r) => (
          <Row key={r.id} row={r} agentName={agentName} />
        ))}
        {rows.length === 0 && <p className="py-8 text-center text-sm text-ink-3">Запросов к AI пока не было</p>}
      </div>
      {!done && rows.length > 0 && (
        <button className={`${button} mt-3`} onClick={() => load(status, rows[rows.length - 1].createdAt)} disabled={busy}>
          {busy && <Loader2 className="size-4 animate-spin" />}
          Показать ещё
        </button>
      )}
      <Note>
        Журнал — для контроля и отладки: кто и что спросил, какие AI-сотрудники, модели и инструменты работали, откуда данные, сколько токенов и времени
        ушло. Ключи API и внутренние инструкции в журнал не пишутся.
      </Note>
    </>
  );
}
