"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { ArrowUpRight, Database, FileSpreadsheet, History, Loader2, Pencil, Plus, RefreshCw, Trash2 } from "lucide-react";
import { Modal } from "@/components/files/Modal";
import { BindingEditor } from "@/components/settings/BindingEditor";
import {
  dataSourcesApi,
  scheduleText,
  sheetsText,
  statusLabel,
  type BindingView,
  type ConnectionView,
  type DataSourcesOverview,
  type RunView,
  type SectionView,
} from "@/lib/data-sources";
import { dateTime } from "@/lib/sales/format";

const input = "h-9 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink focus:border-accent/60 focus:outline-none";
const secondary = "inline-flex h-8 items-center gap-1.5 rounded-lg border border-line px-2.5 text-xs font-medium text-ink-2 hover:bg-muted hover:text-ink disabled:opacity-50";
const primary = "inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-medium text-white hover:bg-accent-strong disabled:opacity-50";

/**
 * «Настройки → Источники данных»: подключения (Linko — факт, Google Sheets — таблицы), ниже — все разделы продаж: откуда факт и какие
 * поля OneBase раздела обновляются из листов Google Sheets, расписание, «Синхронизировать сейчас» и журнал синхронизаций.
 */
export function DataSourcesSettings({ initial }: { initial: DataSourcesOverview }) {
  const [data, setData] = useState(initial);
  const [connectionDialog, setConnectionDialog] = useState<ConnectionView | "new" | null>(null);
  const [editing, setEditing] = useState<{ section: SectionView; binding: BindingView | null } | null>(null);
  const [runsOf, setRunsOf] = useState<BindingView | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [notice, setNotice] = useState<{ ok: boolean; text: string } | null>(null);

  const reload = async () => setData(await dataSourcesApi.overview());
  const act = async (key: string, action: () => Promise<unknown>, done?: (r: unknown) => string | null) => {
    setBusy(key);
    setNotice(null);
    try {
      const result = await action();
      await reload();
      const text = done?.(result);
      if (text) setNotice({ ok: true, text });
    } catch (e) {
      setNotice({ ok: false, text: (e as Error).message });
    } finally {
      setBusy(null);
    }
  };

  // Поле → где уже обновляется (одно поле — один источник).
  const takenBy = (sectionKey: string, bindingId: string | null) => {
    const map: Record<string, string> = {};
    for (const s of data.sections) {
      for (const b of s.bindings) {
        if (b.id === bindingId) continue;
        for (const f of b.fields) map[f.field] = `${sheetsText(b.sheets)} (${s.key === sectionKey ? "этот раздел" : `раздел «${s.name}»`})`;
      }
    }
    return map;
  };

  return (
    <div className="space-y-8">
      {notice && (
        <div className={`rounded-lg px-4 py-2.5 text-sm ${notice.ok ? "bg-ok-soft text-ok" : "bg-bad-soft text-bad"}`} role="status">
          {notice.text}
        </div>
      )}
      {!data.canApply && (
        <div className="rounded-lg bg-warn-soft px-4 py-2.5 text-sm text-ink">
          Синхронизация записывает данные через AI Data Manager: чтобы настраивать и запускать привязки, нужно право «AI Data Manager».
        </div>
      )}

      <section>
        <div className="mb-3 flex flex-wrap items-end gap-3">
          <div className="min-w-0 flex-1">
            <h2 className="text-base font-semibold text-ink">Подключения</h2>
            <p className="text-sm text-ink-3">Откуда OneBase берёт данные: Linko — факт продаж, Google Sheets — таблицы с планами, дилерами и P&L.</p>
          </div>
          <button type="button" onClick={() => setConnectionDialog("new")} className={primary}>
            <Plus className="size-4" />
            Добавить Google Sheets
          </button>
        </div>
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          <div className="flex flex-col rounded-xl border border-line bg-surface p-4">
            <div className="flex items-center gap-3">
              <span className="grid size-10 place-items-center rounded-lg bg-accent-soft text-accent-strong">
                <Database className="size-5" />
              </span>
              <div className="min-w-0 flex-1">
                <div className="font-semibold text-ink">Linko</div>
                <div className="truncate text-xs text-ink-3">{data.linko.host ?? "адрес не задан"}</div>
              </div>
              <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${data.linko.configured ? "bg-ok-soft text-ok" : "bg-warn-soft text-warn"}`}>
                {data.linko.configured ? "подключено" : "не настроено"}
              </span>
            </div>
            <p className="mt-3 text-sm text-ink-2">Факт: заказы, возвраты, визиты, точки, остатки, перемещения, планы ТП. Только чтение.</p>
            <div className="mt-auto flex items-center justify-between gap-2 border-t border-line pt-3 text-xs text-ink-3">
              <span>{data.linko.lastSyncAt ? `Синхронизация: ${dateTime(data.linko.lastSyncAt)}` : "Ещё не синхронизировалось"}</span>
              <Link href="/settings/integrations/sales/linko" className="inline-flex items-center gap-1 font-medium text-accent-strong hover:underline">
                Настроить <ArrowUpRight className="size-3.5" />
              </Link>
            </div>
          </div>

          {data.connections.map((c) => (
            <div key={c.id} className="flex flex-col rounded-xl border border-line bg-surface p-4">
              <div className="flex items-center gap-3">
                <span className="grid size-10 place-items-center rounded-lg bg-ok-soft text-ok">
                  <FileSpreadsheet className="size-5" />
                </span>
                <div className="min-w-0 flex-1">
                  <div className="truncate font-semibold text-ink">{c.name}</div>
                  <a href={c.url} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 text-xs text-accent-strong hover:underline">
                    Google Sheets <ArrowUpRight className="size-3" />
                  </a>
                </div>
                <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${c.lastCheckOk ? "bg-ok-soft text-ok" : "bg-bad-soft text-bad"}`}>
                  {c.lastCheckOk ? "читается" : "нет доступа"}
                </span>
              </div>
              <div className="mt-3 space-y-1 text-xs text-ink-2">
                <div>{c.access === "link" ? "Доступ по ссылке" : `Сервисный аккаунт: ${c.serviceAccountEmail ?? "—"}`}</div>
                {c.lastCheckMessage && <div className={c.lastCheckOk ? "text-ink-3" : "text-bad"}>{c.lastCheckMessage}</div>}
                {c.sheets.length > 0 && <div className="text-ink-3">Листы: {c.sheets.map((s) => s.name).join(", ")}</div>}
              </div>
              <div className="mt-auto flex flex-wrap items-center gap-2 border-t border-line pt-3">
                <button type="button" className={secondary} disabled={busy === `test:${c.id}`} onClick={() => act(`test:${c.id}`, () => dataSourcesApi.testConnection(c.id), () => `«${c.name}»: проверено.`)}>
                  {busy === `test:${c.id}` ? <Loader2 className="size-3.5 animate-spin" /> : <RefreshCw className="size-3.5" />}
                  Проверить
                </button>
                <button type="button" className={secondary} onClick={() => setConnectionDialog(c)}>
                  <Pencil className="size-3.5" />
                  Изменить
                </button>
                <button
                  type="button"
                  className={`${secondary} hover:text-bad`}
                  disabled={busy === `del:${c.id}`}
                  onClick={() => confirm(`Удалить подключение «${c.name}»?`) && act(`del:${c.id}`, () => dataSourcesApi.deleteConnection(c.id), () => "Подключение удалено.")}
                >
                  <Trash2 className="size-3.5" />
                  Удалить
                </button>
                <span className="ml-auto text-xs text-ink-3">привязок: {c.bindings}</span>
              </div>
            </div>
          ))}
        </div>
      </section>

      <section>
        {data.monthPlans.some((p) => p.regions > 0 || p.previousRegions > 0) && (
          <div className="mb-4 rounded-xl border border-line bg-surface p-4">
            <div className="text-sm font-semibold text-ink">Планы на {data.monthPlans[0]?.period.toLowerCase()}</div>
            <p className="mt-0.5 text-xs text-ink-3">
              Факт Linko в новом месяце появляется сам. Планы ведёт OneBase: если план был в прошлом месяце, а на текущий его нет, в первые 10 дней месяца приходит
              уведомление тем, кто загружает планы.
            </p>
            <div className="mt-2.5 flex flex-wrap gap-2">
              {data.monthPlans
                .filter((p) => p.regions > 0 || p.previousRegions > 0)
                .map((p) => (
                  <span key={p.field} className={`rounded-lg px-2.5 py-1 text-xs font-medium ${p.regions > 0 ? "bg-ok-soft text-ok" : "bg-bad-soft text-bad"}`}>
                    {p.label}: {p.regions > 0 ? `загружен (регионов: ${p.regions})` : "ещё не загружен"}
                  </span>
                ))}
            </div>
          </div>
        )}
        <h2 className="text-base font-semibold text-ink">Разделы</h2>
        <p className="mb-3 text-sm text-ink-3">
          Для каждого раздела продаж: откуда факт и какие поля OneBase обновлять из Google Sheets. Обновляются только выбранные в привязке поля; каждая синхронизация —
          операция в журнале AI Data Manager (с проверкой и откатом).
        </p>
        <div className="grid gap-4 lg:grid-cols-2">
          {data.sections.map((s) => (
            <div key={s.key} className="rounded-xl border border-line bg-surface p-4">
              <div className="flex items-center gap-2">
                <Link href={s.href} className="font-semibold text-ink hover:text-accent-strong">
                  {s.name}
                </Link>
                {s.fields.length > 0 && (
                  <button
                    type="button"
                    className={`${secondary} ml-auto`}
                    disabled={data.connections.length === 0 || !data.canApply}
                    title={data.connections.length === 0 ? "Сначала добавьте таблицу Google Sheets" : undefined}
                    onClick={() => setEditing({ section: s, binding: null })}
                  >
                    <Plus className="size-3.5" />
                    Привязать Google Sheets
                  </button>
                )}
              </div>
              <div className="mt-2 flex items-start gap-2 text-sm text-ink-2">
                <span className="mt-0.5 shrink-0 rounded bg-accent-soft px-1.5 py-0.5 text-[11px] font-semibold text-accent-strong">Linko</span>
                <span>Факт: {s.facts}</span>
              </div>
              {s.fields.length === 0 ? (
                <p className="mt-2 text-xs text-ink-3">Все данные раздела — из Linko: полей, которые ведёт сам OneBase и которые можно брать из Google Sheets, здесь нет.</p>
              ) : (
                <div className="mt-2 text-xs text-ink-3">Из Google Sheets можно обновлять: {s.fields.map((f) => f.label).join(", ")}.</div>
              )}
              {s.boundElsewhere.map((b) => (
                <div key={b.field + b.bindingId} className="mt-1 text-xs text-ink-3">
                  «{b.label}» обновляется привязкой раздела «{b.sectionName}».
                </div>
              ))}

              {s.bindings.length > 0 && (
                <div className="mt-3 space-y-2">
                  {s.bindings.map((b) => (
                    <BindingRow
                      key={b.id}
                      binding={b}
                      section={s}
                      busy={busy}
                      canApply={data.canApply}
                      onSync={() =>
                        act(`sync:${b.id}`, () => dataSourcesApi.sync(b.id), (r) => {
                          const run = r as RunView;
                          return `${s.name}, ${sheetsText(b.sheets)}: ${statusLabel[run.status].label.toLowerCase()}. ${run.message ?? ""}`;
                        })
                      }
                      onEdit={() => setEditing({ section: s, binding: b })}
                      onDelete={() => confirm(`Удалить привязку (${sheetsText(b.sheets)})? Данные, уже записанные в OneBase, останутся.`) && act(`delb:${b.id}`, () => dataSourcesApi.deleteBinding(b.id), () => "Привязка удалена.")}
                      onRuns={() => setRunsOf(b)}
                    />
                  ))}
                </div>
              )}
            </div>
          ))}
        </div>
      </section>

      {connectionDialog && (
        <ConnectionDialog
          connection={connectionDialog === "new" ? null : connectionDialog}
          onClose={() => setConnectionDialog(null)}
          onSaved={async (c) => {
            setConnectionDialog(null);
            await reload();
            setNotice(c.lastCheckOk ? { ok: true, text: `«${c.name}»: таблица читается, листов ${c.sheets.length}.` } : { ok: false, text: `«${c.name}» сохранено, но ${c.lastCheckMessage ?? "таблица не читается"}` });
          }}
        />
      )}
      {editing && (
        <BindingEditor
          section={editing.section}
          binding={editing.binding}
          connections={data.connections}
          taken={takenBy(editing.section.key, editing.binding?.id ?? null)}
          onClose={() => setEditing(null)}
          onSaved={async () => {
            setEditing(null);
            await reload();
            setNotice({ ok: true, text: "Привязка сохранена. Нажмите «Синхронизировать сейчас», чтобы обновить данные сразу." });
          }}
        />
      )}
      {runsOf && <RunsDialog binding={runsOf} onClose={() => setRunsOf(null)} />}
    </div>
  );
}

