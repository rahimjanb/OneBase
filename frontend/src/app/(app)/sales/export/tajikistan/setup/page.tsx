import { SalesFrame } from "@/components/sales/SalesFrame";
import { TajikSetup } from "@/components/sales/tajik/TajikSetup";
import { getT } from "@/i18n/server";
import { periodQuery, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import { TAJIK_BASE, type TajikSetupView } from "@/lib/sales/tajik";
import { apiGet, apiSession } from "@/lib/server-api";
import { has, type Me } from "@/lib/users";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Регионы и точки · Таджикистан · Продажи") };
}

/** «Таджикистан → Регионы и точки»: регионы со словами из названий точек, регион ТП по умолчанию, точки вручную. */
export default async function TajikSetupPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const t = await getT();
  const sp = await searchParams;
  const [data, me] = await Promise.all([apiGet<TajikSetupView>("/api/sales/tajik/setup", `${TAJIK_BASE}/setup`), apiSession<Me>("/api/auth/me")]);
  const q = periodQuery(sp);

  return (
    <SalesFrame
      title={t("Регионы и точки")}
      subtitle={t("Как точки дилера в Таджикистане относятся к регионам отчёта. Данные 1С не меняются.")}
      crumbs={[
        { label: t("Экспорт и опт"), href: withQuery("/sales/export", q) },
        { label: t("Таджикистан"), href: withQuery(TAJIK_BASE, q) },
        { label: t("Регионы и точки") },
      ]}
      back={withQuery(TAJIK_BASE, q)}
      sp={sp}
    >
      <TajikSetup initial={data} canManage={has(me, "sales.manage")} />
    </SalesFrame>
  );
}
