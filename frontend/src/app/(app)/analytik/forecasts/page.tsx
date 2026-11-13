import { ForecastsScreen } from "@/components/analytik/forecasts/ForecastsScreen";
import { requireAnalytik, type SearchParams } from "@/components/analytik/guard";
import { getT } from "@/i18n/server";
import { parseParams, type ForecastMetric } from "@/lib/forecasts";
import { screenPermissions } from "@/lib/navigation";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Прогнозы · OneBase") };
}

/** Показатели прогноза, открытые пользователю — как проверяет сервер (ForecastService): продажи, оплаты (вся организация), расход сырья. */
function metricsFor(permissions: readonly string[], organization: boolean): ForecastMetric[] {
  const has = (...codes: string[]) => codes.some((c) => permissions.includes(c));
  return [
    ...(has("sales.read") ? (["sales.revenue", "sales.kg", "sales.orders"] as const) : []),
    ...(has("finance.read") && organization ? (["finance.payments"] as const) : []),
    ...(has("procurement.read", "procurement.manage", "procurement.warehouse", "production.read", "production.manage") ? (["procurement.consumption"] as const) : []),
  ];
}

/**
 * «Аналитик → Прогнозы» по макету (untitled.pen: UVDBc — компьютер, CBnGo — телефон). Параметры — в адресе (metric, item, step, h, whatif,
 * scenario, past); прогноз и точность прошлых месяцев считает сервер, экран их только показывает. На телефоне и планшете общая шапка скрыта
 * (TopbarFrame) — свою рисует экран.
 */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const me = await requireAnalytik();
  const t = await getT();
  const permissions = me ? screenPermissions(me) : [];
  const metrics = metricsFor(permissions, !me?.salesScope || me.salesScope.level === "Organization");
  const sp = await searchParams;
  if (metrics.length === 0) {
    return (
      <div className="p-6 text-[14px] text-ink-2">
        {t("Прогнозы строятся по продажам, оплатам и расходу сырья — у вас нет доступа ни к одному из этих разделов.")}
      </div>
    );
  }
  return (
    <div data-palette="sales-secondary" data-glow="both" className="min-h-full">
      <ForecastsScreen initial={parseParams(sp, metrics)} metrics={metrics} permissions={permissions} />
    </div>
  );
}
