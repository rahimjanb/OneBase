"use client";

import Link from "next/link";
import { usePathname, useSearchParams } from "next/navigation";
import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import { ChevronLeft, LayoutGrid } from "lucide-react";
import { useT } from "@/i18n/client";
import { salesTabAllowed, salesTabHref, type SalesScopeInfo } from "@/lib/sales/access";
import { arrange, emptyPrefs, fit, pin, prefsKey, readPrefs, reorder, unpin, withRare, type NavPrefs } from "@/lib/module-nav";
import { periodQuery } from "@/lib/sales/query";
import { readTaskList } from "@/lib/task-list";
import { Badge, MoreButton, MoreMenu, TabContextMenu, type NavItem } from "./ModuleNavMenu";
import { AllSectionsSheet, PanelSettings } from "./ModuleNavPanels";
import { sectionOf } from "./sections";

const useIsoLayoutEffect = typeof window === "undefined" ? useEffect : useLayoutEffect;

/** Счётчики разделов из /api/work/nav, /api/procurement/nav и /api/quality/nav; признаки руководителя и согласующего. */
type WorkNav = {
  heads: boolean;
  pendingSuggestions?: number;
  needsAction?: number;
  isDirector?: boolean;
  decides?: boolean;
  awaitingApproval?: number;
  toIssue?: number;
  toReceive?: number;
  /** «Качество»: открытые проверки партий. */
  open?: number;
  /** «Производство → Задания»: выпущены и в работе. */
  orders?: number;
  /** «Аналитик»: активные критические риски (/api/analytik/nav). */
  critical?: number;
};

/** Закреплённые и порядок вкладок модуля у пользователя — в localStorage (серверных настроек пользователя в OneBase нет). */
function useNavPrefs(moduleId: string | null, userId: string | null, keys: string[]) {
  const [prefs, setPrefs] = useState<NavPrefs>(emptyPrefs);
  const known = keys.join("|");
  useEffect(() => {
    if (!moduleId || !userId) return setPrefs(emptyPrefs);
    try {
      setPrefs(readPrefs(localStorage.getItem(prefsKey(userId, moduleId)), known.split("|")));
    } catch {
      setPrefs(emptyPrefs);
    }
  }, [moduleId, userId, known]);
  const save = useCallback(
    (next: NavPrefs) => {
      setPrefs(next);
      if (!moduleId || !userId) return;
      try {
        localStorage.setItem(prefsKey(userId, moduleId), JSON.stringify(next));
      } catch {
        // Хранилище недоступно (приватный режим) — настройки живут до перезагрузки.
      }
    },
    [moduleId, userId],
  );
  return [prefs, save] as const;
}

/**
 * Навигация модуля в верхней панели — «Продажи», HR, «Задачи», «Заявки», «Снабжение» (разделы — sections.ts, права и логика разделов —
 * там же). Компьютер (от 1024 px): «‹ к отделам», название модуля и вкладки; что не помещается — в «Ещё» (закреплённые — первыми, открытый
 * раздел из меню показывает сама кнопка, вкладки не перескакивают). Ширины вкладок меряются в скрытом ряду; пересчёт — при изменении
 * ширины, языка, шрифтов, прав и закреплений. Телефон и планшет: строка чипов с прокруткой и кнопка «Все разделы» — шторка с сеткой и
 * поиском. Нажатая вкладка подсвечивается сразу, а не после загрузки страницы.
 */
