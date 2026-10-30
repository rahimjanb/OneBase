import { notFound } from "next/navigation";
import { Consultant } from "@/components/ai/Consultant";
import { getT } from "@/i18n/server";
import type { AiStatus, ConversationSummary, ConversationView } from "@/lib/consultant";
import { apiGet, apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Консультант · OneBase") };
}

type Me = { permissions: string[]; hrAccess?: boolean };

/** Права для экрана: руководителю отдела HR его подразделений открыт без права «HR» (как в меню). */
const screenPermissions = (me: Me) => (me.hrAccess ? [...me.permissions, "hr.department"] : me.permissions);

export default async function ConversationPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const path = `/consultant/${id}`;
  const [status, conversations, conversation, me] = await Promise.all([
    apiGet<AiStatus>("/api/ai/status", path),
    apiGet<ConversationSummary[]>("/api/ai/conversations", path),
    apiGetOrNull<ConversationView>(`/api/ai/conversations/${encodeURIComponent(id)}`, path),
    apiGet<Me>("/api/auth/me", path),
  ]);
  if (!conversation) notFound();
  return (
    <Consultant
      key={conversation.id}
      status={status}
      conversations={conversations}
      conversation={conversation}
      canConfigure={me.permissions.includes("ai.settings.manage")}
      permissions={screenPermissions(me)}
    />
  );
}
