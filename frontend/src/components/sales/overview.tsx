import Link from "next/link";
import { getT } from "@/i18n/server";
import { Alert, Stat, SummaryCard, deltaTone } from "./bits";
import { plural } from "@/lib/format";
import { delta, kg, money, num } from "@/lib/sales/format";
import type { ExcludedSummary, FlagCounts } from "@/lib/sales/types";

/**
 * Блоки экрана «Вторичка» (верхний уровень раздела «Продажи»), которых нет на уровнях ниже:
 * полоса с флагами ТП и сводка «Экспорт и опт». Плитки KPI и карточка «Республика» — общие KpiRow и UnitCard из blocks.tsx.
 * Все значения приходят из /api/sales/overview, демо-данных здесь нет. Серверные (подключает только страница «Вторички»): перевод — getT().
 */

/** Полоса «Из N действующих ТП помечены: ● критично ● риск»; счётчики ведут в «Проблемные агенты». */
export async function FlagsStrip({ activeAgents, flags, vacancies, href }: { activeAgents: number; flags: FlagCounts; vacancies: number; href: string }) {
  const t = await getT();
  const clean = flags.critical === 0 && flags.risk === 0;
  const vacancyWord = plural(vacancies, ["вакансия", "вакансии", "вакансий"]);
  // Число — жирным внутри переведённой фразы: шаблон делится по {n}.
  const [before, after] = t("Из {n} действующих ТП помечены:").split("{n}");
  return (
    <Alert tone={clean ? "ok" : "warn"}>
      <span className="flex flex-wrap items-center gap-x-3 gap-y-1">
        <span>
          {before}
          <b>{num(activeAgents)}</b>
          {after}
        </span>
        <Link href={href} aria-label={t("Открыть «Проблемные агенты»")} className="inline-flex flex-wrap items-center gap-x-3 gap-y-1 font-semibold hover:underline">
          {clean ? (
            <span className="text-ok">{t("замечаний нет")}</span>
          ) : (
            <>
              {flags.critical > 0 && (
                <span className="inline-flex items-center gap-1.5 whitespace-nowrap text-bad">
                  <span className="size-1.5 rounded-full bg-current" aria-hidden />
                  {t("{n} критично", { n: num(flags.critical) })}
                </span>
              )}
              {flags.risk > 0 && (
                <span className="inline-flex items-center gap-1.5 whitespace-nowrap text-warn">
                  <span className="size-1.5 rounded-full bg-current" aria-hidden />
                  {t("{n} риск", { n: num(flags.risk) })}
                </span>
              )}
            </>
          )}
        </Link>
        {vacancies > 0 && (
          <span className="text-ink-3">
            {t("Ещё {n} {word}", { n: num(vacancies), word: t(vacancyWord) })} · {vacancyWord === "вакансия" ? t("не входит в рейтинг") : t("не входят в рейтинг")}
          </span>
        )}
      </span>
    </Alert>
  );
}

/** Сводка «Экспорт и опт»: филиал «Завод» из Linko — отдельно от вторички, чтобы не завышать республику. */
export async function ExportSummaryCard({ data, href }: { data: ExcludedSummary; href: string }) {
  const t = await getT();
  return (
    <SummaryCard title={t("Экспорт и опт")} subtitle={t("филиал «Завод» в Linko · вне вторички")} href={href}>
      <dl className="mt-4 grid grid-cols-2 gap-x-6 gap-y-3">
        <Stat label={t("Факт, кг")} value={kg(data.factKg)} />
        <Stat label={t("Прогноз, кг")} value={kg(data.forecastKg)} />
        <Stat label={t("Выручка")} value={money(data.revenue, t)} />
        <Stat label={t("Заказов")} value={num(data.orders)} />
        <Stat label={t("АКБ")} value={num(data.akb)} />
        <Stat label={t("К прошлому месяцу")} value={delta(data.vsPrevMonth)} tone={deltaTone(data.vsPrevMonth)} />
      </dl>
    </SummaryCard>
  );
}
