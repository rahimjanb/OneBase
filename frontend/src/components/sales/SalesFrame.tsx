import { PageBody, PageHeader, type Crumb } from "@/components/shell/PageHeader";
import { ReportPlans } from "./PlanAvailability";
import { date, dayMonth } from "@/lib/sales/format";
import { periodQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { Period } from "@/lib/sales/types";

/** Чип «данные по ДД.ММ» — отчётный день идущего месяца (последний полный день); у закрытого месяца не показывается. */
function DataThroughChip({ period }: { period: Period }) {
  return (
    <span
      className="rounded-full border border-line bg-surface px-3 py-1 text-xs tabular-nums text-ink-2 max-lg:py-1.5"
      title={`Отчёт — по ${date(period.dataThrough)}, последний полный день: приёмки сегодняшнего дня войдут в отчёт завтра`}
    >
      данные по {dayMonth(period.dataThrough)}
    </span>
  );
}

/**
 * Заголовок и тело страницы раздела «Продажи»: «← Назад» слева, крошки, название.
 * Разделы — в верхней панели, период и «Обновить» — в полосе раздела (sales/layout.tsx).
 * period — у страниц вторички: чип «данные по ДД.ММ» справа в шапке, пока месяц идёт; какие планы заведены на месяц — переключателю плана.
 */
export function SalesFrame({
  title,
  subtitle,
  crumbs = [],
  back,
  sp,
  period,
  children,
}: {
  title: string;
  subtitle?: string;
  crumbs?: Crumb[];
  back?: string;
  sp: SalesSearchParams;
  period?: Period;
  children: React.ReactNode;
}) {
  const query = periodQuery(sp);

  return (
    <>
      <PageHeader
        title={title}
        subtitle={subtitle}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: "Продажи", href: query ? `/sales?${query}` : "/sales" }, ...crumbs]}
        back={back}
        actions={period && !period.closed ? <DataThroughChip period={period} /> : undefined}
      />
      {period && <ReportPlans year={period.year} month={period.month} plans={period.availablePlans} />}
      <PageBody>{children}</PageBody>
    </>
  );
}
