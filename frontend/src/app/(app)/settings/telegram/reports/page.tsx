import { redirect } from "next/navigation";
import { TelegramReports } from "@/components/telegram/TelegramReports";
import { getT } from "@/i18n/server";
import { parseFilters } from "@/lib/telegram-recipients";
import type { ReportOverview } from "@/lib/telegramReports";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Автоматические отчёты · Telegram · OneBase") };
}

/** Автоотчёты: шапка, вкладки и фильтры «Получателей» (из адреса) — в клиентском компоненте; палитра — как в макете. */
export default async function TelegramReportsPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const params = await searchParams;
  const overview = await apiGet<ReportOverview>("/api/settings/telegram/reports", "/settings/telegram/reports");
  // Отчёты строятся по данным «Продаж»: без права на них раздела нет — обратно в настройки Telegram.
  if (!overview.canUseSales) redirect("/settings/telegram");
  const tab = typeof params.tab === "string" ? params.tab : undefined;
  return (
    <div data-palette="sales-secondary" className="min-h-full">
      <TelegramReports initial={overview} initialTab={tab} initialFilters={parseFilters(params)} />
    </div>
  );
}
