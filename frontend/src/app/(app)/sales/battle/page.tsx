import { Alert } from "@/components/sales/bits";
import { BattleView } from "@/components/sales/battle";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { getT } from "@/i18n/server";
import { apiGetOrNull } from "@/lib/server-api";
import type { BattleOverview } from "@/lib/sales/battle";
import type { SalesSearchParams } from "@/lib/sales/query";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Поле боя · Продажи") };
}

/**
 * «Продажи → Поле боя»: «Цели на маршрут» v2 (ТЗ v2 от 24.11.2026) — цели Могул и Twizos на маршруте ТП, два баланса кешбека, команды и регионы,
 * квоты стоек, карточки как в Telegram. Охват — как во всех «Продажах» (ТП — только он, СВР — свои регионы, РМ — свои направления); считает сервер.
 */
export default async function BattlePage({ searchParams }: { searchParams: Promise<SalesSearchParams & { date?: string }> }) {
  const t = await getT();
  const sp = await searchParams;
  const date = typeof sp.date === "string" && /^\d{4}-\d{2}-\d{2}$/.test(sp.date) ? `?date=${sp.date}` : "";
  const data = await apiGetOrNull<BattleOverview>(`/api/sales/battle${date}`, `/sales/battle${date}`);

  return (
    <SalesFrame title={t("Поле боя")} subtitle={t("Цели на маршрут: Могул спящим и Twizos по сегментам, два кешбека, стойки и карточки ТП, СВР и РМ")} sp={sp}>
      {data ? (
        <BattleView overview={data} />
      ) : (
        <Alert tone="info" className="mt-2">
          {t("Акции с целями на маршрут (v2) нет: включите раздел «Цели на маршрут v2» во вкладке «Акции» автоотчётов Telegram.")}
        </Alert>
      )}
    </SalesFrame>
  );
}
