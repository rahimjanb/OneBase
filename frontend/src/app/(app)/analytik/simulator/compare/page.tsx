import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { one, requireAnalytik, type SearchParams } from "@/components/analytik/guard";
import { CompareView } from "@/components/analytik/simulator/SimScenarios";
import { getT } from "@/i18n/server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Сравнение сценариев · OneBase") };
}

/** «Аналитик → Симулятор → Сравнение»: 2–5 сохранённых сценариев (ids через запятую), разница с первым. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireAnalytik();
  const t = await getT();
  const ids = (one(await searchParams, "ids") ?? "").split(",").map((x) => x.trim()).filter(Boolean).slice(0, 5);
  return (
    <div data-palette="sales-secondary" data-glow="both" className="min-h-full">
      <PageHeader
        variant="display"
        back="/analytik/simulator"
        title={t("Сравнение сценариев")}
        subtitle={t("Выручка, расходы, прибыль, производство, сырьё, закупки, остатки и риски — значения и разница с первым сценарием")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Аналитик"), href: "/analytik" }, { label: t("Симулятор"), href: "/analytik/simulator" }, { label: t("Сравнение") }]}
      />
      <PageBody variant="display">
        {ids.length < 2 ? <p className="text-[14px] text-ink-3">{t("Выберите от 2 до 5 сценариев в «Моих сценариях» симулятора.")}</p> : <CompareView ids={ids} />}
      </PageBody>
    </div>
  );
}
