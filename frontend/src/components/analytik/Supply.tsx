"use client";

import { useMemo, useState } from "react";
import { useT } from "@/i18n/client";
import { DataTable, type Column } from "@/components/sales/DataTable";
import {
  compact,
  date,
  num,
  riskLabel,
  warehouseStatusLabel,
  type Risk,
  type SupplyPlan,
  type SupplyRecommendation,
  type WarehouseItem,
  type WarehouseStatus,
} from "@/lib/analytik";
import { Card, Empty, Notes, SeverityBadge } from "./ui";

const horizons = [7, 14, 30, 60, 90];

function Chips<T extends string>({ value, options, onChange }: { value: T; options: { value: T; label: string; count?: number }[]; onChange: (v: T) => void }) {
  const t = useT();
  return (
    <div className="flex flex-wrap gap-1.5">
      {options.map((o) => (
        <button
          key={o.value}
          type="button"
          onClick={() => onChange(o.value)}
          aria-pressed={value === o.value}
          className={`rounded-lg border px-2.5 py-1 text-xs font-medium transition-colors ${value === o.value ? "border-accent bg-accent text-white" : "border-line bg-surface text-ink-3 hover:text-ink"}`}
        >
          {t(o.label)}
          {o.count != null && <span className="ml-1 opacity-70">{o.count}</span>}
        </button>
      ))}
    </div>
  );
}

