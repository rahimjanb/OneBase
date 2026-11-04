import { redirect } from "next/navigation";
import { DepartmentStatusCard } from "@/components/departments";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { loadHrBrief, tashkentToday, type HrBrief } from "@/lib/dashboard-data";
import { departments } from "@/lib/demo-data";
import { canOpen, departmentHref, homeFor } from "@/lib/navigation";
import type { Board } from "@/lib/procurement";
import { apiSession, apiTry } from "@/lib/server-api";
import type { OverviewView } from "@/lib/sales/types";
import { qs } from "@/lib/tasks";
import type { Me } from "@/lib/users";
import type { Department } from "@/lib/demo-data";
import { monthName } from "@/lib/sales/format";
import type { T } from "@/i18n/core";

/**
 * «Отделы» — вход в разделы отделов: «Продажи», «Снабжение» и HR открываются отсюда. Карточки «Продажи», «Снабжение» и HR — по настоящим
 * данным (план месяца, заявки к закупке, явка), без доступа к разделу — «нет данных»; остальные отделы пока без своих разделов.
 */
export default async function DepartmentsPage() {
  const me = await apiSession<Me>("/api/auth/me");
  // Руководитель отдела с подразделениями Verifix видит HR своего отдела — как в меню (hr.department).
  const permissions = me ? (me.hrAccess ? [...me.permissions, "hr.department"] : me.permissions) : [];
  if (me && !canOpen("/departments", permissions)) redirect(homeFor(permissions));
  const t = await getT();
  const today = tashkentToday();
  const [overview, hr, board] = await Promise.all([
    me && canOpen("/sales", permissions) ? apiTry<OverviewView>("/api/sales/overview") : null,
    me && canOpen("/hr", permissions) ? loadHrBrief(today) : null,
    me && canOpen("/procurement", permissions) ? apiTry<Board>(`/api/procurement/board${qs({ from: `${today.slice(0, 8)}01`, to: today })}`) : null,
  ]);
  const month = `${monthName(Number(today.slice(5, 7)), t)} ${today.slice(0, 4)}`;
  const list = departments.map((d) =>
    d.code === "sales" ? salesCard(d, overview, t) : d.code === "hr" ? hrCard(d, hr, month, t) : d.code === "supply" ? supplyCard(d, board, t) : d,
  );
  return (
    <>
      <PageHeader title={t("Отделы")} subtitle={t("Показатели, планы и состояние подразделений")} />
      <PageBody>
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {list.map((d) => (
            <DepartmentStatusCard key={d.code} department={d} href={departmentHref(d.code, permissions)} />
          ))}
        </div>
      </PageBody>
    </>
  );
}

/**
 * Карточка «Продажи»: крупно — прогноз выполнения плана месяца (у закрытого месяца — выполнение), полоса — факт к плану на отчётный день.
 * Статус: план уже выполнен — «План выполнен», прогноз от 100% — «В норме», ниже — «Требует внимания». Плана нет — «Требует внимания»,
 * нет доступа или данных — «Нет данных».
 */
function salesCard(base: Department, o: OverviewView | null, t: T): Department {
  if (!o) {
    return { ...base, score: null, status: "none", caption: t("нет данных продаж"), metric: { label: t("Факт к плану месяца"), value: 0 } };
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

/**
 * Карточка HR (Verifix): крупно — явка с начала месяца, полоса — явка сегодня (на месте от «по графику»). Норма — 80%, как на главной:
 * от неё — «В норме», ниже — «Требует внимания». Нет доступа к HR или данных Verifix — «Нет данных».
 */
function hrCard(base: Department, hr: HrBrief | null, month: string, t: T): Department {
  const score = hr?.monthRate ?? hr?.presentPct ?? null;
  if (hr == null || score == null) {
    return { ...base, score: null, status: "none", caption: t("нет данных Verifix"), metric: { label: t("Явка сегодня"), value: 0 } };
  }

  const pct = (value: number) => `${value.toLocaleString("ru-RU", { maximumFractionDigits: 1 })}%`;
  return {
    ...base,
    score: Math.round(score),
    status: score >= 80 ? "ok" : "attention",
    caption: hr.monthRate != null ? t("явка с начала месяца · {month}", { month }) : t("явка сегодня"),
    metric:
      hr.presentPct != null
        ? { label: t("Явка сегодня: {pct}", { pct: pct(hr.presentPct) }), value: Math.min(100, Math.round(hr.presentPct)) }
        : { label: t("Явка сегодня: отметок ещё нет"), value: 0 },
  };
}

/**
 * Карточка «Снабжение»: крупно — доля заявок к закупке без просрочки (желаемая дата не прошла), полоса — она же, подпись — сколько к
 * закупке, просрочено и на согласовании. Есть просроченные — «Требует внимания», нет — «В норме»; к закупке ничего — 100%. Заявки
 * видят снабжение и директор: у кладовщика и без доступа — «Нет данных».
 */
function supplyCard(base: Department, b: Board | null, t: T): Department {
  const p = b?.pipeline;
  if (!p || p.requestsAwaitingOrder == null) {
    return { ...base, score: null, status: "none", caption: t("заявки видят снабжение и директор"), metric: { label: t("Заявки к закупке"), value: 0 } };
  }

  const toBuy = p.requestsAwaitingOrder;
  const overdue = Math.min(p.overdueToBuy ?? 0, toBuy);
  const onTime = toBuy > 0 ? Math.round((100 * (toBuy - overdue)) / toBuy) : 100;
  const approval = p.onApprovalCount ?? 0;
  return {
    ...base,
    score: onTime,
    status: overdue > 0 ? "attention" : "ok",
    caption: toBuy > 0 ? t("заявок к закупке — без просрочки") : t("всё согласованное закуплено"),
    metric: {
      label:
        approval > 0
          ? t("К закупке: {a}, просрочено: {b}, на согласовании: {c}", { a: toBuy, b: overdue, c: approval })
          : t("К закупке: {a}, просрочено: {b}", { a: toBuy, b: overdue }),
      value: onTime,
    },
  };
}
