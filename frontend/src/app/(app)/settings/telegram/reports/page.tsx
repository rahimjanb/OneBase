import { redirect } from "next/navigation";
import { TelegramReports } from "@/components/telegram/TelegramReports";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { ReportOverview } from "@/lib/telegramReports";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Автоматические отчёты · Telegram · OneBase") };
}

export default async function TelegramReportsPage({ searchParams }: { searchParams: Promise<{ tab?: string | string[] }> }) {
  const { tab } = await searchParams;
  const overview = await apiGet<ReportOverview>("/api/settings/telegram/reports", "/settings/telegram/reports");
  // Отчёты строятся по данным «Продаж»: без права на них раздела нет — обратно в настройки Telegram.
  if (!overview.canUseSales) redirect("/settings/telegram");
  const t = await getT();
  return (
    <>
      <PageHeader
        title={t("Автоматические отчёты")}
        subtitle={t("Отчёты по расписанию в Telegram для РМ, СВ и ТП по оргструктуре «Продаж»: семь — картинками (седьмой — карточки «Поля боя»), восьмой — еженедельный PDF документом")}
        back="/settings/telegram"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Настройки"), href: "/settings" },
          { label: "Telegram", href: "/settings/telegram" },
          { label: t("Автоматические отчёты") },
        ]}
      />
      <PageBody>
        <TelegramReports initial={overview} initialTab={typeof tab === "string" ? tab : undefined} />
      </PageBody>
    </>
  );
}
