"use client";

import Link from "next/link";
import { useState } from "react";
import { Bot, FlaskConical, ScrollText, Users } from "lucide-react";
import { TelegramAgentForm } from "@/components/telegram/TelegramAgentForm";
import { TelegramAgentTest } from "@/components/telegram/TelegramAgentTest";
import { TelegramJournal } from "@/components/telegram/TelegramJournal";
import { TelegramUsers } from "@/components/telegram/TelegramUsers";
import { useT } from "@/i18n/client";
import type { TelegramAccount } from "@/lib/telegram";

type Tab = "agent" | "users" | "test" | "journal";

const tabs: { key: Tab; label: string; icon: typeof Bot }[] = [
  { key: "agent", label: "Настройки агента", icon: Bot },
  { key: "users", label: "Кому отвечает", icon: Users },
  { key: "test", label: "Проверка", icon: FlaskConical },
  { key: "journal", label: "Журнал ответов", icon: ScrollText },
];

/** Агент продаж номера Telegram: настройки, кому и с какими правами отвечает, проверка, журнал — те же компоненты, что в «Настройки → Telegram». */
export function SalesAgentPanel({ initial }: { initial: TelegramAccount }) {
  const t = useT();
  const [account, setAccount] = useState(initial);
  const [tab, setTab] = useState<Tab>("agent");
  return (
    <div className="space-y-4">
      <p className="max-w-3xl text-sm text-ink-2">
        {t("Подключение номера, чаты, рассылки и автоотчёты — в")}{" "}
        <Link className="text-accent-strong hover:underline" href="/settings/telegram">
          {t("«Настройки → Telegram»")}
        </Link>
        .
      </p>
      <nav className="flex gap-1 overflow-x-auto border-b border-line">
        {tabs.map((x) => (
          <button
            key={x.key}
            onClick={() => setTab(x.key)}
            className={`flex shrink-0 items-center gap-1.5 border-b-2 px-3 py-2 text-sm ${tab === x.key ? "border-accent text-ink" : "border-transparent text-ink-3 hover:text-ink"}`}
          >
            <x.icon className="size-4" />
            {t(x.label)}
          </button>
        ))}
      </nav>
      {tab === "agent" && <TelegramAgentForm key={account.id} account={account} onSaved={(enabled) => setAccount({ ...account, agentEnabled: enabled })} />}
      {tab === "users" && <TelegramUsers key={account.id} account={account} onCount={(n) => n !== account.permissions && setAccount({ ...account, permissions: n })} />}
      {tab === "test" && <TelegramAgentTest key={account.id} account={account} />}
      {tab === "journal" && <TelegramJournal key={account.id} account={account} />}
    </div>
  );
}
