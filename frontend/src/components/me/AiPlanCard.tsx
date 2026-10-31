"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ArrowUpRight, CheckCircle2, MessageCircle, RefreshCw, Sparkles } from "lucide-react";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import type { DailyPlan, PlanTone } from "@/lib/my-day";
import m from "./me.module.css";
import { Sk, focusRing } from "./parts";

const numberTone: Record<PlanTone, string> = {
  danger: "bg-bad-soft text-bad",
  warning: "bg-warn-soft text-warn",
  info: "bg-accent-soft text-link",
  ok: "bg-ok-soft text-ok",
};

/** Сколько раз спросить готовность плана (раз в 4 с) — дальше «готовится дольше обычного». */
const POLLS = 20;

const timeOf = (iso: string) =>
  new Intl.DateTimeFormat("ru-RU", { hour: "2-digit", minute: "2-digit", hourCycle: "h23", timeZone: "Asia/Tashkent" }).format(new Date(iso));

/**
 * «План на день от AI»: до трёх рекомендаций с номерами (цвет — по важности), каждая ведёт к задаче или событию. План хранится на сервере:
 * повторное открытие модель не вызывает; первый за день готовится в фоне — страница не ждёт (заготовка «Готовим план на день…»). «Обновить» —
 * не чаще раза в 15 минут. Задач нет — «Всё в порядке — задач пока нет». Внизу — вопрос AI-консультанту о своих задачах.
 */
