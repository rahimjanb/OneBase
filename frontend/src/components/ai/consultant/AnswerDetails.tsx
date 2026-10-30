"use client";

import Link from "next/link";
import { ShieldAlert, ShieldCheck } from "lucide-react";
import { useT } from "@/i18n/client";
import { agentState, type AgentTone } from "@/lib/agent-state";
import { periodText, type ConsultantDetails } from "@/lib/consultant";

/** «Как получен ответ»: данные и период, почему эти AI-сотрудники, перепроверка после возражения, карточки сотрудников. */
export function DetailsPanel({ details }: { details: ConsultantDetails }) {
  const t = useT();
  const sources = details.sources;
  return (
    <div className="mt-3 space-y-3 rounded-2xl border border-line bg-surface p-4">
      {sources.length > 0 && (
        <div className="text-xs">
          <p className="mb-1 text-ink-3">{t("Данные ответа:")}</p>
          <ul className="space-y-0.5">
            {sources.map((s, i) => (
              <li key={i} className="text-ink-2">
                {s.href ? (
                  <Link href={s.href} className="hover:text-link hover:underline">
                    {t(s.title)}
                  </Link>
                ) : (
                  t(s.title)
                )}
                {s.period ? <span className="text-ink-3"> · {periodText(s.period, t)}</span> : null}
              </li>
            ))}
          </ul>
        </div>
      )}
      {details.routingReason && <p className="text-xs text-ink-2">{t("Почему эти AI-сотрудники: {reason}", { reason: t(details.routingReason) })}</p>}
      {(details.memoryUsed ?? 0) > 0 && <p className="text-xs text-ink-3">{t("учтены прошлые анализы: {n}", { n: details.memoryUsed ?? 0 })}</p>}
      {details.usedFallback && details.model && <p className="text-xs text-ink-3">{t("Модель: {model}", { model: details.model })} {t("(резервная)")}</p>}
      {details.correction && <CorrectionCard review={details.correction} />}
      {details.agents.map((a) => (
        <AgentCard key={a.agent} result={a} />
      ))}
    </div>
  );
}

const verdicts: Record<string, { label: string; className: string }> = {
  confirmed: { label: "прошлый ответ подтверждён данными", className: "bg-ok-soft text-ok" },
  corrected: { label: "прошлый ответ исправлен по данным", className: "bg-warn-soft text-warn" },
  insufficient: { label: "данных для проверки недостаточно", className: "bg-surface-2 text-ink-2" },
};

/**
 * Проверка ответа по данным: числа ответа сверены с результатами инструментов (сколько) или какие не подтверждены; если вопрос оспаривал
 * прошлый ответ — итог перепроверки.
 */
export function Grounding({ details }: { details: ConsultantDetails }) {
  const t = useT();
  const g = details.grounding;
  const verdict = details.correction ? verdicts[details.correction.verdict] : null;
  if (!verdict && !(g && g.checked > 0)) return null;
  return (
    <span className="inline-flex flex-wrap items-center gap-2">
      {verdict && <span className={`rounded-full px-2 py-0.5 font-medium ${verdict.className}`}>{t("Перепроверка: {verdict}", { verdict: t(verdict.label) })}</span>}
      {g && g.checked > 0 && (
        <span
          className={`inline-flex items-center gap-1 ${g.warned ? "text-warn" : "text-ok"}`}
          title={g.revised ? t("Первый вариант ответа содержал числа, которых нет в данных, — ответ исправлен") : undefined}
        >
          {g.warned ? <ShieldAlert className="size-3.5" aria-hidden /> : <ShieldCheck className="size-3.5" aria-hidden />}
          {g.warned ? t("не подтверждено данными: {list}", { list: g.unsupported.join(", ") }) : t("чисел сверено с данными: {n}", { n: g.supported })}
        </span>
      )}
    </span>
  );
}

