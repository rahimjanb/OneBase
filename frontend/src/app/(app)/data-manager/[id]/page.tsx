import { notFound } from "next/navigation";
import { DataManager } from "@/components/data-manager/DataManager";
import type { DataConversation, DataConversationSummary, DataStatus } from "@/lib/data-manager";
import { apiGet, apiGetOrNull } from "@/lib/server-api";
import { has, type Me } from "@/lib/users";

export const metadata = { title: "AI Data Manager · OneBase" };

export default async function DataManagerConversationPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const path = `/data-manager/${id}`;
  const [status, conversations, conversation, me] = await Promise.all([
    apiGet<DataStatus>("/api/data-manager/status", path),
    apiGet<DataConversationSummary[]>("/api/data-manager/conversations", path),
    apiGetOrNull<DataConversation>(`/api/data-manager/conversations/${encodeURIComponent(id)}`, path),
    apiGet<Me>("/api/auth/me", path),
  ]);
  if (!conversation) notFound();
  return (
    <DataManager
      key={conversation.id}
      status={status}
      conversations={conversations}
      conversation={conversation}
      canConfigure={has(me, "ai.settings.manage")}
      canChange={has(me, "ai.data.manage")}
    />
  );
}
