"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { CheckCircle2, Eye, EyeOff, Loader2, PlugZap, RefreshCw, XCircle } from "lucide-react";
import { Note, Section } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { momentLabel, VERIFIX_JOURNAL, type VerifixDetails, type VerifixTestResult } from "@/lib/hr";
import { statusView } from "@/lib/integrations";

const field =
  "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
const button = "inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted disabled:opacity-50";
const primary = "inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50";

const secretSource = { OneBase: "сохранён в OneBase (зашифрован)", Environment: "взят из окружения сервера", None: "" } as const;

/**
 * «Настройки интеграции Verifix»: адрес API, client_id и client_secret клиента OAuth2 (секрет не показывается — только последние символы),
 * организация, расписание, глубина истории и допуски; проверка подключения, синхронизация и привязка подразделений Verifix к отделам OneBase.
 */
export function VerifixSettings({ initial }: { initial: VerifixDetails }) {
  const t = useT();
  const [data, setData] = useState(initial);
  const [form, setForm] = useState({
    baseUrl: initial.baseUrl,
    clientId: initial.clientIdFromEnvironment ? "" : initial.clientId,
    filialId: initial.filialId ? String(initial.filialId) : "",
    enabled: initial.enabled,
    autoSync: initial.autoSync,
    intervalMinutes: String(initial.intervalMinutes),
    historyDays: String(initial.historyDays),
    overlapDays: String(initial.overlapDays),
    lateGraceMinutes: String(initial.lateGraceMinutes),
    earlyLeaveGraceMinutes: String(initial.earlyLeaveGraceMinutes),
  });
  const [secret, setSecret] = useState("");
  const [showSecret, setShowSecret] = useState(false);
  const [busy, setBusy] = useState<"test" | "save" | "sync" | "clear" | null>(null);
  const [test, setTest] = useState<VerifixTestResult | null>(null);
  const [message, setMessage] = useState<{ tone: "ok" | "bad"; text: string } | null>(null);

  const reload = async () => setData(await bff<VerifixDetails>("integrations/verifix"));

  useEffect(() => {
    if (!data.sync.running) return;
    const timer = setInterval(() => void reload().catch(() => undefined), 3000);
    return () => clearInterval(timer);
  }, [data.sync.running]);

  const set = (key: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm((f) => ({ ...f, [key]: e.target.type === "checkbox" ? e.target.checked : e.target.value }));
  const num = (value: string, fallback: number) => (Number.isFinite(Number(value)) && value.trim() !== "" ? Math.round(Number(value)) : fallback);

  const run = async (kind: NonNullable<typeof busy>, action: () => Promise<void>) => {
    setBusy(kind);
    setMessage(null);
    try {
      await action();
    } catch (e) {
      setMessage({ tone: "bad", text: (e as Error).message });
    } finally {
      setBusy(null);
    }
  };

  const save = (secretValue: string | null, kind: "save" | "clear") =>
    run(kind, async () => {
      const saved = await bff<VerifixDetails>("integrations/verifix", {
        method: "PUT",
        body: JSON.stringify({
          baseUrl: form.baseUrl.trim(),
          clientId: form.clientId.trim(),
          clientSecret: secretValue,
          filialId: form.filialId.trim() ? num(form.filialId, 0) : null,
          enabled: form.enabled,
          autoSync: form.autoSync,
          intervalMinutes: num(form.intervalMinutes, 60),
          historyDays: num(form.historyDays, 31),
          overlapDays: num(form.overlapDays, 3),
          lateGraceMinutes: num(form.lateGraceMinutes, 0),
          earlyLeaveGraceMinutes: num(form.earlyLeaveGraceMinutes, 0),
        }),
      });
      setData(saved);
      setSecret("");
      setMessage({ tone: "ok", text: kind === "clear" ? t("client_secret удалён из OneBase.") : t("Настройки сохранены.") });
    });

  const runTest = () =>
    run("test", async () => {
      setTest(
        await bff<VerifixTestResult>("integrations/verifix/test", {
          method: "POST",
          body: JSON.stringify({
            baseUrl: form.baseUrl.trim() || null,
            clientId: form.clientId.trim() || null,
            clientSecret: secret.trim() || null,
            filialId: form.filialId.trim() ? num(form.filialId, 0) : null,
          }),
        }),
      );
      await reload();
    });

  const sync = () =>
    run("sync", async () => {
      await bff("integrations/verifix/sync", { method: "POST" });
      setMessage({ tone: "ok", text: t("Синхронизация запущена — ход и итог видны в журнале синхронизации.") });
      await reload();
    });

  const mapDivision = (id: number, departmentCode: string) =>
    run("save", async () => {
      await bff(`integrations/verifix/divisions/${id}/department`, { method: "PUT", body: JSON.stringify({ departmentCode: departmentCode || null }) });
      await reload();
    });

  const status = statusView[data.status];
  const dirty =
    secret.trim() !== "" ||
    form.baseUrl.trim() !== data.baseUrl ||
    form.clientId.trim() !== (data.clientIdFromEnvironment ? "" : data.clientId) ||
    form.enabled !== data.enabled ||
    form.autoSync !== data.autoSync ||
    num(form.intervalMinutes, 0) !== data.intervalMinutes ||
    num(form.historyDays, 0) !== data.historyDays ||
    num(form.overlapDays, 0) !== data.overlapDays ||
    num(form.lateGraceMinutes, -1) !== data.lateGraceMinutes ||
    num(form.earlyLeaveGraceMinutes, -1) !== data.earlyLeaveGraceMinutes ||
    (form.filialId.trim() ? num(form.filialId, 0) : null) !== data.filialId;

  return (
    <div className="space-y-4">
      <Section
        title="Подключение"
        hint="Клиент OAuth2 создаётся в Verifix: Администрирование → Настройки → Внешние системы → «Клиенты OAuth2 для сервера для компании» (тип client credentials, организация и роль с доступом к формам «Внешние системы»)."
        actions={<span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${status.className}`}>{t(status.label)}</span>}
      >
        <div className="grid gap-4 md:grid-cols-2">
          <label className="block text-sm">
            <span className="mb-1 block font-medium text-ink">{t("Адрес API")}</span>
            <input className={field} value={form.baseUrl} onChange={set("baseUrl")} placeholder="https://app.verifix.com" />
            <span className="mt-1 block text-xs text-ink-3">{t("По документации Verifix — https://app.verifix.com.")}</span>
          </label>
          <label className="block text-sm">
            <span className="mb-1 block font-medium text-ink">client_id</span>
            <input className={field} value={form.clientId} onChange={set("clientId")} placeholder={data.clientIdFromEnvironment ? t("взят из окружения сервера") : ""} autoComplete="off" />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block font-medium text-ink">client_secret</span>
            <span className="relative block">
              <input
                className={`${field} pr-10`}
                type={showSecret ? "text" : "password"}
                value={secret}
                onChange={(e) => setSecret(e.target.value)}
                placeholder={data.hasSecret ? `${data.secretHint ?? "••••"} — ${t("оставьте пустым, чтобы не менять")}` : t("секрет клиента OAuth2")}
                autoComplete="new-password"
              />
              <button type="button" onClick={() => setShowSecret((v) => !v)} className="absolute inset-y-0 right-2 grid place-items-center text-ink-3 hover:text-ink" aria-label={t(showSecret ? "Скрыть" : "Показать")}>
                {showSecret ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
              </button>
            </span>
            <span className="mt-1 block text-xs text-ink-3">
              {data.hasSecret ? t("Секрет {source}. В браузер он не возвращается.", { source: t(secretSource[data.secretSource]) }) : t("Секрет не задан.")}
            </span>
          </label>
          <label className="block text-sm">
            <span className="mb-1 block font-medium text-ink">{t("ID организации (filial_id)")}</span>
            <input className={field} value={form.filialId} onChange={set("filialId")} inputMode="numeric" placeholder={t("не обязательно")} />
            <span className="mt-1 block text-xs text-ink-3">{t("С client credentials организация задана в клиенте OAuth2 — поле можно оставить пустым.")}</span>
          </label>
        </div>
        <label className="mt-4 flex items-center gap-2 text-sm text-ink">
          <input type="checkbox" checked={form.enabled} onChange={set("enabled")} className="size-4 accent-[var(--color-accent)]" />
          {t("Интеграция включена")}
        </label>
      </Section>

      <Section title="Синхронизация и правила учёта" hint="Отметки за последние дни перечитываются каждый раз: терминал мог передать их позже, отметку могли исправить. Повторная загрузка дубликатов не создаёт.">
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <label className="flex items-center gap-2 text-sm text-ink sm:col-span-2 lg:col-span-3">
            <input type="checkbox" checked={form.autoSync} onChange={set("autoSync")} className="size-4 accent-[var(--color-accent)]" />
            {t("Синхронизировать автоматически")}
          </label>
          {(
            [
              ["intervalMinutes", "Интервал, минут", "от 15 до 1440"],
              ["historyDays", "Глубина первой загрузки, дней", "до 92"],
              ["overlapDays", "Перечитывать последние дни", "1–14"],
              ["lateGraceMinutes", "Допуск опоздания, минут", "0 — без допуска"],
              ["earlyLeaveGraceMinutes", "Допуск раннего ухода, минут", "0 — без допуска"],
            ] as const
          ).map(([key, label, hint]) => (
            <label key={key} className="block text-sm">
              <span className="mb-1 block font-medium text-ink">{t(label)}</span>
              <input className={field} value={form[key]} onChange={set(key)} inputMode="numeric" />
              <span className="mt-1 block text-xs text-ink-3">{t(hint)}</span>
            </label>
          ))}
          <div className="text-sm">
            <span className="mb-1 block font-medium text-ink">{t("Часовой пояс организации")}</span>
            <div className="flex h-10 items-center text-ink-2">{data.timeZone}</div>
            <span className="mt-1 block text-xs text-ink-3">{t("Время отметок Verifix — во временной зоне организации.")}</span>
          </div>
        </div>
      </Section>

      <div className="flex flex-wrap items-center gap-2">
        <button type="button" className={primary} disabled={busy !== null || !dirty} onClick={() => save(secret.trim() || null, "save")}>
          {busy === "save" && <Loader2 className="size-4 animate-spin" />} {t("Сохранить")}
        </button>
        <button type="button" className={button} disabled={busy !== null} onClick={runTest}>
          {busy === "test" ? <Loader2 className="size-4 animate-spin" /> : <PlugZap className="size-4" />} {t("Проверить подключение")}
        </button>
        <button type="button" className={button} disabled={busy !== null || data.sync.running || data.status === "not_configured" || !data.enabled} onClick={sync}>
          {busy === "sync" || data.sync.running ? <Loader2 className="size-4 animate-spin" /> : <RefreshCw className="size-4" />}{" "}
          {t(data.sync.running ? "Синхронизация идёт…" : "Синхронизировать сейчас")}
        </button>
        {data.secretSource === "OneBase" && (
          <button
            type="button"
            className={button}
            disabled={busy !== null}
            onClick={() => {
              if (confirm(t("Удалить client_secret из OneBase? Синхронизация остановится, пока секрет не задан снова."))) void save("", "clear");
            }}
          >
            {t("Удалить секрет")}
          </button>
        )}
        <Link href={VERIFIX_JOURNAL} className="text-sm text-accent-strong hover:underline">
          {t("Журнал синхронизации")}
        </Link>
      </div>
      {message && <p className={`text-sm ${message.tone === "ok" ? "text-ok" : "text-bad"}`}>{message.text}</p>}

      {(test || data.lastTest) && (
        <Section title="Проверка подключения">
          {test ? (
            <div>
              <p className={`flex items-center gap-2 text-sm font-medium ${test.ok ? "text-ok" : "text-bad"}`}>
                {test.ok ? <CheckCircle2 className="size-4" /> : <XCircle className="size-4" />} {t(test.message)}
                {test.organization && <span className="font-normal text-ink-2">· {test.organization}</span>}
              </p>
              {test.checks.length > 0 && (
                <ul className="mt-2 space-y-1 text-sm">
                  {test.checks.map((c) => (
                    <li key={c.method} className="flex items-start gap-2">
                      {c.ok ? <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-ok" /> : <XCircle className="mt-0.5 size-4 shrink-0 text-bad" />}
                      <span>
                        <span className="font-medium text-ink">{t(c.method)}</span> — <span className="text-ink-2">{t(c.message)}</span>
                      </span>
                    </li>
                  ))}
                </ul>
              )}
              <p className="mt-2 text-xs text-ink-3">{t("{ms} мс", { ms: test.elapsedMs })}</p>
            </div>
          ) : (
            data.lastTest && (
              <p className={`text-sm ${data.lastTest.ok ? "text-ok" : "text-bad"}`}>
                {momentLabel(data.lastTest.at)} — {data.lastTest.message ? t(data.lastTest.message) : ""}
              </p>
            )
          )}
        </Section>
      )}

      <Section title="Загруженные данные">
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          {(
            [
              ["Работают", data.employees.working],
              ["Уволены", data.employees.dismissed],
              ["Не приняты", data.employees.notHired],
              ["Нет в Verifix", data.employees.missing],
            ] as const
          ).map(([label, value]) => (
            <div key={label} className="rounded-lg border border-line p-3">
              <div className="text-lg font-semibold tabular-nums text-ink">{value}</div>
              <div className="text-xs text-ink-3">{t(label)}</div>
            </div>
          ))}
        </div>
        {data.sync.entities.length > 0 && (
          <div className="mt-3 overflow-x-auto">
            <table className="w-full min-w-[560px] text-sm">
              <thead>
                <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                  <th className="py-2 pr-3 font-semibold">{t("Данные")}</th>
                  <th className="py-2 pr-3 font-semibold">{t("Последний успех")}</th>
                  <th className="py-2 pr-3 text-right font-semibold">{t("Строк")}</th>
                  <th className="py-2 pr-3 font-semibold">{t("Полные по")}</th>
                  <th className="py-2 font-semibold">{t("Ошибка")}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {data.sync.entities.map((e) => (
                  <tr key={e.entity}>
                    <td className="py-2 pr-3 text-ink">{t(e.title)}</td>
                    <td className="py-2 pr-3 tabular-nums text-ink-2">{momentLabel(e.lastSuccessAt)}</td>
                    <td className="py-2 pr-3 text-right tabular-nums">{e.lastRows}</td>
                    <td className="py-2 pr-3 tabular-nums text-ink-2">{e.coveredThrough ? momentLabel(e.coveredThrough) : "—"}</td>
                    <td className="py-2 text-xs text-bad">{e.lastError ? t(e.lastError) : ""}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Section>

      <Section
        title="Подразделения Verifix и отделы OneBase"
        hint="Руководитель отдела OneBase (назначается в «Задачи → Проекты → Руководители отделов») видит в HR только сотрудников привязанных подразделений с дочерними. Привязка хранится в OneBase, синхронизация её не меняет."
      >
        {data.divisions.length === 0 ? (
          <p className="text-sm text-ink-3">{t("Подразделения появятся после первой синхронизации.")}</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[520px] text-sm">
              <thead>
                <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                  <th className="py-2 pr-3 font-semibold">{t("Подразделение Verifix")}</th>
                  <th className="py-2 pr-3 text-right font-semibold">{t("Работают")}</th>
                  <th className="py-2 font-semibold">{t("Отдел OneBase")}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {data.divisions.map((d) => (
                  <tr key={d.id}>
                    <td className="py-2 pr-3 text-ink">
                      {d.name}
                      {d.parentId != null && <span className="ml-1.5 text-xs text-ink-3">{t("в «{name}»", { name: data.divisions.find((p) => p.id === d.parentId)?.name ?? `#${d.parentId}` })}</span>}
                      {d.missing && <span className="ml-1.5 text-xs text-warn">{t("нет в Verifix")}</span>}
                    </td>
                    <td className="py-2 pr-3 text-right tabular-nums">{d.employees}</td>
                    <td className="py-2">
                      <select
                        className="h-9 rounded-lg border border-line bg-surface px-2 text-sm text-ink"
                        value={d.departmentCode ?? ""}
                        disabled={busy !== null}
                        onChange={(e) => void mapDivision(d.id, e.target.value)}
                      >
                        <option value="">{t("— не привязано —")}</option>
                        {data.departments.map((dep) => (
                          <option key={dep.code} value={dep.code}>
                            {t(dep.name)}
                          </option>
                        ))}
                      </select>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Section>

      <Note>
        {t(
          "Из Verifix загружаются только данные учёта времени: ФИО, телефон, e-mail, статус, даты приёма и увольнения, подразделение, должность, график, отметки и табель. ПИНФЛ, ИНН, паспорт, фото, биометрические шаблоны и номера карт доступа не загружаются. OneBase в Verifix ничего не записывает.",
        )}
      </Note>
    </div>
  );
}
