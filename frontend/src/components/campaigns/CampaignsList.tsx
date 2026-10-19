"use client";

import { useEffect, useState } from "react";
import { createPortal } from "react-dom";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Loader2, Paperclip, Plus, Users, X } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { Note } from "@/components/sales/bits";
import { active, campaignsApi, campaignStatus, tashkent, type CampaignAccount, type CampaignListItem, type CampaignStatus } from "@/lib/campaigns";

const filters: { key: string; label: string; match: (s: CampaignStatus) => boolean }[] = [
  { key: "all", label: "Все", match: () => true },
  { key: "draft", label: "Черновики", match: (s) => s === "Draft" },
  { key: "planned", label: "Запланированы", match: (s) => s === "Scheduled" },
  { key: "running", label: "Идут", match: (s) => s === "Queued" || s === "Sending" || s === "Paused" },
  { key: "done", label: "Отправлены", match: (s) => s === "Completed" || s === "CompletedWithErrors" || s === "Cancelled" || s === "Failed" },
];

/** Все рассылки: карточка — название, статус, начало текста, файлы, получатели, ход отправки. Клик — подробности. */
export function CampaignsList({ initial, accounts }: { initial: CampaignListItem[]; accounts: CampaignAccount[] }) {
  const [rows, setRows] = useState(initial);
  const [filter, setFilter] = useState("all");
  const [creating, setCreating] = useState(false);
  const router = useRouter();
  const params = useSearchParams();

  // «Новая рассылка» из меню на телефоне (/campaigns?new=1): то же окно, что у кнопки; адрес возвращается к списку.
  const askedNew = params.get("new") === "1";
  useEffect(() => {
    if (!askedNew) return;
    if (accounts.length > 0) setCreating(true);
    router.replace("/campaigns", { scroll: false });
  }, [askedNew, accounts.length, router]);

  // Идущие рассылки обновляются сами.
  useEffect(() => {
    const running = rows.some((r) => active(r.status));
    const t = setInterval(() => campaignsApi.list().then(setRows, () => undefined), running ? 5000 : 30000);
    return () => clearInterval(t);
  }, [rows]);

  const f = filters.find((x) => x.key === filter) ?? filters[0];
  const visible = rows.filter((r) => f.match(r.status));

  return (
    <>
      <div className="flex flex-wrap items-center gap-2">
        <div className="flex flex-wrap gap-1 rounded-xl border border-line bg-surface p-1">
          {filters.map((x) => {
            const n = rows.filter((r) => x.match(r.status)).length;
            return (
              <button
                key={x.key}
                onClick={() => setFilter(x.key)}
                className={`rounded-lg px-3 py-1.5 text-sm ${filter === x.key ? "bg-accent-soft font-medium text-accent-strong" : "text-ink-2 hover:bg-muted"}`}
              >
                {x.label}
                {n > 0 && <span className="ml-1.5 text-xs text-ink-3">{n}</span>}
              </button>
            );
          })}
        </div>
        <button className={`${primary} ml-auto`} onClick={() => setCreating(true)} disabled={accounts.length === 0}>
          <Plus className="size-4" />
          Новая рассылка
        </button>
      </div>

      {accounts.length === 0 && (
        <p className="mt-4 rounded-lg bg-warn-soft px-3 py-2 text-sm text-warn">Нет аккаунтов Telegram — администратор подключает их в «Настройки → Telegram».</p>
      )}

      <div className="mt-4 grid gap-3">
        {visible.length === 0 ? (
          <p className="rounded-xl border border-dashed border-line px-4 py-8 text-center text-sm text-ink-3">
            {rows.length === 0 ? "Рассылок пока нет — создайте первую." : "Здесь пусто."}
          </p>
        ) : (
          visible.map((r) => <CampaignCard key={r.id} r={r} />)
        )}
      </div>

      <Note>
        Рассылка идёт от имени аккаунта Telegram, по одному сообщению с паузой. Пишите тем, кто ждёт ваших сообщений: за массовые сообщения незнакомым людям
        Telegram ограничивает аккаунт — тогда OneBase ставит рассылку на паузу. Всё отправленное агент потом объясняет получателям: что в отчёте и насколько всё
        хорошо или плохо.
      </Note>

      {creating && <CreateDialog accounts={accounts} onClose={() => setCreating(false)} />}
    </>
  );
}

