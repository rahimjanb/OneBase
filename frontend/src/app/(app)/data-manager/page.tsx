import { DataManager } from "@/components/data-manager/DataManager";
import type { DataConversationSummary, DataStatus } from "@/lib/data-manager";
import { apiGet } from "@/lib/server-api";
import { has, type Me } from "@/lib/users";

export const metadata = { title: "AI Data Manager · OneBase" };

export default async function DataManagerPage() {
  const path = "/data-manager";
  const [status, conversations, me] = await Promise.all([
    apiGet<DataStatus>("/api/data-manager/status", path),
    apiGet<DataConversationSummary[]>("/api/data-manager/conversations", path),
    apiGet<Me>("/api/auth/me", path),
  ]);
  return (
    <DataManager
      status={status}
      conversations={conversations}
      conversation={null}
      canConfigure={has(me, "ai.settings.manage")}
      canChange={has(me, "ai.data.manage")}
    />
  );
}
