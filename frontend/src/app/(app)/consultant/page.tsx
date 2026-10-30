import { Consultant } from "@/components/ai/Consultant";
import { getT } from "@/i18n/server";
import type { AiStatus, ConversationSummary } from "@/lib/consultant";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Консультант · OneBase") };
}

type Me = { permissions: string[]; hrAccess?: boolean };

/** Права для экрана: руководителю отдела HR его подразделений открыт без права «HR» (как в меню). */
const screenPermissions = (me: Me) => (me.hrAccess ? [...me.permissions, "hr.department"] : me.permissions);

export default async function ConsultantPage({ searchParams }: { searchParams: Promise<{ ask?: string; period?: string; dept?: string }> }) {
  const { ask, period, dept } = await searchParams;
  const path = "/consultant";
  const [status, conversations, me] = await Promise.all([
    apiGet<AiStatus>("/api/ai/status", path),
    apiGet<ConversationSummary[]>("/api/ai/conversations", path),
    apiGet<Me>("/api/auth/me", path),
  ]);
  return (
    <Consultant
      status={status}
      conversations={conversations}
      conversation={null}
      canConfigure={me.permissions.includes("ai.settings.manage")}
      initialQuestion={ask?.slice(0, 4000)}
      permissions={screenPermissions(me)}
      period={period}
      department={dept}
    />
  );
}
