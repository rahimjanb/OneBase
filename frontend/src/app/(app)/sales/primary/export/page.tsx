import { PrimaryExport } from "@/components/sales/primary";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import { date, monthLabel } from "@/lib/sales/format";
import { apiQuery, periodQuery, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { PrimaryView } from "@/lib/sales/types";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Первичка · Экспорт · Продажи") };
}

/** «Первичка → Экспорт»: заказы филиала «Завод» экспортным точкам — по месяцам, странам и дням; from/to/day/dealer — календарь отгрузок. */
export default async function PrimaryExportPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const t = await getT();
  const sp = await searchParams;
  const data = await apiGet<PrimaryView>(`/api/sales/primary/export${apiQuery(sp, ["from", "to", "day", "dealer"])}`, "/sales/primary/export");
  const q = periodQuery(sp);

  return (
    <SalesFrame
      title={t("Первичка")}
      subtitle={`${t("Завод — на экспорт")} · ${monthLabel(data.year, data.month, t).replace(/^\S+/, (w) => t(w))} · ${data.running ? t("данные по {date}", { date: date(data.dataThrough) }) : data.dataThrough ? t("месяц закрыт") : t("отгрузок нет")}`}
      crumbs={[{ label: t("Первичка"), href: withQuery("/sales/primary", q) }, { label: t("Экспорт") }]}
      back={withQuery("/sales/primary", q)}
      sp={sp}
    >
      <PrimaryExport data={data} />
    </SalesFrame>
  );
}
