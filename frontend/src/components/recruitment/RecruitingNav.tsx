"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { Ban, Briefcase, CalendarCheck, ChartColumn, Globe, Megaphone, Send, Settings, Users, type LucideIcon } from "lucide-react";
import { useT } from "@/i18n/client";
import { recruitmentApi } from "@/lib/recruitment";
import { navBadge, navCaption, type NavCaption, type NavCounters, type NavKey } from "@/lib/recruitment-view";

/*
 * Кнопки разделов «Подбора» (ТЗ prompt-hr-podbor-vakansii.md, раздел 3; образец — кнопки «Технолога»). Компьютер: ряд кнопок по ширине
 * содержимого — цветной квадрат с иконкой, название и подпись-счётчик; страница уже 1340 px — подписи скрыты, не помещается — ряд
 * прокручивается (без полосы, с затуханием справа), активная кнопка — в зоне видимости. Телефон (уже 768): сетка 3 × 3, значок-счётчик у
 * «Telegram» и «Собеседований». Это ссылки — Cmd/Ctrl+клик открывает новую вкладку. Цвета — токены палитры макета (data-palette).
 */

type Tone = "accent" | "violet" | "warn" | "ok" | "bad" | "neutral";

const SECTIONS: { key: NavKey; href: string; label: string; mobile?: string; icon: LucideIcon; tone: Tone; manageOnly?: boolean }[] = [
  { key: "vacancies", href: "/hr/recruitment", label: "Вакансии", icon: Briefcase, tone: "accent" },
  { key: "candidates", href: "/hr/recruitment/candidates", label: "Кандидаты", icon: Users, tone: "violet" },
  { key: "telegram", href: "/hr/recruitment/telegram", label: "Диалоги Telegram", mobile: "Telegram", icon: Send, tone: "accent" },
  { key: "publications", href: "/hr/recruitment/publications", label: "Публикации", icon: Megaphone, tone: "warn" },
  { key: "interviews", href: "/hr/recruitment/interviews", label: "Собеседования", icon: CalendarCheck, tone: "ok" },
  { key: "exceptions", href: "/hr/recruitment/exceptions", label: "Исключения", icon: Ban, tone: "bad" },
  { key: "analytics", href: "/hr/recruitment/analytics", label: "Аналитика", icon: ChartColumn, tone: "violet" },
  { key: "hh", href: "/hr/recruitment/hh", label: "HH.uz", icon: Globe, tone: "warn", manageOnly: true },
  { key: "settings", href: "/hr/recruitment/settings", label: "Настройки", icon: Settings, tone: "neutral" },
];

const box: Record<Tone, string> = {
  accent: "bg-accent-soft text-accent-strong",
  violet: "bg-violet-soft text-violet",
  warn: "bg-warn-soft text-warn",
  ok: "bg-ok-soft text-ok",
  bad: "bg-bad-soft text-bad",
  neutral: "bg-muted text-ink-2",
};

/** Активная кнопка — сплошной акцент (без градиента тёмной темы: белый текст — на детях, а не на самой ссылке). */
const activeButton = "border-[#9DBDFF66] bg-accent shadow-[0_4px_14px_#3D7BFF55]";

export function activeSection(pathname: string): NavKey | null {
  if (pathname === "/hr/recruitment" || pathname.startsWith("/hr/recruitment/vacancies")) return "vacancies";
  const hit = SECTIONS.find((s) => s.key !== "vacancies" && (pathname === s.href || pathname.startsWith(`${s.href}/`)));
  return hit?.key ?? null;
}

/** Последние счётчики — при переходе между разделами подписи видны сразу, затем обновляются. */
let cached: NavCounters | null = null;

const REFRESH = "recruiting-nav:refresh";

/** Обновить подписи кнопок (например, вакансию поставили на паузу — «открыто» стало меньше). */
export const refreshRecruitingNav = () => window.dispatchEvent(new Event(REFRESH));

function useCounters() {
  const [counters, setCounters] = useState<NavCounters | null>(cached);
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    let alive = true;
    const load = () =>
      recruitmentApi.nav().then(
        (c) => {
          cached = c;
          if (alive) {
            setCounters(c);
            setFailed(false);
          }
        },
        () => alive && setFailed(true),
      );
    void load();
    // Вернулись на вкладку — подписи свежие.
    const onShow = () => !document.hidden && void load();
    const onRefresh = () => void load();
    document.addEventListener("visibilitychange", onShow);
    window.addEventListener(REFRESH, onRefresh);
    return () => {
      alive = false;
      document.removeEventListener("visibilitychange", onShow);
      window.removeEventListener(REFRESH, onRefresh);
    };
  }, []);
  return { counters, failed };
}

function Caption({ caption, active, failed }: { caption: NavCaption | null; active: boolean; failed: boolean }) {
  const t = useT();
  if (!caption) return failed ? null : <span aria-hidden className="mt-1 block h-2.5 w-[60px] animate-pulse rounded bg-muted" />;
  const tone = active ? "text-white/80" : caption.tone === "warn" ? "text-warn" : caption.tone === "zero" ? "text-ink-4" : "text-ink-3";
  return <span className={`block truncate text-[11px] font-medium leading-[1.3] ${tone}`}>{t(caption.key, caption.vars)}</span>;
}

