"use client";

import Link from "next/link";
import { useState } from "react";
import { ChevronDown, Loader2 } from "lucide-react";
import { Note } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import { userLabel, type AiLogRow } from "@/lib/ai";
import { periodText } from "@/lib/consultant";
import { bff } from "@/lib/bff";
import { dateTime, num } from "@/lib/sales/format";
import { button } from "./form";

function Row({ row, agentName }: { row: AiLogRow; agentName: (code: string) => string }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const failed = row.status === "failed";
  return (
    <div className="border-b border-line last:border-0">
      <button type="button" onClick={() => setOpen((v) => !v)} className="flex w-full items-start gap-3 py-3 text-left">
        <span className={`mt-1.5 size-2 shrink-0 rounded-full ${failed ? "bg-bad" : "bg-ok"}`} />
        <span className="min-w-0 flex-1">
          <span className="block truncate text-sm font-medium text-ink">{row.question}</span>
          <span className="mt-0.5 block text-xs text-ink-3">
            {dateTime(row.createdAt)} · {userLabel(row.user, t)} · {row.kind === "consultant" ? t("консультант") : t("сотрудник")}
            {row.agents.length > 0 && ` · ${row.agents.map(agentName).join(", ")}`} · {t("{n} ток.", { n: num(row.inputTokens + row.outputTokens) })}
            {row.costUsd != null && ` · $${row.costUsd.toFixed(4)}`} · {t("{s} с", { s: num(row.durationMs / 1000, 1) })}
          </span>
        </span>
        <ChevronDown className={`mt-1 size-4 shrink-0 text-ink-3 transition-transform ${open ? "rotate-180" : ""}`} />
      </button>
      {open && (
        <div className="mb-3 ml-5 space-y-2 rounded-lg bg-muted/50 p-3 text-sm">
          <div>
            <span className="text-ink-3">{t("Вопрос:")} </span>
            <span className="whitespace-pre-wrap text-ink">{row.question}</span>
          </div>
          {row.model && (
            <div>
              <span className="text-ink-3">{t("Модель:")} </span>
              {row.model}
            </div>
          )}
          {row.tools.length > 0 && (
            <div>
              <span className="text-ink-3">{t("Инструменты:")} </span>
              <span className="font-mono text-xs">{row.tools.join(", ")}</span>
            </div>
          )}
          {row.sources && row.sources.length > 0 && (
            <div>
              <span className="text-ink-3">{t("Источники:")} </span>
              {row.sources.map((s, i) => (
                <span key={i}>
                  {i > 0 && "; "}
                  {s.href ? (
                    <Link href={s.href} className="text-accent-strong hover:underline">
                      {t(s.title)}
                    </Link>
                  ) : (
                    t(s.title)
                  )}
                  {s.period ? ` (${periodText(s.period, t)})` : ""}
                </span>
              ))}
            </div>
          )}
          <div>
            <span className="text-ink-3">{t("Токены:")} </span>
            {t("{input} вход · {output} выход", { input: num(row.inputTokens), output: num(row.outputTokens) })}
          </div>
          {row.error && <div className="text-bad">{t("Ошибка: {error}", { error: t(row.error) })}</div>}
          {row.grounding?.check && row.grounding.check.checked > 0 && (
            <div>
              <span className="text-ink-3">{t("Проверка по данным:")} </span>
              {t("подтверждено чисел: {n} из {m}", { n: row.grounding.check.supported, m: row.grounding.check.checked })}
              {row.grounding.check.revised && ` · ${t("ответ исправлялся")}`}
              {row.grounding.check.unsupported.length > 0 && (
                <span className="text-warn"> · {t("не подтверждено: {list}", { list: row.grounding.check.unsupported.join(", ") })}</span>
              )}
            </div>
          )}
          {row.grounding?.correction && (
            <div>
              <span className="text-ink-3">{t("Возражение пользователя:")} </span>
              {t(verdictLabel[row.grounding.correction.verdict] ?? row.grounding.correction.verdict)}
              {row.grounding.correction.attempt > 1 && ` · ${t("подряд: {n}", { n: row.grounding.correction.attempt })}`}
              {row.grounding.correction.userNotConfirmed.length > 0 &&
                ` · ${t("значения пользователя не подтверждены: {list}", { list: row.grounding.correction.userNotConfirmed.join(", ") })}`}
            </div>
          )}
          {row.responsePreview && (
            <div>
              <span className="text-ink-3">{t("Ответ (начало):")} </span>
              <span className="whitespace-pre-wrap text-ink-2">{row.responsePreview}</span>
            </div>
          )}
          {row.conversationId && (
            <p className="text-xs text-ink-3">{t("Полный ответ — в чате пользователя; чаты видит только их автор.")}</p>
          )}
        </div>
      )}
    </div>
  );
}

/** Итог перепроверки после возражения пользователя. */
const verdictLabel: Record<string, string> = {
  confirmed: "прошлый ответ подтверждён данными",
  corrected: "прошлый ответ исправлен по данным",
  insufficient: "данных для проверки недостаточно",
};

export function LogsView({ initial, agentNames }: { initial: AiLogRow[]; agentNames: Record<string, string> }) {
  const t = useT();
  const [rows, setRows] = useState(initial);
  const [status, setStatus] = useState<"" | "completed" | "failed">("");
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(initial.length < 50);
  const agentName = (code: string) => t(agentNames[code] ?? code);

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
            {t(label)}
          </button>
        ))}
      </div>
      <div className="rounded-xl border border-line bg-surface px-4 sm:px-5">
        {rows.map((r) => (
          <Row key={r.id} row={r} agentName={agentName} />
        ))}
        {rows.length === 0 && <p className="py-8 text-center text-sm text-ink-3">{t("Запросов к AI пока не было")}</p>}
      </div>
      {!done && rows.length > 0 && (
        <button className={`${button} mt-3`} onClick={() => load(status, rows[rows.length - 1].createdAt)} disabled={busy}>
          {busy && <Loader2 className="size-4 animate-spin" />}
          {t("Показать ещё")}
        </button>
      )}
      <Note>
        {t(
          "Журнал — для контроля и отладки: кто и что спросил, какие AI-сотрудники, модели и инструменты работали, откуда данные, сколько токенов и времени ушло. Ключи API и внутренние инструкции в журнал не пишутся.",
        )}
      </Note>
    </>
  );
}