function BindingRow({
  binding: b,
  section,
  busy,
  canApply,
  onSync,
  onEdit,
  onDelete,
  onRuns,
}: {
  binding: BindingView;
  section: SectionView;
  busy: string | null;
  canApply: boolean;
  onSync: () => void;
  onEdit: () => void;
  onDelete: () => void;
  onRuns: () => void;
}) {
  const syncing = busy === `sync:${b.id}` || b.running;
  const status = b.lastStatus ? statusLabel[b.lastStatus] : null;
  return (
    <div className={`rounded-lg border border-line p-3 ${b.enabled ? "" : "opacity-60"}`}>
      <div className="flex flex-wrap items-center gap-2">
        <FileSpreadsheet className="size-4 shrink-0 text-ok" />
        <span className="min-w-0 text-sm font-medium text-ink">
          {b.connectionName} → {b.sheets.includes("*") ? "все листы" : b.sheets.length === 1 ? `лист «${b.sheets[0]}»` : `листы: ${sheetsText(b.sheets)}`}
        </span>
        {status && <span className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${status.className}`}>{status.label}</span>}
        {!b.enabled && <span className="rounded-full bg-muted px-2 py-0.5 text-[11px] text-ink-3">выключена</span>}
      </div>
      <div className="mt-1.5 flex flex-wrap gap-1.5">
        {b.fields.map((f) => (
          <span key={f.field} className="rounded-md bg-accent-soft px-2 py-0.5 text-xs text-accent-strong">
            {section.fields.find((x) => x.key === f.field)?.label ?? f.field}
          </span>
        ))}
      </div>
      <div className="mt-2 text-xs text-ink-3">
        Синхронизация: {scheduleText(b)}
        {b.lastRunAt && ` · последняя ${dateTime(b.lastRunAt)}`}
        {b.enabled && b.nextRunAt && ` · следующая ${dateTime(b.nextRunAt)}`}
      </div>
      {b.lastMessage && <div className={`mt-1 text-xs ${b.lastStatus === "error" || b.lastStatus === "conflicts" ? "text-bad" : "text-ink-2"}`}>{b.lastMessage}</div>}
      <div className="mt-2.5 flex flex-wrap gap-2">
        <button type="button" onClick={onSync} disabled={syncing || !canApply} className={secondary}>
          {syncing ? <Loader2 className="size-3.5 animate-spin" /> : <RefreshCw className="size-3.5" />}
          Синхронизировать сейчас
        </button>
        <button type="button" onClick={onRuns} className={secondary}>
          <History className="size-3.5" />
          Журнал
        </button>
        <button type="button" onClick={onEdit} disabled={!canApply} className={secondary}>
          <Pencil className="size-3.5" />
          Изменить
        </button>
        <button type="button" onClick={onDelete} className={`${secondary} hover:text-bad`}>
          <Trash2 className="size-3.5" />
          Удалить
        </button>
      </div>
    </div>
  );
}

/** Таблица Google: название, ссылка, доступ — по ссылке или сервисным аккаунтом (JSON-ключ хранится зашифрованным и не показывается). */
function ConnectionDialog({ connection, onClose, onSaved }: { connection: ConnectionView | null; onClose: () => void; onSaved: (c: ConnectionView) => void }) {
  const [name, setName] = useState(connection?.name ?? "");
  const [url, setUrl] = useState(connection?.url ?? "");
  const [access, setAccess] = useState<"link" | "service_account">(connection?.access ?? "link");
  const [key, setKey] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      const body = { name, url, access, serviceAccountKey: key.trim() || null };
      onSaved(connection ? await dataSourcesApi.updateConnection(connection.id, body) : await dataSourcesApi.createConnection(body));
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal title={connection ? `Таблица «${connection.name}»` : "Новая таблица Google Sheets"} onClose={onClose}>
      <div className="space-y-4">
        <div>
          <label className="mb-1 block text-xs font-medium text-ink-2">Название</label>
          <input value={name} onChange={(e) => setName(e.target.value)} placeholder="План первички 2026" className={input} />
        </div>
        <div>
          <label className="mb-1 block text-xs font-medium text-ink-2">Ссылка на таблицу</label>
          <input value={url} onChange={(e) => setUrl(e.target.value)} placeholder="https://docs.google.com/spreadsheets/d/…" className={input} />
        </div>
        <fieldset className="space-y-2">
          <legend className="mb-1 text-xs font-medium text-ink-2">Доступ</legend>
          <label className="flex items-start gap-2 text-sm text-ink">
            <input type="radio" checked={access === "link"} onChange={() => setAccess("link")} className="mt-1 accent-[var(--color-accent)]" />
            <span>
              По ссылке
              <span className="block text-xs text-ink-3">В Google Sheets: «Настройки доступа» → «Все, у кого есть ссылка» → «Читатель».</span>
            </span>
          </label>
          <label className="flex items-start gap-2 text-sm text-ink">
            <input type="radio" checked={access === "service_account"} onChange={() => setAccess("service_account")} className="mt-1 accent-[var(--color-accent)]" />
            <span>
              Сервисный аккаунт Google — для закрытых таблиц
              <span className="block text-xs text-ink-3">
                Google Cloud → IAM → Сервисные аккаунты → ключ JSON; включите Google Drive API. Поделитесь таблицей с почтой аккаунта (читатель).
              </span>
            </span>
          </label>
        </fieldset>
        {access === "service_account" && (
          <div>
            <label className="mb-1 block text-xs font-medium text-ink-2">
              Ключ JSON{connection?.serviceAccountEmail ? ` (сохранён: ${connection.serviceAccountEmail}; вставьте новый, чтобы заменить)` : ""}
            </label>
            <textarea
              value={key}
              onChange={(e) => setKey(e.target.value)}
              rows={5}
              spellCheck={false}
              placeholder='{"type": "service_account", "client_email": "…", "private_key": "…"}'
              className="w-full rounded-lg border border-line bg-surface px-3 py-2 font-mono text-xs text-ink focus:border-accent/60 focus:outline-none"
            />
            <p className="mt-1 text-xs text-ink-3">Ключ хранится на сервере зашифрованным и в браузер больше не показывается.</p>
          </div>
        )}
        {error && <div className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</div>}
        <div className="flex justify-end gap-2 border-t border-line pt-4">
          <button type="button" onClick={onClose} className="h-9 rounded-lg border border-line px-4 text-sm text-ink hover:bg-muted">
            Отмена
          </button>
          <button type="button" onClick={save} disabled={saving || !name.trim() || !url.trim()} className={primary}>
            {saving && <Loader2 className="size-4 animate-spin" />}
            Сохранить и проверить
          </button>
        </div>
      </div>
    </Modal>
  );
}

/** Журнал синхронизаций привязки: когда, кем или по расписанию, итог по каждому полю и ошибки строк. */
function RunsDialog({ binding, onClose }: { binding: BindingView; onClose: () => void }) {
  const [runs, setRuns] = useState<RunView[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    let cancelled = false;
    dataSourcesApi
      .runs(binding.id)
      .then((r) => !cancelled && setRuns(r))
      .catch((e: Error) => !cancelled && setError(e.message));
    return () => {
      cancelled = true;
    };
  }, [binding.id]);

  return (
    <Modal title={`Журнал: ${binding.connectionName} — ${sheetsText(binding.sheets)}`} onClose={onClose} wide>
      {error ? (
        <div className="text-sm text-bad">{error}</div>
      ) : !runs ? (
        <div className="flex items-center gap-2 text-sm text-ink-3">
          <Loader2 className="size-4 animate-spin" />
          Загружаю…
        </div>
      ) : runs.length === 0 ? (
        <div className="text-sm text-ink-3">Синхронизаций ещё не было.</div>
      ) : (
        <div className="space-y-3">
          {runs.map((r) => (
            <div key={r.id} className="rounded-lg border border-line p-3">
              <div className="flex flex-wrap items-center gap-2 text-sm">
                <span className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${statusLabel[r.status].className}`}>{statusLabel[r.status].label}</span>
                <span className="text-ink">{dateTime(r.startedAt)}</span>
                <span className="text-xs text-ink-3">{r.trigger === "schedule" ? "по расписанию" : "вручную"}{r.userName ? ` · ${r.userName}` : ""}</span>
              </div>
              {r.fields.length === 0 && r.message && <div className="mt-1.5 text-sm text-bad">{r.message}</div>}
              {r.fields.map((f) => (
                <div key={f.field} className="mt-1.5 text-sm">
                  <span className="font-medium text-ink">{f.label}:</span> <span className={f.status === "error" || f.status === "conflicts" ? "text-bad" : "text-ink-2"}>{f.message}</span>
                  {f.changeSetId && f.status === "ok" && (
                    <Link href="/data-manager" className="ml-2 text-xs text-accent-strong hover:underline">
                      операция в журнале AI Data Manager
                    </Link>
                  )}
                  {f.problems?.map((p) => (
                    <div key={p} className="ml-3 text-xs text-ink-3">
                      — {p}
                    </div>
                  ))}
                </div>
              ))}
            </div>
          ))}
        </div>
      )}
    </Modal>
  );
}
