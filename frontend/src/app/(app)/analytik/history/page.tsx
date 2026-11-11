import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { requireAnalytik, one, type SearchParams } from "@/components/analytik/guard";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import { HistoryView } from "@/components/analytik/DataMap";
import type { QueryLogRow } from "@/lib/analytik";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("История аналитики · OneBase") };
}

/** «Аналитик → История». */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const me = await requireAnalytik();
  const t = await getT();
  const sp = await searchParams;
  const all = one(sp, "all") === "1";
  const manager = !!me?.permissions.includes("analytik.manage");
  const [queries, chats] = await Promise.all([
    apiGet<QueryLogRow[]>(`/api/analytik/queries?take=100${all && manager ? "&all=true" : ""}`, "/analytik/history"),
    apiGet<{ id: string; title: string; lastMessageAt: string }[]>("/api/analytik/conversations", "/analytik/history"),
  ]);
  return (
    <>
      <PageHeader title={t("История")} subtitle={t("Ваши исследования и журнал запросов к базе: откуда взята каждая цифра")} breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Аналитик"), href: "/analytik" }, { label: t("История") }]} />
      <PageBody>
        <HistoryView queries={queries} chats={chats} manager={manager} all={all && manager} />
      </PageBody>
    </>
  );
}
