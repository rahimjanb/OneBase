import { SalesFrame } from "@/components/sales/SalesFrame";
import { SetupClient } from "@/components/sales/SetupClient";
import { getT } from "@/i18n/server";
import type { SalesSearchParams } from "@/lib/sales/query";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Настройки продаж · OneBase") };
}

export default async function SalesSetupPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const t = await getT();
  const sp = await searchParams;
  return (
    <SalesFrame
      title={t("Настройки продаж")}
      subtitle={t("Синхронизация, оргструктура (РМ, каналы, СВР и дилеры регионов), справочник ТП и цели")}
      back="/settings"
      sp={sp}
    >
      <SetupClient />
    </SalesFrame>
  );
}
