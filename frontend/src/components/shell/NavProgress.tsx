"use client";

import { useCallback, useEffect, useRef } from "react";
import { usePathname, useSearchParams } from "next/navigation";

const START = "onebase:nav-start";

/** Переход из кода (router.push / refresh): показать ту же полоску загрузки, что и при нажатии на ссылку. */
export function startNavProgress() {
  window.dispatchEvent(new Event(START));
}

/** Полоска появляется, только если переход дольше этого: мгновенные (страница уже загружена заранее) проходят без мигания. */
const SHOW_AFTER = 120;
/** Переход так и не случился (ссылку перехватил обработчик, сервер не ответил) — полоска уходит сама. */
const GIVE_UP = 15_000;

const busy = () => !!document.querySelector('[data-app-scroll] [aria-busy="true"]');

/**
 * Полоска загрузки при переходах между страницами (globals.css, .nav-progress): нажатие на ссылку сразу приглушает её
 * (data-nav-pending), а если страница открывается дольше 120 мс — вверху экрана ползёт полоска. Готово — когда сменился адрес
 * и на странице не осталось блоков, ждущих данные (aria-busy). Заготовок страниц при переходе нет: до прихода новой страницы на экране
 * прежняя, полоска показывает, что нажатие принято.
 */
export function NavProgress() {
  const pathname = usePathname();
  const search = useSearchParams();
  const bar = useRef<HTMLDivElement>(null);
  const run = useRef<{ active: boolean; value: number; link: HTMLElement | null; timers: number[]; observer: MutationObserver | null }>({
    active: false,
    value: 0,
    link: null,
    timers: [],
    observer: null,
  });

  const clear = useCallback(() => {
    const r = run.current;
    r.timers.forEach((id) => window.clearTimeout(id));
    r.timers = [];
    r.observer?.disconnect();
    r.observer = null;
    r.link?.removeAttribute("data-nav-pending");
    r.link = null;
  }, []);

  const finish = useCallback(() => {
    const r = run.current;
    if (!r.active) return;
    r.active = false;
    clear();
    const el = bar.current;
    if (!el || el.dataset.state !== "loading") {
      if (el) el.dataset.state = "";
      return;
    }
    el.style.transform = "scaleX(1)";
    el.dataset.state = "done";
    r.timers.push(
      window.setTimeout(() => {
        el.dataset.state = "";
        el.style.transform = "";
      }, 500),
    );
  }, [clear]);

  const start = useCallback(
    (link: HTMLElement | null = null) => {
      const r = run.current;
      clear();
      r.active = true;
      r.value = 0;
      if (link) {
        r.link = link;
        link.setAttribute("data-nav-pending", "");
      }
      const el = bar.current;
      if (el) {
        el.dataset.state = "";
        el.style.transform = "";
      }
      // Ползёт быстро в начале и всё медленнее к 90 %: до конца доходит, только когда страница пришла.
      const tick = () => {
        if (!r.active || !el) return;
        r.value += (0.9 - r.value) * 0.08;
        el.style.transform = `scaleX(${r.value})`;
        r.timers.push(window.setTimeout(tick, 250));
      };
      r.timers.push(
        window.setTimeout(() => {
          if (!r.active || !el) return;
          r.value = 0.12;
          el.style.transform = `scaleX(${r.value})`;
          el.dataset.state = "loading";
          r.timers.push(window.setTimeout(tick, 250));
        }, SHOW_AFTER),
        window.setTimeout(finish, GIVE_UP),
      );
    },
    [clear, finish],
  );

  useEffect(() => {
    // iOS Safari включает :active (отклик на нажатие, globals.css) только при обработчике касаний на странице.
    const touch = () => {};
    const onClick = (e: MouseEvent) => {
      if (e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
      const link = e.target instanceof Element ? e.target.closest<HTMLAnchorElement>("a[href]") : null;
      if (!link || (link.target && link.target !== "_self") || link.hasAttribute("download")) return;
      const url = new URL(link.href, window.location.href);
      if (url.origin !== window.location.origin) return;
      // Та же страница (или только якорь) — перехода нет.
      if (url.pathname === window.location.pathname && url.search === window.location.search) return;
      start(link);
    };
    const onPop = () => start();
    const onStart = () => start();
    document.addEventListener("touchstart", touch, { passive: true });
    document.addEventListener("click", onClick, true);
    window.addEventListener("popstate", onPop);
    window.addEventListener(START, onStart);
    return () => {
      document.removeEventListener("touchstart", touch);
      document.removeEventListener("click", onClick, true);
      window.removeEventListener("popstate", onPop);
      window.removeEventListener(START, onStart);
    };
  }, [start]);

  // Адрес сменился — новая страница на экране; ждём, пока её блоки дождутся данных (aria-busy), и закрываем полоску.
  useEffect(() => {
    const r = run.current;
    if (!r.active) return;
    if (!busy()) return finish();
    const scroller = document.querySelector("[data-app-scroll]");
    if (!scroller) return finish();
    r.observer?.disconnect();
    r.observer = new MutationObserver(() => {
      if (!busy()) finish();
    });
    r.observer.observe(scroller, { subtree: true, childList: true, attributes: true, attributeFilter: ["aria-busy"] });
  }, [pathname, search, finish]);

  useEffect(() => clear, [clear]);

  return <div ref={bar} className="nav-progress" aria-hidden />;
}
