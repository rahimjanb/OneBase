"use client";

import { useCallback, useEffect, useId, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { usePathname, useRouter } from "next/navigation";
import { ArrowLeft, ArrowRight, Check, ChevronRight, CircleCheck, EyeOff, Rocket, X } from "lucide-react";
import { useLang, useT } from "@/i18n/client";
import { formatDate, localeOf, type T } from "@/i18n/core";
import type { Me } from "@/lib/users";
import {
  CURRENT_RELEASE,
  TAG_LABEL,
  pluralRu,
  seenKey,
  seenRelease,
  shouldShow,
  visibleSteps,
  type WhatsNewRelease,
  type WhatsNewStep,
} from "@/lib/whats-new";
import { COLOR, STEP_ICON, WhatsNewPreview } from "./WhatsNewPreview";

// Окно «Что нового» после обновления (макет untitled.pen: fIyJU, OkLhT — компьютер; nOeLF, IeSiY — телефон). Показывается само один раз на
// версию релиза; закрыто любым способом — отметка в браузере и в профиле на сервере, на другом устройстве окно тоже не появится. Снова —
// Профиль → «Что нового». Карточки — только разделов, открытых пользователю; нет ни одной — окна нет.

const OPEN_EVENT = "onebase:whats-new";

/** Открыть окно вручную: Профиль → «Что нового» (повторное закрытие ничего не меняет). */
export function openWhatsNew() {
  window.dispatchEvent(new Event(OPEN_EVENT));
}

type CloseMethod = "x" | "close_button" | "done" | "esc" | "overlay" | "swipe" | "link";

function readSeen(userId: string): string | null {
  try {
    return localStorage.getItem(seenKey(userId));
  } catch {
    return null;
  }
}

/** Закрыто: сразу в браузере (окно не мигнёт, пока грузится профиль), затем в профиль на сервере; keepalive — переход не обрывает запрос. */
function markSeen(userId: string, version: string) {
  try {
    if (!seenRelease(localStorage.getItem(seenKey(userId)), version)) localStorage.setItem(seenKey(userId), version);
  } catch {
    // Браузер без хранилища: остаётся отметка на сервере.
  }
  void fetch("/bff/api/auth/whats-new", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ version }),
    keepalive: true,
  }).catch(() => undefined);
}

/** Карточки, открытые пользователю (те же права, что у меню). */
function useSteps(me: Me | null, permissions: readonly string[] | null): WhatsNewStep[] {
  return useMemo(
    () => (me && permissions ? visibleSteps(CURRENT_RELEASE, { permissions, agentDepartments: me.agentDepartments }) : []),
    [me, permissions],
  );
}

/**
 * Хозяин окна — в оболочке приложения. Сам показывает окно версии один раз: через 0,7 с после загрузки, когда погасла заставка и не открыто
 * другое окно или шторка (иначе — при следующем переходе между страницами).
 */
export function WhatsNewHost({ me, permissions }: { me: Me | null; permissions: readonly string[] | null }) {
  const release = CURRENT_RELEASE;
  const steps = useSteps(me, permissions);
  const [open, setOpen] = useState(false);
  const pathname = usePathname();
  const settled = useRef(false);

  useEffect(() => {
    if (!me || settled.current) return;
    const local = readSeen(me.id);
    if (!shouldShow(release, me, local, steps.length)) {
      settled.current = true;
      // Браузер помнит закрытие, а сервер нет (запрос не дошёл) — дописать без показа.
      if (seenRelease(local, release.version) && !seenRelease(me.whatsNewSeen, release.version)) markSeen(me.id, release.version);
      return;
    }
    const started = Date.now();
    let timer = 0;
    const attempt = () => {
      // Заставка 1Base ещё на экране (class «gone» — через 0,5 с после «hide»): подождать её, но не дольше 10 с.
      const splash = document.getElementById("splash");
      if (splash && !splash.classList.contains("gone") && Date.now() - started < 10_000) {
        timer = window.setTimeout(attempt, 300);
        return;
      }
      // Открыто другое окно или шторка — не перебивать, попробовать при следующем переходе.
      if (document.querySelector('[aria-modal="true"]')) return;
      settled.current = true;
      setOpen(true);
    };
    timer = window.setTimeout(attempt, 700);
    return () => window.clearTimeout(timer);
  }, [pathname, me, steps.length, release]);

  useEffect(() => {
    const onOpen = () => {
      settled.current = true;
      setOpen(true);
    };
    window.addEventListener(OPEN_EVENT, onOpen);
    return () => window.removeEventListener(OPEN_EVENT, onOpen);
  }, []);

  const close = useCallback(
    (_method: CloseMethod) => {
      setOpen(false);
      if (me) markSeen(me.id, release.version);
    },
    [me, release.version],
  );

  if (!open || !me || steps.length === 0) return null;
  return <WhatsNewDialog release={release} steps={steps} onClose={close} />;
}