function CampaignCard({ r }: { r: CampaignListItem }) {
  const s = campaignStatus[r.status];
  const done = r.counts.sent + r.counts.failed + r.counts.cancelled;
  const when =
    r.status === "Scheduled"
      ? `отправка ${tashkent(r.scheduledAt)}`
      : r.finishedAt
        ? `отправлена ${tashkent(r.finishedAt)}`
        : r.startedAt
          ? `идёт с ${tashkent(r.startedAt)}`
          : `создана ${tashkent(r.createdAt)}`;
  return (
    <Link href={`/campaigns/${r.id}`} className="group block rounded-xl border border-line bg-surface p-4 transition-colors hover:border-accent/40">
      <div className="flex flex-wrap items-start gap-x-3 gap-y-1">
        <span className="min-w-0 flex-1 truncate text-base font-semibold text-ink group-hover:text-accent-strong">{r.name}</span>
        <span className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-medium ${s.className}`}>{s.label}</span>
      </div>
      <p className="mt-1.5 line-clamp-2 text-sm text-ink-2">{r.textPreview ?? <span className="text-ink-3">без текста</span>}</p>
      <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-ink-3">
        <span>{r.accountName}</span>
        <span className="flex items-center gap-1">
          <Users className="size-3.5" />
          {r.counts.total} {plural(r.counts.total, "получатель", "получателя", "получателей")}
        </span>
        {r.files > 0 && (
          <span className="flex items-center gap-1">
            <Paperclip className="size-3.5" />
            {r.files} {plural(r.files, "файл", "файла", "файлов")}
          </span>
        )}
        <span>{when}</span>
        {r.createdBy && <span>{r.createdBy}</span>}
      </div>
      {r.counts.total > 0 && r.status !== "Draft" && r.status !== "Scheduled" && (
        <div className="mt-3">
          <div className="h-1.5 overflow-hidden rounded-full bg-muted">
            <div className="h-full bg-accent" style={{ width: `${Math.round((done / r.counts.total) * 100)}%` }} />
          </div>
          <span className="mt-1 block text-xs text-ink-2">
            отправлено {r.counts.sent} из {r.counts.total}
            {r.counts.failed > 0 && <span className="text-bad"> · ошибок {r.counts.failed}</span>}
          </span>
        </div>
      )}
    </Link>
  );
}

/** Новая рассылка: название и аккаунт → дальше редактор (текст, файлы, получатели). */
function CreateDialog({ accounts, onClose }: { accounts: CampaignAccount[]; onClose: () => void }) {
  const router = useRouter();
  const connected = accounts.filter((a) => a.status === "Connected");
  const [accountId, setAccountId] = useState(connected[0]?.id ?? accounts[0]?.id ?? "");
  const [name, setName] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const close = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", close);
    return () => window.removeEventListener("keydown", close);
  }, [onClose]);

  const create = async () => {
    setBusy(true);
    setError(null);
    try {
      const c = await campaignsApi.create({ accountId, name, description: null, text: null, formatted: true, scheduledAt: null, delaySeconds: 5 });
      router.push(`/campaigns/${c.id}`);
    } catch (e) {
      setError((e as Error).message);
      setBusy(false);
    }
  };

  return createPortal(
    <div className="fixed inset-0 z-50 grid place-items-center bg-black/40 p-4" onClick={onClose}>
      <form
        role="dialog"
        className="w-full max-w-md rounded-xl border border-line bg-surface-solid p-5 shadow-xl"
        onClick={(e) => e.stopPropagation()}
        onSubmit={(e) => {
          e.preventDefault();
          if (name.trim() && accountId && !busy) create();
        }}
      >
        <div className="flex items-center justify-between">
          <h2 className="text-base font-semibold text-ink">Новая рассылка</h2>
          <button type="button" className="text-ink-3 hover:text-ink" onClick={onClose} aria-label="Закрыть">
            <X className="size-5" />
          </button>
        </div>
        <label className="mt-4 block">
          <span className="mb-1.5 block text-sm font-medium text-ink">Название</span>
          <input className={field} value={name} onChange={(e) => setName(e.target.value)} maxLength={200} placeholder="Например, «Отчёт по продажам — дилерам»" autoFocus />
        </label>
        <label className="mt-3 block">
          <span className="mb-1.5 block text-sm font-medium text-ink">Аккаунт Telegram</span>
          <select className={field} value={accountId} onChange={(e) => setAccountId(e.target.value)}>
            {accounts.map((a) => (
              <option key={a.id} value={a.id}>
                {a.name}
                {a.username ? ` (@${a.username})` : ""}
                {a.status !== "Connected" ? " — не подключён" : ""}
              </option>
            ))}
          </select>
        </label>
        {error && <p className="mt-3 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
        <div className="mt-5 flex justify-end gap-2">
          <button type="button" className={button} onClick={onClose} disabled={busy}>
            Отмена
          </button>
          <button type="submit" className={primary} disabled={busy || !name.trim() || !accountId}>
            {busy && <Loader2 className="size-4 animate-spin" />}
            Дальше
          </button>
        </div>
      </form>
    </div>,
    document.body,
  );
}

function plural(n: number, one: string, few: string, many: string) {
  const m10 = n % 10;
  const m100 = n % 100;
  if (m10 === 1 && m100 !== 11) return one;
  if (m10 >= 2 && m10 <= 4 && (m100 < 10 || m100 >= 20)) return few;
  return many;
}
