import { PageBody, PageHeader, type Crumb } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { ReportPlans } from "./PlanAvailability";
import { date, dayMonth } from "@/lib/sales/format";
import { periodQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { Period } from "@/lib/sales/types";

/** Чип «данные по ДД.ММ» — отчётный день идущего месяца (может быть сегодняшним — на время синхронизации); у закрытого месяца не показывается. */
async function DataThroughChip({ through }: { through: string }) {
  const t = await getT();
  return (
    <span
      className="rounded-full border border-line bg-surface px-3 py-1 text-xs tabular-nums text-ink-2 max-lg:py-1.5"
      title={t("Отчёт — по {date} включительно; за сегодняшний день — данные на время последней синхронизации Linko (каждые 30 минут)", { date: date(through) })}
    >
      {t("данные по {date}", { date: dayMonth(through) })}
    </span>
  );
}

/**
 * Заголовок и тело страницы раздела «Продажи»: «← Назад» слева, крошки, название.
 * Разделы — в верхней панели, период и «Обновить» — в полосе раздела (sales/layout.tsx).
 * period — у страниц вторички: чип «данные по ДД.ММ» справа в шапке, пока месяц идёт; какие планы заведены на месяц — переключателю плана.
 * through — тот же чип у страниц со своим периодом (обзор «Первички»): по какой день данные.
 * Серверный (подключают только страницы): перевод — getT(); заголовок и крошки переводит PageHeader.
 */
export async function SalesFrame({
  title,
  subtitle,
  crumbs = [],
  back,
  sp,
  period,
  through,
  children,
}: {
  title: string;
  subtitle?: string;
  crumbs?: Crumb[];
  back?: string;
  sp: SalesSearchParams;
  period?: Period;
  through?: string | null;
  children: React.ReactNode;
}) {
  const t = await getT();
  const query = periodQuery(sp);

  return (
    <>
      <PageHeader
        title={title}
        subtitle={subtitle}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Отделы"), href: "/departments" }, { label: t("Продажи"), href: query ? `/sales?${query}` : "/sales" }, ...crumbs]}
        back={back}
        actions={period && !period.closed ? <DataThroughChip through={period.dataThrough} /> : through ? <DataThroughChip through={through} /> : undefined}
      />
      {period && <ReportPlans year={period.year} month={period.month} plans={period.availablePlans} />}
      <PageBody>{children}</PageBody>
    </>
  );
}
