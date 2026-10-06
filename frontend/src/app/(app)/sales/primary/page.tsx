import { ExecutionBar, SummaryCard } from "@/components/sales/bits";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { apiGet } from "@/lib/server-api";
import { money, num, ordersLabel, pct } from "@/lib/sales/format";
import { apiQuery, periodQuery, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { PrimaryCard, PrimaryView } from "@/lib/sales/types";

export const metadata = { title: "Первичка · Продажи" };

function Card({ title, sub, card, href }: { title: string; sub: string; card: PrimaryCard; href: string | null }) {
  const share = card.share;
  return (
    <SummaryCard
      title={title}
      subtitle={sub}
      href={href}
      aside={
        <div className="shrink-0 text-right">
          <div className="whitespace-nowrap text-[24px] font-semibold leading-none tracking-tight tabular-nums text-ink">
            {num(card.kg / 1000, 1)} <span className="text-sm font-normal text-ink-3">т</span>
          </div>
          <div className="mt-1 text-xs tabular-nums text-ink-2">{money(card.sumFactory)}</div>
        </div>
      }
    >
      <div
        className="mt-4"
        role="progressbar"
        aria-label="Доля всей отгрузки завода"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={share == null ? undefined : Math.round(share * 100)}
      >
        <ExecutionBar value={share} tone="accent" />
      </div>
      <p className="mt-2 text-xs text-ink-2">{pct(share, 1)} всей отгрузки завода</p>
    </SummaryCard>
  );
}

/** «Первичка»: выбор — республика (завод → дилеры) или экспорт (заказы «Завода» экспортным точкам). Цифры — с начала года. */
export default async function PrimaryPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const sp = await searchParams;
  const data = await apiGet<PrimaryView>(`/api/sales/primary${apiQuery(sp)}`, "/sales/primary");
  const q = periodQuery(sp);

  return (
    <SalesFrame title="Первичка" subtitle={`Завод — дилерам и прямым клиентам · ${data.year} год с начала года · выберите, что смотреть`} crumbs={[{ label: "Первичка" }]} sp={sp}>
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        <Card
          title="Республика"
          sub={`завод → дилеры и прямые клиенты · ${num(data.republic.counterparties)} контрагентов`}
          card={data.republic}
          href={withQuery("/sales/primary/republic", q)}
        />
        <Card
          title="Экспорт"
          sub={`завод → экспорт · ${num(data.export.counterparties)} стран · ${ordersLabel(data.export.transfers)}`}
          card={data.export}
          href={withQuery("/sales/primary/export", q)}
        />
      </div>
      <p className="mt-4 text-xs text-ink-3">
        Республика — перемещения Linko со склада завода на склады дилеров и заказы прямых клиентов завода (базары, сети, фирменный магазин), нетто
        возвратов. Экспорт — заказы филиала «Завод» торговым точкам с типом EXPORT; страна — по названию или адресу точки в Linko.
      </p>
    </SalesFrame>
  );
}
