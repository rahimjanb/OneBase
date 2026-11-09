import Link from "next/link";
import { ArrowRight, BadgeCheck, CalendarRange, ClipboardPen, FilePen, Flame, Hourglass, Inbox, Send, Undo2 } from "lucide-react";
import { RequestPriorityPill, RequestStatusPill } from "@/components/requests/RequestKit";
import { NewRequestButton, requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { plural } from "@/lib/format";
import { money, type RequestNav, type RequestOverview, type RequestRow } from "@/lib/procurement";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Заявки · OneBase") };
}

/** Строки заявок в карточке обзора: номер и название, отдел, приоритет, статус, сумма. */
async function Rows({ rows, empty }: { rows: RequestRow[]; empty: string }) {
  const t = await getT();
  if (rows.length === 0) return <p className="py-6 text-center text-sm text-ink-3">{t(empty)}</p>;
  return (
    <ul className="divide-y divide-line">
      {rows.map((r) => (
        <li key={r.id}>
          <Link href={`/requests/${r.id}`} className="group flex items-center gap-3 py-2.5">
            <span className="min-w-0 flex-1">
              <span className="block truncate text-sm font-medium text-ink group-hover:text-accent-strong">{r.title}</span>
              <span className="block truncate text-xs text-ink-3">
                <span className="tabular-nums">{r.number}</span> · {r.departmentName}
                {!r.mine && ` · ${r.initiatorName}`}
              </span>
            </span>
            <RequestPriorityPill priority={r.priority} className="max-sm:hidden" />
            <RequestStatusPill status={r.status} />
            <span className="w-28 text-right text-xs tabular-nums text-ink-2 max-md:hidden">{r.estimatedTotal > 0 ? `${money(r.estimatedTotal)} ${t("сум")}` : "—"}</span>
          </Link>
        </li>
      ))}
    </ul>
  );
}

/**
 * «Заявки → Обзор»: заявки по статусам (из доступных пользователю), что ждёт вашего действия (свои черновики и возвраты, у снабжения —
 * отправленные), заявки месяца с суммой и категориями, последние заявки. Все числа — из базы; заявок нет — так и написано.
 */
