"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { ArrowRight, CheckCircle2, Clock, Lightbulb, MessageSquareText, Send, Sparkles } from "lucide-react";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import {
  compact,
  date,
  dateTime,
  kpiValue,
  num,
  riskLabel,
  type Dashboard,
  type ForecastView,
  type Kpi,
} from "@/lib/analytik";
import { BarList, Card, Empty, HealthRing, InsightCard, KpiGrid, LineChart, Notes, PeriodPicker, SeverityBadge } from "./ui";

/** Главные показатели панели: из блоков отделов — то, что видно пользователю. */
function headline(d: Dashboard): Kpi[] {
  const pick = (kpis: Kpi[] | undefined, ...codes: string[]) => codes.map((c) => kpis?.find((k) => k.code === c)).filter((k): k is Kpi => !!k);
  const need30 = d.supply?.horizons.find((h) => h.days === 30);
  const supply: Kpi[] = d.supply
    ? [
        {
          code: "procurement.budget30",
          title: "Закупки на 30 дней",
          // Бюджет неизвестен, если ни у одной нужной позиции нет цены — «нет данных», а не ноль.
          value: need30 && need30.items > 0 && need30.itemsWithoutPrice === need30.items ? null : (need30?.budget ?? null),
          unit: "сум",
          previous: null,
          changePercent: null,
          kind: "calculated",
          source: "Аналитик → План закупок",
          href: "/analytik/procurement",
          note: `${need30?.items ?? 0} поз.${need30?.itemsWithoutPrice ? `, без цены — ${need30.itemsWithoutPrice}` : ""}`,
        },
        {
          code: "procurement.critical",
          title: "Сырьё в критичном дефиците",
          value: d.supply.risks.critical ?? 0,
          unit: "позиций",
          previous: null,
          changePercent: null,
          kind: "calculated",
          source: "Аналитик → План закупок",
          href: "/analytik/procurement",
          note: null,
        },
      ]
    : [];
  return [
    ...pick(d.sales?.kpis, "sales.revenue", "sales.kg", "sales.plan_execution", "sales.forecast_execution"),
    ...pick(d.finance?.kpis, "finance.payments", "finance.procurement_spend"),
    ...supply,
    ...pick(d.production?.kpis, "production.execution", "production.overdue_orders"),
    ...pick(d.hr?.kpis, "hr.late", "hr.absent_today"),
  ];
}

