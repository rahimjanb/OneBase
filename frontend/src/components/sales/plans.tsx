"use client";

import Link from "next/link";
import { useT } from "@/i18n/client";
import type { T } from "@/i18n/core";
import { DataTable, NameCell, type Column } from "./DataTable";
import { ExecutionBar, levelBarTone, levelClass } from "./bits";
import { kg, money, num, pct } from "@/lib/sales/format";
import type { IndicatorPlan, PlanPersonRow, PlansView } from "@/lib/sales/types";

const NO_REGION = "00000000-0000-0000-0000-000000000000";
const withQuery = (path: string, query: string) => (query ? `${path}?${query}` : path);

const unit = (type: string, t: T) => {
  const text =
    type === "product_sales_weight" ? "кг" : type === "sales_sum" ? "сум" : type === "active_client_count" ? "ТТ" : type === "product_sales_amount" ? "шт" : "";
  return text && t(text);
};

const value = (type: string, v: number, t: T) =>
  type === "product_sales_weight" ? kg(v) : type === "sales_sum" ? money(v, t) : num(v);

function Indicators({ rows, agentId, query, team }: { rows: IndicatorPlan[]; agentId: number; query: string; team: boolean }) {
  const t = useT();
  return (
    <div className="space-y-2">
      {rows.map((r) => (
        <div key={r.indicatorId} className="grid grid-cols-1 items-center gap-x-4 gap-y-1 text-xs sm:grid-cols-[minmax(0,320px)_1fr_minmax(0,230px)]">
          <span className="truncate text-ink-2" title={r.name}>
            {r.name}
          </span>
          <ExecutionBar value={r.execution} tone={levelBarTone(r.executionLevel)} />
          <span className="text-right tabular-nums text-ink">
            {t("{fact} из {plan} {unit}", { fact: value(r.planType, r.fact, t), plan: value(r.planType, r.plan, t), unit: unit(r.planType, t) })}
            <span className={`ml-2 font-semibold ${levelClass(r.executionLevel)}`}>{pct(r.execution)}</span>
          </span>
        </div>
      ))}
      {!team && (
        <Link href={withQuery(`/sales/agents/${agentId}`, query)} className="inline-block pt-1 text-xs font-medium text-accent-strong hover:underline">
          {t("Открыть карточку агента →")}
        </Link>
      )}
    </div>
  );
}

export function RegionPlansTable({ rows, query }: { rows: PlansView["regions"]; query: string }) {
  const t = useT();
  const columns: Column<PlansView["regions"][number]>[] = [
    { key: "name", label: t("Регион"), value: (r) => r.name, render: (r) => <NameCell name={r.id === NO_REGION ? t(r.name) : r.name} /> },
    { key: "agents", label: t("ТП с планом"), align: "right", value: (r) => r.agents, render: (r) => num(r.agents) },
    { key: "plan", label: t("План, кг"), align: "right", value: (r) => r.weightPlan, render: (r) => kg(r.weightPlan) },
    { key: "fact", label: t("Факт Linko, кг"), align: "right", value: (r) => r.weightFact, render: (r) => kg(r.weightFact) },
    { key: "exec", label: t("Вып."), align: "right", value: (r) => r.weightExecution, render: (r) => <span className={levelClass(r.weightExecutionLevel)}>{pct(r.weightExecution)}</span> },
    { key: "rplan", label: t("План, сум"), align: "right", value: (r) => r.revenuePlan, render: (r) => money(r.revenuePlan, t) },
    { key: "rfact", label: t("Факт Linko, сум"), align: "right", value: (r) => (r.revenuePlan == null ? null : r.revenueFact), render: (r) => (r.revenuePlan == null ? "—" : money(r.revenueFact, t)) },
  ];
  return (
    <DataTable
      title={t("По регионам")}
      hint={t("сумма планов агентов")}
      columns={columns}
      rows={rows}
      rowKey={(r) => r.id}
      rowHref={(r) => (r.id === NO_REGION ? null : withQuery(`/sales/regions/${r.id}`, query))}
      empty={t("Планов за этот месяц в Linko нет")}
    />
  );
}

export function PeoplePlansTable({
  rows,
  query,
  title,
  hint,
  note,
  team = false,
}: {
  rows: PlanPersonRow[];
  query: string;
  title: string;
  hint?: string;
  note?: string;
  team?: boolean;
}) {
  const t = useT();
  const columns: Column<PlanPersonRow>[] = [
    {
      key: "name",
      label: team ? t("Супервайзер") : t("ТП"),
      value: (r) => r.name,
      render: (r) => <NameCell name={r.name} sub={[r.job, `ID ${r.agentId}`].filter(Boolean).join(" · ")} />,
    },
    ...(team ? [] : [{ key: "region", label: t("Регион"), value: (r: PlanPersonRow) => r.regionName ?? t("Без региона") } as Column<PlanPersonRow>]),
    { key: "plan", label: t("План, кг"), align: "right", value: (r) => r.weightPlan, render: (r) => kg(r.weightPlan) },
    { key: "fact", label: t("Факт, кг"), align: "right", value: (r) => (r.weightPlan == null ? null : r.weightFact), render: (r) => (r.weightPlan == null ? "—" : kg(r.weightFact)) },
    { key: "exec", label: t("Вып."), align: "right", value: (r) => r.weightExecution, render: (r) => <span className={levelClass(r.weightExecutionLevel)}>{pct(r.weightExecution)}</span> },
    { key: "rplan", label: t("План, сум"), align: "right", value: (r) => r.revenuePlan, render: (r) => money(r.revenuePlan, t) },
    { key: "rfact", label: t("Факт, сум"), align: "right", value: (r) => (r.revenuePlan == null ? null : r.revenueFact), render: (r) => (r.revenuePlan == null ? "—" : money(r.revenueFact, t)) },
    { key: "akb", label: t("АКБ план / факт"), align: "right", value: (r) => r.akbPlan, render: (r) => (r.akbPlan == null ? "—" : `${num(r.akbFact)} / ${num(r.akbPlan)}`) },
    { key: "count", label: t("Показателей"), align: "right", value: (r) => r.indicators.length, render: (r) => num(r.indicators.length) },
  ];

  return (
    <DataTable
      title={t(title)}
      hint={hint && t(hint)}
      columns={columns}
      rows={rows}
      rowKey={(r) => String(r.agentId)}
      expand={(r) => <Indicators rows={r.indicators} agentId={r.agentId} query={query} team={team} />}
      note={note && t(note)}
      empty={t("Нет планов")}
    />
  );
}
