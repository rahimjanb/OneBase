import { ExecutionBar, SummaryCard } from "@/components/sales/bits";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import { money, num, pct, plural } from "@/lib/sales/format";
import { apiQuery, periodQuery, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { PrimaryCard, PrimaryView } from "@/lib/sales/types";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Первичка · Продажи") };
}

async function Card({ title, sub, card, href }: { title: string; sub: string; card: PrimaryCard; href: string | null }) {
  const t = await getT();
  const share = card.share;
  return (
    <SummaryCard
      title={title}
      subtitle={sub}
      href={href}
      aside={
        <div className="shrink-0 text-right">
          <div className="whitespace-nowrap text-[1.5rem] font-semibold leading-none tracking-tight tabular-nums text-ink">
            {num(card.kg / 1000, 1)} <span className="text-sm font-normal text-ink-3">{t("т")}</span>
          </div>
          <div className="mt-1 text-xs tabular-nums text-ink-2">{money(card.sumFactory, t)}</div>
        </div>
      }
    >
      <div
        className="mt-4"
        role="progressbar"
        aria-label={t("Доля всей отгрузки завода")}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={share == null ? undefined : Math.round(share * 100)}
      >
        <ExecutionBar value={share} tone="accent" />
      </div>
      <p className="mt-2 text-xs text-ink-2">{t("{pct} всей отгрузки завода", { pct: pct(share, 1) })}</p>
    </SummaryCard>
  );
}

/** «Первичка»: выбор — республика (завод → дилеры) или экспорт (заказы «Завода» экспортным точкам). Цифры — с начала года. */
export default async function PrimaryPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const t = await getT();
  const sp = await searchParams;
  const data = await apiGet<PrimaryView>(`/api/sales/primary${apiQuery(sp)}`, "/sales/primary");
  const q = periodQuery(sp);

  return (
    <SalesFrame
      title={t("Первичка")}
      subtitle={t("Завод — дилерам и прямым клиентам · {year} год с начала года · выберите, что смотреть", { year: data.year })}
      crumbs={[{ label: t("Первичка") }]}
      sp={sp}
    >
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        <Card
          title={t("Республика")}
          sub={t("завод → дилеры и прямые клиенты · {n} контрагентов", { n: num(data.republic.counterparties) })}
          card={data.republic}
          href={withQuery("/sales/primary/republic", q)}
        />
        <Card
          title={t("Экспорт")}
          sub={t("завод → экспорт · {n} стран · {orders}", {
            n: num(data.export.counterparties),
            orders: `${num(data.export.transfers)} ${t(plural(data.export.transfers, ["заказ", "заказа", "заказов"]))}`,
          })}
          card={data.export}
          href={withQuery("/sales/primary/export", q)}
        />
      </div>
      <p className="mt-4 text-xs text-ink-3">
        {t(
          "Республика — перемещения Linko со склада завода на склады дилеров и заказы прямых клиентов завода (базары, сети, фирменный магазин), нетто возвратов. Экспорт — заказы филиала «Завод» торговым точкам с типом EXPORT; страна — по названию или адресу точки в Linko.",
        )}
      </p>
    </SalesFrame>
  );
}
