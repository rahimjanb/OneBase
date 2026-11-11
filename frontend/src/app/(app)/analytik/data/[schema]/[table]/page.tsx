import { notFound } from "next/navigation";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { requireAnalytik } from "@/components/analytik/guard";
import { TableDetailView } from "@/components/analytik/DataMap";
import { getT } from "@/i18n/server";
import type { CatalogTableDetail } from "@/lib/analytik";
import { apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Карта данных · OneBase") };
}

/** Таблица в «Карте данных»: смысл, колонки, связи, пример данных (по правам), описание для аналитика. */
export default async function TablePage({ params }: { params: Promise<{ schema: string; table: string }> }) {
  const me = await requireAnalytik();
  const t = await getT();
  const { schema, table } = await params;
  const detail = await apiGetOrNull<CatalogTableDetail>(`/api/analytik/catalog/table/${encodeURIComponent(schema)}/${encodeURIComponent(table)}`, "/analytik/data");
  if (!detail) notFound();
  return (
    <>
      <PageHeader title={detail.table.title ?? detail.table.name} subtitle={detail.table.fullName} back="/analytik/data"
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Аналитик"), href: "/analytik" }, { label: t("Карта данных"), href: "/analytik/data" }, { label: detail.table.fullName }]} />
      <PageBody>
        <TableDetailView detail={detail} manager={!!me?.permissions.includes("analytik.manage")} />
      </PageBody>
    </>
  );
}
