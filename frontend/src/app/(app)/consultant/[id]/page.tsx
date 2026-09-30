import { notFound } from "next/navigation";
import { Consultant } from "@/components/ai/Consultant";
import type { AiStatus, ConversationSummary, ConversationView } from "@/lib/consultant";
import { apiGet, apiGetOrNull } from "@/lib/server-api";

export const metadata = { title: "Консультант · OneBase" };

type Me = { permissions: string[] };

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
  return <Consultant key={conversation.id} status={status} conversations={conversations} conversation={conversation} canConfigure={me.permissions.includes("ai.settings.manage")} />;
}
