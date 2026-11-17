import { notFound } from "next/navigation";
import { VerifixTabs } from "@/components/hr/HrBits";
import { VerifixSettings } from "@/components/hr/VerifixSettings";
import { LinkoSettings } from "@/components/integrations/LinkoSettings";
import { OneCSettings } from "@/components/integrations/OneCSettings";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { VerifixDetails } from "@/lib/hr";
import type { OneCStatus } from "@/lib/finance";
import type { LinkoDetails } from "@/lib/integrations";
import { apiGet } from "@/lib/server-api";

/** Интеграции: Linko — для продаж, Verifix — для HR (подключение и синхронизация; журнал — вкладкой рядом), 1С — для финансов. */
export default async function IntegrationPage({ params }: { params: Promise<{ dept: string; code: string }> }) {
  const { dept, code } = await params;
  if (code === "onec") return <OneCPage dept={dept} />;
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

/** «Финансы → 1С:Бухгалтерия»: состояние службы интеграции, синхронизация, историческая загрузка, журнал и ошибки. */
async function OneCPage({ dept }: { dept: string }) {
  if (dept !== "finance") notFound();
  const t = await getT();
  const status = await apiGet<OneCStatus>("/api/integrations/onec", `/settings/integrations/${dept}/onec`);
  return (
    <>
      <PageHeader
        title="1С:Бухгалтерия"
        subtitle={t("Бухгалтерия для Узбекистана (VENKON GROUP): план счетов, проводки, остатки и обороты, контрагенты, документы, валюты — только чтение, 1С → OneBase.")}
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Настройки"), href: "/settings" },
          { label: t("Интеграции"), href: "/settings/integrations" },
          { label: t("Финансы"), href: "/settings/integrations/finance" },
          { label: "1С" },
        ]}
      />
      <PageBody>
        <OneCSettings initial={status} />
      </PageBody>
    </>
  );
}
