"use client";

import Link from "next/link";
import { useEffect, useSyncExternalStore } from "react";
import { CircleCheckBig, TriangleAlert, X } from "lucide-react";
import { useT } from "@/i18n/client";

type Toast = { id: number; tone: "ok" | "error"; text: string; href?: string; action?: string };

let current: Toast | null = null;
let seq = 0;
const listeners = new Set<() => void>();
const emit = () => listeners.forEach((listener) => listener());
const subscribe = (listener: () => void) => {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
};

/** Сообщение после действия с задачей («Задача создана · Открыть»): живёт дольше формы, которая его вызвала. Текст — уже переведённый. */
export function showTaskToast(toast: Omit<Toast, "id">) {
  current = { ...toast, id: ++seq };
  emit();
}

const hide = (id: number) => {
  if (current?.id !== id) return;
  current = null;
  emit();
};

/**
 * Место сообщений о задачах — один раз в оболочке приложения. Снизу по центру; на телефоне — над нижней панелью. Через 6 с исчезает само.
 * Цвета — токены экрана задач (.tasks-ui), как у формы.
 */
export function TaskToastHost() {
  const t = useT();
  const toast = useSyncExternalStore(subscribe, () => current, () => null);

  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => hide(toast.id), 6000);
    return () => window.clearTimeout(timer);
  }, [toast]);

  if (!toast) return null;
  const Icon = toast.tone === "error" ? TriangleAlert : CircleCheckBig;
  return (
    <div className="tasks-ui pointer-events-none fixed inset-x-0 bottom-[calc(96px+env(safe-area-inset-bottom))] z-[70] flex justify-center px-4 lg:bottom-6">
      <div
        role={toast.tone === "error" ? "alert" : "status"}
        className={`pointer-events-auto flex max-w-md items-start gap-3 rounded-xl border bg-surface-solid px-4 py-3 text-sm text-ink shadow-[0_12px_32px_rgb(0_0_0/0.35)] ${
          toast.tone === "error" ? "border-bad/40" : "border-line-strong"
        }`}
      >
        <Icon className={`mt-0.5 size-4 shrink-0 ${toast.tone === "error" ? "text-bad" : "text-ok"}`} aria-hidden />
        <span className="min-w-0 flex-1">{toast.text}</span>
        {toast.href && (
          <Link href={toast.href} onClick={() => hide(toast.id)} className="shrink-0 font-semibold text-link hover:underline">
            {toast.action ?? t("Открыть")}
          </Link>
        )}
        <button type="button" onClick={() => hide(toast.id)} aria-label={t("Закрыть")} className="-mr-1 grid size-6 shrink-0 place-items-center rounded-md text-subtle hover:bg-surface-2 hover:text-ink">
          <X className="size-3.5" />
        </button>
      </div>
    </div>
  );
}
