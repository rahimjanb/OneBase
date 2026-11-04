import { DirectionCard, PrimaryEmpty, PrimaryMethodology, PrimaryTotal } from "@/components/sales/primary/PrimaryOverviewView";
import { PrimaryMobileFilters } from "@/components/sales/primary/PrimaryFilters";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { getT } from "@/i18n/server";
import { choiceQuery, detailMonth, periodWords, readChoice } from "@/lib/primary-period";
import { apiGet } from "@/lib/server-api";
import { monthName, monthShort } from "@/lib/sales/format";
import { periodQuery, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { PrimaryOverview } from "@/lib/sales/types";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Первичка · Продажи") };
}

/**
 * «Первичка»: отгрузка завода всего и два направления — республика (завод → дилеры и прямые клиенты) и экспорт — за месяц, квартал или с
 * начала года (?period=…&year=…&month|quarter=…&unit=t|sum). Подзаголовок и все цифры — из выбранного периода; карточки ведут на
 * детальные страницы направлений (они — за месяц: у квартала и года — последний месяц с данными). Методика направлений — как у них.
 */
export default async function PrimaryPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const t = await getT();
  const sp = await searchParams;
  const choice = readChoice(sp);
  const data = await apiGet<PrimaryOverview>(`/api/sales/primary/overview?${choiceQuery(choice, false)}`, "/sales/primary");
  const words = periodWords(data.period, { month: (m) => monthName(m, t), short: (m) => monthShort(m, t), t });
  const { year, month } = detailMonth(data.period);
  const detail = (path: string) => withQuery(path, periodQuery({ ...sp, year: String(year), month: String(month) }));
  const empty = data.totalTons === 0 && data.totalAmount === 0;

  return (
    <SalesFrame
      title={t("Первичка")}
      subtitle={t("Отгрузки завода дилерам, прямым клиентам и на экспорт · {period}", { period: words })}
      crumbs={[{ label: t("Первичка") }]}
      sp={sp}
      through={data.period.through}
    >
      <div className="space-y-4 lg:space-y-6">
        <PrimaryMobileFilters years={data.years} through={data.dataThrough} />
        <PrimaryTotal data={data} unit={choice.unit} words={words} />
        {empty && <PrimaryEmpty choice={choice} />}
        <section aria-labelledby="primary-directions">
          <h2 id="primary-directions" className="mb-3 text-[15px] font-bold text-ink max-lg:text-[17px]">
            <span className="lg:hidden">{t("Направления")}</span>
            <span className="max-lg:hidden">{t("Выберите направление")}</span>
          </h2>
          <div className="grid gap-4 lg:grid-cols-2">
            {data.directions.map((d) => (
              <DirectionCard key={d.id} d={d} unit={choice.unit} href={detail(d.id === "republic" ? "/sales/primary/republic" : "/sales/primary/export")} />
            ))}
          </div>
        </section>
        <PrimaryMethodology />
      </div>
    </SalesFrame>
  );
}
