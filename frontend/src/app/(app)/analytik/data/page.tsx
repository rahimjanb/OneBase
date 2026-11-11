import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { requireAnalytik } from "@/components/analytik/guard";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import { DataMapView } from "@/components/analytik/DataMap";
import type { AnalytikStatus, CatalogGraph, CatalogTableRow } from "@/lib/analytik";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Карта данных · OneBase") };
}

/** «Аналитик → Карта данных». */
export default async function Page() {
  await requireAnalytik();
  const t = await getT();
  const [status, graph, tables] = await Promise.all([
    apiGet<AnalytikStatus>("/api/analytik/status", "/analytik/data"),
    apiGet<CatalogGraph>("/api/analytik/catalog/graph", "/analytik/data"),
    apiGet<CatalogTableRow[]>("/api/analytik/catalog", "/analytik/data"),
  ]);
  return (
    <>
      <PageHeader title={t("Карта данных")} subtitle={t("Каталог всей базы OneBase: таблицы, колонки, связи, их смысл и что из этого доступно вам")} breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Аналитик"), href: "/analytik" }, { label: t("Карта данных") }]} />
      <PageBody>
        <DataMapView status={status} graph={graph} tables={tables} />
      </PageBody>
    </>
  );
}
