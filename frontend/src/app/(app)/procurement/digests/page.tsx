import Link from "next/link";
import { redirect } from "next/navigation";
import { Newspaper } from "lucide-react";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { dateRu, stampRu, type DigestRow, type Page } from "@/lib/procurement";
import { apiGetOrNull } from "@/lib/server-api";
import { qs } from "@/lib/tasks";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Сводки снабжения · OneBase") };
}

/** Журнал ежедневных сводок снабжения — директору и руководителю снабжения (доступ решает API). */
export default async function DigestsPage({ searchParams }: { searchParams: Promise<{ page?: string }> }) {
  await requireRequests();
  const t = await getT();
  const { page } = await searchParams;
  const data = await apiGetOrNull<Page<DigestRow>>(`/api/procurement/digests${qs({ page })}`, "/procurement/digests");
  if (!data) redirect("/procurement");
  const pages = Math.max(1, Math.ceil(data.total / data.pageSize));
  return (
    <>
      <PageHeader
        title={t("Ежедневные сводки")}
        subtitle={t("Каждое утро — что ждёт решения, просрочки, поступления, претензии и остатки; сводки хранятся здесь")}
        back="/procurement"
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Отделы"), href: "/departments" }, { label: t("Снабжение"), href: "/procurement" }, { label: t("Сводки") }]}
      />
      <PageBody>
        <div className="mx-auto max-w-4xl">
          {data.items.length === 0 ? (
            <p className="rounded-xl border border-dashed border-line bg-surface px-4 py-10 text-center text-sm text-ink-3">
              {t("Сводок ещё нет — первая сформируется утром после 09:00.")}
            </p>
          ) : (
            <ul className="divide-y divide-line overflow-hidden rounded-xl border border-line bg-surface">
              {data.items.map((d) => (
                <li key={d.id}>
                  <Link href={`/procurement/digests/${d.id}`} className="flex items-start gap-3 px-4 py-3 hover:bg-muted/50">
                    <Newspaper className="mt-0.5 size-4 shrink-0 text-ink-3" aria-hidden />
                    <span className="min-w-0 flex-1">
                      <span className="block text-sm font-semibold text-ink">{t("Сводка за {date}", { date: dateRu(d.day) })}</span>
                      <span className="block text-xs text-ink-3">{t(d.summary)}</span>
                    </span>
                    <span className="shrink-0 text-xs tabular-nums text-ink-3">{stampRu(d.createdAt)}</span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
          {pages > 1 && (
            <div className="mt-3 flex items-center justify-end gap-3 text-sm">
              {data.page > 1 && (
                <Link href={`/procurement/digests?page=${data.page - 1}`} className="text-accent-strong hover:underline">
                  {t("Назад")}
                </Link>
              )}
              <span className="text-ink-2">{t("{page} из {pages}", { page: data.page, pages })}</span>
              {data.page < pages && (
                <Link href={`/procurement/digests?page=${data.page + 1}`} className="text-accent-strong hover:underline">
                  {t("Дальше")}
                </Link>
              )}
            </div>
          )}
        </div>
      </PageBody>
    </>
  );
}
