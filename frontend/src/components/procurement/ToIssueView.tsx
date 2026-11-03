"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useRef, useState } from "react";
import { Loader2, PackageCheck } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { RequestStatusPill } from "@/components/requests/RequestKit";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { dateRu, parseNumber, quantity, type ToIssue } from "@/lib/procurement";

const newKey = () => (typeof crypto !== "undefined" && "randomUUID" in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`);

/** Заявки к выдаче со склада: что зарезервировано (со склада или куплено) и сколько на складе; кладовщик выдаёт из резерва (можно частями). */
export function ToIssueView({ items, warehouse }: { items: ToIssue[]; warehouse: boolean }) {
  const t = useT();
  const router = useRouter();
  const [open, setOpen] = useState<ToIssue | null>(null);
  const [qty, setQty] = useState<Record<string, string>>({});
  const [receiver, setReceiver] = useState("");
  const [comment, setComment] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const key = useRef("");

  const start = (r: ToIssue) => {
    setQty(Object.fromEntries(r.lines.map((l) => [l.requestItemId, String(l.left).replace(".", ",")])));
    setReceiver("");
    setComment("");
    setError(null);
    key.current = newKey();
    setOpen(r);
  };

  const issue = async () => {
    if (!open) return;
    const lines = Object.entries(qty)
      .map(([requestItemId, v]) => ({ requestItemId, quantity: parseNumber(v) ?? 0 }))
      .filter((l) => l.quantity > 0);
    if (lines.length === 0) {
      setError(t("Укажите, что и сколько выдать."));
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await bff(`procurement/requests/${open.requestId}/issue`, {
        method: "POST",
        body: JSON.stringify({ lines, receiver: receiver.trim() || null, comment: comment.trim() || null, clientKey: key.current }),
      });
      setOpen(null);
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  if (items.length === 0) {
    return (
      <section className="flex flex-col items-center rounded-2xl border border-line bg-surface px-6 py-14 text-center">
        <span className="grid size-14 place-items-center rounded-2xl bg-ok-soft text-ok">
          <PackageCheck className="size-7" aria-hidden />
        </span>
        <h2 className="mt-4 text-lg font-semibold text-ink">{t("Выдавать нечего")}</h2>
        <p className="mt-1.5 max-w-md text-sm text-ink-2">{t("Здесь появятся заявки, товар по которым на складе: взятый со склада или купленный снабжением. Он зарезервирован и ждёт выдачи.")}</p>
      </section>
    );
  }

  return (
    <>
      <ul className="space-y-3">
        {items.map((r) => (
          <li key={r.requestId} className="rounded-2xl border border-line bg-surface p-4 sm:p-5">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <span className="min-w-0">
                <span className="flex flex-wrap items-center gap-2">
                  <span className="text-xs tabular-nums text-ink-3">{r.number}</span>
                  <RequestStatusPill status={r.status} />
                </span>
                <Link href={`/requests/${r.requestId}`} className="mt-1 block font-semibold text-ink hover:text-accent-strong">
                  {r.title}
                </Link>
                <span className="block text-sm text-ink-2">
                  {r.departmentName} · {r.initiatorName}
                  {r.desiredDate && ` · ${t("нужно к {date}", { date: dateRu(r.desiredDate) })}`}
                </span>
              </span>
              {warehouse && (
                <button type="button" onClick={() => start(r)} className="inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-accent-strong">
                  <PackageCheck className="size-4" aria-hidden /> {t("Выдать")}
                </button>
              )}
            </div>
            <ul className="mt-3 divide-y divide-line rounded-xl bg-muted/60">
              {r.lines.map((l) => (
                <li key={l.requestItemId} className="flex items-center justify-between gap-3 px-3 py-2 text-sm">
                  <span className="min-w-0">
                    <span className="block truncate text-ink">{l.name}</span>
                    <span className="text-xs tabular-nums text-ink-3">{l.sku}</span>
                  </span>
                  <span className="shrink-0 text-right tabular-nums">
                    <span className="font-semibold text-ink">
                      {quantity(l.left)} {l.unitCode}
                    </span>
                    <span className="block text-[11px] text-ink-3">{t("на складе {n}", { n: quantity(l.onHand) })}</span>
                  </span>
                </li>
              ))}
            </ul>
          </li>
        ))}
      </ul>

      <Sheet
        open={!!open}
        onClose={() => setOpen(null)}
        title={t("Выдать со склада")}
        subtitle={open ? `${open.number} · ${open.title}` : undefined}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" onClick={() => setOpen(null)} className="inline-flex h-10 items-center rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted">
              {t("Закрыть")}
            </button>
            <button type="button" onClick={() => void issue()} disabled={busy} className="inline-flex h-10 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50">
              {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}
              {t("Выдать")}
            </button>
          </div>
        }
      >
        <div className="space-y-2">
          {open?.lines.map((l) => (
            <label key={l.requestItemId} className="flex items-center gap-3 rounded-xl border border-line px-3 py-2">
              <span className="min-w-0 flex-1">
                <span className="block truncate text-sm text-ink">{l.name}</span>
                <span className="text-xs text-ink-3">{t("в резерве {n}", { n: `${quantity(l.left)} ${l.unitCode}` })}</span>
              </span>
              <input
                value={qty[l.requestItemId] ?? ""}
                onChange={(e) => setQty((q) => ({ ...q, [l.requestItemId]: e.target.value }))}
                inputMode="decimal"
                aria-label={t("Выдать: {name}", { name: l.name })}
                className="h-9 w-24 rounded-md border border-line bg-surface px-2 text-right text-sm tabular-nums text-ink focus:border-accent focus:outline-none"
              />
            </label>
          ))}
          <label className="block pt-2">
            <span className="mb-1.5 block text-sm font-medium text-ink">
              {t("Кто получил")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
            </span>
            <input value={receiver} onChange={(e) => setReceiver(e.target.value)} maxLength={200} className="h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink focus:border-accent focus:outline-none" />
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">
              {t("Комментарий")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
            </span>
            <input value={comment} onChange={(e) => setComment(e.target.value)} maxLength={1000} className="h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink focus:border-accent focus:outline-none" />
          </label>
          <ErrorNote text={error} />
        </div>
      </Sheet>
    </>
  );
}
