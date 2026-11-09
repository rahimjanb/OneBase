import Link from "next/link";
import { ArrowRight, Gavel, PackageCheck, PackageOpen } from "lucide-react";
import { getT } from "@/i18n/server";
import { hasAttention, type HomeView } from "@/lib/arrivals";
import { money, stampRu } from "@/lib/procurement";

/**
 * Над «Обзором» компании — руководителю отдела, директору и финансисту: заявки на их шаге согласования, что можно получить на складе по
 * заявкам отдела и своим, и (директору, финансисту) что принято на склад за неделю. Дел нет — блока нет.
 */
export async function HomeAttention({ data: request }: { data: Promise<HomeView | null> }) {
  const data = await request;
  if (!data || !hasAttention(data)) return null;
  const t = await getT();
  const q = data.requests;
  const pickUp = Math.max(q.departmentToPickUp, q.mineToPickUp);
  const accepted = (data.roles.director || data.roles.financier) && data.acceptedCount > 0;
  return (
    <section aria-labelledby="home-attention" className="rounded-2xl border border-line bg-surface p-4 sm:p-5">
      <h2 id="home-attention" className="text-base font-semibold text-ink">
        {t("Ждут вас")}
      </h2>
      <div className="mt-3 grid gap-4 md:grid-cols-[repeat(auto-fit,minmax(18rem,1fr))]">
        {q.awaitingMe > 0 && (
          <div className="min-w-0">
            <Link href="/requests/approvals" className="flex items-center gap-2 text-sm font-medium text-accent-strong hover:underline">
              <Gavel className="size-4" aria-hidden /> {t("Ждут вашего согласования: {n}", { n: q.awaitingMe })} <ArrowRight className="size-3.5" aria-hidden />
            </Link>
            <ul className="mt-2 space-y-1.5">
              {q.approvals.slice(0, 3).map((a) => (
                <li key={a.id}>
                  <Link href={`/requests/${a.id}`} className="flex items-baseline justify-between gap-3 text-sm hover:text-accent-strong">
                    <span className="min-w-0 truncate text-ink">{`${a.number} · ${a.title}`}</span>
                    <span className="shrink-0 tabular-nums text-ink-2">{money(a.amount)}</span>
                  </Link>
                </li>
              ))}
            </ul>
          </div>
        )}
        {pickUp > 0 && (
          <div className="min-w-0">
            <Link href={q.departmentToPickUp > 0 ? "/requests/department" : "/requests/my"} className="flex items-center gap-2 text-sm font-medium text-accent-strong hover:underline">
              <PackageCheck className="size-4" aria-hidden /> {t("Можно получить на складе: {n}", { n: pickUp })} <ArrowRight className="size-3.5" aria-hidden />
            </Link>
            <p className="mt-2 text-sm text-ink-2">{t(q.departmentToPickUp > 0 ? "Товар по заявкам отдела ждёт на складе — заберите у кладовщика." : "Товар по вашим заявкам ждёт на складе — заберите у кладовщика.")}</p>
          </div>
        )}
        {accepted && (
          <div className="min-w-0">
            <p className="flex items-center gap-2 text-sm font-medium text-ink">
              <PackageOpen className="size-4 text-ink-3" aria-hidden /> {t("Принято на склад за неделю: {sum} сум", { sum: money(data.acceptedAmount) })}
            </p>
            <ul className="mt-2 space-y-1.5">
              {data.accepted.slice(0, 3).map((a) => (
                <li key={a.id}>
                  <Link href={`/procurement/arrivals/${a.id}`} className="flex items-baseline justify-between gap-3 text-sm hover:text-accent-strong">
                    <span className="min-w-0 truncate text-ink">{`${a.supplierName} · ${a.summary}`}</span>
                    <span className="shrink-0 text-xs text-ink-3">{a.acceptedAt ? stampRu(a.acceptedAt) : ""}</span>
                  </Link>
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </section>
  );
}