export function Overview({ data, canSales }: { data: Dashboard; canSales: boolean }) {
  const t = useT();
  const router = useRouter();
  const [question, setQuestion] = useState("");
  const [forecast, setForecast] = useState<ForecastView | null>(null);
  const [forecastError, setForecastError] = useState<string | null>(null);

  // Прогноз — отдельным запросом: история прогноза — полгода продаж, первый расчёт после запуска сервера дольше остальной панели.
  useEffect(() => {
    if (!canSales) return;
    let alive = true;
    bff<ForecastView>("analytik/forecast?metric=sales.revenue&granularity=day&horizon=30")
      .then((f) => alive && setForecast(f))
      .catch((e: Error) => alive && setForecastError(e.message));
    return () => {
      alive = false;
    };
  }, [canSales]);

  const kpis = headline(data);
  const daily = data.sales?.daily ?? [];
  const history = forecast?.history.slice(-45) ?? [];
  const chartLabels = forecast ? [...history.map((h) => date(h.date)), ...forecast.result.points.map((p) => date(p.date))] : daily.map((d) => date(d.date));
  const ask = (q: string) => router.push(`/analytik/chat?q=${encodeURIComponent(q)}&send=1`);

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <PeriodPicker period={data.period} />
        {data.scope && <span className="rounded-lg bg-accent-soft px-2.5 py-1 text-xs text-accent-strong">{t("Ваша область продаж")}: {data.scope}</span>}
      </div>

      {/* Вопрос аналитику — главный вход раздела. */}
      <form
        onSubmit={(e) => {
          e.preventDefault();
          if (question.trim()) ask(question.trim());
        }}
        className="flex items-center gap-2 rounded-2xl border border-line bg-surface p-2 pl-4"
      >
        <Sparkles className="size-4 shrink-0 text-accent-strong" />
        <input
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
          placeholder={t("Спросите аналитика: почему снизились продажи, сколько сырья закупить, какие риски сейчас…")}
          className="h-10 min-w-0 flex-1 bg-transparent text-sm text-ink outline-none placeholder:text-ink-4"
          aria-label={t("Вопрос аналитику")}
        />
        <button type="submit" disabled={!question.trim()} className="inline-flex h-10 items-center gap-1.5 rounded-xl bg-accent px-4 text-sm font-medium text-white disabled:opacity-40">
          <Send className="size-4" />
          <span className="max-sm:hidden">{t("Спросить")}</span>
        </button>
      </form>

      <div className="grid gap-4 xl:grid-cols-[minmax(0,340px)_minmax(0,1fr)]">
        <Card title={data.health.coverage ? "Состояние ваших разделов" : "Состояние компании"} subtitle={`${data.period.label} · ${t("на")} ${dateTime(data.at)}`}>
          <HealthRing health={data.health} />
          {data.health.coverage && <p className="mt-3 text-xs text-ink-4">{t(data.health.coverage)}</p>}
          {data.errors.length > 0 && <p className="mt-3 text-xs text-warn">{t("Не удалось посчитать")}: {data.errors.join(", ")}</p>}
        </Card>
        <Card title="Главные показатели" subtitle="Факт, расчёт и прогноз отмечены; нажмите — откроется источник">
          {kpis.length === 0 ? <Empty text="Нет показателей, доступных по вашим правам" /> : <KpiGrid kpis={kpis} columns="sm:grid-cols-3 2xl:grid-cols-4" compact />}
        </Card>
      </div>

      <div className="grid gap-4 xl:grid-cols-[minmax(0,1.6fr)_minmax(0,1fr)]">
        {canSales && (
          <Card title={forecast ? "Выручка: история и прогноз на 30 дней" : "Выручка по дням"} href="/analytik/forecasts">
            {forecast ? (
              <>
                <LineChart
                  labels={chartLabels}
                  series={[
                    { name: "Факт", values: [...history.map((h) => h.value), ...forecast.result.points.map(() => null)], color: "var(--color-accent)" },
                    {
                      name: "Прогноз",
                      values: [...history.map((_, i) => (i === history.length - 1 ? history[i].value : null)), ...forecast.result.points.map((p) => p.value)],
                      color: "var(--color-violet)",
                      dashed: true,
                    },
                  ]}
                  band={{ lower: [...history.map(() => null), ...forecast.result.points.map((p) => p.lower)], upper: [...history.map(() => null), ...forecast.result.points.map((p) => p.upper)] }}
                  format={(v) => compact(v)}
                />
                <p className="mt-2 text-xs text-ink-3">
                  {t("Прогноз на 30 дней")}: <b className="text-ink">{compact(forecast.result.total)} {t("сум")}</b> ({t("диапазон")} {compact(forecast.result.totalLower)} —{" "}
                  {compact(forecast.result.totalUpper)}){forecast.result.reliable ? "" : ` · ${t("ориентировочно")}`}
                </p>
              </>
            ) : daily.length > 0 ? (
              <LineChart labels={chartLabels} series={[{ name: "Выручка", values: daily.map((d) => d.revenue), color: "var(--color-accent)" }]} format={(v) => compact(v)} />
            ) : (
              <Empty text={forecastError ?? "Загружаю прогноз…"} />
            )}
            <Notes notes={data.sales?.notes} />
          </Card>
        )}
        <Card title="Критические риски" href="/analytik/risks" subtitle={`${data.risks.length} ${t("активных")}`}>
          {data.risks.length === 0 ? (
            <div className="flex items-center gap-2 text-sm text-ok">
              <CheckCircle2 className="size-4" />
              {t("Активных рисков нет")}
            </div>
          ) : (
            <div className="space-y-2">
              {data.risks.slice(0, 5).map((r) => (
                <InsightCard key={r.id} insight={r} />
              ))}
            </div>
          )}
        </Card>
      </div>

      <div className="grid gap-4 lg:grid-cols-2 2xl:grid-cols-3">
        {data.sales?.plan && (
          <Card title={`План продаж: ${t("месяц")} ${String(data.sales.plan.month).padStart(2, "0")}.${data.sales.plan.year}`} href="/analytik/sales"
            subtitle={`${t("данные по")} ${date(data.sales.plan.dataThrough)} · ${t("выполнение")} ${kpiValue(data.sales.plan.execution, "%")} · ${t("прогноз")} ${kpiValue(data.sales.plan.forecastExecution, "%")}`}>
            <BarList
              rows={data.sales.plan.regions
                .filter((r) => r.planKg)
                .sort((a, b) => (a.forecastExecution ?? 0) - (b.forecastExecution ?? 0))
                .slice(0, 8)
                .map((r) => ({
                  key: r.id,
                  label: r.name,
                  value: r.forecastExecution ?? 0,
                  text: kpiValue(r.forecastExecution, "%"),
                  tone: (r.forecastExecution ?? 0) >= 95 ? "ok" : (r.forecastExecution ?? 0) >= 80 ? "warn" : "bad",
                  sub: `${t("факт")} ${kpiValue(r.kg, "кг")} ${t("из")} ${kpiValue(r.planKg, "кг")}`,
                }))}
            />
          </Card>
        )}
        {data.supply && (
          <Card title="Что закупить в первую очередь" href="/analytik/procurement">
            {data.supply.top.length === 0 ? (
              <Empty text="Критичных и срочных закупок нет" />
            ) : (
              <ul className="divide-y divide-line">
                {data.supply.top.map((r) => (
                  <li key={r.itemId} className="flex items-start justify-between gap-3 py-2 text-[12.5px]">
                    <div className="min-w-0">
                      <p className="truncate font-medium text-ink">{r.name}</p>
                      <p className="text-[11px] text-ink-4">
                        {r.shortageDate ? `${t("закончится")} ${date(r.shortageDate)}` : t("нет в наличии")} · {t("заказать до")} {date(r.orderBy)}
                      </p>
                    </div>
                    <div className="shrink-0 text-right">
                      <SeverityBadge value={r.risk} label={riskLabel[r.risk]} />
                      <p className="mt-1 text-[11px] text-ink-3">
                        {num(r.horizons.find((h) => h.days === 30)?.required ?? 0, 1)} {r.unit}
                      </p>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </Card>
        )}
        <Card title="Рекомендации AI-аналитика" subtitle="Из найденных рисков — подтверждены данными">
          {data.recommendations.length === 0 ? (
            <Empty text="Срочных рекомендаций нет" />
          ) : (
            <ul className="space-y-2 text-[12.5px] text-ink-2">
              {data.recommendations.map((r) => (
                <li key={r} className="flex gap-2">
                  <Lightbulb className="mt-0.5 size-4 shrink-0 text-accent-strong" />
                  <span>{r}</span>
                </li>
              ))}
            </ul>
          )}
        </Card>
        <Card title="Свежесть данных">
          <ul className="space-y-1.5 text-[12.5px]">
            {data.freshness.map((f) => (
              <li key={f.source} className="flex items-center justify-between gap-3">
                <span className="inline-flex items-center gap-1.5 text-ink-2">
                  <Clock className={`size-3.5 ${f.status === "ok" ? "text-ok" : "text-warn"}`} />
                  {t(f.source)}
                </span>
                <span className={f.status === "ok" ? "text-ink-3" : "text-warn"}>{f.at ? dateTime(f.at) : t("не было")}</span>
              </li>
            ))}
          </ul>
        </Card>
        <Card title="Последние исследования" href="/analytik/history">
          {data.recent.length === 0 ? (
            <div className="space-y-2">
              <p className="text-xs text-ink-3">{t("Вы ещё не задавали вопросов аналитику. Попробуйте:")}</p>
              {["Проанализируй всю компанию за текущий месяц", "Какие проблемы сейчас существуют в компании?"].map((q) => (
                <button key={q} type="button" onClick={() => ask(q)} className="flex w-full items-center justify-between gap-2 rounded-lg border border-line px-3 py-2 text-left text-xs text-ink-2 hover:border-line-strong">
                  {t(q)}
                  <ArrowRight className="size-3.5 shrink-0" />
                </button>
              ))}
            </div>
          ) : (
            <ul className="space-y-1">
              {data.recent.map((r) => (
                <li key={r.id}>
                  <Link href={`/analytik/chat/${r.id}`} className="flex items-center gap-2 rounded-lg px-2 py-1.5 text-[12.5px] text-ink-2 hover:bg-muted">
                    <MessageSquareText className="size-3.5 shrink-0 text-ink-4" />
                    <span className="min-w-0 flex-1 truncate">{r.title}</span>
                    <span className="shrink-0 text-[11px] text-ink-4">{dateTime(r.at)}</span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>
    </div>
  );
}
