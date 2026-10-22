"use client";

import { useState } from "react";
import { Bot, FlaskConical, KeyRound, Loader2, MessageSquare, Plus, ScrollText, Smartphone, Users } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { Note, Section } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import { accountStatus, telegramApi, type TelegramAccount, type TelegramOverview } from "@/lib/telegram";
import { TelegramAccountPanel } from "./TelegramAccountPanel";
import { TelegramAgentForm } from "./TelegramAgentForm";
import { TelegramAgentTest } from "./TelegramAgentTest";
import { TelegramChats } from "./TelegramChats";
import { TelegramJournal } from "./TelegramJournal";
import { TelegramUsers } from "./TelegramUsers";

type Tab = "connection" | "agent" | "users" | "chats" | "test" | "journal";

const tabs: { key: Tab; label: string; icon: typeof Bot }[] = [
  { key: "connection", label: "Подключение", icon: Smartphone },
  { key: "agent", label: "AI-агент", icon: Bot },
  { key: "users", label: "Пользователи", icon: Users },
  { key: "chats", label: "Чаты", icon: MessageSquare },
  { key: "test", label: "Проверка", icon: FlaskConical },
  { key: "journal", label: "Журнал", icon: ScrollText },
];

/** «Настройки → Telegram»: ключи приложения, аккаунты и для выбранного аккаунта — подключение, агент, пользователи, чаты, проверка агента, журнал. */
export function TelegramSettings({ initial }: { initial: TelegramOverview }) {
  const t = useT();
  const [data, setData] = useState(initial);
  const [selected, setSelected] = useState<string | null>(initial.accounts[0]?.id ?? null);
  const [tab, setTab] = useState<Tab>("connection");
  const [creating, setCreating] = useState(false);
  const [name, setName] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const account = data.accounts.find((a) => a.id === selected) ?? null;

  const replace = (a: TelegramAccount) => setData((d) => ({ ...d, accounts: d.accounts.map((x) => (x.id === a.id ? a : x)) }));

  // Счётчики во вкладках: обновляем, только если число изменилось (иначе лишняя перерисовка всей страницы).
  const setCount = (field: "permissions" | "chats", n: number) =>
    setData((d) => ({
      ...d,
      accounts: d.accounts.map((x) => (x.id === selected && x[field] !== n ? { ...x, [field]: n } : x)),
    }));

  const create = async () => {
    setBusy(true);
    setError(null);
    try {
      const a = await telegramApi.createAccount(name);
      setData((d) => ({ ...d, accounts: [...d.accounts, a] }));
      setSelected(a.id);
      setTab("connection");
      setCreating(false);
      setName("");
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const removed = async () => {
    const overview = await telegramApi.overview();
    setData(overview);
    setSelected(overview.accounts[0]?.id ?? null);
  };

  return (
    <>
      <Credentials data={data} onSaved={setData} />

      <Section
        title={t("Аккаунты Telegram")}
        hint={`${data.accounts.length}`}
        actions={
          <button className={primary} onClick={() => setCreating(true)} disabled={busy || !data.credentials.configured}>
            <Plus className="size-4" />
            {t("Добавить аккаунт")}
          </button>
        }
      >
        {!data.credentials.configured && (
          <p className="mb-3 rounded-lg bg-warn-soft px-3 py-2 text-sm text-warn">{t("Сначала укажите api_id и api_hash приложения Telegram — выше.")}</p>
        )}
        {creating && (
          <div className="mb-4 flex flex-wrap items-end gap-2 rounded-lg border border-accent/30 bg-accent-soft/30 p-4">
            <label className="block min-w-[260px] flex-1">
              <span className="mb-1.5 block text-sm font-medium text-ink">{t("Название")}</span>
              <input className={field} value={name} onChange={(e) => setName(e.target.value)} maxLength={100} placeholder={t("Например, «Отдел продаж»")} autoFocus />
            </label>
            <button className={primary} onClick={create} disabled={busy || !name.trim()}>
              {busy && <Loader2 className="size-4 animate-spin" />}
              {t("Создать")}
            </button>
            <button className={button} onClick={() => setCreating(false)} disabled={busy}>
              {t("Отмена")}
            </button>
            {error && <p className="w-full rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
          </div>
        )}

        {data.accounts.length === 0 ? (
          <p className="text-sm text-ink-3">{t("Аккаунтов пока нет.")}</p>
        ) : (
          <div className="flex flex-wrap gap-2">
            {data.accounts.map((a) => (
              <button
                key={a.id}
                onClick={() => setSelected(a.id)}
                className={`flex min-w-[220px] items-center gap-3 rounded-lg border px-3 py-2 text-left text-sm ${
                  a.id === selected ? "border-accent bg-accent-soft/40" : "border-line bg-surface hover:bg-muted"
                }`}
              >
                <span className="min-w-0 flex-1">
                  <span className="block truncate font-medium text-ink">{a.name}</span>
                  <span className="block truncate text-xs text-ink-3">
                    {a.username ? `@${a.username}` : (a.displayName ?? a.phoneHint ?? t("не подключён"))}
                    {a.agentEnabled && t(" · агент включён")}
                  </span>
                </span>
                <span className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${accountStatus[a.status].className}`}>{t(accountStatus[a.status].label)}</span>
              </button>
            ))}
          </div>
        )}
      </Section>

      {account && (
        <section className="mt-6 rounded-xl border border-line bg-surface">
          <div className="flex flex-wrap gap-1 border-b border-line px-3 pt-3">
            {tabs.map((item) => (
              <button
                key={item.key}
                onClick={() => setTab(item.key)}
                className={`-mb-px flex items-center gap-2 rounded-t-lg border-b-2 px-3 py-2 text-sm font-medium ${
                  tab === item.key ? "border-accent text-ink" : "border-transparent text-ink-3 hover:text-ink"
                }`}
              >
                <item.icon className="size-4" />
                {t(item.label)}
                {item.key === "users" && <span className="text-xs text-ink-3">{account.permissions}</span>}
                {item.key === "chats" && <span className="text-xs text-ink-3">{account.chats}</span>}
              </button>
            ))}
          </div>
          <div className="p-4 sm:p-5">
            {tab === "connection" && <TelegramAccountPanel key={account.id} account={account} onChange={replace} onRemoved={removed} />}
            {tab === "agent" && <TelegramAgentForm key={account.id} account={account} onSaved={(enabled) => replace({ ...account, agentEnabled: enabled })} />}
            {tab === "users" && <TelegramUsers key={account.id} account={account} onCount={(n) => setCount("permissions", n)} />}
            {tab === "chats" && <TelegramChats key={account.id} account={account} onCount={(n) => setCount("chats", n)} />}
            {tab === "test" && <TelegramAgentTest key={account.id} account={account} />}
          </div>
        </section>
      )}
    </>
  );
}

/** api_id и api_hash приложения (my.telegram.org → API development tools). api_hash в ответах не возвращается — только последние символы. */
function Credentials({ data, onSaved }: { data: TelegramOverview; onSaved: (o: TelegramOverview) => void }) {
  const t = useT();
  const c = data.credentials;
  const [editing, setEditing] = useState(!c.configured);
  const [apiId, setApiId] = useState(c.apiId?.toString() ?? "");
  const [apiHash, setApiHash] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const save = async () => {
    setBusy(true);
    setError(null);
    try {
      onSaved(await telegramApi.saveCredentials(Number(apiId), apiHash.trim() || null));
      setApiHash("");
      setEditing(false);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Section
      title={t("Приложение Telegram")}
      hint={c.configured ? `api_id ${c.apiId} · api_hash …${c.apiHashHint}` : t("не настроено")}
      actions={
        !editing && (
          <button className={button} onClick={() => setEditing(true)}>
            <KeyRound className="size-4" />
            {t("Изменить")}
          </button>
        )
      }
    >
      {editing ? (
        <div className="grid gap-4 sm:grid-cols-[200px_1fr_auto] sm:items-end">
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">api_id</span>
            <input className={field} value={apiId} onChange={(e) => setApiId(e.target.value.replace(/\D/g, ""))} inputMode="numeric" autoComplete="off" />
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">api_hash</span>
            <input
              className={field}
              value={apiHash}
              onChange={(e) => setApiHash(e.target.value)}
              placeholder={c.configured ? t("Пусто — оставить прежний") : t("32 символа")}
              autoComplete="off"
              spellCheck={false}
              type="password"
            />
          </label>
          <div className="flex gap-2">
            <button className={primary} onClick={save} disabled={busy || !apiId || (!c.configured && !apiHash.trim())}>
              {busy && <Loader2 className="size-4 animate-spin" />}
              {t("Сохранить")}
            </button>
            {c.configured && (
              <button className={button} onClick={() => setEditing(false)} disabled={busy}>
                {t("Отмена")}
              </button>
            )}
          </div>
          {error && <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad sm:col-span-3">{error}</p>}
        </div>
      ) : (
        <p className="text-sm text-ink-2">{t("Ключи заданы. api_hash хранится зашифрованным и в браузер не передаётся.")}</p>
      )}
      <Note>
        {t(
          "api_id и api_hash выдаёт Telegram на my.telegram.org → «API development tools». Они нужны, чтобы OneBase работал от имени пользовательского аккаунта (не бота): видел его чаты, отвечал и делал рассылки. Хранятся только на сервере, зашифрованными.",
        )}
      </Note>
    </Section>
  );
}