/** Расшифровка рекомендации: формула по горизонтам, причины, допущения. */
function Explain({ r }: { r: SupplyRecommendation }) {
  const t = useT();
  return (
    <div className="grid gap-4 py-2 text-[12px] lg:grid-cols-[minmax(0,1.2fr)_minmax(0,1fr)]">
      <table className="w-full text-right tabular-nums">
        <thead className="text-[11px] text-ink-4">
          <tr>
            <th className="text-left font-normal">{t("Горизонт")}</th>
            <th className="font-normal">{t("Спрос")}</th>
            <th className="font-normal">+ {t("страховой")}</th>
            <th className="font-normal">− {t("доступно")}</th>
            <th className="font-normal">− {t("поставки")}</th>
            <th className="font-normal">= {t("закупить")}</th>
            <th className="font-normal">{t("Стоимость")}</th>
          </tr>
        </thead>
        <tbody>
          {r.horizons.map((h) => (
            <tr key={h.days} className="border-t border-line">
              <td className="py-1 text-left">{h.days} {t("дн.")}</td>
              <td>{num(h.demand, 2)}</td>
              <td>{num(h.safetyStock, 2)}</td>
              <td>{num(r.available, 2)}</td>
              <td>{num(h.incoming, 2)}</td>
              <td className="font-semibold text-ink">{num(h.required, 2)}</td>
              <td>{h.cost == null ? <span className="text-ink-4">{t("нет цены")}</span> : compact(h.cost)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <div className="space-y-2 text-ink-2">
        <ul className="list-disc space-y-0.5 pl-4">
          {r.reasons.map((x) => (
            <li key={x}>{x}</li>
          ))}
        </ul>
        <p className="text-ink-3">
          {t("Срок поставки")}: {r.leadTimeDays} {t("дн.")} ({r.leadTimeSource}) · {t("цена")}: {r.unitPrice == null ? t("нет") : `${num(r.unitPrice)} ${t("сум")} (${r.priceSource})`}
        </p>
        {r.assumptions.length > 0 && (
          <ul className="space-y-0.5 text-warn">
            {r.assumptions.map((x) => (
              <li key={x}>⚠ {x}</li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

export function ProcurementView({ data }: { data: SupplyPlan }) {
  const t = useT();
  const [horizon, setHorizon] = useState(30);
  const [risk, setRisk] = useState<"action" | Risk | "all">("action");
  const rows = useMemo(
    () => data.items.filter((r) => (risk === "all" ? true : risk === "action" ? r.risk !== "none" && (r.horizons.find((h) => h.days === horizon)?.required ?? 0) > 0 || r.risk === "critical" : r.risk === risk)),
    [data.items, risk, horizon],
  );
  const need = (r: SupplyRecommendation) => r.horizons.find((h) => h.days === horizon)!;
  const columns: Column<SupplyRecommendation>[] = [
    { key: "risk", label: "Риск", value: (r) => ["critical", "high", "medium", "low", "none"].indexOf(r.risk), render: (r) => <SeverityBadge value={r.risk} label={riskLabel[r.risk]} /> },
    { key: "name", label: "Позиция", value: (r) => r.name, render: (r) => <span><span className="font-medium text-ink">{r.name}</span> <span className="text-ink-4">{r.sku}</span></span> },
    { key: "available", label: "Доступно", align: "right", value: (r) => r.available, render: (r) => `${num(r.available, 1)} ${r.unit}` },
    { key: "rate", label: "Расход в день", align: "right", value: (r) => r.avgDailyConsumption, render: (r) => num(r.avgDailyConsumption, 2) },
    { key: "shortage", label: "Закончится", value: (r) => r.shortageDate, render: (r) => (r.shortageDate ? date(r.shortageDate) : "—") },
    { key: "orderBy", label: "Заказать до", value: (r) => r.orderBy, render: (r) => (r.orderBy ? <span className={r.orderNow ? "font-semibold text-bad" : ""}>{date(r.orderBy)}</span> : "—") },
    { key: "need", label: `Закупить на ${horizon} дн.`, align: "right", value: (r) => need(r).required, render: (r) => <b>{num(need(r).required, 2)} {r.unit}</b> },
    { key: "cost", label: "Стоимость", align: "right", value: (r) => need(r).cost, render: (r) => (need(r).cost == null ? <span className="text-ink-4">{t("нет цены")}</span> : compact(need(r).cost)) },
    { key: "supplier", label: "Поставщик", value: (r) => r.supplier, render: (r) => r.supplier ?? <span className="text-ink-4">{t("не определён")}</span> },
  ];
  const total = data.horizons.find((h) => h.days === horizon);
  return (
    <div className="space-y-4">
      <Card title="Потребность в закупке" subtitle="Закупить = спрос за горизонт + страховой запас − доступный остаток − подтверждённые поставки">
        <div className="grid grid-cols-2 gap-2.5 sm:grid-cols-5">
          {data.horizons.map((h) => (
            <button
              key={h.days}
              type="button"
              onClick={() => setHorizon(h.days)}
              className={`rounded-xl border p-3 text-left transition-colors ${horizon === h.days ? "border-accent bg-accent-soft" : "border-line hover:border-line-strong"}`}
            >
              <p className="text-xs text-ink-3">{t("На")} {h.days} {t("дней")}</p>
              <p className="mt-1 text-lg font-semibold text-ink">{num(h.items)} {t("поз.")}</p>
              <p className="text-[11px] text-ink-3">
                {h.budget > 0 ? `${compact(h.budget)} ${t("сум")}` : t("бюджет не посчитан")}
                {h.itemsWithoutPrice > 0 && ` · ${t("без цены")} ${h.itemsWithoutPrice}`}
              </p>
            </button>
          ))}
        </div>
        <div className="mt-3 flex flex-wrap items-center gap-2">
          <Chips
            value={risk}
            onChange={setRisk}
            options={[
              { value: "action", label: "Нужно действие" },
              { value: "critical", label: "Критично", count: data.risks.critical ?? 0 },
              { value: "high", label: "Высокий", count: data.risks.high ?? 0 },
              { value: "medium", label: "Средний", count: data.risks.medium ?? 0 },
              { value: "low", label: "Низкий", count: data.risks.low ?? 0 },
              { value: "all", label: "Все", count: data.items.length },
            ]}
          />
          {horizons.length > 0 && total && (
            <span className="text-xs text-ink-4">
              {t("Бюджет горизонта")}:{" "}
              {total.items > 0 && total.itemsWithoutPrice === total.items ? t("не посчитан — нет цен") : `${compact(total.budget)} ${t("сум")}`}
            </span>
          )}
        </div>
      </Card>
      <DataTable
        title={t("Рекомендации по позициям")}
        hint={t("Нажмите на строку — расчёт по горизонтам, причины и допущения")}
        columns={columns}
        rows={rows}
        rowKey={(r) => r.itemId}
        expand={(r) => <Explain r={r} />}
        limit={100}
        empty="Позиций с потребностью нет"
      />
      {data.suppliers.length > 0 && (
        <Card title="По поставщикам (30 дней)">
          <ul className="divide-y divide-line text-[12.5px]">
            {data.suppliers.map((s) => (
              <li key={s.supplier} className="flex items-center justify-between gap-3 py-2">
                <span className="text-ink-2">{s.supplier}</span>
                <span className="text-ink-3">
                  {s.items} {t("поз.")} · {s.budget30 > 0 ? `${compact(s.budget30)} ${t("сум")}` : t("цены нет")}
                  {s.critical > 0 && <span className="ml-2 text-bad">{t("критично")}: {s.critical}</span>}
                </span>
              </li>
            ))}
          </ul>
        </Card>
      )}
      <Notes notes={[...data.dataNotes, "Закупки не оформляются автоматически: заявку создаёт сотрудник в «Заявках»."]} sources={data.sources} at={data.asOf} />
    </div>
  );
}

export function WarehouseView({ data }: { data: SupplyPlan }) {
  const t = useT();
  const [status, setStatus] = useState<WarehouseStatus | "risk" | "all">("risk");
  const rows = data.warehouse.filter((w) => (status === "all" ? w.status !== "empty" : status === "risk" ? ["out", "deficit", "below_min"].includes(w.status) : w.status === status));
  const columns: Column<WarehouseItem>[] = [
    { key: "status", label: "Статус", value: (r) => r.status, render: (r) => <SeverityBadge value={r.status === "out" ? "critical" : r.status === "deficit" || r.status === "below_min" ? "warning" : r.status === "ok" ? "none" : "info"} label={warehouseStatusLabel[r.status]} /> },
    { key: "name", label: "Позиция", value: (r) => r.name, render: (r) => <span><span className="font-medium text-ink">{r.name}</span> <span className="text-ink-4">{r.sku}</span></span> },
    { key: "category", label: "Категория", value: (r) => r.category },
    { key: "onHand", label: "На складе", align: "right", value: (r) => r.onHand, render: (r) => `${num(r.onHand, 2)} ${r.unit}` },
    { key: "reserved", label: "Резерв", align: "right", value: (r) => r.reserved, render: (r) => num(r.reserved, 2) },
    { key: "unusable", label: "Непригодно", align: "right", value: (r) => r.unusable, render: (r) => (r.unusable > 0 ? <span className="text-warn">{num(r.unusable, 2)}</span> : "0") },
    { key: "available", label: "Доступно", align: "right", value: (r) => r.available, render: (r) => <b>{num(r.available, 2)}</b> },
    { key: "min", label: "Минимум", align: "right", value: (r) => r.minQty, render: (r) => num(r.minQty, 2) },
    { key: "rate", label: "Расход в день", align: "right", value: (r) => r.avgDailyConsumption, render: (r) => num(r.avgDailyConsumption, 2) },
    { key: "days", label: "Дней запаса", align: "right", value: (r) => r.daysOfCover, render: (r) => num(r.daysOfCover, 1) },
    { key: "value", label: "Стоимость", align: "right", value: (r) => r.value, render: (r) => (r.value == null ? <span className="text-ink-4">—</span> : compact(r.value)) },
  ];
  const statuses: WarehouseStatus[] = ["out", "deficit", "below_min", "excess", "dead", "ok"];
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-2.5 sm:grid-cols-4 xl:grid-cols-7">
        <div className="rounded-xl border border-line bg-surface p-3">
          <p className="text-xs text-ink-3">{t("Стоимость запаса")}</p>
          <p className="mt-1 text-lg font-semibold text-ink">{compact(data.stockValue)} {t("сум")}</p>
          <p className="text-[11px] text-ink-4">{t("без цены")}: {data.itemsWithoutPrice}</p>
        </div>
        {statuses.map((s) => (
          <button key={s} type="button" onClick={() => setStatus(s)} className={`rounded-xl border p-3 text-left ${status === s ? "border-accent bg-accent-soft" : "border-line bg-surface hover:border-line-strong"}`}>
            <p className="text-xs text-ink-3">{t(warehouseStatusLabel[s])}</p>
            <p className="mt-1 text-lg font-semibold text-ink">{num(data.warehouseStatus[s] ?? 0)}</p>
          </button>
        ))}
      </div>
      <Chips value={status} onChange={setStatus} options={[{ value: "risk", label: "С риском" }, { value: "all", label: "Все" }, ...statuses.map((s) => ({ value: s, label: warehouseStatusLabel[s] }))]} />
      {rows.length === 0 ? (
        <Empty text="Позиций с таким статусом нет" />
      ) : (
        <DataTable title={t("Склад сырья и материалов")} columns={columns} rows={rows} rowKey={(r) => r.itemId} limit={150} />
      )}
      <Notes notes={["Физический остаток, резерв заявок и непригодное (просрочено, на контроле, заблокировано) показаны отдельно; доступно = на складе − резерв − непригодно.", ...data.dataNotes]} sources={data.sources} at={data.asOf} />
    </div>
  );
}
