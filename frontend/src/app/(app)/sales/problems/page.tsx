import Link from "next/link";
import { Alert, FlagPills, Note, Stat } from "@/components/sales/bits";
import { ProblemsFilters } from "@/components/sales/ProblemsFilters";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { apiGet } from "@/lib/server-api";
import { kg, money, num, pct } from "@/lib/sales/format";
import { apiQuery, periodQuery, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { ProblemsView } from "@/lib/sales/types";

export const metadata = { title: "Проблемные агенты · Продажи" };

export default async function ProblemsPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const sp = await searchParams;
  const data = await apiGet<ProblemsView>(`/api/sales/problems${apiQuery(sp, ["direction", "criterion", "vacancies"])}`, "/sales/problems");
  const q = periodQuery(sp);
  const { fact } = data;

  return (
    <SalesFrame title="Проблемные агенты" subtitle="Кого проверить в первую очередь — по тяжести замечаний" sp={sp} period={data.period}>
      <ProblemsFilters directions={data.directions} vacancies={data.vacancies} found={data.found} />

      {fact.unassignedKg > 0 && (
        <Alert>
          <b>{kg(fact.unassignedKg)} кг</b> факта (<b>{pct(fact.unassignedShare)}</b>) не привязаны к агентам — в заказах Linko нет агента. Рейтинг считается по
          остальным <b>{kg(fact.assignedKg)} кг</b>.
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
                <span className="shrink-0 text-xs tabular-nums text-ink-3" title={`Место в списке по тяжести замечаний (${num(a.score, 1)})`}>
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
        Агент оценивается, если у него за месяц не меньше 20 визитов, иначе — метка «мало данных». Конверсия — заказы, принятые в месяце ÷ выполненные
        визиты. Сравнение — с медианой своего региона по ТП с 20+ визитами, без вакансий и без агентов, у которых заказов больше, чем визитов (у них —
        риск «данные не сходятся», низкая конверсия не оценивается). Порядок — по тяжести замечаний, как в «Полевом контроле»: 10 за критичное, 4 за риск,
        плюс (1 − конверсия) × 3 у оцениваемых; номер в углу — место в этом списке. Пороги критериев задаются в настройках сервера (Sales:Flags).
      </Note>
    </SalesFrame>
  );
}
