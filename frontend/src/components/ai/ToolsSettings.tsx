"use client";

import Link from "next/link";
import { useState } from "react";
import { ChevronDown, Loader2, Play } from "lucide-react";
import { Note, Section } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import type { AiToolView } from "@/lib/ai";
import { bff } from "@/lib/bff";
import { periodText, type DataSource } from "@/lib/consultant";
import { num } from "@/lib/sales/format";
import { button, field } from "./form";

type RunResult = { success: boolean; content: string; sources: DataSource[] | null; elapsedMs: number };

function pretty(text: string) {
  try {
    return JSON.stringify(JSON.parse(text), null, 2);
  } catch {
    return text;
  }
}

function ToolRow({ tool, agentName }: { tool: AiToolView; agentName: (code: string) => string }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const [args, setArgs] = useState("{}");
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<RunResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const params = Object.entries(tool.inputSchema.properties ?? {});

  const run = async () => {
    setError(null);
    let parsed: unknown;
    try {
      parsed = JSON.parse(args || "{}");
    } catch {
      setError(t('Аргументы — JSON-объект, например {"month": 8}'));
      return;
    }
    setBusy(true);
    try {
      setResult(await bff<RunResult>(`ai/tools/${tool.name}/run`, { method: "POST", body: JSON.stringify({ arguments: parsed }) }));
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="border-b border-line py-3 last:border-0">
      <button type="button" onClick={() => setOpen((v) => !v)} className="flex w-full items-start gap-3 text-left">
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-baseline gap-x-2">
            <span className="font-medium text-ink">{t(tool.title)}</span>
            <span className="font-mono text-xs text-ink-3">{tool.name}</span>
          </div>
          <p className="mt-0.5 text-sm text-ink-2">{t(tool.description)}</p>
          <div className="mt-1.5 flex flex-wrap gap-1.5">
            {tool.agents.length === 0 && <span className="text-xs text-ink-3">{t("ни одному агенту не разрешён")}</span>}
            {tool.agents.map((a) => (
              <span key={a} className="rounded-full bg-muted px-2 py-0.5 text-[11px] text-ink-2">
                {agentName(a)}
              </span>
            ))}
          </div>
        </div>
        <ChevronDown className={`mt-1 size-4 shrink-0 text-ink-3 transition-transform ${open ? "rotate-180" : ""}`} />
      </button>
      {open && (
        <div className="mt-3 rounded-lg bg-muted/50 p-3">
          {params.length > 0 && (
            <ul className="mb-2 space-y-0.5 text-xs text-ink-2">
              {params.map(([name, p]) => (
                <li key={name}>
                  <code className="font-mono text-ink">{name}</code> ({p.type}) — {p.description && t(p.description)}
                </li>
              ))}
            </ul>
          )}
          <div className="flex flex-wrap items-center gap-2">
            <input className={`${field} max-w-md font-mono text-xs`} value={args} onChange={(e) => setArgs(e.target.value)} aria-label={t("Аргументы")} spellCheck={false} />
            <button className={button} onClick={run} disabled={busy}>
              {busy ? <Loader2 className="size-4 animate-spin" /> : <Play className="size-4" />}
              {t("Проверить")}
            </button>
          </div>
          {error && <p className="mt-2 text-sm text-bad">{t(error)}</p>}
          {result && (
            <div className="mt-3">
              <p className={`text-xs ${result.success ? "text-ink-3" : "text-bad"}`}>
                {result.success ? t("Результат") : t("Инструмент вернул ошибку")} · {t("{n} символов", { n: num(result.content.length) })} ·{" "}
                {t("{ms} мс", { ms: num(result.elapsedMs) })}
                {result.sources?.map((s, i) => (
                  <span key={i}>
                    {" · "}
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
              </p>
              <pre className="mt-1 max-h-96 overflow-auto rounded-lg border border-line bg-surface p-3 font-mono text-[11px] leading-relaxed text-ink">
                {/* Данные инструмента — как есть; ошибка (неверный аргумент, нет месяца) — текст сервера, он переводится. */}
                {result.success ? pretty(result.content).slice(0, 20000) : t(result.content)}
              </pre>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

export function ToolsSettings({ tools, agents }: { tools: AiToolView[]; agents: { code: string; name: string }[] }) {
  const t = useT();
  const agentName = (code: string) => t(agents.find((a) => a.code === code)?.name ?? code);
  const groups = [...new Set(tools.map((tool) => tool.source))];
  return (
    <>
      {groups.map((source) => {
        const list = tools.filter((tool) => tool.source === source);
        return (
          <Section key={source} title={list[0].sourceName ? t(list[0].sourceName) : source} hint={t("{n} инстр.", { n: list.length })}>
            {list.map((tool) => (
              <ToolRow key={tool.name} tool={tool} agentName={agentName} />
            ))}
          </Section>
        );
      })}
      <Note>
        {t(
          "Все инструменты только читают данные и считают их теми же сервисами, что страницы OneBase. Инструмент выполняется с правами пользователя, задавшего вопрос: без права на данные AI их не получит. Какие инструменты разрешены агенту — в разделе «Агенты».",
        )}
      </Note>
    </>
  );
}
