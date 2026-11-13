"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useMemo, useRef, useState, useTransition } from "react";
import { ArrowLeftRight, BrainCircuit, Calendar, Download, Loader2, Mic, RefreshCw, Send, Square } from "lucide-react";
import { NotificationBell } from "@/components/shell/NotificationBell";
import { analytikTabs } from "@/components/shell/sections";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { periodKinds, periodQuery, type FreshnessRow, type Period, type PeriodKind, type ReportListRow } from "@/lib/analytik";
import { canRecognizeInBrowser, canRecord, recognizeInBrowser, transcribe, VoiceRecorder } from "@/lib/voice";
import { FreshnessList } from "../ui";
import { AiMark } from "./parts";

/** «12.11.2026, 21:53» (Ташкент); short — «12.11, 21:53». */
function stamp(iso: string, short = false): string {
  const t = new Date(iso);
  if (Number.isNaN(t.getTime())) return iso;
  const l = new Date(t.getTime() + 5 * 3600_000);
  const p = (n: number) => String(n).padStart(2, "0");
  const day = `${p(l.getUTCDate())}.${p(l.getUTCMonth() + 1)}`;
  return `${short ? day : `${day}.${l.getUTCFullYear()}`}, ${p(l.getUTCHours())}:${p(l.getUTCMinutes())}`;
}

/** Данные отстают: хотя бы один набор устарел, с ошибкой обмена или не подключён (несведённые планы — не повод). */
const lagging = (rows: FreshnessRow[]) => rows.some((r) => r.status === "stale" || r.status === "error" || r.status === "not_connected");

/** Точка и «Данные на …»; нажатие — свежесть по источникам (что отстаёт и почему, «Обновить Linko»). */
export function FreshnessBadge({ at, rows, short = false }: { at: string; rows: FreshnessRow[]; short?: boolean }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const box = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    const close = (e: MouseEvent) => !box.current?.contains(e.target as Node) && setOpen(false);
    const esc = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    document.addEventListener("mousedown", close);
    document.addEventListener("keydown", esc);
    return () => {
      document.removeEventListener("mousedown", close);
      document.removeEventListener("keydown", esc);
    };
  }, [open]);
  const stale = lagging(rows);
  return (
    <div ref={box} className="relative">
      <button
        type="button"
        onClick={() => setOpen(!open)}
        aria-expanded={open}
        title={stale ? t("Часть данных отстаёт — нажмите, чтобы увидеть какие") : t("Свежесть данных по источникам")}
        className={`inline-flex items-center gap-1.5 rounded-lg font-medium text-ink-3 hover:text-ink ${short ? "text-[11px]" : "text-[12px]"}`}
      >
        <span aria-hidden className={`size-1.5 shrink-0 rounded-full ${stale ? "bg-warn" : "bg-ok"}`} />
        {short ? t("данные на {at}", { at: stamp(at, true) }) : t("Данные на {at}", { at: stamp(at) })}
      </button>
      {open && (
        <div role="dialog" aria-label={t("Свежесть данных")} className="absolute left-0 z-40 mt-2 w-[min(360px,calc(100vw-32px))] rounded-2xl border border-line bg-surface-solid p-4 shadow-xl lg:left-auto lg:right-0">
          <p className="mb-1 text-[13px] font-bold text-ink">{t("Свежесть данных")}</p>
          <p className="mb-3 text-[11px] text-ink-4">{t("Аналитик следит за обменом и обновляет отстающие данные сам")}</p>
          <FreshnessList rows={rows} />
        </div>
      )}
    </div>
  );
}

const outlined =
  "inline-flex h-9 items-center gap-1.5 rounded-[10px] border border-line-strong bg-surface px-3 text-[12px] font-semibold text-ink-2 transition-colors hover:text-ink disabled:opacity-50";

/** Справа в шапке (компьютер): свежесть, «Обновить» (данные пересчитываются, иконка крутится) и «Отчёт PDF» за выбранный период. */
export function OverviewActions({ at, freshness, period }: { at: string; freshness: FreshnessRow[]; period: Period }) {
  const t = useT();
  const router = useRouter();
  const [pending, start] = useTransition();
  const [pdf, setPdf] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const report = async () => {
    setPdf(true);
    setError(null);
    try {
      const row = await bff<ReportListRow>("analytik/reports", {
        method: "POST",
        body: JSON.stringify({ kind: "health", period: period.kind, from: period.kind === "custom" ? period.from : null, to: period.kind === "custom" ? period.to : null }),
      });
      window.location.assign(`/bff/api/analytik/reports/${row.id}/pdf/${encodeURIComponent(row.title || "report")}.pdf`);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setPdf(false);
    }
  };
  return (
    <div className="flex flex-wrap items-center justify-end gap-2.5">
      <FreshnessBadge at={at} rows={freshness} />
      <button type="button" onClick={() => start(() => router.refresh())} disabled={pending} className={outlined}>
        <RefreshCw className={`size-4 ${pending ? "animate-spin" : ""}`} aria-hidden />
        {t("Обновить")}
      </button>
      <button type="button" onClick={() => void report()} disabled={pdf} className={outlined}>
        {pdf ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Download className="size-4" aria-hidden />}
        {t("Отчёт PDF")}
      </button>
      {error && <p className="basis-full text-right text-[11px] text-bad">{error}</p>}
    </div>
  );
}

