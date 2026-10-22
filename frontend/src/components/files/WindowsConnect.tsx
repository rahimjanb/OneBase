"use client";

import { Fragment, useCallback, useEffect, useState } from "react";
import { Check, Copy, Download, Eye, EyeOff, HardDrive, KeyRound, Link2, Loader2, MonitorUp, ShieldOff, Stethoscope } from "lucide-react";
import { Button } from "@/components/ui";
import { useT } from "@/i18n/client";
import {
  accessLabel,
  filesApi,
  modifiedLabel,
  type ConnectionLink,
  type ConnectionTest,
  type ConnectionView,
  type DepartmentAccessRow,
  type DepartmentRef,
} from "@/lib/files";
import { Modal } from "./Modal";

const statusLabel = { active: "Подключено", none: "Не настроено", revoked: "Отозвано" } as const;
const statusTone = { active: "bg-ok/15 text-ok", none: "bg-muted text-ink-2", revoked: "bg-bad/10 text-bad" } as const;

/** Переведённая фраза с вставками-разметкой: {имя} в тексте заменяется узлом из nodes (например, <code>). */
const withNodes = (text: string, nodes: Record<string, React.ReactNode>) =>
  text.split(/\{(\w+)\}/).map((part, i) => (i % 2 ? <Fragment key={i}>{nodes[part]}</Fragment> : part));

/** Кнопка «Подключить к Windows» на странице отдела и окно со всем нужным для подключения. */
export function WindowsConnectButton({ department }: { department: DepartmentRef }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  return (
    <>
      <Button variant="primary" onClick={() => setOpen(true)}>
        <MonitorUp className="size-4" />
        {t("Подключить к Windows")}
      </Button>
      {open && <WindowsConnectDialog department={department} onClose={() => setOpen(false)} />}
    </>
  );
}

