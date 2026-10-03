import Link from "next/link";
import { ArrowRight, ArrowUp, CircleAlert, Clock, Sparkle } from "lucide-react";
import { DepartmentStatusCard } from "@/components/departments";
import { PageBody } from "@/components/shell/PageHeader";
import { Card, SectionTitle, SelectField } from "@/components/ui";
import { categoryNames, type AiAlertsView } from "@/lib/ai";
import { departments, focusOfDay, kpis } from "@/lib/demo-data";
import { greeting, todayLabel } from "@/lib/format";
import { apiSession, apiTry } from "@/lib/server-api";
import type { Me } from "@/lib/users";

export const metadata = { title: "Обзор · OneBase" };

const trendStyles = {
  up: { icon: ArrowUp, className: "text-ok" },
  today: { icon: Clock, className: "text-ok" },
  warn: { icon: CircleAlert, className: "text-warn" },
} as const;

export default async function DashboardPage() {
  // Приветствие по имени и «Фокус дня» — реальные находки проактивного анализа, если есть доступ к консультанту.
  const [me, alerts] = await Promise.all([apiSession<Me>("/api/auth/me"), apiTry<AiAlertsView>("/api/ai/alerts")]);
  const name = me?.firstName || me?.name || me?.login;
  const problems = alerts?.alerts.filter((a) => a.severity !== "Opportunity").slice(0, 2) ?? [];

  return (
    <PageBody>
      <div className="text-[11px] font-semibold uppercase tracking-[0.08em] text-ink-3">{todayLabel()}</div>
      <h1 className="mt-1.5 text-[26px] font-semibold leading-tight tracking-tight text-ink sm:text-[28px]">
        {greeting()}
        {name ? `, ${name}` : ""}!
      </h1>
      <p className="mt-1.5 text-sm text-ink-2">Вот что происходит в компании сегодня.</p>

      <div className="mt-6 flex flex-wrap items-center gap-3">
        <SelectField options={["Последние 30 дней", "Последние 7 дней", "Квартал", "Год"]} className="max-lg:w-full" />
        <SelectField options={["Все отделы", ...departments.map((d) => d.name)]} className="max-lg:w-full" />
        <SelectField options={["Все сотрудники", "Иван Иванов", "Анна Петрова", "Мария Орлова"]} className="max-lg:w-full" />
        <span className="flex items-center gap-2 text-xs text-ink-3">
          <span className="size-1.5 rounded-full bg-ink-3" />
          Обновлено 5 минут назад
        </span>
      </div>

      <div className="mt-5 grid grid-cols-2 gap-3 sm:gap-4 xl:grid-cols-4">
        {kpis.map((kpi) => {
          const trend = trendStyles[kpi.tone];
          return (
            <Card key={kpi.label} className="p-5 max-lg:p-4">
              <div className="text-sm text-ink-2">{kpi.label}</div>
              <div className="mt-3 text-[32px] font-semibold leading-none tracking-tight max-lg:text-2xl">{kpi.value}</div>
              <div className={`mt-4 flex items-center gap-1.5 text-xs ${trend.className}`}>
                <trend.icon className="size-3.5" />
                {kpi.trend}
              </div>
            </Card>
          );
        })}
      </div>

      <div className="mt-8">
        <SectionTitle
          title="Состояние отделов"
          subtitle="Показатели и динамика команд"
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

      <div className="mt-6 flex flex-wrap items-center gap-x-5 gap-y-3 rounded-xl border border-accent/20 bg-accent-soft px-5 py-4">
        <Sparkle className="size-5 shrink-0 text-accent-strong" />
        <div className="min-w-0 flex-1">
          <div className="text-sm font-semibold text-ink">Фокус дня</div>
          <p className="mt-0.5 text-sm text-ink-2">
            {alerts === null ? (
              focusOfDay
            ) : problems.length === 0 ? (
              "Проактивный анализ не нашёл проблем в данных OneBase."
            ) : (
              problems.map((a, i) => (
                <span key={a.id}>
                  {i > 0 && " · "}
                  <span className={a.severity === "Critical" ? "font-semibold text-bad" : "font-semibold text-warn"}>{categoryNames[a.category] ?? a.category}: </span>
                  {a.title}
                </span>
              ))
            )}
          </p>
        </div>
        <Link href={alerts ? "/ai" : "/consultant"} className="inline-flex items-center gap-1.5 text-sm font-semibold text-accent-strong hover:underline">
          {alerts ? "Открыть обзор" : "Открыть консультанта"} <ArrowRight className="size-3.5" />
        </Link>
      </div>
    </PageBody>
  );
}
