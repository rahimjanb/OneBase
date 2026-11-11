import { notFound } from "next/navigation";
import { Consultant } from "@/components/ai/Consultant";
import { requireAnalytik } from "@/components/analytik/guard";
import { getT } from "@/i18n/server";
import { analytikChatMode } from "@/lib/analytik";
import type { AiStatus, ConversationSummary, ConversationView } from "@/lib/consultant";
import { apiGet, apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("AI-аналитик · OneBase") };
}

/**
 * AI-аналитик: тот же экран чата, что у консультанта, но своя команда (Chief Analyst и аналитики), свои чаты и инструменты OneBase Intelligence.
 */
export default async function AnalytikChatPage({ params }: { params: Promise<{ id: string }> }) {
  const me = await requireAnalytik();
  const { id } = await params;
  const [status, conversations, conversation] = await Promise.all([
    apiGet<AiStatus>("/api/analytik/chat/status", "/analytik/chat"),
    apiGet<ConversationSummary[]>("/api/analytik/conversations", "/analytik/chat"),
    apiGetOrNull<ConversationView>(`/api/analytik/conversations/${encodeURIComponent(id)}`, "/analytik/chat"),
  ]);
  if (!conversation) notFound();
  const permissions = me?.permissions ?? [];
  return (
    <Consultant
      key={conversation.id}
      status={status}
      conversations={conversations}
      conversation={conversation}
      canConfigure={permissions.includes("ai.settings.manage")}
      permissions={permissions}
      mode={analytikChatMode(permissions)}
    />
  );
}
