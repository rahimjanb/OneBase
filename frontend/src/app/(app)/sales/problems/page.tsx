import Link from "next/link";
import { Alert, FlagPills, Note, Stat } from "@/components/sales/bits";
import { ProblemsFilters } from "@/components/sales/ProblemsFilters";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { apiGet } from "@/lib/server-api";
import { kg, money, num, pct } from "@/lib/sales/format";
import { apiQuery, periodQuery, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { GroupView, ProblemsView } from "@/lib/sales/types";

export const metadata = { title: "Проблемные агенты · Продажи" };

export default async function ProblemsPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const sp = await searchParams;
  const [data, republic] = await Promise.all([
    apiGet<ProblemsView>(`/api/sales/problems${apiQuery(sp, ["direction", "criterion", "vacancies"])}`, "/sales/problems"),
    apiGet<GroupView>(`/api/sales/republic${apiQuery(sp)}`, "/sales/problems"),
  ]);
  const q = periodQuery(sp);
  const directions = republic.cards.filter((c) => c.kind === "direction").map((c) => ({ id: c.id, name: c.name }));
  const found = data.agents.filter((a) => !a.isVacancy && a.flags.some((f) => f.severity !== "Info")).length;
  const unassigned = republic.unassigned;

  return (
    <SalesFrame title="Проблемные агенты" subtitle="Кого проверить в первую очередь — по тяжести замечаний" sp={sp}>
      <ProblemsFilters directions={directions} vacancies={data.vacancies} found={found} />

      {unassigned.kg > 0 && (
        <Alert>
          <b>{kg(unassigned.kg)} кг</b> факта (<b>{pct(unassigned.share)}</b>) не привязаны к агентам — в заказах Linko нет агента. Рейтинг считается по
          остальным <b>{kg(republic.kpi.factKg - unassigned.kg)} кг</b>.
        </Alert>
      )}

      <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-3 min-[87.5rem]:grid-cols-4">
        {data.agents.map((a) => (
          <Link
            key={a.agentId}
            href={withQuery(`/sales/agents/${a.agentId}`, q)}
            className="flex flex-col rounded-xl border border-line bg-surface p-5 shadow-sm transition-[border-color,box-shadow,transform] hover:-translate-y-0.5 hover:border-accent/40 hover:shadow-md max-lg:active:opacity-75"
          >
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-2 text-base font-semibold leading-snug text-ink">
                  {a.name}
                  {a.isVacancy && <span className="rounded-full bg-muted px-1.5 py-0.5 text-[10px] font-medium text-ink-2">вакансия</span>}
                </div>
                <div className="mt-0.5 text-xs text-ink-3">{[a.regionName ?? "Без региона", a.directionName].filter(Boolean).join(" · ")}</div>
              </div>
              {a.rank != null && (
                <span className="shrink-0 text-xs tabular-nums text-ink-3" title="Место в рейтинге ТП по выручке">
                  #{a.rank}
                </span>
              )}
            </div>
            <dl className="mt-4 grid grid-cols-3 gap-3">
              <Stat label="Конверсия" value={pct(a.conversion)} />
              <Stat label="Визиты" value={num(a.visits)} />
              <Stat label="Выручка" value={money(a.revenue)} />
            </dl>
            <div className="mt-auto pt-4">
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
        агентов, у которых заказов больше, чем визитов. Номер в углу — место ТП в рейтинге по выручке среди действующих ТП. Пороги критериев задаются в
        настройках сервера (Sales:Flags).
      </Note>
    </SalesFrame>
  );
}