/** «Что нового» в меню профиля (аватар на компьютере, «Профиль» на телефоне); нет ни одной открытой карточки — пункта нет. */
export function WhatsNewMenuItem({
  me,
  permissions,
  className,
  onOpen,
}: {
  me: Me | null;
  permissions: readonly string[] | null;
  className: string;
  /** Перед открытием: закрыть меню или шторку профиля. */
  onOpen?: () => void;
}) {
  const t = useT();
  const steps = useSteps(me, permissions);
  if (steps.length === 0) return null;
  return (
    <button
      type="button"
      className={className}
      onClick={(e) => {
        e.currentTarget.closest("details")?.removeAttribute("open");
        onOpen?.();
        // Шторка профиля закрывается своим кадром — окно открывается следом.
        window.setTimeout(openWhatsNew, onOpen ? 80 : 0);
      }}
    >
      <Rocket aria-hidden className="size-4 text-ink-3" />
      {t("Что нового")}
    </button>
  );
}

/** «13 ноября» и «13 ноября 2026» на языке интерфейса (дата релиза — по Ташкенту). */
function releaseDates(release: WhatsNewRelease, locale: string) {
  const day = new Date(`${release.date}T12:00:00+05:00`);
  const short = formatDate(day, locale, { day: "numeric", month: "long", timeZone: "Asia/Tashkent" });
  return { short, long: `${short} ${release.date.slice(0, 4)}` };
}

const tagText = (step: WhatsNewStep, t: T) => t(TAG_LABEL[step.tag]);

