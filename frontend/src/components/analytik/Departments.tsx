"use client";

import { useT } from "@/i18n/client";
import { DataTable, type Column } from "@/components/sales/DataTable";
import {
  compact,
  date,
  kpiValue,
  num,
  type Block,
  type HrDivision,
  type ProducibleRow,
  type ProductionBlock,
  type ProductionOrderRow,
  type SalesBlock,
  type SalesRegionRow,
  type WorkDepartment,
} from "@/lib/analytik";
import { BarList, Card, Empty, KpiGrid, LineChart, Notes, PeriodPicker } from "./ui";

const pct = (v: number | null) => (v == null ? null : Math.round(v * 10) / 10);

export function SalesView({ data }: { data: SalesBlock }) {
  const t = useT();
  const columns: Column<SalesRegionRow>[] = [
    { key: "name", label: "Регион", value: (r) => r.name },
    { key: "revenue", label: "Выручка", align: "right", value: (r) => r.revenue, render: (r) => compact(r.revenue) },
    { key: "kg", label: "Кг", align: "right", value: (r) => r.kg, render: (r) => num(r.kg) },
    { key: "akb", label: "АКБ", align: "right", value: (r) => r.akb },
    {
      key: "change",
      label: "К прошлому, %",
      align: "right",
      value: (r) => pct(r.changePercent),
      render: (r) => <span className={r.changePercent == null ? "text-ink-4" : r.changePercent >= 0 ? "text-ok" : "text-bad"}>{kpiValue(r.changePercent, "%")}</span>,
    },
    { key: "plan", label: "План месяца, кг", align: "right", value: (r) => r.planKg, render: (r) => num(r.planKg) },
    { key: "execution", label: "Выполнение, %", align: "right", value: (r) => pct(r.execution), render: (r) => kpiValue(r.execution, "%") },
    {
      key: "forecast",
      label: "Прогноз, %",
      align: "right",
      value: (r) => pct(r.forecastExecution),
      render: (r) => (
        <span className={r.forecastExecution == null ? "text-ink-4" : r.forecastExecution >= 95 ? "text-ok" : r.forecastExecution >= 80 ? "text-warn" : "text-bad"}>
          {kpiValue(r.forecastExecution, "%")}
        </span>
      ),
    },
  ];
  return (
    <div className="space-y-4">
      <PeriodPicker period={data.period} />
      {data.scope && <p className="text-xs text-accent-strong">{t("Показана ваша область продаж")}: {data.scope}</p>}
      <KpiGrid kpis={data.kpis} columns="sm:grid-cols-4 2xl:grid-cols-8" compact />
      <div className="grid gap-4 xl:grid-cols-[minmax(0,1.6fr)_minmax(0,1fr)]">
        <Card title="Продажи по дням" subtitle={data.dataThrough ? `${t("данные по")} ${date(data.dataThrough)}` : undefined}>
          {data.daily.length === 0 ? (
            <Empty text="Нет продаж по дням за период" hint={data.scope ? "Для области из нескольких регионов показываются итоги регионов." : undefined} />
          ) : (
            <LineChart
              labels={data.daily.map((d) => date(d.date))}
              series={[{ name: "Выручка", values: data.daily.map((d) => d.revenue), color: "var(--color-accent)" }]}
              format={(v) => compact(v)}
            />
          )}
        </Card>
        <Card title="Выручка по регионам">
          {data.regions.length === 0 ? (
            <Empty text="Нет данных по регионам за период" />
          ) : (
            <BarList
              rows={data.regions.slice(0, 10).map((r) => ({
                key: r.id,
                label: r.name,
                value: r.revenue,
                text: compact(r.revenue),
                tone: r.changePercent != null && r.changePercent < -10 ? "bad" : "accent",
                sub: r.changePercent != null ? `${t("к прошлому периоду")} ${kpiValue(r.changePercent, "%")}` : undefined,
              }))}
            />
          )}
        </Card>
      </div>
      {data.plan && (
        <Card
          title={`${t("План месяца")} ${String(data.plan.month).padStart(2, "0")}.${data.plan.year}`}
          subtitle={`${t("План")} ${kpiValue(data.plan.planKg, "кг")} · ${t("факт по плану")} ${kpiValue(data.plan.planFactKg ?? data.plan.factKg, "кг")} · ${t("выполнение")} ${kpiValue(data.plan.execution, "%")} · ${t("прогноз на конец месяца")} ${kpiValue(data.plan.forecastExecution, "%")} · ${t("данные по")} ${date(data.plan.dataThrough)}`}
          href="/sales"
        >
          <p className="text-xs text-ink-3">{t("Прогноз месяца = факт ÷ прошедшие дни × дни месяца — как на странице «Продажи».")}</p>
        </Card>
      )}
      <DataTable title={t("Регионы")} columns={columns} rows={data.regions} rowKey={(r) => r.id} />
      <Notes notes={data.notes} sources={data.sources} />
    </div>
  );
}

