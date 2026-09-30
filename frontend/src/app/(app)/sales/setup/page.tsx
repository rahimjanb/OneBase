import { SalesFrame } from "@/components/sales/SalesFrame";
import { SetupClient } from "@/components/sales/SetupClient";
import type { SalesSearchParams } from "@/lib/sales/query";

export const metadata = { title: "Настройки продаж · OneBase" };

export default async function SalesSetupPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const sp = await searchParams;
  return (
    <SalesFrame
      title="Настройки продаж"
      subtitle="Оргструктура и цели. Планы продаж ведутся только в Linko — здесь их нет"
      back="/settings"
      sp={sp}
    >
      <SetupClient />
    </SalesFrame>
  );
}
