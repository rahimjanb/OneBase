"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { Loader2, Search, Trash2, UserPlus } from "lucide-react";
import { field, primary } from "@/components/ai/form";
import { Note } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import {
  modeLabel,
  permissionState,
  telegramApi,
  type OneBaseUser,
  type PermissionInput,
  type TelegramAccess,
  type TelegramAccount,
  type TelegramMode,
  type TelegramPermission,
} from "@/lib/telegram";

const inputOf = (p: TelegramPermission): PermissionInput => ({
  telegramUserId: p.telegramUserId,
  username: p.username,
  title: p.title,
  mode: p.mode,
  userId: p.userId,
  allowReports: p.allowReports,
  enabled: p.enabled,
});

/**
 * Кому отвечает агент. Сверху — общее правило («всем по отправленным данным» или только списку) и с чьими правами OneBase отвечать
 * полным доступом. Ниже — список: добавить по @username (OneBase сразу находит человека в Telegram), уровень доступа меняется прямо в строке.
 */
export function TelegramUsers({ account, onCount }: { account: TelegramAccount; onCount: (n: number) => void }) {
  const t = useT();
  const [rows, setRows] = useState<TelegramPermission[] | null>(null);
  const [access, setAccess] = useState<TelegramAccess | null>(null);
  const [search, setSearch] = useState("");
  const [username, setUsername] = useState("");
  const [mode, setMode] = useState<TelegramMode>("SentOnly");
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  // onCount — новая функция при каждой отрисовке родителя: держим её в ref, иначе load менялся бы и загрузка шла по кругу.
  const countRef = useRef(onCount);
  useEffect(() => {
    countRef.current = onCount;
  }, [onCount]);

  const load = useCallback(async () => {
    try {
      const list = await telegramApi.permissions(account.id);
      setRows(list);
      countRef.current(list.length);
    } catch (e) {
      setError((e as Error).message);
    }
  }, [account.id]);

  useEffect(() => {
    load();
    telegramApi.access(account.id).then(setAccess, (e: Error) => setError(e.message));
  }, [account.id, load]);

  const run = async (what: string, action: () => Promise<void>) => {
    setBusy(what);
    setError(null);
    setMessage(null);
    try {
      await action();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(null);
    }
  };

  const saveAccess = (replyToAll: boolean, fullAccessUserId: string | null) =>
    run("access", async () => setAccess(await telegramApi.saveAccess(account.id, replyToAll, fullAccessUserId)));

  const add = () =>
    run("add", async () => {
      const p = await telegramApi.savePermission(account.id, null, {
        telegramUserId: null,
        username,
        title: null,
        mode,
        userId: null,
        allowReports: mode === "Full",
        enabled: true,
      });
      setUsername("");
      setMessage(
        p.telegramUserId
          ? t("Добавлен {name} (@{username}) — {mode}.", { name: p.title ?? "@" + p.username, username: p.username ?? "", mode: t(modeLabel[p.mode]).toLowerCase() })
          : t("Добавлен @{username}: аккаунт не подключён — Telegram ID определится по первому сообщению.", { username: p.username ?? "" }),
      );
      await load();
    });

  const update = (p: TelegramPermission, change: Partial<PermissionInput>) =>
    run(p.id, async () => {
      const saved = await telegramApi.savePermission(account.id, p.id, { ...inputOf(p), ...change });
      setRows((r) => r?.map((x) => (x.id === saved.id ? saved : x)) ?? null);
    });

  const remove = (p: TelegramPermission) => {
    if (!confirm(t("Убрать {name} из списка?", { name: p.username ? "@" + p.username : (p.title ?? "") }))) return;
    run(p.id, async () => {
      await telegramApi.deletePermission(account.id, p.id);
      await load();
    });
  };

  const visible = (rows ?? []).filter((p) => {
    const s = search.trim().replace(/^@/, "").toLowerCase();
    return !s || p.username?.includes(s) || p.title?.toLowerCase().includes(s) || String(p.telegramUserId ?? "").includes(s);
  });
  const fullWithoutRights = access && !access.fullAccessUserId && (rows ?? []).some((p) => p.mode === "Full" && !p.userId);

  return (
    <div className="space-y-6">
      {/* Кому отвечать */}
      <div className="rounded-xl border border-line p-4">
        <h3 className="text-sm font-semibold text-ink">{t("Кому отвечает агент")}</h3>
        {access === null ? (
          <Loader2 className="mt-3 size-5 animate-spin text-ink-3" />
        ) : (
          <>
            <div className="mt-3 grid gap-2 sm:grid-cols-2">
              <Choice
                checked={access.replyToAll}
                onChange={() => saveAccess(true, access.fullAccessUserId)}
                title={t("Всем — по отправленным данным")}
                hint={t("Любому, кто напишет: только по сообщениям и отчётам, отправленным ему через OneBase. Тем, кто в списке, — с их уровнем доступа.")}
                disabled={!!busy}
              />
              <Choice
                checked={!access.replyToAll}
                onChange={() => saveAccess(false, access.fullAccessUserId)}
                title={t("Только пользователям из списка")}
                hint={t("Остальным агент не отвечает вовсе.")}
                disabled={!!busy}
              />
            </div>
            <div className="mt-4">
              <span className="mb-1.5 block text-sm font-medium text-ink">{t("Полный доступ — отвечать по данным OneBase с правами пользователя")}</span>
              <div className="max-w-md">
                <UserPicker
                  value={access.fullAccessUserId}
                  name={access.fullAccessUserName ?? ""}
                  onChange={(id) => saveAccess(access.replyToAll, id)}
                  disabled={!!busy}
                />
              </div>
              <span className="mt-1 block text-xs text-ink-3">
                {t(
                  "Тем, у кого в списке «Полный доступ», агент отвечает как AI-консультант OneBase — видит то же, что этот пользователь. Нужен пользователь с правом на AI-консультанта.",
                )}
              </span>
              {fullWithoutRights && (
                <p className="mt-2 rounded-lg bg-warn-soft px-3 py-2 text-sm text-warn">
                  {t("Пользователь не выбран — тем, у кого полный доступ, агент пока отвечает только по отправленным данным.")}
                </p>
              )}
            </div>
          </>
        )}
      </div>

      {/* Добавить по username */}
      <div>
        <h3 className="text-sm font-semibold text-ink">{t("Пользователи")}</h3>
        <form
          className="mt-3 flex flex-wrap items-end gap-2"
          onSubmit={(e) => {
            e.preventDefault();
            if (username.trim() && !busy) add();
          }}
        >
          <label className="block min-w-[220px] flex-1">
            <span className="mb-1.5 block text-xs text-ink-3">{t("Username в Telegram")}</span>
            <input
              className={field}
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              placeholder={t("@ivan_petrov или ссылка t.me/…")}
              spellCheck={false}
              autoComplete="off"
            />
          </label>
          <label className="block">
            <span className="mb-1.5 block text-xs text-ink-3">{t("Доступ")}</span>
            <select className={field.replace("w-full", "w-auto")} value={mode} onChange={(e) => setMode(e.target.value as TelegramMode)}>
              <option value="SentOnly">{t(modeLabel.SentOnly)}</option>
              <option value="Full">{t(modeLabel.Full)}</option>
            </select>
          </label>
          <button type="submit" className={primary} disabled={!!busy || !username.trim()}>
            {busy === "add" ? <Loader2 className="size-4 animate-spin" /> : <UserPlus className="size-4" />}
            {t("Добавить")}
          </button>
        </form>
        <p className="mt-1.5 text-xs text-ink-3">
          {t("OneBase сразу находит человека в Telegram: права закрепляются за его аккаунтом и не переходят к другому, если он сменит username.")}
        </p>
      </div>

      {message && <p className="rounded-lg bg-ok-soft px-3 py-2 text-sm text-ok">{message}</p>}
      {error && <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}

      {(rows?.length ?? 0) > 8 && (
        <div className="relative max-w-sm">
          <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" />
          <input className={`${field} pl-9`} value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("Найти в списке")} />
        </div>
      )}

      <div className="overflow-x-auto">
        {rows === null ? (
          <Loader2 className="size-5 animate-spin text-ink-3" />
        ) : rows.length === 0 ? (
          <p className="text-sm text-ink-3">{t("Список пуст.")}</p>
        ) : (
          <table className="w-full min-w-[760px] text-sm">
            <thead>
              <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                <th className="py-2 pr-3 font-semibold">{t("Пользователь Telegram")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Доступ")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Файлы")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Включён")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Состояние")}</th>
                <th className="py-2" />
              </tr>
            </thead>
            <tbody>
              {visible.map((p) => (
                <tr key={p.id} className={`border-b border-line last:border-b-0 ${p.enabled ? "" : "opacity-60"}`}>
                  <td className="py-2 pr-3">
                    <span className="block font-medium text-ink">{p.username ? `@${p.username}` : p.title}</span>
                    <span className="text-xs text-ink-3">
                      {p.title && p.username ? `${p.title} · ` : ""}
                      {p.telegramUserId ? `ID ${p.telegramUserId}` : t("ID — по первому сообщению")}
                      {p.oneBaseUser && ` · ${t("права {name}", { name: p.oneBaseUser })}`}
                    </span>
                  </td>
                  <td className="py-2 pr-3">
                    <select
                      className={field.replace("w-full", "w-auto").replace("h-10", "h-9")}
                      value={p.mode}
                      disabled={busy === p.id}
                      onChange={(e) => update(p, { mode: e.target.value as TelegramMode, allowReports: e.target.value === "Full" ? p.allowReports : false })}
                    >
                      <option value="Full">{t(modeLabel.Full)}</option>
                      <option value="SentOnly">{t(modeLabel.SentOnly)}</option>
                    </select>
                  </td>
                  <td className="py-2 pr-3">
                    <Switch
                      checked={p.allowReports}
                      disabled={busy === p.id || p.mode !== "Full"}
                      onChange={(v) => update(p, { allowReports: v })}
                      label={t("Отчёты и документы файлом")}
                    />
                  </td>
                  <td className="py-2 pr-3">
                    <Switch checked={p.enabled} disabled={busy === p.id} onChange={(v) => update(p, { enabled: v })} label={t("Включён")} />
                  </td>
                  <td className={`py-2 pr-3 text-xs ${permissionState[p.state]?.className ?? ""}`}>{t(permissionState[p.state]?.label ?? p.state)}</td>
                  <td className="py-2 text-right">
                    <button className="rounded-md p-1.5 text-ink-3 hover:bg-bad-soft hover:text-bad" title={t("Убрать из списка")} disabled={!!busy} onClick={() => remove(p)}>
                      {busy === p.id ? <Loader2 className="size-4 animate-spin" /> : <Trash2 className="size-4" />}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
      <Note>
        {t(
          "«Полный доступ» — ответы по данным OneBase (продажи, планы, отчёты Excel по запросу) с правами пользователя, выбранного выше. «Только отправленные данные» — ответы только по тому, что отправили этому человеку через OneBase: что в отчёте и насколько всё хорошо или плохо, когда следующий отчёт.",
        )}
      </Note>
    </div>
  );
}

function Choice({ checked, onChange, title, hint, disabled }: { checked: boolean; onChange: () => void; title: string; hint: string; disabled?: boolean }) {
  return (
    <label
      className={`flex cursor-pointer items-start gap-3 rounded-lg border p-3 text-sm transition-colors ${
        checked ? "border-accent bg-accent-soft/40" : "border-line hover:bg-muted/50"
      }`}
    >
      <input type="radio" checked={checked} onChange={onChange} disabled={disabled} className="mt-0.5 size-4 accent-[var(--color-accent)]" />
      <span>
        <span className="block font-medium text-ink">{title}</span>
        <span className="block text-xs text-ink-3">{hint}</span>
      </span>
    </label>
  );
}

function Switch({ checked, onChange, disabled, label }: { checked: boolean; onChange: (v: boolean) => void; disabled?: boolean; label: string }) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={label}
      disabled={disabled}
      onClick={() => onChange(!checked)}
      className={`relative inline-flex h-5 w-9 shrink-0 items-center rounded-full transition-colors disabled:opacity-40 ${checked ? "bg-accent" : "bg-muted ring-1 ring-inset ring-line"}`}
    >
      <span className={`inline-block size-4 rounded-full bg-white shadow transition-transform ${checked ? "translate-x-[18px]" : "translate-x-0.5"}`} />
    </button>
  );
}

