import { Suspense } from "react";
import { SalesNav } from "@/components/sales/SalesNav";
import { apiGet } from "@/lib/server-api";
import type { SalesMonth, SyncStatus } from "@/lib/sales/types";

/**
 * Общая часть раздела «Продажи». Layout не перерисовывается при переходах между страницами раздела,
 * поэтому панель остаётся на месте, а статус и список месяцев не запрашиваются на каждый клик.
 */
export default async function SalesLayout({ children }: { children: React.ReactNode }) {
  const [status, months] = await Promise.all([
    apiGet<SyncStatus>("/api/sales/status", "/sales"),
    apiGet<SalesMonth[]>("/api/sales/months", "/sales"),
  ]);

  return (
    <>
      <div className="border-b border-line bg-surface">
        <div className="mx-auto max-w-[1800px] px-4 py-3 sm:px-6">
          <Suspense fallback={<div className="h-9" />}>
            <SalesNav months={months} status={status} />
          </Suspense>
        </div>
      </div>
      {children}
    </>
  );
}
