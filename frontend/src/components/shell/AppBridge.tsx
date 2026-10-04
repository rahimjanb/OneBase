"use client";

import { useEffect, useRef, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { Bell, Smartphone, X } from "lucide-react";
import { enablePush, forgetDeviceBeforeLogout, isIos, notificationPermission, prefetchPushKey, promptRecentlyDismissed, pushSupport, rememberPromptDismissed, ensurePush } from "@/lib/push";

/**
 * Связь страницы с service worker: при запуске тихо проверяет push-подписку (разрешение уже есть — без вопросов) и
 * привязывает её к вошедшему пользователю; пришло уведомление — данные перечитываются; нажали на уведомление — переход.
 */
/** refreshOnFocus — перечитать страницу при возвращении в приложение (Sales Base: задачи и маршрут меняются в течение дня). */
export function AppBridge({ userId, refreshOnFocus = false }: { userId: string | null; refreshOnFocus?: boolean }) {
  const router = useRouter();
  const pathname = usePathname();
  // Адрес, на котором в форму вводили данные (визит, задача). Пока он открыт, сами не обновляем: неудачный refresh
  // в Next.js (Wi-Fi без интернета, перезапуск сервера) перезагружает страницу, и введённое пропало бы.
  // Фильтры и поиск меняют адрес — это не несохранённый ввод.
  const editedAt = useRef<string | null>(null);

  useEffect(() => {
    editedAt.current = null;
  }, [pathname]);

  useEffect(() => {
    if (userId) void ensurePush();
  }, [userId]);

  // Строка состояния установленного приложения — цвета шапки этой оболочки (OneBase или Sales Base): и после перехода
  // между ними без перезагрузки, и у Sales Base на основном домене, и при выбранной вручную теме. Next.js при переходах
  // заново вставляет теги theme-color с цветами домена из generateViewport — их тоже поправляем. Сменилась тема системы —
  // цвет тоже.
  useEffect(() => {
    const shell = document.querySelector<HTMLElement>("[data-app-shell]");
    if (!shell) return;
    const sync = () => {
      const color = getComputedStyle(shell).getPropertyValue("--color-sidebar").trim();
      if (!color) return;
      for (const meta of document.querySelectorAll('meta[name="theme-color"]')) if (meta.getAttribute("content") !== color) meta.setAttribute("content", color);
    };
    sync();
    const scheme = window.matchMedia("(prefers-color-scheme: dark)");
    scheme.addEventListener("change", sync);
    const head = new MutationObserver(sync);
    head.observe(document.head, { childList: true, subtree: true, attributes: true, attributeFilter: ["content"] });
    return () => {
      scheme.removeEventListener("change", sync);
      head.disconnect();
    };
  }, []);

  useEffect(() => {
    if (typeof navigator === "undefined" || !("serviceWorker" in navigator)) return;
    const here = () => window.location.pathname + window.location.search;
    const onEdit = (e: Event) => {
      if (e.target instanceof Element && e.target.closest("[data-app-shell]")) editedAt.current = here();
    };
    document.addEventListener("input", onEdit, true);
    document.addEventListener("change", onEdit, true);
    let lastRefresh = 0;
    // Без связи refresh в Next.js превращается в перезагрузку страницы с потерей введённого — тогда не обновляем.
    const refresh = (minGapMs: number) => {
      if (!navigator.onLine || editedAt.current === here() || Date.now() - lastRefresh < minGapMs) return;
      lastRefresh = Date.now();
      router.refresh();
    };
    const onMessage = (event: MessageEvent) => {
      const data = event.data as { type?: string; url?: string } | null;
      if (data?.type === "push-received") refresh(0);
      // Нажали на уведомление: переход внутри приложения. Ответ service worker'у — «перейду сама», иначе он перейдёт окном.
      if (data?.type === "navigate" && typeof data.url === "string" && data.url.startsWith("/") && !data.url.startsWith("//")) {
        event.ports[0]?.postMessage("ok");
        router.push(data.url);
      }
    };
    navigator.serviceWorker.addEventListener("message", onMessage);
    // Вернулись в приложение — свежие задачи и уведомления (push мог прийти, пока оно было свёрнуто). Не чаще раза в 30 с.
    const onVisible = () => refreshOnFocus && document.visibilityState === "visible" && refresh(30_000);
    document.addEventListener("visibilitychange", onVisible);
    return () => {
      document.removeEventListener("input", onEdit, true);
      document.removeEventListener("change", onEdit, true);
      navigator.serviceWorker.removeEventListener("message", onMessage);
      document.removeEventListener("visibilitychange", onVisible);
    };
  }, [router, refreshOnFocus]);

  return null;
}

/**
 * Баннер «Включите уведомления»: только пока решения нет (не включены и не запрещены), не чаще раза в 14 дней после
 * «Не сейчас». Окно разрешения браузера — по нажатию «Включить» (так требует iPhone, и так Chrome не блокирует сайт).
 */
export function PushPrompt({ appName, text }: { appName: string; text: string }) {
  const [state, setState] = useState<"hidden" | "ask" | "install" | "done">("hidden");

  useEffect(() => {
    const support = pushSupport();
    if (promptRecentlyDismissed()) return;
    if (support === "ios-install") setState("install");
    else if (support === "supported" && notificationPermission() === "default") {
      setState("ask");
      void prefetchPushKey().catch(() => undefined);
    }
  }, []);

  if (state === "hidden") return null;
  const dismiss = () => {
    rememberPromptDismissed();
    setState("hidden");
  };

  return (
    <div className="flex items-start gap-3 rounded-xl border border-accent/30 bg-accent-soft px-4 py-3 text-sm">
      {state === "install" ? <Smartphone className="mt-0.5 size-5 shrink-0 text-accent-strong" /> : <Bell className="mt-0.5 size-5 shrink-0 text-accent-strong" />}
      <div className="min-w-0 flex-1">
        {state === "done" ? (
          <p className="text-ink">Готово: уведомления включены на этом устройстве.</p>
        ) : state === "install" ? (
          <p className="text-ink">
            Чтобы получать уведомления на {isIos() ? "iPhone" : "телефоне"}, установите {appName}: в Safari «Поделиться» → «На экран „Домой“» и открывайте с иконки.
          </p>
        ) : (
          <>
            <p className="text-ink">{text}</p>
            <div className="mt-2 flex flex-wrap gap-2">
              <button
                type="button"
                onClick={async () => {
                  const result = await enablePush();
                  setState(result === "granted" ? "done" : "hidden");
                }}
                className="inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-accent-strong"
              >
                Включить
              </button>
              <button type="button" onClick={dismiss} className="inline-flex h-9 items-center rounded-lg px-3 text-sm text-ink-2 hover:bg-surface">
                Не сейчас
              </button>
            </div>
          </>
        )}
      </div>
      <button type="button" onClick={dismiss} aria-label="Скрыть" className="grid size-8 shrink-0 place-items-center rounded-full text-ink-3 hover:bg-surface">
        <X className="size-4" />
      </button>
    </div>
  );
}

/** Ссылка «Выйти»: сначала устройство перестаёт получать уведомления этого сотрудника (общий телефон), затем выход. */
export function LogoutLink({ href, className, children, role }: { href: string; className?: string; children: React.ReactNode; role?: string }) {
  return (
    <a
      href={href}
      role={role}
      className={className}
      onClick={async (e) => {
        e.preventDefault();
        await forgetDeviceBeforeLogout();
        window.location.href = href;
      }}
    >
      {children}
    </a>
  );
}
