import { notFound } from "next/navigation";
import { LinkoSettings } from "@/components/integrations/LinkoSettings";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { LinkoDetails } from "@/lib/integrations";
import { apiGet } from "@/lib/server-api";

export default async function IntegrationPage({ params }: { params: Promise<{ dept: string; code: string }> }) {
  const { dept, code } = await params;
  // Пока есть одна интеграция — Linko для продаж.
  if (code !== "linko") notFound();

  const path = `/settings/integrations/${dept}/${code}`;
  const linko = await apiGet<LinkoDetails>("/api/integrations/linko", path);
  if (linko.departmentCode !== dept) notFound();
  const t = await getT();

  return (
    <>
      <PageHeader
        title={linko.name}
        subtitle={t(linko.description)}
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Настройки"), href: "/settings" },
          { label: t("Интеграции"), href: "/settings/integrations" },
          { label: t(linko.departmentName), href: `/settings/integrations/${dept}` },
          { label: linko.name },
        ]}
      />
      <PageBody>
        <LinkoSettings initial={linko} />
      </PageBody>
    </>
  );
}
