"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { ArrowRight, Bot, Plus, Send } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { Badge, Card, ErrorText, useAction } from "@/components/recruitment/ui";
import { useT } from "@/i18n/client";
import { agentsApi, kindLabel, type AgentDepartment, type AgentKind } from "@/lib/agents";

/** «Настройки → ИИ-агенты»: отделы и их агенты; создать агента отдела; агенты продаж — у номеров Telegram. */
export function AgentsOverview({ initial }: { initial: AgentDepartment[] }) {
  const t = useT();
  const router = useRouter();
  const [creating, setCreating] = useState<{ department: string; kind: AgentKind; name: string } | null>(null);
  const { busy, error, run } = useAction();

  if (initial.length === 0) {
    return <p className="text-sm text-ink-3">{t("Нет отделов, агентами которых вы управляете.")}</p>;
  }

  return (
    <div className="space-y-4">
      <p className="max-w-3xl text-sm text-ink-2">
        {t(
          "ИИ-агенты ведут переписку в Telegram от имени отдела: отвечают кандидатам, клиентам и поставщикам как опытный сотрудник — сразу, коротко и по делу. Все настройки агента — внутри него: как представляется и пишет, что ему можно и нельзя, рабочее время, готовые ответы, журнал ответов и проверка. Представляется именем и ролью, но на прямой вопрос «вы бот?» честно говорит, что он виртуальный помощник.",
        )}
      </p>
      <div className="grid gap-4 lg:grid-cols-2">
        {initial.map((d) => (
          <Card
            key={d.code}
            title={d.name}
            actions={
              d.kinds.length > 0 && (
                <button className={button} onClick={() => setCreating({ department: d.code, kind: d.kinds[0], name: t(kindLabel[d.kinds[0]]) })}>
                  <Plus className="size-4" /> {t("Новый агент")}
                </button>
              )
            }
          >
            {d.code !== "sales" && d.telegramAccounts === 0 && (
              <p className="mb-3 rounded-lg bg-warn-soft px-3 py-2 text-xs text-warn">
                {t("У отдела нет номера Telegram — агенту некуда писать. Подключите номер в «Настройки → Telegram» и выберите у него «Подразделения: {name}».", {
                  name: d.name,
                })}
              </p>
            )}
            <ul className="divide-y divide-line">
              {d.agents.map((a) => (
                <li key={a.id}>
                  <Link href={`/settings/agents/${a.id}`} className="group flex items-center gap-3 py-2.5">
                    <span className={`grid size-9 shrink-0 place-items-center rounded-full ${a.enabled ? "bg-accent-soft text-accent-strong" : "bg-muted text-ink-3"}`}>
                      <Bot className="size-4" />
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm font-medium text-ink">
                        {a.name}
                        {a.displayName && <span className="font-normal text-ink-3"> · {a.displayName}</span>}
                      </span>
                      <span className="mt-0.5 flex flex-wrap gap-1">
                        <Badge>{t(kindLabel[a.kind])}</Badge>
                        <Badge className={a.enabled ? "bg-ok-soft text-ok" : "bg-muted text-ink-3"}>{a.enabled ? t("включён") : t("выключен")}</Badge>
                        {a.isDefault && <Badge>{t("по умолчанию")}</Badge>}
                        {a.mode === "Approval" && <Badge className="bg-accent-soft text-accent-strong">{t("с проверкой")}</Badge>}
                        {a.drafts > 0 && <Badge className="bg-warn-soft text-warn">{t("черновиков: {n}", { n: a.drafts })}</Badge>}
                        {a.blocked > 0 && <Badge className="bg-bad-soft text-bad">{t("остановлено: {n}", { n: a.blocked })}</Badge>}
                        {a.repliesToday > 0 && <Badge>{t("ответов за сутки: {n}", { n: a.repliesToday })}</Badge>}
                      </span>
                    </span>
                    <ArrowRight className="size-4 text-ink-3 group-hover:text-accent-strong" />
                  </Link>
                </li>
              ))}
              {d.salesAgents.map((s) => (
                <li key={s.accountId}>
                  <Link href={`/settings/agents/sales/${s.accountId}`} className="group flex items-center gap-3 py-2.5">
                    <span className={`grid size-9 shrink-0 place-items-center rounded-full ${s.enabled ? "bg-accent-soft text-accent-strong" : "bg-muted text-ink-3"}`}>
                      <Send className="size-4" />
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm font-medium text-ink">
                        {t("Агент продаж")} · {s.name}
                        {s.username && <span className="font-normal text-ink-3"> · @{s.username}</span>}
                      </span>
                      <span className="mt-0.5 flex flex-wrap gap-1">
                        <Badge className={s.enabled ? "bg-ok-soft text-ok" : "bg-muted text-ink-3"}>{s.enabled ? t("включён") : t("выключен")}</Badge>
                        <Badge>{t("отвечает сотрудникам и клиентам по данным OneBase")}</Badge>
                      </span>
                    </span>
                    <ArrowRight className="size-4 text-ink-3 group-hover:text-accent-strong" />
                  </Link>
                </li>
              ))}
            </ul>
            {d.agents.length === 0 && d.salesAgents.length === 0 && (
              <p className="text-sm text-ink-3">
                {d.code === "sales"
                  ? t("Агент продаж появляется у номера Telegram: подключите номер в «Настройки → Telegram».")
                  : t("Агентов пока нет. Без агента на сообщения отвечают сотрудники отдела.")}
              </p>
            )}
            {creating?.department === d.code && (
              <div className="mt-3 flex flex-wrap items-end gap-2 rounded-lg border border-line p-3">
                {d.kinds.length > 1 && (
                  <select className={field} value={creating.kind} onChange={(e) => setCreating({ ...creating, kind: e.target.value as AgentKind })}>
                    {d.kinds.map((k) => (
                      <option key={k} value={k}>
                        {t(kindLabel[k])}
                      </option>
                    ))}
                  </select>
                )}
                <label className="min-w-48 flex-1">
                  <span className="mb-1 block text-xs font-medium text-ink-2">{t("Название")}</span>
                  <input className={field} value={creating.name} maxLength={100} onChange={(e) => setCreating({ ...creating, name: e.target.value })} autoFocus />
                </label>
                <button
                  className={primary}
                  disabled={!!busy || !creating.name.trim()}
                  onClick={() =>
                    run("create", async () => {
                      const agent = await agentsApi.create(creating.department, creating.kind, creating.name.trim());
                      router.push(`/settings/agents/${agent.id}`);
                    })
                  }
                >
                  {t("Создать")}
                </button>
                <button className={button} onClick={() => setCreating(null)}>
                  {t("Отмена")}
                </button>
                <p className="w-full text-xs text-ink-3">{t("Новый агент выключен: сначала настройте его и проверьте во вкладке «Проверка».")}</p>
              </div>
            )}
          </Card>
        ))}
      </div>
      <ErrorText error={error} />
    </div>
  );
}
