import Link from "next/link";
import { ParamSelect } from "@/components/field/DateSwitch";
import { Bar, FieldPage, Panel, Stat } from "@/components/field/ui";
import { fieldGet, fieldMe, qs, sp, type FieldSearchParams } from "@/lib/field/api";
import { kg, money, monthLabel, num, pct } from "@/lib/sales/format";
import type { FieldKpi } from "@/lib/field/types";

export const metadata = { title: "KPI" };

const tone = (share: number | null, expected: number) => (share == null ? "text-ink-3" : share >= expected ? "text-ok" : share >= expected * 0.8 ? "text-ink" : "text-bad");

/** KPI месяца: агент — свой; супервайзер — агенты команды; РМ — команды и агенты, итог организации. */
export default async function KpiPage({ searchParams }: { searchParams: Promise<FieldSearchParams> }) {
  const params = await searchParams;
  const me = await fieldMe();
  const [ty, tm] = me.today.split("-").map(Number);
  const year = Number(sp(params, "year") ?? ty);
  const month = Number(sp(params, "month") ?? tm);
  const data = await fieldGet<FieldKpi>(`kpi${qs({ year, month, teamId: sp(params, "team") })}`, "/field/kpi");
  const t = data.total;
  const months = Array.from({ length: 6 }, (_, i) => {
    const d = new Date(Date.UTC(ty, tm - 1 - i, 1));
    return { value: `${d.getUTCFullYear()}-${d.getUTCMonth() + 1}`, label: monthLabel(d.getUTCFullYear(), d.getUTCMonth() + 1) };
  });

  return (
    <FieldPage
      title="KPI"
      subtitle={`${monthLabel(data.year, data.month)} · на ${new Date(`${data.asOf}T00:00:00Z`).toLocaleDateString("ru-RU", { timeZone: "UTC" })} · ожидаемый темп ${pct(data.expectedShare)}`}
      actions={
        <form className="flex gap-2" action="/field/kpi">
          <MonthPicker months={months} value={`${year}-${month}`} />
        </form>
      }
    >
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4 xl:grid-cols-6">
        <Stat label="Продажи" value={money(t.sum)} unit="сум" hint={t.planSum ? `план ${money(t.planSum)}` : undefined} share={t.shareSum} />
        <Stat label="Продажи, кг" value={num(t.kg)} hint={t.planKg ? `план ${num(t.planKg)} кг` : "плана нет"} share={t.shareKg} />
        <Stat label="Выполнение" value={pct(t.shareKg)} hint={t.forecastShare != null ? `прогноз ${pct(t.forecastShare)}` : undefined} tone={t.shareKg != null && t.shareKg >= data.expectedShare ? "ok" : "warn"} />
        <Stat label="Активные точки" value={num(t.activeMarkets)} hint={t.planAkb ? `план АКБ ${num(t.planAkb)}` : `заказов ${num(t.orders)}`} />
        <Stat label="Визиты" value={num(t.visitsDone)} hint={`Sales Base: ${t.salesBaseVisits} · ${t.sumPerVisit ? `${money(t.sumPerVisit)} на визит` : ""}`} />
        <Stat label="Покрытие" value={pct(t.marketCoverage)} hint={`задачи ${t.tasksDone}/${t.tasksTotal} · маршруты ${pct(t.routeCoverage)}`} share={t.marketCoverage} />
      </div>

      {data.teams.length > 1 && (
        <Panel title="Команды" hint="средний результат агента, покрытие, эффективность (сум на визит)">
          <div className="-mx-4 overflow-x-auto sm:-mx-5">
            <table className="w-full min-w-[760px] text-sm">
              <thead className="text-left text-xs uppercase tracking-wide text-ink-3">
                <tr className="border-b border-line">
                  <th className="px-4 py-2 font-medium sm:px-5">Команда</th>
                  <th className="px-2 py-2 text-right font-medium">Агентов</th>
                  <th className="px-2 py-2 text-right font-medium">Продажи</th>
                  <th className="px-2 py-2 text-right font-medium">кг / план</th>
                  <th className="px-2 py-2 text-right font-medium">Выполнение</th>
                  <th className="px-2 py-2 text-right font-medium">Средний агент</th>
                  <th className="px-2 py-2 text-right font-medium">Покрытие</th>
                  <th className="px-2 py-2 text-right font-medium">Визиты</th>
                  <th className="px-4 py-2 text-right font-medium sm:px-5">Сум/визит</th>
                </tr>
              </thead>
              <tbody>
                {data.teams.map((team) => (
                  <tr key={team.teamId} className="border-b border-line last:border-0 hover:bg-muted">
                    <td className="px-4 py-2.5 sm:px-5">
                      <Link href={`/field/kpi${qs({ year, month, team: team.teamId })}`} prefetch={false} className="font-medium text-ink hover:underline">
                        {team.name}
                      </Link>
                      <div className="text-xs text-ink-3">{team.supervisor ?? "без супервайзера"}</div>
                    </td>
                    <td className="px-2 py-2.5 text-right tabular-nums">{team.agents}</td>
                    <td className="px-2 py-2.5 text-right tabular-nums">{money(team.sum)}</td>
                    <td className="px-2 py-2.5 text-right tabular-nums text-ink-2">
                      {kg(team.kg)} / {team.planKg ? kg(team.planKg) : "—"}
                    </td>
                    <td className={`px-2 py-2.5 text-right font-medium tabular-nums ${tone(team.shareKg, data.expectedShare)}`}>{pct(team.shareKg)}</td>
                    <td className="px-2 py-2.5 text-right tabular-nums">{pct(team.avgAgentShare)}</td>
                    <td className="px-2 py-2.5 text-right tabular-nums">{pct(team.marketCoverage)}</td>
                    <td className="px-2 py-2.5 text-right tabular-nums">{num(team.visitsDone)}</td>
                    <td className="px-4 py-2.5 text-right tabular-nums sm:px-5">{team.sumPerVisit ? money(team.sumPerVisit) : "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Panel>
      )}

      <Panel title={me.role === "Agent" ? "Мои показатели" : "Агенты"} hint="продажи — доставленные заказы Linko; план — staff_balance Linko">
        <ul className="space-y-3">
          {data.agents.map((a) => (
            <li key={a.agentId} className="rounded-xl border border-line p-3">
              <div className="flex flex-wrap items-baseline justify-between gap-2">
                <Link href={me.role === "Agent" ? "/field" : `/field/agents/${a.agentId}`} prefetch={false} className="font-medium text-ink hover:underline">
                  {a.name}
                </Link>
                <span className={`text-lg font-semibold tabular-nums ${tone(a.shareKg, data.expectedShare)}`}>{pct(a.shareKg)}</span>
              </div>
              {a.shareKg != null && <Bar share={a.shareKg} expected={data.expectedShare} className="mt-2" />}
              <dl className="mt-2.5 grid grid-cols-2 gap-x-4 gap-y-1.5 text-xs sm:grid-cols-4 lg:grid-cols-8">
                <Kv label="Продажи" value={`${money(a.sum)} сум`} />
                <Kv label="кг / план" value={`${kg(a.kg)} / ${a.planKg ? kg(a.planKg) : "—"}`} />
                <Kv label="Заказы" value={num(a.orders)} />
                <Kv label="Активные точки" value={`${a.activeMarkets}${a.planAkb ? ` / ${num(a.planAkb)}` : ""}`} />
                <Kv label="Визиты" value={`${a.visitsDone}${a.visitsPlanned ? ` · план ${pct(a.visitPlanShare)}` : ""}`} />
                <Kv label="Задачи" value={a.tasksTotal ? `${a.tasksDone}/${a.tasksTotal}` : "—"} />
                <Kv label="Маршруты" value={a.routePoints ? `${a.routeVisited}/${a.routePoints}` : "—"} />
                <Kv label="Покрытие точек" value={`${pct(a.marketCoverage)} из ${a.assignedMarkets}`} />
              </dl>
            </li>
          ))}
        </ul>
      </Panel>
    </FieldPage>
  );
}

function Kv({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0">
      <dt className="text-ink-3">{label}</dt>
      <dd className="truncate font-medium tabular-nums text-ink">{value}</dd>
    </div>
  );
}

/** Выбор месяца — обычная форма (работает и без JavaScript). */
function MonthPicker({ months, value }: { months: { value: string; label: string }[]; value: string }) {
  return (
    <>
      <select name="ym" defaultValue={value} className="h-11 rounded-lg border border-line bg-surface px-3 text-sm text-ink lg:h-10">
        {months.map((m) => (
          <option key={m.value} value={m.value}>
            {m.label}
          </option>
        ))}
      </select>
      <button formAction="/field/kpi/go" className="h-11 rounded-lg border border-line bg-surface px-3 text-sm text-ink hover:bg-muted lg:h-10">
        Показать
      </button>
    </>
  );
}
