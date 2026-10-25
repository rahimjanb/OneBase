"use client";

import Link from "next/link";
import { usePathname, useSearchParams } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { periodQuery } from "@/lib/sales/query";
import { sectionOf } from "./sections";
import { useT } from "@/i18n/client";

/**
 * Кнопки разделов в верхней панели — в «Продажах», «HR» и «Задачах» (sections.ts); в других разделах панель без них.
 * На компьютере — в одной строке с меню пользователя; не помещаются — ряд прокручивается по горизонтали, у обрезанного края — затухание,
 * открытый раздел сам прокручивается в видимую часть. На телефоне — отдельной строкой под логотипом, так же с прокруткой.
 * Нажатая кнопка подсвечивается сразу, а не после загрузки страницы: иначе ещё секунду горит прежний раздел.
 */
export function SectionNav({ permissions }: { permissions: string[] | null }) {
  const tr = useT();
  const pathname = usePathname();
  const params = useSearchParams();
  // Нажатая кнопка — пока адрес не сменился; после перехода подсветку определяет адрес.
  const [pending, setPending] = useState<{ tab: string; from: string } | null>(null);
  const nav = useRef<HTMLElement>(null);
  const measure = useRef<() => void>(null);
  // Ширина имени в меню пользователя — запоминается, пока имя видно.
  const nameWidth = useRef(0);
  // Есть ли кнопки за левым и правым краем — для затухания края; состояние меняется только при смене (иначе замер в эффекте зациклится).
  const [edges, setEdges] = useState({ left: false, right: false });
  const edgesNow = useRef(edges);

  const section = sectionOf(pathname);
  const sectionId = section?.id ?? null;
  const current = section?.active(pathname) ?? null;
  // «Задачи»: вкладка «Предложения AI» — руководителям, со счётчиком ожидающих решения (обновляется при переходах внутри раздела).
  const [work, setWork] = useState<{ heads: boolean; pendingSuggestions: number } | null>(null);
  useEffect(() => {
    if (sectionId !== "tasks") return;
    let live = true;
    fetch("/bff/api/work/nav")
      .then((r) => (r.ok ? r.json() : null))
      .then((data) => live && data && setWork(data))
      .catch(() => undefined);
    return () => {
      live = false;
    };
  }, [sectionId, pathname]);
  const tabs = (section?.tabs ?? []).filter((t) => (!t.permission || permissions?.includes(t.permission)) && (!t.headsOnly || work?.heads));
  // Подписи (язык, права) задают ширину ряда — по ним пересчёт.
  const labels = tabs.map((t) => tr(t.label)).join("|");

  useEffect(() => {
    const el = nav.current;
    const topbar = el?.closest<HTMLElement>("[data-topbar]") ?? document.querySelector<HTMLElement>("[data-topbar]");
    if (!el) {
      topbar?.removeAttribute("data-crowded");
      return;
    }
    const update = () => {
      // Тесно (data-crowded) — кнопки разделов не помещаются рядом с меню пользователя вместе с именем: имя прячется, остаётся аватар.
      // Решение не зависит от того, спрятано ли имя сейчас (ширина имени запомнена), — поэтому не мигает.
      const bar = el.parentElement;
      const right = el.nextElementSibling as HTMLElement | null;
      const name = right?.querySelector<HTMLElement>("[data-user-name]");
      const nameVisible = !!name && getComputedStyle(name).display !== "none";
      if (name && nameVisible) nameWidth.current = name.offsetWidth;
      if (bar && right && getComputedStyle(bar).flexWrap === "nowrap") {
        const style = getComputedStyle(bar);
        const inner = bar.clientWidth - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight);
        const rightFull = right.offsetWidth + (nameVisible ? 0 : nameWidth.current + 12);
        topbar?.toggleAttribute("data-crowded", el.scrollWidth + parseFloat(style.columnGap) + rightFull > inner);
      } else {
        topbar?.removeAttribute("data-crowded");
      }
      const left = el.scrollLeft > 2;
      const rightEdge = el.scrollLeft + el.clientWidth < el.scrollWidth - 2;
      if (edgesNow.current.left !== left || edgesNow.current.right !== rightEdge) {
        edgesNow.current = { left, right: rightEdge };
        setEdges(edgesNow.current);
      }
    };
    // Колесо мыши над рядом, который не помещается, прокручивает его вбок — иначе скрытые кнопки достаются только тачпадом.
    const wheel = (e: WheelEvent) => {
      if (el.scrollWidth > el.clientWidth && Math.abs(e.deltaY) > Math.abs(e.deltaX)) {
        el.scrollLeft += e.deltaY;
        e.preventDefault();
      }
    };
    measure.current = update;
    update();
    el.addEventListener("scroll", update, { passive: true });
    el.addEventListener("wheel", wheel, { passive: false });
    const observer = new ResizeObserver(update);
    observer.observe(el);
    if (el.parentElement) observer.observe(el.parentElement);
    return () => {
      el.removeEventListener("scroll", update);
      el.removeEventListener("wheel", wheel);
      observer.disconnect();
      topbar?.removeAttribute("data-crowded");
    };
  }, [sectionId]);

  useEffect(() => measure.current?.(), [labels]);

  useEffect(() => {
    const el = nav.current;
    const link = el?.querySelector<HTMLElement>('[aria-current="page"]');
    if (!el || !link) return;
    // Только по горизонтали внутри ряда: scrollIntoView сдвинул бы и страницу.
    if (link.offsetLeft < el.scrollLeft || link.offsetLeft + link.offsetWidth > el.scrollLeft + el.clientWidth) {
      el.scrollTo({ left: link.offsetLeft - (el.clientWidth - link.offsetWidth) / 2, behavior: "smooth" });
    }
  }, [current]);

  if (!section) return null;
  const active = pending && pending.from === pathname ? pending.tab : current;

  // «Продажи»: период и план переносятся между разделами — тем же правилом, что ссылки внутри страниц (plan — только factory).
  const query = section.keepPeriod ? periodQuery(Object.fromEntries(params)) : "";

  const fade = 28;
  const mask =
    edges.left || edges.right
      ? `linear-gradient(to right, ${edges.left ? `transparent 0, #000 ${fade}px` : "#000 0"}, ${edges.right ? `#000 calc(100% - ${fade}px), transparent 100%` : "#000 100%"})`
      : undefined;

  return (
    <nav
      ref={nav}
      data-sections
      style={{ maskImage: mask, WebkitMaskImage: mask }}
      className="no-scrollbar order-last flex basis-full gap-2 -mx-1 overflow-x-auto px-1 py-1 pb-3 lg:order-none lg:min-w-0 lg:shrink lg:basis-auto lg:gap-1.5 lg:pb-1"
      aria-label={tr(section.label)}
    >
      {tabs.map((t) => {
        const on = active === t.key;
        return (
          <Link
            key={t.key}
            href={query ? `${t.href}?${query}` : t.href}
            aria-current={current === t.key ? "page" : undefined}
            onClick={(e) => {
              if (e.button === 0 && !e.metaKey && !e.ctrlKey && !e.shiftKey && !e.altKey) setPending({ tab: t.key, from: pathname });
            }}
            className={`inline-flex h-8 shrink-0 items-center gap-1.5 whitespace-nowrap rounded-[10px] border px-3 text-[12.5px] backdrop-blur-md transition-colors max-lg:h-10 max-lg:text-sm lg:gap-1 lg:px-2 lg:text-[12px] ${
              on ? "border-accent bg-accent font-medium text-white hover:bg-accent-strong" : "border-line bg-surface text-ink-2 hover:border-accent/50 hover:bg-muted hover:text-ink"
            }`}
          >
            <t.icon className="size-3.5 shrink-0" strokeWidth={1.75} />
            {tr(t.label)}
            {t.key === "suggestions" && sectionId === "tasks" && (work?.pendingSuggestions ?? 0) > 0 && (
              <span className={`rounded-full px-1.5 text-[11px] font-semibold ${on ? "bg-white/25 text-white" : "bg-violet/20 text-violet-light"}`}>{work?.pendingSuggestions}</span>
            )}
          </Link>
        );
      })}
    </nav>
  );
}
