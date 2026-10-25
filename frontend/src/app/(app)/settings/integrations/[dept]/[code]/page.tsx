import { notFound } from "next/navigation";
import { VerifixTabs } from "@/components/hr/HrBits";
import { VerifixSettings } from "@/components/hr/VerifixSettings";
import { LinkoSettings } from "@/components/integrations/LinkoSettings";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { VerifixDetails } from "@/lib/hr";
import type { LinkoDetails } from "@/lib/integrations";
import { apiGet } from "@/lib/server-api";

/** Интеграции: Linko — для продаж, Verifix — для HR (подключение и синхронизация; журнал — вкладкой рядом). */
export default async function IntegrationPage({ params }: { params: Promise<{ dept: string; code: string }> }) {
  const { dept, code } = await params;
  if (code !== "linko" && code !== "verifix") notFound();

  const path = `/settings/integrations/${dept}/${code}`;
  const details = code === "linko" ? await apiGet<LinkoDetails>("/api/integrations/linko", path) : await apiGet<VerifixDetails>("/api/integrations/verifix", path);
  if (details.departmentCode !== dept) notFound();
  const t = await getT();

  return (
    <>
      <PageHeader
        title={details.name}
        subtitle={t(details.description)}
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Настройки"), href: "/settings" },
          { label: t("Интеграции"), href: "/settings/integrations" },
          { label: t(details.departmentName), href: `/settings/integrations/${dept}` },
          { label: details.name },
        ]}
      />
      <PageBody>
        {code === "linko" ? (
          <LinkoSettings initial={details as LinkoDetails} />
        ) : (
          <>
            <VerifixTabs />
            <VerifixSettings initial={details as VerifixDetails} />
          </>
        )}
      </PageBody>
    </>
  );
}
