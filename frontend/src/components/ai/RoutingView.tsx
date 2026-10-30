"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Loader2, Play } from "lucide-react";
import { Note, Section } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import type { AiRoutingView } from "@/lib/ai";
import { bff } from "@/lib/bff";
import { dateTime, num } from "@/lib/sales/format";
import { primary } from "./form";

const usd = (v: number) => `$${v < 1 ? v.toFixed(5) : num(v, 2)}`;
const pct = (v: number) => `${num(v * 100, 1)}%`;
const sec = (ms: number) => (ms < 1000 ? `${num(ms)} мс` : `${num(ms / 1000, 1)} с`);

const categoryNames: Record<string, string> = { route: "Маршрутизация", extract: "Извлечение JSON", answer: "Модели ответа (прогноз)" };
const sourceNames: Record<string, string> = { shadow: "задачи пользователей (фон)", eval: "эталонный набор" };
const modeNames: Record<string, string> = { off: "Выкл", shadow: "Сравнение", on: "Включено" };

function Table({ head, rows, empty }: { head: string[]; rows: (string | number)[][]; empty: string }) {
  return (
    <div className="-mx-4 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
      <table className="w-full min-w-max text-sm">
        <thead>
          <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
            {head.map((h, i) => (
              <th key={h} className={`py-2 pr-3 font-semibold ${i > 1 ? "text-right" : ""}`}>
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((r, i) => (
            <tr key={i} className="border-b border-line last:border-0">
              {r.map((c, j) => (
                <td key={j} className={`py-2 pr-3 align-top ${j > 1 ? "text-right tabular-nums" : "text-ink"}`}>
                  {c}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
      {rows.length === 0 && <p className="py-4 text-center text-sm text-ink-3">{empty}</p>}
    </div>
  );
}

/**
 * «Модели по задачам»: какая модель сейчас выполняет простые задачи, итоги сравнения исходной модели и модели категории (на задачах
 * пользователей в режиме «Сравнение» и на эталонном наборе), запуск проверки на эталонном наборе и расхождения для разбора.
 */
export function RoutingView({ initial }: { initial: AiRoutingView }) {
  const t = useT();
  const [data, setData] = useState(initial);
  const [error, setError] = useState<string | null>(null);
  const [starting, setStarting] = useState(false);
  const run = data.evaluation;
  const running = run?.status === "running";

  // Пока проверка идёт — обновлять итоги раз в 3 секунды.
  useEffect(() => {
    if (!running) return;
    const timer = window.setInterval(async () => {
      try {
        setData(await bff<AiRoutingView>(`ai/routing?days=${data.days}`));
      } catch {
        /* следующая попытка через 3 секунды */
      }
    }, 3000);
    return () => window.clearInterval(timer);
  }, [running, data.days]);

  const start = async () => {
    setStarting(true);
    setError(null);
    try {
      await bff("ai/routing/evaluate", { method: "POST" });
      setData(await bff<AiRoutingView>(`ai/routing?days=${data.days}`));
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setStarting(false);
    }
  };

  const s = data.settings;
  const line = (label: string, model: string | null, mode: string, route: AiRoutingView["settings"]["route"]) =>
    `${t(label)}: ${
      !s.taskRoutingEnabled || mode === "off" || !model
        ? t("основная модель ({model})", { model: s.primary ?? "—" })
        : mode === "shadow"
          ? t("основная модель, {model} — в фоне для сравнения", { model })
          : t("{model}, при сбое — {fallback}", { model: route.model ?? model, fallback: route.fallback ?? "—" })
    } · ${t(modeNames[mode] ?? mode)}`;

  return (
    <>
      <div className="mb-4 rounded-lg bg-muted px-4 py-3 text-sm text-ink-2">
        <div className="font-semibold text-ink">
          {s.taskRoutingEnabled ? t("Отдельные модели для простых задач включены") : t("Отдельные модели для простых задач выключены — всё идёт основной моделью")}
        </div>
        <div className="mt-1">{line("Маршрутизация", s.router, s.routerMode, s.route)}</div>
        <div>{line("Извлечение JSON", s.extract, s.extractMode, s.extractRoute)}</div>
        <Link href="/settings/ai" className="mt-1 inline-block font-medium text-accent-strong hover:underline">
          {t("Изменить в «Общие»")}
        </Link>
      </div>

      <Section title={t("Проверка на эталонном наборе")} hint={t("{n} обезличенных задач маршрутизации", { n: data.evalCases })}>
        <p className="mb-3 text-sm text-ink-2">
          {t(
            "Каждая задача — основной моделью и моделью маршрутизации, с одинаковыми сообщениями, без повторов и резервной модели. Ответы пользователей не меняются. Это реальные вызовы API: стоимость видна ниже и в «Использование».",
          )}
        </p>
        <div className="flex flex-wrap items-center gap-3">
          <button className={primary} onClick={start} disabled={starting || running || !s.router}>
            {starting || running ? <Loader2 className="size-4 animate-spin" /> : <Play className="size-4" />}
            {running ? t("Идёт проверка: {done} из {total}", { done: run!.done, total: run!.total }) : t("Запустить проверку")}
          </button>
          {!s.router && <span className="text-sm text-warn">{t("Сначала выберите «Модель маршрутизации» в «Общие».")}</span>}
          {error && <span className="text-sm text-bad">{error}</span>}
          {run?.status === "failed" && <span className="text-sm text-bad">{run.error}</span>}
        </div>
        {run && run.baseline && run.candidate && (
          <div className="mt-4">
            <Table
              head={[t("Модель"), t("Роль"), t("Верно"), t("JSON корректен"), t("Ошибки"), t("Вход"), t("Кэш-чтение"), t("Выход"), t("Среднее время"), t("P50 / P95")]}
              rows={[run.baseline, run.candidate].map((m, i) => [
                m.model,
                i === 0 ? t("исходная") : t("кандидат"),
                `${m.correct} / ${m.cases}`,
                `${m.valid} / ${m.cases}`,
                num(m.errors),
                num(m.inputTokens),
                num(m.cacheReadTokens),
                num(m.outputTokens),
                sec(m.avgMs),
                m.p95Ms ? `${sec(m.p50Ms ?? 0)} / ${sec(m.p95Ms)}` : "—",
              ])}
              empty=""
            />
            <p className="mt-2 text-xs text-ink-3">
              {t("Выборы совпали в {n} из {total} задач.", { n: run.agree, total: run.done })}{" "}
              {run.finishedAt && t("Проверка завершена {date}.", { date: dateTime(run.finishedAt) })}
            </p>
          </div>
        )}
      </Section>

      <Section title={t("Итоги сравнения")} hint={t("за {n} дней", { n: data.days })}>
        <Table
          head={[t("Категория"), t("Источник"), t("Задач"), t("Совпало"), t("Верно: исходная"), t("Верно: кандидат"), t("JSON кандидата"), t("Стоимость: исходная"), t("Стоимость: кандидат"), t("Время: исходная"), t("Время: кандидат")]}
          rows={data.groups.map((g) => [
            `${t(categoryNames[g.category] ?? g.category)} · ${g.candidateModel}`,
            t(sourceNames[g.source] ?? g.source),
            num(g.count),
            pct(g.agree),
            g.graded ? pct(g.baselineCorrect) : "—",
            g.graded ? pct(g.candidateCorrect) : "—",
            pct(g.candidateValid),
            `${usd(g.baseline.costUsd)}${g.baseline.costIncomplete ? " *" : ""}`,
            `${usd(g.candidate.costUsd)}${g.candidate.costIncomplete ? " *" : ""}`,
            sec(g.baseline.avgMs),
            sec(g.candidate.avgMs),
          ])}
          empty={t("Сравнений пока нет: включите режим «Сравнение» в «Общие» или запустите проверку на эталонном наборе.")}
        />
      </Section>

      <Section title={t("Последние сравнения")} hint={t("сначала расхождения")}>
        <Table
          head={[t("Задача"), t("Ожидалось"), t("Исходная"), t("Кандидат"), t("Совпало"), t("Когда")]}
          rows={data.recent.map((r) => [
            r.task ?? r.caseCode ?? "—",
            r.expected ?? "—",
            `${r.baselineResult ?? "—"}${r.baselineCorrect === false ? ` ✗` : r.baselineCorrect ? " ✓" : ""}`,
            `${r.candidateError && !r.candidateValid ? r.candidateError : (r.candidateResult ?? "—")}${r.candidateCorrect === false ? " ✗" : r.candidateCorrect ? " ✓" : ""}`,
            r.agree ? t("да") : t("нет"),
            dateTime(r.createdAt),
          ])}
          empty={t("Сравнений пока нет")}
        />
      </Section>

      <Note>
        {t(
          "Пустой выбор у маршрутизации — консультант отвечает сам. Стоимость — по ценам моделей из «Модели» (вход, выход, кэш); * — у модели не задана цена. Включайте модель задачи («Включено» в «Общие»), только если она не хуже основной на эталонном наборе и на задачах пользователей; вернуть основную — режим «Выкл» или общий переключатель.",
        )}
      </Note>
    </>
  );
}
