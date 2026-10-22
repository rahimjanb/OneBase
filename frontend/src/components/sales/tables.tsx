"use client";

import { useT } from "@/i18n/client";
import { DeltaPill, FlagCountPills, FlagPills, levelClass } from "./bits";
import { DataTable, NameCell, type Column } from "./DataTable";
import { kg, money, num, pct } from "@/lib/sales/format";
import type { NewMarket, NotBoughtRow, SameDaysRow, SilentMarket, TeamRow, UnitRow } from "@/lib/sales/types";

/** Строка продаж без региона: её имя «Без региона» — подпись сервера (переводится), у остальных строк имя — данные. */
const NO_REGION = "00000000-0000-0000-0000-000000000000";

const withQuery = (path: string, query: string) => (query ? `${path}?${query}` : path);

// ---------- Регионы ----------

export function RegionsTable({ rows, query, title, hint }: { rows: UnitRow[]; query: string; title: string; hint?: string }) {
  const t = useT();
  const columns: Column<UnitRow>[] = [
    { key: "name", label: t("Регион"), value: (r) => r.name, render: (r) => <NameCell name={r.id === NO_REGION ? t(r.name) : r.name} sub={r.subtitle} /> },
    { key: "plan", label: t("План, кг"), align: "right", value: (r) => r.planKg, render: (r) => kg(r.planKg) },
    { key: "planFact", label: t("Факт в плане, кг"), align: "right", value: (r) => r.planFactKg, render: (r) => kg(r.planFactKg) },
    { key: "exec", label: t("Вып."), align: "right", value: (r) => r.execution, render: (r) => <span className={levelClass(r.executionLevel)}>{pct(r.execution)}</span> },
    { key: "fact", label: t("Факт всего, кг"), align: "right", value: (r) => r.factKg, render: (r) => kg(r.factKg) },
    { key: "forecast", label: t("Прогноз, кг"), align: "right", value: (r) => r.forecastKg, render: (r) => kg(r.forecastKg) },
    {
      key: "forecastExec",
      label: t("Прогноз вып."),
      align: "right",
      value: (r) => r.forecastExecution,
      render: (r) => <span className={levelClass(r.forecastExecutionLevel)}>{pct(r.forecastExecution)}</span>,
    },
    { key: "revenue", label: t("Выручка"), align: "right", value: (r) => r.revenue, render: (r) => money(r.revenue, t) },
    { key: "akb", label: t("АКБ"), align: "right", value: (r) => r.akb, render: (r) => num(r.akb) },
    { key: "strike", label: t("Страйк"), align: "right", value: (r) => r.strike, render: (r) => pct(r.strike) },
    { key: "noOrder", label: t("Без заказа"), align: "right", value: (r) => r.visitsWithoutOrder, render: (r) => num(r.visitsWithoutOrder) },
    {
      key: "flags",
      label: t("Флаги"),
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
      note={t("План — РОП или «Завод» (переключатель «План» вверху), «факт в плане» — все продажи региона; если планов регионов на месяц нет — план и факт ТП с планом в Linko. Выполнение считается по «факту в плане». «Факт всего» — все продажи региона. Прогноз — факт всего, растянутый на месяц по текущему темпу; у закрытого месяца прогноза нет. Страйк — заказы ТП региона, принятые в месяце ÷ их выполненные визиты (бывает больше 100%); «без заказа» — визиты минус заказы.")}
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
  const t = useT();
  const columns: Column<SameDaysRow>[] = [
    { key: "name", label: nameLabel, value: (r) => r.name, render: (r) => <NameCell name={r.id === NO_REGION ? t(r.name) : r.name} sub={r.subtitle} /> },
    { key: "kgBefore", label: t("Было, кг"), align: "right", value: (r) => r.kgBefore, render: (r) => kg(r.kgBefore) },
    { key: "kgNow", label: t("Стало, кг"), align: "right", value: (r) => r.kgNow, render: (r) => kg(r.kgNow) },
    { key: "kgDelta", label: "Δ", align: "right", value: (r) => r.kgDelta, render: (r) => <DeltaPill value={r.kgDelta} /> },
    { key: "sumBefore", label: t("Было, сум"), align: "right", value: (r) => r.sumBefore, render: (r) => money(r.sumBefore, t) },
    { key: "sumNow", label: t("Стало, сум"), align: "right", value: (r) => r.sumNow, render: (r) => money(r.sumNow, t) },
    { key: "sumDelta", label: "Δ ", align: "right", value: (r) => r.sumDelta, render: (r) => <DeltaPill value={r.sumDelta} /> },
    { key: "akb", label: t("ТТ"), align: "right", value: (r) => r.akbNow, render: (r) => num(r.akbNow) },
    { key: "akbDelta", label: "Δ  ", align: "right", value: (r) => r.akbDelta, render: (r) => <DeltaPill value={r.akbDelta} /> },
  ];

  return (
    <DataTable
      title={t("К прошлому месяцу за те же дни")}
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
      note={t("Прошлый месяц урезан до тех же чисел — иначе неполный текущий месяц выглядел бы как обвал. Сравниваются одни и те же числа месяца, поэтому выходные совпадают. ТТ — активная клиентская база (АКБ).")}
    />
  );
}

// ---------- Ещё не купили ----------

export function SilentList({ markets }: { markets: SilentMarket[] }) {
  const t = useT();
  if (markets.length === 0) return <p className="text-xs text-ink-3">{t("Все точки уже купили.")}</p>;
  return (
    <div className="max-h-72 overflow-y-auto">
      <table className="w-full text-xs">
        <thead>
          <tr className="text-left text-[11px] uppercase tracking-wide text-ink-3">
            <th className="py-1 pr-3 font-semibold">{t("Магазин")}</th>
            <th className="py-1 pr-3 text-right font-semibold">{t("кг в прошлом месяце")}</th>
            <th className="py-1 text-right font-semibold">{t("Выручка в прошлом месяце")}</th>
          </tr>
        </thead>
        <tbody>
          {markets.map((m) => (
            <tr key={m.marketId} className="border-t border-line">
              <td className="py-1.5 pr-3">
                {m.name} <span className="text-ink-3">№ {m.marketId}</span>
              </td>
              <td className="py-1.5 pr-3 text-right tabular-nums">{kg(m.prevKg)}</td>
              <td className="py-1.5 text-right tabular-nums">{money(m.prevRevenue, t)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function NotBoughtTable({
  rows,
  total,
  nameLabel,
  hint,
  workedDays,
  daysInMonth,
  expandable = false,
}: {
  rows: NotBoughtRow[];
  /** Итог таблицы — считает сервер; noData у итога — нет данных хотя бы у одной строки. */
  total: NotBoughtRow;
  nameLabel: string;
  hint: string;
  workedDays: number;
  daysInMonth: number;
  expandable?: boolean;
}) {
  const t = useT();
  const noData = total.noData;
  const columns: Column<NotBoughtRow>[] = [
    {
      key: "name",
      label: nameLabel,
      value: (r) => r.name,
      render: (r) => (
        <span className="flex items-center gap-2">
          <NameCell name={r.id === NO_REGION ? t(r.name) : r.name} sub={r.subtitle} />
          {r.noData && <span className="whitespace-nowrap rounded-full bg-muted px-1.5 py-0.5 text-[10px] font-medium text-ink-2">{t("нет данных")}</span>}
        </span>
      ),
    },
    { key: "base", label: t("База ТТ"), align: "right", value: (r) => r.base, render: (r) => num(r.base) },
    {
      key: "silent",
      label: t("Молчат"),
      align: "right",
      value: (r) => r.silent,
      render: (r) => (
        <span className={expandable && r.silent > 0 ? "font-semibold text-accent-strong underline decoration-dotted underline-offset-4" : ""}>
          {num(r.silent)}
        </span>
      ),
    },
    { key: "share", label: t("Доля"), align: "right", value: (r) => r.share, render: (r) => pct(r.share) },
    { key: "prev", label: t("Их прошлый месяц"), align: "right", value: (r) => r.silentPrevRevenue, render: (r) => money(r.silentPrevRevenue, t) },
    { key: "new", label: t("Новых"), align: "right", value: (r) => r.new, render: (r) => num(r.new) },
  ];

  return (
    <DataTable
      title={t("Ещё не купили в этом месяце")}
      hint={hint}
      actions={
        <span className="rounded-full bg-muted px-2.5 py-1 text-xs tabular-nums text-ink-2">
          {t("{silent} из {base} ТТ", { silent: num(total.silent), base: num(total.base) })}
        </span>
      }
      columns={columns}
      rows={rows}
      rowKey={(r) => r.id}
      expand={expandable ? (r) => (r.silent > 0 ? <SilentList markets={r.silentMarkets} /> : null) : undefined}
      note={
        t(
          "Это рабочий список, а не отток: точки из прошлого месяца, которые ещё не покупали в этом. По ходу месяца список тает сам. Пройдено {worked} из {days} дней.",
          { worked: workedDays, days: daysInMonth },
        ) +
        (noData
          ? t(" «Нет данных» — в регионе за месяц нет ни одной покупки: это почти всегда дыра в данных, а не замолчавшая база, поэтому доли нет; в итог такие точки входят.")
          : "") +
        (expandable ? t(" Нажмите на строку, чтобы увидеть магазины.") : "")
      }
    />
  );
}

// ---------- Команда ТП ----------

export function TeamTable({ rows, query }: { rows: TeamRow[]; query: string }) {
  const t = useT();
  const columns: Column<TeamRow>[] = [
    {
      key: "name",
      label: t("ТП"),
      value: (r) => r.name,
      render: (r) => (
        <span className="block min-w-[170px]">
          <span className="flex items-center gap-2 font-medium text-ink">
            {r.name}
            {r.isVacancy && <span className="rounded-full bg-muted px-1.5 py-0.5 text-[10px] font-medium text-ink-2">{t("вакансия")}</span>}
          </span>
          <span className="block text-xs text-ink-3">
            ID {r.agentId}
            {!r.isSalesRep && <span title={t("не ТП: в численность ТП, медианы и рейтинг не входит")}> · {t("не ТП")}{r.job ? ` (${r.job})` : ""}</span>}
          </span>
        </span>
      ),
    },
    { key: "plan", label: t("План ТП, кг"), align: "right", value: (r) => r.planKg, render: (r) => kg(r.planKg) },
    { key: "fact", label: t("Факт, кг"), align: "right", value: (r) => r.factKg, render: (r) => kg(r.factKg) },
    { key: "exec", label: t("Вып."), align: "right", value: (r) => r.execution, render: (r) => <span className={levelClass(r.executionLevel)}>{pct(r.execution)}</span> },
    { key: "forecast", label: t("Прогноз, кг"), align: "right", value: (r) => r.forecastKg, render: (r) => kg(r.forecastKg) },
    {
      key: "forecastExec",
      label: t("Прогноз вып."),
      align: "right",
      value: (r) => r.forecastExecution,
      render: (r) => <span className={levelClass(r.forecastExecutionLevel)}>{pct(r.forecastExecution)}</span>,
    },
    { key: "revenue", label: t("Выручка"), align: "right", value: (r) => r.revenue, render: (r) => money(r.revenue, t) },
    { key: "visits", label: t("Визиты"), align: "right", value: (r) => r.visits, render: (r) => num(r.visits) },
    { key: "orders", label: t("Заказы"), align: "right", value: (r) => r.orders, render: (r) => num(r.orders) },
    { key: "strike", label: t("Страйк"), align: "right", value: (r) => r.strike, render: (r) => pct(r.strike) },
    { key: "spv", label: t("Сум/визит"), align: "right", value: (r) => r.sumPerVisit, render: (r) => money(r.sumPerVisit, t) },
    { key: "cats", label: t("Категорий"), align: "right", value: (r) => r.categories, render: (r) => num(r.categories) },
    {
      key: "flags",
      label: t("Риск"),
      value: (r) => r.flags.filter((f) => f.severity === "Critical").length * 100 + r.flags.filter((f) => f.severity === "Risk").length,
      render: (r) => <FlagPills flags={r.flags} empty={r.isVacancy ? null : t("Замечаний нет")} />,
    },
  ];

  return (
    <DataTable
      title={t("Команда ТП")}
      hint={t("клик по строке — карточка агента")}
      columns={columns}
      rows={rows}
      rowKey={(r) => String(r.agentId)}
      rowHref={(r) => withQuery(`/sales/agents/${r.agentId}`, query)}
      note={t("Факт ТП — продажи агента в этом регионе. Заказы — принятые в месяце, страйк — заказы ÷ выполненные визиты (бывает больше 100%). Флаги считаются, только если у агента за месяц не меньше 20 визитов; сравнение — с медианой региона по ТП без вакансий, с 20+ визитами и без «данные не сходятся». Прогноз — только у идущего месяца.")}
    />
  );
}

// ---------- Карточка агента ----------

export function SilentMarketsTable({ rows, base, revenue, workedDays, daysInMonth }: { rows: SilentMarket[]; base: number; revenue: number; workedDays: number; daysInMonth: number }) {
  const t = useT();
  const columns: Column<SilentMarket>[] = [
    { key: "name", label: t("Магазин"), value: (r) => r.name, render: (r) => <NameCell name={r.name} sub={t("Магазин № {id}", { id: r.marketId })} /> },
    { key: "kg", label: t("кг в прошлом месяце"), align: "right", value: (r) => r.prevKg, render: (r) => kg(r.prevKg) },
    { key: "revenue", label: t("Выручка в прошлом месяце"), align: "right", value: (r) => r.prevRevenue, render: (r) => money(r.prevRevenue, t) },
    { key: "sku", label: t("SKU всего"), align: "right", value: (r) => r.sku, render: (r) => num(r.sku) },
  ];

  return (
    <DataTable
      title={t("Ещё не купили в этом месяце")}
      hint={t("база — прошлый месяц, кто пока молчит")}
      actions={
        <span className="rounded-full bg-muted px-2.5 py-1 text-xs tabular-nums text-ink-2">
          {t("{silent} из {base} ТТ", { silent: num(rows.length), base: num(base) })} · {money(revenue, t)}
        </span>
      }
      columns={columns}
      rows={rows}
      rowKey={(r) => String(r.marketId)}
      empty={t("Все точки прошлого месяца уже купили")}
      note={t("Это рабочий список, а не отток: по ходу месяца список тает сам. Пройдено {worked} из {days} дней.", { worked: workedDays, days: daysInMonth })}
    />
  );
}

export function NewMarketsTable({ rows }: { rows: NewMarket[] }) {
  const t = useT();
  const columns: Column<NewMarket>[] = [
    { key: "name", label: t("Магазин"), value: (r) => r.name, render: (r) => <NameCell name={r.name} sub={t("Магазин № {id}", { id: r.marketId })} /> },
    { key: "kg", label: t("Факт, кг"), align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "revenue", label: t("Выручка"), align: "right", value: (r) => r.revenue, render: (r) => money(r.revenue, t) },
  ];

  return (
    <DataTable
      title={t("Новые точки")}
      hint={t("в прошлом месяце не покупали")}
      actions={<span className="rounded-full bg-muted px-2.5 py-1 text-xs tabular-nums text-ink-2">{num(rows.length)}</span>}
      columns={columns}
      rows={rows}
      rowKey={(r) => String(r.marketId)}
      empty={t("Новых точек пока нет")}
    />
  );
}

export function NotInDirectoryTable({ rows, query }: { rows: { agentId: number; name: string; kg: number; revenue: number }[]; query: string }) {
  const t = useT();
  const columns: Column<(typeof rows)[number]>[] = [
    { key: "name", label: t("Агент"), value: (r) => r.name, render: (r) => <NameCell name={r.name} sub={`ID ${r.agentId}`} /> },
    { key: "kg", label: t("Факт, кг"), align: "right", value: (r) => r.kg, render: (r) => kg(r.kg) },
    { key: "revenue", label: t("Выручка"), align: "right", value: (r) => r.revenue, render: (r) => money(r.revenue, t) },
  ];

  return (
    <DataTable
      title={t("Продают, но не заведены в справочнике")}
      hint={t("есть заказы, но в Linko у пользователя не должность «Агент»")}
      columns={columns}
      rows={rows}
      rowKey={(r) => String(r.agentId)}
      rowHref={(r) => withQuery(`/sales/agents/${r.agentId}`, query)}
      note={t("Справочник ТП ведётся в Linko: чтобы человек вошёл в команду, ему нужна должность «Агент» в Linko. Их продажи в итогах региона уже есть.")}
    />
  );
}
