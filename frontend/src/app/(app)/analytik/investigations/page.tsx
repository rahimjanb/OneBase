import Link from "next/link";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { one, requireAnalytik, type SearchParams } from "@/components/analytik/guard";
import { InvestigationList } from "@/components/analytik/intelligence/InvestigationView";
import { getT } from "@/i18n/server";
import type { InvestigationRow } from "@/lib/intelligence";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Исследования · OneBase") };
}

/** «Аналитик → Исследования»: сложные вопросы, которые AI-аналитик исследовал по плану, — шаги, гипотезы, проверки, стоимость. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const me = await requireAnalytik();
  const t = await getT();
  const manager = !!me?.permissions.includes("analytik.manage");
  const all = manager && one(await searchParams, "all") === "1";
  const rows = await apiGet<InvestigationRow[]>(`/api/analytik/investigations?take=100${all ? "&all=true" : ""}`, "/analytik/investigations");
  return (
    <>
      <PageHeader
        title={t("Исследования")}
        subtitle={t("Сложные вопросы, которые AI-аналитик исследовал по плану: шаги, гипотезы с проверкой расчётом, противоречия, проверка выводов")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Аналитик"), href: "/analytik" }, { label: t("Исследования") }]}
      />
      <PageBody>
        {manager && (
          <div className="mb-3 flex gap-1.5 text-xs">
            <Link href="/analytik/investigations" className={`rounded-full px-3 py-1 font-medium ${all ? "bg-muted text-ink-2" : "bg-accent text-white"}`}>{t("Мои")}</Link>
            <Link href="/analytik/investigations?all=1" className={`rounded-full px-3 py-1 font-medium ${all ? "bg-accent text-white" : "bg-muted text-ink-2"}`}>{t("Все пользователи")}</Link>
          </div>
        )}
        <InvestigationList rows={rows} showAuthor={all} />
      </PageBody>
    </>
  );
}
