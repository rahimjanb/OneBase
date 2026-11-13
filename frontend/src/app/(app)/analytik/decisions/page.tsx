import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { one, requireAnalytik, type SearchParams } from "@/components/analytik/guard";
import { DecisionsView } from "@/components/analytik/intelligence/DecisionsView";
import { getT } from "@/i18n/server";
import { decodeDraft, type DecisionView } from "@/lib/intelligence";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Решения · OneBase") };
}

/**
 * «Аналитик → Решения»: память управленческих решений и проверка их результата в срок. ?draft — черновик, подготовленный AI-аналитиком
 * (сохраняет человек).
 */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireAnalytik();
  const t = await getT();
  const draft = decodeDraft(one(await searchParams, "draft"));
  const list = await apiGet<DecisionView[]>("/api/analytik/decisions", "/analytik/decisions");
  const today = new Date(Date.now() + 5 * 3600_000).toISOString().slice(0, 10);
  return (
    <>
      <PageHeader
        title={t("Решения")}
        subtitle={t("Что решили, почему и что из этого вышло: исходный показатель, ожидание, срок проверки, факт и вердикт")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Аналитик"), href: "/analytik" }, { label: t("Решения") }]}
      />
      <PageBody>
        <DecisionsView initial={list} draft={draft} today={today} />
      </PageBody>
    </>
  );
}
