import { TelegramReports } from "@/components/telegram/TelegramReports";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { ReportOverview } from "@/lib/telegramReports";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Автоматические отчёты · Telegram · OneBase") };
}

export default async function TelegramReportsPage() {
  const overview = await apiGet<ReportOverview>("/api/settings/telegram/reports", "/settings/telegram/reports");
  const t = await getT();
  return (
    <>
      <PageHeader
        title={t("Автоматические отчёты")}
        subtitle={t("План на день, план / факт и итоги дня картинками 16:9 в чаты и группы Telegram — по регионам и супервайзерам")}
        back="/settings/telegram"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Настройки"), href: "/settings" },
          { label: "Telegram", href: "/settings/telegram" },
          { label: t("Автоматические отчёты") },
        ]}
      />
      <PageBody>
        <TelegramReports initial={overview} />
      </PageBody>
    </>
  );
}
