"use client";

import { useState } from "react";
import { Phone } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { Card, ErrorText, useAction } from "@/components/recruitment/ui";
import { useT } from "@/i18n/client";
import { accountChange, accountLabel, agentsApi, type AgentView } from "@/lib/agents";

const statusText: Record<string, string> = {
  New: "не подключён",
  CodeRequired: "ждёт код",
  PasswordRequired: "ждёт пароль",
  Disconnected: "отключён",
  Error: "ошибка подключения",
};

/**
 * «Номер Telegram» агента: через какой номер он отвечает. Пусто — любой номер, который работает на отдел. Выбор номера сохраняется сразу
 * (отдельно от остальных настроек) и показывает, что изменится у номера.
 */
export function AgentNumber({ agent, onChange }: { agent: AgentView; onChange: (v: AgentView) => void }) {
  const t = useT();
  const [value, setValue] = useState(agent.accountId ?? "");
  const { busy, error, run } = useAction();
  const current = agent.accounts.find((a) => a.id === agent.accountId) ?? null;
  const picked = agent.accounts.find((a) => a.id === value) ?? null;
  const change = picked ? accountChange(picked, agent.department, agent.canConfigureAccounts) : null;
  const dirty = value !== (agent.accountId ?? "");
  const departmentNumbers = agent.accounts.filter((a) => a.serves);

  const save = () =>
    run("account", async () => {
      const v = await agentsApi.account(agent.id, value || null);
      onChange(v);
      setValue(v.accountId ?? "");
    });

  return (
    <Card title="Номер Telegram">
      <div className="max-w-3xl space-y-3">
        <p className="text-xs text-ink-3">
          {t("Через какой номер агент отвечает. Агент ведёт только диалоги, которые пришли на этот номер; на номере без своего агента отвечает агент отдела без номера.")}
        </p>
        <div className="flex flex-wrap items-center gap-2">
          <Phone className="size-4 shrink-0 text-ink-3" aria-hidden />
          <select
            aria-label={t("Номер Telegram")}
            className={`${field} min-w-0 flex-1 sm:max-w-md`}
            value={value}
            disabled={!!busy}
            onChange={(e) => setValue(e.target.value)}
          >
            <option value="">{t("Любой номер отдела")}</option>
            {agent.accounts.map((a) => (
              <option key={a.id} value={a.id} disabled={!a.selectable && a.id !== agent.accountId}>
                {accountLabel(a)}
                {a.status !== "Connected" ? ` — ${t(statusText[a.status] ?? a.status)}` : ""}
                {a.takenBy ? ` — ${t("у агента «{name}»", { name: a.takenBy })}` : !a.selectable ? ` — ${t("не работает на отдел")}` : ""}
              </option>
            ))}
          </select>
          {dirty && (
            <>
              <button className={primary} disabled={!!busy} onClick={save}>
                {busy ? t("Сохраняю…") : t("Сохранить номер")}
              </button>
              <button className={button} disabled={!!busy} onClick={() => setValue(agent.accountId ?? "")}>
                {t("Отмена")}
              </button>
            </>
          )}
        </div>

        {agent.accounts.length === 0 && (
          <p className="rounded-lg bg-warn-soft px-3 py-2 text-sm text-warn">
            {t("Номеров Telegram нет. Подключите номер в «Настройки → Telegram», затем выберите его здесь.")}
          </p>
        )}

        {/* Что будет после сохранения выбранного номера. */}
        {dirty && picked && change === "configure" && (
          <p className="rounded-lg bg-accent-soft/50 px-3 py-2 text-sm text-ink-2">
            {t("После сохранения номер «{name}» начнёт работать на отдел «{department}», и новые диалоги на нём пойдут агенту.", {
              name: picked.name,
              department: agent.departmentName,
            })}{" "}
            {picked.defaultDepartment === "sales"
              ? t("Люди из списка агента продаж и уже начатые диалоги остаются у продаж; рассылки и автоотчёты с номера не меняются.")
              : t("Уже начатые диалоги остаются у своих отделов.")}
          </p>
        )}
        {dirty && picked && change === "foreign-default" && (
          <p className="rounded-lg bg-warn-soft px-3 py-2 text-sm text-warn">
            {t("Новые диалоги на номере «{name}» идут отделу «{department}» — агент получит только диалоги, которые попадут в его отдел. Отдать номеру новые диалоги может администратор Telegram.", {
              name: picked.name,
              department: picked.defaultDepartmentName ?? t("не распределён"),
            })}
          </p>
        )}

        {!dirty && current && current.status !== "Connected" && (
          <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">
            {t("Номер «{name}» {status} — сообщения не принимаются и не отправляются. Переподключите его в «Настройки → Telegram».", {
              name: current.name,
              status: t(statusText[current.status] ?? current.status),
            })}
          </p>
        )}
        {!dirty && !current && (
          <p className="text-xs text-ink-3">
            {departmentNumbers.length > 0
              ? t("Сейчас отвечает на номерах отдела: {names}.", { names: departmentNumbers.map((a) => a.name).join(", ") })
              : t("Ни один номер ещё не работает на отдел «{department}» — выберите номер, и агент начнёт отвечать.", { department: agent.departmentName })}
          </p>
        )}
        {!agent.canConfigureAccounts && (
          <p className="text-xs text-ink-3">{t("Можно выбрать номера, которые уже работают на отдел. Подключить к отделу другой номер может администратор Telegram.")}</p>
        )}
        <ErrorText error={error} />
      </div>
    </Card>
  );
}
