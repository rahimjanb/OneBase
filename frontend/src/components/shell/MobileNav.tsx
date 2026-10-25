"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useCallback, useEffect, useMemo, useRef, useState, type CSSProperties } from "react";
import {
  ChartNoAxesColumn,
  ClipboardList,
  Database,
  DatabaseZap,
  Grid3x3,
  House,
  LayoutGrid,
  LogOut,
  MessageSquarePlus,
  X,
  Send,
  SlidersHorizontal,
  UsersRound,
  Sparkles,
  UserRound,
  type LucideIcon,
} from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { LinkTelegramButton } from "@/components/telegram/LinkTelegramButton";
import { useT } from "@/i18n/client";
import { canOpen } from "@/lib/navigation";
import { initials, type Me } from "@/lib/users";
import { LogoutLink } from "./AppBridge";
import { useKeyboardOpen } from "./AppScroll";
import { fanLayout, type FanLayout } from "./fan";

/** short — подпись вкладки на узком экране (уже 375px), где полная не помещается. */
type Section = { href: string; label: string; short?: string; icon: LucideIcon; exact?: boolean; also?: string[] };
type FanItem = Section & { color: string; action?: boolean };

const home: Section = { href: "/", label: "Главная", icon: House, exact: true };
const consultant: Section = { href: "/consultant", label: "Консультант", short: "AI", icon: Sparkles, also: ["/ai"] };
const sales: Section = { href: "/sales", label: "Продажи", icon: ChartNoAxesColumn };
const tasksHref = "/tasks";

/** Веер слева направо: разделы, которых нет во вкладках, и создание рассылки. Цвета иконок — умеренные, у каждого пункта свой. */
const fanSections: (FanItem & { allowed: (p: readonly string[]) => boolean })[] = [
  { href: "/departments", label: "Отделы", icon: Grid3x3, color: "#22b573", allowed: (p) => canOpen("/departments", p) },
  { href: "/base", label: "Файлы", icon: Database, color: "#ff8a00", also: ["/files"], allowed: (p) => canOpen("/base", p) },
  { href: "/data-manager", label: "Данные", icon: DatabaseZap, color: "#14b8a6", allowed: (p) => canOpen("/data-manager", p) },
  { href: tasksHref, label: "Задачи", icon: ClipboardList, color: "#8b5cf6", allowed: (p) => canOpen(tasksHref, p) },
  { href: "/hr", label: "HR", icon: UsersRound, color: "#06b6d4", allowed: (p) => canOpen("/hr", p) },
  { href: "/campaigns", label: "Рассылки", icon: Send, color: "#0878ff", allowed: (p) => canOpen("/campaigns", p) },
  { href: "/campaigns?new=1", label: "Новая рассылка", icon: MessageSquarePlus, color: "#f43f5e", action: true, allowed: (p) => canOpen("/campaigns", p) },
];
const settingsItem: FanItem = { href: "/settings", label: "Настройки", icon: SlidersHorizontal, color: "#64748b" };

const isCurrent = (pathname: string, s: Section) => {
  const under = (href: string) => pathname === href || pathname.startsWith(`${href}/`);
  return s.exact ? pathname === s.href : under(s.href) || (s.also ?? []).some(under);
};

/**
 * Нижняя панель на телефоне, как в нативном приложении: стеклянная панель со скруглённым верхом, четыре вкладки и синяя кнопка меню
 * (четыре квадрата) по центру. Кнопка раскрывает веер круглых кнопок над панелью — остальные разделы (по правам) и «Новая рассылка»; фон затемняется
 * и размывается. Закрывается повторным нажатием, тапом по фону, Esc или выбором пункта. «Профиль» — шторка с привязкой Telegram и выходом.
 * Вкладки: «Главная», «Консультант» (AI-консультант), «Продажи»; без доступа к какой-то из них — первый доступный раздел из веера
 * («Задачи», «Отделы» …). Панель в потоке под областью
 * прокрутки — не двигается при прокрутке; при открытой клавиатуре прячется. Отступ снизу — под полоску «домой» (safe area).
 */
