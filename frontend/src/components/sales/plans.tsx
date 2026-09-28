"use client";

import Link from "next/link";
import { DataTable, NameCell, type Column } from "./DataTable";
import { ExecutionBar, execClass } from "./bits";
import { kg, money, num, pct } from "@/lib/sales/format";
import type { IndicatorPlan, PlanPersonRow, PlansView } from "@/lib/sales/types";

const NO_REGION = "00000000-0000-0000-0000-000000000000";
const withQuery = (path: string, query: string) => (query ? `${path}?${query}` : path);

const unit = (type: string) =>
  type === "product_sales_weight" ? "кг" : type === "sales_sum" ? "сум" : type === "active_client_count" ? "ТТ" : type === "product_sales_amount" ? "шт" : "";

const value = (type: string, v: number) =>
  type === "product_sales_weight" ? kg(v) : type === "sales_sum" ? money(v) : num(v);

function Indicators({ rows, agentId, query, team }: { rows: IndicatorPlan[]; agentId: number; query: string; team: boolean }) {
  return (
    <div className="space-y-2">
      {rows.map((r) => (
        <div key={r.indicatorId} className="grid grid-cols-1 items-center gap-x-4 gap-y-1 text-xs sm:grid-cols-[minmax(0,320px)_1fr_minmax(0,230px)]">
          <span className="truncate text-ink-2" title={r.name}>
            {r.name}
          </span>
          <ExecutionBar value={r.execution} tone={r.execution != null && r.execution < 0.7 ? (r.execution < 0.4 ? "bad" : "warn") : "accent"} />
          <span className="text-right tabular-nums text-ink">
            {value(r.planType, r.fact)} из {value(r.planType, r.plan)} {unit(r.planType)}
            <span className={`ml-2 font-semibold ${execClass(r.execution)}`}>{pct(r.execution)}</span>
          </span>
        </div>
      ))}
      {!team && (
        <Link href={withQuery(`/sales/agents/${agentId}`, query)} className="inline-block pt-1 text-xs font-medium text-accent-strong hover:underline">
          Открыть карточку агента →
        </Link>
      )}
    </div>
  );
}

export function RegionPlansTable({ rows, query }: { rows: PlansView["regions"]; query: string }) {
  const columns: Column<PlansView["regions"][number]>[] = [
    { key: "name", label: "Регион", value: (r) => r.name, render: (r) => <NameCell name={r.name} /> },
    { key: "agents", label: "ТП с планом", align: "right", value: (r) => r.agents, render: (r) => num(r.agents) },
    { key: "plan", label: "План, кг", align: "right", value: (r) => r.weightPlan, render: (r) => kg(r.weightPlan) },
    { key: "fact", label: "Факт Linko, кг", align: "right", value: (r) => r.weightFact, render: (r) => kg(r.weightFact) },
    { key: "exec", label: "Вып.", align: "right", value: (r) => r.weightExecution, render: (r) => <span className={execClass(r.weightExecution)}>{pct(r.weightExecution)}</span> },
    { key: "rplan", label: "План, сум", align: "right", value: (r) => r.revenuePlan, render: (r) => money(r.revenuePlan) },
    { key: "rfact", label: "Факт Linko, сум", align: "right", value: (r) => (r.revenuePlan == null ? null : r.revenueFact), render: (r) => (r.revenuePlan == null ? "—" : money(r.revenueFact)) },
  ];
  return (
    <DataTable
      title="По регионам"
      hint="сумма планов агентов"
      columns={columns}
      rows={rows}
      rowKey={(r) => r.id}
      rowHref={(r) => (r.id === NO_REGION ? null : withQuery(`/sales/regions/${r.id}`, query))}
      empty="Планов за этот месяц в Linko нет"
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
  const columns: Column<PlanPersonRow>[] = [
    {
      key: "name",
      label: team ? "Супервайзер" : "ТП",
      value: (r) => r.name,
      render: (r) => <NameCell name={r.name} sub={[r.job, `ID ${r.agentId}`].filter(Boolean).join(" · ")} />,
    },
    ...(team ? [] : [{ key: "region", label: "Регион", value: (r: PlanPersonRow) => r.regionName ?? "Без региона" } as Column<PlanPersonRow>]),
    { key: "plan", label: "План, кг", align: "right", value: (r) => r.weightPlan, render: (r) => kg(r.weightPlan) },
    { key: "fact", label: "Факт, кг", align: "right", value: (r) => (r.weightPlan == null ? null : r.weightFact), render: (r) => (r.weightPlan == null ? "—" : kg(r.weightFact)) },
    { key: "exec", label: "Вып.", align: "right", value: (r) => r.weightExecution, render: (r) => <span className={execClass(r.weightExecution)}>{pct(r.weightExecution)}</span> },
    { key: "rplan", label: "План, сум", align: "right", value: (r) => r.revenuePlan, render: (r) => money(r.revenuePlan) },
    { key: "rfact", label: "Факт, сум", align: "right", value: (r) => (r.revenuePlan == null ? null : r.revenueFact), render: (r) => (r.revenuePlan == null ? "—" : money(r.revenueFact)) },
    { key: "akb", label: "АКБ план / факт", align: "right", value: (r) => r.akbPlan, render: (r) => (r.akbPlan == null ? "—" : `${num(r.akbFact)} / ${num(r.akbPlan)}`) },
    { key: "count", label: "Показателей", align: "right", value: (r) => r.indicators.length, render: (r) => num(r.indicators.length) },
  ];

  return (
    <DataTable
      title={title}
      hint={hint}
      columns={columns}
      rows={rows}
      rowKey={(r) => String(r.agentId)}
      expand={(r) => <Indicators rows={r.indicators} agentId={r.agentId} query={query} team={team} />}
      note={note}
      empty="Нет планов"
    />
  );
}
