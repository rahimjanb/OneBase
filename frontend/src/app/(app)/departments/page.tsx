import { redirect } from "next/navigation";
import { DepartmentsBoard, DepartmentsControls, DepartmentsSubtitle } from "@/components/department-board";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { tashkentToday } from "@/lib/dashboard-data";
import {
  hrCard,
  monthOptions,
  monthRange,
  noDataCard,
  parseMonth,
  salesCard,
  supplyCard,
  type Ctx,
  type DeptCard,
  type HrData,
  type Load,
  type SalesData,
  type SupplyData,
} from "@/lib/departments-view";
import type { HrOverview } from "@/lib/hr";
import { canOpen, departmentHref, departmentOpen, homeFor, screenPermissions } from "@/lib/navigation";
import type { Board } from "@/lib/procurement";
import { apiOutcome, apiSession } from "@/lib/server-api";
import { monthLabel, monthName } from "@/lib/sales/format";
import type { OverviewView } from "@/lib/sales/types";
import { qs } from "@/lib/tasks";
import type { Me } from "@/lib/users";

type SearchParams = Record<string, string | string[] | undefined>;
const one = (sp: SearchParams, key: string) => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

const forbidden = { state: "forbidden" } as const;

/**
 * «Отделы» — вход в разделы отделов и их показатели за месяц (?month=2026-11, по умолчанию — текущий; ?view=list — списком). Видны только
 * отделы, доступные пользователю: открывается свой раздел отдела или задачи отдела (lib/navigation departmentOpen); остальных нет совсем.
 * Отделы — в прежнем порядке. Настоящие показатели — у «Продаж» (план месяца), HR (явка) и «Снабжения» (заявки к закупке, только на
 * сегодня); у «Производства», «Финансов» и «Маркетинга» источников пока нет — «Нет данных». Каждая карточка — свой запрос: показатели закрыты
 * правами — карточка без числа, сбой — «Не удалось загрузить» и «Повторить», остальные карточки не страдают.
 */
export default async function DepartmentsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const me = await apiSession<Me>("/api/auth/me");
  // Права — как в меню: руководитель отдела с подразделениями Verifix видит HR своего отдела (hr.department), заявки на сотрудника
  // (hr.requests), признак «открывается хоть один отдел» (departments.open).
  const permissions = me ? screenPermissions(me) : [];
  if (me && !canOpen("/departments", permissions)) redirect(homeFor(permissions));
  // Без ответа API (me = null) отделы не прячем — разделы сами проверят доступ.
  const open = (code: string) => !me || departmentOpen(code, permissions, me.departments);
  const t = await getT();
  const sp = await searchParams;
  const today = tashkentToday();
  const chosen = one(sp, "month");
  const month = parseMonth(chosen, today);
  const range = monthRange(month, today);
  const view = one(sp, "view") === "list" ? "list" : "grid";
  const can = (href: string) => Boolean(me) && canOpen(href, permissions);

  // Показатели — только у видимых отделов с открытым разделом.
  const [sales, hr, supply] = await Promise.all([
    // Продажи без выбранного месяца — период сервера (как в разделе «Продажи»), с выбранным — этот месяц.
    open("sales") && can("/sales") ? apiOutcome<OverviewView>(`/api/sales/overview${chosen ? qs({ year: month.year, month: month.month }) : ""}`) : forbidden,
    open("hr") && can("/hr") ? loadHr(month.current, range) : forbidden,
    open("supply") && can("/procurement") && month.current ? apiOutcome<Board>(`/api/procurement/board${qs({ from: `${today.slice(0, 8)}01`, to: today })}`) : forbidden,
  ]);

  const ctx: Ctx = { t, month: `${monthName(month.month, t)} ${month.year}`, current: month.current, monthLabel: (y, m) => `${monthName(m, t)} ${y}` };
  const cards: DeptCard[] = [
    noDataCard("production", ctx),
    noDataCard("finance", ctx),
    salesCard(sales as Load<SalesData>, ctx),
    noDataCard("marketing", ctx),
    supplyCard(supply.state === "ok" ? { state: "ok", data: (supply.data.pipeline ?? { requestsAwaitingOrder: null }) as SupplyData } : supply, ctx),
    hrCard(hr, ctx),
  ].filter((card) => open(card.code));
  const months = monthOptions(today).map((key) => ({ key, label: monthLabel(Number(key.slice(0, 4)), Number(key.slice(5, 7)), t) }));

  return (
    <>
      <PageHeader
        title={t("Отделы")}
        subtitle={<DepartmentsSubtitle month={month.key} monthText={ctx.month} months={months} />}
        actions={<DepartmentsControls month={month.key} months={months} view={view} />}
      />
      <PageBody>
        <DepartmentsBoard cards={cards.map((c) => ({ ...c, href: departmentHref(c.code, permissions) }))} view={view} />
      </PageBody>
    </>
  );
}

/** Явка за месяц (отчёт HR за дни месяца) и, у текущего месяца, явка сегодня (как на главной: на месте от «по графику»). */
async function loadHr(current: boolean, range: { from: string; to: string }): Promise<Load<HrData>> {
  const [report, overview] = await Promise.all([
    apiOutcome<{ attendanceRate: number | null }>(`/api/hr/reports${qs(range)}`),
    current ? apiOutcome<HrOverview>("/api/hr/overview") : Promise.resolve(forbidden),
  ]);
  if (report.state !== "ok" && overview.state !== "ok") return report.state === "error" || overview.state === "error" ? { state: "error" } : forbidden;
  const presentPct = overview.state === "ok" && overview.data.scheduled > 0 ? (100 * overview.data.present) / overview.data.scheduled : null;
  return { state: "ok", data: { monthRate: report.state === "ok" ? report.data.attendanceRate : null, presentPct } };
}
