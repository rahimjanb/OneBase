"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import {
  CalendarClock,
  Copy,
  Download,
  FileSpreadsheet,
  Loader2,
  Paperclip,
  Pause,
  Play,
  RefreshCw,
  RotateCcw,
  Search,
  Send,
  Trash2,
  Upload,
  X,
} from "lucide-react";
import { button, danger, field, primary } from "@/components/ai/form";
import {
  active,
  attachmentUrl,
  campaignsApi,
  campaignStatus,
  editable,
  fromLocal,
  recipientStatus,
  size,
  tashkent,
  toLocal,
  uploadCampaignFile,
  type Campaign,
  type CampaignAccount,
  type CampaignChat,
  type CampaignInput,
  type CampaignPreview,
  type UserReport,
} from "@/lib/campaigns";

const typeLabel = { Private: "Личный", Group: "Группа", Channel: "Канал" } as const;

const inputOf = (c: Campaign): CampaignInput => ({
  accountId: c.accountId,
  name: c.name,
  description: c.description,
  text: c.text,
  formatted: c.formatted,
  scheduledAt: c.scheduledAt,
  delaySeconds: c.delaySeconds,
});

/**
 * Рассылка. Черновик — одна страница: слева текст (файлы — перетащить сюда), справа чаты и группы аккаунта галочками (куда писать нельзя —
 * серым, с причиной), внизу — тест и отправка. Изменения сохраняются сами. После запуска — что отправлено, файлы, статус каждого получателя.
 */