/** Перепроверка после возражения: какие числа прошлого ответа и пользователя подтвердили свежие данные. */
function CorrectionCard({ review }: { review: NonNullable<ConsultantDetails["correction"]> }) {
  const t = useT();
  const rows: [string, string[], string][] = [
    ["Прошлый ответ — подтверждено данными", review.previousConfirmed, "text-ok"],
    ["Прошлый ответ — не подтверждено", review.previousNotConfirmed, "text-warn"],
    ["Ваши значения — подтверждено данными", review.userConfirmed, "text-ok"],
    ["Ваши значения — не подтверждено", review.userNotConfirmed, "text-warn"],
  ];
  return (
    <div className="rounded-xl border border-line bg-surface px-4 py-3 text-xs">
      <div className="text-sm font-semibold text-ink">
        {t("Перепроверка по данным")}
        {review.attempt > 1 && <span className="ml-2 font-normal text-ink-3">{t("возражение подряд: {n}", { n: review.attempt })}</span>}
      </div>
      <p className="mt-1 text-ink-2">{t("Возражение не меняет ответ само по себе: AI-сотрудники заново получили данные, итог посчитан по ним.")}</p>
      <dl className="mt-2 grid gap-1 sm:grid-cols-2">
        {rows
          .filter(([, items]) => items.length > 0)
          .map(([label, items, tone]) => (
            <div key={label}>
              <dt className="text-ink-3">{t(label)}</dt>
              <dd className={`font-medium ${tone}`}>{items.join(", ")}</dd>
            </div>
          ))}
      </dl>
    </div>
  );
}

/** Статусы, при которых итог сотрудника написала модель. */
const answered = new Set(["completed", "partial", "no_data"]);

const toneClass: Record<AgentTone, string> = {
  ok: "bg-ok-soft text-ok",
  warn: "bg-warn-soft text-warn",
  muted: "bg-muted text-ink-2",
  bad: "bg-bad-soft text-bad",
};

export function AgentCard({ result }: { result: ConsultantDetails["agents"][number] }) {
  const t = useT();
  const section = (title: string, items: string[]) =>
    items.length > 0 && (
      <div className="mt-2">
        <div className="text-[11px] font-semibold uppercase tracking-wide text-ink-3">{title}</div>
        <ul className="mt-1 list-disc space-y-0.5 pl-5 text-sm text-ink-2 marker:text-ink-3">
          {items.map((x, i) => (
            <li key={i}>{x}</li>
          ))}
        </ul>
      </div>
    );
  // Подпись — по фактической причине: «не найдено», «период недоступен», «нет в источниках OneBase», «сбой источника» — не одно «нет данных».
  const state = agentState(result.status, result.reason);
  return (
    <div className="rounded-xl border border-line bg-surface px-4 py-3">
      <div className="flex items-center justify-between gap-2">
        <span className="text-sm font-semibold text-ink">{t(result.agentName)}</span>
        <span className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${toneClass[state.tone]}`}>{t(state.label)}</span>
      </div>
      {/* Итог пишет модель; у неотработавшего сотрудника (выключен, нет доступа) — текст сервера, он переводится. У сбоя — только пояснение:
          ни слов модели «данных нет», ни текста ошибки сервера. */}
      {state.showSummary && result.summary && <p className="mt-1 text-sm text-ink-2">{answered.has(result.status) ? result.summary : t(result.summary)}</p>}
      {state.note ? (
        <p className="mt-1 text-sm text-bad">{t(state.note)}</p>
      ) : (
        result.error && <p className={`mt-1 text-sm ${state.tone === "bad" ? "text-bad" : "text-ink-2"}`}>{t(result.error)}</p>
      )}
      {result.metrics.length > 0 && (
        <div className="mt-2 flex flex-wrap gap-2">
          {result.metrics.map((m, i) => (
            <span key={i} className="rounded-lg bg-muted px-2.5 py-1 text-xs text-ink-2">
              {m.name}: <b className="font-semibold text-ink">{m.value}</b>
              {m.unit ? ` ${m.unit}` : ""}
            </span>
          ))}
        </div>
      )}
      {section(t("Факты"), result.findings)}
      {section(t("Проблемы"), result.problems)}
      {section(t("Рекомендации"), result.recommendations)}
      {(result.unverified?.length ?? 0) > 0 && (
        <p className="mt-2 flex items-start gap-1.5 text-xs text-warn">
          <ShieldAlert className="mt-px size-3.5 shrink-0" aria-hidden />
          {t("Не подтверждено результатами инструментов: {list}", { list: result.unverified!.join(", ") })}
        </p>
      )}
      {result.toolsUsed.length > 0 && <p className="mt-2 text-[11px] text-ink-3">{t("Инструменты: {list}", { list: result.toolsUsed.join(", ") })}</p>}
    </div>
  );
}
