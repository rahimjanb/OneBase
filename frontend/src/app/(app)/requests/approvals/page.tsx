import Link from "next/link";
import { redirect } from "next/navigation";
import { ArrowRight, Gavel, Repeat } from "lucide-react";
import { RequestPriorityPill } from "@/components/requests/RequestKit";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { OrderStatusPill } from "@/components/procurement/ProcKit";
import { dateRu, money, stampRu, type OrderRow, type Page, type RequestNav, type RequestPage } from "@/lib/procurement";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Согласования · OneBase") };
}

/**
 * «Согласования» — очередь директора: заявки, переданные снабжением на согласование (давно ждущие — первыми), с суммой к закупке,
 * причиной и пометкой повторного согласования. Решение — в карточке заявки (там позиции, остатки, история и вложения). Видна только
 * роли «Директор»; утвердить через API может тоже только она.
 */
export default async function ApprovalsPage() {
  await requireRequests();
  const t = await getT();
  const nav = await apiGet<RequestNav>("/api/procurement/nav", "/requests/approvals");
  if (!nav.isDirector) redirect("/requests");
  const [data, orders] = await Promise.all([
    apiGet<RequestPage>("/api/procurement/requests?scope=approvals&pageSize=100", "/requests/approvals"),
    apiGet<Page<OrderRow>>("/api/procurement/orders?status=awaiting&pageSize=100", "/requests/approvals"),
  ]);
  const total = data.items.reduce((sum, r) => sum + r.purchaseTotal, 0);

  return (
    <>
      <PageHeader
        title={t("Согласования")}
        subtitle={t("Заявки, которые снабжение передало вам на согласование: утвердите, отклоните или верните на доработку")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Заявки"), href: "/requests" }, { label: t("Согласования") }]}
      />
      <PageBody>
        {orders.items.length > 0 && (
          <section className="mb-6">
            <h2 className="mb-2 text-sm font-semibold text-ink">{t("Заказы поставщикам: дороже утверждённого или изменены после утверждения")}</h2>
            <ul className="space-y-2">
              {orders.items.map((o) => (
                <li key={o.id}>
                  <Link href={`/procurement/orders/${o.id}`} className="group flex flex-wrap items-center gap-3 rounded-2xl border border-violet/30 bg-surface p-4 hover:border-violet/60">
                    <span className="min-w-0 flex-1">
                      <span className="flex flex-wrap items-center gap-2">
                        <span className="text-xs tabular-nums text-ink-3">{o.number}</span>
                        <OrderStatusPill status={o.status} />
                        {o.approvalRound > 1 && <span className="text-[11px] text-violet-light">{t("повторно")}</span>}
                      </span>
                      <span className="mt-1 block font-semibold text-ink group-hover:text-accent-strong">{o.supplierName}</span>
                      <span className="block text-sm text-ink-2">
                        {o.firstItem}
                        {o.lines > 1 && ` ${t("и ещё {n}", { n: o.lines - 1 })}`} · {o.requests.join(", ")}
                      </span>
                    </span>
                    <span className="text-right">
                      <span className="block text-lg font-semibold tabular-nums text-ink">{money(o.total)}</span>
                      <span className="block text-xs text-ink-3">{o.expectedDate ? t("ожидается {date}", { date: dateRu(o.expectedDate) }) : t("сум")}</span>
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          </section>
        )}
        {data.items.length === 0 && orders.items.length > 0 ? null : data.items.length === 0 ? (
          <section className="flex flex-col items-center rounded-2xl border border-line bg-surface px-6 py-14 text-center">
            <span className="grid size-14 place-items-center rounded-2xl bg-ok-soft text-ok">
              <Gavel className="size-7" aria-hidden />
            </span>
            <h2 className="mt-4 text-lg font-semibold text-ink">{t("Согласовывать нечего")}</h2>
            <p className="mt-1.5 max-w-md text-sm text-ink-2">{t("Новые заявки появятся здесь, когда снабжение передаст их вам.")}</p>
          </section>
        ) : (
          <>
            <p className="mb-3 text-sm text-ink-2">
              {t("Ждут решения: {n}", { n: data.items.length })}
              {total > 0 && (
                <span className="text-ink-3">
                  {" "}
                  · {t("к закупке")} ≈ {money(total)} {t("сум")}
                </span>
              )}
            </p>
            <ul className="space-y-3">
              {data.items.map((r) => (
                <li key={r.id}>
                  <Link href={`/requests/${r.id}`} className="group flex flex-col gap-3 rounded-2xl border border-line bg-surface p-4 transition-colors hover:border-accent/40 sm:flex-row sm:items-center sm:p-5">
                    <span className="min-w-0 flex-1">
                      <span className="flex flex-wrap items-center gap-2">
                        <span className="text-xs tabular-nums text-ink-3">{r.number}</span>
                        <RequestPriorityPill priority={r.priority} />
                        {r.approvalRound > 1 && (
                          <span className="inline-flex items-center gap-1 rounded-md bg-violet-soft px-1.5 py-0.5 text-[11px] font-semibold text-violet-light">
                            <Repeat className="size-3" aria-hidden /> {t("повторно")}
                          </span>
                        )}
                      </span>
                      <span className="mt-1 block font-semibold text-ink group-hover:text-accent-strong">{r.title}</span>
                      <span className="mt-0.5 block text-sm text-ink-2">
                        {r.departmentName} · {r.initiatorName} · {r.itemsCount === 1 ? r.firstItem : t("{name} и ещё {n}", { name: r.firstItem ?? "", n: r.itemsCount - 1 })}
                      </span>
                      {r.reason && <span className="mt-1 line-clamp-2 block text-sm text-ink-3">{r.reason}</span>}
                    </span>
                    <span className="flex shrink-0 items-center justify-between gap-4 sm:flex-col sm:items-end sm:justify-center">
                      <span className="text-right">
                        <span className="block text-lg font-semibold tabular-nums text-ink">{r.purchaseTotal > 0 ? money(r.purchaseTotal) : "—"}</span>
                        <span className="block text-xs text-ink-3">{t("к закупке, сум")}</span>
                      </span>
                      <span className="inline-flex items-center gap-1 text-xs text-ink-3">
                        {r.forwardedAt && t("ждёт с {date}", { date: stampRu(r.forwardedAt) })} <ArrowRight className="size-3.5" aria-hidden />
                      </span>
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          </>
        )}
      </PageBody>
    </>
  );
}