/** Телефон и планшет: своя шапка вместо общей (знак, «Аналитик», свежесть, колокольчик) и разделы модуля чипами с прокруткой. */
export function MobileHeader({ at, freshness, permissions }: { at: string; freshness: FreshnessRow[]; permissions: readonly string[] }) {
  const t = useT();
  const tabs = useMemo(
    () => analytikTabs.filter((tab) => !tab.permission || (Array.isArray(tab.permission) ? tab.permission : [tab.permission]).some((p) => permissions.includes(p))),
    [permissions],
  );
  return (
    <div className="-mx-4 -mt-3.5 mb-1 sm:-mx-6 lg:hidden">
      <header className="flex min-h-14 items-center gap-2.5 px-4 pt-[env(safe-area-inset-top)] sm:px-6">
        <AiMark icon={BrainCircuit} size={36} radius={10} iconSize={18} />
        <div className="min-w-0 flex-1">
          <h1 className="text-[17px] font-bold leading-tight text-ink">{t("Аналитик")}</h1>
          <FreshnessBadge at={at} rows={freshness} short />
        </div>
        <NotificationBell permissions={permissions} />
      </header>
      <nav aria-label={t("Разделы модуля")} className="no-scrollbar flex gap-1.5 overflow-x-auto px-4 pb-1 pt-2 sm:px-6">
        {tabs.map((tab) => {
          const on = tab.key === "overview";
          return (
            <Link
              key={tab.key}
              href={tab.href}
              aria-current={on ? "page" : undefined}
              className={`inline-flex h-[34px] shrink-0 items-center gap-1.5 whitespace-nowrap rounded-[10px] px-[11px] text-[13px] ${on ? "bg-accent font-semibold text-white" : "border border-line bg-surface text-ink-2"}`}
            >
              <tab.icon className="size-3.5 shrink-0" aria-hidden strokeWidth={1.9} />
              {t(tab.label)}
            </Link>
          );
        })}
      </nav>
    </div>
  );
}

/**
 * Период: Сегодня / Вчера / Неделя / Месяц / Квартал / Год / «Период» (свои даты) и плашка сравнения. Период и даты — в адресе страницы
 * (данные пересчитывает сервер). Телефон — 5 сегментов, «Вчера» и свой период — кнопкой с календарём.
 */
