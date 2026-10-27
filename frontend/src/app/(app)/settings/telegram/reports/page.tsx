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
        subtitle={t("Семь отчётов по расписанию картинками в Telegram — для РМ, СВ и ТП, по оргструктуре «Продаж»; седьмой — карточки «Поля боя» для ТП и СВ")}
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
