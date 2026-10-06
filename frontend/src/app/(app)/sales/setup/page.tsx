import { SalesFrame } from "@/components/sales/SalesFrame";
import { SetupClient } from "@/components/sales/SetupClient";
import type { SalesSearchParams } from "@/lib/sales/query";

export const metadata = { title: "Настройки продаж · OneBase" };

export default async function SalesSetupPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const sp = await searchParams;
  return (
    <SalesFrame
      title="Настройки продаж"
      subtitle="Синхронизация, оргструктура (РМ, каналы, СВР и дилеры регионов), справочник ТП и цели"
      back="/settings"
      sp={sp}
    >
      <SetupClient />
    </SalesFrame>
  );
}