export function RecruitingNav({ canManage, className }: { canManage: boolean; className?: string }) {
  const t = useT();
  const pathname = usePathname();
  const current = activeSection(pathname);
  const { counters, failed } = useCounters();
  const sections = SECTIONS.filter((s) => !s.manageOnly || canManage);

  // Ряд на компьютере: затухание справа, пока есть что прокрутить; активная кнопка — в зоне видимости.
  const row = useRef<HTMLUListElement>(null);
  const [more, setMore] = useState(false);
  useLayoutEffect(() => {
    const el = row.current;
    if (!el) return;
    const update = () => setMore(el.scrollLeft + el.clientWidth < el.scrollWidth - 2);
    const active = el.querySelector<HTMLElement>("[aria-current=page]");
    if (active && (active.offsetLeft < el.scrollLeft || active.offsetLeft + active.offsetWidth > el.scrollLeft + el.clientWidth)) {
      el.scrollLeft = active.offsetLeft - 24;
    }
    update();
    el.addEventListener("scroll", update, { passive: true });
    const observer = new ResizeObserver(update);
    observer.observe(el);
    return () => {
      el.removeEventListener("scroll", update);
      observer.disconnect();
    };
  }, [current]);

  const label = (s: (typeof SECTIONS)[number], mobile: boolean) => {
    const caption = navCaption(s.key, counters);
    const name = t(mobile && s.mobile ? s.mobile : s.label);
    return caption ? `${name}, ${t(caption.key, caption.vars)}` : name;
  };

  return (
    <nav aria-label={t("Разделы подбора")} className={className}>
      {/* Телефон: сетка 3 × 3. */}
      <ul className="grid grid-cols-3 gap-2 md:hidden">
        {sections.map((s) => {
          const on = s.key === current;
          const badge = navBadge(s.key, counters);
          const Icon = s.icon;
          return (
            <li key={s.key} className="min-w-0">
              <Link
                href={s.href}
                aria-current={on ? "page" : undefined}
                aria-label={label(s, true)}
                className={`relative flex h-[78px] flex-col items-center justify-center gap-[7px] rounded-[14px] border px-1.5 outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2 focus-visible:ring-offset-page active:translate-y-px ${
                  on ? activeButton : "border-line-strong bg-surface active:bg-muted"
                }`}
              >
                <span aria-hidden className={`grid size-[34px] shrink-0 place-items-center rounded-[10px] ${on ? "bg-white/15 text-white" : box[s.tone]}`}>
                  <Icon className="size-[18px]" />
                </span>
                <span className={`max-w-full truncate text-[12px] font-semibold leading-tight ${on ? "text-white" : "text-ink"}`}>{t(s.mobile ?? s.label)}</span>
                {badge && (
                  <span aria-hidden className="absolute right-2 top-2 grid h-[18px] min-w-[18px] place-items-center rounded-[9px] bg-bad px-1.5 text-[10px] font-bold leading-none text-white tabular-nums">
                    {badge}
                  </span>
                )}
              </Link>
            </li>
          );
        })}
      </ul>

      {/* Планшет и компьютер: ряд кнопок; подписи — когда хватает ширины (≈ 1340 px на все девять). */}
      <div className="relative @container max-md:hidden">
        <ul
          ref={row}
          className={`no-scrollbar -mx-2 -mb-3 -mt-1 flex gap-2 overflow-x-auto px-2 pb-4 pt-1 ${more ? "[mask-image:linear-gradient(to_right,black_calc(100%_-_56px),transparent)]" : ""}`}
        >
          {sections.map((s) => {
            const on = s.key === current;
            const Icon = s.icon;
            return (
              <li key={s.key} className="shrink-0">
                <Link
                  href={s.href}
                  aria-current={on ? "page" : undefined}
                  aria-label={label(s, false)}
                  className={`group flex items-center gap-2.5 rounded-xl border py-2 pl-2 pr-3.5 outline-none transition-colors focus-visible:ring-2 focus-visible:ring-accent active:translate-y-px ${
                    on ? activeButton : "border-line-strong bg-surface shadow-[var(--sec-float-shadow)] hover:bg-muted"
                  }`}
                >
                  <span
                    aria-hidden
                    className={`grid size-[34px] shrink-0 place-items-center rounded-[10px] transition-[filter] ${on ? "bg-white/15 text-white" : `${box[s.tone]} group-hover:brightness-125`}`}
                  >
                    <Icon className="size-[17px]" />
                  </span>
                  <span className="min-w-0">
                    <span className={`block whitespace-nowrap text-[13px] font-semibold leading-[1.2] ${on ? "text-white" : "text-ink"}`}>{t(s.label)}</span>
                    <span className="hidden @min-[1340px]:block">
                      <Caption caption={navCaption(s.key, counters)} active={on} failed={failed} />
                    </span>
                  </span>
                </Link>
              </li>
            );
          })}
        </ul>
      </div>
    </nav>
  );
}