export function ProductionView({ data }: { data: ProductionBlock }) {
  const t = useT();
  const orders: Column<ProductionOrderRow>[] = [
    { key: "number", label: "Задание", value: (r) => r.number },
    { key: "recipe", label: "Техкарта", value: (r) => r.recipe },
    { key: "date", label: "Дата", value: (r) => r.plannedDate, render: (r) => date(r.plannedDate) },
    { key: "status", label: "Статус", value: (r) => r.status },
    { key: "plan", label: "План", align: "right", value: (r) => r.plannedQty, render: (r) => `${num(r.plannedQty, 1)} ${r.unit}` },
    { key: "fact", label: "Факт", align: "right", value: (r) => r.outputQty, render: (r) => (r.outputQty == null ? "—" : `${num(r.outputQty, 1)} ${r.unit}`) },
    { key: "done", label: "Выполнение, %", align: "right", value: (r) => r.completionPercent, render: (r) => kpiValue(r.completionPercent, "%") },
  ];
  const producible: Column<ProducibleRow>[] = [
    { key: "recipe", label: "Техкарта", value: (r) => `${r.code} ${r.name}` },
    { key: "can", label: "Можно произвести", align: "right", value: (r) => r.producible, render: (r) => `${num(r.producible, 1)} ${r.outputUnit}` },
    { key: "limit", label: "Ограничивает", value: (r) => r.limitingItem },
    {
      key: "cost",
      label: "Сырьё на базу, сум",
      align: "right",
      value: (r) => r.materialCost,
      render: (r) => (r.materialCost == null ? <span className="text-ink-4">{t("нет цен")}</span> : `${compact(r.materialCost)}${r.costComplete ? "" : " *"}`),
    },
    { key: "base", label: "База техкарты", align: "right", value: (r) => r.baseQuantity, render: (r) => `${num(r.baseQuantity)} ${r.outputUnit}` },
  ];
  return (
    <div className="space-y-4">
      <PeriodPicker period={data.period} />
      <KpiGrid kpis={data.kpis} columns="sm:grid-cols-3 xl:grid-cols-5" compact />
      <Card title="Сколько можно произвести из доступного сырья" subtitle="По действующим техкартам: выпуск ограничивает позиция с наименьшим запасом; * — не у всех позиций есть цена">
        {data.producible.length === 0 ? (
          <Empty text="Действующих техкарт нет" hint="Утвердите техкарты в «Производство → Технолог»." />
        ) : (
          <DataTable title={t("Техкарты")} columns={producible} rows={data.producible} rowKey={(r) => r.recipeId} />
        )}
      </Card>
      <div className="grid gap-4 xl:grid-cols-2">
        <Card title="Перерасход сырья против нормы" href="/production/reports">
          {data.deviations.filter((d) => d.overconsumed).length === 0 ? (
            <Empty text="Перерасхода в периоде нет" />
          ) : (
            <BarList
              rows={data.deviations
                .filter((d) => d.overconsumed)
                .slice(0, 10)
                .map((d) => ({ key: `${d.orderNumber}-${d.item}`, label: `${d.item} · ${d.orderNumber}`, value: d.deviation, text: `+${num(d.deviation, 2)} ${d.unitCode}`, tone: "bad", sub: d.percent != null ? `+${num(d.percent, 1)}% ${t("к норме")}` : undefined }))}
            />
          )}
        </Card>
        <Card title="Рост цен сырья за период" href="/production">
          {data.prices.length === 0 ? (
            <Empty text="Изменений цен прихода нет" />
          ) : (
            <BarList
              rows={data.prices.slice(0, 10).map((p) => ({
                key: p.name,
                label: p.name,
                value: p.changePercent ?? 0,
                text: kpiValue(p.changePercent, "%"),
                tone: (p.changePercent ?? 0) > 0 ? "bad" : "ok",
                sub: `${num(p.firstPrice)} → ${num(p.lastPrice)} ${t("сум")}/${p.unitCode}`,
              }))}
            />
          )}
        </Card>
      </div>
      {data.orders.length > 0 && <DataTable title={t("Задания периода")} columns={orders} rows={data.orders} rowKey={(r) => r.number} />}
      <Notes notes={data.notes} sources={data.sources} />
    </div>
  );
}

