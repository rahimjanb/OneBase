"use client";

import { useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { BadgeCheck, Ban, CircleCheckBig, Download, FilePen, FileText, Hourglass, Loader2, Paperclip, PackageOpen, Send, Star, Trash2, Truck, type LucideIcon } from "lucide-react";
import { allowedFiles, fileProblem } from "@/components/requests/RequestForm";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { fileSize } from "@/lib/format";
import { documentKindLabel, orderStatusLabel, ratingText, stampRu, type OrderStatus, type ProcDocument } from "@/lib/procurement";

/** Вид статусов заказа поставщику — один для списка, карточки и заявки. */
export const orderStatusLook: Record<OrderStatus, { icon: LucideIcon; tone: string }> = {
  Draft: { icon: FilePen, tone: "bg-muted text-ink-2" },
  AwaitingApproval: { icon: Hourglass, tone: "bg-violet-soft text-violet-light" },
  Approved: { icon: BadgeCheck, tone: "bg-accent-soft text-accent-strong" },
  Ordered: { icon: Send, tone: "bg-accent-soft text-accent-strong" },
  PartiallyDelivered: { icon: PackageOpen, tone: "bg-warn-soft text-warn" },
  Delivered: { icon: Truck, tone: "bg-ok-soft text-ok" },
  Completed: { icon: CircleCheckBig, tone: "bg-ok-soft text-ok" },
  Cancelled: { icon: Ban, tone: "bg-muted text-ink-3" },
};

export function OrderStatusPill({ status, className = "" }: { status: OrderStatus; className?: string }) {
  const t = useT();
  const look = orderStatusLook[status];
  return (
    <span className={`inline-flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded-md px-2 py-0.5 text-xs font-semibold leading-5 ${look.tone} ${className}`}>
      <look.icon className="size-3.5" aria-hidden />
      {t(orderStatusLabel[status])}
    </span>
  );
}

/** Рейтинг поставщика: «4,2» со звездой; нет данных — «нет оценки». */
export function RatingBadge({ rating }: { rating: number | null }) {
  const t = useT();
  if (rating === null) return <span className="text-xs text-ink-3">{t("нет оценки")}</span>;
  const tone = rating >= 80 ? "text-ok" : rating >= 60 ? "text-warn" : "text-bad";
  return (
    <span className={`inline-flex items-center gap-1 text-sm font-semibold tabular-nums ${tone}`} title={t("Рейтинг {n} из 100", { n: rating })}>
      <Star className="size-3.5 fill-current" aria-hidden /> {ratingText(rating)}
    </span>
  );
}

/**
 * Документы поставщика, предложения, заказа или тендерной закупки: список (скачать, удалить), прикрепить файл с видом (договор, КП, счёт…). Права проверяет
 * сервер; can — показать кнопку прикрепления.
 */
export function DocumentsBlock({ owner, id, documents, can, kinds }: { owner: "supplier" | "offer" | "order" | "receipt" | "tender" | "batch" | "qc"; id: string; documents: ProcDocument[]; can: boolean; kinds: string[] }) {
  const t = useT();
  const router = useRouter();
  const input = useRef<HTMLInputElement>(null);
  const [kind, setKind] = useState(kinds[0]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const upload = async (files: FileList) => {
    setError(null);
    setBusy(true);
    try {
      for (const file of Array.from(files)) {
        const problem = fileProblem(file);
        if (problem) {
          setError(`«${file.name}»: ${t(problem)}`);
          continue;
        }
        const form = new FormData();
        form.append("file", file);
        const response = await fetch(`/bff/api/procurement/documents?owner=${owner}&id=${id}&kind=${kind}`, { method: "POST", body: form });
        if (!response.ok) {
          const json = await response.json().catch(() => ({}));
          throw new Error(json.error ? t(json.error) : t("Ошибка {status}", { status: response.status }));
        }
      }
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      {documents.length > 0 ? (
        <ul className="space-y-1">
          {documents.map((d) => {
            const href = `/bff/api/procurement/attachments/${d.id}`;
            return (
              <li key={d.id} className="group flex items-center gap-3 rounded-xl px-1 py-1.5 hover:bg-muted/60">
                <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-muted text-ink-3">
                  <FileText className="size-4" aria-hidden />
                </span>
                <a href={href} className="min-w-0 flex-1">
                  <span className="block truncate text-sm font-medium text-ink group-hover:text-accent-strong">{d.fileName}</span>
                  <span className="block truncate text-[11px] text-ink-3">
                    {d.kind ? `${t(documentKindLabel[d.kind] ?? d.kind)} · ` : ""}
                    {fileSize(d.sizeBytes)}
                    {d.uploadedByName ? ` · ${d.uploadedByName}` : ""} · {stampRu(d.createdAt)}
                  </span>
                </a>
                <a href={href} aria-label={t("Скачать {name}", { name: d.fileName })} className="grid size-8 shrink-0 place-items-center rounded-md text-ink-3 hover:text-ink">
                  <Download className="size-4" aria-hidden />
                </a>
                {d.canDelete && (
                  <button
                    type="button"
                    onClick={async () => {
                      if (!window.confirm(t("Удалить документ «{name}»?", { name: d.fileName }))) return;
                      try {
                        await bff(`procurement/attachments/${d.id}`, { method: "DELETE" });
                        router.refresh();
                      } catch (e) {
                        setError(e instanceof Error ? e.message : String(e));
                      }
                    }}
                    aria-label={t("Удалить {name}", { name: d.fileName })}
                    className="grid size-8 shrink-0 place-items-center rounded-md text-ink-3 hover:text-bad"
                  >
                    <Trash2 className="size-4" aria-hidden />
                  </button>
                )}
              </li>
            );
          })}
        </ul>
      ) : (
        <p className="text-sm text-ink-3">{t("Документов нет")}</p>
      )}
      {can && (
        <div className="mt-3 flex flex-wrap items-center gap-2">
          {kinds.length > 1 && (
            <select value={kind} onChange={(e) => setKind(e.target.value)} aria-label={t("Вид документа")} className="h-10 rounded-lg border border-line bg-surface px-3 text-sm text-ink">
              {kinds.map((k) => (
                <option key={k} value={k}>
                  {t(documentKindLabel[k] ?? k)}
                </option>
              ))}
            </select>
          )}
          <input
            ref={input}
            type="file"
            multiple
            accept={allowedFiles.join(",")}
            className="sr-only"
            tabIndex={-1}
            onChange={(e) => {
              if (e.target.files?.length) void upload(e.target.files);
              e.target.value = "";
            }}
          />
          <button
            type="button"
            onClick={() => input.current?.click()}
            disabled={busy}
            className="inline-flex h-10 items-center gap-2 rounded-lg border border-dashed border-line-strong px-3.5 text-sm font-medium text-ink-2 hover:border-accent hover:text-accent-strong disabled:opacity-50"
          >
            {busy ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Paperclip className="size-4" aria-hidden />} {t("Прикрепить")}
          </button>
        </div>
      )}
      <div className="mt-2">
        <ErrorNote text={error} />
      </div>
    </div>
  );
}
