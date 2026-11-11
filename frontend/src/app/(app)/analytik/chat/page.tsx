import { Consultant } from "@/components/ai/Consultant";
import { requireAnalytik } from "@/components/analytik/guard";
import { getT } from "@/i18n/server";
import { analytikChatMode } from "@/lib/analytik";
import type { AiStatus, ConversationSummary, ConversationView } from "@/lib/consultant";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("AI-аналитик · OneBase") };
}

/**
 * AI-аналитик: тот же экран чата, что у консультанта, но своя команда (Chief Analyst и аналитики), свои чаты и инструменты OneBase Intelligence.
 */
export default async function AnalytikChatPage({ searchParams }: { searchParams: Promise<{ q?: string; send?: string }> }) {
  const me = await requireAnalytik();
  const { q, send } = await searchParams;
  const [status, conversations] = await Promise.all([
    apiGet<AiStatus>("/api/analytik/chat/status", "/analytik/chat"),
    apiGet<ConversationSummary[]>("/api/analytik/conversations", "/analytik/chat"),
  ]);
  
  const permissions = me?.permissions ?? [];
  return (
    <Consultant
      status={status}
      conversations={conversations}
      conversation={null}
      canConfigure={permissions.includes("ai.settings.manage")}
      initialQuestion={q?.slice(0, 4000)}
      autoSend={send === "1"}
      permissions={permissions}
      mode={analytikChatMode(permissions)}
    />
  );
}
