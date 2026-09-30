import Link from "next/link";
import { ArrowRight, ArrowUp, CircleAlert, Clock, Plus, Sparkle } from "lucide-react";
import { DepartmentStatusCard } from "@/components/departments";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { Card, SectionTitle, SelectField } from "@/components/ui";
import { categoryNames, type AiAlertsView } from "@/lib/ai";
import { attentionItems, departments, kpis } from "@/lib/demo-data";
import { apiTry } from "@/lib/server-api";

const trendStyles = {
  up: { icon: ArrowUp, className: "text-ok" },
  today: { icon: Clock, className: "text-ok" },
  warn: { icon: CircleAlert, className: "text-warn" },
} as const;

export default async function DashboardPage() {
  // Карточка AI — реальные находки проактивного анализа (если у пользователя есть доступ к консультанту).
  const alerts = await apiTry<AiAlertsView>("/api/ai/alerts");
  const top = alerts?.alerts.filter((a) => a.severity !== "Opportunity").slice(0, 2) ?? [];
  return (
    <>
      <PageHeader title="Обзор компании" subtitle="Единый центр управления всеми подразделениями" />
      <PageBody>
        <div className="flex flex-wrap items-end gap-4">
          <SelectField label="Период" options={["Последние 30 дней", "Последние 7 дней", "Квартал", "Год"]} />
          <SelectField label="Отдел" options={["Все отделы", ...departments.map((d) => d.name)]} />
          <SelectField label="Ответственный" options={["Все сотрудники", "Иван Иванов", "Анна Петрова", "Мария Орлова"]} />
          <span className="flex items-center gap-2 pb-3 text-xs text-ink-2 sm:ml-auto">
            <span className="size-2 rounded-full bg-ink-3" />
            Обновлено 5 минут назад
          </span>
        </div>

        <div className="mt-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          {kpis.map((kpi) => {
            const trend = trendStyles[kpi.tone];
            return (
              <Card key={kpi.label} className="p-5">
                <div className="text-sm text-ink-2">{kpi.label}</div>
                <div className="mt-3 text-[32px] font-semibold leading-none tracking-tight">{kpi.value}</div>
                <div className={`mt-4 flex items-center gap-1.5 text-xs ${trend.className}`}>
                  <trend.icon className="size-3.5" />
                  {kpi.trend}
                </div>
              </Card>
            );
          })}
        </div>

        <div className="mt-10">
          <SectionTitle
            title="Состояние отделов"
            subtitle="Показатели и состояние рабочих пространств"
            action={
              <Link href="/departments" className="flex items-center gap-1.5 text-sm font-medium text-accent-strong hover:underline">
                Все отделы <ArrowRight className="size-3.5" />
              </Link>
            }
          />
        </div>

        <div className="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {departments.map((d) => (
            <DepartmentStatusCard key={d.code} department={d} />
          ))}
        </div>

        <div className="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          <button
            type="button"
            className="flex min-h-[64px] items-center justify-center gap-2 rounded-xl border border-line bg-surface/60 text-sm font-medium text-accent-strong transition-colors hover:border-accent/40 hover:bg-surface xl:self-start"
          >
            <Plus className="size-4" />
            Добавить отдел
          </button>

          <Card className="p-5">
            <h3 className="font-semibold">Требует внимания</h3>
            <ul className="mt-3 space-y-2.5">
              {attentionItems.map((item) => (
                <li key={item.department} className="flex items-center gap-3 text-sm text-ink">
                  <span className="size-2 shrink-0 rounded-full bg-warn" />
                  <span>
                    {item.department} · {item.text}
                  </span>
                </li>
              ))}
            </ul>
          </Card>

          <div className="rounded-xl border border-accent/25 bg-accent-soft p-5 md:col-span-2 xl:col-span-1">
            <div className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wide text-accent-strong">
              <Sparkle className="size-3.5" />
              AI-консультант
            </div>
            {alerts === null ? (
              <p className="mt-3 text-sm leading-relaxed text-ink">Задайте вопрос о компании — консультант привлечёт AI-сотрудников отделов.</p>
            ) : top.length === 0 ? (
              <p className="mt-3 text-sm leading-relaxed text-ink">Проактивный анализ не нашёл проблем в данных OneBase.</p>
            ) : (
              <ul className="mt-3 space-y-2 text-sm leading-relaxed text-ink">
                {top.map((a) => (
                  <li key={a.id}>
                    <span className={a.severity === "Critical" ? "font-semibold text-bad" : "font-semibold text-warn"}>{categoryNames[a.category] ?? a.category}: </span>
                    {a.title}
                  </li>
                ))}
              </ul>
            )}
            {alerts && alerts.summary.problems > 0 && (
              <p className="mt-2 text-xs text-ink-2">
                Проблем: {alerts.summary.problems}, критических: {alerts.summary.critical}, возможностей: {alerts.summary.opportunities}
              </p>
            )}
            <Link href={alerts ? "/ai" : "/consultant"} className="mt-3 inline-flex items-center gap-1.5 text-sm font-semibold text-accent-strong hover:underline">
              {alerts ? "Открыть AI Dashboard" : "Открыть консультанта"} <ArrowRight className="size-3.5" />
            </Link>
          </div>
        </div>

        <p className="mt-8 text-xs text-ink-3">
          Единое рабочее пространство · Отделы можно добавлять по мере роста компании
        </p>
      </PageBody>
    </>
  );
}
