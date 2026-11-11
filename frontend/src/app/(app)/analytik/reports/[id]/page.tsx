import { notFound } from "next/navigation";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { requireAnalytik } from "@/components/analytik/guard";
import { ReportView } from "@/components/analytik/Insights";
import { getT } from "@/i18n/server";
import type { ReportDoc } from "@/lib/analytik";
import { apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Отчёт · OneBase") };
}

/** Отчёт «Аналитика»: только разделы по правам читателя (решает сервер). */
export default async function ReportPage({ params }: { params: Promise<{ id: string }> }) {
  await requireAnalytik();
  const t = await getT();
  const { id } = await params;
  const doc = await apiGetOrNull<ReportDoc>(`/api/analytik/reports/${encodeURIComponent(id)}`, `/analytik/reports/${id}`);
  if (!doc) notFound();
  return (
    <>
      <PageHeader title={doc.title} breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Аналитик"), href: "/analytik" }, { label: t("Отчёты"), href: "/analytik/reports" }, { label: doc.title }]} back="/analytik/reports" />
      <PageBody>
        <ReportView id={id} doc={doc} />
      </PageBody>
    </>
  );
}
