import Link from "next/link";
import { ArrowUpRight } from "lucide-react";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { apiGet } from "@/lib/server-api";
import { money, num, pct } from "@/lib/sales/format";
import { apiQuery, periodQuery, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { PrimaryCard, PrimaryView } from "@/lib/sales/types";

export const metadata = { title: "Первичка · Продажи" };

function Card({ title, sub, card, share, href }: { title: string; sub: string; card: PrimaryCard; share: number | null; href: string | null }) {
  const body = (
    <div className={`flex h-full flex-col rounded-xl border border-line bg-surface p-5 shadow-sm ${href ? "transition-colors group-hover:border-accent/40" : ""}`}>
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <div className="flex items-center gap-2 font-semibold text-ink">
            {title}
            {href && <ArrowUpRight className="size-4 text-ink-3 group-hover:text-accent-strong" />}
          </div>
          <div className="mt-0.5 text-xs text-ink-3">{sub}</div>
        </div>
        <div className="text-right">
          <div className="whitespace-nowrap text-[26px] font-semibold leading-none tabular-nums text-ink">
            {num(card.kg / 1000, 1)} <span className="text-sm font-normal text-ink-3">т</span>
          </div>
          <div className="mt-1 text-xs tabular-nums text-ink-2">{money(card.sumFactory)}</div>
        </div>
      </div>
      <div className="mt-4 h-2 overflow-hidden rounded-full bg-muted">
        <div className="h-full rounded-full bg-accent" style={{ width: `${Math.min(100, (share ?? 0) * 100)}%` }} />
      </div>
      <div className="mt-2 text-xs text-ink-2">{pct(share, 1)} всей отгрузки завода</div>
    </div>
  );
  return href ? (
    <Link href={href} className="group block">
      {body}
    </Link>
  ) : (
    body
  );
}

/** «Первичка»: выбор — республика (завод → дилеры) или экспорт (завод → склад экспорта). Цифры — с начала года. */
export default async function PrimaryPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const sp = await searchParams;
  const data = await apiGet<PrimaryView>(`/api/sales/primary${apiQuery(sp)}`, "/sales/primary");
  const q = periodQuery(sp);
  const total = data.republic.kg + data.export.kg;

  return (
    <SalesFrame title="Первичка" subtitle={`Завод — дилерам · ${data.year} год с начала года · выберите, что смотреть`} crumbs={[{ label: "Первичка" }]} sp={sp}>
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        <Card
          title="Республика"
          sub={`завод → дилеры · ${num(data.republic.counterparties)} контрагентов`}
          card={data.republic}
          share={total ? data.republic.kg / total : null}
          href={withQuery("/sales/primary/republic", q)}
        />
        <Card
          title="Экспорт"
          sub={`завод → склад «${data.exportStock ?? "Экспорт"}» · ${num(data.export.transfers)} перемещений`}
          card={data.export}
          share={total ? data.export.kg / total : null}
          href={null}
        />
      </div>
      <p className="mt-4 text-xs text-ink-3">
        Источник — перемещения Linko со склада завода. Экспорт по странам в Linko не разбит: здесь — всё, что завод отправил на склад экспорта.
      </p>
    </SalesFrame>
  );
}
