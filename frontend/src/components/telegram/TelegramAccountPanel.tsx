"use client";

import { useState } from "react";
import { Loader2, LogOut, Pencil, RefreshCw, Smartphone, Trash2 } from "lucide-react";
import { button, danger, field, primary } from "@/components/ai/form";
import { Note } from "@/components/sales/bits";
import { accountStatus, ago, telegramApi, type TelegramAccount } from "@/lib/telegram";

const when = (iso: string | null) => (iso ? new Date(iso).toLocaleString("ru-RU", { dateStyle: "short", timeStyle: "short" }) : "—");

/**
 * Подключение аккаунта: номер → код из Telegram → пароль двухэтапной защиты (если включена). Код и пароль уходят на сервер один раз и
 * нигде не сохраняются; поля очищаются после отправки.
 */
export function TelegramAccountPanel({
  account,
  onChange,
  onRemoved,
}: {
  account: TelegramAccount;
  onChange: (a: TelegramAccount) => void;
  onRemoved: () => Promise<void>;
}) {
  const [step, setStep] = useState<"idle" | "phone" | "code" | "password">(
    account.status === "CodeRequired" ? "code" : account.status === "PasswordRequired" ? "password" : "idle",
  );
  const [phone, setPhone] = useState("");
  const [code, setCode] = useState("");
  const [password, setPassword] = useState("");
  const [renaming, setRenaming] = useState(false);
  const [name, setName] = useState(account.name);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

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

  const advance = (status: TelegramAccount["status"], a: TelegramAccount) => {
    onChange(a);
    if (status === "CodeRequired") setStep("code");
    else if (status === "PasswordRequired") setStep("password");
    else {
      setStep("idle");
      if (status === "Connected") setMessage(`Подключено: ${a.username ? "@" + a.username : a.displayName}. Чаты загружены.`);
    }
  };

  const connected = account.status === "Connected";
  const s = accountStatus[account.status];

  return (
    <div>
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          {renaming ? (
            <div className="flex gap-2">
              <input className={field} value={name} onChange={(e) => setName(e.target.value)} maxLength={100} />
              <button
                className={primary}
                disabled={!!busy || !name.trim()}
                onClick={() =>
                  run("rename", async () => {
                    onChange(await telegramApi.renameAccount(account.id, name));
                    setRenaming(false);
                  })
                }
              >
                Сохранить
              </button>
            </div>
          ) : (
            <h3 className="flex items-center gap-2 text-base font-semibold text-ink">
              {account.name}
              <button className="text-ink-3 hover:text-ink" onClick={() => setRenaming(true)} title="Переименовать">
                <Pencil className="size-3.5" />
              </button>
            </h3>
          )}
          <dl className="mt-2 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
            <dt className="text-ink-3">Статус</dt>
            <dd>
              <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${s.className}`}>{s.label}</span>
            </dd>
            <dt className="text-ink-3">Аккаунт</dt>
            <dd className="text-ink">
              {account.displayName ?? "—"}
              {account.username && <span className="ml-1 text-ink-2">@{account.username}</span>}
            </dd>
            <dt className="text-ink-3">Номер</dt>
            <dd className="text-ink">{account.phoneHint ?? "—"}</dd>
            <dt className="text-ink-3">Подключён</dt>
            <dd className="text-ink">{when(account.connectedAt)}</dd>
            <dt className="text-ink-3">Активность</dt>
            <dd className="text-ink">{when(account.lastActivityAt)}</dd>
            {account.status === "Connected" && (
              <>
                <dt className="text-ink-3">Приём сообщений</dt>
                <dd className="text-ink">
                  {!account.live?.online ? (
                    <span className="text-warn">клиент не запущен — нажмите «Проверить»</span>
                  ) : account.live.events === 0 ? (
                    <span className="text-ink-2">работает, событий от Telegram пока не было</span>
                  ) : (
                    <span className="text-ok">
                      работает · последнее событие {ago(account.live.lastEventAt)}
                      {account.live.messages > 0 && `, новых сообщений с запуска: ${account.live.messages}`}
                    </span>
                  )}
                </dd>
              </>
            )}
          </dl>
          {/* Причина последней ошибки аккаунта; если только что была ошибка действия — она показана ниже, дважды не выводим. */}
          {account.lastError && !error && <p className="mt-2 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{account.lastError}</p>}
        </div>
        <div className="flex flex-wrap gap-2">
          {connected ? (
            <>
              <button className={button} disabled={!!busy} onClick={() => run("check", async () => {
                onChange(await telegramApi.check(account.id));
                setMessage("Сессия действует.");
              })}>
                {busy === "check" ? <Loader2 className="size-4 animate-spin" /> : <RefreshCw className="size-4" />}
                Проверить
              </button>
              <button className={button} disabled={!!busy} onClick={() => setStep("phone")}>
                <Smartphone className="size-4" />
                Переподключить
              </button>
              <button
                className={danger}
                disabled={!!busy}
                onClick={() => {
                  if (confirm("Отключить аккаунт? OneBase выйдет из сессии Telegram; агент и рассылки этого аккаунта остановятся.")) {
                    run("disconnect", async () => onChange(await telegramApi.disconnect(account.id)));
                  }
                }}
              >
                <LogOut className="size-4" />
                Отключить
              </button>
            </>
          ) : (
            <button className={primary} disabled={!!busy} onClick={() => setStep("phone")}>
              <Smartphone className="size-4" />
              Подключить
            </button>
          )}
          {!connected && (
            <button
              className={danger}
              disabled={!!busy}
              onClick={() => {
                if (confirm(`Удалить аккаунт «${account.name}» вместе со списком пользователей, чатами и журналом агента?`)) {
                  run("delete", async () => {
                    await telegramApi.deleteAccount(account.id);
                    await onRemoved();
                  });
                }
              }}
            >
              <Trash2 className="size-4" />
              Удалить
            </button>
          )}
        </div>
      </div>

      {step !== "idle" && (
        <div className="mt-5 max-w-xl rounded-lg border border-accent/30 bg-accent-soft/30 p-4">
          {step === "phone" && (
            <Step
              label="Номер телефона аккаунта"
              hint="Telegram пришлёт код в приложение Telegram на этом номере (или SMS). Прежняя сессия OneBase этого аккаунта завершится."
              busy={busy === "login"}
              disabled={phone.replace(/\D/g, "").length < 10}
              onCancel={() => setStep("idle")}
              onSubmit={() =>
                run("login", async () => {
                  const r = await telegramApi.startLogin(account.id, phone);
                  advance(r.status, r.account);
                })
              }
              action="Получить код"
            >
              <input className={field} value={phone} onChange={(e) => setPhone(e.target.value)} placeholder="+998 90 123 45 67" type="tel" autoComplete="off" />
            </Step>
          )}
          {step === "code" && (
            <Step
              label="Код из Telegram"
              hint="Код пришёл в приложение Telegram (чат «Telegram») или SMS. Не пересылайте его никому — даже в OneBase он не сохраняется."
              busy={busy === "code"}
              disabled={code.replace(/\D/g, "").length < 4}
              onCancel={() => setStep("idle")}
              onSubmit={() =>
                run("code", async () => {
                  const r = await telegramApi.submitCode(account.id, code);
                  setCode("");
                  advance(r.status, r.account);
                })
              }
              action="Подтвердить"
            >
              <input className={field} value={code} onChange={(e) => setCode(e.target.value)} inputMode="numeric" autoComplete="one-time-code" autoFocus />
            </Step>
          )}
          {step === "password" && (
            <Step
              label="Пароль двухэтапной защиты"
              hint="У аккаунта включена двухэтапная защита (облачный пароль Telegram). Пароль не сохраняется."
              busy={busy === "password"}
              disabled={!password}
              onCancel={() => setStep("idle")}
              onSubmit={() =>
                run("password", async () => {
                  const r = await telegramApi.submitPassword(account.id, password);
                  setPassword("");
                  advance(r.status, r.account);
                })
              }
              action="Войти"
            >
              <input className={field} value={password} onChange={(e) => setPassword(e.target.value)} type="password" autoComplete="off" autoFocus />
            </Step>
          )}
        </div>
      )}

      {message && <p className="mt-4 rounded-lg bg-ok-soft px-3 py-2 text-sm text-ok">{message}</p>}
      {error && <p className="mt-4 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}

      <Note>
        OneBase работает от имени этого аккаунта: видит его чаты, отвечает (если включён агент) и рассылает сообщения. Сессия хранится на сервере
        зашифрованной и в браузер не передаётся. Отозвать доступ можно и из Telegram: «Настройки → Устройства» — сессия «OneBase».
        Telegram ограничивает частоту сообщений и может заблокировать аккаунт за массовые сообщения незнакомым людям — пишите тем, кто ждёт сообщений.
      </Note>
    </div>
  );
}

function Step({
  label,
  hint,
  busy,
  disabled,
  action,
  onSubmit,
  onCancel,
  children,
}: {
  label: string;
  hint: string;
  busy: boolean;
  disabled: boolean;
  action: string;
  onSubmit: () => void;
  onCancel: () => void;
  children: React.ReactNode;
}) {
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        if (!disabled && !busy) onSubmit();
      }}
    >
      <span className="mb-1.5 block text-sm font-medium text-ink">{label}</span>
      {children}
      <span className="mt-1 block text-xs text-ink-3">{hint}</span>
      <div className="mt-3 flex gap-2">
        <button type="submit" className={primary} disabled={disabled || busy}>
          {busy && <Loader2 className="size-4 animate-spin" />}
          {action}
        </button>
        <button type="button" className={button} onClick={onCancel} disabled={busy}>
          Отмена
        </button>
      </div>
    </form>
  );
}
