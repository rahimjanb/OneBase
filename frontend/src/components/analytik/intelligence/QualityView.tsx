"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { CircleAlert, FlaskConical, Loader2, PlayCircle, TriangleAlert } from "lucide-react";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { dateTime } from "@/lib/analytik";
import {
  duration,
  evidenceClass,
  hypothesisStatus,
  operationStatus,
  researchKinds,
  reviewIssueLabel,
  share,
  usd,
  type AnswerExplanation,
  type CountItem,
  type QualityReport,
} from "@/lib/intelligence";
import { Card, Empty } from "../ui";

function Tile({ label, value, hint, tone }: { label: string; value: string; hint?: string; tone?: "ok" | "warn" | "bad" }) {
  const t = useT();
  const color = tone === "bad" ? "text-bad" : tone === "warn" ? "text-warn" : tone === "ok" ? "text-ok" : "text-ink";
  return (
    <div className="min-w-0 rounded-2xl border border-line bg-surface p-3.5">
      <p className="text-[11px] text-ink-4">{t(label)}</p>
      <p className={`mt-1 text-xl font-semibold tabular-nums ${color}`}>{value}</p>
      {hint && <p className="mt-0.5 text-[11px] text-ink-3">{hint}</p>}
    </div>
  );
}

function Counts({ items, label, empty }: { items: CountItem[]; label?: (key: string) => string; empty: string }) {
  const t = useT();
  if (!items.length) return <p className="text-sm text-ink-3">{t(empty)}</p>;
  const max = Math.max(...items.map((i) => i.count));
  return (
    <ul className="grid gap-1.5">
      {items.map((i) => (
        <li key={i.key} className="grid grid-cols-[1fr_auto] items-center gap-2 text-sm">
          <div className="min-w-0">
            <p className="truncate text-ink-2" title={i.key}>{label ? t(label(i.key)) : i.key}</p>
            <div className="mt-0.5 h-1.5 rounded-full bg-muted">
              <div className="h-1.5 rounded-full bg-accent" style={{ width: `${Math.max(4, (i.count / max) * 100)}%` }} />
            </div>
          </div>
          <span className="tabular-nums text-ink-3">{i.count}</span>
        </li>
      ))}
    </ul>
  );
}

/**
 * «Аналитик → Качество»: доля решённых вопросов, неполные данные, ошибки планировщика срезов, расчётов и инструментов, шаги, время и стоимость
 * исследований, частота исправлений проверки, повторяющиеся причины отказов (слабые места) и прогоны набора сложных вопросов.
 */
