"use client";

import { useState } from "react";
import { useT } from "@/i18n/client";

/** Общие мелочи экранов подбора: действие с «занято/ошибка», поле с подписью, карточка, метка. Кнопки разделов — RecruitingNav. */

export function useAction() {
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
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
  return { busy, error, setError, run };
}

export function ErrorText({ error }: { error: string | null }) {
  const t = useT();
  return error ? <p className="mt-3 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{t(error)}</p> : null;
}

export function Label({ label, hint, children, className }: { label: string; hint?: string; children: React.ReactNode; className?: string }) {
  const t = useT();
  return (
    <label className={`block ${className ?? ""}`}>
      <span className="mb-1 block text-xs font-medium text-ink-2">{t(label)}</span>
      {children}
      {hint && <span className="mt-1 block text-xs text-ink-3">{t(hint)}</span>}
    </label>
  );
}

export function Card({ title, actions, children, className }: { title?: string; actions?: React.ReactNode; children: React.ReactNode; className?: string }) {
  const t = useT();
  return (
    <section className={`rounded-xl border border-line bg-surface p-4 ${className ?? ""}`}>
      {(title || actions) && (
        <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
          {title && <h3 className="text-sm font-semibold text-ink">{t(title)}</h3>}
          {actions}
        </div>
      )}
      {children}
    </section>
  );
}

export function Badge({ className, children }: { className?: string; children: React.ReactNode }) {
  return <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${className ?? "bg-muted text-ink-2"}`}>{children}</span>;
}

export const textarea =
  "min-h-20 w-full rounded-lg border border-line bg-surface px-3 py-2 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