export function PeriodBar({ period }: { period: Period }) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [custom, setCustom] = useState(false);
  const [from, setFrom] = useState(period.from);
  const [to, setTo] = useState(period.to);
  const go = (kind: PeriodKind, f?: string, tt?: string) => {
    const rest: Record<string, string | undefined> = {};
    params.forEach((v, k) => {
      if (!["period", "from", "to"].includes(k)) rest[k] = v;
    });
    setCustom(false);
    router.push(`${pathname}${periodQuery(kind, f, tt, rest)}`);
  };
  const segment = (on: boolean) =>
    `rounded-[9px] px-3 py-[7px] text-[13px] font-medium transition-colors ${on ? "bg-accent font-semibold text-white" : "text-ink-2 hover:text-ink"}`;
  const customForm = custom && (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        go("custom", from, to);
      }}
      className="absolute left-0 top-full z-30 mt-2 flex flex-wrap items-center gap-2 rounded-2xl border border-line bg-surface-solid p-3 shadow-xl"
    >
      <input type="date" value={from} max={to} onChange={(e) => setFrom(e.target.value)} className="h-9 rounded-lg border border-line bg-surface px-2 text-[13px] text-ink" aria-label={t("С")} />
      <span className="text-ink-4">—</span>
      <input type="date" value={to} min={from} onChange={(e) => setTo(e.target.value)} className="h-9 rounded-lg border border-line bg-surface px-2 text-[13px] text-ink" aria-label={t("По")} />
      <button type="submit" className="h-9 rounded-lg bg-accent px-3 text-[13px] font-semibold text-white">
        {t("Показать")}
      </button>
      <button type="button" onClick={() => go("yesterday")} className="h-9 rounded-lg border border-line px-3 text-[13px] text-ink-2 md:hidden">
        {t("Вчера")}
      </button>
    </form>
  );
  const compare = (
    <span title={t("Сравнение — с таким же периодом перед выбранным")} className="inline-flex min-w-0 items-center gap-2 rounded-[10px] border border-line bg-surface px-3 py-2 text-[12px]">
      <b className="font-semibold text-ink">{period.label}</b>
      <ArrowLeftRight className="size-3.5 shrink-0 text-ink-4" aria-hidden />
      <span className="truncate text-ink-3">
        {t("сравнение с")} {period.previousLabel}
      </span>
    </span>
  );
  return (
    <>
      {/* Компьютер и планшет */}
      <div className="flex flex-wrap items-center gap-3 max-md:hidden">
        <div className="relative">
          <div role="radiogroup" aria-label={t("Период")} className="inline-flex flex-wrap rounded-xl border border-line bg-surface p-1">
            {periodKinds.map((p) => {
              const on = p.kind === "custom" ? custom || period.kind === "custom" : !custom && period.kind === p.kind;
              return (
                <button key={p.kind} type="button" role="radio" aria-checked={on} onClick={() => (p.kind === "custom" ? setCustom(!custom) : go(p.kind))} className={segment(on)}>
                  {p.kind === "custom" ? (
                    <span className="inline-flex items-center gap-1.5">
                      <Calendar className="size-3.5" aria-hidden />
                      {t(p.label)}
                    </span>
                  ) : (
                    t(p.label)
                  )}
                </button>
              );
            })}
          </div>
          {customForm}
        </div>
        {compare}
      </div>

      {/* Телефон: 5 равных сегментов; «Вчера» и свои даты — кнопкой с календарём */}
      <div className="relative md:hidden">
        <div className="flex items-center gap-1.5">
          <div role="radiogroup" aria-label={t("Период")} className="grid min-w-0 flex-1 grid-cols-5 rounded-xl border border-line bg-surface p-[3px]">
            {periodKinds
              .filter((p) => !["yesterday", "custom"].includes(p.kind))
              .map((p) => {
                const on = period.kind === p.kind;
                return (
                  <button
                    key={p.kind}
                    type="button"
                    role="radio"
                    aria-checked={on}
                    onClick={() => go(p.kind)}
                    className={`h-8 truncate rounded-[9px] px-1 text-[12px] font-medium ${on ? "bg-accent font-semibold text-white" : "text-ink-2"}`}
                  >
                    {t(p.label)}
                  </button>
                );
              })}
          </div>
          <button
            type="button"
            onClick={() => setCustom(!custom)}
            aria-label={t("Другой период")}
            aria-expanded={custom}
            className={`grid size-[38px] shrink-0 place-items-center rounded-xl border ${period.kind === "custom" || period.kind === "yesterday" ? "border-accent bg-accent text-white" : "border-line bg-surface text-ink-2"}`}
          >
            <Calendar className="size-4" aria-hidden />
          </button>
        </div>
        {(period.kind === "custom" || period.kind === "yesterday") && <p className="mt-1.5 text-[11px] text-ink-3">{period.label}</p>}
        {customForm}
      </div>
    </>
  );
}

/** Голос вопроса: браузерное распознавание, где оно есть, иначе запись и распознавание на сервере; нет ни того ни другого — кнопки нет. */
function useVoice(onText: (text: string) => void) {
  const t = useT();
  const [state, setState] = useState<"idle" | "listening" | "transcribing">("idle");
  const [error, setError] = useState<string | null>(null);
  const [supported, setSupported] = useState(false);
  const stop = useRef<(() => void) | null>(null);
  const recorder = useRef<VoiceRecorder | null>(null);
  useEffect(() => setSupported(canRecognizeInBrowser() || canRecord()), []);
  const toggle = async () => {
    setError(null);
    if (state === "listening") {
      if (stop.current) {
        stop.current();
        return;
      }
      const rec = recorder.current;
      recorder.current = null;
      setState("transcribing");
      try {
        const blob = await rec?.stop();
        if (blob) onText(await transcribe(blob, t));
      } catch (e) {
        setError((e as Error).message);
      } finally {
        setState("idle");
      }
      return;
    }
    if (canRecognizeInBrowser()) {
      setState("listening");
      const handle = recognizeInBrowser(
        (text) => {
          stop.current = null;
          setState("idle");
          if (text) onText(text);
        },
        (message) => {
          stop.current = null;
          setState("idle");
          setError(message);
        },
        t,
      );
      stop.current = handle.stop;
      return;
    }
    try {
      const rec = new VoiceRecorder();
      await rec.start(t);
      recorder.current = rec;
      setState("listening");
    } catch (e) {
      setError((e as Error).message);
    }
  };
  return { supported, state, error, toggle };
}

