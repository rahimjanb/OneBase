import { Consultant } from "@/components/ai/Consultant";
import type { AiStatus, ConversationSummary } from "@/lib/consultant";
import { apiGet } from "@/lib/server-api";

export const metadata = { title: "Консультант · OneBase" };

type Me = { permissions: string[] };

export default async function ConsultantPage() {
  const path = "/consultant";
  const [status, conversations, me] = await Promise.all([
    apiGet<AiStatus>("/api/ai/status", path),
    apiGet<ConversationSummary[]>("/api/ai/conversations", path),
    apiGet<Me>("/api/auth/me", path),
  ]);
  return <Consultant status={status} conversations={conversations} conversation={null} canConfigure={me.permissions.includes("ai.settings.manage")} />;
}
