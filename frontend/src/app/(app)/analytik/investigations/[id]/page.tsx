import { notFound } from "next/navigation";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { requireAnalytik } from "@/components/analytik/guard";
import { InvestigationView } from "@/components/analytik/intelligence/InvestigationView";
import { getT } from "@/i18n/server";
import type { InvestigationDetail } from "@/lib/intelligence";
import { apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Исследование · OneBase") };
}

/** Исследование: план волнами, промежуточные результаты шагов, гипотезы, противоречия, проверки, трасса и метрики. Видит автор и управляющий. */
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  await requireAnalytik();
  const t = await getT();
  const { id } = await params;
  const data = await apiGetOrNull<InvestigationDetail>(`/api/analytik/investigations/${encodeURIComponent(id)}`, "/analytik/investigations");
  if (!data) notFound();
  return (
    <>
      <PageHeader
        title={t("Исследование")}
        subtitle={data.row.question}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Аналитик"), href: "/analytik" }, { label: t("Исследования"), href: "/analytik/investigations" }, { label: t("Исследование") }]}
      />
      <PageBody>
        <InvestigationView data={data} />
      </PageBody>
    </>
  );
}
