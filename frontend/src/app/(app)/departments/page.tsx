import { redirect } from "next/navigation";
import { DepartmentStatusCard } from "@/components/departments";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { departments } from "@/lib/demo-data";
import { canOpen, homeFor } from "@/lib/navigation";
import { apiSession, apiTry } from "@/lib/server-api";
import type { OverviewView } from "@/lib/sales/types";
import type { Me } from "@/lib/users";
import type { Department } from "@/lib/demo-data";
import { monthName } from "@/lib/sales/format";
import type { T } from "@/i18n/core";

export default async function DepartmentsPage() {
  const me = await apiSession<Me>("/api/auth/me");
  if (me && !canOpen("/departments", me.permissions)) redirect(homeFor(me.permissions));
  const t = await getT();
  // «Продажи» — по данным вторички (план и прогноз месяца); остальные отделы пока демонстрационные.
  const overview = me && canOpen("/sales", me.permissions) ? await apiTry<OverviewView>("/api/sales/overview") : null;
  const list = departments.map((d) => (d.code === "sales" ? salesCard(d, overview, t) : d));
  return (
    <>
      <PageHeader title={t("Отделы")} subtitle={t("Показатели, планы и состояние подразделений")} />
      <PageBody>
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {list.map((d) => (
            <DepartmentStatusCard key={d.code} department={d} />
          ))}
        </div>
      </PageBody>
    </>
  );
}

/**
 * Карточка «Продажи»: крупно — прогноз выполнения плана месяца (у закрытого месяца — выполнение), полоса — факт к плану на отчётный день.
 * Статус: план уже выполнен — «План выполнен», прогноз от 100% — «В норме», ниже — «Требует внимания». Плана или доступа нет — так и сказано.
 */
function salesCard(base: Department, o: OverviewView | null, t: T): Department {
  if (!o) {
    return { ...base, score: null, status: "attention", caption: t("нет данных продаж"), metric: { label: t("Факт к плану месяца"), value: 0 } };
  }

  const k = o.kpi;
  const month = `${monthName(o.period.month, t)} ${o.period.year}`;
  const tons = (kg: number) => (kg / 1000).toLocaleString("ru-RU", { maximumFractionDigits: 1 });
  if (k.planKg == null || k.planKg <= 0) {
    return { ...base, score: null, status: "attention", caption: t("плана на {month} нет", { month }), metric: { label: t("Факт: {fact} т", { fact: tons(k.factKg) }), value: 0 } };
  }

  const execution = k.execution ?? k.factKg / k.planKg;
  const shown = o.period.closed ? execution : (k.forecastExecution ?? execution);
  const status = execution >= 1 ? "done" : shown >= 1 ? "ok" : "attention";
  return {
    ...base,
    score: Math.round(shown * 100),
    status,
    caption: o.period.closed ? t("выполнение плана · {month}", { month }) : t("прогноз выполнения плана · {month}", { month }),
    metric: { label: t("Факт к плану: {fact} из {plan} т", { fact: tons(k.factKg), plan: tons(k.planKg) }), value: Math.min(100, Math.round(execution * 100)) },
  };
}
