import { Alert } from "@/components/sales/bits";
import { DmpReport } from "@/components/sales/dmp";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { getT } from "@/i18n/server";
import { apiGetOrNull } from "@/lib/server-api";
import type { SalesSearchParams } from "@/lib/sales/query";
import type { DmpView } from "@/lib/sales/dmp";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("ДМП · Продажи") };
}

/** «ДМП»: стеллажи в магазинах на дату данных — приход на склады дилеров, установки и как магазины заказывают после; всё считает сервер. */
export default async function DmpPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const t = await getT();
  const sp = await searchParams;
  const data = await apiGetOrNull<DmpView>("/api/sales/dmp", "/sales/dmp");

  return (
    <SalesFrame title={t("ДМП")} subtitle={t("Стеллажи в магазинах: сколько получили дилеры, сколько поставили и как магазины заказывают после")} sp={sp}>
      {data ? (
        <DmpReport data={data} />
      ) : (
        <Alert tone="info" className="mt-2">
          {t("Стеллажей в Linko нет: товаров типа «Оборудование» не найдено. Раздел заполнится, когда стеллаж заведут товаром в Linko.")}
        </Alert>
      )}
    </SalesFrame>
  );
}
