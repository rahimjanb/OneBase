import { DialogsScreen } from "@/components/recruitment/dialogs/DialogsScreen";
import { loadRecruitment } from "@/components/recruitment/RecruitmentPage";
import type { DialogSummary } from "@/lib/hr-dialogs";
import { apiTry } from "@/lib/server-api";
import { getT } from "@/i18n/server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Диалоги Telegram · Подбор · OneBase") };
}

/**
 * HR → «Подбор» → «Диалоги Telegram». Список и открытый диалог (/hr/recruitment/telegram/{id}) — один экран: переход между диалогами без
 * перезагрузки. Состояние номера приходит с сервера, чтобы баннер «не подключён» не мелькал.
 */
export default async function Page({ searchParams }: { searchParams: Promise<{ filter?: string }> }) {
  const ctx = await loadRecruitment("/hr/recruitment/telegram");
  const summary = await apiTry<DialogSummary>("/api/hr/recruitment/telegram/dialogs/summary");
  // ?filter=need_hr — из напоминания «Ждут ответа HR»: сразу диалоги, где AI передал кандидата человеку.
  const { filter } = await searchParams;
  return <DialogsScreen initialSummary={summary} canManage={ctx.canManage} initialFilter={filter === "need_hr" ? "need_hr" : "all"} />;
}
