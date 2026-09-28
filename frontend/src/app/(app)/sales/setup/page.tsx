import { SalesFrame } from "@/components/sales/SalesFrame";
import { SetupClient } from "@/components/sales/SetupClient";
import type { SalesSearchParams } from "@/lib/sales/query";

export const metadata = { title: "Настройки продаж · OneBase" };

export default async function SalesSetupPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const sp = await searchParams;
  return (
    <SalesFrame
      title="Настройки продаж"
      subtitle="Оргструктура, цели и планы — то, чего нет в Linko"
      tab="setup"
      sp={sp}
      returnTo="/sales/setup"
    >
      <SetupClient />
    </SalesFrame>
  );
}
