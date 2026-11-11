import { AgentsCard, ExportCard, KpiCards, MonthDays, PlanCard, RepublicCard } from "@/components/sales/overview";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { getT } from "@/i18n/server";
import { apiGet, apiSession } from "@/lib/server-api";
import { apiQuery, periodQuery, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { OverviewView } from "@/lib/sales/types";
import type { Me } from "@/lib/users";
import { redirect } from "next/navigation";
import { isLimited } from "@/lib/sales/access";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Продажи · OneBase") };
}

/**
 * «Вторичка», верхний уровень: «Выполнение плана» с темпом, пять KPI, блок ТП и карточки «Республика» и «Экспорт и опт».
 * Права пользователя — только для ссылки «Задать план»: ведёт туда, где план можно загрузить (у кого прав нет — ссылки нет).
 * data-palette — палитра ТЗ «Вторички» (globals.css): цвета только этой страницы, остальные страницы — на токенах приложения.
 */
export default async function SalesPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const t = await getT();
  const sp = await searchParams;
  // РМ / СВР / ТП — сразу на свою страницу (направление, регион, карточку ТП), с тем же месяцем: старт продаж — вся страна.
  const scope = (await apiSession<Me>("/api/auth/me"))?.salesScope;
  if (isLimited(scope)) redirect(withQuery(scope!.home, periodQuery(sp)));
  const [data, me] = await Promise.all([apiGet<OverviewView>(`/api/sales/overview${apiQuery(sp)}`, "/sales"), apiSession<Me>("/api/auth/me")]);
  const q = periodQuery(sp);

  return (
    <div data-palette="sales-secondary">
      <SalesFrame
        title={t("Вторичка")}
        // На телефоне подзаголовка нет, как в макете: справа от названия — «по 07.11 · 7 из 30 дн.».
        subtitle={<span className="text-ink-3 max-md:hidden">{t("План, факт и работа торговых представителей")}</span>}
        sp={sp}
        period={data.period}
        aside={data.period.closed ? undefined : <MonthDays period={data.period} />}
        display
      >
        <div className="flex flex-col gap-3.5 md:gap-[18px]">
          <PlanCard kpi={data.kpi} period={data.period} permissions={me?.permissions ?? []} />
          <KpiCards kpi={data.kpi} period={data.period} href={withQuery("/sales/republic", q)} />
          <AgentsCard activeAgents={data.activeAgents} flags={data.flags} vacancies={data.vacancies} query={q} />
          <div className="grid gap-3.5 md:grid-cols-2 md:gap-4">
            <RepublicCard unit={data.republic} period={data.period} href={withQuery("/sales/republic", q)} level={data.kpi.pace?.level} />
            {data.excluded && <ExportCard data={data.excluded} href={withQuery("/sales/export", q)} />}
          </div>
        </div>
      </SalesFrame>
    </div>
  );
}