export function ModuleNav({ permissions, userId, salesScope = null }: { permissions: string[] | null; userId: string | null; salesScope?: SalesScopeInfo | null }) {
  const t = useT();
  const pathname = usePathname();
  const params = useSearchParams();
  const section = sectionOf(pathname);
  const sectionId = section?.id ?? null;

  // Нажатая вкладка — пока адрес не сменился; после перехода подсветку определяет адрес.
  const [pending, setPending] = useState<{ tab: string; from: string } | null>(null);
  // Карточка задачи — подсвечена вкладка списка, из которого её открыли (канбан, мои…); известна только в браузере.
  const [origin, setOrigin] = useState<string | null>(null);
  useEffect(() => setOrigin(pathname.startsWith("/tasks/task/") ? (readTaskList()?.tab ?? null) : null), [pathname]);
  const current = origin ?? section?.active(pathname) ?? null;
  const active = pending && pending.from === pathname ? pending.tab : current;

  const [work, setWork] = useState<WorkNav | null>(null);
  useEffect(() => {
    const url =
      sectionId === "tasks"
        ? "/bff/api/work/nav"
        : sectionId === "requests" || sectionId === "procurement"
          ? "/bff/api/procurement/nav"
          : sectionId === "production"
            ? "/bff/api/production/nav"
            : sectionId === "analytik"
              ? "/bff/api/analytik/nav"
              : null;
    if (!url) return;
    let live = true;
    fetch(url)
      .then((r) => (r.ok ? r.json() : null))
      .then((data) => live && data && setWork(data))
      .catch(() => undefined);
    return () => {
      live = false;
    };
  }, [sectionId, pathname]);

  // «Продажи»: период и план переносятся между разделами — тем же правилом, что ссылки внутри страниц.
  const query = section?.keepPeriod ? periodQuery(Object.fromEntries(params)) : "";
  const allowed = useMemo(
    () =>
      (section?.tabs ?? []).filter(
        // «Согласования» — тем, кто решает шаг маршрута заявок, и директору; «Предложения AI», «Заявки отдела» — руководителям.
        (tab) =>
          (!tab.permission || (Array.isArray(tab.permission) ? tab.permission : [tab.permission]).some((p) => permissions?.includes(p))) &&
          (!tab.headsOnly || work?.heads) &&
          (!tab.approverOnly || work?.decides || work?.isDirector) &&
          // «Продажи» с охватом РМ / СВР / ТП: только вкладки своей части оргструктуры, «Вторичка» — своя страница.
          (sectionId !== "sales" || salesTabAllowed(tab.key, salesScope)),
      ).map((tab) => (sectionId === "sales" ? { ...tab, href: salesTabHref(tab.key, tab.href, salesScope) } : tab)),
    [section, sectionId, permissions, work, salesScope],
  );
  const badgeOf = useCallback(
    (key: string): number | undefined => {
      const n =
        sectionId === "tasks" && key === "suggestions"
          ? work?.pendingSuggestions
          : sectionId === "requests" && key === "approvals"
            ? work?.awaitingApproval
            : sectionId === "requests" && key === "overview"
              ? work?.needsAction
              : sectionId === "procurement" && key === "issue"
                ? work?.toIssue
                : sectionId === "procurement" && key === "receipts"
                  ? work?.toReceive
                  : sectionId === "production" && key === "quality"
                    ? work?.open
                    : sectionId === "production" && key === "orders"
                      ? work?.orders
                      : sectionId === "analytik" && key === "risks"
                        ? work?.critical
                        : undefined;
      return n && n > 0 ? n : undefined;
    },
    [sectionId, work],
  );

  const [prefs, savePrefs] = useNavPrefs(sectionId, userId, (section?.tabs ?? []).map((tab) => tab.key));
  // Редкие разделы (rare) — в конце и всегда в «Ещё», если пользователь их не закрепил; regular — сколько обычных.
  const { items, regular } = useMemo(() => {
    const byKey = new Map(allowed.map((tab) => [tab.key, tab]));
    const order = withRare(
      arrange(
        allowed.map((tab) => tab.key),
        prefs,
      ),
      allowed.filter((tab) => tab.rare).map((tab) => tab.key),
      prefs,
    );
    const list: NavItem[] = order.keys.map((key) => {
      const tab = byKey.get(key)!;
      return {
        key,
        label: t(tab.label),
        short: tab.short ? t(tab.short) : undefined,
        description: tab.description ? t(tab.description) : undefined,
        href: query ? `${tab.href}?${query}` : tab.href,
        icon: tab.icon,
        badge: badgeOf(key),
        badgeTone: sectionId === "analytik" && key === "risks" ? ("danger" as const) : undefined,
      };
    });
    return { items: list, regular: order.regular };
  }, [allowed, prefs, query, badgeOf, t, sectionId]);
  const activeItem = items.find((i) => i.key === active) ?? null;

  // ---------- Компьютер: сколько вкладок помещается ----------
  const zone = useRef<HTMLElement>(null);
  const meter = useRef<HTMLDivElement>(null);
  const [count, setCount] = useState<number | null>(null);
  const signature = items.map((i) => `${i.key}:${i.label}:${i.badge ?? ""}`).join("|") + `|${active}`;
  const measure = useCallback(() => {
    const box = zone.current;
    const row = meter.current;
    if (!box || !row || box.clientWidth === 0) return;
    const cells = Array.from(row.children) as HTMLElement[];
    const widths = cells.slice(0, items.length).map((c) => c.offsetWidth);
    const more = cells[items.length]?.offsetWidth ?? 96;
    const moreActive = cells[items.length + 1]?.offsetWidth ?? more;
    const gap = parseFloat(getComputedStyle(box).columnGap) || 4;
    // Редкие разделы всегда в «Ещё»: в строку — только обычные, место под «Ещё» — всегда.
    const rare = regular < items.length;
    const own = widths.slice(0, regular);
    let n = fit(own, box.clientWidth, more, gap, rare);
    // Открытый раздел не помещается — кнопка «Ещё» покажет его название, она шире: пересчёт с её шириной.
    const at = items.findIndex((i) => i.key === active);
    if (n < items.length && at >= n && moreActive > more) n = fit(own, box.clientWidth, moreActive, gap, rare);
    setCount(n);
  }, [items, active, regular]);

  useIsoLayoutEffect(() => measure(), [signature, measure]);
  useEffect(() => {
    const box = zone.current;
    if (!box) return;
    let frame = 0;
    const observer = new ResizeObserver(() => {
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(() => measure());
    });
    observer.observe(box);
    document.fonts?.ready.then(() => measure()).catch(() => undefined);
    return () => {
      cancelAnimationFrame(frame);
      observer.disconnect();
    };
  }, [measure, sectionId]);

  // Имя в меню пользователя прячется (остаётся аватар): место — вкладкам; так и в макете модуля.
  useEffect(() => {
    const topbar = document.querySelector<HTMLElement>("[data-topbar]");
    if (!section) return;
    topbar?.setAttribute("data-crowded", "");
    return () => topbar?.removeAttribute("data-crowded");
  }, [section]);

  const [menu, setMenu] = useState<{ keyboard: boolean } | null>(null);
  const [settings, setSettings] = useState(false);
  const [context, setContext] = useState<{ key: string; x: number; y: number } | null>(null);
  const [sheet, setSheet] = useState(false);
  const moreRef = useRef<HTMLButtonElement>(null);
  const closeMenu = useCallback((returnFocus: boolean) => {
    setMenu(null);
    if (returnFocus) moreRef.current?.focus();
  }, []);
  useEffect(() => {
    setMenu(null);
    setSheet(false);
    setContext(null);
  }, [pathname]);
  const togglePin = useCallback((key: string, on: boolean) => savePrefs(on ? pin(prefs, key) : unpin(prefs, key)), [prefs, savePrefs]);

  // ---------- Телефон: открытый чип — в видимой части строки ----------
  const chips = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const el = chips.current;
    const link = el?.querySelector<HTMLElement>('[aria-current="page"]');
    if (!el || !link) return;
    // Только по горизонтали внутри ряда: scrollIntoView сдвинул бы и страницу.
    if (link.offsetLeft < el.scrollLeft || link.offsetLeft + link.offsetWidth > el.scrollLeft + el.clientWidth - 60) {
      el.scrollTo({ left: link.offsetLeft - (el.clientWidth - 60 - link.offsetWidth) / 2, behavior: "smooth" });
    }
  }, [current, sheet]);

  if (!section) return null;
  const shown = count === null ? items : items.slice(0, count);
  const hidden = count === null ? [] : items.slice(count);
  const activeHidden = activeItem && hidden.some((i) => i.key === activeItem.key) ? activeItem : null;
  // Тревога у скрытых разделов (критические риски Аналитика) — красным счётчиком на «Ещё».
  const alert = hidden.filter((i) => i.badgeTone === "danger" && i.key !== activeHidden?.key).reduce((sum, i) => sum + (i.badge ?? 0), 0);
  const press = (key: string) => (e: React.MouseEvent) => {
    if (e.button === 0 && !e.metaKey && !e.ctrlKey && !e.shiftKey && !e.altKey) setPending({ tab: key, from: pathname });
  };
  const title = t(section.title);

  // Вкладка; для замера — та же разметка без ссылки (активная — жирнее, поэтому замер — по жирной: место есть всегда).
  const tabClass = (on: boolean) =>
    `inline-flex h-[34px] shrink-0 items-center gap-1.5 whitespace-nowrap rounded-[9px] px-[11px] text-[13px] transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent/60 ${
      on ? "bg-accent font-semibold text-white shadow-[0_4px_12px_rgb(61_123_255/0.3)] hover:bg-accent-strong" : "font-medium text-ink-2 hover:bg-muted hover:text-ink"
    }`;
  const tabBody = (item: NavItem, on: boolean) => (
    <>
      <item.icon aria-hidden className="size-[15px] shrink-0" strokeWidth={1.9} />
      {item.label}
      {item.badge ? <Badge value={item.badge} on={on} tone={item.badgeTone} /> : null}
    </>
  );
  const tab = (item: NavItem) => {
    const on = active === item.key;
    return (
      <Link
        key={item.key}
        href={item.href}
        aria-current={current === item.key ? "page" : undefined}
        onClick={press(item.key)}
        onContextMenu={(e) => {
          e.preventDefault();
          setContext({ key: item.key, x: e.clientX, y: e.clientY });
        }}
        title={prefs.pins.includes(item.key) ? t("Закреплено") : undefined}
        className={tabClass(on)}
      >
        {tabBody(item, on)}
      </Link>
    );
  };

  return (
    <>
      {/* Компьютер: «‹», название модуля, вкладки и «Ещё». */}
      <div className="hidden min-w-0 flex-1 items-center gap-3 lg:flex">
        {section.back && (
          <Link
            href={section.back.href}
            aria-label={t(section.back.label)}
            title={t(section.back.label)}
            className="grid size-[30px] shrink-0 place-items-center rounded-[9px] border border-line bg-surface text-ink-2 transition-colors hover:bg-muted hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent/60"
          >
            <ChevronLeft aria-hidden className="size-4" />
          </Link>
        )}
        {section.mark && (
          <span aria-hidden className="grid size-[30px] shrink-0 place-items-center rounded-[9px] bg-[linear-gradient(135deg,#5b97ff,#8b5cf6)] text-white">
            <section.mark className="size-4" strokeWidth={2} />
          </span>
        )}
        <span className="shrink-0 whitespace-nowrap text-[15px] font-bold text-ink">{title}</span>
        <span aria-hidden className="h-6 w-px shrink-0 bg-line-strong" />
        <nav
          ref={zone}
          aria-label={t("Разделы модуля")}
          className={`relative flex min-w-0 flex-1 items-center gap-1 ${count === null ? "pointer-events-none overflow-hidden opacity-0" : ""}`}
        >
          {shown.map((item) => tab(item))}
          {hidden.length > 0 && (
            <MoreButton
              buttonRef={moreRef}
              count={activeHidden ? hidden.length - 1 : hidden.length}
              active={activeHidden}
              open={menu !== null}
              dot={hidden.some((i) => i.badge && i.key !== activeHidden?.key)}
              alert={alert || undefined}
              onClick={(keyboard) => setMenu((m) => (m ? null : { keyboard }))}
            />
          )}
          {/* Скрытый ряд для замера: все вкладки, «Ещё» и «Ещё» с названием открытого раздела. */}
          <div aria-hidden className="pointer-events-none invisible absolute left-0 top-0 h-0 w-0 overflow-hidden">
            <div ref={meter} className="flex w-max items-center gap-1">
              {items.map((item) => (
                <span key={item.key} className={tabClass(true)}>
                  {tabBody(item, true)}
                </span>
              ))}
              <MoreButton count={88} measuring />
              {activeItem ? <MoreButton count={88} active={activeItem} measuring /> : <span />}
            </div>
          </div>
        </nav>
      </div>

      {/* Телефон и планшет: чипы с прокруткой и «Все разделы». */}
      {/* min-w-0: иначе обёртка растягивается на ширину всех чипов — строка не прокручивается, кнопка «Все разделы» уезжает за экран. */}
      <div className="relative order-last min-w-0 basis-full lg:hidden">
        <nav ref={chips} className="no-scrollbar flex gap-1.5 overflow-x-auto pb-2 pr-[60px] pt-1" aria-label={t("Разделы модуля")}>
          {items.map((item) => {
            const on = active === item.key;
            return (
              <Link
                key={item.key}
                href={item.href}
                aria-current={current === item.key ? "page" : undefined}
                onClick={press(item.key)}
                className={`inline-flex h-[34px] shrink-0 items-center gap-1.5 whitespace-nowrap rounded-[18px] border px-3 text-[13px] transition-colors ${
                  on ? "border-accent bg-accent font-semibold text-white" : "border-line bg-surface text-ink-2"
                }`}
              >
                <item.icon aria-hidden className="size-3.5 shrink-0" strokeWidth={1.9} />
                {item.short ?? item.label}
                {item.badge ? <Badge value={item.badge} on={on} tone={item.badgeTone} /> : null}
              </Link>
            );
          })}
        </nav>
        <div aria-hidden className="pointer-events-none absolute inset-y-0 right-0 w-[100px] bg-gradient-to-r from-transparent to-sidebar" />
        <button
          type="button"
          onClick={() => setSheet(true)}
          aria-haspopup="dialog"
          aria-label={t("Все разделы")}
          className="absolute right-0 top-1 grid h-[34px] w-[38px] place-items-center rounded-full border border-accent/50 bg-muted text-accent-strong"
        >
          <LayoutGrid aria-hidden className="size-4" strokeWidth={1.9} />
        </button>
      </div>

      {menu && moreRef.current && (
        <MoreMenu
          anchor={moreRef.current}
          hidden={hidden}
          all={items}
          activeKey={current}
          pins={prefs.pins}
          focusSearch={menu.keyboard}
          onPin={togglePin}
          onSettings={() => {
            setMenu(null);
            setSettings(true);
          }}
          onClose={closeMenu}
        />
      )}
      {context && (
        <TabContextMenu
          x={context.x}
          y={context.y}
          pinned={prefs.pins.includes(context.key)}
          onPin={() => togglePin(context.key, !prefs.pins.includes(context.key))}
          onSettings={() => setSettings(true)}
          onClose={() => setContext(null)}
        />
      )}
      <PanelSettings
        open={settings}
        title={title}
        items={items}
        pins={prefs.pins}
        onOrder={(keys) => savePrefs(reorder(prefs, keys))}
        onPin={togglePin}
        onReset={() => savePrefs(emptyPrefs)}
        onClose={() => setSettings(false)}
      />
      {sheet && <AllSectionsSheet title={title} items={items} activeKey={current} onClose={() => setSheet(false)} />}
    </>
  );
}
