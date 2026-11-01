import Link from "next/link";
import { notFound } from "next/navigation";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { dateRu, stampRu, type DigestView } from "@/lib/procurement";
import { apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Сводка снабжения · OneBase") };
}

/** Сводка дня — снимок на момент формирования: разделы со ссылками; соседние дни — стрелками. */
export default async function DigestPage({ params }: { params: Promise<{ id: string }> }) {
  await requireRequests();
  const t = await getT();
  const { id } = await params;
  const d = await apiGetOrNull<DigestView>(`/api/procurement/digests/${id}`, `/procurement/digests/${id}`);
  if (!d) notFound();
  return (
    <>
      <PageHeader
        title={t("Сводка за {date}", { date: dateRu(d.day) })}
        subtitle={t("Сформирована {at} · получателей: {n}", { at: stampRu(d.createdAt), n: d.recipients })}
        back="/procurement/digests"
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Снабжение"), href: "/procurement" }, { label: t("Сводки"), href: "/procurement/digests" }, { label: dateRu(d.day) }]}
        actions={
          <span className="inline-flex gap-2">
            {d.previousId && (
              <Link href={`/procurement/digests/${d.previousId}`} className="inline-flex h-9 items-center gap-1 rounded-lg border border-line bg-surface px-3 text-sm hover:bg-muted">
                <ChevronLeft className="size-4" aria-hidden /> {t("Раньше")}
              </Link>
            )}
            {d.nextId && (
              <Link href={`/procurement/digests/${d.nextId}`} className="inline-flex h-9 items-center gap-1 rounded-lg border border-line bg-surface px-3 text-sm hover:bg-muted">
                {t("Позже")} <ChevronRight className="size-4" aria-hidden />
              </Link>
            )}
          </span>
        }
      />
      <PageBody>
        <div className="mx-auto max-w-4xl space-y-3">
          <p className="rounded-xl bg-muted px-4 py-3 text-sm text-ink-2">{t(d.summary)}</p>
          {d.sections.map((s) => (
            <section key={s.title} className="rounded-2xl border border-line bg-surface p-4">
              <h2 className="flex items-baseline justify-between gap-3 text-base font-semibold text-ink">
                {t(s.title)} <span className="text-sm font-normal tabular-nums text-ink-3">{s.count}</span>
              </h2>
              {s.items.length === 0 ? (
                <p className="mt-1 text-sm text-ink-3">{t("Нет.")}</p>
              ) : (
                <ul className="mt-2 space-y-1.5 text-sm">
                  {s.items.map((i, n) => (
                    <li key={n}>{i.link ? <Link href={i.link} className="text-ink hover:text-accent-strong">{t(i.text)}</Link> : <span className="text-ink">{t(i.text)}</span>}</li>
                  ))}
                  {s.count > s.items.length && <li className="text-xs text-ink-3">{t("и ещё {n}", { n: s.count - s.items.length })}</li>}
                </ul>
              )}
            </section>
          ))}
        </div>
      </PageBody>
    </>
  );
}
