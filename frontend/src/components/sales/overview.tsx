import Link from "next/link";
import { Alert, Stat, SummaryCard, deltaTone } from "./bits";
import { plural } from "@/lib/format";
import { delta, kg, money, num } from "@/lib/sales/format";
import type { ExcludedSummary, FlagCounts } from "@/lib/sales/types";

/**
 * Блоки экрана «Вторичка» (верхний уровень раздела «Продажи»), которых нет на уровнях ниже:
 * полоса с флагами ТП и сводка «Экспорт и опт». Плитки KPI и карточка «Республика» — общие KpiRow и UnitCard из blocks.tsx.
 * Все значения приходят из /api/sales/overview, демо-данных здесь нет.
 */

/** Полоса «Из N действующих ТП помечены: ● критично ● риск»; счётчики ведут в «Проблемные агенты». */
export function FlagsStrip({ activeAgents, flags, vacancies, href }: { activeAgents: number; flags: FlagCounts; vacancies: number; href: string }) {
  const clean = flags.critical === 0 && flags.risk === 0;
  const vacancyWord = plural(vacancies, ["вакансия", "вакансии", "вакансий"]);
  return (
    <Alert tone={clean ? "ok" : "warn"}>
      <span className="flex flex-wrap items-center gap-x-3 gap-y-1">
        <span>
          Из <b>{num(activeAgents)}</b> действующих ТП помечены:
        </span>
        <Link href={href} aria-label="Открыть «Проблемные агенты»" className="inline-flex flex-wrap items-center gap-x-3 gap-y-1 font-semibold hover:underline">
          {clean ? (
            <span className="text-ok">замечаний нет</span>
          ) : (
            <>
              {flags.critical > 0 && (
                <span className="inline-flex items-center gap-1.5 whitespace-nowrap text-bad">
                  <span className="size-1.5 rounded-full bg-current" aria-hidden />
                  {num(flags.critical)} критично
                </span>
              )}
              {flags.risk > 0 && (
                <span className="inline-flex items-center gap-1.5 whitespace-nowrap text-warn">
                  <span className="size-1.5 rounded-full bg-current" aria-hidden />
                  {num(flags.risk)} риск
                </span>
              )}
            </>
          )}
        </Link>
        {vacancies > 0 && (
          <span className="text-ink-3">
            Ещё {num(vacancies)} {vacancyWord} · {vacancyWord === "вакансия" ? "не входит" : "не входят"} в рейтинг
          </span>
        )}
      </span>
    </Alert>
  );
}

/** Сводка «Экспорт и опт»: филиал «Завод» из Linko — отдельно от вторички, чтобы не завышать республику. */
export function ExportSummaryCard({ data, href }: { data: ExcludedSummary; href: string }) {
  return (
    <SummaryCard title="Экспорт и опт" subtitle="филиал «Завод» в Linko · вне вторички" href={href}>
      <dl className="mt-4 grid grid-cols-2 gap-x-6 gap-y-3">
        <Stat label="Факт, кг" value={kg(data.factKg)} />
        <Stat label="Прогноз, кг" value={kg(data.forecastKg)} />
        <Stat label="Выручка" value={money(data.revenue)} />
        <Stat label="Заказов" value={num(data.orders)} />
        <Stat label="АКБ" value={num(data.akb)} />
        <Stat label="К прошлому месяцу" value={delta(data.vsPrevMonth)} tone={deltaTone(data.vsPrevMonth)} />
      </dl>
    </SummaryCard>
  );
}
