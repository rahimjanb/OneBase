import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { requireAnalytik, periodParams, type SearchParams } from "@/components/analytik/guard";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import { Overview } from "@/components/analytik/Overview";
import { OverviewActions } from "@/components/analytik/overview/top";
import type { Dashboard } from "@/lib/analytik";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Аналитик · OneBase") };
}

/**
 * «Аналитик → Обзор»: Executive Intelligence Dashboard за период (прогноз выручки грузится на экране отдельно). Палитра макета, как у
 * «Вторички» и «Симулятора»: непрозрачные карточки, свечение сверху справа и снизу слева. На телефоне и планшете общая шапка скрыта
 * (TopbarFrame) — свою рисует экран.
 */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const me = await requireAnalytik();
  const t = await getT();
  const sp = await searchParams;
  const data = await apiGet<Dashboard>(`/api/analytik/dashboard?${periodParams(sp)}`, "/analytik");
  return (
    <div data-palette="sales-secondary" data-glow="both" className="min-h-full">
      <div className="max-lg:hidden">
        <PageHeader
          title={t("Аналитик")}
          subtitle={t("OneBase Intelligence: состояние компании, риски, прогнозы и AI-аналитик по данным всех отделов")}
          breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Аналитик"), href: "/analytik" }, { label: t("Обзор") }]}
          actions={<OverviewActions at={data.at} freshness={data.freshness} period={data.period} />}
        />
      </div>
      <PageBody variant="display">
        <Overview data={data} permissions={me?.permissions ?? []} />
      </PageBody>
    </div>
  );
}
