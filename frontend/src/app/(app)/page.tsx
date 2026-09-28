import Link from "next/link";
import { ArrowRight, ArrowUp, CircleAlert, Clock, Plus, Sparkle } from "lucide-react";
import { DepartmentStatusCard } from "@/components/departments";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { Card, SectionTitle, SelectField } from "@/components/ui";
import { attentionItems, departments, directorInsight, kpis } from "@/lib/demo-data";

const trendStyles = {
  up: { icon: ArrowUp, className: "text-ok" },
  today: { icon: Clock, className: "text-ok" },
  warn: { icon: CircleAlert, className: "text-warn" },
} as const;

export default function DashboardPage() {
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
            className="flex min-h-[64px] items-center justify-center gap-2 rounded-xl border border-line bg-white/60 text-sm font-medium text-accent-strong transition-colors hover:border-accent/40 hover:bg-white xl:self-start"
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
              AI Director
            </div>
            <p className="mt-3 text-sm leading-relaxed text-ink">{directorInsight}</p>
            <Link href="/ai" className="mt-3 inline-flex items-center gap-1.5 text-sm font-semibold text-accent-strong hover:underline">
              Открыть анализ <ArrowRight className="size-3.5" />
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
