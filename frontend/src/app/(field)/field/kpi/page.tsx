import { formatDate } from "@/i18n/core";
import Link from "next/link";
import { ParamSelect } from "@/components/field/DateSwitch";
import { Bar, FieldPage, Panel, Stat } from "@/components/field/ui";
import { getLocale, getT } from "@/i18n/server";
import { fieldGet, fieldMe, qs, sp, type FieldSearchParams } from "@/lib/field/api";
import { kg, money, num, pct } from "@/lib/sales/format";
import type { FieldKpi } from "@/lib/field/types";

export const metadata = { title: "KPI" };

const tone = (share: number | null, expected: number) => (share == null ? "text-ink-3" : share >= expected ? "text-ok" : share >= expected * 0.8 ? "text-ink" : "text-bad");

/** KPI месяца: агент — свой; супервайзер — агенты команды; РМ — команды и агенты, итог организации. */
export default async function KpiPage({ searchParams }: { searchParams: Promise<FieldSearchParams> }) {
  const params = await searchParams;
  const t = await getT();
  const locale = await getLocale();
  const me = await fieldMe();
  const [ty, tm] = me.today.split("-").map(Number);
  const year = Number(sp(params, "year") ?? ty);
  const month = Number(sp(params, "month") ?? tm);
  const data = await fieldGet<FieldKpi>(`kpi${qs({ year, month, teamId: sp(params, "team") })}`, "/field/kpi");
  const total = data.total;
  // «Октябрь 2026»: название месяца — на языке интерфейса (по-русски — как monthLabel).
  const monthLabel = (y: number, m: number) => {
    const name = formatDate(new Date(Date.UTC(y, m - 1, 1)), locale, { month: "long", timeZone: "UTC" });
    return `${name[0].toUpperCase()}${name.slice(1)} ${y}`;
  };
  const months = Array.from({ length: 6 }, (_, i) => {
    const d = new Date(Date.UTC(ty, tm - 1 - i, 1));
    return { value: `${d.getUTCFullYear()}-${d.getUTCMonth() + 1}`, label: monthLabel(d.getUTCFullYear(), d.getUTCMonth() + 1) };
  });

  return (
    <FieldPage
      title="KPI"
      subtitle={`${monthLabel(data.year, data.month)} · ${t("на {date}", { date: new Date(`${data.asOf}T00:00:00Z`).toLocaleDateString("ru-RU", { timeZone: "UTC" }) })} · ${t("ожидаемый темп {pct}", { pct: pct(data.expectedShare) })}`}
      actions={
        <form className="flex gap-2" action="/field/kpi">
          <MonthPicker months={months} value={`${year}-${month}`} />
        </form>
      }
    >
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4 xl:grid-cols-6">
        <Stat label={t("Продажи")} value={money(total.sum, t)} unit={t("сум")} hint={total.planSum ? t("план {sum}", { sum: money(total.planSum, t) }) : undefined} share={total.shareSum} />
        <Stat label={t("Продажи, кг")} value={num(total.kg)} hint={total.planKg ? t("план {kg} кг", { kg: num(total.planKg) }) : t("плана нет")} share={total.shareKg} />
        <Stat label={t("Выполнение")} value={pct(total.shareKg)} hint={total.forecastShare != null ? t("прогноз {pct}", { pct: pct(total.forecastShare) }) : undefined} tone={total.shareKg != null && total.shareKg >= data.expectedShare ? "ok" : "warn"} />
        <Stat label={t("Активные точки")} value={num(total.activeMarkets)} hint={total.planAkb ? t("план АКБ {n}", { n: num(total.planAkb) }) : t("заказов {n}", { n: num(total.orders) })} />
        <Stat label={t("Визиты")} value={num(total.visitsDone)} hint={`Sales Base: ${total.salesBaseVisits} · ${total.sumPerVisit ? t("{sum} на визит", { sum: money(total.sumPerVisit, t) }) : ""}`} />
        <Stat label={t("Покрытие")} value={pct(total.marketCoverage)} hint={t("задачи {done}/{total} · маршруты {pct}", { done: total.tasksDone, total: total.tasksTotal, pct: pct(total.routeCoverage) })} share={total.marketCoverage} />
      </div>

      {data.teams.length > 1 && (
        <Panel title={t("Команды")} hint={t("средний результат агента, покрытие, эффективность (сум на визит)")}>
          <div className="-mx-4 overflow-x-auto sm:-mx-5">
            <table className="w-full min-w-[760px] text-sm">
              <thead className="text-left text-xs uppercase tracking-wide text-ink-3">
                <tr className="border-b border-line">
                  <th className="px-4 py-2 font-medium sm:px-5">{t("Команда")}</th>
                  <th className="px-2 py-2 text-right font-medium">{t("Агентов")}</th>
                  <th className="px-2 py-2 text-right font-medium">{t("Продажи")}</th>
                  <th className="px-2 py-2 text-right font-medium">{t("кг / план")}</th>
                  <th className="px-2 py-2 text-right font-medium">{t("Выполнение")}</th>
                  <th className="px-2 py-2 text-right font-medium">{t("Средний агент")}</th>
                  <th className="px-2 py-2 text-right font-medium">{t("Покрытие")}</th>
                  <th className="px-2 py-2 text-right font-medium">{t("Визиты")}</th>
                  <th className="px-4 py-2 text-right font-medium sm:px-5">{t("Сум/визит")}</th>
                </tr>
              </thead>
              <tbody>
                {data.teams.map((team) => (
                  <tr key={team.teamId} className="border-b border-line last:border-0 hover:bg-muted">
                    <td className="px-4 py-2.5 sm:px-5">
                      <Link href={`/field/kpi${qs({ year, month, team: team.teamId })}`} prefetch={false} className="font-medium text-ink hover:underline">
                        {team.name}
                      </Link>
                      <div className="text-xs text-ink-3">{team.supervisor ?? t("без супервайзера")}</div>
                    </td>
                    <td className="px-2 py-2.5 text-right tabular-nums">{team.agents}</td>
                    <td className="px-2 py-2.5 text-right tabular-nums">{money(team.sum, t)}</td>
                    <td className="px-2 py-2.5 text-right tabular-nums text-ink-2">
                      {kg(team.kg)} / {team.planKg ? kg(team.planKg) : "—"}
                    </td>
                    <td className={`px-2 py-2.5 text-right font-medium tabular-nums ${tone(team.shareKg, data.expectedShare)}`}>{pct(team.shareKg)}</td>
                    <td className="px-2 py-2.5 text-right tabular-nums">{pct(team.avgAgentShare)}</td>
                    <td className="px-2 py-2.5 text-right tabular-nums">{pct(team.marketCoverage)}</td>
                    <td className="px-2 py-2.5 text-right tabular-nums">{num(team.visitsDone)}</td>
                    <td className="px-4 py-2.5 text-right tabular-nums sm:px-5">{team.sumPerVisit ? money(team.sumPerVisit, t) : "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Panel>
      )}

      <Panel title={me.role === "Agent" ? t("Мои показатели") : t("Агенты")} hint={t("продажи — доставленные заказы Linko; план — staff_balance Linko")}>
        <ul className="space-y-3">
          {data.agents.map((a) => (
            <li key={a.agentId} className="rounded-xl border border-line p-3">
              <div className="flex flex-wrap items-baseline justify-between gap-2">
                <Link href={me.role === "Agent" ? "/field" : `/field/agents/${a.agentId}`} prefetch={false} className="font-medium text-ink hover:underline">
                  {a.name}
                </Link>
                <span className={`text-lg font-semibold tabular-nums ${tone(a.shareKg, data.expectedShare)}`}>{pct(a.shareKg)}</span>
              </div>
              {a.shareKg != null && <Bar share={a.shareKg} expected={data.expectedShare} expectedLabel={t("Ожидаемый темп")} className="mt-2" />}
              <dl className="mt-2.5 grid grid-cols-2 gap-x-4 gap-y-1.5 text-xs sm:grid-cols-4 lg:grid-cols-8">
                <Kv label={t("Продажи")} value={`${money(a.sum, t)} ${t("сум")}`} />
                <Kv label={t("кг / план")} value={`${kg(a.kg)} / ${a.planKg ? kg(a.planKg) : "—"}`} />
                <Kv label={t("Заказы")} value={num(a.orders)} />
                <Kv label={t("Активные точки")} value={`${a.activeMarkets}${a.planAkb ? ` / ${num(a.planAkb)}` : ""}`} />
                <Kv label={t("Визиты")} value={`${a.visitsDone}${a.visitsPlanned ? ` · ${t("план {pct}", { pct: pct(a.visitPlanShare) })}` : ""}`} />
                <Kv label={t("Задачи")} value={a.tasksTotal ? `${a.tasksDone}/${a.tasksTotal}` : "—"} />
                <Kv label={t("Маршруты")} value={a.routePoints ? `${a.routeVisited}/${a.routePoints}` : "—"} />
                <Kv label={t("Покрытие точек")} value={t("{pct} из {n}", { pct: pct(a.marketCoverage), n: a.assignedMarkets })} />
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
async function MonthPicker({ months, value }: { months: { value: string; label: string }[]; value: string }) {
  const t = await getT();
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
        {t("Показать")}
      </button>
    </>
  );
}