export function AiPlanCard() {
  const t = useT();
  const router = useRouter();
  const [plan, setPlan] = useState<DailyPlan | null>(null);
  const [error, setError] = useState(false);
  const [note, setNote] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [question, setQuestion] = useState("");
  const [polls, setPolls] = useState(0);
  // «Обновить» доступно после refreshAt: время сверяется при ответе и по таймеру, а не при каждой отрисовке.
  const [locked, setLocked] = useState(false);

  const load = useCallback(async () => {
    try {
      setPlan(await bff<DailyPlan>("me/ai-plan"));
      setError(false);
    } catch {
      setError(true);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  // Пока план готовится — спрашиваем раз в 4 секунды.
  useEffect(() => {
    if (plan?.status !== "generating" || polls >= POLLS) return;
    const timer = window.setTimeout(() => {
      setPolls((n) => n + 1);
      void load();
    }, 4000);
    return () => window.clearTimeout(timer);
  }, [plan, polls, load]);

  useEffect(() => {
    const until = plan?.refreshAt ? new Date(plan.refreshAt).getTime() : 0;
    const check = () => setLocked(until > Date.now());
    const first = window.setTimeout(check, 0);
    const timer = until ? window.setInterval(check, 30_000) : undefined;
    return () => {
      window.clearTimeout(first);
      if (timer) window.clearInterval(timer);
    };
  }, [plan?.refreshAt]);

  const refresh = async () => {
    setBusy(true);
    setNote(null);
    setPolls(0);
    try {
      setPlan(await bff<DailyPlan>("me/ai-plan/refresh", { method: "POST" }));
    } catch (e) {
      setNote((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const ask = (e: React.FormEvent) => {
    e.preventDefault();
    const text = question.trim() || t("Какие мои задачи сделать сегодня в первую очередь?");
    router.push(`/consultant?ask=${encodeURIComponent(text)}`);
  };

  const generating = plan?.status === "generating";
  const items = plan?.items ?? [];
  const refreshLocked = locked;
  const slow = generating && polls >= POLLS;

  return (
    <section aria-label={t("План на день от AI")} aria-busy={generating || !plan} className={`rounded-[20px] border p-4 lg:rounded-2xl lg:p-5 ${m.ai}`}>
      <div className="flex items-start gap-3">
        <span aria-hidden className="ai-gradient grid size-10 shrink-0 place-items-center rounded-xl text-white">
          <Sparkles className="size-5" />
        </span>
        <div className="min-w-0 flex-1">
          <h2 className="text-[16px] font-semibold leading-tight text-ink lg:text-[17px]">{t("План на день от AI")}</h2>
          <p className="mt-0.5 text-[12.5px] text-ink-3">
            {generating
              ? items.length > 0
                ? t("Обновляем план…")
                : t("Готовим план на день…")
              : plan?.generatedAt && plan.status !== "unavailable"
                ? t("обновлено в {time}", { time: timeOf(plan.generatedAt) })
                : " "}
          </p>
        </div>
        <button
          type="button"
          onClick={refresh}
          disabled={busy || generating || refreshLocked || !plan || plan.status === "unavailable"}
          title={refreshLocked && plan?.refreshAt ? t("Обновить можно после {time}", { time: timeOf(plan.refreshAt) }) : t("Обновить план")}
          aria-label={t("Обновить план")}
          className={`grid size-11 shrink-0 place-items-center rounded-full border border-line text-ink-2 transition-colors hover:bg-surface-2 disabled:opacity-50 lg:size-9 ${focusRing}`}
        >
          <RefreshCw aria-hidden className={`size-4 ${busy || generating ? "animate-spin" : ""}`} />
        </button>
      </div>

      <div className="mt-4" aria-live="polite">
        {error ? (
          <p className="text-[13.5px] text-ink-3">
            {t("Рекомендации временно недоступны.")}{" "}
            <button type="button" onClick={() => void load()} className={`font-semibold text-link ${focusRing}`}>
              {t("Повторить")}
            </button>
          </p>
        ) : !plan || (generating && items.length === 0) ? (
          <div className="space-y-3">
            {[0, 1, 2].map((i) => (
              <div key={i} className="flex gap-3">
                <Sk className="size-6 shrink-0 rounded-full" />
                <Sk className="h-10 flex-1" />
              </div>
            ))}
            {slow && <p className="text-[12.5px] text-ink-3">{t("План готовится дольше обычного — загляните чуть позже.")}</p>}
          </div>
        ) : plan.status === "unavailable" ? (
          <p className="text-[13.5px] text-ink-3">{t(plan.message ?? "Рекомендации временно недоступны.")}</p>
        ) : (
          <ol className={`space-y-2.5 ${generating ? "opacity-60" : ""}`}>
            {items.map((item, i) => {
              const body = (
                <>
                  {item.tone === "ok" ? (
                    <CheckCircle2 aria-hidden className="mt-0.5 size-6 shrink-0 text-ok" />
                  ) : (
                    <span aria-hidden className={`grid size-6 shrink-0 place-items-center rounded-full text-[12px] font-bold ${numberTone[item.tone]}`}>{i + 1}</span>
                  )}
                  {/* Текст модели — как есть; готовые фразы сервера («Всё в порядке…») — на языке интерфейса. */}
                  <span className="text-[14px] leading-snug text-ink">{t(item.text)}</span>
                </>
              );
              return (
                <li key={i}>
                  {item.href ? (
                    <Link href={item.href} className={`-mx-2 flex gap-3 rounded-lg px-2 py-1.5 transition-colors hover:bg-white/5 ${focusRing}`}>
                      {body}
                    </Link>
                  ) : (
                    <div className="flex gap-3 py-1.5">{body}</div>
                  )}
                </li>
              );
            })}
          </ol>
        )}
        {plan?.message && plan.status === "stale" && <p className="mt-2 text-[12px] text-ink-3">{t(plan.message)}</p>}
        {note && <p className="mt-2 text-[12.5px] text-warn">{note}</p>}
      </div>

      <form onSubmit={ask} className={`mt-4 flex items-center gap-2 rounded-xl border border-line px-3 ${m.aiInput}`}>
        <MessageCircle aria-hidden className="size-4 shrink-0 text-ink-3" />
        <input
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
          maxLength={500}
          placeholder={t("Спросить AI о своих задачах…")}
          aria-label={t("Спросить AI о своих задачах")}
          className="min-h-[44px] min-w-0 flex-1 bg-transparent text-[14px] text-ink outline-none placeholder:text-ink-3"
        />
        <button type="submit" aria-label={t("Спросить")} className={`grid size-8 shrink-0 place-items-center rounded-lg text-ink-3 hover:text-ink ${focusRing}`}>
          <ArrowUpRight aria-hidden className="size-4" />
        </button>
      </form>
    </section>
  );
}
