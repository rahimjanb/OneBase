"use client";

import { DeltaPill, FlagCountPills, FlagPills, execClass } from "./bits";
import { DataTable, NameCell, type Column } from "./DataTable";
import { kg, money, num, pct } from "@/lib/sales/format";
import type { NewMarket, NotBoughtRow, SameDaysRow, SilentMarket, TeamRow, UnitRow } from "@/lib/sales/types";

const NO_REGION = "00000000-0000-0000-0000-000000000000";

const withQuery = (path: string, query: string) => (query ? `${path}?${query}` : path);

// ---------- Регионы ----------

export function RegionsTable({ rows, query, title, hint }: { rows: UnitRow[]; query: string; title: string; hint?: string }) {
  const columns: Column<UnitRow>[] = [
    { key: "name", label: "Регион", value: (r) => r.name, render: (r) => <NameCell name={r.name} sub={r.subtitle} /> },
    { key: "plan", label: "План, кг", align: "right", value: (r) => r.planKg, render: (r) => kg(r.planKg) },
    { key: "planFact", label: "Факт в плане, кг", align: "right", value: (r) => r.planFactKg, render: (r) => kg(r.planFactKg) },
    { key: "exec", label: "Вып.", align: "right", value: (r) => r.execution, render: (r) => <span className={execClass(r.execution)}>{pct(r.execution)}</span> },
    { key: "fact", label: "Факт всего, кг", align: "right", value: (r) => r.factKg, render: (r) => kg(r.factKg) },
    { key: "forecast", label: "Прогноз, кг", align: "right", value: (r) => r.forecastKg, render: (r) => kg(r.forecastKg) },
    {
      key: "forecastExec",
      label: "Прогноз вып.",
      align: "right",
      value: (r) => r.forecastExecution,
      render: (r) => <span className={execClass(r.forecastExecution)}>{pct(r.forecastExecution)}</span>,
    },
    { key: "revenue", label: "Выручка", align: "right", value: (r) => r.revenue, render: (r) => money(r.revenue) },
    { key: "akb", label: "АКБ", align: "right", value: (r) => r.akb, render: (r) => num(r.akb) },
    { key: "strike", label: "Страйк", align: "right", value: (r) => r.strike, render: (r) => pct(r.strike) },
    { key: "noOrder", label: "Без заказа", align: "right", value: (r) => r.visitsWithoutOrder, render: (r) => num(r.visitsWithoutOrder) },
    {
      key: "flags",
      label: "Флаги",
      value: (r) => r.flags.critical * 1000 + r.flags.risk,
      render: (r) => <FlagCountPills flags={r.flags} />,
    },
  ];

  return (
    <DataTable
      title={title}
      hint={hint}
      columns={columns}
      rows={rows}
      rowKey={(r) => r.id}
      rowHref={(r) => (r.id === NO_REGION ? null : withQuery(`/sales/regions/${r.id}`, query))}
      note="«Факт в плане» — продажи тех ТП, у кого есть план (если у региона свой ручной план — все продажи региона); выполнение считается по нему. «Факт всего» — все продажи региона. Прогноз — факт всего, растянутый на месяц по текущему темпу. Страйк — доля визитов, после которых в тот же день был заказ в этой ТТ."
    />
  );
}

// ---------- К прошлому месяцу за те же дни ----------

