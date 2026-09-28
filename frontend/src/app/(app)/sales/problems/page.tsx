import Link from "next/link";
import { FlagPills, Note } from "@/components/sales/bits";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { apiGet } from "@/lib/server-api";
import { money, num, pct } from "@/lib/sales/format";
import { apiQuery, param, periodQuery, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { FlagKind, GroupView, ProblemsView } from "@/lib/sales/types";

export const metadata = { title: "Проблемные агенты · Продажи" };

const criteria: { key: FlagKind; label: string }[] = [
  { key: "LowConversion", label: "Низкая конверсия" },
  { key: "VisitsNoSales", label: "Ходит, но не продаёт" },
  { key: "SmallCheck", label: "Мелкий чек" },
  { key: "NarrowAssortment", label: "Узкий ассортимент" },
  { key: "TempoDrop", label: "Падение темпа" },
  { key: "DataMismatch", label: "Данные не сходятся" },
  { key: "LowData", label: "Мало данных (< 20 визитов)" },
];

const select = "h-9 rounded-lg border border-line bg-surface px-3 text-sm text-ink";

export default async function ProblemsPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const sp = await searchParams;
  const [data, republic] = await Promise.all([
    apiGet<ProblemsView>(`/api/sales/problems${apiQuery(sp, ["direction", "criterion", "vacancies"])}`, "/sales/problems"),
    apiGet<GroupView>(`/api/sales/republic${apiQuery(sp)}`, "/sales/problems"),
  ]);
  const q = periodQuery(sp);
  const direction = param(sp, "direction") ?? "";
  const criterion = param(sp, "criterion") ?? "";
  const vacancies = param(sp, "vacancies") === "true";

  return (
    <SalesFrame title="Проблемные агенты" subtitle="Кого проверить в первую очередь — по тяжести замечаний" tab="problems" sp={sp} period={data.period} returnTo="/sales/problems">
      <form method="get" className="flex flex-wrap items-end gap-3 rounded-xl border border-line bg-surface p-4">
        {["year", "month", "plan"].map((k) => param(sp, k) && <input key={k} type="hidden" name={k} value={param(sp, k)} />)}
        <label className="block">
          <span className="mb-1 block text-xs text-ink-2">РМ / направление</span>
          <select name="direction" defaultValue={direction} className={select}>
            <option value="">Все</option>
            {republic.cards.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
        </label>
        <label className="block">
          <span className="mb-1 block text-xs text-ink-2">Критерий</span>
          <select name="criterion" defaultValue={criterion} className={select}>
            <option value="">Любой (критично и риск)</option>
            {criteria.map((c) => (
              <option key={c.key} value={c.key}>
                {c.label}
              </option>
            ))}
          </select>
        </label>
        <label className="flex h-9 items-center gap-2 text-sm text-ink">
          <input type="checkbox" name="vacancies" value="true" defaultChecked={vacancies} className="size-4 accent-[var(--color-accent)]" />
          Показывать вакансии ({num(data.vacancies)})
        </label>
        <button type="submit" className="h-9 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong">
          Показать
        </button>
      </form>

      <p className="mt-4 text-sm text-ink-2">Найдено агентов: {num(data.agents.length)}</p>

      <div className="mt-3 grid gap-3 lg:grid-cols-2">
        {data.agents.map((a) => (
          <Link
            key={a.agentId}
            href={withQuery(`/sales/agents/${a.agentId}`, q)}
            className="block rounded-xl border border-line bg-surface p-4 transition-colors hover:border-accent/40"
          >
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div className="min-w-0">
                <div className="flex items-center gap-2 font-semibold text-ink">
                  {a.name}
                  {a.isVacancy && <span className="rounded-full bg-muted px-1.5 py-0.5 text-[10px] font-medium text-ink-2">вакансия</span>}
                </div>
                <div className="mt-0.5 text-xs text-ink-3">
                  {[a.regionName, a.directionName].filter(Boolean).join(" · ")} · ID {a.agentId}
                </div>
              </div>
              {a.rank != null && <span className="rounded-full bg-muted px-2 py-0.5 text-xs tabular-nums text-ink-2">#{a.rank} в рейтинге</span>}
            </div>
            <dl className="mt-3 grid grid-cols-3 gap-3 text-xs">
              <div>
                <dt className="text-ink-3">Конверсия</dt>
                <dd className="mt-0.5 font-semibold tabular-nums text-ink">{pct(a.conversion, 1)}</dd>
              </div>
              <div>
                <dt className="text-ink-3">Визиты</dt>
                <dd className="mt-0.5 font-semibold tabular-nums text-ink">{num(a.visits)}</dd>
              </div>
              <div>
                <dt className="text-ink-3">Выручка</dt>
                <dd className="mt-0.5 font-semibold tabular-nums text-ink">{money(a.revenue)}</dd>
              </div>
            </dl>
            <div className="mt-3">
              <FlagPills flags={a.flags} empty={a.isVacancy ? null : "Замечаний нет"} />
            </div>
          </Link>
        ))}
      </div>
      {data.agents.length === 0 && (
        <p className="mt-6 rounded-xl border border-dashed border-line py-10 text-center text-sm text-ink-3">Агентов с замечаниями не найдено</p>
      )}

      <Note>
        Агент оценивается, если у него за месяц не меньше 20 визитов, иначе — метка «мало данных». Сравнение — с медианой своего региона без вакансий и без
        агентов, у которых заказов больше, чем визитов. Место в рейтинге — по выручке среди действующих ТП. Порог каждого критерия задаётся в настройках
        сервера (Sales:Flags).
      </Note>
    </SalesFrame>
  );
}
