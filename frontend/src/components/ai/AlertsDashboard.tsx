"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { AlertTriangle, ArrowRight, Lightbulb, Loader2, MessageSquare, RefreshCw, TriangleAlert } from "lucide-react";
import { useT } from "@/i18n/client";
import { alertText, categoryNames, type AiAlert, type AiAlertsView } from "@/lib/ai";
import { bff } from "@/lib/bff";
import { dateTime } from "@/lib/sales/format";
import { button } from "./form";

const severityView = {
  Critical: { label: "Критично", icon: AlertTriangle, className: "bg-bad-soft text-bad", border: "border-l-bad" },
  Warning: { label: "Внимание", icon: TriangleAlert, className: "bg-warn-soft text-warn", border: "border-l-warn" },
  Opportunity: { label: "Возможность", icon: Lightbulb, className: "bg-ok-soft text-ok", border: "border-l-ok" },
} as const;

function Tile({ label, value, tone }: { label: string; value: number; tone?: "bad" | "ok" }) {
  return (
    <div className="rounded-xl border border-line bg-surface p-5">
      <div className="text-sm text-ink-2">{label}</div>
      <div className={`mt-2 text-[2rem] font-semibold leading-none tracking-tight ${tone === "bad" ? "text-bad" : tone === "ok" ? "text-ok" : "text-ink"}`}>
        {value}
      </div>
    </div>
  );
}

function AlertCard({ alert }: { alert: AiAlert }) {
  const t = useT();
  const view = severityView[alert.severity];
  // Находку пишет сервер по-русски — показывается на языке интерфейса (шаблоны словаря, единицы).
  const title = alertText(alert.title, t);
  const message = alertText(alert.message, t);
  const ask = t("Разбери находку: «{title}». {message} Что делать?", { title, message });
  return (
    <div className={`rounded-xl border border-l-4 border-line bg-surface p-4 ${view.border}`}>
      <div className="flex flex-wrap items-center gap-2">
        <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-medium ${view.className}`}>
          <view.icon className="size-3" />
          {t(view.label)}
        </span>
        <span className="rounded-full bg-muted px-2 py-0.5 text-[11px] text-ink-2">{categoryNames[alert.category] ? t(categoryNames[alert.category]) : alert.category}</span>
        <span className="ml-auto text-[11px] text-ink-3">{t("с {date}", { date: dateTime(alert.detectedAt) })}</span>
      </div>
      <div className="mt-2 font-semibold text-ink">{title}</div>
      <p className="mt-1 text-sm text-ink-2">{message}</p>
      {alert.recommendation && (
        <p className="mt-2 text-sm text-ink">
          <span className="font-medium">{t("Рекомендация:")} </span>
          {alertText(alert.recommendation, t)}
        </p>
      )}
      <div className="mt-3 flex flex-wrap gap-3 text-sm">
        {alert.href && (
          <Link href={alert.href} className="inline-flex items-center gap-1 font-medium text-accent-strong hover:underline">
            {t("Открыть данные")} <ArrowRight className="size-3.5" />
          </Link>
        )}
        <Link href={`/consultant?ask=${encodeURIComponent(ask)}`} className="inline-flex items-center gap-1 font-medium text-accent-strong hover:underline">
          <MessageSquare className="size-3.5" /> {t("Спросить консультанта")}
        </Link>
      </div>
    </div>
  );
}

export function AlertsDashboard({ initial, canRun }: { initial: AiAlertsView; canRun: boolean }) {
  const t = useT();
  const router = useRouter();
  const [data, setData] = useState(initial);
  const [category, setCategory] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const run = async () => {
    setBusy(true);
    setError(null);
    try {
      await bff("ai/alerts/run", { method: "POST" });
      setData(await bff<AiAlertsView>("ai/alerts"));
      router.refresh();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const shown = data.alerts.filter((a) => !category || a.category === category);
  const categories = data.summary.byCategory;

  return (
    <>
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <Tile label={t("Обнаружено проблем")} value={data.summary.problems} />
        <Tile label={t("Критических")} value={data.summary.critical} tone={data.summary.critical > 0 ? "bad" : undefined} />
        <Tile label={t("Рекомендаций")} value={data.summary.recommendations} />
        <Tile label={t("Новых возможностей")} value={data.summary.opportunities} tone={data.summary.opportunities > 0 ? "ok" : undefined} />
      </div>

      <div className="mt-6 flex flex-wrap items-center gap-2">
        <button
          type="button"
          onClick={() => setCategory(null)}
          className={`rounded-full border px-3 py-1 text-sm ${category === null ? "border-accent bg-accent-soft text-accent-strong" : "border-line bg-surface text-ink-2 hover:text-ink"}`}
        >
          {t("Все")} · {data.alerts.length}
        </button>
        {categories.map((c) => {
          const count = c.problems + c.opportunities;
          return (
            <button
              key={c.category}
              type="button"
              onClick={() => setCategory(c.category)}
              disabled={count === 0}
              className={`rounded-full border px-3 py-1 text-sm disabled:opacity-40 ${category === c.category ? "border-accent bg-accent-soft text-accent-strong" : "border-line bg-surface text-ink-2 hover:text-ink"}`}
            >
              {categoryNames[c.category] ? t(categoryNames[c.category]) : categoryNames[c.category]} · {count}
              {c.critical > 0 && <span className="ml-1 text-bad">{t("({n} крит.)", { n: c.critical })}</span>}
            </button>
          );
        })}
        <span className="ml-auto flex items-center gap-3 text-xs text-ink-3">
          {data.lastRun ? t("Проверено {date}", { date: dateTime(data.lastRun.at) }) : t("Проверка по расписанию — раз в час")}
          {canRun && (
            <button className={button} onClick={run} disabled={busy}>
              {busy ? <Loader2 className="size-4 animate-spin" /> : <RefreshCw className="size-4" />}
              {t("Проверить сейчас")}
            </button>
          )}
        </span>
      </div>
      {error && <p className="mt-2 text-sm text-bad">{t(error)}</p>}
      {data.lastRun && data.lastRun.errors.length > 0 && (
        <p className="mt-2 text-sm text-warn">{t("Не всё проверено: {errors}", { errors: data.lastRun.errors.map((e) => t(e)).join("; ") })}</p>
      )}

      <div className="mt-4 grid gap-3 xl:grid-cols-2">
        {shown.map((a) => (
          <AlertCard key={a.id} alert={a} />
        ))}
      </div>
      {shown.length === 0 && (
        <p className="mt-6 rounded-xl border border-dashed border-line p-6 text-center text-sm text-ink-2">
          {data.alerts.length === 0 ? t("Находок нет: проверка не нашла отклонений или ещё не запускалась.") : t("В этом отделе находок нет.")}
        </p>
      )}
      <p className="mt-4 text-xs text-ink-3">
        {t(
          "Находки ищут правила по данным OneBase (без модели — цифры точные): снижение продаж к тем же дням прошлого месяца, план, дефицит и неликвид на складах, проблемные агенты, ассортимент. Для финансов, маркетинга, HR и производства данных в OneBase пока нет — находок по ним не будет.",
        )}
      </p>
    </>
  );
}