/** Поиск пользователя OneBase по имени или логину. */
function UserPicker({ value, name, onChange, disabled }: { value: string | null; name: string; onChange: (id: string | null) => void; disabled?: boolean }) {
  const t = useT();
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<OneBaseUser[]>([]);
  const [open, setOpen] = useState(false);

  useEffect(() => {
    if (!open) return;
    const timer = setTimeout(() => telegramApi.oneBaseUsers(query).then(setResults, () => setResults([])), 200);
    return () => clearTimeout(timer);
  }, [query, open]);

  if (value && !open) {
    return (
      <div className="flex gap-2">
        <span className={`${field} flex items-center truncate`}>{name || t("выбран")}</span>
        <button type="button" className="shrink-0 rounded-lg border border-line px-3 text-sm text-ink hover:bg-muted" onClick={() => setOpen(true)} disabled={disabled}>
          {t("Сменить")}
        </button>
        <button type="button" className="shrink-0 rounded-lg border border-line px-3 text-sm text-ink-3 hover:bg-muted" onClick={() => onChange(null)} disabled={disabled}>
          {t("Убрать")}
        </button>
      </div>
    );
  }

  return (
    <div className="relative">
      <input className={field} value={query} onChange={(e) => setQuery(e.target.value)} onFocus={() => setOpen(true)} placeholder={t("Имя или логин сотрудника OneBase")} disabled={disabled} />
      {open && (
        <div className="absolute z-10 mt-1 max-h-64 w-full overflow-auto rounded-xl border border-line bg-surface-solid p-1.5 shadow-lg">
          {results.length === 0 ? (
            <p className="px-3 py-2 text-sm text-ink-3">{t("Никого не найдено")}</p>
          ) : (
            results.map((u) => (
              <button
                key={u.id}
                type="button"
                className="block w-full rounded-lg px-3 py-2 text-left text-sm hover:bg-muted"
                onClick={() => {
                  onChange(u.id);
                  setOpen(false);
                  setQuery("");
                }}
              >
                <span className="block text-ink">{u.fullName}</span>
                <span className="text-xs text-ink-3">
                  {u.login}
                  {u.position && ` · ${u.position}`}
                </span>
              </button>
            ))
          )}
          <button type="button" className="mt-1 block w-full rounded-lg px-3 py-1.5 text-left text-xs text-ink-3 hover:bg-muted" onClick={() => setOpen(false)}>
            {t("Закрыть")}
          </button>
        </div>
      )}
    </div>
  );
}