export function MobileNav({ me, canOpenSettings, permissions }: { me: Me | null; canOpenSettings: boolean; permissions: readonly string[] | null }) {
  const pathname = usePathname();
  const t = useT();
  const keyboardOpen = useKeyboardOpen();
  const [open, setOpen] = useState(false);
  const [profile, setProfile] = useState(false);
  const [layout, setLayout] = useState<FanLayout | null>(null);
  const barRef = useRef<HTMLDivElement>(null);
  const fabRef = useRef<HTMLButtonElement>(null);
  const firstRef = useRef<HTMLAnchorElement>(null);

  const { tabs, items } = useMemo(() => {
    // Права: null — API не ответил, меню не прячем (страницы сами проверят доступ).
    const allowed = (check: (p: readonly string[]) => boolean) => permissions === null || check(permissions);
    const candidates = [
      ...(allowed((p) => canOpen("/", p)) ? [home] : []),
      ...(allowed((p) => canOpen("/consultant", p)) ? [consultant] : []),
      ...(allowed((p) => p.includes("sales.read")) ? [sales] : []),
    ];
    const sections = fanSections.filter((s) => allowed(s.allowed));
    // Вкладок три (четвёртая — «Профиль»): недостающие берутся из веера, «Задачи» — первыми.
    const fill = [...sections.filter((s) => s.href === tasksHref), ...sections.filter((s) => !s.action && s.href !== tasksHref)];
    const tabs: Section[] = [...candidates, ...fill].slice(0, 3);
    const items: FanItem[] = [...sections.filter((s) => !tabs.includes(s)), ...(canOpenSettings ? [settingsItem] : [])];
    return { tabs, items };
  }, [permissions, canOpenSettings]);

  const measure = useCallback(() => {
    const fab = fabRef.current?.getBoundingClientRect();
    const bar = barRef.current?.getBoundingClientRect();
    if (!fab || !bar) return;
    const cx = fab.left + fab.width / 2;
    const cy = fab.top + fab.height / 2;
    setLayout(fanLayout(items.map((i) => t(i.label)), window.innerWidth, cx, cy, bar.top - cy));
  }, [items, t]);

  const close = useCallback((refocus = false) => {
    setOpen(false);
    if (refocus) fabRef.current?.focus({ preventScroll: true });
  }, []);

  const toggle = () => {
    if (open) return close();
    measure();
    setOpen(true);
  };

  // Открыто: Esc закрывает, фон не прокручивается, раскладка следует за поворотом и размером экрана, фокус — на первый пункт.
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && close(true);
    const target = document.querySelector<HTMLElement>("[data-app-scroll]");
    const overflow = target?.style.overflow ?? "";
    if (target) target.style.overflow = "hidden";
    document.addEventListener("keydown", onKey);
    window.addEventListener("resize", measure);
    const focus = requestAnimationFrame(() => firstRef.current?.focus({ preventScroll: true }));
    return () => {
      cancelAnimationFrame(focus);
      document.removeEventListener("keydown", onKey);
      window.removeEventListener("resize", measure);
      if (target) target.style.overflow = overflow;
    };
  }, [open, close, measure]);

  const size = layout?.size ?? 62;
  const n = items.length;

  return (
    <nav
      aria-label={t("Основные разделы")}
      data-open={open || undefined}
      className={`rnav shrink-0 px-1.5 lg:hidden ${keyboardOpen ? "hidden" : ""}`}
    >
      <div className="rnav-layer" aria-hidden="true">
        <div className="rnav-overlay" onClick={() => close()} />
      </div>

      <div id="rnav-fan" className="rnav-anchor" role="group" aria-label={t("Разделы и действия")} inert={!open}>
        {items.map((item, i) => {
          const slot = layout?.slots[i] ?? { x: 0, y: 0 };
          const delay = open ? i * 32 : (n - 1 - i) * 22;
          const length = Math.max(0, Math.hypot(slot.x, slot.y) - size / 2 + 6);
          const angle = (Math.atan2(slot.x, -slot.y) * 180) / Math.PI;
          return (
            <span
              key={`beam-${item.href}`}
              className="rnav-beam"
              style={{ "--bw": `${Math.round(size * 0.95)}px`, "--len": `${length}px`, "--a": `${angle}deg`, "--d": `${delay}ms` } as CSSProperties}
            />
          );
        })}
        {items.map((item, i) => {
          const slot = layout?.slots[i] ?? { x: 0, y: 0 };
          const delay = open ? i * 32 : (n - 1 - i) * 22;
          const current = !item.action && isCurrent(pathname, item);
          const Icon = item.icon;
          return (
            <Link
              key={item.href}
              ref={i === 0 ? firstRef : undefined}
              href={item.href}
              onClick={() => close()}
              aria-current={current ? "page" : undefined}
              tabIndex={open ? undefined : -1}
              className="rnav-item"
              style={{ "--x": `${slot.x}px`, "--y": `${slot.y}px`, "--s": `${size}px`, "--d": `${delay}ms`, "--c": item.color } as CSSProperties}
            >
              <span className="rnav-btn">
                <Icon className={size < 60 ? "size-[22px]" : "size-6"} strokeWidth={2} />
              </span>
              <span className="rnav-label">{t(item.label)}</span>
            </Link>
          );
        })}
      </div>

      <div ref={barRef} className="rnav-bar pb-[max(calc(env(safe-area-inset-bottom)-10px),6px)]">
        <ul className="grid h-[74px] grid-cols-5 px-1">
          {[tabs[0], tabs[1], null, tabs[2]].map((tab, i) =>
            i === 2 ? (
              <li key="fab" aria-hidden="true" />
            ) : (
              <li key={tab?.href ?? `empty-${i}`} className="min-w-0">
                {tab && <TabLink tab={tab} active={isCurrent(pathname, tab)} />}
              </li>
            ),
          )}
          <li className="min-w-0">
            <button
              type="button"
              onClick={() => setProfile(true)}
              aria-haspopup="dialog"
              aria-expanded={profile}
              className="rnav-tab flex h-full w-full flex-col items-center justify-center gap-1 text-[11px] font-medium"
            >
              <UserRound className="size-6" strokeWidth={profile ? 2.2 : 1.8} />
              <span className="max-w-full truncate px-0.5">{t("Профиль")}</span>
            </button>
          </li>
        </ul>
      </div>

      <div className="rnav-halo" aria-hidden="true" />
      <button
        ref={fabRef}
        type="button"
        onClick={toggle}
        aria-label={open ? t("Закрыть меню") : t("Открыть меню разделов")}
        aria-expanded={open}
        aria-controls="rnav-fan"
        className="rnav-fab"
      >
        <LayoutGrid className="rnav-fab-menu size-7" fill="currentColor" strokeWidth={1.6} aria-hidden />
        <X className="rnav-fab-close size-7" strokeWidth={2.4} aria-hidden />
      </button>

      <Sheet open={profile} onClose={() => setProfile(false)} title={t("Профиль")}>
        {me && (
          <div className="flex items-center gap-3 rounded-xl bg-muted px-3 py-3">
            <span className="grid size-11 shrink-0 place-items-center rounded-full bg-accent-soft text-sm font-semibold text-accent-strong">{initials(me)}</span>
            <span className="min-w-0">
              <span className="block truncate font-medium text-ink">{me.name || me.login}</span>
              <span className="block truncate text-xs text-ink-3">{me.position ?? me.roles[0] ?? me.login}</span>
            </span>
          </div>
        )}
        <div className="mt-3 rounded-xl border border-line p-1.5 text-ink">
          {me && <LinkTelegramButton />}
          {/* Обычная ссылка: /logout при загрузке закрывает сеанс, заранее его загружать нельзя. */}
          <LogoutLink href="/logout" className="flex items-center gap-2 rounded-md px-2.5 py-2.5 text-sm text-bad hover:bg-muted">
            <LogOut className="size-4" />
            {t("Выйти")}
          </LogoutLink>
        </div>
      </Sheet>
    </nav>
  );
}

function TabLink({ tab, active }: { tab: Section; active: boolean }) {
  const t = useT();
  const Icon = tab.icon;
  return (
    <Link
      href={tab.href}
      aria-current={active ? "page" : undefined}
      className="rnav-tab flex h-full flex-col items-center justify-center gap-1 text-[11px] font-medium"
    >
      <Icon className="size-6" strokeWidth={active ? 2.2 : 1.8} fill={active ? "currentColor" : "none"} fillOpacity={active ? 0.16 : 0} />
      {tab.short ? (
        <>
          <span className="max-w-full truncate tracking-[-0.02em] max-[374px]:hidden">{t(tab.label)}</span>
          <span className="max-w-full truncate px-0.5 min-[375px]:hidden">{t(tab.short)}</span>
        </>
      ) : (
        <span className="max-w-full truncate px-0.5">{t(tab.label)}</span>
      )}
    </Link>
  );
}
