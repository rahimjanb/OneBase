import { Alert } from "@/components/sales/bits";
import { PromoReport } from "@/components/sales/promo";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { getT } from "@/i18n/server";
import { apiGetOrNull } from "@/lib/server-api";
import type { SalesSearchParams } from "@/lib/sales/query";
import type { PromoView } from "@/lib/sales/promo";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Акции · Продажи") };
}

/**
 * «Акции»: скидки и бонусы вторички по акциям Linko. Свой выбор месяца (year, month — месяцы года, по умолчанию последний), rm — РМ,
 * promos — отмеченные акции (через запятую). Всё считает сервер.
 */
export default async function PromoPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const t = await getT();
  const sp = await searchParams;
  const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);
  const query = new URLSearchParams();
  if (one(sp.year) && one(sp.month)) query.set("month", `${one(sp.year)}-${Number(one(sp.month))}`);
  if (one(sp.rm)) query.set("rm", one(sp.rm)!);
  if (one(sp.promos)) query.set("promos", one(sp.promos)!);
  const data = await apiGetOrNull<PromoView>(`/api/sales/promo${query.size ? `?${query}` : ""}`, "/sales/promo");

  return (
    <SalesFrame title={t("Акции")} subtitle={t("Скидки и бонусы вторички по акциям Linko: сколько стоили, где и кому, окупились ли")} sp={sp}>
      {data ? (
        <PromoReport data={data} />
      ) : (
        <Alert tone="info" className="mt-2">
          {t("Строк со скидками в Linko за год нет — раздел заполнится после синхронизации заказов.")}
        </Alert>
      )}
    </SalesFrame>
  );
}
