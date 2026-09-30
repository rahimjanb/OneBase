import Link from "next/link";
import { ArrowRight } from "lucide-react";
import { AiSettingsNav } from "@/components/ai/AiSettingsNav";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { aiCrumbs, type AiAgentListItem } from "@/lib/ai";
import { apiGet } from "@/lib/server-api";

export const metadata = { title: "AI-сотрудники · Настройки · OneBase" };

export default async function AiAgentsSettingsPage() {
  const agents = await apiGet<AiAgentListItem[]>("/api/ai/agents/settings", "/settings/ai/agents");
  return (
    <>
      <PageHeader
        title="AI"
        subtitle="Главный консультант и AI-сотрудники отделов"
        back="/settings"
        breadcrumbs={[...aiCrumbs, { label: "AI", href: "/settings/ai" }, { label: "Агенты" }]}
      />
      <PageBody>
        <AiSettingsNav />
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {agents.map((a) => (
            <Link
              key={a.code}
              href={`/settings/ai/agents/${a.code}`}
              className={`group flex flex-col rounded-xl border bg-surface p-5 transition-colors hover:border-accent/40 ${a.isConsultant ? "border-accent/30" : "border-line"}`}
            >
              <div className="flex items-start justify-between gap-3">
                <div>
                  <div className="font-semibold text-ink">{a.name}</div>
                  {a.role && <div className="text-sm text-ink-3">{a.role}</div>}
                </div>
                <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${a.enabled ? "bg-ok-soft text-ok" : "bg-muted text-ink-2"}`}>
                  {a.enabled ? "включён" : "выключен"}
                </span>
              </div>
              {a.description && <p className="mt-3 text-sm text-ink-2">{a.description}</p>}
              <div className="mt-auto flex flex-wrap items-center gap-x-3 gap-y-1 pt-4 text-xs text-ink-3">
                <span>Модель: {a.model ? a.model.model : "основная"}</span>
                <span>Источников: {a.sources}</span>
                {!a.isConsultant && <span>Инструментов: {a.tools}</span>}
                {!a.promptIsDefault && <span className="text-accent-strong">своя инструкция</span>}
                <ArrowRight className="ml-auto size-4 text-ink-3 group-hover:text-accent-strong" />
              </div>
            </Link>
          ))}
        </div>
      </PageBody>
    </>
  );
}