/**
 * Вопрос AI-аналитику: поле, голос, «Спросить» и вопросы по текущим отклонениям. Enter, кнопка или чип открывают AI-аналитик с вопросом
 * (и выбранным периодом в вопросе) — он сразу отправляется.
 */
export function AskBar({ chips, periodLabel }: { chips: string[]; periodLabel: string | null }) {
  const t = useT();
  const router = useRouter();
  const [question, setQuestion] = useState("");
  const voice = useVoice((text) => setQuestion((q) => (q ? `${q} ${text}` : text)));
  const ask = (q: string) => {
    const text = q.trim();
    if (!text) return;
    const withPeriod = periodLabel && !/\d{4}|сегодня|вчера|недел|месяц|квартал|год/i.test(text) ? `${text} (${t("период")}: ${periodLabel})` : text;
    router.push(`/analytik/chat?q=${encodeURIComponent(withPeriod)}&send=1`);
  };
  return (
    <form
      aria-label={t("Вопрос AI-аналитику")}
      onSubmit={(e) => {
        e.preventDefault();
        ask(question);
      }}
      className="rounded-2xl border border-violet/25 bg-surface bg-[linear-gradient(90deg,rgb(139_92_246/0.10),rgb(61_123_255/0.03))] p-3.5 pl-[18px] max-md:p-3"
    >
      <div className="flex items-center gap-3 max-md:gap-2.5">
        <span className="max-md:hidden">
          <AiMark size={36} radius={10} iconSize={18} />
        </span>
        <span className="md:hidden">
          <AiMark size={34} radius={10} iconSize={17} />
        </span>
        {/* На телефоне длинная подсказка не помещается — своя короткая поверх поля. */}
        <div className="relative min-w-0 flex-1">
          <input
            value={question}
            onChange={(e) => setQuestion(e.target.value)}
            placeholder={t("Спросите аналитика: почему снизились продажи, сколько сырья закупить, какие риски сейчас…")}
            aria-label={t("Вопрос аналитику")}
            className="h-10 w-full bg-transparent text-[14px] text-ink outline-none placeholder:text-ink-4 max-md:placeholder:text-transparent"
          />
          {!question && (
            <span aria-hidden className="pointer-events-none absolute inset-y-0 left-0 flex items-center text-[14px] text-ink-4 md:hidden">
              {t("Спросите аналитика…")}
            </span>
          )}
        </div>
        {voice.supported && (
          <button
            type="button"
            onClick={() => void voice.toggle()}
            disabled={voice.state === "transcribing"}
            aria-label={voice.state === "listening" ? t("Остановить запись") : t("Сказать вопрос голосом")}
            aria-pressed={voice.state === "listening"}
            className={`grid size-10 shrink-0 place-items-center rounded-[10px] border transition-colors max-md:hidden ${voice.state === "listening" ? "border-bad/40 bg-bad-soft text-bad" : "border-line-strong bg-surface text-ink-2 hover:text-ink"}`}
          >
            {voice.state === "transcribing" ? <Loader2 className="size-4 animate-spin" aria-hidden /> : voice.state === "listening" ? <Square className="size-3.5" aria-hidden /> : <Mic className="size-4" aria-hidden />}
          </button>
        )}
        <button type="submit" disabled={!question.trim()} className="inline-flex h-10 shrink-0 items-center gap-1.5 rounded-[10px] bg-accent px-4 text-[13px] font-semibold text-white disabled:opacity-50 max-md:size-9 max-md:justify-center max-md:px-0">
          <Send className="size-4" aria-hidden />
          <span className="max-md:sr-only">{t("Спросить")}</span>
        </button>
      </div>
      {voice.error && <p className="mt-2 pl-12 text-[11px] text-bad max-md:pl-0">{voice.error}</p>}
      {chips.length > 0 && (
        <div className="mt-2.5 flex items-center gap-2 pl-12 max-md:no-scrollbar max-md:-mr-3 max-md:overflow-x-auto max-md:pl-0 max-md:pr-3">
          <span className="shrink-0 text-[12px] text-ink-4 max-md:hidden">{t("Часто спрашивают:")}</span>
          <div className="flex gap-2 md:flex-wrap">
            {chips.map((c) => (
              <button
                key={c}
                type="button"
                onClick={() => ask(c)}
                className="shrink-0 whitespace-nowrap rounded-lg border border-line bg-muted px-2.5 py-[5px] text-[12px] font-medium text-ink-2 hover:border-line-strong hover:text-ink max-md:text-[11px]"
              >
                {c}
              </button>
            ))}
          </div>
        </div>
      )}
    </form>
  );
}
