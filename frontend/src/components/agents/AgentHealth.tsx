"use client";

import { useCallback, useEffect, useState } from "react";
import { AlertTriangle, CheckCircle2, Info, RefreshCw, XCircle } from "lucide-react";
import { button } from "@/components/ai/form";
import { Card, ErrorText } from "@/components/recruitment/ui";
import { useT } from "@/i18n/client";
import { agentsApi, type AgentCheck, type AgentHealth as Health } from "@/lib/agents";
import { when } from "@/lib/recruitment";

const icon = {
  ok: { Icon: CheckCircle2, className: "text-ok", label: "в порядке" },
  info: { Icon: Info, className: "text-ink-3", label: "для сведения" },
  warn: { Icon: AlertTriangle, className: "text-warn", label: "внимание" },
  bad: { Icon: XCircle, className: "text-bad", label: "мешает ответу" },
} as const;

/** Итог проверки одной строкой: не ответит / ответит с оговорками / готов. */
function summary(checks: AgentCheck[]) {
  const bad = checks.filter((c) => c.status === "bad").length;
  if (bad > 0) return { text: "Агент сейчас не ответит — причин: {count}", count: bad, className: "bg-bad-soft text-bad" };
  if (checks.some((c) => c.status === "warn")) return { text: "Агент ответит, но есть замечания", count: 0, className: "bg-warn-soft text-warn" };
  return { text: "Всё готово: агент отвечает", count: 0, className: "bg-ok-soft text-ok" };
}

/**
 * «Почему агент может не отвечать»: проверки (включён, номер, кому новые диалоги, рабочее время, режим, вакансии, отправка) и последние
 * диалоги номера — куда ушли и что с ответом. Обновляется после сохранения настроек и номера (version) и по кнопке.
 */
export function AgentHealth({ agentId, version }: { agentId: string; version: string }) {
  const t = useT();
  const [health, setHealth] = useState<Health | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const load = useCallback(() => {
    setLoading(true);
    agentsApi
      .health(agentId)
      .then((h) => {
        setHealth(h);
        setError(null);
      })
      .catch((e: Error) => setError(e.message))
      .finally(() => setLoading(false));
  }, [agentId]);

  useEffect(() => {
    load();
  }, [load, version]);

  const s = health ? summary(health.checks) : null;
  return (
    <Card
      title="Почему агент может не отвечать"
      actions={
        <button className={`${button} inline-flex items-center gap-1.5`} disabled={loading} onClick={load}>
          <RefreshCw className={`size-4 ${loading ? "animate-spin" : ""}`} aria-hidden />
          {t("Проверить")}
        </button>
      }
    >
      {s && <p className={`mb-3 rounded-lg px-3 py-2 text-sm font-medium ${s.className}`}>{t(s.text, { count: s.count })}</p>}
      {health && (
        <ul className="space-y-2">
          {health.checks.map((c, i) => {
            const { Icon, className, label } = icon[c.status];
            return (
              <li key={i} className="flex items-start gap-2 text-sm">
                <Icon className={`mt-0.5 size-4 shrink-0 ${className}`} aria-label={t(label)} />
                <span className="min-w-0">
                  <span className="text-ink">{c.title}</span>
                  {c.detail && <span className="block text-xs text-ink-3">{c.detail}</span>}
                </span>
              </li>
            );
          })}
        </ul>
      )}

      {health && (
        <div className="mt-4 border-t border-line pt-3">
          <p className="mb-2 text-xs font-medium text-ink-2">{t("Последние диалоги номера")}</p>
          {health.dialogs.length === 0 ? (
            <p className="text-xs text-ink-3">{t("На номер агента пока никто не писал (или номер работает только на продажи — тогда диалоги не распределяются).")}</p>
          ) : (
            <ul className="divide-y divide-line">
              {health.dialogs.map((d) => (
                <li key={d.id} className="grid gap-x-3 gap-y-0.5 py-2 text-sm sm:grid-cols-[minmax(0,1fr)_minmax(0,1.4fr)]">
                  <span className="min-w-0">
                    <span className="block truncate text-ink">{d.name}</span>
                    <span className="block text-xs tabular-nums text-ink-3">
                      {when(d.at)} · {d.accountName}
                    </span>
                  </span>
                  <span className="min-w-0">
                    <span className={`block ${d.ours ? "text-ink" : "text-ink-3"}`}>{d.outcome}</span>
                    <span className="block text-xs text-ink-3">
                      {d.department} — {d.reason}
                    </span>
                  </span>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
      {!health && !error && <p className="text-sm text-ink-3">{t("Проверяю…")}</p>}
      <ErrorText error={error} />
    </Card>
  );
}
