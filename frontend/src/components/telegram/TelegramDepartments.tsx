"use client";

import { useEffect, useState } from "react";
import { Building2, Loader2 } from "lucide-react";
import { button, primary } from "@/components/ai/form";
import { useT } from "@/i18n/client";
import { telegramApi, type TelegramDepartments as Departments } from "@/lib/telegram";

const UNASSIGNED = "";

/**
 * Для каких подразделений работает номер: продажи, HR, … и кому новые диалоги. Пока ничего не задано — все диалоги у продаж, как раньше.
 * Вход и сессия аккаунта от этого не меняются; номер, отданный другому подразделению, не предлагается рассылкам и автоотчётам продаж.
 */
export function TelegramDepartments({ accountId }: { accountId: string }) {
  const t = useT();
  const [view, setView] = useState<Departments | null>(null);
  const [selected, setSelected] = useState<string[]>([]);
  const [fallback, setFallback] = useState<string>(UNASSIGNED);
  const [editing, setEditing] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const apply = (v: Departments) => {
    setView(v);
    setSelected(v.departments.filter((d) => d.enabled).map((d) => d.code));
    setFallback(v.departments.find((d) => d.isDefault)?.code ?? UNASSIGNED);
  };

  useEffect(() => {
    let alive = true;
    telegramApi
      .departments(accountId)
      .then((v) => alive && apply(v))
      .catch((e: Error) => alive && setError(e.message));
    return () => {
      alive = false;
    };
  }, [accountId]);

  if (!view) {
    return error ? <p className="mt-4 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p> : null;
  }

  const enabled = view.departments.filter((d) => d.enabled);
  const fallbackName = view.departments.find((d) => d.isDefault)?.name;
  const toggle = (code: string) => {
    const next = selected.includes(code) ? selected.filter((c) => c !== code) : [...selected, code];
    setSelected(next);
    if (fallback !== UNASSIGNED && !next.includes(fallback)) setFallback(UNASSIGNED);
  };

  const save = async () => {
    setBusy(true);
    setError(null);
    try {
      apply(await telegramApi.saveDepartments(accountId, selected, fallback === UNASSIGNED ? null : fallback));
      setEditing(false);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="mt-5 rounded-lg border border-line p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h4 className="flex items-center gap-2 text-sm font-semibold text-ink">
            <Building2 className="size-4 text-ink-3" />
            {t("Подразделения")}
          </h4>
          {!editing && (
            <p className="mt-1 text-sm text-ink-2">
              {!view.configured
                ? t("Не заданы — все диалоги этого номера у продаж.")
                : t("Работает для: {list}. Новые диалоги: {fallback}.", {
                    list: enabled.map((d) => t(d.name)).join(", "),
                    fallback: fallbackName ? t(fallbackName) : t("не распределять — решает человек"),
                  })}
            </p>
          )}
          {!editing && enabled.some((d) => !d.hasHandler) && (
            <p className="mt-1 text-xs text-ink-3">
              {t("У подразделения {list} AI-обработчика пока нет: его диалогам никто не отвечает автоматически.", {
                list: enabled.filter((d) => !d.hasHandler).map((d) => t(d.name)).join(", "),
              })}
            </p>
          )}
        </div>
        {!editing && (
          <button className={button} onClick={() => setEditing(true)}>
            {t("Изменить")}
          </button>
        )}
      </div>

      {editing && (
        <div className="mt-3 space-y-3">
          <p className="text-xs text-ink-3">
            {t(
              "Диалог принадлежит одному подразделению: агент продаж не видит диалоги HR, и наоборот. Диалоги, уже закреплённые за подразделением, остаются у него. Номер, который работает не для продаж, не предлагается рассылкам и автоотчётам продаж.",
            )}
          </p>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs text-ink-3">
                  <th className="py-1 pr-3 font-medium">{t("Подразделение")}</th>
                  <th className="py-1 pr-3 font-medium">{t("Работает с номером")}</th>
                  <th className="py-1 font-medium">{t("Новые диалоги")}</th>
                </tr>
              </thead>
              <tbody>
                {view.departments.map((d) => {
                  const on = selected.includes(d.code);
                  return (
                    <tr key={d.code} className="border-t border-line">
                      <td className="py-2 pr-3 text-ink">
                        {t(d.name)}
                        {!d.hasHandler && <span className="ml-2 text-xs text-ink-3">{t("без AI-обработчика")}</span>}
                      </td>
                      <td className="py-2 pr-3">
                        <input type="checkbox" className="size-4 accent-[var(--color-accent)]" checked={on} onChange={() => toggle(d.code)} aria-label={t(d.name)} />
                      </td>
                      <td className="py-2">
                        <input
                          type="radio"
                          name={`fallback-${accountId}`}
                          className="size-4 accent-[var(--color-accent)]"
                          disabled={!on}
                          checked={fallback === d.code}
                          onChange={() => setFallback(d.code)}
                          aria-label={t("Новые диалоги — {name}", { name: t(d.name) })}
                        />
                      </td>
                    </tr>
                  );
                })}
                <tr className="border-t border-line">
                  <td className="py-2 pr-3 text-ink-2" colSpan={2}>
                    {t("Не распределять — решает человек")}
                  </td>
                  <td className="py-2">
                    <input
                      type="radio"
                      name={`fallback-${accountId}`}
                      className="size-4 accent-[var(--color-accent)]"
                      checked={fallback === UNASSIGNED}
                      onChange={() => setFallback(UNASSIGNED)}
                      aria-label={t("Не распределять — решает человек")}
                    />
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
          <div className="flex flex-wrap gap-2">
            <button className={primary} disabled={busy || selected.length === 0} onClick={save}>
              {busy && <Loader2 className="size-4 animate-spin" />}
              {t("Сохранить")}
            </button>
            <button
              className={button}
              disabled={busy}
              onClick={() => {
                apply(view);
                setEditing(false);
                setError(null);
              }}
            >
              {t("Отмена")}
            </button>
          </div>
        </div>
      )}

      {error && <p className="mt-3 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
    </section>
  );
}