export default async function RequestsPage() {
  await requireRequests();
  const t = await getT();
  const [overview, nav] = await Promise.all([apiGet<RequestOverview>("/api/procurement/overview", "/requests"), apiGet<RequestNav>("/api/procurement/nav", "/requests")]);
  const list = nav.readsAll ? "/requests/all" : "/requests/my";
  const s = overview.byStatus;
  const total = Object.values(s).reduce((a, b) => a + b, 0);

  const tiles = [
    { label: "Черновики", value: s.Draft, icon: FilePen, href: "/requests/my?status=draft", tone: "text-ink-2" },
    {
      label: "На согласовании",
      value: (s.HeadApproval ?? 0) + (s.AnalystApproval ?? 0) + (s.FinanceApproval ?? 0) + (s.SupplyApproval ?? 0) + s.DirectorApproval + s.Submitted,
      icon: Hourglass,
      href: `${list}?status=HeadApproval,AnalystApproval,FinanceApproval,SupplyApproval,DirectorApproval,Submitted`,
      tone: "text-violet-light",
    },
    {
      label: "Ждут моего решения",
      value: nav.awaitingApproval,
      icon: Send,
      href: nav.decides || nav.isDirector ? "/requests/approvals" : `${list}?status=open`,
      tone: "text-accent-strong",
    },
    { label: "На доработке", value: s.RevisionRequired, icon: Undo2, href: `${list}?status=RevisionRequired`, tone: "text-warn" },
    { label: "Утверждены", value: s.Approved + s.PartiallyFulfilled, icon: BadgeCheck, href: `${list}?status=Approved,PartiallyFulfilled`, tone: "text-ok" },
    { label: "Срочные в работе", value: overview.urgentOpen, icon: Flame, href: `${list}?status=open&priority=Urgent`, tone: "text-bad" },
  ];
  // Полоса категории — по сумме (главное в закупках); сумм нет (цены не указаны) — по числу заявок.
  const bySum = overview.monthCategories.some((c) => c.total > 0);
  const measure = (c: { requests: number; total: number }) => (bySum ? c.total : c.requests);
  const maxCategory = Math.max(1, ...overview.monthCategories.map(measure));

  return (
    <>
      <PageHeader
        title={t("Заявки")}
        subtitle={t("Заявки на закупку: от потребности отдела до согласования директором")}
        actions={nav.canCreate ? <NewRequestButton /> : undefined}
      />
      <PageBody>
        {total === 0 ? (
          <section className="flex flex-col items-center rounded-2xl border border-line bg-surface px-6 py-14 text-center">
            <span className="grid size-14 place-items-center rounded-2xl bg-accent-soft text-accent-strong">
              <ClipboardPen className="size-7" aria-hidden />
            </span>
            <h2 className="mt-4 text-lg font-semibold text-ink">{t("Заявок пока нет")}</h2>
            <p className="mt-1.5 max-w-md text-sm text-ink-2">
              {t("Нужно что-то купить для отдела — бумага, картриджи, оборудование, услуги? Создайте заявку: снабжение проверит её и передаст директору на согласование.")}
            </p>
            {nav.canCreate && (
              <div className="mt-5">
                <NewRequestButton />
              </div>
            )}
          </section>
        ) : (
          <>
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-6">
              {tiles.map((tile) => (
                <Link key={tile.label} href={tile.href} className="rounded-xl border border-line bg-surface p-4 transition-colors hover:border-accent/40">
                  <tile.icon className={`size-4 ${tile.tone}`} aria-hidden />
                  <div className="mt-2 text-2xl font-semibold tabular-nums text-ink">{tile.value}</div>
                  <div className="mt-0.5 text-xs text-ink-3">{t(tile.label)}</div>
                </Link>
              ))}
            </div>

            <div className="mt-4 grid gap-4 xl:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]">
              <section className="rounded-xl border border-line bg-surface p-4 sm:p-5">
                <h2 className="mb-1 flex items-center gap-2 text-sm font-semibold text-ink">
                  <Inbox className="size-4" aria-hidden /> {t("Ждут вашего действия")}
                </h2>
                <p className="mb-2 text-xs text-ink-3">
                  {nav.decides
                    ? t("Заявки на вашем согласовании, ваши черновики и возвраты")
                    : nav.manages
                      ? t("Ваши черновики и возвраты на доработку, отправленные заявки — на проверку снабжению")
                      : t("Ваши черновики и заявки, возвращённые на доработку")}
                </p>
                <Rows rows={overview.needsAction} empty="Ничего не ждёт — всё в порядке." />
              </section>

              <section className="rounded-xl border border-line bg-surface p-4 sm:p-5">
                <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold text-ink">
                  <CalendarRange className="size-4" aria-hidden /> {t("Этот месяц")}
                </h2>
                <div className="flex flex-wrap items-end gap-x-6 gap-y-2">
                  <div>
                    <div className="text-2xl font-semibold tabular-nums text-ink">{overview.monthRequests}</div>
                    <div className="text-xs text-ink-3">{t("заявок отправлено")}</div>
                  </div>
                  <div>
                    <div className="text-2xl font-semibold tabular-nums text-ink">{overview.monthTotal > 0 ? money(overview.monthTotal) : "—"}</div>
                    <div className="text-xs text-ink-3">{t("предполагаемая сумма, сум")}</div>
                  </div>
                </div>
                {overview.monthCategories.length > 0 ? (
                  <ul className="mt-4 space-y-2.5">
                    {overview.monthCategories.map((c) => (
                      <li key={c.name}>
                        <div className="flex items-baseline justify-between gap-3 text-sm">
                          <span className="truncate text-ink-2">{c.name}</span>
                          <span className="shrink-0 text-xs tabular-nums text-ink-3">
                            {c.requests} {t(plural(c.requests, ["заявка", "заявки", "заявок"]))}
                            {c.total > 0 && ` · ${money(c.total)}`}
                          </span>
                        </div>
                        <div className="mt-1 h-1.5 overflow-hidden rounded-full bg-muted">
                          <div className="h-full rounded-full bg-accent" style={{ width: `${Math.max(2, (measure(c) / maxCategory) * 100)}%` }} />
                        </div>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="mt-4 text-sm text-ink-3">{t("В этом месяце заявок ещё не отправляли.")}</p>
                )}
              </section>
            </div>

            <section className="mt-4 rounded-xl border border-line bg-surface p-4 sm:p-5">
              <div className="mb-2 flex items-center justify-between gap-3">
                <h2 className="text-sm font-semibold text-ink">{t("Последние заявки")}</h2>
                <Link href={list} className="inline-flex items-center gap-1 text-sm text-accent-strong hover:underline">
                  {t("Все")} <ArrowRight className="size-3.5" aria-hidden />
                </Link>
              </div>
              <Rows rows={overview.recent} empty="Отправленных заявок пока нет." />
            </section>
          </>
        )}
      </PageBody>
    </>
  );
}