export function CampaignEditor({ initial, accounts }: { initial: Campaign; accounts: CampaignAccount[] }) {
  const router = useRouter();
  const [c, setC] = useState(initial);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(async () => setC(await campaignsApi.get(initial.id)), [initial.id]);

  useEffect(() => {
    if (!active(c.status)) return;
    const t = setInterval(() => reload().catch(() => undefined), 5000);
    return () => clearInterval(t);
  }, [c.status, reload]);

  const run = async (what: string, action: () => Promise<void>) => {
    setBusy(what);
    setError(null);
    try {
      await action();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(null);
    }
  };

  const s = campaignStatus[c.status];
  const done = c.counts.sent + c.counts.failed + c.counts.cancelled;
  const sent = !editable(c.status);

  return (
    <>
      <section className="rounded-xl border border-line bg-surface p-4 sm:p-5">
        <div className="flex flex-wrap items-center gap-3">
          <span className={`rounded-full px-2.5 py-1 text-xs font-medium ${s.className}`}>{s.label}</span>
          {c.status === "Scheduled" && <span className="text-sm text-ink-2">отправка {tashkent(c.scheduledAt)} (Ташкент)</span>}
          {sent && c.counts.total > 0 && (
            <div className="min-w-[220px] flex-1">
              <div className="h-2 overflow-hidden rounded-full bg-muted">
                <div className="h-full bg-accent transition-all" style={{ width: `${Math.round((done / c.counts.total) * 100)}%` }} />
              </div>
              <span className="mt-1 block text-xs text-ink-2">
                Отправлено {c.counts.sent} из {c.counts.total}
                {c.counts.pending > 0 && ` · ждут ${c.counts.pending}`}
                {c.counts.failed > 0 && <span className="text-bad"> · ошибок {c.counts.failed}</span>}
                {c.counts.cancelled > 0 && ` · отменено ${c.counts.cancelled}`}
              </span>
            </div>
          )}
          <div className="ml-auto flex flex-wrap gap-2">
            {(c.status === "Queued" || c.status === "Sending" || c.status === "Scheduled") && (
              <button className={button} disabled={!!busy} onClick={() => run("pause", async () => setC(await campaignsApi.pause(c.id)))}>
                <Pause className="size-4" />
                Пауза
              </button>
            )}
            {c.status === "Paused" && (
              <button className={primary} disabled={!!busy} onClick={() => run("resume", async () => setC(await campaignsApi.resume(c.id)))}>
                <Play className="size-4" />
                Продолжить
              </button>
            )}
            {(c.status === "CompletedWithErrors" || c.status === "Failed" || (c.status === "Paused" && c.counts.failed > 0)) && (
              <button className={button} disabled={!!busy} onClick={() => run("retry", async () => setC(await campaignsApi.retry(c.id)))}>
                <RotateCcw className="size-4" />
                Повторить ошибки ({c.counts.failed})
              </button>
            )}
            {sent && (
              <button
                className={button}
                disabled={!!busy}
                title="Новый черновик с тем же текстом, файлами и получателями"
                onClick={() => run("copy", async () => router.push(`/campaigns/${(await campaignsApi.duplicate(c.id)).id}`))}
              >
                {busy === "copy" ? <Loader2 className="size-4 animate-spin" /> : <Copy className="size-4" />}
                Повторить как новую
              </button>
            )}
            {["Scheduled", "Queued", "Sending", "Paused"].includes(c.status) && (
              <button
                className={danger}
                disabled={!!busy}
                onClick={() => {
                  if (confirm("Отменить рассылку? Неотправленным сообщение не уйдёт; отправленные останутся у получателей.")) {
                    run("cancel", async () => setC(await campaignsApi.cancel(c.id)));
                  }
                }}
              >
                <X className="size-4" />
                Отменить
              </button>
            )}
            {(c.status === "Draft" || c.status === "Cancelled") && c.counts.sent === 0 && (
              <button
                className={danger}
                disabled={!!busy}
                onClick={() => {
                  if (confirm(`Удалить рассылку «${c.name}»?`)) {
                    run("delete", async () => {
                      await campaignsApi.remove(c.id);
                      router.push("/campaigns");
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
        {c.lastError && <p className="mt-3 rounded-lg bg-warn-soft px-3 py-2 text-sm text-warn">{c.lastError}</p>}
        {error && <p className="mt-3 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
      </section>

      {editable(c.status) ? <Composer c={c} accounts={accounts} onChange={setC} /> : <Details c={c} />}
    </>
  );
}

// ---------- Черновик ----------

function Composer({ c, accounts, onChange }: { c: Campaign; accounts: CampaignAccount[]; onChange: (c: Campaign) => void }) {
  return (
    <>
      <div className="mt-5 grid gap-5 xl:grid-cols-[minmax(0,1fr)_420px]">
        <MessageCard c={c} accounts={accounts} onChange={onChange} />
        <RecipientsCard c={c} onChange={onChange} />
      </div>
      <SendBar c={c} onChange={onChange} />
    </>
  );
}

/** Текст (сохраняется сам), файлы — перетаскиванием или кнопкой, отчёты консультанта, предпросмотр. */
function MessageCard({ c, accounts, onChange }: { c: Campaign; accounts: CampaignAccount[]; onChange: (c: Campaign) => void }) {
  const [form, setForm] = useState<CampaignInput>(inputOf(c));
  const [saving, setSaving] = useState<"idle" | "saving" | "saved">("idle");
  const [uploading, setUploading] = useState<string[]>([]);
  const [dragging, setDragging] = useState(false);
  const [reports, setReports] = useState<UserReport[] | null>(null);
  const [reportId, setReportId] = useState("");
  const [preview, setPreview] = useState<CampaignPreview | null>(null);
  const [error, setError] = useState<string | null>(null);
  const saved = useRef(JSON.stringify(inputOf(c)));
  const fileInput = useRef<HTMLInputElement>(null);

  useEffect(() => {
    campaignsApi.reports().then(setReports, () => setReports([]));
  }, []);

  // Автосохранение: через 0,8 с после последней правки.
  useEffect(() => {
    const json = JSON.stringify(form);
    if (json === saved.current || !form.name.trim() || (form.text?.length ?? 0) > 4096) return;
    setSaving("saving");
    const t = setTimeout(async () => {
      try {
        const next = await campaignsApi.update(c.id, form);
        saved.current = json;
        onChange(next);
        setSaving("saved");
        setError(null);
      } catch (e) {
        setError((e as Error).message);
        setSaving("idle");
      }
    }, 800);
    return () => clearTimeout(t);
  }, [form, c.id, onChange]);

  useEffect(() => {
    const t = setTimeout(() => campaignsApi.preview(c.id).then(setPreview, () => undefined), 300);
    return () => clearTimeout(t);
  }, [c]);

  const changeAccount = (accountId: string) => {
    if (c.counts.total > 0 && !confirm("Сменить аккаунт? Получатели очистятся — у другого аккаунта другие чаты.")) return;
    setForm((f) => ({ ...f, accountId }));
  };

  const upload = async (files: FileList | File[]) => {
    const list = Array.from(files);
    if (list.length === 0) return;
    setError(null);
    setUploading(list.map((f) => f.name));
    try {
      for (const file of list) {
        await uploadCampaignFile(c.id, file);
      }
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setUploading([]);
      onChange(await campaignsApi.get(c.id));
    }
  };

  const textLength = form.text?.length ?? 0;

  return (
    <section className="rounded-xl border border-line bg-surface p-4 sm:p-5">
      <div className="grid gap-3 sm:grid-cols-[1fr_240px]">
        <input
          className={`${field} text-base font-semibold`}
          value={form.name}
          onChange={(e) => setForm({ ...form, name: e.target.value })}
          maxLength={200}
          aria-label="Название рассылки"
        />
        <select className={field} value={form.accountId} onChange={(e) => changeAccount(e.target.value)} aria-label="Аккаунт Telegram">
          {accounts.map((a) => (
            <option key={a.id} value={a.id}>
              {a.name}
              {a.username ? ` (@${a.username})` : ""}
              {a.status !== "Connected" ? " — не подключён" : ""}
            </option>
          ))}
        </select>
      </div>

      <div
        className={`relative mt-3 rounded-xl border-2 border-dashed transition-colors ${dragging ? "border-accent bg-accent-soft/30" : "border-transparent"}`}
        onDragOver={(e) => {
          if (e.dataTransfer.types.includes("Files")) {
            e.preventDefault();
            setDragging(true);
          }
        }}
        onDragLeave={(e) => {
          if (!e.currentTarget.contains(e.relatedTarget as Node)) setDragging(false);
        }}
        onDrop={(e) => {
          e.preventDefault();
          setDragging(false);
          upload(e.dataTransfer.files);
        }}
      >
        <textarea
          className={`${field} h-56 resize-y py-3 leading-relaxed`}
          value={form.text ?? ""}
          onChange={(e) => setForm({ ...form, text: e.target.value || null })}
          placeholder="Текст сообщения. Файлы — перетащите сюда или нажмите «Прикрепить файл»."
        />
        {dragging && (
          <div className="pointer-events-none absolute inset-0 grid place-items-center rounded-xl bg-surface-solid/80 text-sm font-medium text-accent-strong">
            <span className="flex items-center gap-2">
              <Upload className="size-5" />
              Отпустите — файлы прикрепятся к рассылке
            </span>
          </div>
        )}
      </div>

      <div className="mt-2 flex flex-wrap items-center justify-between gap-2 text-xs text-ink-3">
        <span>
          {saving === "saving" ? "Сохраняется…" : saving === "saved" ? "Сохранено" : "Изменения сохраняются сами"}
          {" · "}
          <span className={textLength > 4096 ? "text-bad" : ""}>{textLength} / 4096</span>
        </span>
        <label className="flex items-center gap-2 text-ink-2">
          <input type="checkbox" checked={form.formatted} onChange={(e) => setForm({ ...form, formatted: e.target.checked })} className="size-4 accent-[var(--color-accent)]" />
          Разметка: **жирный**, *курсив*, [ссылка](https://…)
        </label>
      </div>

      {(c.attachments.length > 0 || uploading.length > 0) && (
        <div className="mt-3 flex flex-wrap gap-2">
          {c.attachments.map((a) => (
            <span key={a.id} className="flex max-w-full items-center gap-2 rounded-lg border border-line bg-muted/40 px-2.5 py-1.5 text-sm">
              {a.kind === "report" ? <FileSpreadsheet className="size-4 shrink-0 text-ok" /> : <Paperclip className="size-4 shrink-0 text-ink-3" />}
              <a href={attachmentUrl(c.id, a.id)} className="truncate text-ink hover:underline">
                {a.fileName}
              </a>
              <span className="shrink-0 text-xs text-ink-3">{size(a.sizeBytes)}</span>
              <button
                className="shrink-0 text-ink-3 hover:text-bad"
                title="Убрать"
                onClick={async () => {
                  await campaignsApi.removeAttachment(c.id, a.id);
                  onChange(await campaignsApi.get(c.id));
                }}
              >
                <X className="size-4" />
              </button>
            </span>
          ))}
          {uploading.map((n) => (
            <span key={n} className="flex items-center gap-2 rounded-lg border border-dashed border-line px-2.5 py-1.5 text-sm text-ink-3">
              <Loader2 className="size-4 animate-spin" />
              {n}
            </span>
          ))}
        </div>
      )}

      <div className="mt-3 flex flex-wrap items-center gap-2">
        <input
          ref={fileInput}
          type="file"
          multiple
          className="hidden"
          onChange={(e) => {
            const files = e.target.files ? Array.from(e.target.files) : [];
            e.target.value = "";
            upload(files);
          }}
        />
        <button className={button} disabled={uploading.length > 0 || c.attachments.length >= 10} onClick={() => fileInput.current?.click()}>
          <Paperclip className="size-4" />
          Прикрепить файл
        </button>
        <select className={field.replace("w-full", "w-auto max-w-[260px]")} value={reportId} onChange={(e) => setReportId(e.target.value)} aria-label="Отчёт консультанта">
          <option value="">{reports === null ? "Загрузка отчётов…" : reports.length === 0 ? "Отчётов консультанта нет" : "Отчёт консультанта…"}</option>
          {reports?.map((r) => (
            <option key={r.id} value={r.id}>
              {r.title} · {tashkent(r.createdAt)}
            </option>
          ))}
        </select>
        {reportId && (
          <button
            className={button}
            onClick={async () => {
              try {
                await campaignsApi.addReport(c.id, reportId);
                setReportId("");
                onChange(await campaignsApi.get(c.id));
              } catch (e) {
                setError((e as Error).message);
              }
            }}
          >
            <FileSpreadsheet className="size-4" />
            Добавить отчёт
          </button>
        )}
        <span className="text-xs text-ink-3">до 10 файлов по 50 МБ; отчёт фиксируется файлом Excel в момент добавления</span>
      </div>

      {error && <p className="mt-3 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}

      {preview?.html && (
        <details className="mt-4">
          <summary className="cursor-pointer text-sm font-medium text-ink-2 hover:text-ink">Как увидит получатель</summary>
          <div className="mt-2 rounded-xl bg-muted/50 p-3">
            <div
              className="whitespace-pre-wrap break-words text-sm text-ink [&_a]:text-accent-strong [&_a]:underline [&_pre]:overflow-x-auto [&_pre]:text-xs"
              dangerouslySetInnerHTML={{ __html: preview.html }}
            />
          </div>
        </details>
      )}
    </section>
  );
}

/** Чаты и группы аккаунта галочками: отметил — получатель добавлен; куда писать нельзя — серым, с причиной. */
function RecipientsCard({ c, onChange }: { c: Campaign; onChange: (c: Campaign) => void }) {
  const [chats, setChats] = useState<CampaignChat[] | null>(null);
  const [search, setSearch] = useState("");
  const [type, setType] = useState("");
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      setChats(await campaignsApi.chats(c.id, search, type));
    } catch (e) {
      setError((e as Error).message);
    }
  }, [c.id, search, type]);

  useEffect(() => {
    const t = setTimeout(load, 200);
    return () => clearTimeout(t);
  }, [load, c.accountId]);

  const toggle = async (chat: CampaignChat) => {
    setBusy(chat.id);
    setError(null);
    try {
      if (chat.added) {
        const r = c.recipients.find((x) => x.chatId === chat.id);
        if (r) await campaignsApi.removeRecipient(c.id, r.id);
      } else {
        const result = await campaignsApi.addRecipients(c.id, [chat.id]);
        if (result.skipped.length > 0) setError(`${result.skipped[0].title}: ${result.skipped[0].reason}`);
      }
      setChats((list) => list?.map((x) => (x.id === chat.id ? { ...x, added: !x.added } : x)) ?? null);
      onChange(await campaignsApi.get(c.id));
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(null);
    }
  };

  const available = (chats ?? []).filter((x) => x.canSend && !x.added);
  const allSelected = (chats ?? []).filter((x) => x.canSend).length > 0 && available.length === 0;

  const selectAll = async () => {
    if (allSelected && !confirm("Убрать всех получателей?")) return;
    setBusy("all");
    setError(null);
    try {
      if (allSelected) await campaignsApi.clearRecipients(c.id);
      else await campaignsApi.addRecipients(c.id, available.map((x) => x.id));
      onChange(await campaignsApi.get(c.id));
      await load();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(null);
    }
  };

  return (
    <section className="flex max-h-[720px] flex-col rounded-xl border border-line bg-surface p-4 sm:p-5">
      <div className="flex items-center justify-between gap-2">
        <h2 className="text-base font-semibold text-ink">
          Получатели <span className="text-sm font-normal text-ink-3">· выбрано {c.counts.total}</span>
        </h2>
        <button
          className="rounded-lg p-2 text-ink-3 hover:bg-muted hover:text-ink"
          title="Обновить чаты из Telegram"
          disabled={!!busy}
          onClick={async () => {
            setBusy("sync");
            try {
              await campaignsApi.syncChats(c.id);
              await load();
            } catch (e) {
              setError((e as Error).message);
            } finally {
              setBusy(null);
            }
          }}
        >
          {busy === "sync" ? <Loader2 className="size-4 animate-spin" /> : <RefreshCw className="size-4" />}
        </button>
      </div>

      <div className="relative mt-3">
        <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" />
        <input className={`${field} pl-9`} value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Чат, группа или @username" />
      </div>
      <div className="mt-2 flex flex-wrap gap-1">
        {[
          ["", "Все"],
          ["Private", "Личные"],
          ["Group", "Группы"],
          ["Channel", "Каналы"],
        ].map(([key, label]) => (
          <button
            key={key}
            onClick={() => setType(key)}
            className={`rounded-full px-3 py-1 text-xs ${type === key ? "bg-accent-soft font-medium text-accent-strong" : "text-ink-2 hover:bg-muted"}`}
          >
            {label}
          </button>
        ))}
      </div>

      <div className="mt-2 flex items-center justify-between text-xs">
        <button className="text-accent-strong hover:underline disabled:opacity-50" disabled={!!busy || (available.length === 0 && !allSelected)} onClick={selectAll}>
          {allSelected ? "Снять все" : `Выбрать все доступные${search || type ? " в списке" : ""} (${available.length})`}
        </button>
        {busy === "all" && <Loader2 className="size-4 animate-spin text-ink-3" />}
      </div>

      {error && <p className="mt-2 rounded-lg bg-warn-soft px-3 py-2 text-xs text-warn">{error}</p>}

      <div className="mt-2 min-h-0 flex-1 overflow-y-auto rounded-lg border border-line">
        {chats === null ? (
          <Loader2 className="m-3 size-5 animate-spin text-ink-3" />
        ) : chats.length === 0 ? (
          <p className="p-3 text-sm text-ink-3">Чатов нет — обновите их из Telegram (кнопка справа вверху).</p>
        ) : (
          chats.map((x) => (
            <label
              key={x.id}
              className={`flex items-start gap-3 border-b border-line px-3 py-2.5 text-sm last:border-b-0 ${
                x.canSend ? "cursor-pointer hover:bg-muted/40" : "cursor-not-allowed opacity-55"
              }`}
              title={x.reason ?? undefined}
            >
              {busy === x.id ? (
                <Loader2 className="mt-0.5 size-4 shrink-0 animate-spin text-ink-3" />
              ) : (
                <input
                  type="checkbox"
                  disabled={!x.canSend || !!busy}
                  checked={x.added}
                  onChange={() => toggle(x)}
                  className="mt-0.5 size-4 shrink-0 accent-[var(--color-accent)]"
                />
              )}
              <span className="min-w-0 flex-1">
                <span className={`block truncate ${x.canSend ? "text-ink" : "text-ink-3"}`}>{x.title}</span>
                <span className="block text-xs text-ink-3">
                  {typeLabel[x.type]}
                  {x.username && ` · @${x.username}`}
                </span>
                {!x.canSend && <span className="mt-0.5 block text-xs text-ink-3">Нельзя писать: {x.reason ?? "нет доступа"}</span>}
              </span>
            </label>
          ))
        )}
      </div>
    </section>
  );
}

/** Тест в один чат, отправка сейчас или по расписанию. */
function SendBar({ c, onChange }: { c: Campaign; onChange: (c: Campaign) => void }) {
  const [testChat, setTestChat] = useState("");
  const [testChats, setTestChats] = useState<CampaignChat[]>([]);
  const [schedule, setSchedule] = useState(toLocal(c.scheduledAt));
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    campaignsApi.chats(c.id).then((list) => setTestChats(list.filter((x) => x.canSend)), () => undefined);
  }, [c.id, c.accountId]);

  const empty = !c.text && c.attachments.length === 0;

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

  const launch = (scheduled: boolean) =>
    run("launch", async () => {
      const at = scheduled ? fromLocal(schedule) : null;
      if (scheduled && !at) throw new Error("Укажите дату и время отправки.");
      const question = scheduled
        ? `Запланировать рассылку на ${schedule.replace("T", " ")} (Ташкент) для ${c.counts.total} получателей?`
        : `Отправить сейчас ${c.counts.total} получателям?`;
      if (!confirm(question)) return;
      if (at !== c.scheduledAt) await campaignsApi.update(c.id, { ...inputOf(c), scheduledAt: at });
      onChange(await campaignsApi.launch(c.id));
    });

  return (
    <section className="mt-5 rounded-xl border border-line bg-surface p-4 sm:p-5">
      <div className="flex flex-wrap items-end gap-x-6 gap-y-4">
        <div className="min-w-[260px] flex-1">
          <span className="mb-1.5 block text-sm font-medium text-ink">Тест — отправить себе или коллеге</span>
          <div className="flex gap-2">
            <select className={field} value={testChat} onChange={(e) => setTestChat(e.target.value)} aria-label="Чат для теста">
              <option value="">Выберите чат…</option>
              {testChats.map((x) => (
                <option key={x.id} value={x.id}>
                  {x.title}
                  {x.username ? ` (@${x.username})` : ""}
                </option>
              ))}
            </select>
            <button
              className={button}
              disabled={!!busy || !testChat || empty}
              onClick={() =>
                run("test", async () => {
                  const r = await campaignsApi.test(c.id, testChat);
                  setMessage(`Тест отправлен: ${r.sentTo}.`);
                })
              }
            >
              {busy === "test" ? <Loader2 className="size-4 animate-spin" /> : <Send className="size-4" />}
              Тест
            </button>
          </div>
        </div>
        <div className="flex flex-wrap items-end gap-2">
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">По расписанию (Ташкент)</span>
            <input className={field.replace("w-full", "w-auto")} type="datetime-local" value={schedule} onChange={(e) => setSchedule(e.target.value)} />
          </label>
          <button className={button} disabled={!!busy || !schedule || c.counts.total === 0 || empty} onClick={() => launch(true)}>
            <CalendarClock className="size-4" />
            Запланировать
          </button>
          <button className={`${primary} h-10 px-5`} disabled={!!busy || c.counts.total === 0 || empty} onClick={() => launch(false)}>
            {busy === "launch" ? <Loader2 className="size-4 animate-spin" /> : <Send className="size-4" />}
            Отправить сейчас · {c.counts.total}
          </button>
        </div>
      </div>
      {(empty || c.counts.total === 0) && (
        <p className="mt-3 text-xs text-ink-3">{empty ? "Напишите текст или прикрепите файл." : "Отметьте получателей справа."}</p>
      )}
      {message && <p className="mt-3 rounded-lg bg-ok-soft px-3 py-2 text-sm text-ok">{message}</p>}
      {error && <p className="mt-3 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
    </section>
  );
}

// ---------- После запуска ----------

/** Что отправлено (текст как у получателя, файлы — скачать) и статус каждого получателя. */
function Details({ c }: { c: Campaign }) {
  const [preview, setPreview] = useState<CampaignPreview | null>(null);
  const [filter, setFilter] = useState("");

  useEffect(() => {
    campaignsApi.preview(c.id).then(setPreview, () => undefined);
  }, [c.id]);

  const rows = filter ? c.recipients.filter((r) => r.status === filter) : c.recipients;

  return (
    <div className="mt-5 grid gap-5 xl:grid-cols-[minmax(0,1fr)_minmax(0,1.2fr)]">
      <section className="rounded-xl border border-line bg-surface p-4 sm:p-5">
        <h2 className="text-base font-semibold text-ink">Отправленные данные</h2>
        <p className="mt-0.5 text-xs text-ink-3">
          {c.accountName}
          {c.startedAt && ` · начата ${tashkent(c.startedAt)}`}
          {c.finishedAt && ` · завершена ${tashkent(c.finishedAt)}`}
          {c.createdBy && ` · ${c.createdBy}`}
        </p>
        <div className="mt-3 rounded-xl bg-muted/50 p-3">
          {preview?.html ? (
            <div
              className="whitespace-pre-wrap break-words text-sm text-ink [&_a]:text-accent-strong [&_a]:underline [&_pre]:overflow-x-auto [&_pre]:text-xs"
              dangerouslySetInnerHTML={{ __html: preview.html }}
            />
          ) : (
            <p className="text-sm text-ink-3">{c.text ? "…" : "Без текста"}</p>
          )}
        </div>
        {c.attachments.length > 0 && (
          <div className="mt-3 space-y-2">
            {c.attachments.map((a) => (
              <a key={a.id} href={attachmentUrl(c.id, a.id)} className="flex items-center gap-3 rounded-lg border border-line px-3 py-2 text-sm hover:bg-muted/40">
                {a.kind === "report" ? <FileSpreadsheet className="size-4 shrink-0 text-ok" /> : <Paperclip className="size-4 shrink-0 text-ink-3" />}
                <span className="min-w-0 flex-1 truncate text-ink">{a.fileName}</span>
                <span className="text-xs text-ink-3">{size(a.sizeBytes)}</span>
                <Download className="size-4 text-ink-3" />
              </a>
            ))}
          </div>
        )}
      </section>

      <section className="rounded-xl border border-line bg-surface p-4 sm:p-5">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-base font-semibold text-ink">
            Получатели <span className="text-sm font-normal text-ink-3">· {c.counts.total}</span>
          </h2>
          <select className={field.replace("w-full", "w-auto").replace("h-10", "h-9")} value={filter} onChange={(e) => setFilter(e.target.value)} aria-label="Статус">
            <option value="">Все</option>
            {Object.entries(recipientStatus).map(([key, v]) => (
              <option key={key} value={key}>
                {v.label}
              </option>
            ))}
          </select>
        </div>
        <div className="mt-3 max-h-[600px] overflow-y-auto">
          {rows.map((r) => (
            <div key={r.id} className="flex items-start gap-3 border-b border-line py-2.5 text-sm last:border-b-0">
              <span className="min-w-0 flex-1">
                <span className="block truncate text-ink">{r.title}</span>
                <span className="text-xs text-ink-3">
                  {typeLabel[r.type]}
                  {r.username && ` · @${r.username}`}
                  {r.attempts > 1 && ` · попыток ${r.attempts}`}
                </span>
                {r.error && <span className="mt-0.5 block text-xs text-bad">{r.error}</span>}
              </span>
              <span className="shrink-0 text-right">
                <span className={`block text-sm ${recipientStatus[r.status].className}`}>{recipientStatus[r.status].label}</span>
                <span className="text-xs text-ink-3">
                  {r.sentAt ? tashkent(r.sentAt) : r.nextAttemptAt && r.status === "Pending" ? `повтор ${tashkent(r.nextAttemptAt)}` : ""}
                </span>
              </span>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}
