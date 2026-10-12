import { SalesFrame } from "@/components/sales/SalesFrame";
import { PnlReport } from "@/components/sales/pnl";
import { apiGet, apiSession } from "@/lib/server-api";
import type { SalesSearchParams } from "@/lib/sales/query";
import type { PnlView } from "@/lib/sales/pnl";
import { has, type Me } from "@/lib/users";

export const metadata = { title: "P&L дилеров · Продажи" };

/** «P&L дилеров»: свой выбор месяца (pnl=2026-09) — месяцы загруженного P&L, а не месяцы продаж из полосы раздела. */
export default async function PnlPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const sp = await searchParams;
  const period = typeof sp.pnl === "string" ? /^(\d{4})-(\d{2})$/.exec(sp.pnl) : null;
  const query = period ? `?year=${period[1]}&month=${Number(period[2])}` : "";
  const [data, me] = await Promise.all([apiGet<PnlView>(`/api/sales/pnl${query}`, "/sales/pnl"), apiSession<Me>("/api/auth/me")]);

  return (
    <SalesFrame title="P&L дилеров" subtitle="Прибыль завода на дилере за месяц, сум" sp={sp}>
      <PnlReport data={data} canLoad={has(me, "ai.data.manage")} />
    </SalesFrame>
  );
}