export function SameDaysTable({
  rows,
  nameLabel,
  hint,
  link,
  query,
}: {
  rows: SameDaysRow[];
  nameLabel: string;
  hint: string;
  link?: "region" | "agent";
  query: string;
}) {
  const columns: Column<SameDaysRow>[] = [
    { key: "name", label: nameLabel, value: (r) => r.name, render: (r) => <NameCell name={r.name} sub={r.subtitle} /> },
    { key: "kgBefore", label: "Было, кг", align: "right", value: (r) => r.kgBefore, render: (r) => kg(r.kgBefore) },
    { key: "kgNow", label: "Стало, кг", align: "right", value: (r) => r.kgNow, render: (r) => kg(r.kgNow) },
    { key: "kgDelta", label: "Δ", align: "right", value: (r) => r.kgDelta, render: (r) => <DeltaPill value={r.kgDelta} /> },
    { key: "sumBefore", label: "Было, сум", align: "right", value: (r) => r.sumBefore, render: (r) => money(r.sumBefore) },
    { key: "sumNow", label: "Стало, сум", align: "right", value: (r) => r.sumNow, render: (r) => money(r.sumNow) },
    { key: "sumDelta", label: "Δ ", align: "right", value: (r) => r.sumDelta, render: (r) => <DeltaPill value={r.sumDelta} /> },
    { key: "akb", label: "ТТ", align: "right", value: (r) => r.akbNow, render: (r) => num(r.akbNow) },
    { key: "akbDelta", label: "Δ  ", align: "right", value: (r) => r.akbDelta, render: (r) => <DeltaPill value={r.akbDelta} /> },
  ];

  return (
    <DataTable
      title="К прошлому месяцу за те же дни"
      hint={hint}
      columns={columns}
      rows={rows}
      rowKey={(r) => r.id}
      rowHref={
        link === "region"
          ? (r) => (r.id === NO_REGION ? null : withQuery(`/sales/regions/${r.id}`, query))
          : link === "agent"
            ? (r) => withQuery(`/sales/agents/${r.id}`, query)
            : undefined
      }
      note="Прошлый месяц урезан до тех же чисел — иначе неполный текущий месяц выглядел бы как обвал. Сравниваются одни и те же числа месяца, поэтому выходные совпадают. ТТ — активная клиентская база (АКБ)."
    />
  );
}

// ---------- Ещё не купили ----------