function WhatsNewDialog({ release, steps, onClose }: { release: WhatsNewRelease; steps: WhatsNewStep[]; onClose: (method: CloseMethod) => void }) {
  const t = useT();
  const lang = useLang();
  const router = useRouter();
  const [index, setIndex] = useState(0);
  const [dir, setDir] = useState<"next" | "prev">("next");
  const panel = useRef<HTMLDivElement>(null);
  const headingId = useId();
  const touch = useRef<{ x: number; y: number } | null>(null);

  const step = steps[index];
  const count = steps.length;
  const single = count === 1;
  const last = index === count - 1;
  const dates = releaseDates(release, localeOf(lang));
  const total = `${count} ${t(pluralRu(count, ["нововведение", "нововведения", "нововведений"]))}`;
  const stepOf = t("Шаг {n} из {total}", { n: index + 1, total: count });
  const doneLabel = single ? t("Понятно") : t("Готово, начать работу");

  const go = useCallback(
    (to: number) => {
      if (to < 0 || to >= count || to === index) return;
      setDir(to > index ? "next" : "prev");
      setIndex(to);
    },
    [count, index],
  );
  const next = useCallback(() => (last ? onClose("done") : go(index + 1)), [go, index, last, onClose]);
  const prev = useCallback(() => go(index - 1), [go, index]);
  const follow = () => {
    onClose("link");
    router.push(step.route);
  };

  // Фокус — на «Далее» (видимой на этой ширине), после закрытия — туда, откуда открыли; фон не прокручивается.
  useEffect(() => {
    const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const primary = [...(panel.current?.querySelectorAll<HTMLElement>("[data-wn-primary]") ?? [])].find((el) => el.offsetParent !== null);
    (primary ?? panel.current)?.focus({ preventScroll: true });
    const target = document.querySelector<HTMLElement>("[data-app-scroll]") ?? document.body;
    const overflow = target.style.overflow;
    target.style.overflow = "hidden";
    return () => {
      target.style.overflow = overflow;
      if (opener?.isConnected) opener.focus({ preventScroll: true });
    };
  }, []);

  // Клавиатура: → — следующий шаг, Enter — «Далее» / «Готово», ← — назад, Esc — закрыть; Tab не выходит за окно.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const el = e.target instanceof HTMLElement ? e.target : null;
      if (e.key === "Escape") {
        e.preventDefault();
        onClose("esc");
      } else if (e.key === "ArrowRight") {
        // Стрелка только листает: на последнем шаге окно закрывают «Готово» или Enter, а не случайное нажатие.
        e.preventDefault();
        go(index + 1);
      } else if (e.key === "Enter" && !el?.closest("button, a")) {
        e.preventDefault();
        next();
      } else if (e.key === "ArrowLeft") {
        e.preventDefault();
        prev();
      } else if (e.key === "Tab" && panel.current) {
        const items = [...panel.current.querySelectorAll<HTMLElement>("button:not([disabled]), a[href], [tabindex='0']")].filter((x) => x.offsetParent !== null);
        if (items.length === 0) return;
        const first = items[0];
        const lastItem = items[items.length - 1];
        if (e.shiftKey && (document.activeElement === first || !panel.current.contains(document.activeElement))) {
          e.preventDefault();
          lastItem.focus();
        } else if (!e.shiftKey && (document.activeElement === lastItem || !panel.current.contains(document.activeElement))) {
          e.preventDefault();
          first.focus();
        }
      }
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [go, index, next, prev, onClose]);

  // Телефон: свайп влево / вправо по содержимому — шаги; вниз по шапке — закрыть.
  const onTouchStart = (e: React.TouchEvent) => {
    const p = e.touches[0];
    touch.current = { x: p.clientX, y: p.clientY };
  };
  const swipe = (e: React.TouchEvent) => {
    const start = touch.current;
    touch.current = null;
    if (!start) return null;
    const p = e.changedTouches[0];
    return { dx: p.clientX - start.x, dy: p.clientY - start.y };
  };
  const onContentTouchEnd = (e: React.TouchEvent) => {
    const d = swipe(e);
    if (!d || Math.abs(d.dx) < 50 || Math.abs(d.dx) < Math.abs(d.dy) * 1.5) return;
    if (d.dx < 0 && !last) go(index + 1);
    else if (d.dx > 0) prev();
  };
  const onHeaderTouchEnd = (e: React.TouchEvent) => {
    const d = swipe(e);
    if (d && d.dy > 70 && d.dy > Math.abs(d.dx)) onClose("swipe");
  };

  const c = COLOR[step.color];
  const Icon = STEP_ICON[step.icon];
  const host = document.querySelector<HTMLElement>("[data-app-shell]") ?? document.body;

  const dialog = (
    <div
      className="wn-overlay fixed inset-0 z-[60] flex items-end justify-center overscroll-contain bg-black/70 md:items-center md:bg-[#03060dcc] md:p-4 md:backdrop-blur-[4px]"
      onClick={() => onClose("overlay")}
    >
      <div
        ref={panel}
        role="dialog"
        aria-modal="true"
        aria-labelledby={headingId}
        tabIndex={-1}
        onClick={(e) => e.stopPropagation()}
        className="wn-panel relative flex max-h-[calc(100dvh-84px)] w-full flex-col overflow-hidden rounded-t-[24px] border-t border-line-strong bg-surface-solid text-ink outline-none md:h-[min(820px,92vh)] md:w-[min(720px,100vw-32px)] md:rounded-[20px] md:border md:shadow-[0_30px_80px_rgb(0_0_0/0.55)] lg:w-[min(1000px,100vw-48px)] lg:flex-row"
      >
        {/* Компьютер: шаги слева. */}
        <aside className="hidden w-[300px] shrink-0 flex-col gap-6 overflow-y-auto border-r border-line bg-page px-5 pb-6 pt-7 lg:flex">
          <div className="flex flex-col gap-3 px-2">
            <span className="inline-flex w-fit items-center gap-1.5 rounded-full bg-accent/15 px-2.5 py-[5px] text-[12px] font-semibold text-accent">
              <Rocket aria-hidden className="size-[13px]" />
              {t("Обновление {date}", { date: dates.short })}
            </span>
            <span className="text-[22px] font-bold leading-tight tracking-[-0.3px] text-ink">{t("Что нового в 1Base")}</span>
            <span className="text-[13px] text-ink-3">
              {dates.long} · {total}
            </span>
          </div>
          {!single && (
            <div role="tablist" aria-orientation="vertical" aria-label={t("Нововведения")} className="flex flex-col gap-1">
              {steps.map((s, i) => {
                const state = i < index ? "done" : i === index ? "current" : "next";
                return (
                  <button
                    key={s.id}
                    type="button"
                    role="tab"
                    aria-selected={state === "current"}
                    onClick={() => go(i)}
                    className={`flex w-full items-center gap-3 rounded-xl border p-2.5 text-left transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent ${
                      state === "current" ? "border-line-strong bg-muted" : "border-transparent hover:bg-muted/60"
                    }`}
                  >
                    <span
                      aria-hidden
                      className={`grid size-7 shrink-0 place-items-center rounded-full text-[12px] font-semibold ${
                        state === "done" ? "bg-ok/15 text-ok" : state === "current" ? "bg-accent text-white" : "border border-line-strong text-ink-4"
                      }`}
                    >
                      {state === "done" ? <Check className="size-3.5" /> : i + 1}
                    </span>
                    <span className="min-w-0">
                      <span className={`block text-[13px] leading-snug ${state === "current" ? "font-semibold text-ink" : state === "done" ? "font-medium text-ink-2" : "font-medium text-ink-3"}`}>
                        {t(s.title)}
                      </span>
                      <span className={`block text-[11px] ${state === "current" ? COLOR[s.color].text : "text-ink-4"}`}>{tagText(s, t)}</span>
                    </span>
                  </button>
                );
              })}
            </div>
          )}
          <div className="flex-1" />
          <div className="flex gap-2.5 rounded-xl border border-line bg-surface-solid p-3 text-[12px] leading-[1.45] text-ink-3">
            <EyeOff aria-hidden className="mt-px size-[15px] shrink-0" />
            <span>{t("Окно показывается один раз. Открыть снова: Профиль → «Что нового»")}</span>
          </div>
        </aside>

        <section className="flex min-h-0 min-w-0 flex-1 flex-col gap-4 px-5 pb-[calc(1.75rem+env(safe-area-inset-bottom))] pt-2 md:gap-[22px] md:px-7 md:pb-6 md:pt-6">
          {/* Телефон: полоска, шапка с ракетой, шаг и его название. Свайп вниз — закрыть. */}
          <div className="md:hidden" onTouchStart={onTouchStart} onTouchEnd={onHeaderTouchEnd}>
            <div aria-hidden className="mx-auto h-1 w-10 rounded-full bg-line-strong" />
            <div className="mt-4 flex items-center gap-3">
              <span aria-hidden className="grid size-[34px] shrink-0 place-items-center rounded-[10px] bg-accent/15 text-accent">
                <Rocket className="size-4" />
              </span>
              <span className="min-w-0 flex-1">
                <span className="block truncate text-[15px] font-bold text-ink">{t("Что нового в 1Base")}</span>
                <span className="block truncate text-[12px] text-ink-3">{t("Обновление {date}", { date: dates.short })}</span>
              </span>
              <CloseButton label={t("Закрыть")} onClick={() => onClose("x")} size="size-[34px]" />
            </div>
            {!single && (
              <>
                <div className="mt-4 flex items-center justify-between gap-3 text-[12px]">
                  <span className="font-medium text-ink-3">{stepOf}</span>
                  <span className={`truncate font-semibold ${c.text}`}>{t(step.title)}</span>
                </div>
                <div className="mt-2 flex gap-1" role="progressbar" aria-label={stepOf} aria-valuemin={1} aria-valuemax={count} aria-valuenow={index + 1}>
                  {steps.map((s, i) => (
                    <span key={s.id} className={`h-1 flex-1 rounded-full ${i <= index ? "bg-accent" : "bg-line-strong"}`} />
                  ))}
                </div>
              </>
            )}
          </div>

          {/* Компьютер и планшет: шаг, полоски, крестик. */}
          <div className="hidden items-center gap-4 md:flex">
            {!single && (
              <>
                <span className="text-[13px] font-medium text-ink-3">{stepOf}</span>
                <span className="flex items-center gap-1" role="progressbar" aria-label={stepOf} aria-valuemin={1} aria-valuemax={count} aria-valuenow={index + 1}>
                  {steps.map((s, i) => (
                    <span key={s.id} className={`h-1 rounded-full transition-all duration-200 ${i === index ? "w-9" : "w-5"} ${i <= index ? "bg-accent" : "bg-line-strong"}`} />
                  ))}
                </span>
              </>
            )}
            <span className="flex-1" />
            <CloseButton label={t("Закрыть")} onClick={() => onClose("x")} size="size-9" />
          </div>

          {/* Не помещается (невысокий экран, длинный шаг) — прокручивается; низ затухает, в конце прокрутки текст виден целиком. */}
          <div
            className="-mx-1 min-h-0 flex-1 overflow-y-auto overscroll-contain px-1 [mask-image:linear-gradient(to_bottom,#000_calc(100%-20px),transparent)]"
            onTouchStart={onTouchStart}
            onTouchEnd={onContentTouchEnd}
          >
            <div key={step.id} className={`flex flex-col gap-4 pb-5 md:gap-[22px] ${dir === "next" ? "wn-next" : "wn-prev"}`}>
              <div
                role="img"
                aria-label={t(step.alt)}
                className="grid shrink-0 place-items-center overflow-hidden rounded-2xl border border-line bg-muted px-[18px] py-[22px] md:h-[270px] md:px-6 md:py-0 [@media(min-width:768px)_and_(max-height:800px)]:h-auto [@media(min-width:768px)_and_(max-height:800px)]:py-4"
                style={{ backgroundImage: `radial-gradient(140% 130% at 50% 0%, color-mix(in srgb, ${c.glow} 25%, transparent), transparent 70%)` }}
              >
                <WhatsNewPreview icon={step.icon} color={step.color} />
              </div>

              <div className="flex flex-col gap-2.5 md:gap-3">
                <span className={`inline-flex w-fit items-center gap-1.5 rounded-full px-2.5 py-1 text-[11px] font-semibold md:text-[12px] ${c.soft} ${c.text}`}>
                  <Icon aria-hidden className="size-3 md:size-[13px]" />
                  <span className="md:hidden">{tagText(step, t)}</span>
                  <span className="hidden md:inline">
                    {tagText(step, t)} · {t(step.title)}
                  </span>
                </span>
                <h2 id={headingId} className="text-[22px] font-bold leading-[1.2] tracking-[-0.3px] text-ink md:text-[26px] md:tracking-[-0.4px]">
                  {t(step.heading)}
                </h2>
                <p className="text-[14px] leading-[1.5] text-ink-2 md:text-[15px] md:leading-[1.55]">
                  <span className="md:hidden">{t(step.descriptionShort)}</span>
                  <span className="hidden md:inline">{t(step.description)}</span>
                </p>
                <ul className="mt-1 flex flex-col gap-[9px]">
                  {step.bullets.map((b) => (
                    <li key={b} className="flex items-start gap-2.5 text-[13px] leading-snug text-ink md:text-[14px]">
                      <CircleCheck aria-hidden className="mt-px size-[15px] shrink-0 text-ok md:size-4" />
                      {t(b)}
                    </li>
                  ))}
                </ul>
                <div className="mt-1 flex flex-wrap items-center gap-2 text-[12px] md:text-[13px]">
                  <span className="text-ink-3">{t("Где найти:")}</span>
                  {step.path.map((p, i) => {
                    const lastChip = i === step.path.length - 1;
                    const chip = "rounded-lg border border-line bg-muted px-2.5 py-1 text-[11px] font-medium text-ink-2 md:text-[12px]";
                    return (
                      <span key={p} className="inline-flex items-center gap-2">
                        {i > 0 && <ChevronRight aria-hidden className="size-3.5 text-ink-4" />}
                        {lastChip ? (
                          <button
                            type="button"
                            onClick={follow}
                            className={`${chip} transition-colors hover:border-accent/60 hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent`}
                            title={t("Открыть раздел")}
                          >
                            {t(p)}
                          </button>
                        ) : (
                          <span className={chip}>{t(p)}</span>
                        )}
                      </span>
                    );
                  })}
                </div>
              </div>
            </div>
          </div>

          {/* Компьютер и планшет: «Закрыть» слева, «Назад» и «Далее» справа. */}
          <div className="hidden items-center gap-3 border-t border-line pt-[18px] md:flex">
            <button
              type="button"
              onClick={() => onClose("close_button")}
              className="rounded-[10px] px-4 py-[11px] text-[14px] font-medium text-ink-3 transition-colors hover:bg-muted hover:text-ink-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
            >
              {t("Закрыть")}
            </button>
            <span className="flex-1" />
            {!single && (
              <button
                type="button"
                onClick={prev}
                disabled={index === 0}
                className="inline-flex items-center gap-2 rounded-[10px] border border-line-strong bg-surface-solid px-4 py-[11px] text-[14px] font-medium text-ink-2 transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent disabled:pointer-events-none disabled:opacity-40"
              >
                <ArrowLeft aria-hidden className="size-4" />
                {t("Назад")}
              </button>
            )}
            <button
              type="button"
              data-wn-primary
              onClick={next}
              className="inline-flex items-center gap-2 rounded-[10px] bg-accent px-5 py-[11px] text-[14px] font-semibold text-white transition-colors hover:bg-accent-strong focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2 focus-visible:ring-offset-surface-solid"
            >
              {last ? doneLabel : t("Далее")}
              {last ? <Check aria-hidden className="size-4" /> : <ArrowRight aria-hidden className="size-4" />}
            </button>
          </div>

          {/* Телефон: «Назад» и «Далее» высотой 50, под ними «Закрыть» и подпись. */}
          <div className="flex flex-col items-center gap-3 md:hidden">
            <div className="flex w-full gap-3">
              {!single && (
                <button
                  type="button"
                  onClick={prev}
                  disabled={index === 0}
                  className="inline-flex h-[50px] w-[110px] shrink-0 items-center justify-center gap-2 rounded-xl border border-line-strong bg-muted text-[15px] font-medium text-ink-2 disabled:opacity-40"
                >
                  <ArrowLeft aria-hidden className="size-4" />
                  {t("Назад")}
                </button>
              )}
              <button
                type="button"
                data-wn-primary
                onClick={next}
                className="inline-flex h-[50px] min-w-0 flex-1 items-center justify-center gap-2 rounded-xl bg-accent text-[15px] font-semibold text-white active:bg-accent-strong"
              >
                {last ? (single ? t("Понятно") : t("Готово")) : t("Далее")}
                {last ? <Check aria-hidden className="size-[18px]" /> : <ArrowRight aria-hidden className="size-[18px]" />}
              </button>
            </div>
            <button type="button" onClick={() => onClose("close_button")} className="px-3 py-1 text-[14px] font-medium text-ink-3">
              {t("Закрыть")}
            </button>
            <span className="flex items-center gap-1.5 text-[11px] text-ink-4">
              <EyeOff aria-hidden className="size-3" />
              {t("Окно больше не появится после закрытия")}
            </span>
          </div>
        </section>
      </div>
    </div>
  );
  return createPortal(dialog, host);
}

function CloseButton({ label, onClick, size }: { label: string; onClick: () => void; size: string }) {
  return (
    <button
      type="button"
      aria-label={label}
      onClick={onClick}
      className={`grid ${size} shrink-0 place-items-center rounded-[10px] border border-line bg-muted text-ink-2 transition-colors hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent`}
    >
      <X aria-hidden className="size-[18px]" />
    </button>
  );
}