export function FinanceView({ data }: { data: Block & { period: import("@/lib/analytik").Period; payments: { type: string; amount: number; count: number; previousAmount: number | null }[] } }) {
  const t = useT();
  return (
    <div className="space-y-4">
      <PeriodPicker period={data.period} />
      <KpiGrid kpis={data.kpis} columns="sm:grid-cols-3" />
      <Card title="Оплаты точек по видам">
        {data.payments.length === 0 ? (
          <Empty text="Оплат за период нет или они вам не видны" />
        ) : (
          <BarList rows={data.payments.map((p) => ({ key: p.type, label: t(p.type === "cash" ? "Наличные" : p.type === "bank" ? "Банк" : p.type), value: p.amount, text: `${compact(p.amount)} ${t("сум")}`, sub: `${num(p.count)} ${t("оплат")}${p.previousAmount != null ? ` · ${t("прошлый период")} ${compact(p.previousAmount)}` : ""}` }))} />
        )}
      </Card>
      <Card title="Чего нет в финансовых данных OneBase">
        <Notes notes={data.notes} sources={data.sources} />
      </Card>
    </div>
  );
}

export function HrView({ hr, work, period }: { hr: (Block & { divisions: HrDivision[] }) | null; work: (Block & { departments: WorkDepartment[] }) | null; period: import("@/lib/analytik").Period }) {
  const t = useT();
  const divisions: Column<HrDivision>[] = [
    { key: "division", label: "Подразделение", value: (r) => r.division },
    { key: "late", label: "Опоздания", align: "right", value: (r) => r.late },
    { key: "minutes", label: "Минут опозданий", align: "right", value: (r) => r.lateMinutes },
    { key: "early", label: "Ранние уходы", align: "right", value: (r) => r.earlyLeave },
    { key: "absent", label: "Отсутствия", align: "right", value: (r) => r.absent },
    { key: "employees", label: "Сотрудников с отклонениями", align: "right", value: (r) => r.employees },
  ];
  const departments: Column<WorkDepartment>[] = [
    { key: "department", label: "Отдел", value: (r) => r.department },
    { key: "open", label: "Открыто", align: "right", value: (r) => r.open },
    { key: "overdue", label: "Просрочено", align: "right", value: (r) => r.overdue, render: (r) => <span className={r.overdue > 0 ? "text-bad" : ""}>{r.overdue}</span> },
    { key: "done", label: "Выполнено за период", align: "right", value: (r) => r.doneInPeriod },
    { key: "blocked", label: "Заблокировано", align: "right", value: (r) => r.blocked },
    { key: "ontime", label: "В срок, %", align: "right", value: (r) => r.onTimePercent, render: (r) => kpiValue(r.onTimePercent, "%") },
  ];
  return (
    <div className="space-y-4">
      <PeriodPicker period={period} />
      {hr && (
        <>
          <KpiGrid kpis={hr.kpis} columns="sm:grid-cols-4 2xl:grid-cols-7" compact />
          {hr.divisions.length === 0 ? <Empty text="Отклонений за период нет" /> : <DataTable title={t("Отклонения по подразделениям")} columns={divisions} rows={hr.divisions} rowKey={(r) => r.division} />}
          <Notes notes={hr.notes} sources={hr.sources} />
        </>
      )}
      {work && (
        <>
          <KpiGrid kpis={work.kpis} columns="sm:grid-cols-3" compact />
          <DataTable title={t("Задачи по отделам")} columns={departments} rows={work.departments} rowKey={(r) => r.department} />
        </>
      )}
      {!hr && !work && <Empty text="Нет доступа к данным персонала и задач" />}
    </div>
  );
}