export function SilentList({ markets }: { markets: SilentMarket[] }) {
  if (markets.length === 0) return <p className="text-xs text-ink-3">Все точки уже купили.</p>;
  return (
    <div className="max-h-72 overflow-y-auto">
      <table className="w-full text-xs">
        <thead>
          <tr className="text-left text-[11px] uppercase tracking-wide text-ink-3">
            <th className="py-1 pr-3 font-semibold">Магазин</th>
            <th className="py-1 pr-3 text-right font-semibold">кг в прошлом месяце</th>
            <th className="py-1 text-right font-semibold">Выручка в прошлом месяце</th>
          </tr>
        </thead>
        <tbody>
          {markets.map((m) => (
            <tr key={m.marketId} className="border-t border-line">
              <td className="py-1.5 pr-3">
                {m.name} <span className="text-ink-3">№ {m.marketId}</span>
              </td>
              <td className="py-1.5 pr-3 text-right tabular-nums">{kg(m.prevKg)}</td>
              <td className="py-1.5 text-right tabular-nums">{money(m.prevRevenue)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function NotBoughtTable({
  rows,
  nameLabel,
  hint,
  workedDays,
  daysInMonth,
  expandable = false,
}: {
  rows: NotBoughtRow[];
  nameLabel: string;
  hint: string;
  workedDays: number;
  daysInMonth: number;
  expandable?: boolean;
}) {
  const columns: Column<NotBoughtRow>[] = [
    { key: "name", label: nameLabel, value: (r) => r.name, render: (r) => <NameCell name={r.name} sub={r.subtitle} /> },
    { key: "base", label: "База ТТ", align: "right", value: (r) => r.base, render: (r) => num(r.base) },
    {
      key: "silent",
      label: "Молчат",
      align: "right",
      value: (r) => r.silent,
      render: (r) => (
        <span className={expandable && r.silent > 0 ? "font-semibold text-accent-strong underline decoration-dotted underline-offset-4" : ""}>
          {num(r.silent)}
        </span>
      ),
    },
    { key: "share", label: "Доля", align: "right", value: (r) => r.share, render: (r) => pct(r.share) },
    { key: "prev", label: "Их прошлый месяц", align: "right", value: (r) => r.silentPrevRevenue, render: (r) => money(r.silentPrevRevenue) },
    { key: "new", label: "Новых", align: "right", value: (r) => r.new, render: (r) => num(r.new) },
  ];

  const base = rows.reduce((s, r) => s + r.base, 0);
  const silent = rows.reduce((s, r) => s + r.silent, 0);

  return (
    <DataTable
      title="Ещё не купили в этом месяце"
      hint={hint}
      actions={
        <span className="rounded-full bg-muted px-2.5 py-1 text-xs tabular-nums text-ink-2">
          {num(silent)} из {num(base)} ТТ
        </span>
      }
      columns={columns}
      rows={rows}
      rowKey={(r) => r.id}
      expand={expandable ? (r) => (r.silent > 0 ? <SilentList markets={r.silentMarkets} /> : null) : undefined}
      note={`Это рабочий список, а не отток: точки из прошлого месяца, которые ещё не покупали в этом. По ходу месяца список тает сам. Пройдено ${workedDays} из ${daysInMonth} дней.${expandable ? " Нажмите на строку, чтобы увидеть магазины." : ""}`}
    />
  );
}

// ---------- Команда ТП ----------

export function TeamTable({ rows, query }: { rows: TeamRow[]; query: string }) {
  const columns: Column<TeamRow>[] = [
    {
      key: "name",
      label: "ТП",
      value: (r) => r.name,
      render: (r) => (
        <span className="block min-w-[170px]">
          <span className="flex items-center gap-2 font-medium text-ink">
            {r.name}
            {r.isVacancy && <span className="rounded-full bg-muted px-1.5 py-0.5 text-[10px] font-medium text-ink-2">вакансия</span>}
          </span>
          <span className="block text-xs text-ink-3">
            ID {r.agentId}
            {!r.isSalesRep && <span title="не ТП: в численность ТП, медианы и рейтинг не входит"> · не ТП{r.job ? ` (${r.job})` : ""}</span>}
          </span>
        </span>
      ),
    },
    { key: "plan", label: "План ТП, кг", align: "right", value: (r) => r.planKg, render: (r) => kg(r.planKg) },
    { key: "fact", label: "Факт, кг", align: "right", value: (r) => r.factKg, render: (r) => kg(r.factKg) },
    { key: "exec", label: "Вып.", align: "right", value: (r) => r.execution, render: (r) => <span className={execClass(r.execution)}>{pct(r.execution)}</span> },
    { key: "forecast", label: "Прогноз, кг", align: "right", value: (r) => r.forecastKg, render: (r) => kg(r.forecastKg) },
    {
      key: "forecastExec",
      label: "Прогноз вып.",
      align: "right",
      value: (r) => r.forecastExecution,
      render: (r) => <span className={execClass(r.forecastExecution)}>{pct(r.forecastExecution)}</span>,
    },
    { key: "revenue", label: "Выручка", align: "right", value: (r) => r.revenue, render: (r) => money(r.revenue) },
    { key: "visits", label: "Визиты", align: "right", value: (r) => r.visits, render: (r) => num(r.visits) },
    { key: "orders", label: "Заказы", align: "right", value: (r) => r.orders, render: (r) => num(r.orders) },
    { key: "strike", label: "Страйк", align: "right", value: (r) => r.strike, render: (r) => pct(r.strike) },
    { key: "spv", label: "Сум/визит", align: "right", value: (r) => r.sumPerVisit, render: (r) => money(r.sumPerVisit) },
    { key: "cats", label: "Категорий", align: "right", value: (r) => r.categories, render: (r) => num(r.categories) },
    {
      key: "flags",
      label: "Риск",
      value: (r) => r.flags.filter((f) => f.severity === "Critical").length * 100 + r.flags.filter((f) => f.severity === "Risk").length,
      render: (r) => <FlagPills flags={r.flags} empty={r.isVacancy ? null : "Замечаний нет"} />,
    },
  ];

  return (
    <DataTable
      title="Команда ТП"
      hint="клик по строке — карточка агента"
      columns={columns}
      rows={rows}
      rowKey={(r) => String(r.agentId)}
      rowHref={(r) => withQuery(`/sales/agents/${r.agentId}`, query)}
      note="Факт ТП — продажи агента в этом регионе. Флаги считаются, только если у агента за месяц не меньше 20 визитов; сравнение — с медианой региона без вакансий."
    />
  );
}

// ---------- Карточка агента ----------

export function SilentMarketsTable({ rows, base, revenue, workedDays, daysInMonth }: { rows: SilentMarket[]; base: number; revenue: number; workedDays: number; daysInMonth: number }) {
  const columns: Column<SilentMarket>[] = [
    { key: "name", label: "Магазин", value: (r) => r.name, render: (r) => <NameCell name={r.name} sub={`Магазин № ${r.marketId}`} /> },
    { key: "kg", label: "кг в прошлом месяце", align: "right", value: (r) => r.prevKg, render: (r) => kg(r.prevKg) },
    { key: "revenue", label: "Выручка в прошлом месяце", align: "right", value: (r) => r.prevRevenue, render: (r) => money(r.prevRevenue) },
    { key: "sku", label: "SKU всего", align: "right", value: (r) => r.sku, render: (r) => num(r.sku) },
  ];

  return (
    <DataTable
      title="Ещё не купили в этом месяце"
      hint="база — прошлый месяц, кто пока молчит"
      actions={
        <span className="rounded-full bg-muted px-2.5 py-1 text-xs tabular-nums text-ink-2">
          {num(rows.length)} из {num(base)} ТТ · {money(revenue)}
        </span>
      }
      columns={columns}
      rows={rows}
      rowKey={(r) => String(r.marketId)}
      empty="Все точки прошлого месяца уже купили"
      note={`Это рабочий список, а не отток: по ходу месяца список тает сам. Пройдено ${workedDays} из ${daysInMonth} дней.`}
    />
  );
}

export function NewMarketsTable({ rows }: { rows: NewMarket[] }) {
  const columns: Column<NewMarket>[] = [
    { key: "name", label: "Магазин", value: (r) => r.name, render: (r) => <NameCell name={r.name} sub={`Магазин № ${r.marketId}`} /> },
    { key: "kg", label: "Факт, кг", align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "revenue", label: "Выручка", align: "right", value: (r) => r.revenue, render: (r) => money(r.revenue) },
  ];

  return (
    <DataTable
      title="Новые точки"
      hint="в прошлом месяце не покупали"
      actions={<span className="rounded-full bg-muted px-2.5 py-1 text-xs tabular-nums text-ink-2">{num(rows.length)}</span>}
      columns={columns}
      rows={rows}
      rowKey={(r) => String(r.marketId)}
      empty="Новых точек пока нет"
    />
  );
}

export function NotInDirectoryTable({ rows, query }: { rows: { agentId: number; name: string; kg: number; revenue: number }[]; query: string }) {
  const columns: Column<(typeof rows)[number]>[] = [
    { key: "name", label: "Агент", value: (r) => r.name, render: (r) => <NameCell name={r.name} sub={`ID ${r.agentId}`} /> },
    { key: "kg", label: "Факт, кг", align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "revenue", label: "Выручка", align: "right", value: (r) => r.revenue, render: (r) => money(r.revenue) },
  ];

  return (
    <DataTable
      title="Продают, но не заведены в справочнике"
      hint="есть заказы, но агента нет в оргструктуре OneBase"
      columns={columns}
      rows={rows}
      rowKey={(r) => String(r.agentId)}
      rowHref={(r) => withQuery(`/sales/agents/${r.agentId}`, query)}
      note="Добавьте их в «Настройки → Продажи: оргструктура и планы → Агенты», чтобы назначить регион, план и отметить вакансии."
    />
  );
}