function WindowsConnectDialog({ department, onClose }: { department: DepartmentRef; onClose: () => void }) {
  const t = useT();
  const [view, setView] = useState<ConnectionView | null>(null);
  const [password, setPassword] = useState<string | null>(null);
  const [showPassword, setShowPassword] = useState(false);
  const [link, setLink] = useState<ConnectionLink | null>(null);
  const [showLink, setShowLink] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [copied, setCopied] = useState<string | null>(null);
  const [test, setTest] = useState<ConnectionTest | null>(null);

  const load = useCallback(() => {
    filesApi.connection(department.code).then(setView, (e: Error) => setError(e.message));
  }, [department.code]);
  useEffect(load, [load]);

  // Пароль запрашивается только по нажатию и живёт в состоянии окна; в адрес, логи и аналитику не попадает.
  const revealPassword = async () => {
    if (password) return password;
    const { password: value } = await filesApi.password(department.code);
    setPassword(value);
    return value;
  };

  // Адрес без пароля — как пароль: только по нажатию, в состоянии окна.
  const revealLink = async () => {
    if (link) return link;
    const value = await filesApi.link(department.code);
    setLink(value);
    return value;
  };

  const run = async (key: string, action: () => Promise<unknown>) => {
    setBusy(key);
    setError(null);
    try {
      await action();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(null);
    }
  };

  const copy = (key: string, value: string | (() => Promise<string> | string)) =>
    run(key, async () => {
      await navigator.clipboard.writeText(typeof value === "function" ? await value() : value);
      setCopied(key);
      window.setTimeout(() => setCopied((c) => (c === key ? null : c)), 1800);
    });

  const allText = async () => {
    const { address, uncPath } = await revealLink();
    return [
      t("Папка отдела «{name}» — подключить сетевым диском в Windows, логин и пароль не нужны.", { name: t(department.name) }),
      t("Адрес: {value}", { value: address }),
      uncPath ? t("Путь Windows: {value}", { value: uncPath }) : null,
    ]
      .filter(Boolean)
      .join("\n");
  };

  const downloadGuide = () => {
    if (!view) return;
    const text = [
      t("Как подключить папку отдела «{name}» к Windows", { name: t(department.name) }),
      "",
      t("Способ: {value}", { value: view.connectionType }),
      t("Адрес без пароля: выдаёт OneBase в окне «Подключить к Windows» (в файл не записывается)."),
      "",
      ...view.guide.map((step, i) => t("Шаг {n}. {step}", { n: i + 1, step: t(step) })),
      "",
      t("Если не получается:"),
      ...view.notes.map((n) => `— ${t(n)}`),
    ].join("\r\n");
    const url = URL.createObjectURL(new Blob([text], { type: "text/plain;charset=utf-8" }));
    const a = Object.assign(document.createElement("a"), { href: url, download: t("Подключение {name} к Windows.txt", { name: t(department.name) }) });
    a.click();
    URL.revokeObjectURL(url);
  };

  const active = view?.status === "active";
  return (
    <Modal title={t("Подключение «{name}» к Windows", { name: t(department.name) })} onClose={onClose} wide>
      {!view && !error && <Loader2 className="size-5 animate-spin text-ink-3" />}
      {error && <p className="mb-3 rounded-lg bg-bad/10 px-3 py-2 text-sm text-bad">{error}</p>}
      {view && (
        <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
          <div className="space-y-4">
            <div className="flex flex-wrap items-center gap-2 text-sm">
              <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${statusTone[view.status]}`}>{t(statusLabel[view.status])}</span>
              <span className="text-ink-2">{view.connectionType}</span>
              {view.lastUsedAt && <span className="text-xs text-ink-3">{t("последняя активность: {date}", { date: modifiedLabel(view.lastUsedAt, t) })}</span>}
            </div>

            {!view.domainConfigured && (
              <div className="space-y-2 rounded-lg bg-warn/15 px-3 py-2 text-sm text-ink">
                <p>{t("Адрес подключения ещё не настроен на сервере. Пока его нет, подключить папку к Windows нельзя — файлы отдела доступны здесь, на сайте.")}</p>
                {view.canManage && (
                  <ol className="list-decimal space-y-1 pl-5 text-xs text-ink-2">
                    <li>
                      {withNodes(t("В {env} на сервере: {domain}, затем {command}."), {
                        env: <code>.env</code>,
                        domain: <code>FILE_STORAGE_DOMAIN=files.1base.uz</code>,
                        command: <code>docker compose up -d api nginx</code>,
                      })}
                    </li>
                    <li>
                      {withNodes(t("В Cloudflare Zero Trust → Tunnels → Public Hostname: {host} → {target} и правило кэша «Bypass» для этого домена."), {
                        host: <code>files.1base.uz</code>,
                        target: <code>http://nginx:82</code>,
                      })}
                    </li>
                    <li>{t("Здесь: «Создать подключение» → «Проверить подключение» — все шаги должны быть ✓.")}</li>
                  </ol>
                )}
              </div>
            )}

            {!view.canView && (
              <p className="text-sm text-ink-2">
                {t(
                  "У вас доступ к отделу только на чтение — подключение даёт запись, поэтому логин и пароль показываются сотрудникам отдела с правом записи и администраторам. Файлы отдела доступны здесь, на сайте.",
                )}
              </p>
            )}

            {view.canView && view.status !== "active" && (
              <p className="text-sm text-ink-2">
                {view.status === "revoked" ? t("Подключение отозвано — вход по старому логину не работает.") : t("Для отдела ещё не создано подключение.")}{" "}
                {view.canManage ? t("Создайте его кнопкой ниже.") : t("Попросите администратора OneBase его создать.")}
              </p>
            )}

            {active && view.canView && (
              <div className="space-y-3">
                {view.linkAddress && (
                  <>
                    <Field
                      label={t("Адрес без пароля")}
                      value={showLink && link ? link.address : view.linkAddress}
                      onCopy={() => copy("link", async () => (await revealLink()).address)}
                      copied={copied === "link"}
                      extra={
                        <button
                          type="button"
                          className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs text-ink-2 hover:bg-muted"
                          onClick={() => (showLink ? setShowLink(false) : run("reveal-link", async () => (await revealLink(), setShowLink(true))))}
                        >
                          {showLink ? <EyeOff className="size-3.5" /> : <Eye className="size-3.5" />}
                          {showLink ? t("Скрыть") : t("Показать")}
                        </button>
                      }
                    />
                    {showLink && link?.uncPath && (
                      <Field label={t("Путь Windows")} value={link.uncPath} onCopy={() => copy("link-unc", link.uncPath!)} copied={copied === "link-unc"} />
                    )}
                    <p className="text-xs text-ink-3">
                      {t("Диск подключается по этому адресу без логина и пароля, Word и Excel открывают файлы сразу. Адрес работает как пароль — отправляйте его только сотрудникам отдела.")}
                    </p>
                  </>
                )}
                <div className="flex flex-wrap gap-2 pt-1">
                  <Button onClick={() => copy("all", allText)}>
                    {copied === "all" ? <Check className="size-4 text-ok" /> : <Copy className="size-4" />}
                    {t("Скопировать для сотрудников")}
                  </Button>
                  <Button onClick={downloadGuide}>
                    <Download className="size-4" />
                    {t("Скачать инструкцию")}
                  </Button>
                </div>
                <details className="rounded-lg border border-line px-3 py-2">
                  <summary className="cursor-pointer select-none text-sm text-ink-2">{t("Вход по логину и паролю")}</summary>
                  <div className="mt-3 space-y-3">
                    <p className="text-xs text-ink-3">
                      {t("Для Проводника. Word и Excel из Microsoft 365 по паролю файлы не открывают — для работы с документами подключайте диск по адресу без пароля.")}
                    </p>
                    <Field label={t("Адрес папки")} value={view.address ?? "—"} onCopy={view.address ? () => copy("address", view.address!) : undefined} copied={copied === "address"} />
                    <Field label={t("Логин")} value={view.username ?? "—"} onCopy={() => copy("username", view.username!)} copied={copied === "username"} />
                    <Field
                      label={t("Пароль")}
                      value={showPassword && password ? password : "••••••••••••••••••••"}
                      mono
                      onCopy={() => copy("password", revealPassword)}
                      copied={copied === "password"}
                      extra={
                        <button
                          type="button"
                          className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs text-ink-2 hover:bg-muted"
                          onClick={() => (showPassword ? setShowPassword(false) : run("reveal", async () => (await revealPassword(), setShowPassword(true))))}
                        >
                          {showPassword ? <EyeOff className="size-3.5" /> : <Eye className="size-3.5" />}
                          {showPassword ? t("Скрыть") : t("Показать")}
                        </button>
                      }
                    />
                  </div>
                </details>
              </div>
            )}

            {view.canManage && (
              <div className="space-y-3 border-t border-line pt-4">
                <p className="text-xs font-semibold uppercase tracking-wide text-ink-3">{t("Администратор")}</p>
                <div className="flex flex-wrap gap-2">
                  {!active && (
                    <Button variant="primary" disabled={!!busy} onClick={() => run("create", async () => (setView(await filesApi.createConnection(department.code)), setPassword(null)))}>
                      <KeyRound className="size-4" />
                      {t("Создать подключение")}
                    </Button>
                  )}
                  {active && (
                    <>
                      <Button
                        disabled={!!busy}
                        onClick={() =>
                          window.confirm(t("Выпустить новую ссылку? Старая перестанет работать сразу — на компьютерах отдела диск нужно будет подключить по новой.")) &&
                          run("regenerate-link", async () => (setView(await filesApi.regenerateLink(department.code)), setLink(null), setShowLink(false)))
                        }
                      >
                        <Link2 className="size-4" />
                        {t("Новая ссылка")}
                      </Button>
                      <Button
                        disabled={!!busy}
                        onClick={() =>
                          window.confirm(t("Сменить пароль? Старый перестанет работать сразу — на подключённых компьютерах нужно будет ввести новый.")) &&
                          run("regenerate", async () => (setView(await filesApi.regenerate(department.code)), setPassword(null), setShowPassword(false)))
                        }
                      >
                        <KeyRound className="size-4" />
                        {t("Сменить пароль")}
                      </Button>
                      <Button
                        disabled={!!busy}
                        onClick={() =>
                          window.confirm(t("Отозвать подключение? Все компьютеры отдела — и по ссылке, и по логину — сразу потеряют доступ к папке.")) &&
                          run("revoke", async () => (setView(await filesApi.revoke(department.code)), setPassword(null), setShowPassword(false), setLink(null), setShowLink(false)))
                        }
                      >
                        <ShieldOff className="size-4" />
                        {t("Отозвать")}
                      </Button>
                    </>
                  )}
                  <Button disabled={!!busy} onClick={() => run("test", async () => setTest(await filesApi.test(department.code)))}>
                    {busy === "test" ? <Loader2 className="size-4 animate-spin" /> : <Stethoscope className="size-4" />}
                    {t("Проверить подключение")}
                  </Button>
                </div>
                <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-xs text-ink-2">
                  {view.createdAt && (<><dt>{t("Создано")}</dt><dd>{modifiedLabel(view.createdAt, t)}</dd></>)}
                  {view.passwordChangedAt && (<><dt>{t("Пароль сменён")}</dt><dd>{modifiedLabel(view.passwordChangedAt, t)}</dd></>)}
                  {view.revokedAt && (<><dt>{t("Отозвано")}</dt><dd>{modifiedLabel(view.revokedAt, t)}</dd></>)}
                </dl>
                {test && (
                  <ul className="space-y-1.5 rounded-lg border border-line p-3 text-sm">
                    {test.steps.map((s) => (
                      <li key={s.name} className="flex gap-2">
                        <span className={s.ok ? "text-ok" : "text-bad"}>{s.ok ? "✓" : "✕"}</span>
                        <span>
                          <span className="font-medium">{t(s.name)}</span> — <span className="text-ink-2">{t(s.detail)}</span>
                        </span>
                      </li>
                    ))}
                  </ul>
                )}
                <DepartmentAccess department={department} />
              </div>
            )}
          </div>

          <div className="space-y-4">
            {active && view.canView && view.guide.length > 0 && (
              <ol className="space-y-2">
                {view.guide.map((step, i) => (
                  <li key={i} className="flex gap-3 text-sm">
                    <span className="flex size-6 shrink-0 items-center justify-center rounded-full bg-accent/10 text-xs font-semibold text-accent">{i + 1}</span>
                    <span className="pt-0.5">{t(step)}</span>
                  </li>
                ))}
              </ol>
            )}
            <div className="rounded-lg bg-muted/60 p-3">
              <p className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-ink-3">
                <HardDrive className="size-3.5" />
                {t("Если не получается")}
              </p>
              <ul className="space-y-1.5 text-xs text-ink-2">
                {view.notes.map((n) => (
                  <li key={n} className="break-words">{t(n)}</li>
                ))}
              </ul>
            </div>
          </div>
        </div>
      )}
    </Modal>
  );
}

function Field({
  label,
  value,
  onCopy,
  copied,
  mono = false,
  extra,
}: {
  label: string;
  value: string;
  onCopy?: () => void;
  copied: boolean;
  mono?: boolean;
  extra?: React.ReactNode;
}) {
  const t = useT();
  return (
    <div>
      <p className="mb-1 text-xs text-ink-3">{label}</p>
      <div className="flex items-center gap-2 rounded-lg border border-line bg-surface px-3 py-2">
        <span className={`min-w-0 flex-1 truncate text-sm ${mono ? "font-mono" : ""}`}>{value}</span>
        {extra}
        {onCopy && (
          <button type="button" onClick={onCopy} className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs text-ink-2 hover:bg-muted" aria-label={t("Скопировать: {label}", { label })}>
            {copied ? <Check className="size-3.5 text-ok" /> : <Copy className="size-3.5" />}
            {copied ? t("Скопировано") : t("Скопировать")}
          </button>
        )}
      </div>
    </div>
  );
}

/** Доступ других отделов к папке этого отдела (администратор). */
function DepartmentAccess({ department }: { department: DepartmentRef }) {
  const t = useT();
  const [rows, setRows] = useState<DepartmentAccessRow[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    filesApi.access(department.code).then(setRows, (e: Error) => setError(e.message));
  }, [department.code]);

  const change = async (row: DepartmentAccessRow, access: "none" | "read" | "write") => {
    setError(null);
    try {
      await filesApi.setAccess(department.code, row.id, access);
      setRows((list) => list?.map((r) => (r.id === row.id ? { ...r, access } : r)) ?? null);
    } catch (e) {
      setError((e as Error).message);
    }
  };

  return (
    <div className="space-y-2 pt-2">
      <p className="text-xs font-semibold uppercase tracking-wide text-ink-3">{t("Доступ других отделов к папке")}</p>
      <p className="text-xs text-ink-3">{t("Только на сайте и с их собственным логином: подключение Windows этого отдела им не выдаётся.")}</p>
      {error && <p className="text-xs text-bad">{error}</p>}
      <div className="divide-y divide-line rounded-lg border border-line">
        {rows?.map((row) => (
          <label key={row.id} className="flex items-center gap-3 px-3 py-2 text-sm">
            <span className="flex-1">{t(row.name)}</span>
            <select
              className="rounded-md border border-line bg-surface px-2 py-1 text-xs"
              value={row.access === "manage" ? "write" : row.access}
              onChange={(e) => change(row, e.target.value as "none" | "read" | "write")}
            >
              <option value="none">{t(accessLabel.none)}</option>
              <option value="read">{t(accessLabel.read)}</option>
              <option value="write">{t(accessLabel.write)}</option>
            </select>
          </label>
        ))}
      </div>
    </div>
  );
}