export function QualityView({ report, days }: { report: QualityReport; days: number }) {
  const t = useT();
  const router = useRouter();
  const [busy, setBusy] = useState<"regression" | "live" | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [confirm, setConfirm] = useState(false);
  const [budget, setBudget] = useState("2");
  const { chat, investigations: inv, hypotheses, queries, computations } = report;

  const run = async (kind: "regression" | "live") => {
    setBusy(kind);
    setMessage(null);
    try {
      const r = await bff<{ passed: number; total: number; status: string; costUsd?: number | null; error?: string | null }>(`analytik/benchmark/${kind}`, {
        method: "POST",
        body: kind === "live" ? JSON.stringify({ confirm, cases: null, maxCostUsd: Number(budget.replace(",", ".")) || 2 }) : undefined,
      });
      setMessage(r.error ?? `${t("Прогон завершён")}: ${r.passed} / ${r.total}${r.costUsd != null ? ` · ${usd(r.costUsd)}` : ""}`);
      router.refresh();
    } catch (e) {
      setMessage(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap gap-1.5">
        {[7, 30, 90].map((d) => (
          <a key={d} href={`/analytik/quality?days=${d}`} className={`rounded-full px-3 py-1 text-xs font-medium ${d === days ? "bg-accent text-white" : "bg-muted text-ink-2 hover:bg-line"}`}>
            {t("{n} дней", { n: d })}
          </a>
        ))}
      </div>

      <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
        <Tile label="Ответов AI-аналитика" value={String(chat.answers)} hint={`${t("ушло в исследование")}: ${chat.investigated}`} />
        <Tile label="Решено полностью" value={share(chat.successRate)} tone={chat.successRate >= 0.8 ? "ok" : chat.successRate >= 0.6 ? "warn" : "bad"} />
        <Tile label="С неполными данными" value={share(chat.incompleteRate)} tone={chat.incompleteRate > 0.3 ? "warn" : undefined} />
        <Tile label="Среднее время ответа" value={duration(chat.avgDurationMs)} hint={`${t("стоимость")}: ${usd(chat.avgCostUsd)}`} />
        <Tile label="Исследований" value={String(inv.total)} hint={`${t("готово")} ${inv.completed} · ${t("частично")} ${inv.partial} · ${t("без данных")} ${inv.failed}`} />
        <Tile label="Шагов и глубина" value={`${inv.avgSteps} / ${inv.avgDepth}`} hint={`${t("вызовов модели")} ${inv.avgLlmCalls} · ${t("инструментов")} ${inv.avgToolCalls}`} />
        <Tile label="Возвраты проверки" value={share(inv.reviewRejectionRate)} hint={`${t("перепланирований")}: ${share(inv.replanRate)}`} tone={inv.reviewRejectionRate > 0.3 ? "warn" : undefined} />
        <Tile label="Ошибки срезов" value={share(queries.errorRate)} hint={`${queries.errors} ${t("из")} ${queries.total}`} tone={queries.errorRate > 0.1 ? "bad" : undefined} />
      </div>

      <Card title="Слабые места" subtitle="Повторяющиеся причины отказов и ошибок — с чего начинать улучшения">
        {report.weakSpots.length === 0 ? (
          <Empty text="Повторяющихся проблем нет" hint="Причины, встретившиеся хотя бы дважды, появятся здесь" />
        ) : (
          <ul className="grid gap-2">
            {report.weakSpots.map((w, i) => (
              <li key={i} className={`flex gap-2 rounded-xl px-3 py-2 text-sm ${w.severity === "critical" ? "bg-bad/10" : "bg-warn/10"}`}>
                {w.severity === "critical" ? <CircleAlert className="mt-0.5 size-4 shrink-0 text-bad" /> : <TriangleAlert className="mt-0.5 size-4 shrink-0 text-warn" />}
                <div className="min-w-0">
                  <p className="text-xs font-medium text-ink-3">{t(w.area)} · {w.count}×</p>
                  <p className="text-ink-2">{w.text}</p>
                </div>
              </li>
            ))}
          </ul>
        )}
      </Card>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card title="Гипотезы" subtitle={`${t("проверено")}: ${hypotheses.total}`}>
          <div className="grid gap-4 sm:grid-cols-2">
            <Counts items={hypotheses.byStatus} label={(k) => hypothesisStatus[k]?.label ?? k} empty="Гипотез не было" />
            <Counts items={hypotheses.byClass} label={(k) => evidenceClass[k] ?? k} empty="—" />
          </div>
        </Card>
        <Card title="Замечания проверки ответов">
          <Counts items={report.reviewIssues} label={reviewIssueLabel} empty="Замечаний не было" />
        </Card>
        <Card title="Ошибки срезов (планировщик)" subtitle="Одинаковые причины сложены вместе">
          <Counts items={queries.topErrors} empty="Ошибок не было" />
        </Card>
        <Card title="Расчёты" subtitle={`${t("всего")}: ${computations.total}`}>
          <div className="grid gap-4 sm:grid-cols-2">
            <Counts items={computations.byMethod} empty="Расчётов не было" />
            <Counts items={computations.topErrors} empty="Ошибок не было" />
          </div>
        </Card>
        <Card title="Сбои инструментов">
          <Counts items={report.toolErrors} empty="Сбоев не было" />
        </Card>
        <Card title="Исследования без данных">
          <Counts items={inv.failureReasons} empty="Таких не было" />
        </Card>
      </div>

      {report.research && report.research.total > 0 && (
        <Card title="Исследования без модели" subtitle={`${t("всего")}: ${report.research.total} · ${t("готово")} ${report.research.completed} · ${t("частично")} ${report.research.partial} · ${t("без данных")} ${report.research.failed} · ${t("отменено")} ${report.research.cancelled}`}>
          <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
            <Tile label="Среднее время" value={duration(report.research.avgDurationMs)} hint={`${t("срезы")} ${duration(report.research.avgSqlMs)} · ${t("расчёты")} ${duration(report.research.avgStatisticsMs)}`} />
            <Tile label="Операций на исследование" value={String(report.research.avgOperations)} />
            <Tile label="Срезов из кэша" value={share(report.research.cacheHitRatio)} />
            <Tile label="Повторов операций" value={String(report.research.retries)} />
          </div>
          <div className="mt-4 grid gap-4 md:grid-cols-3">
            <Counts items={report.research.byKind} label={(k) => researchKinds.find((x) => x.kind === k)?.title ?? k} empty="—" />
            <Counts items={report.research.operationStatuses} label={(k) => operationStatus[k]?.label ?? k} empty="—" />
            <Counts items={report.research.failedOperations} empty="Сбоев операций не было" />
          </div>
        </Card>
      )}

      <ExplainCard />

      <Card title="Набор сложных вопросов" subtitle="104 сценария по 14 категориям: регрессия — бесплатно, на текущих данных; живой прогон — вопросы модели (платно)">
        <div className="flex flex-wrap items-center gap-2">
          <button type="button" disabled={busy !== null} onClick={() => run("regression")} className="inline-flex h-9 items-center gap-1.5 rounded-lg bg-accent px-3 text-sm font-medium text-white hover:bg-accent-strong disabled:opacity-60">
            {busy === "regression" ? <Loader2 className="size-4 animate-spin" /> : <FlaskConical className="size-4" />}
            {t("Запустить регрессию")}
          </button>
          <label className="inline-flex items-center gap-1.5 text-xs text-ink-3">
            <input type="checkbox" checked={confirm} onChange={(e) => setConfirm(e.target.checked)} />
            {t("Понимаю: живой прогон — платные вызовы модели")}
          </label>
          <label className="inline-flex items-center gap-1 text-xs text-ink-3">
            {t("бюджет, $")}
            <input className="h-8 w-16 rounded-lg border border-line bg-surface px-2 text-sm" value={budget} onChange={(e) => setBudget(e.target.value)} inputMode="decimal" />
          </label>
          <button type="button" disabled={busy !== null || !confirm} onClick={() => run("live")} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-line px-3 text-sm font-medium text-ink-2 hover:bg-muted disabled:opacity-50">
            {busy === "live" ? <Loader2 className="size-4 animate-spin" /> : <PlayCircle className="size-4" />}
            {t("Живой прогон")}
          </button>
        </div>
        {message && <p className="mt-2 text-sm text-ink-2">{message}</p>}
        {report.benchmarks.length === 0 ? (
          <p className="mt-3 text-sm text-ink-3">{t("Прогонов ещё не было")}</p>
        ) : (
          <div className="mt-3 overflow-x-auto">
            <table className="w-full min-w-[480px] text-sm">
              <thead className="text-left text-[11px] text-ink-4">
                <tr>
                  <th className="py-1 font-medium">{t("Когда")}</th>
                  <th className="py-1 font-medium">{t("Вид")}</th>
                  <th className="py-1 font-medium">{t("Решено")}</th>
                  <th className="py-1 font-medium">{t("Оценка")}</th>
                  <th className="py-1 font-medium">{t("Стоимость")}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {report.benchmarks.map((b) => (
                  <tr key={b.id}>
                    <td className="py-1.5 text-ink-2">{dateTime(b.startedAt)}</td>
                    <td className="py-1.5 text-ink-2">{b.mode === "live" ? t("живой") : t("регрессия")}</td>
                    <td className="py-1.5 tabular-nums">{b.passed} / {b.total}</td>
                    <td className="py-1.5 tabular-nums">{share(b.score)}</td>
                    <td className="py-1.5 tabular-nums">{usd(b.costUsd)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </div>
  );
}

/** «Почему AI не ответил»: разбор ответа по id разговора — сбои инструментов, отклонённые срезы, расчёты, операции исследований. */
function ExplainCard() {
  const t = useT();
  const [id, setId] = useState("");
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<AnswerExplanation | null>(null);
  const [error, setError] = useState<string | null>(null);
  const run = async () => {
    const value = id.trim().split("/").pop() ?? "";
    if (!value) return;
    setBusy(true);
    setError(null);
    setResult(null);
    try {
      setResult(await bff<AnswerExplanation>(`analytik/quality/explain?conversationId=${encodeURIComponent(value)}`));
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };
  return (
    <Card title="Почему AI ответил так" subtitle="Разбор последнего ответа разговора: сбои инструментов и источников, отклонённые срезы, расчёты, операции исследований">
      <div className="flex flex-col gap-2 sm:flex-row">
        <input value={id} onChange={(e) => setId(e.target.value)} placeholder={t("id разговора или ссылка на чат")}
          className="h-9 min-w-0 flex-1 rounded-lg border border-line bg-surface px-2 text-sm" />
        <button type="button" disabled={busy || !id.trim()} onClick={run}
          className="inline-flex h-9 items-center justify-center gap-1.5 rounded-lg border border-line px-3 text-sm font-medium text-ink-2 hover:bg-muted disabled:opacity-50">
          {busy ? <Loader2 className="size-4 animate-spin" /> : <CircleAlert className="size-4" />}
          {t("Разобрать")}
        </button>
      </div>
      {error && <p className="mt-2 text-sm text-bad">{error}</p>}
      {result && (
        <div className="mt-3 grid gap-2 text-sm">
          <p className="text-ink-3">{dateTime(result.at)} · {result.question}</p>
          <p className="font-medium text-ink">{result.verdict}</p>
          {result.items.length > 0 ? (
            <ul className="grid gap-1.5">
              {result.items.map((x, i) => (
                <li key={i} className="flex flex-col gap-0.5 rounded-lg bg-muted/60 px-3 py-1.5 sm:flex-row sm:gap-2">
                  <span className="shrink-0 text-xs font-medium text-ink-3">{x.area} · {t(operationStatus[x.status]?.label ?? x.status)}</span>
                  <span className="text-ink-2">{x.text}</span>
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-ink-3">{t("Сбоев не найдено")}</p>
          )}
        </div>
      )}
    </Card>
  );
}
